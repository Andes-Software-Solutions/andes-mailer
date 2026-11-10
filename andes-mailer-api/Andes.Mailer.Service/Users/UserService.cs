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
    /// Deactivates an active user and revokes their roles, so adding them again later grants no permissions until roles are re-granted.
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

        var email = action.Email.Trim().ToLowerInvariant();
        if (await _ctx.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new ConflictException("A user with this email already exists.");
        }

        var account = await _directory.FindByEmailAsync(email, cancellationToken)
            ?? throw new NotFoundException("No directory account has this email.");

        // The key is the Entra object id, so a deactivated user comes back as the same row rather than a second one.
        var user = await FindIncludingDeactivatedAsync(account.Id, cancellationToken);
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
        else if (user.DateDeactivated is null)
        {
            throw new ConflictException("This directory account is already a user under another email.");
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

        return UserMapper.MapToUserDto(user);
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

        // Roles are revoked, not just hidden, so re-adding the user later restores no permissions nobody re-granted.
        _ctx.RemoveRange(await _ctx.UserRoles.Where(ur => ur.UserId == id).ToListAsync(cancellationToken));

        // DeactivationInterceptor turns the user's delete into a DateDeactivated stamp.
        _ctx.Remove(user);
        await SaveAsync(cancellationToken);
    }

    #region Private methods

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
