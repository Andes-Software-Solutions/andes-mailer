using System.Linq.Expressions;

using Andes.Mailer.Dto.ContactGroups;
using Andes.Mailer.Entity.ContactGroups;

namespace Andes.Mailer.Service.ContactGroups;

/// <summary>
/// Maps contact groups and their members to response DTOs.
/// </summary>
public static class ContactGroupMapper
{
    /// <summary>
    /// Gets the projection that maps a group to a <see cref="ContactGroupDto"/> inside a query.
    /// </summary>
    /// <remarks>
    /// The ordering matches <see cref="MapToContactGroupDto"/>, so a group reads back in the order it was saved whichever path built the DTO.
    /// </remarks>
    public static Expression<Func<ContactGroup, ContactGroupDto>> MapToContactGroupDtoExpression { get; } = group => new ContactGroupDto(
        group.Id,
        group.Name,
        group.Description,
        group.Members
            .OrderBy(m => m.Position)
            .Select(m => new ContactGroupMemberDto(m.Address, m.UserId, m.DisplayName))
            .ToList(),
        group.DateCreated,
        group.DateModified,
        Convert.ToBase64String(group.Version));

    /// <summary>
    /// Gets the projection that maps a group to a <see cref="ContactGroupSummaryDto"/> inside a query.
    /// </summary>
    public static Expression<Func<ContactGroup, ContactGroupSummaryDto>> MapToContactGroupSummaryDtoExpression { get; } = group => new ContactGroupSummaryDto(
        group.Id,
        group.Name,
        group.Description,
        group.Members.Count);

    #region Public static methods

    /// <summary>
    /// Maps a loaded group, with its members, to a <see cref="ContactGroupDto"/>; members removed in the same unit of work are left out.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The DTO.</returns>
    public static ContactGroupDto MapToContactGroupDto(ContactGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        return new ContactGroupDto(
            group.Id,
            group.Name,
            group.Description,
            [.. group.Members
                .Where(m => m.DateDeactivated is null)
                .OrderBy(m => m.Position)
                .Select(m => new ContactGroupMemberDto(m.Address, m.UserId, m.DisplayName))],
            group.DateCreated,
            group.DateModified,
            Convert.ToBase64String(group.Version));
    }

    #endregion
}
