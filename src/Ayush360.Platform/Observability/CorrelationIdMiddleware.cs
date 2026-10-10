using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Ayush360.Platform.Security;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Ayush360.Platform.Observability;

/// <summary>
/// Reads X-Correlation-ID (the React app sends one per request) or creates one, returns it in the
/// response, and attaches it to every log line and trace for this request.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault();
        if (!IsSafe(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });
        Activity.Current?.SetTag("correlation.id", correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    // Only accept short ids made of letters, digits, '-' and '_' (keeps logs clean, blocks log injection).
    private static bool IsSafe([NotNullWhen(true)] string? value) =>
        !string.IsNullOrEmpty(value)
        && value.Length <= 64
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}

/// <summary>
/// Runs after authentication: adds TenantId and UserId to logs and traces for the rest of the request.
/// </summary>
public sealed class RequestContextEnricherMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var tenantId = context.User.FindFirst(PlatformClaims.TenantId)?.Value;
        var userId = context.User.FindFirst(PlatformClaims.UserId)?.Value;

        if (tenantId is not null)
        {
            Activity.Current?.SetTag("ayush360.tenant_id", tenantId);
        }

        if (userId is not null)
        {
            Activity.Current?.SetTag("enduser.id", userId);
        }

        using (LogContext.PushProperty("TenantId", tenantId ?? "-"))
        using (LogContext.PushProperty("UserId", userId ?? "-"))
        {
            await next(context);
        }
    }
}
