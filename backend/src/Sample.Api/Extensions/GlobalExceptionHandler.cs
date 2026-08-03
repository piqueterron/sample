namespace Sample.Api.Extensions;

using System.Net.Mime;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Global exception handler that maps unhandled exceptions to RFC 7807
/// <c>ProblemDetails</c> responses so every endpoint's
/// <c>ProducesProblem(StatusCodes.Status500InternalServerError)</c> OpenAPI
/// declaration is actually honored at runtime.
/// </summary>
/// <remarks>
/// Domain invariants (thrown as <c>DomainException</c> from
/// <c>Sample.Domain</c>) are mapped to <c>409 Conflict</c>. Validation
/// failures (<c>ValidationException</c>) are mapped to <c>400 Bad Request</c>
/// when the FluentValidation integration is in place. Anything else is treated
/// as an internal error and returns <c>500</c> with a sanitized message.
/// </remarks>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            // FluentValidation's validation pipeline (Sample.Application).
            // Surfaces 400 with each failing property in the `errors` extension.
            FluentValidation.ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),

            // DomainException is defined in Sample.Domain. Thrown when an
            // aggregate invariant is broken; surfaced as 409 to distinguish a
            // business rule violation from generic bad input.
            _ when IsDomainException(exception) => (StatusCodes.Status409Conflict, "Domain invariant violated"),

            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        // 4xx are client errors (expected): log as Warning. 5xx are bugs: Error.
        if (status >= 500)
        {
            _logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "Domain/validity exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception.Message,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = MediaTypeNames.Application.ProblemJson;

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        // Returning true signals that the exception has been handled and no
        // further handlers should run.
        return true;
    }

    private static bool IsDomainException(Exception exception)
    {
        for (var current = exception as Exception; current is not null; current = current.InnerException)
        {
            if (current.GetType().FullName is { } fullName
                && (fullName == "Sample.Domain.Exceptions.DomainException"
                    || fullName.EndsWith(".DomainException", StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }
}

