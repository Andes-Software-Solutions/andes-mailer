using Andes.Mailer.Common.Constants.Emails;
using Andes.Mailer.Dto.Actions.Emails;

using Microsoft.Extensions.Time.Testing;

namespace Andes.Mailer.Unit.Test.Dto.Actions.Emails;

public sealed class SendEmailActionDtoValidatorTests
{
    private const string ValidVersion = "AAAAAAAAB9E=";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero));
    private readonly SendEmailActionDtoValidator _validator;

    public SendEmailActionDtoValidatorTests() => _validator = new(_time);

    [Fact]
    public void Validate_SendNow_Passes()
    {
        var result = _validator.Validate(new SendEmailActionDto(ValidVersion, null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ScheduledInTheFuture_Passes()
    {
        var result = _validator.Validate(new SendEmailActionDto(ValidVersion, _time.GetUtcNow().AddMinutes(5)));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-60)]
    public void Validate_ScheduledNowOrInThePast_Fails(int offsetSeconds)
    {
        var result = _validator.Validate(new SendEmailActionDto(ValidVersion, _time.GetUtcNow().AddSeconds(offsetSeconds)));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SendEmailActionDto.ScheduledFor));
    }

    [Fact]
    public void Validate_ScheduledBeyondTheLimit_Fails()
    {
        var result = _validator.Validate(new SendEmailActionDto(ValidVersion, _time.GetUtcNow().AddDays(EmailSendLimits.MaxScheduleAheadDays).AddMinutes(1)));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SendEmailActionDto.ScheduledFor));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("AAAA")]
    public void Validate_VersionNotAnEightByteRowVersion_Fails(string version)
    {
        var result = _validator.Validate(new SendEmailActionDto(version, null));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SendEmailActionDto.Version));
    }
}
