using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Teams;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Service.Teams;
using Andes.Mailer.Service.Users;
using Andes.Mailer.Service.Users.Models;
using Andes.Mailer.Unit.Test.TestInfrastructure;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

namespace Andes.Mailer.Unit.Test.Service.Teams;

public sealed class TeamMemberServiceTests : IDisposable
{
    private readonly MailerDbFixture _fixture = new();
    private readonly IDirectoryUserService _directory = Substitute.For<IDirectoryUserService>();

    [Fact]
    public async Task AddAsync_PersonNotYetAUser_AddsUserAndMembership()
    {
        var team = await _fixture.AddTeamAsync();
        var account = new DirectoryUser(Guid.NewGuid(), "Grace", "Hopper", "grace@contoso.com");
        _directory.FindByEmailAsync("grace@contoso.com", Arg.Any<CancellationToken>()).Returns(account);
        await using var ctx = _fixture.CreateContext();

        var member = await CreateService(ctx).AddAsync(team.Id, new AddTeamMemberActionDto("grace@contoso.com", Roles.TeamMember), TestContext.Current.CancellationToken);

        Assert.Equal(account.Id, member.UserId);
        Assert.Equal("Grace Hopper", member.FullName);
        Assert.Equal(Roles.TeamMember, member.Role);
        await using var readCtx = _fixture.CreateContext();
        Assert.True(await readCtx.Users.AnyAsync(u => u.Id == account.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddAsync_UserInAnotherTeam_AddsSecondMembership()
    {
        var team = await _fixture.AddTeamAsync();
        var otherTeam = await _fixture.AddTeamAsync();
        var user = await _fixture.AddUserAsync("ada@contoso.com");
        await _fixture.AddTeamMemberAsync(otherTeam.Id, user.Id, Roles.TeamAdministrator);
        await using var ctx = _fixture.CreateContext();

        var member = await CreateService(ctx).AddAsync(team.Id, new AddTeamMemberActionDto("ada@contoso.com", Roles.TeamMember), TestContext.Current.CancellationToken);

        Assert.Equal(user.Id, member.UserId);
        await using var readCtx = _fixture.CreateContext();
        Assert.Equal(2, await readCtx.TeamMembers.CountAsync(m => m.UserId == user.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddAsync_ActiveMember_ThrowsConflict()
    {
        var team = await _fixture.AddTeamAsync();
        var user = await _fixture.AddUserAsync("ada@contoso.com");
        await _fixture.AddTeamMemberAsync(team.Id, user.Id);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(ctx).AddAsync(team.Id, new AddTeamMemberActionDto("ada@contoso.com", Roles.TeamMember), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddAsync_RemovedMember_ReactivatesSameRowWithNewRole()
    {
        var team = await _fixture.AddTeamAsync();
        var user = await _fixture.AddUserAsync("ada@contoso.com");
        var other = await _fixture.AddUserAsync("bob@contoso.com");
        await _fixture.AddTeamMemberAsync(team.Id, other.Id, Roles.TeamAdministrator);
        var original = await _fixture.AddTeamMemberAsync(team.Id, user.Id);
        await RemoveAsync(team.Id, user.Id);
        await using var ctx = _fixture.CreateContext();

        var member = await CreateService(ctx).AddAsync(team.Id, new AddTeamMemberActionDto("ada@contoso.com", Roles.TeamAdministrator), TestContext.Current.CancellationToken);

        Assert.Equal(Roles.TeamAdministrator, member.Role);
        await using var readCtx = _fixture.CreateContext();
        var row = await readCtx.TeamMembers.IgnoreQueryFilters().SingleAsync(m => m.TeamId == team.Id && m.UserId == user.Id, TestContext.Current.CancellationToken);
        Assert.Equal(original.Id, row.Id);
        Assert.Null(row.DateDeactivated);
    }

    [Fact]
    public async Task AddAsync_NoDirectoryAccount_ThrowsNotFound()
    {
        var team = await _fixture.AddTeamAsync();
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService(ctx).AddAsync(team.Id, new AddTeamMemberActionDto("nobody@contoso.com", Roles.TeamMember), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateRoleAsync_PromoteMember_ChangesRole()
    {
        var team = await _fixture.AddTeamAsync();
        var user = await _fixture.AddUserAsync("ada@contoso.com");
        var member = await _fixture.AddTeamMemberAsync(team.Id, user.Id);
        await using var ctx = _fixture.CreateContext();

        var updated = await CreateService(ctx).UpdateRoleAsync(
            team.Id, user.Id, new UpdateTeamMemberActionDto(Roles.TeamAdministrator, Convert.ToBase64String(member.Version)), TestContext.Current.CancellationToken);

        Assert.Equal(Roles.TeamAdministrator, updated.Role);
    }

    [Fact]
    public async Task UpdateRoleAsync_DemoteLastAdministrator_ThrowsConflict()
    {
        var team = await _fixture.AddTeamAsync();
        var user = await _fixture.AddUserAsync("ada@contoso.com");
        var member = await _fixture.AddTeamMemberAsync(team.Id, user.Id, Roles.TeamAdministrator);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ConflictException>(() => CreateService(ctx).UpdateRoleAsync(
            team.Id, user.Id, new UpdateTeamMemberActionDto(Roles.TeamMember, Convert.ToBase64String(member.Version)), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateRoleAsync_DemoteOneOfTwoAdministrators_ChangesRoleAndWritesTeamRow()
    {
        var team = await _fixture.AddTeamAsync();
        var ada = await _fixture.AddUserAsync("ada@contoso.com");
        var bob = await _fixture.AddUserAsync("bob@contoso.com");
        var member = await _fixture.AddTeamMemberAsync(team.Id, ada.Id, Roles.TeamAdministrator);
        await _fixture.AddTeamMemberAsync(team.Id, bob.Id, Roles.TeamAdministrator);
        await using var ctx = _fixture.CreateContext();

        var updated = await CreateService(ctx).UpdateRoleAsync(
            team.Id, ada.Id, new UpdateTeamMemberActionDto(Roles.TeamMember, Convert.ToBase64String(member.Version)), TestContext.Current.CancellationToken);

        Assert.Equal(Roles.TeamMember, updated.Role);
        await using var readCtx = _fixture.CreateContext();
        var reloaded = await readCtx.Teams.SingleAsync(t => t.Id == team.Id, TestContext.Current.CancellationToken);
        // SQLite never bumps the emulated rowversion, so the write that makes the team version the arbiter shows as DateModified.
        Assert.Equal(_fixture.Time.GetUtcNow(), reloaded.DateModified);
    }

    [Fact]
    public async Task UpdateRoleAsync_StaleVersion_ThrowsConflict()
    {
        var team = await _fixture.AddTeamAsync();
        var user = await _fixture.AddUserAsync("ada@contoso.com");
        await _fixture.AddTeamMemberAsync(team.Id, user.Id);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ConflictException>(() => CreateService(ctx).UpdateRoleAsync(
            team.Id, user.Id, new UpdateTeamMemberActionDto(Roles.TeamAdministrator, Convert.ToBase64String(new byte[8])), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateRoleAsync_MemberOfAnotherTeam_ThrowsNotFound()
    {
        var team = await _fixture.AddTeamAsync();
        var otherTeam = await _fixture.AddTeamAsync();
        var user = await _fixture.AddUserAsync("ada@contoso.com");
        var member = await _fixture.AddTeamMemberAsync(otherTeam.Id, user.Id);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService(ctx).UpdateRoleAsync(
            team.Id, user.Id, new UpdateTeamMemberActionDto(Roles.TeamAdministrator, Convert.ToBase64String(member.Version)), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveAsync_Member_DeactivatesMembershipOnlyAndKeepsOtherTeams()
    {
        var team = await _fixture.AddTeamAsync();
        var otherTeam = await _fixture.AddTeamAsync();
        var user = await _fixture.AddUserAsync("ada@contoso.com");
        await _fixture.AddTeamMemberAsync(team.Id, user.Id);
        await _fixture.AddTeamMemberAsync(otherTeam.Id, user.Id);

        await RemoveAsync(team.Id, user.Id);

        await using var ctx = _fixture.CreateContext();
        Assert.True(await ctx.Users.AnyAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken));
        Assert.Equal([otherTeam.Id], await ctx.TeamMembers.Where(m => m.UserId == user.Id).Select(m => m.TeamId).ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveAsync_LastAdministrator_ThrowsConflict()
    {
        var team = await _fixture.AddTeamAsync();
        var user = await _fixture.AddUserAsync("ada@contoso.com");
        await _fixture.AddTeamMemberAsync(team.Id, user.Id, Roles.TeamAdministrator);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ConflictException>(() => CreateService(ctx).RemoveAsync(team.Id, user.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveAsync_NotAMember_ThrowsNotFound()
    {
        var team = await _fixture.AddTeamAsync();
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService(ctx).RemoveAsync(team.Id, Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListAsync_Team_ReturnsActiveMembersOrderedByName()
    {
        var team = await _fixture.AddTeamAsync();
        var otherTeam = await _fixture.AddTeamAsync();
        var grace = await _fixture.AddUserAsync("grace@contoso.com", "Grace", "Hopper");
        var charles = await _fixture.AddUserAsync("charles@contoso.com", "Charles", "Babbage");
        var outsider = await _fixture.AddUserAsync("alan@contoso.com", "Alan", "Turing");
        await _fixture.AddTeamMemberAsync(team.Id, grace.Id, Roles.TeamAdministrator);
        await _fixture.AddTeamMemberAsync(team.Id, charles.Id);
        await _fixture.AddTeamMemberAsync(otherTeam.Id, outsider.Id);
        await using var ctx = _fixture.CreateContext();

        var page = await CreateService(ctx).ListAsync(team.Id, new ListTeamMembersActionDto(), TestContext.Current.CancellationToken);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(["Charles Babbage", "Grace Hopper"], page.Items.Select(m => m.FullName));
        Assert.Equal([Roles.TeamMember, Roles.TeamAdministrator], page.Items.Select(m => m.Role));
    }

    public void Dispose() => _fixture.Dispose();

    #region Private methods

    private TeamMemberService CreateService(AndesMailerDbContext ctx) => new(ctx, new UserService(ctx, _directory));

    private async Task RemoveAsync(Guid teamId, Guid userId)
    {
        await using var ctx = _fixture.CreateContext();
        await CreateService(ctx).RemoveAsync(teamId, userId, TestContext.Current.CancellationToken);
    }

    #endregion
}
