namespace Sample.Infrastructure.Behaviors;

using Mediator;
using Microsoft.Extensions.Logging;

public sealed class ValidationBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly ILogger<ValidationBehavior<TMessage, TResponse>> _logger;

    public ValidationBehavior(ILogger<ValidationBehavior<TMessage, TResponse>> logger)
    {
        _logger = logger;
    }

    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        //TODO: Implement validation logic here, e.g., using FluentValidation or custom validation rules.
        return await next(message, cancellationToken);
    }
}