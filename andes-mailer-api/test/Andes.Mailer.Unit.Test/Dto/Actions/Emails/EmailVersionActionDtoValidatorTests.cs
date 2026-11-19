using Andes.Mailer.Dto.Actions.Emails;

namespace Andes.Mailer.Unit.Test.Dto.Actions.Emails;

public sealed class EmailVersionActionDtoValidatorTests
{
    private readonly EmailVersionActionDtoValidator _validator = new();

    [Fact]
    public void Validate_RowVersion_Passes()
    {
        var result = _validator.Validate(new EmailVersionActionDto("AAAAAAAAB9E="));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("AAAA")]
    public void Validate_VersionNotAnEightByteRowVersion_Fails(string version)
    {
        var result = _validator.Validate(new EmailVersionActionDto(version));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EmailVersionActionDto.Version));
    }
}
