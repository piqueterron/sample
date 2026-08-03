namespace Sample.Domain;

/// <summary>
/// Base class for domain entities. Equality is based on the identity
/// (<typeparamref name="TId"/>) rather than reference identity, following
/// the DDD pattern: two entities with the same id are the same entity
/// regardless of the CLR reference they live on.
/// </summary>
/// <typeparam name="TId">The type of the entity's identity. Must be a non-nullable type.</typeparam>
public abstract class Entity<TId>
    where TId : notnull
{
    /// <summary>
    /// The entity's unique identifier. Set via <see cref="SetId"/> by
    /// derived aggregates when the identity has been assigned by a factory
    /// or a repository.
    /// </summary>
    public TId Id { get; protected set; } = default!;

    /// <summary>
    /// Equality is identity-based: two entities of the same runtime type
    /// with the same id are considered equal.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetType() != other.GetType())
        {
            return false;
        }

        // If either side is transient (id not yet assigned), fall back to
        // reference identity to avoid collisions on default(Guid) etc.
        if (IsTransient() || other.IsTransient())
        {
            return false;
        }

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override int GetHashCode()
    {
        return IsTransient()
            ? base.GetHashCode()
            : EqualityComparer<TId>.Default.GetHashCode(Id);
    }

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
    {
        return left?.Equals(right) ?? right is null;
    }

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
    {
        return !(left == right);
    }

    /// <summary>
    /// True when the entity has not yet been assigned an identity (e.g. a
    /// brand-new aggregate before the repository has persisted it).
    /// </summary>
    protected virtual bool IsTransient()
    {
        return Id is null || EqualityComparer<TId>.Default.Equals(Id, default);
    }

    /// <summary>
    /// Sets the identity. Used by derived aggregates and by the persistence
    /// layer when rehydrating from storage. Reserved for repository /
    /// factory use; domain code should go through the aggregate's own
    /// factory methods which assign the id at construction.
    /// </summary>
    protected void SetId(TId id)
    {
        Id = id;
    }
}
