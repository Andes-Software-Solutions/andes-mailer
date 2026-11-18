using Andes.Mailer.Common.Constants.ContactGroups;
using Andes.Mailer.Dto.Actions.ContactGroups;

namespace Andes.Mailer.Unit.Test.Dto.Actions.ContactGroups;

public sealed class CreateContactGroupActionDtoValidatorTests
{
    private readonly CreateContactGroupActionDtoValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("Customers we invoice monthly")]
    public void Validate_Group_IsValid(string? description) =>
        Assert.True(_validator.Validate(new CreateContactGroupActionDto("Clients", description, Members("a@fabrikam.com", "b@contoso.com"))).IsValid);

    [Fact]
    public void Validate_EmptyName_FailsOnName()
    {
        var result = _validator.Validate(new CreateContactGroupActionDto(string.Empty, null, Members("a@fabrikam.com")));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateContactGroupActionDto.Name));
    }

    [Fact]
    public void Validate_NameOverMaxLength_FailsOnName()
    {
        var result = _validator.Validate(new CreateContactGroupActionDto(new string('a', ContactGroupLimits.NameMaxLength + 1), null, Members("a@fabrikam.com")));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateContactGroupActionDto.Name));
    }

    [Fact]
    public void Validate_DescriptionOverMaxLength_FailsOnDescription()
    {
        var result = _validator.Validate(new CreateContactGroupActionDto("Clients", new string('a', ContactGroupLimits.DescriptionMaxLength + 1), Members("a@fabrikam.com")));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateContactGroupActionDto.Description));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(ContactGroupLimits.MembersMaxCount + 1)]
    public void Validate_MemberCountOutOfRange_FailsOnMembers(int count)
    {
        var members = Enumerable.Range(0, count).Select(i => $"person{i}@fabrikam.com").ToArray();

        var result = _validator.Validate(new CreateContactGroupActionDto("Clients", null, Members(members)));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateContactGroupActionDto.Members));
    }

    [Fact]
    public void Validate_MaxMemberCount_IsValid()
    {
        var members = Enumerable.Range(0, ContactGroupLimits.MembersMaxCount).Select(i => $"person{i}@fabrikam.com").ToArray();

        Assert.True(_validator.Validate(new CreateContactGroupActionDto("Clients", null, Members(members))).IsValid);
    }

    [Fact]
    public void Validate_AddressRepeatedInAnotherCase_FailsOnMembers()
    {
        var result = _validator.Validate(new CreateContactGroupActionDto("Clients", null, Members("a@fabrikam.com", " A@Fabrikam.com ")));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateContactGroupActionDto.Members));
    }

    [Fact]
    public void Validate_NullMembers_FailsOnMembers()
    {
        var result = _validator.Validate(new CreateContactGroupActionDto("Clients", null, null!));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateContactGroupActionDto.Members));
    }

    [Theory]
    [InlineData("not-an-address")]
    [InlineData("Ana <ana@fabrikam.com>")]
    [InlineData("ana @fabrikam.com")]
    public void Validate_InvalidAddress_FailsOnMemberAddress(string address)
    {
        var result = _validator.Validate(new CreateContactGroupActionDto("Clients", null, Members(address)));

        Assert.Contains(result.Errors, e => e.PropertyName == "Members[0].Address");
    }

    #region Private methods

    private static ContactGroupMemberActionDto[] Members(params string[] addresses) => [.. addresses.Select(a => new ContactGroupMemberActionDto(a))];

    #endregion
}
