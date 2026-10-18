using MentalHealthTracker.Api.Core.Middleware;
using MentalHealthTracker.Api.Modules.Auth;
using Microsoft.OpenApi;

namespace MentalHealthTracker.Api.Core.OpenApi;

public static class OpenApiServiceCollectionExtensions
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Mental Health Tracker API",
                Version = "v1",
                Description = "Mental Health Tracker API: authentication and daily wellbeing logging.",
            });

            options.AddSecurityDefinition(
                JwtService.SessionCookieName,
                new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    Name = JwtService.SessionCookieName,
                    In = ParameterLocation.Cookie,
                    Description = "Session cookie issued after signing in with Google.",
                });

            options.OperationFilter<RequireAuthSecurityOperationFilter>();

            options.SchemaFilter<FluentValidationSchemaFilter>();
            options.ParameterFilter<FluentValidationParameterFilter>();
        });

        return services;
    }

    public static IApplicationBuilder UseApiDocumentation(
        this IApplicationBuilder app,
        IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Mental Health Tracker API v1");
                options.RoutePrefix = SecurityHeadersMiddleware.DocsRoutePrefix;
            });
        }

        return app;
    }
}
