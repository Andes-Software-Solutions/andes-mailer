using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;

using Andes.Mailer.Api.Middleware;
using Andes.Mailer.Unit.Test.TestInfrastructure;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Andes.Mailer.Unit.Test.Api.Middleware;

public sealed class RequestLoggingMiddlewareTests
{
    private const string RouteTemplate = "/api/teams/{teamId:guid}/emails";

    private static readonly string _userId = Guid.NewGuid().ToString();
    private static readonly string _teamId = Guid.NewGuid().ToString();

    private readonly FakeTimeProvider _time = new();
    private readonly FakeLogger<RequestLoggingMiddleware> _logger = new();

    [Fact]
    public async Task InvokeAsync_AuthenticatedRequest_LogsCallerRouteStatusAndDuration()
    {
        var context = CreateContext($"/api/teams/{_teamId}/emails", _userId);
        var middleware = CreateMiddleware(ctx =>
        {
            _time.Advance(TimeSpan.FromMilliseconds(120));
            ctx.Response.StatusCode = StatusCodes.Status201Created;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Information, record.Level);
        Assert.Equal(_userId, record.GetStructuredStateValue("UserId"));
        Assert.Equal(_teamId, record.GetStructuredStateValue("TeamId"));
        Assert.Equal(RouteTemplate, record.GetStructuredStateValue("RouteTemplate"));
        Assert.Equal("POST", record.GetStructuredStateValue("Method"));
        Assert.Equal("201", record.GetStructuredStateValue("StatusCode"));
        Assert.Equal(120, double.Parse(record.GetStructuredStateValue("ElapsedMs")!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task InvokeAsync_RequestActivity_LogsItsTraceId()
    {
        using var activity = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();
        var context = CreateContext("/api/users/me", _userId);
        context.Features.Set<IHttpActivityFeature>(new FakeHttpActivityFeature(activity));

        await CreateMiddleware(_ => Task.CompletedTask).InvokeAsync(context);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(activity.TraceId.ToHexString(), record.GetStructuredStateValue("TraceId"));
    }

    [Fact]
    public async Task InvokeAsync_AnonymousRequest_LogsWithoutUser()
    {
        var context = CreateContext("/api/users/me", userId: null);

        await CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }).InvokeAsync(context);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Null(record.GetStructuredStateValue("UserId"));
        Assert.Equal("401", record.GetStructuredStateValue("StatusCode"));
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task InvokeAsync_HealthProbe_DoesNotLog(string path)
    {
        var nextCalled = false;
        var context = CreateContext(path, userId: null);

        await CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }).InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Empty(_logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task InvokeAsync_NextThrows_LogsServerErrorAsWarningAndRethrows()
    {
        var context = CreateContext("/api/users/me", _userId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateMiddleware(_ => throw new InvalidOperationException()).InvokeAsync(context));

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal("500", record.GetStructuredStateValue("StatusCode"));
    }

    [Fact]
    public async Task InvokeAsync_EndpointClearedDownstream_LogsRouteReadBeforeTheCall()
    {
        var context = CreateContext($"/api/teams/{_teamId}/emails", _userId);

        // As the exception handler does before it writes its response.
        await CreateMiddleware(ctx =>
        {
            ctx.SetEndpoint(null);
            ctx.Request.RouteValues.Clear();
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }).InvokeAsync(context);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(RouteTemplate, record.GetStructuredStateValue("RouteTemplate"));
        Assert.Equal(_teamId, record.GetStructuredStateValue("TeamId"));
        Assert.Equal("404", record.GetStructuredStateValue("StatusCode"));
    }

    #region Private methods

    private RequestLoggingMiddleware CreateMiddleware(RequestDelegate next) => new(next, _time, _logger);

    private static DefaultHttpContext CreateContext(string path, string? userId)
    {
        List<Claim> claims = userId is null ? [] : [new("oid", userId)];
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, userId is null ? null : "Test")),
        };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = path;
        if (path.StartsWith("/api/teams/", StringComparison.Ordinal))
        {
            context.SetEndpoint(new RouteEndpoint(
                _ => Task.CompletedTask,
                RoutePatternFactory.Parse(RouteTemplate),
                0,
                EndpointMetadataCollection.Empty,
                "test"));
            context.Request.RouteValues[RequestContextMiddleware.TeamIdRouteValue] = _teamId;
        }

        return context;
    }

    #endregion
}
