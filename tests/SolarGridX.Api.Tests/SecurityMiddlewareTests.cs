// Smart Solar Microgrid Trading System - SecurityHeadersMiddleware unit tests.
using Microsoft.AspNetCore.Http;
using SolarGridX.Api.Middleware;

namespace SolarGridX.Api.Tests;

public sealed class SecurityMiddlewareTests
{
    [Fact]
    public async Task SecurityHeadersMiddleware_AddsExpectedSecurityHeaders()
    {
        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(innerContext =>
        {
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        var response = context.Response;
        Assert.Equal("nosniff", response.Headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", response.Headers["X-Frame-Options"]);
        Assert.Equal("strict-origin-when-cross-origin", response.Headers["Referrer-Policy"]);
    }
}
