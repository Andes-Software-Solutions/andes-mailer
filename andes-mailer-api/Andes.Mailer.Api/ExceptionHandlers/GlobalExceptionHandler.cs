using Andes.Mailer.Api.Problems.Constants;
using Andes.Mailer.Service.Exceptions;

using FluentValidation;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Andes.Mailer.Api.ExceptionHandlers;

internal sealed partial class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails = problemDetails;
    private readonly ILogger<GlobalExceptionHandler> _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return true;
        }

        var problem = MapToProblem(exception);
        var status = problem.Status ?? StatusCodes.Status500InternalServerError;
        if (status >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(_logger, exception, httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
    }

    #region Private methods

    private static ProblemDetails MapToProblem(Exception exception) => exception switch
    {
        ValidationException validation => new HttpValidationProblemDetails(ToErrors(validation))
        {
            Status = StatusCodes.Status400BadRequest,
            Type = ProblemTypes.BadRequest,
            Title = "One or more validation errors occurred.",
        },
        BadHttpRequestException badRequest => Problem(badRequest.StatusCode, ProblemTypes.BadRequest, "The request could not be read.", null),
        NotFoundException => Problem(StatusCodes.Status404NotFound, ProblemTypes.NotFound, "The resource was not found.", exception.Message),
        ForbiddenException => Problem(StatusCodes.Status403Forbidden, ProblemTypes.Forbidden, "Access to the resource is forbidden.", exception.Message),
        ConflictException => Problem(StatusCodes.Status409Conflict, ProblemTypes.Conflict, "The request conflicts with the current state of the resource.", exception.Message),
        _ => Problem(StatusCodes.Status500InternalServerError, ProblemTypes.Unexpected, "An unexpected error occurred.", null),
    };

    private static ProblemDetails Problem(int status, string type, string title, string? detail) =>
        new() { Status = status, Type = type, Title = title, Detail = detail };

    private static Dictionary<string, string[]> ToErrors(ValidationException exception) =>
        exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

    #endregion

    #region Logging

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string method, PathString path);

    #endregion
}
