using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Teams;

/// <summary>
/// Selects one page of a team's active members.
/// </summary>
/// <param name="Page">The one-based page number. The default is 1.</param>
/// <param name="PageSize">The maximum number of members on a page. The default is 25.</param>
public sealed record ListTeamMembersActionDto(int Page = 1, int PageSize = 25);

/// <summary>
/// Validates a <see cref="ListTeamMembersActionDto"/>.
/// </summary>
public sealed class ListTeamMembersActionDtoValidator : AbstractValidator<ListTeamMembersActionDto>
{
    private const int MaxPageSize = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListTeamMembersActionDtoValidator"/> class.
    /// </summary>
    public ListTeamMembersActionDtoValidator()
    {
        // Bounded so (Page - 1) * PageSize cannot overflow int.
        RuleFor(a => a.Page).InclusiveBetween(1, int.MaxValue / MaxPageSize);
        RuleFor(a => a.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}
