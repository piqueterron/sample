namespace Sample.Application.Behaviors;

using FluentValidation;
using Mediator;
using Microsoft.Extensions.Logging;

/// <summary>
/// Mediator pipeline behavior that runs FluentValidation validators for the
/// incoming message before the handler executes. Validators are resolved
/// from the DI container; if no <c>IValidator&lt;TMessage&gt;</c> is
/// registered, the handler runs unchanged (no validation enforced).
///
/// Validation failures are surfaced as <see cref="ValidationException"/>,
/// which the API's <c>GlobalExceptionHandler</c> should map to a 400
/// ProblemDetails response (see Phase 6 for that wiring).
/// </summary>
public sealed class ValidationBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly IEnumerable<IValidator<TMessage>> _validators;
    private readonly ILogger<ValidationBehavior<TMessage, TResponse>> _logger;

    public ValidationBehavior(
        IEnumerable<IValidator<TMessage>> validators,
        ILogger<ValidationBehavior<TMessage, TResponse>> logger)
    {
        _validators = validators;
        _logger = logger;
    }

    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            // No validator registered for this message type: short-circuit to
            // avoid the async allocation of ValidationContext.
            return await next(message, cancellationToken);
        }

        var context = new ValidationContext<TMessage>(message);

        var results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count != 0)
        {
            _logger.LogWarning(
                "Validation failed for {MessageType}: {Failures}",
                typeof(TMessage).Name,
                string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}")));

            throw new ValidationException(failures);
        }

        return await next(message, cancellationToken);
    }
}
