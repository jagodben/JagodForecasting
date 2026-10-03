using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;

namespace ElectionForecaster.Api.Services;

/// <summary>
/// The API's rate limit: a fixed window per client, so one heavy visitor can't use up everyone
/// else's requests.
///
/// On Render every request arrives via Cloudflare and then Render's load balancer, so the socket
/// address is a proxy shared by every visitor. The client comes from Cloudflare's CF-Connecting-IP
/// instead, which Cloudflare sets at its edge and overwrites if a client sends its own. Not
/// X-Forwarded-For: Render's proxies append to that header rather than reset it, so a client could
/// pad it with fake addresses and claim a fresh limit on every request.
/// </summary>
public static class ApiRateLimit
{
    public const string PolicyName = "api";
    public const int PermitsPerWindow = 100;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private const string CloudflareClientHeader = "CF-Connecting-IP";

    public static RateLimitPartition<string> Partition(HttpContext context) =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(ClientAddress(context)),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = PermitsPerWindow,
                Window = Window,
                QueueLimit = 10,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            });

    /// <summary>
    /// The visitor's address: Cloudflare's header when present, else the socket address (local
    /// development). A missing or malformed header falls back to the socket — on Render that's the
    /// shared proxy, so a gap degrades to one shared limit rather than to a spoofable one.
    /// </summary>
    public static IPAddress? ClientAddress(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CloudflareClientHeader, out var header) &&
            IPAddress.TryParse(header.ToString().Trim(), out var client))
            return Normalize(client);

        var socket = context.Connection.RemoteIpAddress;
        return socket is null ? null : Normalize(socket);
    }

    /// <summary>
    /// IPv4 addresses each get their own limit; IPv6 addresses share one per /64, since a single
    /// subscriber is routinely given a whole /64 and could otherwise rotate through it.
    /// </summary>
    public static string PartitionKey(IPAddress? address)
    {
        if (address is null) return "unknown";
        if (address.AddressFamily != AddressFamily.InterNetworkV6) return address.ToString();

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }

    private static IPAddress Normalize(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
