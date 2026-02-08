namespace ElectionForecaster.Api.Middleware;

/// <summary>
/// Middleware that validates API key for all requests.
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;
    private const string ApiKeyHeaderName = "X-API-Key";

    public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        // Skip API key check for health checks and swagger in development
        var path = context.Request.Path.Value?.ToLower() ?? "";
        if (path.Contains("/health") || path == "/")
        {
            await _next(context);
            return;
        }

        // Get the expected API key from configuration
        var expectedApiKey = configuration["ApiKey"];

        // If no API key is configured, allow all requests (development mode)
        if (string.IsNullOrEmpty(expectedApiKey))
        {
            await _next(context);
            return;
        }

        // Check for API key in header
        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedApiKey))
        {
            _logger.LogWarning("API request without API key from {IP}",
                context.Connection.RemoteIpAddress);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "API key is required" });
            return;
        }

        if (!string.Equals(providedApiKey, expectedApiKey, StringComparison.Ordinal))
        {
            _logger.LogWarning("Invalid API key provided from {IP}",
                context.Connection.RemoteIpAddress);
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid API key" });
            return;
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods for API key middleware.
/// </summary>
public static class ApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyMiddleware>();
    }
}
