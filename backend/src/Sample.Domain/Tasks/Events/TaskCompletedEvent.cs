namespace Sample.Domain.Tasks.Events;

using Sample.Domain;

/// <summary>
/// Raised when a <see cref="TaskItem"/> transitions to the <c>Done</c>
/// state via <see cref="TaskItem.Complete"/>. Subscribers may emit a Slack
/// notification, update KPIs, or schedule a follow-up task.
/// </summary>
public sealed record TaskCompletedEvent(
    Guid TaskId,
    Guid OwnerId,
    DateTimeOffset OccurredOnUtc) : IDomainEvent;
