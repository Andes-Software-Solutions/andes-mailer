using System.Text.RegularExpressions;

using Andes.Mailer.Common.Constants.Users;

using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Users;

/// <summary>
/// Searches the Entra directory for people by name or email address, as someone types.
/// </summary>
/// <param name="Query">The text typed so far: a name, or the start of an email address.</param>
/// <param name="Limit">The maximum number of people to return. The default is 10.</param>
public sealed record SearchDirectoryActionDto(string Query = "", int Limit = DirectorySearchLimits.DefaultLimit);

/// <summary>
/// Validates a <see cref="SearchDirectoryActionDto"/>.
/// </summary>
public sealed partial class SearchDirectoryActionDtoValidator : AbstractValidator<SearchDirectoryActionDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SearchDirectoryActionDtoValidator"/> class.
    /// </summary>
    public SearchDirectoryActionDtoValidator()
    {
        RuleFor(a => a.Query)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(q => q.Trim().Length >= DirectorySearchLimits.QueryMinLength)
            .WithMessage($"'Query' must hold at least {DirectorySearchLimits.QueryMinLength} characters.")
            .MaximumLength(DirectorySearchLimits.QueryMaxLength)

            // Graph's $search fails on an ampersand, and no other symbol outside these appears in a name or an address.
            .Matches(SearchableText())
            .WithMessage("'Query' may hold only letters, digits, spaces, and . ' @ _ + -");
        RuleFor(a => a.Limit).InclusiveBetween(1, DirectorySearchLimits.MaxLimit);
    }

    #region Private methods

    [GeneratedRegex(@"^[\p{L}\p{M}\p{N} .'@_+-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SearchableText();

    #endregion
}
