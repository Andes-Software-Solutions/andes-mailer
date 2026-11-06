using Andes.Mailer.Dto.Actions.Users;

namespace Andes.Mailer.Unit.Test.Dto.Actions.Users;

public sealed class ListUsersActionDtoValidatorTests
{
    private const int MaxPage = int.MaxValue / 100;

    private readonly ListUsersActionDtoValidator _validator = new();

    [Theory]
    [InlineData(1, 1)]
    [InlineData(MaxPage, 100)]
    public void Validate_PageWithinBounds_IsValid(int page, int pageSize) =>
        Assert.True(_validator.Validate(new ListUsersActionDto(page, pageSize)).IsValid);

    [Theory]
    [InlineData(0)]
    [InlineData(MaxPage + 1)]
    public void Validate_PageOutOfBounds_FailsOnPage(int page)
    {
        var result = _validator.Validate(new ListUsersActionDto(page, 100));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ListUsersActionDto.Page));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_PageSizeOutOfBounds_FailsOnPageSize(int pageSize)
    {
        var result = _validator.Validate(new ListUsersActionDto(1, pageSize));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ListUsersActionDto.PageSize));
    }
}
