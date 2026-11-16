using Andes.Mailer.Common.Constants.Teams;
using Andes.Mailer.Dto.Actions.Teams;

namespace Andes.Mailer.Unit.Test.Dto.Actions.Teams;

public sealed class CreateTeamActionDtoValidatorTests
{
    private readonly CreateTeamActionDtoValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("Invoices to customers")]
    public void Validate_Team_IsValid(string? description) =>
        Assert.True(_validator.Validate(new CreateTeamActionDto("Sales", description, "sales@contoso.com")).IsValid);

    [Fact]
    public void Validate_EmptyName_FailsOnName()
    {
        var result = _validator.Validate(new CreateTeamActionDto(string.Empty, null, "sales@contoso.com"));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateTeamActionDto.Name));
    }

    [Fact]
    public void Validate_NameOverMaxLength_FailsOnName()
    {
        var result = _validator.Validate(new CreateTeamActionDto(new string('a', TeamLimits.NameMaxLength + 1), null, "sales@contoso.com"));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateTeamActionDto.Name));
    }

    [Fact]
    public void Validate_DescriptionOverMaxLength_FailsOnDescription()
    {
        var result = _validator.Validate(new CreateTeamActionDto("Sales", new string('a', TeamLimits.DescriptionMaxLength + 1), "sales@contoso.com"));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateTeamActionDto.Description));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-address")]
    public void Validate_InvalidServiceAccountEmail_FailsOnServiceAccountEmail(string email)
    {
        var result = _validator.Validate(new CreateTeamActionDto("Sales", null, email));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateTeamActionDto.ServiceAccountEmail));
    }
}
