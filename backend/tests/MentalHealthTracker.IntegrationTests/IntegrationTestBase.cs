using System.Net;
using System.Net.Http.Json;
using MentalHealthTracker.Api.Modules.Auth;
using MentalHealthTracker.Domain.Entities;
using MentalHealthTracker.Domain.Models;
using MentalHealthTracker.Domain.Repositories;
using MentalHealthTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
// Al heredar de esta clase hay que escribir la lista de bases como
// `: IntegrationTestBase(factory), IClassFixture<CustomWebApplicationFactory>`, con la
// clase base primero. Al revés no compila: con constructor primario, C# no acepta que una
// interfaz preceda a una clase base con argumentos y falla con CS1003.
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

    private static string ReadSessionToken(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            throw new InvalidOperationException("/api/auth/test-login no devolvió ninguna cookie.");
        }

        var prefix = $"{JwtService.SessionCookieName}=";
        var sessionCookie = cookies.FirstOrDefault(cookie => cookie.StartsWith(prefix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Ninguna de las cookies devueltas es '{JwtService.SessionCookieName}'.");

        return sessionCookie[prefix.Length..].Split(';')[0];
    }

    private sealed class SessionCookieHandler(string token) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            request.Headers.TryAddWithoutValidation("Cookie", $"{JwtService.SessionCookieName}={token}");
            return base.SendAsync(request, cancellationToken);
        }
    }
}
