namespace Sample.Application.Abstractions;

using Sample.Domain.Users;

/// <summary>
/// Read/write repository abstraction for the <see cref="User"/> aggregate.
/// Defined in the application layer so use cases depend on the abstraction,
/// not on the EF Core implementation in <c>Sample.Infrastructure</c>.
/// </summary>
public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> GetByKeycloakSubjectAsync(string keycloakSubject, CancellationToken cancellationToken);

    void Add(User user);

    void Remove(User user);
}
