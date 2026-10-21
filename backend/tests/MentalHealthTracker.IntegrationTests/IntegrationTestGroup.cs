using Xunit;

// Asegura que ninguna colección corra en paralelo con otra: todas truncan las tablas de la
// misma base de datos, así que correr a la vez haría que una prueba borrara los datos de otra
// que aún está en vuelo. Los tests unitarios viven en otro proyecto y no se ven afectados.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MentalHealthTracker.IntegrationTests;

// Las pruebas de integración se reparten en dos colecciones:
//
//  - Name: la mayoría. Comparten UNA CustomWebApplicationFactory (ICollectionFixture), de modo que
//    hay un arranque de la API y una migración por ejecución, no uno por clase. A cambio comparten
//    el estado del proceso (limitador de peticiones, conexiones de SignalR…); por eso cada test
//    usa usuarios propios y los que necesitan otra configuración crean un host derivado con
//    WithWebHostBuilder.
//  - IsolatedName: las clases que agotan cuotas del limitador y necesitan contadores limpios
//    (RateLimitingTests, ForwardedHeadersTests, ForwardedClientPartitionTests). Cada una trae su
//    propia factory como fixture de clase. Corren secuencialmente con las demás (ver el
//    CollectionBehavior de arriba).
public static class IntegrationTestGroup
{
    public const string Name = "IntegrationTests";

    public const string IsolatedName = "IntegrationTests.IsolatedFactories";
}

[CollectionDefinition(IntegrationTestGroup.Name)]
public sealed class SharedFactoryDefinition : ICollectionFixture<CustomWebApplicationFactory>;
