using Andes.Mailer.Common.Constants.ContactGroups;

using FluentValidation;

namespace Andes.Mailer.Dto.Actions.ContactGroups;

/// <summary>
/// Selects one page of a team's active contact groups.
/// </summary>
/// <param name="Search">Text the group's name must contain, ignoring case, or <see langword="null"/> for every group.</param>
/// <param name="Page">The one-based page number. The default is 1.</param>
/// <param name="PageSize">The maximum number of groups on a page. The default is 25.</param>
public sealed record ListContactGroupsActionDto(string? Search = null, int Page = 1, int PageSize = 25);

/// <summary>
/// Validates a <see cref="ListContactGroupsActionDto"/>.
/// </summary>
public sealed class ListContactGroupsActionDtoValidator : AbstractValidator<ListContactGroupsActionDto>
{
    private const int MaxPageSize = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListContactGroupsActionDtoValidator"/> class.
    /// </summary>
    public ListContactGroupsActionDtoValidator()
    {
        RuleFor(a => a.Search).MaximumLength(ContactGroupLimits.SearchMaxLength);

        // Bounded so (Page - 1) * PageSize cannot overflow int.
        RuleFor(a => a.Page).InclusiveBetween(1, int.MaxValue / MaxPageSize);
        RuleFor(a => a.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}
