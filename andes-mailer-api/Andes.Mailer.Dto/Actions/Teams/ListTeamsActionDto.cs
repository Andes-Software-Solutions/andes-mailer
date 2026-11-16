using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Teams;

/// <summary>
/// Selects one page of the teams the signed-in user can see.
/// </summary>
/// <param name="Page">The one-based page number. The default is 1.</param>
/// <param name="PageSize">The maximum number of teams on a page. The default is 25.</param>
public sealed record ListTeamsActionDto(int Page = 1, int PageSize = 25);

/// <summary>
/// Validates a <see cref="ListTeamsActionDto"/>.
/// </summary>
public sealed class ListTeamsActionDtoValidator : AbstractValidator<ListTeamsActionDto>
{
    private const int MaxPageSize = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListTeamsActionDtoValidator"/> class.
    /// </summary>
    public ListTeamsActionDtoValidator()
    {
        // Bounded so (Page - 1) * PageSize cannot overflow int.
        RuleFor(a => a.Page).InclusiveBetween(1, int.MaxValue / MaxPageSize);
        RuleFor(a => a.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}
