using Andes.Mailer.Common.Constants.Emails;
using Andes.Mailer.Dto.Actions.Emails.Templates;

namespace Andes.Mailer.Unit.Test.Dto.Actions.Emails.Templates;

public sealed class DuplicateEmailTemplateActionDtoValidatorTests
{
    private readonly DuplicateEmailTemplateActionDtoValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("Welcome back")]
    public void Validate_NoNameOrAName_Passes(string? name)
    {
        var result = _validator.Validate(new DuplicateEmailTemplateActionDto(name));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankName_Fails(string name)
    {
        var result = _validator.Validate(new DuplicateEmailTemplateActionDto(name));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(DuplicateEmailTemplateActionDto.Name));
    }

    [Fact]
    public void Validate_NameOverTheLimit_Fails()
    {
        var result = _validator.Validate(new DuplicateEmailTemplateActionDto(new string('a', EmailTemplateLimits.NameMaxLength + 1)));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(DuplicateEmailTemplateActionDto.Name));
    }
}
