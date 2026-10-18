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
            ("Database__ConnectionString", "Database__ConnectionString_FILE", "Database:ConnectionString"),
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
                    Data[configurationKey] = ReadSecretFile(fileEnvironmentVariable, filePath);
                }
            }
        }

        // Un *_FILE que apunta a un fichero inexistente es un error de despliegue (volumen o
        // secret sin montar): el arranque falla igual que antes, pero el mensaje nombra la
        // variable y la ruta en lugar de un FileNotFoundException genérico.
        private static string ReadSecretFile(string fileEnvironmentVariable, string filePath)
        {
            try
            {
                return File.ReadAllText(filePath).Trim();
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                throw new InvalidOperationException(
                    $"{fileEnvironmentVariable} points to '{filePath}', but that file does not exist.",
                    exception);
            }
        }
    }
}