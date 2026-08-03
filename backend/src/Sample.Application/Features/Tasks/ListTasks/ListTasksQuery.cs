namespace Sample.Application.Features.Tasks.ListTasks;

using Mediator;
using Sample.Application.Abstractions;
using Sample.Application.Features.Tasks.CreateTask;
using Sample.Domain.Tasks;

/// <summary>
/// Lists tasks. Optional <see cref="OwnerId"/> filter; optional status filter.
/// </summary>
public sealed record ListTasksQuery(Guid? OwnerId, TaskStatus? Status = null) : IRequest<ListTasksResult>;

public sealed record ListTasksResult(IReadOnlyList<TaskDto> Tasks);

public sealed class ListTasksQueryHandler : IRequestHandler<ListTasksQuery, ListTasksResult>
{
    private readonly ITaskRepository _taskRepository;

    public ListTasksQueryHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async ValueTask<ListTasksResult> Handle(ListTasksQuery request, CancellationToken cancellationToken)
    {
        var tasks = await _taskRepository.GetAllAsync(request.OwnerId, cancellationToken);

        IEnumerable<TaskItem> sequence = tasks;

        if (request.Status is not null)
        {
            sequence = sequence.Where(t => t.Status == request.Status);
        }

        var dtos = sequence
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .Select(CreateTaskCommandHandler.MapToDto)
            .ToList();

        return new ListTasksResult(dtos);
    }
}
