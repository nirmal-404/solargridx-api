// Smart Solar Microgrid Trading System - request and authentication audit logger.
using System.Diagnostics;
using System.Security.Claims;

namespace SolarGridX.Api.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var path = context.Request.Path;
        var method = context.Request.Method;

        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            var statusCode = context.Response.StatusCode;
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Anonymous";
            var role = context.User.FindFirstValue(ClaimTypes.Role) ?? "None";
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            // Audit log authentication endpoints or slow/failing requests
            if (path.StartsWithSegments("/api/auth") || statusCode >= 400 || stopwatch.ElapsedMilliseconds > 1000)
            {
                logger.LogInformation(
                    "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms [User: {UserId}, Role: {Role}, IP: {Ip}]",
                    method,
                    path,
                    statusCode,
                    stopwatch.ElapsedMilliseconds,
                    userId,
                    role,
                    ip
                );
            }
        }
    }
}
