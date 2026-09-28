using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MentalHealthTracker.IntegrationTests;

// Fabrica de la API completa (WebApplicationFactory<Program>) con configuración propia
// de test: apunta a postgres-test (docker-compose.test.yml, puerto 5433) y provee los
// secretos ficticios que el entry point valida al arrancar (Jwt:Secret, GoogleOAuth:*,
// AppUrls:FrontendUrl). Sin estos valores ValidateOnStart de Program.cs abortaría el boot.
// El entorno se fija a "Testing", que es lo que habilita MapTestAuthEndpoints en
// Program.cs (endpoint /api/auth/test-login) y desactiva Swagger, que solo se monta en
// Development. El esquema no se crea aquí: Program.cs llama a DatabaseInitializer.MigrateAsync
// durante el arranque, antes de que la factory sirva la primera petición, y ese migrate
// espera a que postgres-test acepte conexiones con backoff exponencial.
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Host=localhost;Port=5433;Database=mht_test;Username=mht_test;Password=mht_test",
                ["Jwt:Secret"] = "integration-test-jwt-secret-key-value",
                ["Jwt:ExpiresInDays"] = "7",
                ["GoogleOAuth:ClientId"] = "integration-test-client.apps.googleusercontent.com",
                ["GoogleOAuth:ClientSecret"] = "integration-test-client-secret",
                ["GoogleOAuth:RedirectUri"] = "http://localhost:5173/auth/callback",
                ["AppUrls:FrontendUrl"] = "http://localhost:5173",
            });
        });
    }
}