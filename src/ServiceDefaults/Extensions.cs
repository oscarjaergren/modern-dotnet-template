using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Adds the cross-cutting concerns every service in this solution gets for free:
/// service discovery, resilience, health checks, and OpenTelemetry.
/// </summary>
/// <remarks>
/// Keep this file to those concerns only. Shared models or helpers belong in a normal class
/// library — this project is referenced by everything, so anything added here is added everywhere.
/// </remarks>
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(options =>
                        // Health probes would otherwise dominate the traces.
                        // OrdinalIgnoreCase, not the default: URL paths are not culture-sensitive
                        // text, and with InvariantGlobalization=true a culture-aware comparison
                        // behaves differently rather than failing. Flagged by MA0074.
                        options.Filter = context =>
                            !context.Request.Path.StartsWithSegments(
                                HealthEndpointPath, StringComparison.OrdinalIgnoreCase)
                            && !context.Request.Path.StartsWithSegments(
                                AlivenessEndpointPath, StringComparison.OrdinalIgnoreCase))
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>
    /// Maps <c>/health</c> (readiness) and <c>/alive</c> (liveness), in every environment.
    /// </summary>
    /// <remarks>
    /// The Aspire template maps these in Development only, on the grounds that health detail leaks
    /// information about your dependencies. The leak is real but it comes from the *response body*,
    /// not from the route: a detailed writer names every check and its status. So this template
    /// keeps the routes and pins the writer instead.
    /// <para>
    /// <see cref="WriteStatusOnly"/> writes the aggregate status
    /// and nothing else — <c>Healthy</c> or <c>Unhealthy</c>, no check names, no exception text. It
    /// matches the framework default, but the framework's own writer is internal, so it is spelled
    /// out here — which also makes swapping in a detailed writer a visible edit rather than an
    /// accident.
    /// </para>
    /// <para>
    /// The trade-off that remains: an unauthenticated caller can tell that the service is up. That
    /// is the same thing every load balancer already knows, and it is the price of a container that
    /// Kubernetes and Container Apps can actually probe. If you add a writer that reports per-check
    /// detail, put it behind authentication or a separate port. See
    /// <c>docs/containers-and-deployment.md</c> and https://aka.ms/aspire/healthchecks.
    /// </para>
    /// </remarks>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(HealthEndpointPath, new HealthCheckOptions
        {
            ResponseWriter = WriteStatusOnly,
        });

        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live"),
            ResponseWriter = WriteStatusOnly,
        });

        return app;
    }

    // The aggregate status and nothing else. Deliberately not the detailed JSON writer: that one
    // names every registered check, which is exactly the information a public probe should not
    // hand out.
    private static Task WriteStatusOnly(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "text/plain";
        return context.Response.WriteAsync(report.Status.ToString(), context.RequestAborted);
    }
}
