using MentalHealthTracker.Domain.Repositories;
using MentalHealthTracker.Infrastructure.Persistence;
using MentalHealthTracker.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace MentalHealthTracker.Infrastructure;

public static class DependencyInjection
{
    // Clave de la cadena de conexión. La sección Database se enlaza y valida como opciones
    // en la Api (DatabaseOptions), que es quien arranca; Infrastructure no puede referenciar
    // ese tipo, así que lee la clave directamente.
    private const string ConnectionStringKey = "Database:ConnectionString";

    // Todo lo que esta capa aporta al contenedor: el DbContext, los repositorios que
    // implementan los contratos de Domain y el sembrador de datos de demostración.
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Un único NpgsqlDataSource para toda la aplicación: EF Core, el health check y el cierre
        // ordenado comparten su pool de conexiones. La cadena se lee al crearlo (la primera vez
        // que alguien lo pide), no al registrarlo: así lo que añadan los proveedores de
        // configuración después (p. ej. los tests de integración) también cuenta. El contenedor
        // lo libera al apagar el host.
        services.AddSingleton(_ => new NpgsqlDataSourceBuilder(configuration[ConnectionStringKey]).Build());
        // Con pool de contextos: AppDbContext no guarda estado propio (solo el modelo y las
        // opciones, y EF restablece el seguimiento de cambios al devolver el contexto), así que
        // reutilizar instancias evita construir uno por petición. Medido con 1000 peticiones
        // GET /api/logs: ~+23 % de peticiones/s en secuencia y ~+18 % con 8 en paralelo.
        services.AddDbContextPool<AppDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>()));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDailyLogRepository, DailyLogRepository>();
        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
