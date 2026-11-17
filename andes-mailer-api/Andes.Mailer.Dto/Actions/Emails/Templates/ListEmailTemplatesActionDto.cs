using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Emails.Templates;

/// <summary>
/// Selects one page of a team's email templates.
/// </summary>
/// <param name="Page">The one-based page number. The default is 1.</param>
/// <param name="PageSize">The maximum number of templates on a page. The default is 25.</param>
public sealed record ListEmailTemplatesActionDto(int Page = 1, int PageSize = 25);

/// <summary>
/// Validates a <see cref="ListEmailTemplatesActionDto"/>.
/// </summary>
public sealed class ListEmailTemplatesActionDtoValidator : AbstractValidator<ListEmailTemplatesActionDto>
{
    private const int MaxPageSize = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListEmailTemplatesActionDtoValidator"/> class.
    /// </summary>
    public ListEmailTemplatesActionDtoValidator()
    {
        // Bounded so (Page - 1) * PageSize cannot overflow int.
        RuleFor(a => a.Page).InclusiveBetween(1, int.MaxValue / MaxPageSize);
        RuleFor(a => a.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}
