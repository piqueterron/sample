namespace Sample.Api.IntegrationTests.Endpoints;

using System.Net;
using Sample.Api.IntegrationTests.Infrastructure;
using Xunit;

/// <summary>
/// Authorization matrix for the <c>GET /users</c> endpoint, whose policy
/// <c>admin</c> resolves roles out of the <c>realm_access.roles</c> JSON
/// claim emitted by Keycloak (see <c>BuilderExtensions.AddKeycloakAuth</c>).
///
/// Scenarios covered (mirrors the matrix documented in README.md):
/// <list type="table">
///   <item><term>admin role token</term><description><c>200 OK</c></description></item>
///   <item><term>user role token</term><description><c>403 Forbidden</c></description></item>
///   <item><term>no token</term><description><c>401 Unauthorized</c></description></item>
/// </list>
/// Tokens are real JWTs minted by the Testcontainers Keycloak instance against
/// the <c>portal-api</c> public client (which carries the
/// <c>oidc-audience-mapper</c> required by the JwtBearer audience validation).
/// </summary>
public sealed class UsersEndpointAuthorizationTests : IntegrationTestBase
{
    public UsersEndpointAuthorizationTests(KeycloakFixture keycloak) : base(keycloak)
    {
    }

    [Fact]
    public async Task GetUsers_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_WithAdminToken_Returns200()
    {
        var token = await GetAdminTokenAsync();
        using var adminClient = CreateClientWithToken(token);

        var response = await adminClient.GetAsync("/users");

        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_WithUserRoleToken_Returns403()
    {
        var token = await GetUserTokenAsync();
        using var userClient = CreateClientWithToken(token);

        var response = await userClient.GetAsync("/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_WithGarbageToken_Returns401()
    {
        using var badClient = CreateClientWithToken("not-a-valid-jwt");

        var response = await badClient.GetAsync("/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
