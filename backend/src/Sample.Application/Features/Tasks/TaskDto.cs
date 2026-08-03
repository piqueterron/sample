namespace Sample.Application.Features.Tasks;

using Sample.Domain.Tasks;

/// <summary>
/// Read DTO returned by the tasks endpoints. A flat projection of
/// <see cref="TaskItem"/>; never exposes mutation methods.
/// </summary>
public sealed record TaskDto(
    Guid Id,
    string Title,
    string Description,
    string Status,
    string Priority,
    Guid OwnerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);
