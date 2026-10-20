using System.Reflection;
using MentalHealthTracker.Api.Core.Hosting;
using MentalHealthTracker.Domain.Common;
using MentalHealthTracker.Infrastructure;

namespace MentalHealthTracker.UnitTests;

// Reglas de dependencia entre capas, comprobadas con reflexión sobre los ensamblados
// compilados (sin paquetes de arquitectura): Api -> Infrastructure -> Domain, y nunca al
// revés. El compilador solo anota en un ensamblado las referencias que usa de verdad, así que
// una dependencia cuyo código se borre desaparece sola; lo que estas pruebas impiden es que
// aparezca una prohibida.
public sealed class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(SnakeCaseEnum<>).Assembly;

    private static readonly Assembly Infrastructure = typeof(DependencyInjection).Assembly;

    private static readonly Assembly Api = typeof(ApiPipeline).Assembly;

    private static List<string> ReferencedNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty).ToList();

    // Controles positivos: sin ellos, un fallo al leer las referencias dejaría las reglas
    // siguientes en verde sin comprobar nada.
    [Fact]
    public void TheAssembliesAreTheOnesWeThinkTheyAre()
    {
        Assert.Equal("MentalHealthTracker.Domain", Domain.GetName().Name);
        Assert.Equal("MentalHealthTracker.Infrastructure", Infrastructure.GetName().Name);
        Assert.Equal("MentalHealthTracker.Api", Api.GetName().Name);
    }

    [Fact]
    public void TheReferenceReaderSeesTheLegitimateDependencies()
    {
        Assert.Contains("MentalHealthTracker.Domain", ReferencedNames(Infrastructure));
        Assert.Contains("Microsoft.EntityFrameworkCore", ReferencedNames(Infrastructure));
        Assert.Contains("MentalHealthTracker.Infrastructure", ReferencedNames(Api));
        Assert.Contains("MentalHealthTracker.Domain", ReferencedNames(Api));
    }

    [Fact]
    public void Domain_DoesNotReferenceEntityFrameworkCore()
    {
        Assert.DoesNotContain(
            ReferencedNames(Domain),
            name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Domain_DoesNotReferenceAspNetCore()
    {
        Assert.DoesNotContain(
            ReferencedNames(Domain),
            name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_DoesNotReferenceTheApi()
    {
        Assert.DoesNotContain(ReferencedNames(Infrastructure), name => name == "MentalHealthTracker.Api");
    }
}
