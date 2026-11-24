using System.Security.Claims;

using Andes.Mailer.Api.Filters;
using Andes.Mailer.Api.Filters.Exceptions;
using Andes.Mailer.Api.Idempotency;
using Andes.Mailer.Api.Options;
using Andes.Mailer.Dto.Actions.Teams;
using Andes.Mailer.Service.Exceptions;

using FluentValidation;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Time.Testing;

namespace Andes.Mailer.Unit.Test.Api.Filters;

public sealed class IdempotencyEndpointFilterTests
{
    private static readonly Guid _callerId = Guid.NewGuid();

    private readonly IdempotencyEndpointFilter _filter;
    private int _nextCalls;
    private Func<IResult> _handler = () => TypedResults.Created("/api/teams/1", "team");

    public IdempotencyEndpointFilterTests()
    {
        var store = new IdempotencyStore(new FakeTimeProvider(), Microsoft.Extensions.Options.Options.Create(new IdempotencyOptions()));
        _filter = new IdempotencyEndpointFilter(store);
    }

    [Fact]
    public async Task InvokeAsync_NoKey_CallsNextEveryTime()
    {
        await _filter.InvokeAsync(CreateContext(key: null), NextAsync);
        await _filter.InvokeAsync(CreateContext(key: null), NextAsync);

        Assert.Equal(2, _nextCalls);
    }

    [Fact]
    public async Task InvokeAsync_RepeatedKeyAfterSuccess_ReplaysFirstResultWithoutCallingNext()
    {
        var first = await _filter.InvokeAsync(CreateContext("key-1"), NextAsync);
        var replayContext = CreateContext("key-1");

        var second = await _filter.InvokeAsync(replayContext, NextAsync);

        Assert.Equal(1, _nextCalls);
        Assert.Same(first, second);
        Assert.Equal("true", replayContext.HttpContext.Response.Headers["Idempotent-Replayed"]);
    }

    [Fact]
    public async Task InvokeAsync_QuotedKey_MatchesSameTokenUnquoted()
    {
        await _filter.InvokeAsync(CreateContext("\"key-1\""), NextAsync);

        await _filter.InvokeAsync(CreateContext("key-1"), NextAsync);

        Assert.Equal(1, _nextCalls);
    }

    [Fact]
    public async Task InvokeAsync_SameKeyDifferentBody_ThrowsMismatch()
    {
        await _filter.InvokeAsync(CreateContext("key-1", body: Body("Sales")), NextAsync);

        await Assert.ThrowsAsync<IdempotencyKeyMismatchException>(() =>
            _filter.InvokeAsync(CreateContext("key-1", body: Body("Support")), NextAsync).AsTask());
    }

    [Fact]
    public async Task InvokeAsync_SameKeyDifferentPath_ThrowsMismatch()
    {
        await _filter.InvokeAsync(CreateContext("key-1", path: "/api/teams/a"), NextAsync);

        await Assert.ThrowsAsync<IdempotencyKeyMismatchException>(() =>
            _filter.InvokeAsync(CreateContext("key-1", path: "/api/teams/b"), NextAsync).AsTask());
    }

    [Fact]
    public async Task InvokeAsync_SameKeyOtherCaller_RunsIndependently()
    {
        await _filter.InvokeAsync(CreateContext("key-1"), NextAsync);

        await _filter.InvokeAsync(CreateContext("key-1", callerId: Guid.NewGuid()), NextAsync);

        Assert.Equal(2, _nextCalls);
    }

    [Fact]
    public async Task InvokeAsync_SameKeyWhileFirstRuns_ThrowsInProgress()
    {
        var release = new TaskCompletionSource();
        var first = _filter.InvokeAsync(CreateContext("key-1"), async _ =>
        {
            await release.Task;

            return TypedResults.NoContent();
        });

        await Assert.ThrowsAsync<IdempotencyRequestInProgressException>(() =>
            _filter.InvokeAsync(CreateContext("key-1"), NextAsync).AsTask());

        release.SetResult();
        await first;
    }

    [Fact]
    public async Task InvokeAsync_HandlerThrows_ReleasesKeyForRetry()
    {
        _handler = () => throw new ConflictException("stale");
        await Assert.ThrowsAsync<ConflictException>(() => _filter.InvokeAsync(CreateContext("key-1"), NextAsync).AsTask());
        _handler = () => TypedResults.NoContent();

        var result = await _filter.InvokeAsync(CreateContext("key-1", body: Body("Fixed")), NextAsync);

        Assert.IsType<NoContent>(result);
        Assert.Equal(2, _nextCalls);
    }

    [Fact]
    public async Task InvokeAsync_HandlerReturnsFailure_DoesNotStoreIt()
    {
        _handler = () => TypedResults.ValidationProblem([]);
        await _filter.InvokeAsync(CreateContext("key-1"), NextAsync);

        await _filter.InvokeAsync(CreateContext("key-1"), NextAsync);

        Assert.Equal(2, _nextCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("ключ")]
    public async Task InvokeAsync_MalformedKey_ThrowsValidation(string key)
    {
        await Assert.ThrowsAsync<ValidationException>(() => _filter.InvokeAsync(CreateContext(key), NextAsync).AsTask());

        Assert.Equal(0, _nextCalls);
    }

    [Fact]
    public async Task InvokeAsync_KeyOverMaxLength_ThrowsValidation() =>
        await Assert.ThrowsAsync<ValidationException>(() => _filter.InvokeAsync(CreateContext(new string('k', 129)), NextAsync).AsTask());

    [Fact]
    public async Task InvokeAsync_NoObjectId_ThrowsForbidden() =>
        await Assert.ThrowsAsync<ForbiddenException>(() => _filter.InvokeAsync(CreateContext("key-1", anonymous: true), NextAsync).AsTask());

    #region Private methods

    private static CreateTeamActionDto Body(string name) => new(name, null, "sales@contoso.com");

    private static DefaultEndpointFilterInvocationContext CreateContext(
        string? key,
        Guid? callerId = null,
        string path = "/api/teams",
        CreateTeamActionDto? body = null,
        bool anonymous = false)
    {
        Claim[] claims = anonymous ? [] : [new("oid", (callerId ?? _callerId).ToString())];
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) };
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = path;
        if (key is not null)
        {
            httpContext.Request.Headers["Idempotency-Key"] = key;
        }

        return new DefaultEndpointFilterInvocationContext(httpContext, body ?? Body("Sales"), CancellationToken.None);
    }

    private ValueTask<object?> NextAsync(EndpointFilterInvocationContext context)
    {
        _nextCalls++;

        return ValueTask.FromResult<object?>(_handler());
    }

    #endregion
}
