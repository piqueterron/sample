namespace Sample.Domain.Tasks;

/// <summary>
/// Lifecycle states of a <see cref="TaskItem"/>. Stored as a string in
/// PostgreSQL for human-readable values in the DB and audit logs.
/// </summary>
public enum TaskStatus
{
    /// <summary>The task has been created but no work has started.</summary>
    Pending = 0,

    /// <summary>Someone is actively working on the task.</summary>
    InProgress = 1,

    /// <summary>The task has been completed successfully.</summary>
    Done = 2,

    /// <summary>The task was abandoned without completion.</summary>
    Cancelled = 3
}
