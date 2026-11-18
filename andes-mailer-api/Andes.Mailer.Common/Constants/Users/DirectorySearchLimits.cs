namespace Andes.Mailer.Common.Constants.Users;

/// <summary>
/// Holds the limits of a directory search request.
/// </summary>
public static class DirectorySearchLimits
{
    /// <summary>
    /// The minimum length of the text searched for, after trimming.
    /// </summary>
    public const int QueryMinLength = 2;

    /// <summary>
    /// The maximum length of the text searched for.
    /// </summary>
    public const int QueryMaxLength = 100;

    /// <summary>
    /// The default number of accounts a search returns.
    /// </summary>
    public const int DefaultLimit = 10;

    /// <summary>
    /// The maximum number of accounts a search returns.
    /// </summary>
    public const int MaxLimit = 25;
}
