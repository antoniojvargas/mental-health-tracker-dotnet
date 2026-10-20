using MentalHealthTracker.Api.Core.Hosting;
using MentalHealthTracker.Api.Core.Serialization;
using MentalHealthTracker.Api.Modules.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.UnitTests;

// Las enums del dominio ya no llevan [JsonConverter]: el convertidor snake_case tiene que estar
// en cada serializador por el que viajan (controladores, Http.Json de minimal APIs y SignalR).
public sealed class JsonSerializationRegistrationTests
{
    [Fact]
    public void AddApiCore_RegistersTheConverterInMvcAndHttpJsonOptions()
    {
        using var provider = new ServiceCollection().AddLogging().AddApiCore().BuildServiceProvider();

        Assert.Contains(
            provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions.Converters,
            converter => converter is SnakeCaseEnumJsonConverter);
        Assert.Contains(
            provider.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions.Converters,
            converter => converter is SnakeCaseEnumJsonConverter);
    }

    [Fact]
    public void AddRealtimeModule_RegistersTheConverterInTheSignalRJsonProtocol()
    {
        using var provider = new ServiceCollection().AddLogging().AddRealtimeModule().BuildServiceProvider();

        Assert.Contains(
            provider.GetRequiredService<IOptions<JsonHubProtocolOptions>>().Value.PayloadSerializerOptions.Converters,
            converter => converter is SnakeCaseEnumJsonConverter);
    }
}
