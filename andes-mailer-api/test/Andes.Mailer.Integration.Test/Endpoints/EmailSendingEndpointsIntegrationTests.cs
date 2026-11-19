using System.Net;
using System.Net.Http.Json;

using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Emails;
using Andes.Mailer.Dto.Emails;
using Andes.Mailer.Integration.Test.TestInfrastructure;
using Andes.Mailer.Service.Emails.Sending;
using Andes.Mailer.Service.Emails.Sending.Exceptions;
using Andes.Mailer.Service.Emails.Sending.Models;
using Andes.Mailer.Service.Options;
using Andes.Mailer.Service.Users;
using Andes.Mailer.Service.Users.Models;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Andes.Mailer.Integration.Test.Endpoints;

// The only class whose hosts run the dispatcher; its tests run one after another, so no two dispatchers share the database at once.
public sealed class EmailSendingEndpointsIntegrationTests(SqlServerFixture fixture, AzuriteFixture azurite)
{
    private static readonly TimeSpan _settleTimeout = TimeSpan.FromSeconds(30);

    private readonly SqlServerFixture _fixture = fixture;
    private readonly AzuriteFixture _azurite = azurite;
    private readonly IDirectoryUserService _directory = Substitute.For<IDirectoryUserService>();
    private readonly IMailboxClient _mailbox = Substitute.For<IMailboxClient>();

    [Fact]
    public async Task SendEmail_TeamMember_ReturnsForbidden()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team);
        await using var factory = CreateFactory();
        var draft = await CreateDraftAsync(factory, team, caller, "ada@contoso.com");
        using var client = TestCallers.CreateClient(factory, caller);

        using var response = await client.PostAsJsonAsync($"{EmailPath(team, draft.Id)}/send", new SendEmailActionDto(draft.Version, null), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SendEmail_AdministratorOfAnotherTeam_ReturnsNotFound()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var author = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var otherTeam = await TestCallers.SeedTeamAsync(_fixture);
        var outsider = await TestCallers.SeedMemberAsync(_fixture, otherTeam, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        var draft = await CreateDraftAsync(factory, team, author, "ada@contoso.com");
        using var client = TestCallers.CreateClient(factory, outsider);

        using var response = await client.PostAsJsonAsync($"{EmailPath(team, draft.Id)}/send", new SendEmailActionDto(draft.Version, null), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SendEmail_ScheduledInThePast_ReturnsValidationProblem()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        var draft = await CreateDraftAsync(factory, team, caller, "ada@contoso.com");
        using var client = TestCallers.CreateClient(factory, caller);

        using var response = await client.PostAsJsonAsync(
            $"{EmailPath(team, draft.Id)}/send",
            new SendEmailActionDto(draft.Version, DateTimeOffset.UtcNow.AddMinutes(-5)),
            ApiJson.Options,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SendEmail_TeamAdministratorNow_DeliversToEveryRecipientAndCompletesTheEmail()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        var draft = await CreateDraftAsync(factory, team, caller, "ada@contoso.com", "bob@contoso.com");
        using var client = TestCallers.CreateClient(factory, caller);

        using var response = await client.PostAsJsonAsync($"{EmailPath(team, draft.Id)}/send", new SendEmailActionDto(draft.Version, null), ApiJson.Options, TestContext.Current.CancellationToken);
        var accepted = await response.Content.ReadFromJsonAsync<EmailDto>(ApiJson.Options, TestContext.Current.CancellationToken);
        var deliveries = await PollUntilSettledAsync(client, team, draft.Id);
        var email = await client.GetFromJsonAsync<EmailDto>(EmailPath(team, draft.Id), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal($"{EmailPath(team, draft.Id)}/deliveries", response.Headers.Location?.OriginalString);
        Assert.Equal(EmailStatuses.Sending, accepted?.Status);
        Assert.Equal(caller, accepted?.SentById);
        Assert.All(deliveries, d => Assert.Equal(DeliveryStatuses.Sent, d.Status));
        Assert.Equal(["ada@contoso.com", "bob@contoso.com"], deliveries.Select(d => d.Address));
        Assert.Equal(EmailStatuses.Sent, email?.Status);
        Assert.Equal(new EmailDeliverySummaryDto(0, 2, 0), email?.Deliveries);
        await _mailbox.Received(2).SendDraftAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnscheduleEmail_ScheduledForLater_ReturnsItToADraft()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        var draft = await CreateDraftAsync(factory, team, caller, "ada@contoso.com");
        using var client = TestCallers.CreateClient(factory, caller);

        using var send = await client.PostAsJsonAsync(
            $"{EmailPath(team, draft.Id)}/send",
            new SendEmailActionDto(draft.Version, DateTimeOffset.UtcNow.AddHours(1)),
            ApiJson.Options,
            TestContext.Current.CancellationToken);
        var scheduled = await send.Content.ReadFromJsonAsync<EmailDto>(ApiJson.Options, TestContext.Current.CancellationToken);
        using var unschedule = await client.PostAsJsonAsync(
            $"{EmailPath(team, draft.Id)}/unschedule",
            new EmailVersionActionDto(scheduled!.Version),
            ApiJson.Options,
            TestContext.Current.CancellationToken);
        var unscheduled = await unschedule.Content.ReadFromJsonAsync<EmailDto>(ApiJson.Options, TestContext.Current.CancellationToken);
        var deliveries = await client.GetFromJsonAsync<List<EmailDeliveryDto>>($"{EmailPath(team, draft.Id)}/deliveries", ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(EmailStatuses.Scheduled, scheduled.Status);
        Assert.Equal(HttpStatusCode.OK, unschedule.StatusCode);
        Assert.Equal(EmailStatuses.Draft, unscheduled?.Status);
        Assert.Null(unscheduled?.ScheduledFor);
        Assert.Empty(deliveries!);
        await _mailbox.DidNotReceive().CreateDraftAsync(Arg.Any<Guid>(), Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RetryEmail_OneRecipientRejected_SendsAgainOnlyToThatRecipient()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        var rejectBad = true;
        _mailbox.CreateDraftAsync(Arg.Any<Guid>(), Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>())
            .Returns(call => rejectBad && call.Arg<OutgoingMessage>().RecipientAddress == "bad@contoso.com"
                ? throw new MailboxException(MailboxFailureKinds.Permanent, "The mail server rejected the message (ErrorInvalidRecipients).")
                : $"draft-{Guid.NewGuid():N}");
        var draft = await CreateDraftAsync(factory, team, caller, "ada@contoso.com", "bad@contoso.com");
        using var client = TestCallers.CreateClient(factory, caller);

        using var send = await client.PostAsJsonAsync($"{EmailPath(team, draft.Id)}/send", new SendEmailActionDto(draft.Version, null), ApiJson.Options, TestContext.Current.CancellationToken);
        send.EnsureSuccessStatusCode();
        var first = await PollUntilSettledAsync(client, team, draft.Id);
        var partial = await client.GetFromJsonAsync<EmailDto>(EmailPath(team, draft.Id), ApiJson.Options, TestContext.Current.CancellationToken);
        rejectBad = false;
        using var retry = await client.PostAsJsonAsync($"{EmailPath(team, draft.Id)}/retry", new EmailVersionActionDto(partial!.Version), ApiJson.Options, TestContext.Current.CancellationToken);
        var second = await PollUntilSettledAsync(client, team, draft.Id);
        var email = await client.GetFromJsonAsync<EmailDto>(EmailPath(team, draft.Id), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(EmailStatuses.PartiallySent, partial.Status);
        Assert.Equal(DeliveryStatuses.Failed, first.Single(d => d.Address == "bad@contoso.com").Status);
        Assert.Contains("ErrorInvalidRecipients", first.Single(d => d.Address == "bad@contoso.com").LastError, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
        Assert.All(second, d => Assert.Equal(DeliveryStatuses.Sent, d.Status));
        Assert.Equal(EmailStatuses.Sent, email?.Status);
        await _mailbox.Received(1).CreateDraftAsync(Arg.Any<Guid>(), Arg.Is<OutgoingMessage>(m => m.RecipientAddress == "ada@contoso.com"), Arg.Any<CancellationToken>());
        await _mailbox.Received(2).SendDraftAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteEmail_WhileScheduled_ReturnsConflict()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        var draft = await CreateDraftAsync(factory, team, caller, "ada@contoso.com");
        using var client = TestCallers.CreateClient(factory, caller);

        using var send = await client.PostAsJsonAsync(
            $"{EmailPath(team, draft.Id)}/send",
            new SendEmailActionDto(draft.Version, DateTimeOffset.UtcNow.AddHours(1)),
            ApiJson.Options,
            TestContext.Current.CancellationToken);
        send.EnsureSuccessStatusCode();
        using var delete = await client.DeleteAsync(new Uri(EmailPath(team, draft.Id), UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    #region Private methods

    private static string EmailPath(Guid teamId, Guid emailId) => $"/api/teams/{teamId}/emails/{emailId}";

    private AndesMailerApiFactory CreateFactory()
    {
        _mailbox.CreateDraftAsync(Arg.Any<Guid>(), Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>())
            .Returns(_ => $"draft-{Guid.NewGuid():N}");
        _mailbox.DraftExistsAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _directory.FindByEmailsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, DirectoryUser>());

        return new(
            _fixture.ConnectionString,
            _azurite.ConnectionString,
            useTestAuthentication: true,
            configuration: new Dictionary<string, string?> { [$"{EmailSendingOptions.SectionName}:{nameof(EmailSendingOptions.Enabled)}"] = "true" },
            configureTestServices: services => services.AddScoped(_ => _directory).AddScoped(_ => _mailbox));
    }

    private static async Task<EmailDto> CreateDraftAsync(AndesMailerApiFactory factory, Guid teamId, Guid callerId, params string[] addresses)
    {
        using var client = TestCallers.CreateClient(factory, callerId);
        using var response = await client.PostAsJsonAsync(
            $"/api/teams/{teamId}/emails",
            new CreateEmailActionDto("Report", "<p>Hi</p>", [.. addresses.Select(a => new EmailRecipientActionDto(a, RecipientTypes.To))]),
            ApiJson.Options,
            TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<EmailDto>(ApiJson.Options, TestContext.Current.CancellationToken))!;
    }

    // The dispatcher works in the background, so the test reads the deliveries until none is waiting or in progress.
    private static async Task<List<EmailDeliveryDto>> PollUntilSettledAsync(HttpClient client, Guid teamId, Guid emailId)
    {
        var deadline = DateTimeOffset.UtcNow + _settleTimeout;
        while (true)
        {
            var deliveries = await client.GetFromJsonAsync<List<EmailDeliveryDto>>($"{EmailPath(teamId, emailId)}/deliveries", ApiJson.Options, TestContext.Current.CancellationToken);
            var email = await client.GetFromJsonAsync<EmailDto>(EmailPath(teamId, emailId), ApiJson.Options, TestContext.Current.CancellationToken);
            if (email?.Status is not EmailStatuses.Sending || DateTimeOffset.UtcNow > deadline)
            {
                return deliveries!;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        }
    }

    #endregion
}
