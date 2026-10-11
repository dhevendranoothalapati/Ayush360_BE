using Ayush360.Platform.Errors;
using Ayush360.Platform.Observability;
using Ayush360.Platform.Security;
using Ayush360.Platform.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ayush360.Platform;

/// <summary>
/// One call per API wires up everything every Ayush360 service must have:
/// <code>
/// builder.AddAyushPlatform("ayush360-identity");
/// ...
/// app.UseAyushPlatform();
/// </code>
/// </summary>
public static class PlatformExtensions
{
    public const string CorsPolicyName = "ayush360-frontend";

    public static WebApplicationBuilder AddAyushPlatform(this WebApplicationBuilder builder, string serviceName)
    {
        builder.AddAyushObservability(serviceName);

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();

        builder.Services.AddAyushAuthentication(builder.Configuration);
        builder.Services.AddAyushAuthorization();

        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();

        // [ApiController] model-binding errors use the same { message, errors } shape.
        builder.Services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(entry => entry.Value is { Errors.Count: > 0 })
                    .ToDictionary(
                        entry => ToFieldName(entry.Key),
                        entry => entry.Value!.Errors[0].ErrorMessage);
                return new BadRequestObjectResult(new ErrorResponse(
                    "Please correct the highlighted fields.", errors, context.HttpContext.TraceIdentifier));
            };
        });

        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        builder.Services.AddCors(options => options.AddPolicy(CorsPolicyName, policy => policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)));

        builder.Services.AddHealthChecks();

        // Behind proxies (Nginx in the web container, Azure ingress) the client IP arrives in X-Forwarded-For.
        // Turned on with ASPNETCORE_FORWARDEDHEADERS_ENABLED=true (set in the Docker image). Each proxy hop adds
        // one entry: ForwardedHeaders:ForwardLimit = number of proxies between the browser-facing Nginx and this API
        // (1 = Docker Compose, 2 = Azure Container Apps internal ingress). Default 1.
        var forwardLimit = builder.Configuration.GetValue<int?>("ForwardedHeaders:ForwardLimit");
        if (forwardLimit is > 0)
        {
            builder.Services.Configure<ForwardedHeadersOptions>(options => options.ForwardLimit = forwardLimit);
        }

        return builder;
    }

    public static WebApplication UseAyushPlatform(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();
        app.UseAyushRequestLogging();
        app.UseCors(CorsPolicyName);
        app.UseAuthentication();
        app.UseMiddleware<RequestContextEnricherMiddleware>();
        app.UseAuthorization();

        app.MapHealthChecks("/health").AllowAnonymous();

        return app;
    }

    // "$.firstName" / "FirstName" / "patient.MobileNumber" -> "firstName" / "patient.mobileNumber"
    private static string ToFieldName(string key)
    {
        var trimmed = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;
        return string.Join('.', trimmed.Split('.').Select(part =>
            part.Length == 0 ? part : char.ToLowerInvariant(part[0]) + part[1..]));
    }
}
