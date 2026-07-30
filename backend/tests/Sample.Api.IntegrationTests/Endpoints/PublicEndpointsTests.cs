namespace Sample.Api.IntegrationTests.Endpoints;

using System.Net.Http.Json;
using Sample.Api.IntegrationTests.Infrastructure;
using Xunit;

/// <summary>
/// End-to-end coverage of the public, non-authenticated endpoints of the
/// API. Each test runs entirely in-process against a real
/// <c>WebApplicationFactory&lt;Program&gt;</c> instance backed by the shared
/// Keycloak container.
/// </summary>
public sealed class PublicEndpointsTests : IntegrationTestBase
{
    public PublicEndpointsTests(KeycloakFixture keycloak) : base(keycloak)
    {
    }

    [Fact]
    public async Task GetHealth_Returns200()
    {
        var response = await Client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task PostToken_WithAdminCredentials_Returns200AndAccessToken()
    {
        var response = await Client.PostAsJsonAsync(
            "/auth/token",
            new { username = "admin", password = "admin", scope = "openid profile email" });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>()
            ?? throw new InvalidOperationException("Expected non-null token response.");

        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    [Fact]
    public async Task PostToken_WithMissingCredentials_Returns400()
    {
        var response = await Client.PostAsJsonAsync(
            "/auth/token",
            new { username = "", password = "", scope = "openid" });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record TokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken);
}
