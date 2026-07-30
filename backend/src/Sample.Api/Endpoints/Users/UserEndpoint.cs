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
            .WithDescription("Get all users")
            .WithSummary("Get all users")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> GetUsersAsync(IMediator mediator, CancellationToken cancellationToken)
    {
        await mediator.Send(new UserQuery(), cancellationToken);

        return Results.Ok();
    }
}
