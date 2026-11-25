using System.Linq.Expressions;

using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Teams;
using Andes.Mailer.Entity.Teams;
using Andes.Mailer.Entity.Users;

namespace Andes.Mailer.Service.Teams;

/// <summary>
/// Maps teams and their members to response DTOs.
/// </summary>
public static class TeamMapper
{
    /// <summary>
    /// Gets the projection that maps a team to a <see cref="TeamDto"/> inside a query.
    /// </summary>
    /// <remarks>
    /// The member counts read the <see cref="Team.Members"/> navigation, so the membership query filters drop removed members and deactivated users.
    /// </remarks>
    public static Expression<Func<Team, TeamDto>> MapToTeamDtoExpression { get; } = team => new TeamDto(
        team.Id,
        team.Name,
        team.Description,
        team.ServiceAccountEmail,
        team.AvatarColorId,
        team.DefaultSignature,
        team.ConfirmRecipientsAbove,
        team.Members.Count,
        team.Members.Count(m => m.RoleId == Roles.TeamAdministrator),
        team.DateCreated,
        team.DateModified,
        Convert.ToBase64String(team.Version));

    /// <summary>
    /// Gets the projection that maps a membership, with its user, to a <see cref="TeamMemberDto"/> inside a query.
    /// </summary>
    /// <remarks>
    /// It calls <see cref="User.ComposeFullName"/> rather than reading <see cref="User.FullName"/>, which is not mapped and so cannot be read from a query.
    /// </remarks>
    public static Expression<Func<TeamMember, TeamMemberDto>> MapToTeamMemberDtoExpression { get; } = member => new TeamMemberDto(
        member.UserId,
        member.User!.FirstName,
        member.User.LastName,
        User.ComposeFullName(member.User.FirstName, member.User.LastName),
        member.User.Email,
        member.RoleId,
        member.DateCreated,
        member.DateModified,
        Convert.ToBase64String(member.Version));

    #region Public static methods

    /// <summary>
    /// Maps a team that has no members yet, such as one just created, to a <see cref="TeamDto"/>.
    /// </summary>
    /// <param name="team">The team.</param>
    /// <returns>The DTO, with both member counts at zero.</returns>
    public static TeamDto MapToNewTeamDto(Team team)
    {
        ArgumentNullException.ThrowIfNull(team);

        return new TeamDto(
            team.Id,
            team.Name,
            team.Description,
            team.ServiceAccountEmail,
            team.AvatarColorId,
            team.DefaultSignature,
            team.ConfirmRecipientsAbove,
            0,
            0,
            team.DateCreated,
            team.DateModified,
            Convert.ToBase64String(team.Version));
    }

    #endregion
}
