namespace Sample.Domain.Tasks;

using Sample.Domain.Exceptions;

/// <summary>
/// Represents the title of a <see cref="TaskItem"/>. Enforces the invariant
/// "non-empty, 1-200 characters, no leading/trailing whitespace only" so any
/// constructed <c>TaskTitle</c> is a valid title by definition.
/// </summary>
/// <remarks>
/// This is a ValueObject: two <c>TaskTitle</c>s with the same value (after
/// trimming) are considered equal. Persistence as an owned value type is
/// handled by EF Core's <c>OwnsOne</c> in <c>TaskItemConfiguration</c>.
/// </remarks>
public sealed class TaskTitle : ValueObject
{
    public const int MaxLength = 200;
    public const int MinLength = 1;

    public string Value { get; private set; } = string.Empty;

    private TaskTitle(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a <c>TaskTitle</c> from raw input. Throws a
    /// <see cref="DomainException"/> if the invariant is violated.
    /// </summary>
    public static TaskTitle Create(string? raw)
    {
        if (raw is null)
        {
            throw new DomainException("A task title is required.");
        }

        var value = raw.Trim();

        if (value.Length < MinLength)
        {
            throw new DomainException($"A task title must be at least {MinLength} character long.");
        }

        if (value.Length > MaxLength)
        {
            throw new DomainException($"A task title cannot exceed {MaxLength} characters.");
        }

        return new TaskTitle(value);
    }

    /// <summary>
    /// Rehydrate from persistence without re-validation (the DB column-level
    /// constraints are assumed to hold for rows that already exist). Reserved
    /// for EF Core.
    /// </summary>
    public static TaskTitle Rehydrate(string value) => new(value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        // Use ordinal comparison; no normalization beyond the constructor trim,
        // so "Foo" and "foo" are NOT equal but they ARE both valid.
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(TaskTitle title) => title.Value;
}
