using Andes.Mailer.Common.Constants.Teams;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Teams;

namespace Andes.Mailer.Unit.Test.Dto.Actions.Teams;

public sealed class UpdateTeamActionDtoValidatorTests
{
    private static readonly UpdateTeamActionDto _valid = new(
        "Sales",
        null,
        "sales@contoso.com",
        TeamColors.Mint,
        null,
        50,
        Convert.ToBase64String(new byte[8]));

    private readonly UpdateTeamActionDtoValidator _validator = new();

    [Fact]
    public void Validate_Settings_IsValid() => Assert.True(_validator.Validate(_valid).IsValid);

    [Theory]
    [InlineData(TeamLimits.ConfirmRecipientsAboveMin - 1)]
    [InlineData(TeamLimits.ConfirmRecipientsAboveMax + 1)]
    public void Validate_ThresholdOutOfRange_FailsOnConfirmRecipientsAbove(int threshold)
    {
        var result = _validator.Validate(_valid with { ConfirmRecipientsAbove = threshold });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateTeamActionDto.ConfirmRecipientsAbove));
    }

    [Fact]
    public void Validate_SignatureOverMaxLength_FailsOnDefaultSignature()
    {
        var result = _validator.Validate(_valid with { DefaultSignature = new string('a', TeamLimits.DefaultSignatureMaxLength + 1) });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateTeamActionDto.DefaultSignature));
    }

    [Fact]
    public void Validate_UnknownColor_FailsOnAvatarColor()
    {
        var result = _validator.Validate(_valid with { AvatarColor = (TeamColors)99 });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateTeamActionDto.AvatarColor));
    }

    [Fact]
    public void Validate_MalformedVersion_FailsOnVersion()
    {
        var result = _validator.Validate(_valid with { Version = "AAAA" });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateTeamActionDto.Version));
    }
}
