using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Teams;
using Andes.Mailer.Dto.Pagination;
using Andes.Mailer.Dto.Teams;
using Andes.Mailer.Entity.Teams;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Emails;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Service.Security;
using Andes.Mailer.Service.Users;
using Andes.Mailer.Service.Users.Models;

using Microsoft.EntityFrameworkCore;

namespace Andes.Mailer.Service.Teams;

/// <summary>
/// Manages the teams of the application and the service accounts they send from.
/// </summary>
public interface ITeamService
{
    /// <summary>
    /// Lists one page of the teams the caller can see, ordered by name: every team for a caller whose global role grants
    /// <see cref="Permissions.ViewTeam"/>, otherwise only the caller's own teams.
    /// </summary>
    /// <param name="callerId">The Entra object id of the signed-in user.</param>
    /// <param name="action">The page to return.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The page, with the caller's role in each team.</returns>
    Task<PaginatedResponseDto<TeamSummaryDto>> ListAsync(Guid callerId, ListTeamsActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Gets an active team.
    /// </summary>
    /// <param name="id">The team's id.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The team.</returns>
    /// <exception cref="NotFoundException">No active team has the id.</exception>
    Task<TeamDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a team that sends from the directory mailbox that owns an email address.
    /// </summary>
    /// <param name="action">The name, description, and service account's address.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The new team.</returns>
    /// <exception cref="NotFoundException">No directory account owns the service account's address.</exception>
    /// <exception cref="ConflictException">An active team already has the name or the service account.</exception>
    Task<TeamDto> CreateAsync(CreateTeamActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces an active team's details and settings.
    /// </summary>
    /// <param name="callerId">The Entra object id of the signed-in user; only a caller whose global role grants <see cref="Permissions.ModifyTeam"/> may change the service account.</param>
    /// <param name="id">The team's id.</param>
    /// <param name="action">The new details and settings and the row version the caller last read.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated team.</returns>
    /// <exception cref="NotFoundException">No active team has the id, or no directory account owns a new service account's address.</exception>
    /// <exception cref="ForbiddenException">The service account changes and the caller holds <see cref="Permissions.ModifyTeam"/> only through a team role.</exception>
    /// <exception cref="ConflictException">Another active team has the name or the service account, or the team changed since the caller read it.</exception>
    Task<TeamDto> UpdateAsync(Guid callerId, Guid id, UpdateTeamActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Deactivates an active team, which hides its memberships and emails with it.
    /// </summary>
    /// <param name="id">The team's id.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the team is deactivated.</returns>
    /// <exception cref="NotFoundException">No active team has the id.</exception>
    /// <exception cref="ConflictException">The team changed during the operation.</exception>
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// Manages the teams of the application in the SQL store, verifying service accounts against the directory.
/// </summary>
/// <param name="ctx">The store.</param>
/// <param name="directory">The directory service accounts are verified against.</param>
/// <param name="permissions">The permission checks that decide which teams a caller can list.</param>
public sealed class TeamService(AndesMailerDbContext ctx, IDirectoryUserService directory, IPermissionService permissions) : ITeamService
{
    private const string NotFoundMessage = "The team was not found.";

    private readonly AndesMailerDbContext _ctx = ctx;
    private readonly IDirectoryUserService _directory = directory;
    private readonly IPermissionService _permissions = permissions;

    /// <inheritdoc/>
    public async Task<PaginatedResponseDto<TeamSummaryDto>> ListAsync(Guid callerId, ListTeamsActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var teams = _ctx.Teams.AsNoTracking();
        if (!await _permissions.HasPermissionAsync(callerId, Permissions.ViewTeam, cancellationToken))
        {
            teams = teams.Where(t => t.Members.Any(m => m.UserId == callerId));
        }

        var total = await teams.CountAsync(cancellationToken);
        var items = await teams
            .OrderBy(t => t.Name)
            .ThenBy(t => t.Id)
            .Skip((action.Page - 1) * action.PageSize)
            .Take(action.PageSize)
            .Select(t => new TeamSummaryDto(
                t.Id,
                t.Name,
                t.Description,
                t.ServiceAccountEmail,
                t.AvatarColorId,
                t.Members.Count,
                t.Members.Where(m => m.UserId == callerId).Select(m => (Roles?)m.RoleId).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new PaginatedResponseDto<TeamSummaryDto>(items, action.Page, action.PageSize, total);
    }

    /// <inheritdoc/>
    public async Task<TeamDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var team = await _ctx.Teams
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(TeamMapper.MapToTeamDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);

        return team ?? throw new NotFoundException(NotFoundMessage);
    }

    /// <inheritdoc/>
    public async Task<TeamDto> CreateAsync(CreateTeamActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var account = await FindServiceAccountAsync(action.ServiceAccountEmail, cancellationToken);
        var team = new Team
        {
            Name = action.Name.Trim(),
            Description = NormalizeDescription(action.Description),
            ServiceAccountEmail = account.Email,
            ServiceAccountId = account.Id,
            AvatarColorId = action.AvatarColor,
        };
        await EnsureAvailableAsync(team, cancellationToken);
        _ctx.Add(team);
        await SaveAsync(team, cancellationToken);

        return TeamMapper.MapToNewTeamDto(team);
    }

    /// <inheritdoc/>
    public async Task<TeamDto> UpdateAsync(Guid callerId, Guid id, UpdateTeamActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var team = await _ctx.Teams.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);

        // The directory is asked again only for a new address, so renaming a team never depends on Graph being reachable.
        if (!string.Equals(action.ServiceAccountEmail.Trim(), team.ServiceAccountEmail, StringComparison.OrdinalIgnoreCase))
        {
            // A team administrator edits their own team, but the mailbox decides who the mail comes from, so it stays with the global role.
            if (!await _permissions.HasPermissionAsync(callerId, Permissions.ModifyTeam, cancellationToken))
            {
                throw new ForbiddenException("Only an administrator can change the team's service mailbox.");
            }

            var account = await FindServiceAccountAsync(action.ServiceAccountEmail, cancellationToken);
            team.ServiceAccountEmail = account.Email;
            team.ServiceAccountId = account.Id;
        }

        // The version the client last read, so the UPDATE matches no row if anyone changed the team since.
        _ctx.Entry(team).Property(t => t.Version).OriginalValue = Convert.FromBase64String(action.Version);
        team.Name = action.Name.Trim();
        team.Description = NormalizeDescription(action.Description);
        team.AvatarColorId = action.AvatarColor;
        team.DefaultSignature = NormalizeSignature(action.DefaultSignature);
        team.ConfirmRecipientsAbove = action.ConfirmRecipientsAbove;
        await EnsureAvailableAsync(team, cancellationToken);
        await SaveAsync(team, cancellationToken);

        // Not cancellable: the change is committed, and a cancelled read would free the Idempotency-Key, so a retry would hit the
        // stale version as a 409 instead of replaying this result.
        return await GetAsync(id, CancellationToken.None);
    }

    /// <inheritdoc/>
    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var team = await _ctx.Teams.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);

        // DeactivationInterceptor turns the delete into a DateDeactivated stamp; the membership and email filters hide the team's rows with it.
        _ctx.Remove(team);
        await SaveAsync(null, cancellationToken);
    }

    #region Private methods

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    // Sanitized like an email body, because the signature is pasted into new emails and rendered as HTML.
    private static string? NormalizeSignature(string? signature)
    {
        var sanitized = EmailBodySanitizer.Sanitize(signature ?? string.Empty);

        return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
    }

    private async Task<DirectoryUser> FindServiceAccountAsync(string email, CancellationToken cancellationToken) =>
        await _directory.FindByEmailAsync(email.Trim().ToLowerInvariant(), cancellationToken)
            ?? throw new NotFoundException("No directory account has the service account's email.");

    // A fresh query, not the tracker, so it sees only what is committed, never the team being saved.
    private async Task EnsureAvailableAsync(Team team, CancellationToken cancellationToken)
    {
        var others = _ctx.Teams.AsNoTracking().Where(t => t.Id != team.Id);
        if (await others.AnyAsync(t => t.Name == team.Name, cancellationToken))
        {
            throw new ConflictException("A team with this name already exists.");
        }

        // By object id too: a mailbox's UPN and mail address are two spellings of the same account.
        if (await others.AnyAsync(t => t.ServiceAccountEmail == team.ServiceAccountEmail || t.ServiceAccountId == team.ServiceAccountId, cancellationToken))
        {
            throw new ConflictException("Another team already sends from this service account.");
        }
    }

    private async Task SaveAsync(Team? saved, CancellationToken cancellationToken)
    {
        try
        {
            await _ctx.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The team was changed by another request. Read it again and retry.");
        }
        catch (DbUpdateException) when (saved is not null)
        {
            // Re-checked rather than read from the provider's error number, which would bring SQL Server types into Service.
            // Any other store fault propagates, so GlobalExceptionHandler logs it as a 500.
            await EnsureAvailableAsync(saved, cancellationToken);

            throw;
        }
    }

    #endregion
}
