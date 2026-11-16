namespace Andes.Mailer.Dto.Teams;

/// <summary>
/// Describes a team.
/// </summary>
/// <param name="Id">The team's id.</param>
/// <param name="Name">The name.</param>
/// <param name="Description">The description, or <see langword="null"/> if there is none.</param>
/// <param name="ServiceAccountEmail">The email address of the mailbox the team sends from, in lowercase.</param>
/// <param name="DateCreated">The moment the team was created.</param>
/// <param name="DateModified">The moment the team last changed, or <see langword="null"/> if never.</param>
/// <param name="Version">The row version as base64; send it back to update the team.</param>
public sealed record TeamDto(
    Guid Id,
    string Name,
    string? Description,
    string ServiceAccountEmail,
    DateTimeOffset DateCreated,
    DateTimeOffset? DateModified,
    string Version);
