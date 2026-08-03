namespace Sample.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using Sample.Application.Abstractions;
using Sample.Domain.Users;
using Sample.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IUserRepository"/>. Lives in
/// <c>Sample.Infrastructure</c> so the application layer depends only on the
/// abstraction defined there. The <c>SampleDbContext</c> is injected with a
/// scoped lifetime to match the Mediator's own lifetime and that of the
/// HTTP request.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly SampleDbContext _context;

    public UserRepository(SampleDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.Username)
            .ToListAsync(cancellationToken);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public Task<User?> GetByKeycloakSubjectAsync(string keycloakSubject, CancellationToken cancellationToken)
    {
        return _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.KeycloakSubject == keycloakSubject, cancellationToken);
    }

    public void Add(User user)
    {
        _context.Users.Add(user);
    }

    public void Remove(User user)
    {
        _context.Users.Remove(user);
    }
}
