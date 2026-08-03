namespace Sample.Api.Endpoints.Tasks;

using global::Mediator;
using Microsoft.AspNetCore.Http;
using Sample.Application.Features.Tasks;
using Sample.Application.Features.Tasks.CompleteTask;
using Sample.Application.Features.Tasks.CreateTask;
using Sample.Application.Features.Tasks.ListTasks;
using Sample.Domain.Tasks;

public sealed class TasksEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tasks")
            .WithTags("Tasks")
            .RequireAuthorization("admin");

        group.MapPost("/", CreateTaskAsync)
            .WithSummary("Create a task")
            .WithDescription("Creates a new pending task for the given owner.")
            .Accepts<CreateTaskCommand>("application/json")
            .Produces<CreateTaskResult>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", ListTasksAsync)
            .WithSummary("List tasks")
            .WithDescription("Returns all tasks, optionally filtered by `ownerId` and/or `status`.")
            .Produces<ListTasksResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/{id:guid}/complete", CompleteTaskAsync)
            .WithSummary("Complete a task")
            .WithDescription("Transitions an InProgress task to Done. Returns 404 if the task does not exist, 409 if the state transition is not allowed.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> CreateTaskAsync(
        IMediator mediator,
        CreateTaskCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        return Results.Created($"/tasks/{result.Task.Id}", result);
    }

    private static async Task<IResult> ListTasksAsync(
        IMediator mediator,
        Guid? ownerId,
        string? status,
        CancellationToken cancellationToken)
    {
        TaskStatus? statusEnum = null;

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<TaskStatus>(status, ignoreCase: true, out var parsed))
        {
            statusEnum = parsed;
        }

        var result = await mediator.Send(new ListTasksQuery(ownerId, statusEnum), cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> CompleteTaskAsync(
        IMediator mediator,
        Guid id,
        CancellationToken cancellationToken)
    {
        await mediator.Send(new CompleteTaskCommand(id), cancellationToken);

        return Results.NoContent();
    }
}
