namespace Andes.Mailer.Common.Constants.Base;

/// <summary>
/// Holds the column limits shared by every entity that users edit.
/// </summary>
public static class BaseModifiedEntityLimits
{
    /// <summary>
    /// The length in bytes of a row version, which SQL Server stores as an 8-byte <c>rowversion</c>.
    /// </summary>
    public const int VersionLength = 8;
}
