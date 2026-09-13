using Microsoft.Extensions.Configuration;

namespace MentalHealthTracker.Api.Core.Configuration;

public static class SecretFileConfigurationExtensions
{
    public static IConfigurationBuilder AddSecretFileConfiguration(this IConfigurationBuilder builder)
    {
        builder.Add(new SecretFileConfigurationSource());
        return builder;
    }

    private sealed class SecretFileConfigurationSource : IConfigurationSource
    {
        public IConfigurationProvider Build(IConfigurationBuilder builder) =>
            new SecretFileConfigurationProvider();
    }

    private sealed class SecretFileConfigurationProvider : ConfigurationProvider
    {
        private static readonly (string EnvironmentVariable, string FileEnvironmentVariable, string ConfigurationKey)[] Secrets =
        [
            ("JWT_SECRET", "JWT_SECRET_FILE", "Jwt:Secret"),
            ("GOOGLE_CLIENT_SECRET", "GOOGLE_CLIENT_SECRET_FILE", "GoogleOAuth:ClientSecret"),
            ("ConnectionStrings__Default", "ConnectionStrings__Default_FILE", "ConnectionStrings:Default"),
        ];

        public override void Load()
        {
            foreach (var (environmentVariable, fileEnvironmentVariable, configurationKey) in Secrets)
            {
                var plainValue = Environment.GetEnvironmentVariable(environmentVariable);
                if (!string.IsNullOrEmpty(plainValue))
                {
                    Data[configurationKey] = plainValue;
                }
                else if (Environment.GetEnvironmentVariable(fileEnvironmentVariable) is { } filePath)
                {
                    Data[configurationKey] = File.ReadAllText(filePath).Trim();
                }
            }
        }
    }
}