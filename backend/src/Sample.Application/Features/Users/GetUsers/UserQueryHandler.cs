namespace Sample.Application.Features.Users.GetUsers;

using Mediator;
using Sample.Application.Abstractions;

/// <summary>
/// Lists users. Honors the optional <see cref="UserQuery.Search"/> filter
/// (case-insensitive substring on username OR email). Sorts by username for
/// deterministic output. Read paths query with AsNoTracking on the
/// repository; this handler never mutates state.
/// </summary>
public sealed class UserQueryHandler : IRequestHandler<UserQuery, UserQueryResult>
{
    private readonly IUserRepository _userRepository;

    public UserQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async ValueTask<UserQueryResult> Handle(UserQuery request, CancellationToken cancellationToken)
    {
        var users = await _userRepository.GetAllAsync(cancellationToken);

        IEnumerable<Domain.Users.User> sequence = users;

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // Case-insensitive containment on either username or email. Done in
            // memory since the repository already materialized the collection at
            // this point. For larger datasets, push this filter down into the
            // repository (a `SearchAsync(filter, ct)` backed by ILIKE).
            var search = request.Search.Trim();
            sequence = sequence.Where(u =>
                u.Username.Contains(search, StringComparison.OrdinalIgnoreCase)
                || u.Email.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var dtos = sequence
            .OrderBy(u => u.Username, StringComparer.OrdinalIgnoreCase)
            .Select(u => new UserDto(u.Id, u.Username, u.Email, u.DisplayName, u.CreatedAt))
            .ToList();

        return new UserQueryResult(dtos);
    }
}
