using System.Threading.RateLimiting;
using Ayush360.Platform.Errors;

namespace Ayush360.Identity.Api.Auth;

/// <summary>
/// Slows down password guessing: at most 10 login / refresh attempts per minute from one IP address.
/// (Account lockout after 5 wrong passwords protects each user on top of this.)
/// </summary>
public static class RateLimits
{
    public const string Login = "login";

    public static IServiceCollection AddAyushRateLimits(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse(
                        "Too many sign-in attempts. Please wait a minute and try again.",
                        null,
                        context.HttpContext.TraceIdentifier),
                    ct);
            };
            options.AddPolicy(Login, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });
}
