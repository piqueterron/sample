namespace Sample.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Sample.Application.Abstractions;
using Sample.Domain.Tasks;
using Sample.Infrastructure.Persistence;

public sealed class TaskRepository : ITaskRepository
{
    private readonly SampleDbContext _context;

    public TaskRepository(SampleDbContext context)
    {
        _context = context;
    }

    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        // Tracked on purpose: the caller (typically a command handler) will
        // mutate the aggregate, then call SaveChangesAsync to commit it.
        return _context.Set<TaskItem>()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<TaskItem>> GetAllAsync(Guid? ownerIdFilter, CancellationToken cancellationToken)
    {
        var query = _context.Set<TaskItem>().AsNoTracking();

        if (ownerIdFilter is { } ownerId)
        {
            query = query.Where(t => t.OwnerId == ownerId);
        }

        return await query
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public void Add(TaskItem task)
    {
        _context.Set<TaskItem>().Add(task);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        // The repository deliberately owns SaveChanges for the TaskItem
        // aggregate so domain events can be dispatched after a successful
        // commit (currently a no-op logging dispatcher; see
        // LoggingDomainEventDispatcher when it is wired up).
        return _context.SaveChangesAsync(cancellationToken);
    }
}
