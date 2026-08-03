namespace Sample.Domain.Tasks;

using Sample.Domain;
using Sample.Domain.Exceptions;
using Sample.Domain.Tasks.Events;

/// <summary>
/// Aggregate root representing a todo item. Enforces all state transitions
/// and raises domain events (<see cref="TaskCompletedEvent"/>) on the
/// transitions that matter business-wise.
/// </summary>
/// <remarks>
/// Invariants enforced by the aggregate:
/// <list type="bullet">
///   <item>Title must be a valid <see cref="TaskTitle"/> (non-empty, &lt;=200 chars).</item>
///   <item>OwnerId must be set (no orphan tasks).</item>
///   <item><see cref="Start"/> only valid from <see cref="TaskStatus.Pending"/>.</item>
///   <item><see cref="Complete"/> only valid from <see cref="TaskStatus.InProgress"/>.</item>
///   <item><see cref="Cancel"/> not valid from <see cref="TaskStatus.Done"/>.</item>
/// </list>
/// </remarks>
public sealed class TaskItem : AggregateRoot<Guid>
{
    public TaskTitle Title { get; private set; } = null!;
    public string Description { get; private set; } = string.Empty;
    public TaskStatus Status { get; private set; }
    public TaskPriority Priority { get; private set; }
    public Guid OwnerId { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    // --- Factories ---

    /// <summary>
    /// Creates a brand-new pending task. Title invariants enforced by
    /// <see cref="TaskTitle.Create"/>. Throws on invariant violation.
    /// </summary>
    public static TaskItem Create(
        string title,
        Guid ownerId,
        string? description = null,
        TaskPriority priority = TaskPriority.Medium)
    {
        var titleVo = TaskTitle.Create(title);

        if (ownerId == Guid.Empty)
        {
            throw new DomainException("A task must have a non-empty owner id.");
        }

        var task = new TaskItem
        {
            Title = titleVo,
            Description = description ?? string.Empty,
            Priority = priority,
            Status = TaskStatus.Pending,
            OwnerId = ownerId
        };

        return task;
    }

    /// <summary>
    /// Rehydrates a <see cref="TaskItem"/> from persistence. Reserved for EF
    /// Core: domain code uses the <see cref="Create"/> factory.
    /// </summary>
    public static TaskItem Rehydrate(
        Guid id,
        TaskTitle title,
        string description,
        TaskStatus status,
        TaskPriority priority,
        Guid ownerId,
        DateTimeOffset? completedAt,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        var task = new TaskItem
        {
            Title = title,
            Description = description,
            Status = status,
            Priority = priority,
            OwnerId = ownerId,
            CompletedAt = completedAt
        };

        task.SetId(id);
        task.MarkCreated(createdAt);
        task.MarkUpdated(updatedAt);

        return task;
    }

    // --- State transitions (command methods) ---

    /// <summary>
    /// Marks the task as started. Only valid from <see cref="TaskStatus.Pending"/>.
    /// </summary>
    public void Start()
    {
        if (Status != TaskStatus.Pending)
        {
            throw new DomainException($"Cannot start a task that is currently '{Status}'.");
        }

        Status = TaskStatus.InProgress;
    }

    /// <summary>
    /// Marks the task as completed. Only valid from
    /// <see cref="TaskStatus.InProgress"/>. Raises a
    /// <see cref="TaskCompletedEvent"/>.
    /// </summary>
    public void Complete(DateTimeOffset? completedAtUtc = null)
    {
        if (Status != TaskStatus.InProgress)
        {
            throw new DomainException($"Cannot complete a task that is currently '{Status}'.");
        }

        Status = TaskStatus.Done;
        CompletedAt = completedAtUtc ?? DateTimeOffset.UtcNow;

        Raise(new TaskCompletedEvent(Id, OwnerId, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Cancels the task. Not valid from <see cref="TaskStatus.Done"/> (a done
    /// task is a permanent record, not cancellable post-hoc).
    /// </summary>
    public void Cancel()
    {
        if (Status == TaskStatus.Done)
        {
            throw new DomainException("A completed task cannot be cancelled.");
        }
        if (Status == TaskStatus.Cancelled)
        {
            // Idempotent: cancelling an already-cancelled task is a no-op
            // rather than an error, matching user expectations on the UI.
            return;
        }

        Status = TaskStatus.Cancelled;
    }

    /// <summary>
    /// Reassigns the title. Enforces the same invariant as the create path:
    /// only valid <see cref="TaskTitle"/>s accepted.
    /// </summary>
    public void UpdateTitle(string title)
    {
        Title = TaskTitle.Create(title);
    }

    /// <summary>
    /// Reassigns the description. Empty is allowed (description is optional).
    /// </summary>
    public void UpdateDescription(string? description)
    {
        Description = description ?? string.Empty;
    }

    public void ReassignTo(Guid newOwnerId)
    {
        if (newOwnerId == Guid.Empty)
        {
            throw new DomainException("A task must be assigned to a non-empty owner id.");
        }

        OwnerId = newOwnerId;
    }

    public void ChangePriority(TaskPriority priority)
    {
        Priority = priority;
    }
}
