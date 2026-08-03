namespace Sample.Domain;

/// <summary>
/// Base class for value objects: immutable objects compared by the values of
/// their components rather than by reference identity (e.g. <c>Money</c>,
/// <c>EmailAddress</c>, <c>TaskTitle</c>). Enforces the canonical
/// <c>equals/getHashCode</c> contract by reflecting over the implementing
/// type's atomic components.
/// </summary>
/// <remarks>
/// Derived types must override <see cref="GetEqualityComponents"/> and return
/// each component that participates in equality, in declaration order.
/// Components may be <c>null</c>; they are compared with
/// <c>EqualityComparer&lt;object&gt;.Default</c>.
/// </remarks>
public abstract class ValueObject
{
    /// <summary>
    /// The atomic components that participate in equality. Derived types
    /// <c>yield return</c> each component.
    /// </summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType())
        {
            return false;
        }

        var other = (ValueObject)obj;

        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override int GetHashCode()
    {
        // XOR generates a stable hash that depends on every component's
        // value, regardless of order. Order-dependence is intentionally
        // discarded because component order is itself part of the type's
        // definition and thus already consistent across instances.
        return GetEqualityComponents()
            .Aggregate(0, (hash, obj) => hash ^ (obj?.GetHashCode() ?? 0));
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        return left?.Equals(right) ?? right is null;
    }

    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return !(left == right);
    }
}
