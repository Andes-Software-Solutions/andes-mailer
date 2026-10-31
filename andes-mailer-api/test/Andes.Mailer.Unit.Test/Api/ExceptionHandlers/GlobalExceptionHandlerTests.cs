using Andes.Mailer.Api.ExceptionHandlers;
using Andes.Mailer.Api.Problems.Constants;
using Andes.Mailer.Service.Exceptions;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

namespace Andes.Mailer.Unit.Test.Api.ExceptionHandlers;

public sealed class GlobalExceptionHandlerTests
{
    private const string Message = "Caller-facing message.";

    private readonly IProblemDetailsService _problemDetails = Substitute.For<IProblemDetailsService>();
    private readonly GlobalExceptionHandler _handler;

    public GlobalExceptionHandlerTests()
    {
        _problemDetails.TryWriteAsync(Arg.Any<ProblemDetailsContext>()).Returns(ValueTask.FromResult(true));
        _handler = new GlobalExceptionHandler(_problemDetails, NullLogger<GlobalExceptionHandler>.Instance);
    }

    [Theory]
    [InlineData(typeof(ValidationException), StatusCodes.Status400BadRequest, ProblemTypes.BadRequest)]
    [InlineData(typeof(BadHttpRequestException), StatusCodes.Status400BadRequest, ProblemTypes.BadRequest)]
    [InlineData(typeof(NotFoundException), StatusCodes.Status404NotFound, ProblemTypes.NotFound)]
    [InlineData(typeof(ForbiddenException), StatusCodes.Status403Forbidden, ProblemTypes.Forbidden)]
    [InlineData(typeof(ConflictException), StatusCodes.Status409Conflict, ProblemTypes.Conflict)]
    [InlineData(typeof(InvalidOperationException), StatusCodes.Status500InternalServerError, ProblemTypes.Unexpected)]
    public async Task TryHandleAsync_MappedException_WritesMatchingProblem(Type exceptionType, int status, string problemType)
    {
        var context = new DefaultHttpContext();
        var exception = (Exception)Activator.CreateInstance(exceptionType, Message)!;

        var handled = await _handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(status, context.Response.StatusCode);
        await _problemDetails.Received(1).TryWriteAsync(Arg.Is<ProblemDetailsContext>(c =>
            c.ProblemDetails.Status == status && c.ProblemDetails.Type == problemType));
    }

    [Theory]
    [InlineData(typeof(NotFoundException))]
    [InlineData(typeof(ForbiddenException))]
    [InlineData(typeof(ConflictException))]
    public async Task TryHandleAsync_SharedException_PassesMessageAsDetail(Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, Message)!;

        await _handler.TryHandleAsync(new DefaultHttpContext(), exception, TestContext.Current.CancellationToken);

        await _problemDetails.Received(1).TryWriteAsync(Arg.Is<ProblemDetailsContext>(c => c.ProblemDetails.Detail == Message));
    }

    [Fact]
    public async Task TryHandleAsync_UnexpectedException_HidesExceptionDetail()
    {
        await _handler.TryHandleAsync(new DefaultHttpContext(), new InvalidOperationException(Message), TestContext.Current.CancellationToken);

        await _problemDetails.Received(1).TryWriteAsync(Arg.Is<ProblemDetailsContext>(c => c.ProblemDetails.Detail == null));
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_GroupsErrorsByProperty()
    {
        var exception = new ValidationException([new ValidationFailure("Subject", "Required."), new ValidationFailure("Subject", "Too long.")]);

        await _handler.TryHandleAsync(new DefaultHttpContext(), exception, TestContext.Current.CancellationToken);

        await _problemDetails.Received(1).TryWriteAsync(Arg.Is<ProblemDetailsContext>(c =>
            ((HttpValidationProblemDetails)c.ProblemDetails).Errors["Subject"].Length == 2));
    }

    [Fact]
    public async Task TryHandleAsync_CancelledByClient_Returns499WithoutBody()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var context = new DefaultHttpContext { RequestAborted = cancellation.Token };

        var handled = await _handler.TryHandleAsync(context, new OperationCanceledException(), TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status499ClientClosedRequest, context.Response.StatusCode);
        await _problemDetails.DidNotReceive().TryWriteAsync(Arg.Any<ProblemDetailsContext>());
    }
}
