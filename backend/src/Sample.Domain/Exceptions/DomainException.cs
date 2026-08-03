namespace Sample.Domain.Exceptions;

/// <summary>
/// Thrown when a domain invariant is violated. The
/// <c>GlobalExceptionHandler</c> in <c>Sample.Api</c> maps this to a
/// <c>409 Conflict</c> ProblemDetails response, distinguishing a domain-level
/// business rule breakage from a generic server error.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
