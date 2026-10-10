using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace Ayush360.Platform.Observability;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Logs: Serilog (structured, with CorrelationId / TenantId / UserId on every line).
    /// Traces + metrics: OpenTelemetry for incoming HTTP, outgoing HTTP and PostgreSQL (Npgsql).
    /// Exporter: none yet. On Azure, add Azure.Monitor.OpenTelemetry.AspNetCore and call
    /// UseAzureMonitor() here so everything goes to Application Insights.
    /// </summary>
    public static WebApplicationBuilder AddAyushObservability(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Services.AddSerilog((services, logger) => logger
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", serviceName)
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} | corr={CorrelationId} tenant={TenantId} user={UserId}{NewLine}{Exception}"));

        builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddSource("Npgsql"))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("Npgsql"));

        return builder;
    }

    /// <summary>One summary log line per request: method, path, status, time.</summary>
    public static IApplicationBuilder UseAyushRequestLogging(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} -> {StatusCode} in {Elapsed:0} ms";
            options.GetLevel = (context, _, exception) =>
                exception is not null || context.Response.StatusCode >= 500
                    ? Serilog.Events.LogEventLevel.Error
                    : context.Request.Path.StartsWithSegments("/health")
                        ? Serilog.Events.LogEventLevel.Verbose
                        : Serilog.Events.LogEventLevel.Information;
        });
}
