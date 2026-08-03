namespace Sample.Domain;

/// <summary>
/// Base class for entities that track creation and last-modification
/// timestamps. Persisted by EF Core as <c>CreatedAt</c> (UTC, immutable)
/// and <c>UpdatedAt</c> (UTC, refreshed on every write).
/// </summary>
public abstract class AuditableEntity<TId> : Entity<TId>
    where TId : notnull
{
    public DateTimeOffset CreatedAt { get; protected set; }

    public DateTimeOffset UpdatedAt { get; protected set; }

    /// <summary>
    /// Called by the repository / persistence layer when the entity is first
    /// persisted. Domain code does NOT set this directly; the timestamp is
    /// an infrastructure concern exposed for read-only consumption.
    /// </summary>
    public void MarkCreated(DateTimeOffset at)
    {
        CreatedAt = at;
        UpdatedAt = at;
    }

    /// <summary>
    /// Called by the persistence layer on every subsequent write.
    /// </summary>
    public void MarkUpdated(DateTimeOffset at)
    {
        UpdatedAt = at;
    }
}
