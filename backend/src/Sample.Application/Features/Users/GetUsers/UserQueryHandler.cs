namespace Sample.Application.Features.Users.GetUsers;

using Mediator;
using Microsoft.Extensions.Logging;

public sealed class UserQueryHandler : IRequestHandler<UserQuery>
{
    private readonly ILogger<UserQueryHandler> _logger;

    public UserQueryHandler(ILogger<UserQueryHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask<Unit> Handle(UserQuery request, CancellationToken cancellationToken)
    {
        //TODO : Implement the logic to handle the UserQuery request and return the appropriate result.
        _logger.LogInformation($"[{Guid.CreateVersion7()}] Handling UserQuery request.");

        return ValueTask.FromResult(Unit.Value);
    }
}

public sealed class UserQuery : IRequest
{
    //TODO : Define any properties or parameters needed for the UserQuery request.
}
