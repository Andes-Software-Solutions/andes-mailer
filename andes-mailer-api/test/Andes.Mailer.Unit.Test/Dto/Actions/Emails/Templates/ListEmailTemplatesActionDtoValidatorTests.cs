using Andes.Mailer.Dto.Actions.Emails.Templates;

namespace Andes.Mailer.Unit.Test.Dto.Actions.Emails.Templates;

public sealed class ListEmailTemplatesActionDtoValidatorTests
{
    private readonly ListEmailTemplatesActionDtoValidator _validator = new();

    [Fact]
    public void Validate_Defaults_Passes()
    {
        var result = _validator.Validate(new ListEmailTemplatesActionDto());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Validate_PageOutOfRange_Fails(int page, int pageSize)
    {
        var result = _validator.Validate(new ListEmailTemplatesActionDto(page, pageSize));

        Assert.False(result.IsValid);
    }
}
