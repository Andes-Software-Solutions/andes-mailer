using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Teams;
using Andes.Mailer.Dto.Pagination;
using Andes.Mailer.Dto.Teams;
using Andes.Mailer.Entity.Teams;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Service.Users;

using Microsoft.EntityFrameworkCore;

namespace Andes.Mailer.Service.Teams;

/// <summary>
/// Manages who belongs to a team and the role each member holds in it. Every operation is scoped to one active team,
/// which the caller has already been checked against.
/// </summary>
public interface ITeamMemberService
{
    /// <summary>
    /// Lists one page of a team's active members, ordered by last name, then first name.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="action">The page to return.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The page.</returns>
    Task<PaginatedResponseDto<TeamMemberDto>> ListAsync(Guid teamId, ListTeamMembersActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Gets an active member of a team.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="userId">The member's Entra object id.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The member.</returns>
    /// <exception cref="NotFoundException">The user is not an active member of the team.</exception>
    Task<TeamMemberDto> GetAsync(Guid teamId, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the person who owns an email address to a team, adding them as a user first if they are not one yet, or reactivates
    /// their membership if they were removed.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="action">The person's email address and the role they hold in the team.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The new member.</returns>
    /// <exception cref="NotFoundException">No user has the address and no directory account owns it.</exception>
    /// <exception cref="ConflictException">
    /// The person is already an active member of the team, is a user an administrator deactivated, or was added by another request at the same time.
    /// </exception>
    Task<TeamMemberDto> AddAsync(Guid teamId, AddTeamMemberActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Changes the role a member holds in a team.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="userId">The member's Entra object id.</param>
    /// <param name="action">The new role and the membership's row version the caller last read.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated member.</returns>
    /// <exception cref="NotFoundException">The user is not an active member of the team.</exception>
    /// <exception cref="ConflictException">The change would leave the team without a team administrator, or the membership or team changed since the caller read it.</exception>
    Task<TeamMemberDto> UpdateRoleAsync(Guid teamId, Guid userId, UpdateTeamMemberActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a member from a team. The user stays active and stays in their other teams.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="userId">The member's Entra object id.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the membership is deactivated.</returns>
    /// <exception cref="NotFoundException">The user is not an active member of the team.</exception>
    /// <exception cref="ConflictException">The member is the team's last team administrator, or the membership or team changed during the operation.</exception>
    Task RemoveAsync(Guid teamId, Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// Manages team memberships in the SQL store, adding new people as users through <see cref="IUserService"/>.
/// </summary>
/// <param name="ctx">The store.</param>
/// <param name="users">The users new members are added as.</param>
public sealed class TeamMemberService(AndesMailerDbContext ctx, IUserService users) : ITeamMemberService
{
    private const string NotFoundMessage = "The member was not found.";

    private readonly AndesMailerDbContext _ctx = ctx;
    private readonly IUserService _users = users;

    /// <inheritdoc/>
    public async Task<PaginatedResponseDto<TeamMemberDto>> ListAsync(Guid teamId, ListTeamMembersActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var members = _ctx.TeamMembers.AsNoTracking().Where(m => m.TeamId == teamId);
        var total = await members.CountAsync(cancellationToken);
        var items = await members
            .OrderBy(m => m.User!.LastName)
            .ThenBy(m => m.User!.FirstName)
            .ThenBy(m => m.UserId)
            .Skip((action.Page - 1) * action.PageSize)
            .Take(action.PageSize)
            .Select(TeamMapper.MapToTeamMemberDtoExpression)
            .ToListAsync(cancellationToken);

        return new PaginatedResponseDto<TeamMemberDto>(items, action.Page, action.PageSize, total);
    }

    /// <inheritdoc/>
    public async Task<TeamMemberDto> GetAsync(Guid teamId, Guid userId, CancellationToken cancellationToken) =>
        await _ctx.TeamMembers
            .AsNoTracking()
            .Where(m => m.TeamId == teamId && m.UserId == userId)
            .Select(TeamMapper.MapToTeamMemberDtoExpression)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);

    /// <inheritdoc/>
    public async Task<TeamMemberDto> AddAsync(Guid teamId, AddTeamMemberActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var userId = await _users.EnsureActiveAsync(action.Email, cancellationToken);

        // Unique over every row, so a removed member comes back as the same row rather than a second one.
        var member = await _ctx.TeamMembers
            .IgnoreQueryFilters(["Deactivation"])
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId, cancellationToken);
        if (member is null)
        {
            _ctx.Add(new TeamMember { TeamId = teamId, UserId = userId, RoleId = action.Role });
        }
        else if (member.DateDeactivated is null)
        {
            throw new ConflictException("This user is already a member of the team.");
        }
        else
        {
            member.DateDeactivated = null;
            member.RoleId = action.Role;
        }

        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Re-checked rather than read from the provider's error number, which would bring SQL Server types into Service.
            // Any other store fault propagates, so GlobalExceptionHandler logs it as a 500.
            if (await _ctx.TeamMembers.AsNoTracking().AnyAsync(m => m.TeamId == teamId && m.UserId == userId, cancellationToken))
            {
                throw new ConflictException("Another request added this user to the team at the same time.");
            }

            throw;
        }

        return await GetAsync(teamId, userId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TeamMemberDto> UpdateRoleAsync(Guid teamId, Guid userId, UpdateTeamMemberActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var member = await FindAsync(teamId, userId, cancellationToken);
        if (member.RoleId == Roles.TeamAdministrator && action.Role != Roles.TeamAdministrator)
        {
            await GuardLastAdministratorAsync(teamId, userId, cancellationToken);
        }

        // The version the client last read, so the UPDATE matches no row if anyone changed the membership since.
        _ctx.Entry(member).Property(m => m.Version).OriginalValue = Convert.FromBase64String(action.Version);
        member.RoleId = action.Role;
        await SaveAsync(cancellationToken);

        return await GetAsync(teamId, userId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(Guid teamId, Guid userId, CancellationToken cancellationToken)
    {
        var member = await FindAsync(teamId, userId, cancellationToken);
        if (member.RoleId == Roles.TeamAdministrator)
        {
            await GuardLastAdministratorAsync(teamId, userId, cancellationToken);
        }

        // DeactivationInterceptor turns the delete into a DateDeactivated stamp; the user row is untouched.
        _ctx.Remove(member);
        await SaveAsync(cancellationToken);
    }

    #region Private methods

    private async Task<TeamMember> FindAsync(Guid teamId, Guid userId, CancellationToken cancellationToken) =>
        await _ctx.TeamMembers.FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);

    // Two requests could each demote a different administrator after both counted two. Saving the team row with the membership
    // makes its version the arbiter: the second save matches no row and fails as a 409, rolling its membership change back.
    private async Task GuardLastAdministratorAsync(Guid teamId, Guid userId, CancellationToken cancellationToken)
    {
        var team = await _ctx.Teams.FirstAsync(t => t.Id == teamId, cancellationToken);
        var othersRemain = await _ctx.TeamMembers
            .AsNoTracking()
            .AnyAsync(m => m.TeamId == teamId && m.UserId != userId && m.RoleId == Roles.TeamAdministrator, cancellationToken);
        if (!othersRemain)
        {
            throw new ConflictException("A team must keep at least one team administrator. Make another member a team administrator first.");
        }

        _ctx.Entry(team).State = EntityState.Modified;
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _ctx.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The member or team was changed by another request. Read it again and retry.");
        }
    }

    #endregion
}
