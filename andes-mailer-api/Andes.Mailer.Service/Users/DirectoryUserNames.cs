using Andes.Mailer.Entity.Users;
using Andes.Mailer.Service.Users.Models;

namespace Andes.Mailer.Service.Users;

// Email recipients and contact group members store the same directory display name, cut to the same column size.
internal static class DirectoryUserNames
{
    #region Public static methods

    public static string DisplayNameOf(DirectoryUser account, int maxLength)
    {
        var name = User.ComposeFullName(account.FirstName, account.LastName);

        return name.Length <= maxLength ? name : name[..maxLength];
    }

    #endregion
}
