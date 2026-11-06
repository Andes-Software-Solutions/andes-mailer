using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Users;
using Andes.Mailer.Dto.Users;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Service.Users;
using Andes.Mailer.Service.Users.Models;
using Andes.Mailer.Unit.Test.TestInfrastructure;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

namespace Andes.Mailer.Unit.Test.Service.Users;

public sealed class UserServiceTests : IDisposable
{
    private readonly MailerDbFixture _fixture = new();
    private readonly IDirectoryUserService _directory = Substitute.For<IDirectoryUserService>();

    [Fact]
    public async Task CreateAsync_DirectoryAccount_InsertsUserWithDirectoryDetails()
    {
        var account = new DirectoryUser(Guid.NewGuid(), "Grace", "Hopper", "grace@contoso.com");
        _directory.FindByEmailAsync("grace@contoso.com", Arg.Any<CancellationToken>()).Returns(account);
        await using var ctx = _fixture.CreateContext();

        var user = await CreateService(ctx).CreateAsync(new CreateUserActionDto(" Grace@Contoso.COM "), TestContext.Current.CancellationToken);

        Assert.Equal(account.Id, user.Id);
        Assert.Equal("Grace Hopper", user.FullName);
        Assert.Equal("grace@contoso.com", user.Email);
        Assert.Equal(_fixture.Time.GetUtcNow(), user.DateCreated);
        Assert.NotEmpty(user.Version);
    }

    [Fact]
    public async Task CreateAsync_ActiveUserHasEmail_ThrowsConflictWithoutDirectoryLookup()
    {
        await _fixture.AddUserAsync("grace@contoso.com");
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(ctx).CreateAsync(new CreateUserActionDto("GRACE@contoso.com"), TestContext.Current.CancellationToken));

        await _directory.DidNotReceive().FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_EmailNotInDirectory_ThrowsNotFound()
    {
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService(ctx).CreateAsync(new CreateUserActionDto("nobody@contoso.com"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_DirectoryAccountIsActiveUserUnderOtherEmail_ThrowsConflict()
    {
        var existing = await _fixture.AddUserAsync("old@contoso.com");
        _directory.FindByEmailAsync("new@contoso.com", Arg.Any<CancellationToken>())
            .Returns(new DirectoryUser(existing.Id, "Ada", "Lovelace", "new@contoso.com"));
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(ctx).CreateAsync(new CreateUserActionDto("new@contoso.com"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_DeactivatedUser_ReactivatesWithDirectoryDetails()
    {
        var existing = await _fixture.AddUserAsync("old@contoso.com");
        await _fixture.DeactivateUserAsync(existing.Id);
        _directory.FindByEmailAsync("ada@contoso.com", Arg.Any<CancellationToken>())
            .Returns(new DirectoryUser(existing.Id, "Augusta", "King", "ada@contoso.com"));

        await using (var ctx = _fixture.CreateContext())
        {
            await CreateService(ctx).CreateAsync(new CreateUserActionDto("ada@contoso.com"), TestContext.Current.CancellationToken);
        }

        await using var verifyCtx = _fixture.CreateContext();
        var row = await verifyCtx.Users.SingleAsync(u => u.Id == existing.Id, TestContext.Current.CancellationToken);
        Assert.Null(row.DateDeactivated);
        Assert.Equal("Augusta King", row.FullName);
        Assert.Equal("ada@contoso.com", row.Email);
    }

    [Fact]
    public async Task UpdateAsync_CurrentVersion_ChangesNameAndStampsModified()
    {
        var existing = await _fixture.AddUserAsync("ada@contoso.com");
        var current = await GetAsync(existing.Id);
        _fixture.Time.Advance(TimeSpan.FromMinutes(5));
        await using var ctx = _fixture.CreateContext();

        var user = await CreateService(ctx).UpdateAsync(
            existing.Id,
            new UpdateUserActionDto(" Augusta ", "King", current.Version),
            TestContext.Current.CancellationToken);

        Assert.Equal("Augusta King", user.FullName);
        Assert.Equal(_fixture.Time.GetUtcNow(), user.DateModified);
    }

    [Fact]
    public async Task UpdateAsync_StaleVersion_ThrowsConflict()
    {
        var existing = await _fixture.AddUserAsync("ada@contoso.com");
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ConflictException>(() => CreateService(ctx).UpdateAsync(
            existing.Id,
            new UpdateUserActionDto("Augusta", "King", Convert.ToBase64String(new byte[8])),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_DeactivatedUser_ThrowsNotFound()
    {
        var existing = await _fixture.AddUserAsync("ada@contoso.com");
        await _fixture.DeactivateUserAsync(existing.Id);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService(ctx).UpdateAsync(
            existing.Id,
            new UpdateUserActionDto("Augusta", "King", Convert.ToBase64String(new byte[8])),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeactivateAsync_OtherUser_HidesUser()
    {
        var existing = await _fixture.AddUserAsync("ada@contoso.com");

        await using (var ctx = _fixture.CreateContext())
        {
            await CreateService(ctx).DeactivateAsync(existing.Id, Guid.NewGuid(), TestContext.Current.CancellationToken);
        }

        await using var verifyCtx = _fixture.CreateContext();
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService(verifyCtx).GetAsync(existing.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeactivateAsync_UserWithRole_RevokesRole()
    {
        var existing = await _fixture.AddUserAsync("ada@contoso.com", role: Roles.Administrator);

        await using (var ctx = _fixture.CreateContext())
        {
            await CreateService(ctx).DeactivateAsync(existing.Id, Guid.NewGuid(), TestContext.Current.CancellationToken);
        }

        await using var verifyCtx = _fixture.CreateContext();
        Assert.False(await verifyCtx.UserRoles
            .IgnoreQueryFilters(["Deactivation"])
            .AnyAsync(ur => ur.UserId == existing.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeactivateAsync_Self_ThrowsConflictAndKeepsUser()
    {
        var existing = await _fixture.AddUserAsync("ada@contoso.com");
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(ctx).DeactivateAsync(existing.Id, existing.Id, TestContext.Current.CancellationToken));

        Assert.Equal(existing.Id, (await GetAsync(existing.Id)).Id);
    }

    [Fact]
    public async Task DeactivateAsync_MissingUser_ThrowsNotFound()
    {
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService(ctx).DeactivateAsync(Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListAsync_ActiveUsers_ReturnsRequestedPageOrderedByName()
    {
        await _fixture.AddUserAsync("c@contoso.com", "Charles", "Babbage");
        await _fixture.AddUserAsync("g@contoso.com", "Grace", "Hopper");
        await _fixture.AddUserAsync("a@contoso.com", "Ada", "Lovelace");
        var deactivated = await _fixture.AddUserAsync("x@contoso.com", "Alan", "Turing");
        await _fixture.DeactivateUserAsync(deactivated.Id);
        await using var ctx = _fixture.CreateContext();

        var page = await CreateService(ctx).ListAsync(new ListUsersActionDto(Page: 2, PageSize: 2), TestContext.Current.CancellationToken);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal("Ada Lovelace", Assert.Single(page.Items).FullName);
    }

    public void Dispose() => _fixture.Dispose();

    #region Private methods

    private UserService CreateService(AndesMailerDbContext ctx) => new(ctx, _directory);

    private async Task<UserDto> GetAsync(Guid id)
    {
        await using var ctx = _fixture.CreateContext();

        return await CreateService(ctx).GetAsync(id, TestContext.Current.CancellationToken);
    }

    #endregion
}
