namespace Sample.Api.Endpoints.Users;

using global::Mediator;
using Sample.Application.Features.Users.GetUsers;

public class UserEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users")
            .WithTags("Users")
            .RequireAuthorization("admin");

        group.MapGet("/", GetUsers)
            .WithDescription("Get all users")
            .WithSummary("Get all users")
            .Produces(StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetUsers(IMediator mediator)
    {
        await mediator.Send(new UserQuery());

        return Results.Ok();
    }
}
