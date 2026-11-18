using Andes.Mailer.Common.Constants.ContactGroups;
using Andes.Mailer.Dto.Actions.ContactGroups;
using Andes.Mailer.Dto.ContactGroups;
using Andes.Mailer.Dto.Pagination;
using Andes.Mailer.Entity.ContactGroups;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Service.Users;

using Microsoft.EntityFrameworkCore;

namespace Andes.Mailer.Service.ContactGroups;

/// <summary>
/// Manages a team's contact groups: named lists of addresses its members add to emails in one step.
/// </summary>
public interface IContactGroupService
{
    /// <summary>
    /// Lists one page of a team's active contact groups, ordered by name.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="action">The page to return, and optional text the names must contain.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The page, with each group's member count.</returns>
    Task<PaginatedResponseDto<ContactGroupSummaryDto>> ListAsync(Guid teamId, ListContactGroupsActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Gets one of a team's active contact groups with its members.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="id">The group's id.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The group.</returns>
    /// <exception cref="NotFoundException">The team has no active group with the id.</exception>
    Task<ContactGroupDto> GetAsync(Guid teamId, Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a contact group in a team, naming each member found in the directory.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="callerId">The Entra object id of the signed-in user, recorded as the group's creator and each member's adder.</param>
    /// <param name="action">The name, description, and members.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The new group.</returns>
    /// <exception cref="ConflictException">The team already has an active group with the name.</exception>
    Task<ContactGroupDto> CreateAsync(Guid teamId, Guid callerId, CreateContactGroupActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the name, description, and members of one of a team's active contact groups.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="id">The group's id.</param>
    /// <param name="callerId">The Entra object id of the signed-in user, recorded as the adder of new members and the remover of dropped ones.</param>
    /// <param name="action">The new content and the row version the caller last read.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated group.</returns>
    /// <exception cref="NotFoundException">The team has no active group with the id.</exception>
    /// <exception cref="ConflictException">Another active group of the team has the name, or the group changed since the caller read it.</exception>
    Task<ContactGroupDto> UpdateAsync(Guid teamId, Guid id, Guid callerId, UpdateContactGroupActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Deactivates one of a team's active contact groups. Emails the group was added to keep their recipients.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="id">The group's id.</param>
    /// <param name="callerId">The Entra object id of the signed-in user, recorded as the group's deactivator.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the group is deactivated.</returns>
    /// <exception cref="NotFoundException">The team has no active group with the id.</exception>
    /// <exception cref="ConflictException">The group changed during the operation.</exception>
    Task DeactivateAsync(Guid teamId, Guid id, Guid callerId, CancellationToken cancellationToken);
}

/// <summary>
/// Manages contact groups in the SQL store, naming members from the directory.
/// </summary>
/// <param name="ctx">The store.</param>
/// <param name="directory">The directory members are looked up in.</param>
/// <param name="time">The clock that stamps deactivations.</param>
public sealed class ContactGroupService(AndesMailerDbContext ctx, IDirectoryUserService directory, TimeProvider time) : IContactGroupService
{
    private const string NotFoundMessage = "The contact group was not found.";

    private readonly AndesMailerDbContext _ctx = ctx;
    private readonly IDirectoryUserService _directory = directory;
    private readonly TimeProvider _time = time;

    /// <inheritdoc/>
    public async Task<PaginatedResponseDto<ContactGroupSummaryDto>> ListAsync(Guid teamId, ListContactGroupsActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var groups = _ctx.ContactGroups.AsNoTracking().Where(g => g.TeamId == teamId);
        if (!string.IsNullOrWhiteSpace(action.Search))
        {
            var search = action.Search.Trim();
            groups = groups.Where(g => g.Name.Contains(search));
        }

        var total = await groups.CountAsync(cancellationToken);
        var items = await groups
            .OrderBy(g => g.Name)
            .ThenBy(g => g.Id)
            .Skip((action.Page - 1) * action.PageSize)
            .Take(action.PageSize)
            .Select(ContactGroupMapper.MapToContactGroupSummaryDtoExpression)
            .ToListAsync(cancellationToken);

        return new PaginatedResponseDto<ContactGroupSummaryDto>(items, action.Page, action.PageSize, total);
    }

    /// <inheritdoc/>
    public async Task<ContactGroupDto> GetAsync(Guid teamId, Guid id, CancellationToken cancellationToken)
    {
        var group = await _ctx.ContactGroups
            .AsNoTracking()
            .Where(g => g.Id == id && g.TeamId == teamId)
            .Select(ContactGroupMapper.MapToContactGroupDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);

        return group ?? throw new NotFoundException(NotFoundMessage);
    }

    /// <inheritdoc/>
    public async Task<ContactGroupDto> CreateAsync(Guid teamId, Guid callerId, CreateContactGroupActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var group = new ContactGroup
        {
            TeamId = teamId,
            Name = action.Name.Trim(),
            Description = NormalizeDescription(action.Description),
            CreatedById = callerId,
        };
        await EnsureNameAvailableAsync(group, cancellationToken);
        await ApplyMembersAsync(group, callerId, action.Members, cancellationToken);
        _ctx.Add(group);
        await SaveAsync(group, cancellationToken);

        return ContactGroupMapper.MapToContactGroupDto(group);
    }

    /// <inheritdoc/>
    public async Task<ContactGroupDto> UpdateAsync(Guid teamId, Guid id, Guid callerId, UpdateContactGroupActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var group = await _ctx.ContactGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == id && g.TeamId == teamId, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);

        // The version the client last read, so the UPDATE matches no row if anyone changed the group since.
        _ctx.Entry(group).Property(g => g.Version).OriginalValue = Convert.FromBase64String(action.Version);
        group.Name = action.Name.Trim();
        group.Description = NormalizeDescription(action.Description);
        await EnsureNameAvailableAsync(group, cancellationToken);
        await ApplyMembersAsync(group, callerId, action.Members, cancellationToken);

        // A members-only change touches no column of the group itself; marking it modified keeps the version check and bumps the version.
        _ctx.Entry(group).State = EntityState.Modified;
        await SaveAsync(group, cancellationToken);

        return ContactGroupMapper.MapToContactGroupDto(group);
    }

    /// <inheritdoc/>
    public async Task DeactivateAsync(Guid teamId, Guid id, Guid callerId, CancellationToken cancellationToken)
    {
        var group = await _ctx.ContactGroups.FirstOrDefaultAsync(g => g.Id == id && g.TeamId == teamId, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);

        // Stamped here rather than through _ctx.Remove: DeactivationInterceptor writes only DateDeactivated, which would drop who did it.
        // The members keep their rows, which the group filter hides with it.
        group.DateDeactivated = _time.GetUtcNow();
        group.DeactivatedById = callerId;
        await SaveAsync(null, cancellationToken);
    }

    #region Private methods

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    // Keeps the rows of addresses that stay, so their directory lookup is not repeated and unchanged members are not rewritten.
    // A dropped address is deactivated, not deleted, so the group's rows keep who added and removed each address and when.
    private async Task ApplyMembersAsync(ContactGroup group, Guid callerId, IReadOnlyList<ContactGroupMemberActionDto> members, CancellationToken cancellationToken)
    {
        var wanted = members
            .Select((m, position) => (Address: m.Address.Trim().ToLowerInvariant(), Position: position))
            .ToList();
        var existing = group.Members.Where(m => m.DateDeactivated is null).ToDictionary(m => m.Address, StringComparer.Ordinal);
        var addresses = wanted.Select(w => w.Address).ToHashSet(StringComparer.Ordinal);
        var now = _time.GetUtcNow();
        foreach (var stale in existing.Values.Where(m => !addresses.Contains(m.Address)))
        {
            // Stamped here rather than through _ctx.Remove: DeactivationInterceptor writes only DateDeactivated, which would drop RemovedById.
            stale.DateDeactivated = now;
            stale.RemovedById = callerId;
        }

        var newAddresses = wanted.Where(w => !existing.ContainsKey(w.Address)).Select(w => w.Address).ToList();
        var accounts = await _directory.FindByEmailsAsync(newAddresses, cancellationToken);
        foreach (var (address, position) in wanted)
        {
            if (existing.TryGetValue(address, out var member))
            {
                member.Position = position;
                continue;
            }

            var account = accounts.GetValueOrDefault(address);
            group.Members.Add(new ContactGroupMember
            {
                Address = address,
                Position = position,
                AddedById = callerId,
                UserId = account?.Id,
                DisplayName = account is null ? null : DirectoryUserNames.DisplayNameOf(account, ContactGroupLimits.DisplayNameMaxLength),
            });
        }
    }

    // A fresh query, not the tracker, so it sees only what is committed, never the group being saved.
    private async Task EnsureNameAvailableAsync(ContactGroup group, CancellationToken cancellationToken)
    {
        if (await _ctx.ContactGroups.AsNoTracking().AnyAsync(g => g.TeamId == group.TeamId && g.Id != group.Id && g.Name == group.Name, cancellationToken))
        {
            throw new ConflictException("The team already has a contact group with this name.");
        }
    }

    private async Task SaveAsync(ContactGroup? saved, CancellationToken cancellationToken)
    {
        try
        {
            await _ctx.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The contact group was changed by another request. Read it again and retry.");
        }
        catch (DbUpdateException) when (saved is not null)
        {
            // Re-checked rather than read from the provider's error number, which would bring SQL Server types into Service.
            // Any other store fault propagates, so GlobalExceptionHandler logs it as a 500.
            await EnsureNameAvailableAsync(saved, cancellationToken);

            throw;
        }
    }

    #endregion
}
