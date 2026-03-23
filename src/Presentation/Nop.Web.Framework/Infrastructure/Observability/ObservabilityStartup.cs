using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Nop.Web.Framework.Infrastructure.Observability;

/// <summary>
/// Registers the OpenTelemetry SDK, exporters, and tracing middleware.
///
/// Order = 5: runs after NopProxyStartup (-1) and ErrorHandlerStartup (0) so that
/// forwarded headers and error handling are already in place, but before all other
/// middleware — ensuring every HTTP request is captured as a root span.
/// </summary>
public class ObservabilityStartup : INopStartup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: DiagnosticsConfig.ServiceName,
                    serviceVersion: typeof(ObservabilityStartup).Assembly.GetName().Version?.ToString() ?? "1.0.0"))
            .WithTracing(tracing => tracing
                // Built-in ASP.NET Core instrumentation (HTTP request spans)
                .AddAspNetCoreInstrumentation(opts =>
                {
                    // Filter out noise from static files and health checks
                    opts.Filter = httpContext =>
                        !httpContext.Request.Path.StartsWithSegments("/lib") &&
                        !httpContext.Request.Path.StartsWithSegments("/css") &&
                        !httpContext.Request.Path.StartsWithSegments("/js") &&
                        !httpContext.Request.Path.StartsWithSegments("/images") &&
                        !httpContext.Request.Path.StartsWithSegments("/favicon.ico");
                })
                // Outbound HTTP calls (payment gateways, external APIs)
                .AddHttpClientInstrumentation()
                // Our custom ActivitySources
                .AddSource(DiagnosticsConfig.CheckoutSource.Name)
                .AddSource(DiagnosticsConfig.EventsSource.Name)
                .AddSource(DiagnosticsConfig.DataSource.Name)
                // PII safety net — runs before export
                .AddProcessor(new PiiSanitizingProcessor())
                // OTLP export for traces (Jaeger)
                .AddOtlpExporter(opts => opts.Endpoint = new Uri(otlpEndpoint)))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                // Our custom Meter (matches the one in OrderProcessingService)
                .AddMeter(DiagnosticsConfig.CheckoutMeter.Name)
                .AddMeter("NopCommerce.Checkout")
                // Prometheus scrape endpoint at /metrics
                .AddPrometheusExporter());
    }

    public void Configure(IApplicationBuilder application)
    {
        // Expose /metrics for Prometheus to scrape
        application.UseOpenTelemetryPrometheusScrapingEndpoint();
    }

    public int Order => 5;
}
