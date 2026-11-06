using System.Security.Claims;

using Andes.Mailer.Api.Filters;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Service.Security;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Andes.Mailer.Unit.Test.Api.Filters;

public sealed class PermissionEndpointFilterTests
{
    private static readonly object _nextResult = new();
    private static readonly Guid _userId = Guid.NewGuid();

    private readonly IPermissionService _permissions = Substitute.For<IPermissionService>();
    private readonly PermissionEndpointFilter _filter = new(Permissions.CreateUser);
    private bool _nextCalled;

    [Fact]
    public async Task InvokeAsync_CallerHasPermission_CallsNext()
    {
        _permissions.HasPermissionAsync(_userId, Permissions.CreateUser, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _filter.InvokeAsync(CreateContext(_userId.ToString()), NextAsync);

        Assert.True(_nextCalled);
        Assert.Same(_nextResult, result);
    }

    [Fact]
    public async Task InvokeAsync_CallerLacksPermission_ThrowsForbiddenWithoutCallingNext()
    {
        _permissions.HasPermissionAsync(_userId, Permissions.CreateUser, Arg.Any<CancellationToken>()).Returns(false);

        await Assert.ThrowsAsync<ForbiddenException>(() => _filter.InvokeAsync(CreateContext(_userId.ToString()), NextAsync).AsTask());

        Assert.False(_nextCalled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    public async Task InvokeAsync_NoObjectId_ThrowsForbiddenWithoutCheckingPermission(string? objectId)
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => _filter.InvokeAsync(CreateContext(objectId), NextAsync).AsTask());

        await _permissions.DidNotReceive().HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Permissions>(), Arg.Any<CancellationToken>());
        Assert.False(_nextCalled);
    }

    #region Private methods

    private DefaultEndpointFilterInvocationContext CreateContext(string? objectId)
    {
        Claim[] claims = objectId is null ? [] : [new("oid", objectId)];
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
            RequestServices = new ServiceCollection().AddSingleton(_permissions).BuildServiceProvider(),
        };

        return new DefaultEndpointFilterInvocationContext(httpContext);
    }

    private ValueTask<object?> NextAsync(EndpointFilterInvocationContext context)
    {
        _nextCalled = true;

        return ValueTask.FromResult<object?>(_nextResult);
    }

    #endregion
}
