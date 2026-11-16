using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Teams;

namespace Andes.Mailer.Unit.Test.Dto.Actions.Teams;

public sealed class TeamMemberActionDtoValidatorTests
{
    private static readonly string _version = Convert.ToBase64String(new byte[8]);

    private readonly AddTeamMemberActionDtoValidator _addValidator = new();
    private readonly UpdateTeamMemberActionDtoValidator _updateValidator = new();

    [Theory]
    [InlineData(Roles.TeamAdministrator)]
    [InlineData(Roles.TeamMember)]
    public void Validate_AddWithTeamRole_IsValid(Roles role) =>
        Assert.True(_addValidator.Validate(new AddTeamMemberActionDto("ada@contoso.com", role)).IsValid);

    [Theory]
    [InlineData(Roles.Administrator)]
    [InlineData((Roles)99)]
    public void Validate_AddWithNonTeamRole_FailsOnRole(Roles role)
    {
        var result = _addValidator.Validate(new AddTeamMemberActionDto("ada@contoso.com", role));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddTeamMemberActionDto.Role));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-address")]
    public void Validate_AddWithInvalidEmail_FailsOnEmail(string email)
    {
        var result = _addValidator.Validate(new AddTeamMemberActionDto(email, Roles.TeamMember));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AddTeamMemberActionDto.Email));
    }

    [Fact]
    public void Validate_UpdateWithTeamRole_IsValid() =>
        Assert.True(_updateValidator.Validate(new UpdateTeamMemberActionDto(Roles.TeamAdministrator, _version)).IsValid);

    [Fact]
    public void Validate_UpdateToAdministrator_FailsOnRole()
    {
        var result = _updateValidator.Validate(new UpdateTeamMemberActionDto(Roles.Administrator, _version));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateTeamMemberActionDto.Role));
    }

    [Theory]
    [InlineData("")]
    [InlineData("AA==")]
    public void Validate_UpdateWithInvalidVersion_FailsOnVersion(string version)
    {
        var result = _updateValidator.Validate(new UpdateTeamMemberActionDto(Roles.TeamMember, version));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateTeamMemberActionDto.Version));
    }
}
