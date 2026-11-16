namespace Andes.Mailer.Common.Constants.Teams;

/// <summary>
/// Holds the column limits of a team, shared by the store mapping and the request validators.
/// </summary>
public static class TeamLimits
{
    /// <summary>
    /// The maximum length of a name.
    /// </summary>
    public const int NameMaxLength = 100;

    /// <summary>
    /// The maximum length of a description.
    /// </summary>
    public const int DescriptionMaxLength = 500;

    /// <summary>
    /// The maximum length of the service account's email address.
    /// </summary>
    public const int ServiceAccountEmailMaxLength = 256;
}
