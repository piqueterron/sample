namespace Sample.Application.Abstractions;

using Sample.Domain.Tasks;

/// <summary>
/// Read/write repository abstraction for the <see cref="TaskItem"/> aggregate.
/// </summary>
public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskItem>> GetAllAsync(Guid? ownerIdFilter, CancellationToken cancellationToken);

    void Add(TaskItem task);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
