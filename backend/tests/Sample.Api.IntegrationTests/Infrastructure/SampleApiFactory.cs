namespace Sample.Api.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

/// <summary>
/// Bootstraps the real <c>Sample.Api</c> process in-memory using
/// <see cref="WebApplicationFactory{TEntryPoint}"/> and rewires its
/// configuration so the OIDC/JwtBearer stack points at the Keycloak
/// instance spun up by <see cref="KeycloakFixture"/> instead of the
/// dev <c>localhost:8080</c> realm.
/// </summary>
/// <remarks>
/// The factory also disables the OpenTelemetry OTLP exporter by overriding
/// <c>Otel:Endpoint:Default</c> with an unreachable placeholder - the
/// integration test suite deliberately does not bring up the Grafana LGTM
/// observability stack (Alloy / Tempo / Loki / Prometheus), which adds nothing
/// to the assertions and would only slow the run down.
/// </remarks>
public sealed class SampleApiFactory : WebApplicationFactory<Program>
{
    private readonly KeycloakFixture _keycloak;

    public SampleApiFactory(KeycloakFixture keycloak)
    {
        _keycloak = keycloak;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // IMPORTANT: `Program.cs` reads `builder.Configuration["Keycloak:Authority"]`
        // *during its top-level execution*, which happens before any
        // `ConfigureAppConfiguration` callback runs. Therefore the overrides MUST
        // use `UseSetting` (which feeds the pre-built IConfiguration that the
        // entrypoint sees) rather than an `AddInMemoryCollection` layer added
        // after the host has already been constructed.
        builder.UseEnvironment("IntegrationTest");
        builder.UseSetting("Keycloak:Authority", _keycloak.Authority);
        builder.UseSetting("Keycloak:Audience", "portal-api");
        builder.UseSetting("Keycloak:Realm", "company");
        builder.UseSetting("Keycloak:ClientId", _keycloak.ClientId);
        builder.UseSetting("Keycloak:TokenEndpoint", _keycloak.TokenEndpoint);
        builder.UseSetting("Keycloak:RequireHttpsMetadata", "false");
        builder.UseSetting("Keycloak:ValidIssuers", _keycloak.Authority);

        // The OTLP exporter reads these on startup. Pointing them at an
        // endpoint nothing listens on keeps OpenTelemetry's attempts
        // quick/non-fatal while we exercise HTTP paths. The integration test
        // suite deliberately does NOT bring up the Grafana LGTM observability
        // stack (Alloy / Tempo / Loki / Prometheus).
        builder.UseSetting("Otel:Endpoint:Default", "http://127.0.0.1:0");
        builder.UseSetting("Otel:Endpoint:Traces", "http://127.0.0.1:0/v1/traces");
        builder.UseSetting("Otel:Endpoint:Metrics", "http://127.0.0.1:0/v1/metrics");
    }
}
