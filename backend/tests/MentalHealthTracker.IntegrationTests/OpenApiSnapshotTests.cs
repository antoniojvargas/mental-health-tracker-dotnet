using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Swagger;

namespace MentalHealthTracker.IntegrationTests;

// frontend/openapi.json es el snapshot del contrato del que se generan los tipos del
// frontend (src/types/api.generated.d.ts). Este test genera el documento con
// ISwaggerProvider, lo serializa como lo hace el endpoint /swagger/v1/swagger.json
// (OpenAPI 3.0, JSON) y lo compara SEMÁNTICAMENTE con el snapshot: ambos se parsean, así
// que el orden de las propiedades, los espacios y los saltos de línea no cuentan.
// Si la API cambia sin que se regenere el snapshot, el frontend se quedaría con tipos
// que ya no son los de la API.
[Collection(IntegrationTestGroup.Name)]
public sealed class OpenApiSnapshotTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory), IClassFixture<CustomWebApplicationFactory>
{
    private const string RegenerateHelp = """
        Para regenerar el snapshot tras un cambio de API INTENCIONADO:
          1) cd backend/MentalHealthTracker.Api
             ASPNETCORE_ENVIRONMENT=Development dotnet run   (con Postgres arriba y la configuración del README)
          2) curl -s http://localhost:3000/swagger/v1/swagger.json > frontend/openapi.json
          3) cd frontend && npm run generate:types
          4) revisar el diff de frontend/openapi.json y de src/types/api.generated.d.ts
        Atajo: UPDATE_OPENAPI_SNAPSHOT=1 dotnet test --filter OpenApiSnapshotTests reescribe frontend/openapi.json
        (después, igualmente, npm run generate:types).
        """;

    [Fact]
    public async Task GeneratedDocument_MatchesTheFrontendSnapshot()
    {
        var generated = JsonNode.Parse(await GenerateJsonAsync())!;
        RemoveTestingOnlyEndpoints(generated);
        var snapshotPath = SnapshotPath();

        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI_SNAPSHOT") == "1")
        {
            await File.WriteAllTextAsync(snapshotPath, generated.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
            return;
        }

        var snapshot = JsonNode.Parse(await File.ReadAllTextAsync(snapshotPath))!;

        Assert.True(
            JsonNode.DeepEquals(snapshot, generated),
            $"El documento OpenAPI generado difiere de {snapshotPath}.\n\n{Differences(snapshot, generated)}\n{RegenerateHelp}");
    }

    // La factory arranca en Testing, donde Program.cs registra POST /api/auth/test-login (y su
    // TestLoginRequest) para que los tests entren sin Google. No existe en Development ni en
    // producción, que es de donde sale el snapshot, así que no forma parte del contrato.
    private static void RemoveTestingOnlyEndpoints(JsonNode document)
    {
        document["paths"]!.AsObject().Remove("/api/auth/test-login");
        document["components"]!["schemas"]!.AsObject().Remove("TestLoginRequest");
    }

    private async Task<string> GenerateJsonAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<ISwaggerProvider>();
        var document = provider.GetSwagger("v1");

        await using var stream = new MemoryStream();
        await using (var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true))
        {
            document.SerializeAsV3(new OpenApiJsonWriter(writer));
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // Primeras diferencias, con su ruta JSON, para no obligar a comparar 30 KB a ojo.
    private static string Differences(JsonNode expected, JsonNode actual)
    {
        var found = new List<string>();
        Compare("$", expected, actual, found);
        var shown = found.Take(15).ToList();
        var more = found.Count > shown.Count ? $"\n... y {found.Count - shown.Count} diferencias más" : string.Empty;
        return "Diferencias (snapshot -> generado):\n  " + string.Join("\n  ", shown) + more;
    }

    private static void Compare(string path, JsonNode? expected, JsonNode? actual, List<string> found)
    {
        if (found.Count >= 100 || JsonNode.DeepEquals(expected, actual))
        {
            return;
        }

        if (expected is JsonObject left && actual is JsonObject right)
        {
            foreach (var key in left.Select(p => p.Key).Union(right.Select(p => p.Key)))
            {
                Compare($"{path}.{key}", left[key], right[key], found);
            }
        }
        else if (expected is JsonArray leftArray && actual is JsonArray rightArray && leftArray.Count == rightArray.Count)
        {
            for (var i = 0; i < leftArray.Count; i++)
            {
                Compare($"{path}[{i}]", leftArray[i], rightArray[i], found);
            }
        }
        else
        {
            found.Add($"{path}: {Short(expected)} -> {Short(actual)}");
        }
    }

    private static string Short(JsonNode? node)
    {
        if (node is null)
        {
            return "(ausente)";
        }

        var text = node.ToJsonString();
        return text.Length > 80 ? text[..80] + "…" : text;
    }

    private static string SnapshotPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "frontend", "openapi.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("No se encontró frontend/openapi.json subiendo desde el directorio de pruebas.");
    }
}
