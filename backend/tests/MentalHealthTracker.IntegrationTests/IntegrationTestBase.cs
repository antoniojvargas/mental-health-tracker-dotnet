using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Api.Modules.DailyLog.Dtos;
using MentalHealthTracker.Domain.Entities;
using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.Repositories;
using MentalHealthTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.IntegrationTests;

// Base común de las pruebas de integración: vacía las tablas de la base de datos de test
// antes de cada prueba para que todas partan de un estado conocido, en lugar de depender
// de los datos que dejó la anterior. Los datos se aíslan hoy por emails únicos, lo que
// funciona pero no protege contra pruebas que lean o escriban en filas ajenas.
//
// Por qué TRUNCATE y no drop/recreate: recrear el esquema por prueba reaplicaría las
// migraciones en cada una, convirtiendo la suite en una serie de migraciones en lugar de
// una prueba. TRUNCATE borra filas sin tocar el esquema, y el CASCADE resuelve la FK
// daily_logs -> users sin depender del orden en que se listen las tablas.
//
// Solo limpia en InitializeAsync: cada prueba puede dejar la base como quiera porque la
// siguiente la vacía igual al arrancar. Así una prueba que falle a la mitad, o que ni
// llegue a DisposeAsync, no puede arrastrar basura a la siguiente.
//
// La factory llega por constructor: la comparte toda la colección IntegrationTestGroup.Name
// (ICollectionFixture, un arranque y una migración por ejecución) o, en la colección
// aislada, la trae la propia clase como IClassFixture. Se hereda con
// `: IntegrationTestBase(factory)`.
public abstract class IntegrationTestBase(CustomWebApplicationFactory factory) : IAsyncLifetime
{
    protected CustomWebApplicationFactory Factory { get; } = factory;

    public async Task InitializeAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE users, daily_logs CASCADE");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // Inicia sesión por HTTP contra /api/auth/test-login y devuelve un HttpClient que ya
    // envía la cookie de sesión, listo para usar sin cabeceras manuales.
    protected async Task<HttpClient> CreateAuthenticatedClientAsync(
        string email,
        string name = "Integration Test User")
    {
        var (client, _, _) = await CreateAuthenticatedSessionAsync(email, name);
        return client;
    }

    // Igual que el anterior pero además devuelve el User y el token JWT en crudo, que
    // hacen falta en dos sitios: las pruebas que afirman sobre el usuario o siembran
    // registros con user.Id, y el handshake de SignalR, que exige el token como cabecera
    // Cookie porque no pasa por el HttpClient.
    //
    // El token se sacan del Set-Cookie de la respuesta y se reinyecta con un
    // DelegatingHandler, en vez de confiar en HandleCookies: WebApplicationFactoryClientOptions
    // no expone el CookieContainer, así que con HandleCookies la cookie quedaría encerrada
    // en un contenedor interno del que no se puede leer el token.
    protected async Task<(HttpClient Client, User User, string Token)> CreateAuthenticatedSessionAsync(
        string email,
        string name = "Integration Test User")
    {
        var loginClient = Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        using var response = await loginClient.PostAsJsonAsync("/api/auth/test-login", new { email, name });

        if (response.StatusCode != HttpStatusCode.NoContent)
        {
            throw new InvalidOperationException(
                $"/api/auth/test-login devolvió {(int)response.StatusCode} en vez de 204.");
        }

        var token = ReadSessionToken(response);

        // /api/auth/test-login no devuelve el usuario en el body, e IUserRepository no
        // tiene búsqueda por email. Se repite el mismo upsert por GoogleId que hace el
        // endpoint ("e2e-{email}"): como el upsert es idempotente sobre GoogleId,
        // devuelve el usuario que el login acaba de crear, no uno nuevo.
        await using var scope = Factory.Services.CreateAsyncScope();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await userRepository.UpsertByGoogleIdAsync(new GoogleProfile($"e2e-{email}", email, name, null));

        var client = Factory.CreateDefaultClient(new SessionCookieHandler(token));

        return (client, user, token);
    }

    // Las opciones de JSON de la propia API (las de MVC: camelCase y enums en snake_case). Se
    // resuelven de la factory para que las peticiones de test viajen exactamente como las
    // serializa y deserializa la aplicación, y no con las opciones por defecto de System.Text.Json.
    protected JsonSerializerOptions AppJsonOptions =>
        Factory.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;

    protected JsonContent JsonBody(CreateDailyLogRequest request) =>
        JsonContent.Create(request, options: AppJsonOptions);

    // POST /api/logs con la cookie de sesión del token. Se construye el cuerpo con
    // CreateDailyLogRequestBuilder para declarar solo lo que cada prueba quiere variar.
    protected async Task<HttpResponseMessage> PostLogAsync(string token, CreateDailyLogRequest request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/logs");
        message.Headers.TryAddWithoutValidation("Cookie", $"{SessionCookie.AccessTokenName}={token}");
        message.Content = JsonBody(request);

        return await Factory.CreateClient().SendAsync(message);
    }

    // Sesión creada sin pasar por HTTP: inserta el usuario con el repositorio y firma el token con
    // JwtService, ambos del contenedor de la propia factory. Es más barata que el login por
    // /api/auth/test-login y devuelve solo el usuario y el token (sin HttpClient), que es lo que
    // necesitan las pruebas que montan su propio cliente, su cabecera Cookie o el handshake de
    // SignalR. Cada llamada crea un usuario nuevo con GoogleId y email únicos.
    protected async Task<(User User, string Token)> CreateSessionTokenAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var jwtService = scope.ServiceProvider.GetRequiredService<JwtService>();

        var email = $"e2e-{Guid.NewGuid():N}@example.com";
        var user = await repository.UpsertByGoogleIdAsync(
            new GoogleProfile($"e2e-{Guid.NewGuid():N}", email, "Integration Test User", null));

        return (user, jwtService.Sign(user.Id, user.Email));
    }

    // Devuelve el token con la firma alterada de forma que SEGURO deje de verificar.
    // Se cambia el PRIMER carácter de la firma (tercer segmento): usa sus 6 bits, así que
    // cualquier cambio altera el primer byte. Cambiar el último no vale: solo aporta 4 de
    // sus 6 bits y ~1 de cada 16 veces el token alterado decodifica a los mismos bytes.
    protected static string TamperSignature(string token)
    {
        var segments = token.Split('.');
        var signature = segments[2];
        segments[2] = (signature[0] == 'A' ? 'B' : 'A') + signature[1..];
        return string.Join('.', segments);
    }

    private static string ReadSessionToken(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            throw new InvalidOperationException("/api/auth/test-login no devolvió ninguna cookie.");
        }

        var prefix = $"{SessionCookie.AccessTokenName}=";
        var sessionCookie = cookies.FirstOrDefault(cookie => cookie.StartsWith(prefix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Ninguna de las cookies devueltas es '{SessionCookie.AccessTokenName}'.");

        return sessionCookie[prefix.Length..].Split(';')[0];
    }

    private sealed class SessionCookieHandler(string token) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            request.Headers.TryAddWithoutValidation("Cookie", $"{SessionCookie.AccessTokenName}={token}");
            return base.SendAsync(request, cancellationToken);
        }
    }
}
