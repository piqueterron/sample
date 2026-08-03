namespace Sample.Api.Endpoints.Users;

using global::Mediator;
using Sample.Application.Features.Users.GetUsers;

public sealed class UserEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users")
            .WithTags("Users")
            .RequireAuthorization("admin");

        group.MapGet("/", GetUsersAsync)
            .WithDescription("List all users. Optional `search` query parameter filters by " +
                             "username or email (case-insensitive substring).")
            .WithSummary("List users")
            .Produces<UserQueryResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> GetUsersAsync(
        IMediator mediator,
        string? search,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UserQuery(search), cancellationToken);

        return Results.Ok(result);
    }
}
