namespace Sample.Domain.Users;

using Sample.Domain.Exceptions;

/// <summary>
/// A user record synced from Keycloak (the API does NOT manage authentication;
/// it stores the reflection of an authenticated identity for read/query use).
/// </summary>
public sealed class User : AuditableEntity<Guid>
{
    /// <summary>
    /// The Keycloak <c>sub</c> claim. Unique across the realm.
    /// </summary>
    public string KeycloakSubject { get; private set; } = string.Empty;

    public string Username { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// Factory used by the application layer when materializing a user from
    /// Keycloak claims. Invariants (non-empty subject, username, email) are
    /// enforced here.
    /// </summary>
    public static User Create(string keycloakSubject, string username, string email, string displayName)
    {
        if (string.IsNullOrWhiteSpace(keycloakSubject))
        {
            throw new DomainException("A user requires a non-empty Keycloak subject ('sub').");
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new DomainException("A user requires a non-empty username.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("A user requires a non-empty email.");
        }

        var user = new User
        {
            KeycloakSubject = keycloakSubject,
            Username = username,
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName
        };

        return user;
    }

    /// <summary>
    /// Rehydrates a user from persistence. Reserved for EF Core; domain code
    /// uses the <see cref="Create"/> factory.
    /// </summary>
    public static User Rehydrate(
        Guid id,
        string keycloakSubject,
        string username,
        string email,
        string displayName,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        var user = new User
        {
            KeycloakSubject = keycloakSubject,
            Username = username,
            Email = email,
            DisplayName = displayName
        };

        user.SetId(id);
        user.MarkCreated(createdAt);
        user.MarkUpdated(updatedAt);

        return user;
    }
}
