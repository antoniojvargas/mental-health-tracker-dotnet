using MentalHealthTracker.Api.Core.Configuration;

namespace MentalHealthTracker.Api.Core.Hosting;

public static class ApiCors
{
    public const string PolicyName = "Frontend";

    public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
    {
        var appUrls = configuration
            .GetSection(AppUrlsOptions.SectionName)
            .Get<AppUrlsOptions>();
        services.AddCors(options =>
            options.AddPolicy(PolicyName, policy =>
                policy.WithOrigins(appUrls?.FrontendUrl ?? string.Empty).AllowCredentials()));

        return services;
    }
}
