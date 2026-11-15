using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Emails;

namespace Andes.Mailer.Unit.Test.Dto.Actions.Emails;

public sealed class UpdateEmailActionDtoValidatorTests
{
    private const string ValidVersion = "AAAAAAAAB9E=";

    private readonly UpdateEmailActionDtoValidator _validator = new();

    [Fact]
    public void Validate_ValidUpdate_Passes()
    {
        var result = _validator.Validate(new UpdateEmailActionDto("Hi", "<p>Hi</p>", [new("amy@contoso.com", RecipientTypes.To)], ValidVersion));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("AAAA")]
    public void Validate_VersionNotAnEightByteRowVersion_Fails(string version)
    {
        var result = _validator.Validate(new UpdateEmailActionDto("Hi", string.Empty, [], version));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateEmailActionDto.Version));
    }

    [Fact]
    public void Validate_AddressRepeated_Fails()
    {
        var result = _validator.Validate(new UpdateEmailActionDto("Hi", string.Empty, [new("amy@contoso.com", RecipientTypes.To), new("amy@contoso.com", RecipientTypes.To)], ValidVersion));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateEmailActionDto.Recipients));
    }
}
