namespace Sample.Application.Features.Users.GetUsers;

using FluentValidation;

/// <summary>
/// Validates <see cref="UserQuery"/>. Prevents pathological inputs from
/// reaching the database (e.g. a 10K char search string would produce a
/// LIKE query that bloats the plan cache). Validation runs via the
/// pipeline behavior registered in <c>BuilderExtensions.AddDispatcher</c>.
/// </summary>
public sealed class UserQueryValidator : AbstractValidator<UserQuery?>
{
    public UserQueryValidator()
    {
        // Optional search filter. When provided, bounded between 1 and 100
        // characters to keep the LIKE predicate bounded.
        RuleFor(x => x!.Search)
            .MaximumLength(100)
            .When(x => !string.IsNullOrWhiteSpace(x?.Search));
    }
}
