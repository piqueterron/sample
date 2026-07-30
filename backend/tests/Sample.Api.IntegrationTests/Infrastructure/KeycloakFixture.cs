namespace Sample.Api.IntegrationTests.Infrastructure;

using System.Net.Http.Json;
using Testcontainers.Keycloak;
using Xunit;

/// <summary>
/// A shared fixture that brings up a minimal Keycloak container (no Postgres,
/// no observability stack) for the integration tests. The realm defined in
/// <c>keycloak/realm-export.json</c> is imported on first boot, so the
/// <c>portal-api</c> public client, its <c>oidc-audience-mapper</c> and the two
/// test users (<c>admin</c>/<c>admin</c>, <c>test</c>/<c>Password123!</c>) are
/// available without any Keycloak UI interaction.
/// </summary>
public sealed class KeycloakFixture : IAsyncLifetime
{
    private const string Realm = "company";
    private const string ImportFile = "keycloak/realm-export.json";
    private const string ClientIdValue = "portal-api";

    private readonly KeycloakContainer _container;

    public KeycloakFixture()
    {
        // The image-based ctor is the supported entry point in Testcontainers.Keycloak 4.13+.
        // WithRealm maps the JSON file to /opt/keycloak/data/import/ and adds the
        // --import-realm command line so Keycloak imports it during startup.
        _container = new KeycloakBuilder("quay.io/keycloak/keycloak:26.2")
            .WithUsername("admin")
            .WithPassword("admin")
            .WithRealm(ImportFile)
            .Build();
    }

    /// <summary>
    /// The browser/host-facing base URL of the Keycloak realm used for both
    /// OIDC discovery (<c>Keycloak:Authority</c>) and token issuance. Both
    /// point to the container's randomized mapped port.
    /// </summary>
    public string Authority => $"{_container.GetBaseAddress().TrimEnd('/')}/realms/{Realm}";

    public string ClientId => ClientIdValue;

    /// <summary>
    /// The Keycloak realm token endpoint used by the API's
    /// <c>POST /auth/token</c> proxy (it constructs it from
    /// <c>Keycloak:Authority</c> if <c>Keycloak:TokenEndpoint</c> is not set,
    /// so leaving this null is equivalent to the host config).
    /// </summary>
    public string TokenEndpoint => $"{Authority}/protocol/openid-connect/token";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Wait until the OIDC discovery document is reachable. The Keycloak
        // container reports "healthy" before the realm import finishes bootstrapping,
        // so the discovery probe is what actually gates us.
        //
        // The discovery URL is built as a full absolute string. A relative URI
        // resolved against an `HttpClient.BaseAddress` that does not end with '/'
        // will *replace* the last segment ("company") instead of extending it,
        // which leads to /realms/.well-known/openid-configuration (404).
        var discoveryUrl = Authority.TrimEnd('/') + "/.well-known/openid-configuration";
        using var http = new HttpClient();

        await WaitStrategy.WaitAsync(
            async () =>
            {
                try
                {
                    using var res = await http.GetAsync(discoveryUrl);
                    return res.IsSuccessStatusCode;
                }
                catch (HttpRequestException)
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(120),
            TimeSpan.FromSeconds(1));
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Issues a real access token via the password grant against the spun-up
    /// Keycloak using the configured <c>admin</c>/</c>admin</c> user (role
    /// <c>admin</c>). Returns the raw JWT string to be used as a Bearer token.
    /// </summary>
    public async Task<string> GetAccessTokenAsync(string username = "admin", string password = "admin")
    {
        // Build the URL as a full absolute string. A relative URI resolved
        // against an `HttpClient.BaseAddress` not ending in '/' replaces the
        // last segment ("company") instead of extending it.
        using var http = new HttpClient();
        var tokenUrl = Authority.TrimEnd('/') + "/protocol/openid-connect/token";

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = ClientId,
            ["username"] = username,
            ["password"] = password,
            ["scope"] = "openid profile email"
        });

        var response = await http.PostAsync(tokenUrl, form);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Keycloak token request to '{tokenUrl}' for user '{username}' failed with {(int)response.StatusCode} {response.StatusCode}: {body}");
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return token?.AccessToken
            ?? throw new InvalidOperationException("Keycloak did not return an access_token.");
    }

    private sealed record TokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken);

    private static class WaitStrategy
    {
        public static async Task WaitAsync(Func<Task<bool>> predicate, TimeSpan timeout, TimeSpan interval)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (await predicate())
                {
                    return;
                }

                await Task.Delay(interval);
            }

            throw new TimeoutException($"Condition not met within {timeout.TotalSeconds:F0}s.");
        }
    }
}
