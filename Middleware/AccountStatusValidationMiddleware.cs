// Smart Solar Microgrid Trading System - validates that authenticated JWT holder remains in Active status.
using System.Security.Claims;
using MongoDB.Driver;
using SolarGridX.Api.DTOs.Responses;
using SolarGridX.Api.Infrastructure;
using SolarGridX.Api.Models.Enums;

namespace SolarGridX.Api.Middleware;

public sealed class AccountStatusValidationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, MongoContext db)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var user = await db.Users
                    .Find(x => x.Id == userId)
                    .Project(x => new { x.AccountStatus })
                    .FirstOrDefaultAsync();

                if (user is null || user.AccountStatus != AccountStatus.Active)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new ErrorResponse(
                        StatusCodes.Status403Forbidden,
                        "Your account has been deactivated or is pending administrative approval.",
                        null,
                        DateTime.UtcNow
                    ));
                    return;
                }
            }
        }

        await next(context);
    }
}
