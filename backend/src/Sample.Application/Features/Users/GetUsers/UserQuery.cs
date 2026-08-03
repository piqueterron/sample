namespace Sample.Application.Features.Users.GetUsers;

using Mediator;

/// <summary>
/// Lists users. <paramref name="Search"/> is an optional case-insensitive
/// substring filter applied to either <c>Username</c> or <c>Email</c>; when
/// null/empty the full collection is returned capped by sane defaults.
/// </summary>
public sealed record UserQuery(string? Search = null) : IRequest<UserQueryResult>;

public sealed record UserQueryResult(IReadOnlyList<UserDto> Users);
