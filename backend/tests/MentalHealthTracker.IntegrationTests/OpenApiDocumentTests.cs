using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

namespace MentalHealthTracker.IntegrationTests;

// El documento OpenAPI se registra con AddSwaggerGen en todos los entornos (solo la UI de
// Swagger se monta en Development), así que el generador es resolvible desde el contenedor
// de la API también en Testing. Generarlo ejercita los filtros de Swashbuckle que traducen
// las reglas de FluentValidation al esquema (FluentValidationSchemaFilter,
// FluentValidationParameterFilter y FluentValidationConstraints), que de otro modo quedan sin
// cobertura porque los tests de integración no piden /swagger.
[Collection(IntegrationTestGroup.Name)]
public sealed class OpenApiDocumentTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    [Fact]
    public void SwaggerDocument_GeneratesDailyLogsSchemaWithValidationBounds()
    {
        using var scope = Factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<ISwaggerProvider>();

        var document = provider.GetSwagger("v1");

        Assert.NotNull(document);
        Assert.Contains("/api/logs", document.Paths.Keys);
    }
}
