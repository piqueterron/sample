namespace Sample.Domain;

/// <summary>
/// Marker interface for a domain event raised by an aggregate root. Domain
/// events represent something that happened in the past (they are immutable
/// and named in past tense: <c>TaskCompleted</c>, <c>UserRegistered</c>).
/// The dispatcher is wired in infrastructure and is a no-op by default; see
/// <c>Infrastructure/DomainEvents/LoggingDomainEventDispatcher</c>.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// When the event was raised. Always UTC.
    /// </summary>
    DateTimeOffset OccurredOnUtc { get; }
}
