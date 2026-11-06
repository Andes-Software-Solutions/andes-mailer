using Andes.Mailer.Common.Constants.Users;
using Andes.Mailer.Dto.Actions.Users;

namespace Andes.Mailer.Unit.Test.Dto.Actions.Users;

public sealed class UpdateUserActionDtoValidatorTests
{
    private static readonly string _version = Convert.ToBase64String(new byte[8]);

    private readonly UpdateUserActionDtoValidator _validator = new();

    [Theory]
    [InlineData("Ada", "Lovelace")]
    [InlineData("Ada", "")]
    public void Validate_Names_IsValid(string firstName, string lastName) =>
        Assert.True(_validator.Validate(new UpdateUserActionDto(firstName, lastName, _version)).IsValid);

    [Fact]
    public void Validate_EmptyFirstName_FailsOnFirstName()
    {
        var result = _validator.Validate(new UpdateUserActionDto(string.Empty, "Lovelace", _version));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateUserActionDto.FirstName));
    }

    [Fact]
    public void Validate_NullLastName_FailsOnLastName()
    {
        var result = _validator.Validate(new UpdateUserActionDto("Ada", null!, _version));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateUserActionDto.LastName));
    }

    [Fact]
    public void Validate_LastNameOverMaxLength_FailsOnLastName()
    {
        var result = _validator.Validate(new UpdateUserActionDto("Ada", new string('a', UserLimits.LastNameMaxLength + 1), _version));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateUserActionDto.LastName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64!")]
    public void Validate_InvalidVersion_FailsOnVersion(string version)
    {
        var result = _validator.Validate(new UpdateUserActionDto("Ada", "Lovelace", version));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateUserActionDto.Version));
    }
}
