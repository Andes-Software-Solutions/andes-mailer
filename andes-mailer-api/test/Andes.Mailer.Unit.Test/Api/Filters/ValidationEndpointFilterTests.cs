using Andes.Mailer.Api.Filters;
using Andes.Mailer.Unit.Test.TestInfrastructure;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Andes.Mailer.Unit.Test.Api.Filters;

public sealed class ValidationEndpointFilterTests
{
    private static readonly object _nextResult = new();

    private readonly ValidationEndpointFilter<SampleRequest> _filter;
    private bool _nextCalled;

    public ValidationEndpointFilterTests()
    {
        var validator = new InlineValidator<SampleRequest>();
        validator.RuleFor(r => r.Subject).NotEmpty();
        _filter = new ValidationEndpointFilter<SampleRequest>(validator);
    }

    [Fact]
    public async Task InvokeAsync_ValidRequest_CallsNext()
    {
        var context = EndpointFilterInvocationContext.Create<SampleRequest?>(new DefaultHttpContext(), new SampleRequest("Hello"));

        var result = await _filter.InvokeAsync(context, NextAsync);

        Assert.True(_nextCalled);
        Assert.Same(_nextResult, result);
    }

    [Fact]
    public async Task InvokeAsync_InvalidRequest_ReturnsValidationProblemWithoutCallingNext()
    {
        var context = EndpointFilterInvocationContext.Create<SampleRequest?>(new DefaultHttpContext(), new SampleRequest(string.Empty));

        var result = await _filter.InvokeAsync(context, NextAsync);

        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Contains(nameof(SampleRequest.Subject), problem.ProblemDetails.Errors.Keys);
        Assert.False(_nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_MissingRequest_ReturnsValidationProblemWithoutCallingNext()
    {
        var context = EndpointFilterInvocationContext.Create<SampleRequest?>(new DefaultHttpContext(), null);

        var result = await _filter.InvokeAsync(context, NextAsync);

        var problem = Assert.IsType<ValidationProblem>(result);
        Assert.Contains(nameof(SampleRequest), problem.ProblemDetails.Errors.Keys);
        Assert.False(_nextCalled);
    }

    #region Private methods

    private ValueTask<object?> NextAsync(EndpointFilterInvocationContext context)
    {
        _nextCalled = true;

        return ValueTask.FromResult<object?>(_nextResult);
    }

    #endregion
}
