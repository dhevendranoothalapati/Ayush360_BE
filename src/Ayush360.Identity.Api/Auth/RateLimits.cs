using System.Threading.RateLimiting;
using Ayush360.Platform.Errors;

namespace Ayush360.Identity.Api.Auth;

/// <summary>
/// Per-IP limits on the sign-in endpoints. A whole hospital usually reaches us from one public IP
/// (every front-desk PC behind the same router), so limits are sized for a busy hospital, not one person:
///   login   → 60 per minute per IP (token bucket: bursts allowed, refills every minute)
///   refresh → 300 per minute per IP (every open tab refreshes its token silently)
/// Account lockout after 5 wrong passwords still protects each individual user on top of this.
/// In Azure this needs UseForwardedHeaders, otherwise every request looks like it comes from the proxy's IP.
/// </summary>
public static class RateLimits
{
    public const string Login = "login";
    public const string Refresh = "refresh";

    public static IServiceCollection AddAyushRateLimits(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse(
                        "Too many sign-in attempts. Please wait a minute and try again.",
                        null,
                        context.HttpContext.TraceIdentifier),
                    ct);
            };
            options.AddPolicy(Login, httpContext => PerIp(httpContext, Login, perMinute: 60));
            options.AddPolicy(Refresh, httpContext => PerIp(httpContext, Refresh, perMinute: 300));
        });

    private static RateLimitPartition<string> PerIp(HttpContext httpContext, string policy, int perMinute) =>
        RateLimitPartition.GetTokenBucketLimiter(
            $"{policy}:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = perMinute,
                TokensPerPeriod = perMinute,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
}
