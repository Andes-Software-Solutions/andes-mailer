using Andes.Mailer.Common.Constants.Emails;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Emails;

namespace Andes.Mailer.Unit.Test.Dto.Actions.Emails;

public sealed class CreateEmailActionDtoValidatorTests
{
    private readonly CreateEmailActionDtoValidator _validator = new();

    [Fact]
    public void Validate_DraftWithRecipients_Passes()
    {
        var result = _validator.Validate(new CreateEmailActionDto("Hi", "<p>Hi</p>", [new("amy@contoso.com", RecipientTypes.To), new("bob@contoso.com", RecipientTypes.Bcc)]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyDraft_Passes()
    {
        var result = _validator.Validate(new CreateEmailActionDto(string.Empty, string.Empty, []));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_AddressRepeatedUnderAnotherHeader_Fails()
    {
        var result = _validator.Validate(new CreateEmailActionDto("Hi", string.Empty, [new("amy@contoso.com", RecipientTypes.To), new(" AMY@contoso.com ", RecipientTypes.Cc)]));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEmailActionDto.Recipients));
    }

    [Fact]
    public void Validate_TooManyRecipients_Fails()
    {
        EmailRecipientActionDto[] recipients = [.. Enumerable.Range(0, EmailLimits.RecipientsMaxCount + 1).Select(i => new EmailRecipientActionDto($"user{i}@contoso.com", RecipientTypes.To))];

        var result = _validator.Validate(new CreateEmailActionDto("Hi", string.Empty, recipients));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEmailActionDto.Recipients));
    }

    [Theory]
    [InlineData("not-an-address")]
    [InlineData("")]
    [InlineData("amy smith@contoso.com")]
    [InlineData("amy@contoso.com\r\nBcc: attacker@evil.example")]
    [InlineData("Amy <amy@contoso.com>")]
    public void Validate_InvalidAddress_Fails(string address)
    {
        var result = _validator.Validate(new CreateEmailActionDto("Hi", string.Empty, [new(address, RecipientTypes.To)]));

        Assert.Contains(result.Errors, e => e.PropertyName == "Recipients[0].Address");
    }

    [Fact]
    public void Validate_UndefinedRecipientType_Fails()
    {
        var result = _validator.Validate(new CreateEmailActionDto("Hi", string.Empty, [new("amy@contoso.com", (RecipientTypes)99)]));

        Assert.Contains(result.Errors, e => e.PropertyName == "Recipients[0].Type");
    }

    [Fact]
    public void Validate_SubjectTooLong_Fails()
    {
        var result = _validator.Validate(new CreateEmailActionDto(new string('a', EmailLimits.SubjectMaxLength + 1), string.Empty, []));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreateEmailActionDto.Subject));
    }

    [Fact]
    public void Validate_MissingFields_Fails()
    {
        var result = _validator.Validate(new CreateEmailActionDto(null!, null!, null!));

        Assert.Equal(
            [nameof(CreateEmailActionDto.BodyHtml), nameof(CreateEmailActionDto.Recipients), nameof(CreateEmailActionDto.Subject)],
            result.Errors.Select(e => e.PropertyName).Distinct().Order());
    }
}
