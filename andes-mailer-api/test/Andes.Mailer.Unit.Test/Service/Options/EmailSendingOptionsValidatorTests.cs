using Andes.Mailer.Service.Options;

namespace Andes.Mailer.Unit.Test.Service.Options;

public sealed class EmailSendingOptionsValidatorTests
{
    private readonly EmailSendingOptionsValidator _validator = new();

    [Fact]
    public void Validate_Defaults_Pass()
    {
        var result = _validator.Validate(new EmailSendingOptions());

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("*/5 * * * *")]
    [InlineData("30 */2 * * * *")]
    public void Validate_CronWithOrWithoutSeconds_Passes(string schedule)
    {
        var result = _validator.Validate(new EmailSendingOptions { Schedule = schedule });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("every minute")]
    [InlineData("61 * * * *")]
    public void Validate_ScheduleNotCron_Fails(string schedule)
    {
        var result = _validator.Validate(new EmailSendingOptions { Schedule = schedule });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EmailSendingOptions.Schedule));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Validate_MaxAttemptsOutOfRange_Fails(int attempts)
    {
        var result = _validator.Validate(new EmailSendingOptions { MaxAttempts = attempts });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EmailSendingOptions.MaxAttempts));
    }

    [Fact]
    public void Validate_MaxDelayShorterThanBaseDelay_Fails()
    {
        var result = _validator.Validate(new EmailSendingOptions { RetryBaseDelay = TimeSpan.FromMinutes(10), RetryMaxDelay = TimeSpan.FromMinutes(5) });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EmailSendingOptions.RetryMaxDelay));
    }
}
