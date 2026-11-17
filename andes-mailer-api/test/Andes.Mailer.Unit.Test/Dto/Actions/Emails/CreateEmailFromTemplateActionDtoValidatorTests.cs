using Andes.Mailer.Dto.Actions.Emails;

namespace Andes.Mailer.Unit.Test.Dto.Actions.Emails;

public sealed class CreateEmailFromTemplateActionDtoValidatorTests
{
    private readonly CreateEmailFromTemplateActionDtoValidator _validator = new();

    [Fact]
    public void Validate_TemplateId_Passes()
    {
        var result = _validator.Validate(new CreateEmailFromTemplateActionDto(Guid.NewGuid()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyTemplateId_Fails()
    {
        var result = _validator.Validate(new CreateEmailFromTemplateActionDto(Guid.Empty));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEmailFromTemplateActionDto.TemplateId));
    }
}
