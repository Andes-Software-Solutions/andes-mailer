using Andes.Mailer.Dto.Actions.ContactGroups;

namespace Andes.Mailer.Unit.Test.Dto.Actions.ContactGroups;

public sealed class UpdateContactGroupActionDtoValidatorTests
{
    private static readonly ContactGroupMemberActionDto[] _members = [new("a@fabrikam.com")];

    private readonly UpdateContactGroupActionDtoValidator _validator = new();

    [Fact]
    public void Validate_Group_IsValid() =>
        Assert.True(_validator.Validate(new UpdateContactGroupActionDto("Clients", null, _members, Convert.ToBase64String(new byte[8]))).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("not base64")]
    [InlineData("AAAA")]
    public void Validate_InvalidVersion_FailsOnVersion(string version)
    {
        var result = _validator.Validate(new UpdateContactGroupActionDto("Clients", null, _members, version));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateContactGroupActionDto.Version));
    }

    [Fact]
    public void Validate_NoMembers_FailsOnMembers()
    {
        var result = _validator.Validate(new UpdateContactGroupActionDto("Clients", null, [], Convert.ToBase64String(new byte[8])));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateContactGroupActionDto.Members));
    }
}
