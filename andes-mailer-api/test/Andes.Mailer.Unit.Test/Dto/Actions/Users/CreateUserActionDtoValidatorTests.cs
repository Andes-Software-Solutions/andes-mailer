using Andes.Mailer.Common.Constants.Users;
using Andes.Mailer.Dto.Actions.Users;

namespace Andes.Mailer.Unit.Test.Dto.Actions.Users;

public sealed class CreateUserActionDtoValidatorTests
{
    private readonly CreateUserActionDtoValidator _validator = new();

    [Fact]
    public void Validate_EmailAddress_IsValid() =>
        Assert.True(_validator.Validate(new CreateUserActionDto("ada@contoso.com")).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Validate_InvalidEmail_FailsOnEmail(string email)
    {
        var result = _validator.Validate(new CreateUserActionDto(email));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUserActionDto.Email));
    }

    [Fact]
    public void Validate_EmailOverMaxLength_FailsOnEmail()
    {
        var email = new string('a', UserLimits.EmailMaxLength) + "@contoso.com";

        var result = _validator.Validate(new CreateUserActionDto(email));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateUserActionDto.Email));
    }
}
