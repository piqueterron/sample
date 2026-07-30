namespace Sample.Api.IntegrationTests.Infrastructure;

using System.Net.Http.Headers;
using Xunit;

/// <summary>
/// xUnit collection that shares the single <see cref="KeycloakFixture"/>
/// (and therefore the single Keycloak container) across every test in the
/// <see cref="IntegrationTestCollection"/>. The fixture lifetime matches the
/// collection lifetime, so the container is started once per test run and
/// torn down at the end.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<KeycloakFixture>
{
    public const string Name = "IntegrationTests";
}

/// <summary>
/// Base class shared by every integration test class. Exposes the
/// pre-configured <see cref="HttpClient"/> so concrete tests only have to
/// state their HTTP request and assert the response.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    private readonly SampleApiFactory _factory;

    protected IntegrationTestBase(KeycloakFixture keycloak)
    {
        _factory = new SampleApiFactory(keycloak);
        Keycloak = keycloak;
        Client = _factory.CreateClient();
    }

    protected HttpClient Client { get; }

    protected KeycloakFixture Keycloak { get; }

    protected Task<string> GetAdminTokenAsync() => Keycloak.GetAccessTokenAsync("admin", "admin");

    protected Task<string> GetUserTokenAsync() => Keycloak.GetAccessTokenAsync("test", "Password123!");

    protected HttpClient CreateClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
    }
}
