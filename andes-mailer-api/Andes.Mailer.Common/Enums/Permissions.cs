using System.ComponentModel;

namespace Andes.Mailer.Common.Enums;

/// <summary>
/// Names the permissions a role grants. Values are stored as keys of <c>[Core.Ref].[Permission]</c> and are never renumbered.
/// </summary>
public enum Permissions
{
    /// <summary>
    /// Adds a user from the directory.
    /// </summary>
    [Description("Adds a user from the directory")]
    CreateUser = 1,

    /// <summary>
    /// Changes a user's details.
    /// </summary>
    [Description("Changes a user's details")]
    ModifyUser = 2,

    /// <summary>
    /// Deactivates a user.
    /// </summary>
    [Description("Deactivates a user")]
    DeactivateUser = 3,

    /// <summary>
    /// Adds a user to a team.
    /// </summary>
    [Description("Adds a user to a team")]
    CreateTeamUser = 4,

    /// <summary>
    /// Changes the details of a team's user.
    /// </summary>
    [Description("Changes the details of a team's user")]
    ModifyTeamUser = 5,

    /// <summary>
    /// Deactivates a team's user.
    /// </summary>
    [Description("Deactivates a team's user")]
    DeactivateTeamUser = 6,

    /// <summary>
    /// Uploads documents to, and removes them from, an email.
    /// </summary>
    [Description("Uploads documents to, and removes them from, an email")]
    CanAttachDocuments = 7,

    /// <summary>
    /// Creates a team.
    /// </summary>
    [Description("Creates a team")]
    CreateTeam = 8,

    /// <summary>
    /// Changes a team's details.
    /// </summary>
    [Description("Changes a team's details")]
    ModifyTeam = 9,

    /// <summary>
    /// Deactivates a team.
    /// </summary>
    [Description("Deactivates a team")]
    DeactivateTeam = 10,

    /// <summary>
    /// Reads a team and its members.
    /// </summary>
    [Description("Reads a team and its members")]
    ViewTeam = 11,

    /// <summary>
    /// Reads, creates, changes, and deletes a team's emails.
    /// </summary>
    [Description("Reads, creates, changes, and deletes a team's emails")]
    ComposeTeamEmails = 12,

    /// <summary>
    /// Creates, changes, and deactivates a team's email templates.
    /// </summary>
    [Description("Creates, changes, and deactivates a team's email templates")]
    ManageTeamTemplates = 13,

    /// <summary>
    /// Duplicates a team's emails and email templates.
    /// </summary>
    [Description("Duplicates a team's emails and email templates")]
    DuplicateTeamEmails = 14,

    /// <summary>
    /// Creates, changes, and deactivates a team's contact groups.
    /// </summary>
    [Description("Creates, changes, and deactivates a team's contact groups")]
    ManageTeamContactGroups = 15,
}
