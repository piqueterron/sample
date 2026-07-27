namespace Sample.Api.Extensions;

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Sample;

public static class ObservabilityExtensions
{
    public const string ActivitySourceName = "Sample.Api";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceName = configuration["Otel:ServiceName"] ?? "sample-api";
        var serviceVersion = configuration["Otel:ServiceVersion"] ?? "1.0.0";
        var environment = configuration["Otel:Environment"] ?? "development";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("deployment.environment", environment)
                }))
            .WithTracing(tracing => tracing
                .AddSource(ActivitySourceName)
                .AddSource(Sample.Mediator.ActivitySourceName)
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = httpContext =>
                        !httpContext.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(options => ConfigureOtlp(options, configuration, "Traces")))
            .WithMetrics(metrics => metrics
                .AddMeter(ActivitySourceName)
                .AddMeter(Sample.Mediator.MeterName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddOtlpExporter((exporterOptions, _) => ConfigureOtlp(exporterOptions, configuration, "Metrics")));

        return services;
    }

    private static void ConfigureOtlp(OtlpExporterOptions options, IConfiguration configuration, string signal)
    {
        var protocol = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL")
            ?? configuration["Otel:Protocol"]
            ?? "http/protobuf";

        options.Protocol = string.Equals(protocol, "grpc", StringComparison.OrdinalIgnoreCase)
            ? OtlpExportProtocol.Grpc
            : OtlpExportProtocol.HttpProtobuf;

        var signalUpper = signal.ToUpperInvariant();
        var signalLower = signal.ToLowerInvariant();

        var baseEndpoint =
            Environment.GetEnvironmentVariable($"OTEL_EXPORTER_OTLP_{signalUpper}_ENDPOINT")
            ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
            ?? configuration[$"Otel:Endpoint:{signal}"]
            ?? configuration["Otel:Endpoint:Default"]
            ?? "http://localhost:4318";

        var uri = new Uri(baseEndpoint);

        if (string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/")
        {
            baseEndpoint = baseEndpoint.TrimEnd('/') + $"/v1/{signalLower}";
        }

        options.Endpoint = new Uri(baseEndpoint);
    }
}
