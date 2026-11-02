using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace MentalHealthTracker.Api.Core.Scaling;

public static class RedisBackplaneExtensions
{
    // Backplane de SignalR sobre Redis, solo si Scaling:Redis:ConnectionString está definida. Se lee
    // al registrar (SignalR elige su HubLifetimeManager al construir el contenedor), a diferencia de
    // lo demás, que se lee al usarse. Los grupos user:{id} pasan a sincronizarse entre réplicas sin
    // que ILogEventEmitter ni LogsHub cambien.
    public static ISignalRServerBuilder AddRedisBackplaneIfConfigured(
        this ISignalRServerBuilder builder,
        IConfiguration? configuration)
    {
        var redis = (configuration?.GetSection(ScalingOptions.SectionName).Get<ScalingOptions>() ?? new()).Redis;
        if (!redis.Enabled)
        {
            return builder;
        }

        return builder.AddStackExchangeRedis(
            redis.ConnectionString,
            options => options.Configuration.ChannelPrefix = RedisChannel.Literal(redis.KeyPrefix));
    }
}
