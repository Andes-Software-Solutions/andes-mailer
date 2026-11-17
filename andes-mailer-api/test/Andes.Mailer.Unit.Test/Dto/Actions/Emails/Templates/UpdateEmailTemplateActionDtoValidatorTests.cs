using Andes.Mailer.Dto.Actions.Emails.Templates;

namespace Andes.Mailer.Unit.Test.Dto.Actions.Emails.Templates;

public sealed class UpdateEmailTemplateActionDtoValidatorTests
{
    private const string ValidVersion = "AAAAAAAAB9E=";

    private readonly UpdateEmailTemplateActionDtoValidator _validator = new();

    [Fact]
    public void Validate_ValidUpdate_Passes()
    {
        var result = _validator.Validate(new UpdateEmailTemplateActionDto("Welcome", "Hi", "<p>Hi</p>", [], ValidVersion));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("AAAA")]
    public void Validate_VersionNotAnEightByteRowVersion_Fails(string version)
    {
        var result = _validator.Validate(new UpdateEmailTemplateActionDto("Welcome", string.Empty, string.Empty, [], version));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateEmailTemplateActionDto.Version));
    }

    [Fact]
    public void Validate_BlankName_Fails()
    {
        var result = _validator.Validate(new UpdateEmailTemplateActionDto(string.Empty, string.Empty, string.Empty, [], ValidVersion));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateEmailTemplateActionDto.Name));
    }
}
