namespace Sample.Application.Features.Tasks.CreateTask;

using FluentValidation;
using Mediator;
using Sample.Application.Abstractions;
using Sample.Domain.Tasks;

/// <summary>
/// Creates a new pending task for the given owner.
/// </summary>
public sealed record CreateTaskCommand(
    string Title,
    string? Description,
    Guid OwnerId,
    TaskPriority Priority = TaskPriority.Medium) : IRequest<CreateTaskResult>;

public sealed record CreateTaskResult(TaskDto Task);

/// <summary>
/// Validates <see cref="CreateTaskCommand"/>. Mirrors the
/// <see cref="TaskTitle"/> invariant but is enforced at the boundary to give
/// early 400s with clear field-level errors (the domain validation still
/// throws <c>DomainException</c> as a defense-in-depth measure).
/// </summary>
public sealed class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("A task title is required.")
            .MaximumLength(TaskTitle.MaxLength).WithMessage($"Title cannot exceed {TaskTitle.MaxLength} characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).When(x => x.Description is not null);

        RuleFor(x => x.OwnerId)
            .NotEqual(Guid.Empty).WithMessage("OwnerId must be a non-empty GUID.");

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage("Priority must be a valid TaskPriority enum value.");
    }
}

public sealed class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, CreateTaskResult>
{
    private readonly ITaskRepository _taskRepository;

    public CreateTaskCommandHandler(ITaskRepository taskRepository)
    {
        _taskRepository = taskRepository;
    }

    public async ValueTask<CreateTaskResult> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        // Domain factory enforces TaskTitle + OwnerId invariants; if the request
        // passed the validator, this should always succeed. Defense in depth:
        // a DomainException thrown here surfaces as 409 rather than 400.
        var task = TaskItem.Create(request.Title, request.OwnerId, request.Description, request.Priority);

        _taskRepository.Add(task);
        await _taskRepository.SaveChangesAsync(cancellationToken);

        return new CreateTaskResult(MapToDto(task));
    }

    internal static TaskDto MapToDto(TaskItem task) => new(
        task.Id,
        task.Title.Value,
        task.Description,
        task.Status.ToString(),
        task.Priority.ToString(),
        task.OwnerId,
        task.CreatedAt,
        task.UpdatedAt,
        task.CompletedAt);
}
