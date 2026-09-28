namespace MentalHealthTracker.IntegrationTests;

// Todas las pruebas de integración comparten esta colección. En xUnit eso las obliga a
// ejecutarse de forma secuencial entre sí, que es el requisito de IntegrationTestBase:
// cada una trunca las tablas de la misma base de datos, así que correr en paralelo haría
// que una prueba borrara los datos de otra que aún está en vuelo. Los tests unitarios
// viven en otro proyecto y no se ven afectados.
public static class IntegrationTestCollection
{
    public const string Name = "IntegrationTests";
}
