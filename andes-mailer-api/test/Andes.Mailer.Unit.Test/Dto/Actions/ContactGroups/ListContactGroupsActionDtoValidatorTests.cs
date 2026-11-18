using Andes.Mailer.Common.Constants.ContactGroups;
using Andes.Mailer.Dto.Actions.ContactGroups;

namespace Andes.Mailer.Unit.Test.Dto.Actions.ContactGroups;

public sealed class ListContactGroupsActionDtoValidatorTests
{
    private readonly ListContactGroupsActionDtoValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("clients")]
    public void Validate_Page_IsValid(string? search) =>
        Assert.True(_validator.Validate(new ListContactGroupsActionDto(search)).IsValid);

    [Fact]
    public void Validate_SearchOverMaxLength_FailsOnSearch()
    {
        var result = _validator.Validate(new ListContactGroupsActionDto(new string('a', ContactGroupLimits.SearchMaxLength + 1)));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ListContactGroupsActionDto.Search));
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Validate_PageOutOfRange_IsInvalid(int page, int pageSize) =>
        Assert.False(_validator.Validate(new ListContactGroupsActionDto(null, page, pageSize)).IsValid);
}
