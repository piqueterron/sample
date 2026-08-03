namespace Sample.Application.Features.Tasks.CompleteTask;

using FluentValidation;
using Mediator;
using Sample.Application.Abstractions;
using Sample.Domain.Exceptions;
using Sample.Domain.Tasks;

/// <summary>
/// Marks an existing InProgress task as Done. Raises a
/// <see cref="TaskCompletedEvent"/> on the aggregate.
/// </summary>
public sealed record CompleteTaskCommand(Guid TaskId) : IRequest;

public sealed class CompleteTaskCommandValidator : AbstractValidator<CompleteTaskCommand>
{
    public CompleteTaskCommandValidator()
    {
        RuleFor(x => x.TaskId)
            .NotEqual(Guid.Empty).WithMessage("TaskId must be a non-empty GUID.");
    }
}

public sealed class CompleteTaskCommandHandler : IRequestHandler<CompleteTaskCommand>
{
    private readonly ITaskRepository _taskRepository;

    public CompleteTaskCommandHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async ValueTask<Unit> Handle(CompleteTaskCommand request, CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new DomainException($"No task with id '{request.TaskId}' was found.");

        task.Complete();

        await _taskRepository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
