using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MentalHealthTracker.Api.Core.Scaling;

// Con Redis activado, la cadena tiene que ser una cadena de StackExchange.Redis válida y el
// prefijo no puede estar vacío: se falla al arrancar y no en la primera conexión.
public sealed class ScalingOptionsValidator : IValidateOptions<ScalingOptions>
{
    public ValidateOptionsResult Validate(string? name, ScalingOptions options)
    {
        var redis = options.Redis;
        if (!redis.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(redis.KeyPrefix))
        {
            return ValidateOptionsResult.Fail("Scaling:Redis:KeyPrefix must not be empty.");
        }

        try
        {
            _ = ConfigurationOptions.Parse(redis.ConnectionString);
        }
        catch (ArgumentException exception)
        {
            return ValidateOptionsResult.Fail($"Scaling:Redis:ConnectionString is not a valid Redis connection string: {exception.Message}");
        }

        return ValidateOptionsResult.Success;
    }
}
