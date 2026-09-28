using MentalHealthTracker.Infrastructure.Persistence;
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
}
