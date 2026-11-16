using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Emails;

/// <summary>
/// Selects one page of a team's emails.
/// </summary>
/// <param name="Page">The one-based page number. The default is 1.</param>
/// <param name="PageSize">The maximum number of emails on a page. The default is 25.</param>
public sealed record ListEmailsActionDto(int Page = 1, int PageSize = 25);

/// <summary>
/// Validates a <see cref="ListEmailsActionDto"/>.
/// </summary>
public sealed class ListEmailsActionDtoValidator : AbstractValidator<ListEmailsActionDto>
{
    private const int MaxPageSize = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListEmailsActionDtoValidator"/> class.
    /// </summary>
    public ListEmailsActionDtoValidator()
    {
        // Bounded so (Page - 1) * PageSize cannot overflow int.
        RuleFor(a => a.Page).InclusiveBetween(1, int.MaxValue / MaxPageSize);
        RuleFor(a => a.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}
