using MentalHealthTracker.Api.Core.Scaling;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace MentalHealthTracker.Api.Core.RateLimiting;

// Contadores de ventana fija en Redis. Es atómico en el servidor (un script Lua): INCRBY suma, y si
// la clave no tiene caducidad —recién creada, o sin ella por un fallo entre dos comandos— le pone la
// de la ventana. La conexión se abre en el primer uso, no al arrancar, y con AbortOnConnectFail=false:
// una API que arranca con Redis caído no se cae, y es el limitador quien decide qué hacer cuando
// el contador no responde.
public sealed class RedisRateLimitCounterStore(IOptions<ScalingOptions> options) : IRateLimitCounterStore, IAsyncDisposable
{
    private const string Script = """
        local count = redis.call('INCRBY', KEYS[1], ARGV[1])
        local ttl = redis.call('PTTL', KEYS[1])
        if ttl < 0 then
          redis.call('PEXPIRE', KEYS[1], ARGV[2])
          ttl = tonumber(ARGV[2])
        end
        return {count, ttl}
        """;

    private readonly Lazy<Task<ConnectionMultiplexer>> _connection = new(() =>
    {
        var configuration = ConfigurationOptions.Parse(options.Value.Redis.ConnectionString);
        configuration.AbortOnConnectFail = false;
        return ConnectionMultiplexer.ConnectAsync(configuration);
    });

    public RateLimitCount Increment(string key, int by, TimeSpan window) =>
        IncrementAsync(key, by, window, CancellationToken.None).GetAwaiter().GetResult();

    public async Task<RateLimitCount> IncrementAsync(string key, int by, TimeSpan window, CancellationToken cancellationToken)
    {
        var connection = await _connection.Value.WaitAsync(cancellationToken);
        var result = (RedisResult[])(await connection.GetDatabase().ScriptEvaluateAsync(
            Script,
            [(RedisKey)key],
            [by, (long)window.TotalMilliseconds]))!;

        return new RateLimitCount((long)result[0], TimeSpan.FromMilliseconds((long)result[1]));
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection.IsValueCreated)
        {
            await (await _connection.Value).DisposeAsync();
        }
    }
}
