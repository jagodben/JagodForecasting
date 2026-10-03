using System.Net;
using System.Threading.RateLimiting;
using ElectionForecaster.Api.Services;
using Microsoft.AspNetCore.Http;

namespace ElectionForecaster.Tests;

/// <summary>
/// Pins that the API rate limit is per visitor rather than shared, and that a client can't claim a
/// fresh limit by forging forwarding headers.
/// </summary>
public class ApiRateLimitTests
{
    private static HttpContext Request(string socket, string? cfConnectingIp = null, string? xForwardedFor = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(socket);
        if (cfConnectingIp != null) context.Request.Headers["CF-Connecting-IP"] = cfConnectingIp;
        if (xForwardedFor != null) context.Request.Headers["X-Forwarded-For"] = xForwardedFor;
        return context;
    }

    [Fact]
    public void CloudflareHeaderIdentifiesTheClientBehindTheProxy()
    {
        var client = ApiRateLimit.ClientAddress(Request("10.0.0.1", "203.0.113.7", xForwardedFor: "198.51.100.9"));
        Assert.Equal(IPAddress.Parse("203.0.113.7"), client);
    }

    [Fact]
    public void MissingOrMalformedHeaderFallsBackToTheSocket()
    {
        Assert.Equal(IPAddress.Parse("10.0.0.1"), ApiRateLimit.ClientAddress(Request("10.0.0.1")));
        Assert.Equal(IPAddress.Parse("10.0.0.1"), ApiRateLimit.ClientAddress(Request("10.0.0.1", "not-an-ip")));
        Assert.Equal(IPAddress.Parse("10.0.0.1"), ApiRateLimit.ClientAddress(Request("::ffff:10.0.0.1")));
    }

    [Fact]
    public void Ipv6AddressesShareALimitPerSlash64()
    {
        static string Key(string ip) => ApiRateLimit.PartitionKey(IPAddress.Parse(ip));
        Assert.Equal(Key("2001:db8:1:2::1"), Key("2001:db8:1:2:ffff::9"));
        Assert.NotEqual(Key("2001:db8:1:2::1"), Key("2001:db8:1:3::1"));
    }

    [Fact]
    public void OneVisitorExhaustingTheLimitDoesNotBlockAnother()
    {
        using var limiter = PartitionedRateLimiter.Create<HttpContext, string>(ApiRateLimit.Partition);
        var heavy = Request("10.0.0.1", "203.0.113.7");

        for (int i = 0; i < ApiRateLimit.PermitsPerWindow; i++)
            Assert.True(limiter.AttemptAcquire(heavy).IsAcquired);
        Assert.False(limiter.AttemptAcquire(heavy).IsAcquired);

        // Forging X-Forwarded-For doesn't buy a fresh limit...
        Assert.False(limiter.AttemptAcquire(Request("10.0.0.1", "203.0.113.7", xForwardedFor: "198.51.100.1")).IsAcquired);
        // ...while a different visitor behind the same proxy is unaffected.
        Assert.True(limiter.AttemptAcquire(Request("10.0.0.1", "198.51.100.20")).IsAcquired);
    }
}
