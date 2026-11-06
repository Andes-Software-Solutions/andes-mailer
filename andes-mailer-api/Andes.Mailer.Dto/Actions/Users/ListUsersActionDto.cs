using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Users;

/// <summary>
/// Selects one page of active users.
/// </summary>
/// <param name="Page">The one-based page number. The default is 1.</param>
/// <param name="PageSize">The maximum number of users on a page. The default is 25.</param>
public sealed record ListUsersActionDto(int Page = 1, int PageSize = 25);

/// <summary>
/// Validates a <see cref="ListUsersActionDto"/>.
/// </summary>
public sealed class ListUsersActionDtoValidator : AbstractValidator<ListUsersActionDto>
{
    private const int MaxPageSize = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListUsersActionDtoValidator"/> class.
    /// </summary>
    public ListUsersActionDtoValidator()
    {
        // Bounded so (Page - 1) * PageSize cannot overflow int.
        RuleFor(a => a.Page).InclusiveBetween(1, int.MaxValue / MaxPageSize);
        RuleFor(a => a.PageSize).InclusiveBetween(1, MaxPageSize);
    }
}
