using Andes.Mailer.Dto.Actions.Users;
using Andes.Mailer.Dto.Pagination;
using Andes.Mailer.Dto.Users;
using Andes.Mailer.Entity.Users;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Service.Users.Models;

using Microsoft.EntityFrameworkCore;

namespace Andes.Mailer.Service.Users;

/// <summary>
/// Manages the users of the application.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Lists one page of active users, ordered by last name, then first name.
    /// </summary>
    /// <param name="action">The page to return.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The page.</returns>
    Task<PaginatedResponseDto<UserDto>> ListAsync(ListUsersActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Gets an active user.
    /// </summary>
    /// <param name="id">The user's Entra object id.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The user.</returns>
    /// <exception cref="NotFoundException">No active user has the id.</exception>
    Task<UserDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the directory account that owns an email address as a user, or reactivates it if it was deactivated.
    /// </summary>
    /// <param name="action">The email address to look up.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The added or reactivated user.</returns>
    /// <exception cref="NotFoundException">No directory account owns the address.</exception>
    /// <exception cref="ConflictException">An active user already has the address or the directory account.</exception>
    Task<UserDto> CreateAsync(CreateUserActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the active user an email address belongs to, adding the directory account that owns it as a new user if there is none.
    /// </summary>
    /// <param name="email">The email address to look up.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The active user's Entra object id.</returns>
    /// <exception cref="NotFoundException">No user has the address and no directory account owns it.</exception>
    /// <exception cref="ConflictException">The directory account is a deactivated user, or another request added the user at the same time.</exception>
    Task<Guid> EnsureActiveAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Changes an active user's name.
    /// </summary>
    /// <param name="id">The user's Entra object id.</param>
    /// <param name="action">The new name and the row version the caller last read.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The updated user.</returns>
    /// <exception cref="NotFoundException">No active user has the id.</exception>
    /// <exception cref="ConflictException">The user changed since the caller read it.</exception>
    Task<UserDto> UpdateAsync(Guid id, UpdateUserActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Deactivates an active user and revokes their roles and team memberships, so adding them again later grants no access until it is re-granted.
    /// </summary>
    /// <param name="id">The user's Entra object id.</param>
    /// <param name="callerId">The Entra object id of the signed-in user.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the user is deactivated.</returns>
    /// <exception cref="NotFoundException">No active user has the id.</exception>
    /// <exception cref="ConflictException">The caller is deactivating themselves, or the user changed during the operation.</exception>
    Task DeactivateAsync(Guid id, Guid callerId, CancellationToken cancellationToken);
}

/// <summary>
/// Manages the users of the application in the SQL store, taking new users' details from the directory.
/// </summary>
/// <param name="ctx">The store.</param>
/// <param name="directory">The directory that new users come from.</param>
public sealed class UserService(AndesMailerDbContext ctx, IDirectoryUserService directory) : IUserService
{
    private const string NotFoundMessage = "The user was not found.";

    private readonly AndesMailerDbContext _ctx = ctx;
    private readonly IDirectoryUserService _directory = directory;

    /// <inheritdoc/>
    public async Task<PaginatedResponseDto<UserDto>> ListAsync(ListUsersActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var users = _ctx.Users.AsNoTracking();
        var total = await users.CountAsync(cancellationToken);
        var items = await users
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ThenBy(u => u.Id)
            .Skip((action.Page - 1) * action.PageSize)
            .Take(action.PageSize)
            .Select(UserMapper.MapToUserDtoExpression)
            .ToListAsync(cancellationToken);

        return new PaginatedResponseDto<UserDto>(items, action.Page, action.PageSize, total);
    }

    /// <inheritdoc/>
    public async Task<UserDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await _ctx.Users
            .AsNoTracking()
            .Where(u => u.Id == id)
            .Select(UserMapper.MapToUserDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);

        return user ?? throw new NotFoundException(NotFoundMessage);
    }

    /// <inheritdoc/>
    public async Task<UserDto> CreateAsync(CreateUserActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var email = NormalizeEmail(action.Email);
        if (await _ctx.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new ConflictException("A user with this email already exists.");
        }

        var account = await FindAccountAsync(email, cancellationToken);
        var user = await FindIncludingDeactivatedAsync(account.Id, cancellationToken);
        if (user is { DateDeactivated: null })
        {
            throw new ConflictException("This directory account is already a user under another email.");
        }

        return UserMapper.MapToUserDto(await AddOrReactivateAsync(user, account, cancellationToken));
    }

    /// <inheritdoc/>
    public async Task<Guid> EnsureActiveAsync(string email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        var normalized = NormalizeEmail(email);
        var existingId = await _ctx.Users
            .Where(u => u.Email == normalized)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingId is not null)
        {
            return existingId.Value;
        }

        var account = await FindAccountAsync(normalized, cancellationToken);
        var user = await FindIncludingDeactivatedAsync(account.Id, cancellationToken);

        // An address the directory resolves to an account that is already active under its other address, such as the UPN rather than the mail.
        if (user is { DateDeactivated: null })
        {
            return user.Id;
        }

        // Deactivating a user is an administrator's decision, so only CreateAsync, behind CreateUser, may undo it.
        if (user is not null)
        {
            throw new ConflictException("This user was deactivated. An administrator must add them again before they can join a team.");
        }

        return (await AddOrReactivateAsync(null, account, cancellationToken)).Id;
    }

    /// <inheritdoc/>
    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var user = await _ctx.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);

        // The version the client last read, so the UPDATE matches no row if anyone changed the user since.
        _ctx.Entry(user).Property(u => u.Version).OriginalValue = Convert.FromBase64String(action.Version);
        user.FirstName = action.FirstName.Trim();
        user.LastName = action.LastName.Trim();
        await SaveAsync(cancellationToken);

        return UserMapper.MapToUserDto(user);
    }

    /// <inheritdoc/>
    public async Task DeactivateAsync(Guid id, Guid callerId, CancellationToken cancellationToken)
    {
        if (id == callerId)
        {
            throw new ConflictException("You cannot deactivate your own user.");
        }

        var user = await _ctx.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);

        // Roles and memberships are revoked, not just hidden, so re-adding the user later restores no access nobody re-granted.
        _ctx.RemoveRange(await _ctx.UserRoles.Where(ur => ur.UserId == id).ToListAsync(cancellationToken));
        // Deactivated teams' memberships too, so reactivating such a team later brings back nobody who was deactivated meanwhile.
        _ctx.RemoveRange(await _ctx.TeamMembers
            .IgnoreQueryFilters(["TeamDeactivation"])
            .Where(m => m.UserId == id)
            .ToListAsync(cancellationToken));

        // DeactivationInterceptor turns the user's and the memberships' deletes into DateDeactivated stamps.
        _ctx.Remove(user);
        await SaveAsync(cancellationToken);
    }

    #region Private methods

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private async Task<DirectoryUser> FindAccountAsync(string email, CancellationToken cancellationToken) =>
        await _directory.FindByEmailAsync(email, cancellationToken)
            ?? throw new NotFoundException("No directory account has this email.");

    // The key is the Entra object id, so a deactivated user comes back as the same row rather than a second one.
    private async Task<User> AddOrReactivateAsync(User? user, DirectoryUser account, CancellationToken cancellationToken)
    {
        if (user is null)
        {
            user = new User
            {
                Id = account.Id,
                FirstName = account.FirstName,
                LastName = account.LastName,
                Email = account.Email,
            };
            _ctx.Add(user);
        }
        else
        {
            user.DateDeactivated = null;
            user.FirstName = account.FirstName;
            user.LastName = account.LastName;
            user.Email = account.Email;
        }

        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Re-checked rather than read from the provider's error number, which would bring SQL Server types into Service.
            // Any other store fault propagates, so GlobalExceptionHandler logs it as a 500.
            if (await IsTakenAsync(account, cancellationToken))
            {
                throw new ConflictException("Another request added a user with this email or account at the same time.");
            }

            throw;
        }

        return user;
    }

    private Task<User?> FindIncludingDeactivatedAsync(Guid id, CancellationToken cancellationToken) =>
        _ctx.Users
            .IgnoreQueryFilters(["Deactivation"])
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    // A fresh query, not the tracker, which still holds the entity whose save just failed.
    private Task<bool> IsTakenAsync(DirectoryUser account, CancellationToken cancellationToken) =>
        _ctx.Users
            .AsNoTracking()
            .AnyAsync(u => u.Email == account.Email || u.Id == account.Id, cancellationToken);

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _ctx.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The user was changed by another request. Read it again and retry.");
        }
    }

    #endregion
}
