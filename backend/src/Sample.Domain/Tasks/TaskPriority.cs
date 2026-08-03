namespace Sample.Domain.Tasks;

/// <summary>
/// Priority of a <see cref="TaskItem"/>. Higher numeric value = higher
/// priority. Ordered Low &lt; Medium &lt; High.
/// </summary>
public enum TaskPriority
{
    /// <summary>Default, non-urgent.</summary>
    Low = 0,

    /// <summary>Should be addressed soon.</summary>
    Medium = 1,

    /// <summary>Time-sensitive, treat with precedence.</summary>
    High = 2
}
