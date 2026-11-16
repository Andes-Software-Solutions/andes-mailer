using System.Security.Claims;

using Andes.Mailer.Api.Filters;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Service.Security;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Andes.Mailer.Unit.Test.Api.Filters;

public sealed class TeamPermissionEndpointFilterTests
{
    private static readonly object _nextResult = new();
    private static readonly Guid _userId = Guid.NewGuid();
    private static readonly Guid _teamId = Guid.NewGuid();

    private readonly IPermissionService _permissions = Substitute.For<IPermissionService>();
    private readonly TeamPermissionEndpointFilter _filter = new(Permissions.ComposeTeamEmails);
    private bool _nextCalled;

    [Fact]
    public async Task InvokeAsync_Granted_CallsNext()
    {
        _permissions.CheckTeamPermissionAsync(_userId, _teamId, Permissions.ComposeTeamEmails, Arg.Any<CancellationToken>()).Returns(TeamAccess.Granted);

        var result = await _filter.InvokeAsync(CreateContext(_userId.ToString(), _teamId.ToString()), NextAsync);

        Assert.True(_nextCalled);
        Assert.Same(_nextResult, result);
    }

    [Fact]
    public async Task InvokeAsync_Forbidden_ThrowsForbiddenWithoutCallingNext()
    {
        _permissions.CheckTeamPermissionAsync(_userId, _teamId, Permissions.ComposeTeamEmails, Arg.Any<CancellationToken>()).Returns(TeamAccess.Forbidden);

        await Assert.ThrowsAsync<ForbiddenException>(() => _filter.InvokeAsync(CreateContext(_userId.ToString(), _teamId.ToString()), NextAsync).AsTask());

        Assert.False(_nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_NotFound_ThrowsNotFoundWithoutCallingNext()
    {
        _permissions.CheckTeamPermissionAsync(_userId, _teamId, Permissions.ComposeTeamEmails, Arg.Any<CancellationToken>()).Returns(TeamAccess.NotFound);

        await Assert.ThrowsAsync<NotFoundException>(() => _filter.InvokeAsync(CreateContext(_userId.ToString(), _teamId.ToString()), NextAsync).AsTask());

        Assert.False(_nextCalled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task InvokeAsync_NoObjectId_ThrowsForbiddenWithoutCheckingPermission(string? objectId)
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => _filter.InvokeAsync(CreateContext(objectId, _teamId.ToString()), NextAsync).AsTask());

        await _permissions.DidNotReceive().CheckTeamPermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Permissions>(), Arg.Any<CancellationToken>());
        Assert.False(_nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_NoTeamRouteValue_ThrowsNotFoundWithoutCheckingPermission()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _filter.InvokeAsync(CreateContext(_userId.ToString(), null), NextAsync).AsTask());

        await _permissions.DidNotReceive().CheckTeamPermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Permissions>(), Arg.Any<CancellationToken>());
        Assert.False(_nextCalled);
    }

    #region Private methods

    private DefaultEndpointFilterInvocationContext CreateContext(string? objectId, string? teamId)
    {
        Claim[] claims = objectId is null ? [] : [new("oid", objectId)];
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
            RequestServices = new ServiceCollection().AddSingleton(_permissions).BuildServiceProvider(),
        };
        if (teamId is not null)
        {
            httpContext.Request.RouteValues["teamId"] = teamId;
        }

        return new DefaultEndpointFilterInvocationContext(httpContext);
    }

    private ValueTask<object?> NextAsync(EndpointFilterInvocationContext context)
    {
        _nextCalled = true;

        return ValueTask.FromResult<object?>(_nextResult);
    }

    #endregion
}
