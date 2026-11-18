using System.Linq.Expressions;

using Andes.Mailer.Dto.Users;
using Andes.Mailer.Entity.Users;
using Andes.Mailer.Service.Users.Models;

namespace Andes.Mailer.Service.Users;

/// <summary>
/// Maps users and directory accounts to their response DTOs.
/// </summary>
public static class UserMapper
{
    /// <summary>
    /// Gets the projection that maps a user to a <see cref="UserDto"/> inside a query.
    /// </summary>
    /// <remarks>
    /// It calls <see cref="User.ComposeFullName"/> rather than reading <see cref="User.FullName"/>, which is not mapped and so cannot be read from a query.
    /// </remarks>
    public static Expression<Func<User, UserDto>> MapToUserDtoExpression { get; } = user => new UserDto(
        user.Id,
        user.FirstName,
        user.LastName,
        User.ComposeFullName(user.FirstName, user.LastName),
        user.Email,
        user.DateCreated,
        user.DateModified,
        Convert.ToBase64String(user.Version));

    #region Public static methods

    /// <summary>
    /// Maps a loaded user to a <see cref="UserDto"/>.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <returns>The DTO.</returns>
    public static UserDto MapToUserDto(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.FullName,
            user.Email,
            user.DateCreated,
            user.DateModified,
            Convert.ToBase64String(user.Version));
    }

    /// <summary>
    /// Maps a directory account to a <see cref="DirectoryUserDto"/>.
    /// </summary>
    /// <param name="account">The account.</param>
    /// <returns>The DTO.</returns>
    public static DirectoryUserDto MapToDirectoryUserDto(DirectoryUser account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return new DirectoryUserDto(
            account.Id,
            account.FirstName,
            account.LastName,
            User.ComposeFullName(account.FirstName, account.LastName),
            account.Email);
    }

    #endregion
}
