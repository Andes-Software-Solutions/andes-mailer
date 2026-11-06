namespace Andes.Mailer.Common.Constants.Users;

/// <summary>
/// Holds the column limits of a user, shared by the store mapping and the request validators.
/// </summary>
public static class UserLimits
{
    /// <summary>
    /// The maximum length of a first name.
    /// </summary>
    public const int FirstNameMaxLength = 100;

    /// <summary>
    /// The maximum length of a last name.
    /// </summary>
    public const int LastNameMaxLength = 100;

    /// <summary>
    /// The maximum length of an email address.
    /// </summary>
    public const int EmailMaxLength = 256;
}
