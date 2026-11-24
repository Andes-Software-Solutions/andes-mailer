using System.ComponentModel;

namespace Andes.Mailer.Common.Enums;

/// <summary>
/// Names the preset colours of a team's avatar. Values are stored as keys of <c>[Core.Ref].[TeamColor]</c> and are never renumbered.
/// </summary>
public enum TeamColors
{
    /// <summary>
    /// Light mint with dark green initials.
    /// </summary>
    [Description("Light mint")]
    Mint = 1,

    /// <summary>
    /// Seafoam with dark green initials.
    /// </summary>
    [Description("Seafoam")]
    Seafoam = 2,

    /// <summary>
    /// Pale mist with ocean-blue initials.
    /// </summary>
    [Description("Mist")]
    Mist = 3,

    /// <summary>
    /// Pale sky with ocean-blue initials.
    /// </summary>
    [Description("Sky")]
    Sky = 4,

    /// <summary>
    /// Andes green with white initials.
    /// </summary>
    [Description("Andes green")]
    Green = 5,

    /// <summary>
    /// Ocean blue with white initials.
    /// </summary>
    [Description("Ocean blue")]
    Ocean = 6,
}
