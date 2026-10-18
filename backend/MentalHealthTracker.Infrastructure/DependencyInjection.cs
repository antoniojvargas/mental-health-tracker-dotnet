using MentalHealthTracker.Domain.Repositories;
using MentalHealthTracker.Infrastructure.Persistence;
using MentalHealthTracker.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        // La cadena se lee al crear el contexto, no al registrarlo: así lo que añadan los
        // proveedores de configuración después (p. ej. los tests de integración) también cuenta.
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration[ConnectionStringKey]));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IDailyLogRepository, DailyLogRepository>();
        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
