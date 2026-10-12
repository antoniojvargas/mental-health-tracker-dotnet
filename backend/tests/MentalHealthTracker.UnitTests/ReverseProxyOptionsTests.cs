using System.Net;
using MentalHealthTracker.Api.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace MentalHealthTracker.UnitTests;

public sealed class ReverseProxyOptionsTests
{
    private static ValidateOptionsResult Validate(ReverseProxyOptions options) =>
        new ReverseProxyOptionsValidator().Validate(null, options);

    [Fact]
    public void Defaults_AreDisabledWithForwardLimitOne()
    {
        var options = new ReverseProxyOptions();

        Assert.False(options.Enabled);
        Assert.Empty(options.KnownProxies);
        Assert.Empty(options.KnownNetworks);
        Assert.Equal(1, options.ForwardLimit);
        Assert.True(Validate(options).Succeeded);
    }

    [Fact]
    public void Validate_Enabled_WithoutProxiesOrNetworks_Fails()
    {
        var result = Validate(new ReverseProxyOptions { Enabled = true });

        Assert.True(result.Failed);
        Assert.Contains("spoofing", result.FailureMessage);
    }

    [Fact]
    public void Validate_Enabled_WithProxy_Succeeds()
    {
        Assert.True(Validate(new ReverseProxyOptions { Enabled = true, KnownProxies = ["10.0.0.2"] }).Succeeded);
    }

    [Fact]
    public void Validate_Enabled_WithNetwork_Succeeds()
    {
        Assert.True(Validate(new ReverseProxyOptions { Enabled = true, KnownNetworks = ["172.28.0.0/24"] }).Succeeded);
    }

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("10.0.0.256")]
    [InlineData("")]
    public void Validate_InvalidProxy_FailsShowingValue(string proxy)
    {
        var result = Validate(new ReverseProxyOptions { KnownProxies = [proxy] });

        Assert.True(result.Failed);
        Assert.Contains($"'{proxy}'", result.FailureMessage);
    }

    [Theory]
    [InlineData("172.28.0.0")]
    [InlineData("172.28.0.0/33")]
    [InlineData("172.28.0.0/abc")]
    [InlineData("nope/24")]
    public void Validate_InvalidNetwork_FailsShowingValue(string network)
    {
        var result = Validate(new ReverseProxyOptions { KnownNetworks = [network] });

        Assert.True(result.Failed);
        Assert.Contains($"'{network}'", result.FailureMessage);
    }

    [Fact]
    public void Validate_DisabledStillRejectsInvalidEntries()
    {
        Assert.True(Validate(new ReverseProxyOptions { Enabled = false, KnownProxies = ["bad"] }).Failed);
    }

    [Fact]
    public void Apply_ClearsLoopbackDefaults_AndKeepsOnlyConfiguredTrust()
    {
        var forwarded = new ForwardedHeadersOptions();
        Assert.NotEmpty(forwarded.KnownProxies); // ::1 por defecto

        ReverseProxyServiceCollectionExtensions.Apply(
            forwarded,
            new ReverseProxyOptions
            {
                Enabled = true,
                KnownProxies = ["10.0.0.2"],
                KnownNetworks = ["172.28.0.0/24"],
                ForwardLimit = 2,
            });

        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, forwarded.ForwardedHeaders);
        Assert.Equal(2, forwarded.ForwardLimit);
        Assert.Equal([IPAddress.Parse("10.0.0.2")], forwarded.KnownProxies);

        var network = Assert.Single(forwarded.KnownNetworks);
        Assert.True(network.Contains(IPAddress.Parse("172.28.0.99")));
        Assert.False(network.Contains(IPAddress.Parse("172.28.1.1")));
    }

    [Fact]
    public void Apply_WithoutConfiguredTrust_LeavesNoTrustedProxies()
    {
        var forwarded = new ForwardedHeadersOptions();

        ReverseProxyServiceCollectionExtensions.Apply(forwarded, new ReverseProxyOptions());

        Assert.Empty(forwarded.KnownProxies);
        Assert.Empty(forwarded.KnownNetworks);
    }
}
