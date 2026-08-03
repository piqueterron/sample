namespace Sample.Application.Features.Users.GetUsers;

/// <summary>
/// Read DTO returned by the <c>GET /users</c> endpoint. Fields are a flat
/// projection of <c>User</c>; we never leak the aggregate's mutation surface
/// (no setter-style properties) to the API surface.
/// </summary>
public sealed record UserDto(
    Guid Id,
    string Username,
    string Email,
    string DisplayName,
    DateTimeOffset CreatedAt);
