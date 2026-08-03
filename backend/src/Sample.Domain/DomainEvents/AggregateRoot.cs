namespace Sample.Domain;

/// <summary>
/// Implemented by aggregate roots (or any entity) that collect domain events
/// raised during a unit of work. The repository dispatches them after a
/// successful <c>SaveChanges</c>.
/// </summary>
public interface IHasDomainEvents
{
    /// <summary>Events raised since the last <see cref="ClearDomainEvents"/>.</summary>
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>
    /// Clears the in-memory event buffer. Called by the infrastructure after
    /// dispatching the events, so an aggregate does not re-raise them on
    /// the next save.
    /// </summary>
    void ClearDomainEvents();
}

/// <summary>
/// Convenience base class: an entity that aggregates domain events raised
/// during a unit of work. Aggregate roots should derive from this type
/// instead of <see cref="Entity{TId}"/> when they need to publish events.
/// </summary>
public abstract class AggregateRoot<TId> : AuditableEntity<TId>, IHasDomainEvents
    where TId : notnull
{
    private readonly List<IDomainEvent> _events = [];

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _events;

    /// <summary>
    /// Raises a domain event. Event constructors should be the ONLY way
    /// domain code raises events, so the event's invariants (Ids, values)
    /// are guaranteed consistent.
    /// </summary>
    protected void Raise(IDomainEvent @event)
    {
        if (@event is null)
        {
            throw new ArgumentNullException(nameof(@event));
        }

        _events.Add(@event);
    }

    public void ClearDomainEvents()
    {
        _events.Clear();
    }
}
