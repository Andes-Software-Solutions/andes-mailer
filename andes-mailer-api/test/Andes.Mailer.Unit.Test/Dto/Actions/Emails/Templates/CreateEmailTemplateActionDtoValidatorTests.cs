using Andes.Mailer.Common.Constants.Emails;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Emails;
using Andes.Mailer.Dto.Actions.Emails.Templates;

namespace Andes.Mailer.Unit.Test.Dto.Actions.Emails.Templates;

public sealed class CreateEmailTemplateActionDtoValidatorTests
{
    private readonly CreateEmailTemplateActionDtoValidator _validator = new();

    [Fact]
    public void Validate_ValidTemplate_Passes()
    {
        var result = _validator.Validate(new CreateEmailTemplateActionDto("Welcome", "Hi", "<p>Hi</p>", [new("amy@contoso.com", RecipientTypes.To)]));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankName_Fails(string name)
    {
        var result = _validator.Validate(new CreateEmailTemplateActionDto(name, string.Empty, string.Empty, []));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEmailTemplateActionDto.Name));
    }

    [Fact]
    public void Validate_NameTooLong_Fails()
    {
        var result = _validator.Validate(new CreateEmailTemplateActionDto(new string('a', EmailTemplateLimits.NameMaxLength + 1), string.Empty, string.Empty, []));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEmailTemplateActionDto.Name));
    }

    [Fact]
    public void Validate_AddressRepeated_Fails()
    {
        var result = _validator.Validate(new CreateEmailTemplateActionDto("Welcome", string.Empty, string.Empty, [new("amy@contoso.com", RecipientTypes.To), new("AMY@contoso.com", RecipientTypes.Cc)]));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEmailTemplateActionDto.Recipients));
    }

    [Fact]
    public void Validate_InvalidAddress_Fails()
    {
        var result = _validator.Validate(new CreateEmailTemplateActionDto("Welcome", string.Empty, string.Empty, [new EmailRecipientActionDto("Amy <amy@contoso.com>", RecipientTypes.To)]));

        Assert.False(result.IsValid);
    }
}
