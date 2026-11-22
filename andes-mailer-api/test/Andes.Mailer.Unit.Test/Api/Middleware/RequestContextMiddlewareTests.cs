using System.Diagnostics;
using System.Security.Claims;

using Andes.Mailer.Api.Middleware;
using Andes.Mailer.Common.Constants;
using Andes.Mailer.Unit.Test.TestInfrastructure;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Andes.Mailer.Unit.Test.Api.Middleware;

public sealed class RequestContextMiddlewareTests
{
    private static readonly string _userId = Guid.NewGuid().ToString();
    private static readonly string _teamId = Guid.NewGuid().ToString();

    private readonly FakeLogger<RequestContextMiddleware> _logger = new();

    [Fact]
    public async Task InvokeAsync_AuthenticatedCaller_ScopesDownstreamLogsWithUserId()
    {
        var context = CreateContext(_userId, teamId: null);

        await CreateMiddleware().InvokeAsync(context);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        var scope = Assert.Single(ScopeValues(record));
        Assert.Equal(new KeyValuePair<string, object?>(TelemetryNames.UserIdProperty, _userId), scope);
    }

    [Fact]
    public async Task InvokeAsync_TeamRoute_ScopesDownstreamLogsWithTeamId()
    {
        var context = CreateContext(_userId, _teamId);

        await CreateMiddleware().InvokeAsync(context);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Contains(new KeyValuePair<string, object?>(TelemetryNames.TeamIdProperty, _teamId), ScopeValues(record));
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedCaller_TagsRequestActivityWithUserAndTeam()
    {
        using var activity = new Activity("request");
        var context = CreateContext(_userId, _teamId);
        context.Features.Set<IHttpActivityFeature>(new FakeHttpActivityFeature(activity));

        await CreateMiddleware().InvokeAsync(context);

        Assert.Equal(_userId, activity.GetTagItem(TelemetryNames.EndUserIdTag));
        Assert.Equal(_teamId, activity.GetTagItem(TelemetryNames.TeamIdTag));
    }

    [Fact]
    public async Task InvokeAsync_AnonymousCaller_AddsNoScopeOrTag()
    {
        using var activity = new Activity("request");
        var context = CreateContext(userId: null, teamId: null);
        context.Features.Set<IHttpActivityFeature>(new FakeHttpActivityFeature(activity));

        await CreateMiddleware().InvokeAsync(context);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Empty(record.Scopes);
        Assert.Null(activity.GetTagItem(TelemetryNames.EndUserIdTag));
    }

    #region Private methods

    // Logs through the middleware's own logger, so the record carries whatever scope the middleware opened.
    private RequestContextMiddleware CreateMiddleware() => new(
        _ =>
        {
            _logger.Log(LogLevel.Information, new EventId(1), "downstream", null, static (state, _) => state);
            return Task.CompletedTask;
        },
        _logger);

    private static DefaultHttpContext CreateContext(string? userId, string? teamId)
    {
        List<Claim> claims = userId is null ? [] : [new("oid", userId)];
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, userId is null ? null : "Test")),
        };
        if (teamId is not null)
        {
            context.Request.RouteValues[RequestContextMiddleware.TeamIdRouteValue] = teamId;
        }

        return context;
    }

    private static IEnumerable<KeyValuePair<string, object?>> ScopeValues(FakeLogRecord record) =>
        record.Scopes.OfType<IEnumerable<KeyValuePair<string, object?>>>().SelectMany(scope => scope);

    #endregion
}
