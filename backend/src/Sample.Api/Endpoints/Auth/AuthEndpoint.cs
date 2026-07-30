namespace Sample.Api.Endpoints.Auth;

using System.Net.Http.Headers;
using System.Net.Mime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

public sealed class AuthEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth")
            .WithTags("Auth");

        group.MapPost("/token", ExchangeAsync)
            .WithDescription("""
                Exchange administrator credentials for a Keycloak access token (password grant).
                Use the returned access_token as Bearer scheme against protected endpoints such as GET /users.
            """)
            .WithSummary("Get a token from Keycloak")
            .AllowAnonymous()
            .Accepts<TokenRequest>(MediaTypeNames.Application.FormUrlEncoded)
            .Accepts<TokenRequest>(MediaTypeNames.Application.Json)
            .Produces<string>(StatusCodes.Status200OK, contentType: MediaTypeNames.Application.Json)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status502BadGateway);
    }

    private static async Task<IResult> ExchangeAsync(HttpContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var tokenRequest = await ReadTokenRequestAsync(context, cancellationToken);
        if (tokenRequest is null
            || string.IsNullOrWhiteSpace(tokenRequest.Username)
            || string.IsNullOrWhiteSpace(tokenRequest.Password))
        {
            return Problem(
                StatusCodes.Status400BadRequest,
                "Invalid credentials",
                "Both 'username' and 'password' are required.");
        }

        var clientId = configuration["Keycloak:ClientId"] ?? "portal-api";
        var tokenEndpoint = configuration["Keycloak:TokenEndpoint"];

        if (string.IsNullOrWhiteSpace(tokenEndpoint))
        {
            var authority = configuration["Keycloak:Authority"] ?? throw new InvalidOperationException("Keycloak:Authority is not configured.");
            tokenEndpoint = authority.TrimEnd('/') + "/protocol/openid-connect/token";
        }

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = clientId,
            ["username"] = tokenRequest.Username,
            ["password"] = tokenRequest.Password,
            ["scope"] = tokenRequest.Scope ?? "openid profile email"
        });

        var client = httpClientFactory.CreateClient("keycloak-token");

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = form
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));

        HttpResponseMessage response;

        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return Problem(
                StatusCodes.Status502BadGateway,
                "Keycloak unreachable",
                ex.Message);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        return Results.Content(body, MediaTypeNames.Application.Json, statusCode: (int)response.StatusCode);
    }

    private static IResult Problem(int status, string title, string detail)
    {
        var payload = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };

        return Results.Json(payload, statusCode: status, contentType: MediaTypeNames.Application.ProblemJson);
    }

    private static async Task<TokenRequest?> ReadTokenRequestAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var contentType = context.Request.ContentType?.Split(';')[0].Trim().ToLowerInvariant();

        if (contentType == "application/x-www-form-urlencoded")
        {
            var form = await context.Request.ReadFormAsync(cancellationToken);

            string? username = form["username"];
            string? password = form["password"];
            string? scope = form["scope"];

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                // Tolerate the empty-form case by returning null so the caller returns 400.
                return null;
            }

            return new TokenRequest(username, password, scope);
        }

        return await context.Request.ReadFromJsonAsync<TokenRequest>(cancellationToken);
    }
}

public sealed record TokenRequest(string Username, string Password, string? Scope);
