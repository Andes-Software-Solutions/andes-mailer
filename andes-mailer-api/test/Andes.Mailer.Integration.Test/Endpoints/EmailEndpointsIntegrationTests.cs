using System.Net;
using System.Net.Http.Json;

using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Emails;
using Andes.Mailer.Dto.Emails;
using Andes.Mailer.Dto.Pagination;
using Andes.Mailer.Integration.Test.TestInfrastructure;
using Andes.Mailer.Service.Users;
using Andes.Mailer.Service.Users.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Andes.Mailer.Integration.Test.Endpoints;

public sealed class EmailEndpointsIntegrationTests(SqlServerFixture fixture, AzuriteFixture azurite)
{
    private readonly SqlServerFixture _fixture = fixture;
    private readonly AzuriteFixture _azurite = azurite;
    private readonly IDirectoryUserService _directory = Substitute.For<IDirectoryUserService>();

    [Fact]
    public async Task CreateEmail_ThenGet_ReturnsDraftExactlyAsSaved()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team);
        var colleagueId = Guid.NewGuid();
        _directory.FindByEmailsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, DirectoryUser> { ["grace@contoso.com"] = new(colleagueId, "Grace", "Hopper", "grace@contoso.com") });
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);
        var action = new CreateEmailActionDto(
            " Quarterly report ",
            """<p style="text-align: center">Hi <strong>team</strong></p><script>alert(1)</script><img src="x" onerror="alert(1)">""",
            [
                new("zed@external.org", RecipientTypes.Cc),
                new(" Grace@Contoso.com ", RecipientTypes.To),
                new("bob@external.org", RecipientTypes.Bcc),
                new("amy@external.org", RecipientTypes.To),
            ]);

        using var response = await client.PostAsJsonAsync(EmailsPath(team), action, ApiJson.Options, TestContext.Current.CancellationToken);
        var created = await response.Content.ReadFromJsonAsync<EmailDto>(ApiJson.Options, TestContext.Current.CancellationToken);
        var reloaded = await client.GetFromJsonAsync<EmailDto>($"{EmailsPath(team)}/{created!.Id}", ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"{EmailsPath(team)}/{created.Id}", response.Headers.Location?.OriginalString);
        Assert.NotNull(reloaded);
        Assert.Equal(caller, reloaded.AuthorId);
        Assert.Equal("Quarterly report", reloaded.Subject);
        Assert.Equal("""<p style="text-align: center">Hi <strong>team</strong></p>""", reloaded.BodyHtml);
        Assert.Equal(EmailStatuses.Draft, reloaded.Status);
        Assert.Equal(
            [
                new EmailRecipientDto("grace@contoso.com", RecipientTypes.To, colleagueId, "Grace Hopper"),
                new EmailRecipientDto("amy@external.org", RecipientTypes.To, null, null),
                new EmailRecipientDto("zed@external.org", RecipientTypes.Cc, null, null),
                new EmailRecipientDto("bob@external.org", RecipientTypes.Bcc, null, null),
            ],
            reloaded.Recipients);
        Assert.Equal(created.Version, reloaded.Version);
    }

    [Fact]
    public async Task CreateEmail_CallerNotAMember_ReturnsNotFound()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedAsync(_fixture);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var response = await client.PostAsJsonAsync(EmailsPath(team), new CreateEmailActionDto("Hi", string.Empty, []), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateEmail_AddressRepeatedUnderAnotherHeader_ReturnsValidationProblem()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);
        var action = new CreateEmailActionDto("Hi", string.Empty, [new("amy@external.org", RecipientTypes.To), new("AMY@external.org", RecipientTypes.Cc)]);

        using var response = await client.PostAsJsonAsync(EmailsPath(team), action, ApiJson.Options, TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(nameof(CreateEmailActionDto.Recipients), problem!.Errors.Keys);
    }

    [Fact]
    public async Task GetEmail_MemberOfAnotherTeam_ReturnsNotFoundThroughEitherTeam()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var otherTeam = await TestCallers.SeedTeamAsync(_fixture);
        var author = await TestCallers.SeedMemberAsync(_fixture, team);
        var outsider = await TestCallers.SeedMemberAsync(_fixture, otherTeam);
        await using var factory = CreateFactory();
        var email = await CreateDraftAsync(factory, team, author);
        using var client = TestCallers.CreateClient(factory, outsider);

        using var throughOwnTeam = await client.GetAsync(new Uri($"{EmailsPath(otherTeam)}/{email.Id}", UriKind.Relative), TestContext.Current.CancellationToken);
        using var throughEmailsTeam = await client.GetAsync(new Uri($"{EmailsPath(team)}/{email.Id}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, throughOwnTeam.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, throughEmailsTeam.StatusCode);
    }

    [Fact]
    public async Task GetEmail_AdministratorOutsideTheTeam_ReturnsDraft()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var author = await TestCallers.SeedMemberAsync(_fixture, team);
        var administrator = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        await using var factory = CreateFactory();
        var email = await CreateDraftAsync(factory, team, author);
        using var client = TestCallers.CreateClient(factory, administrator);

        using var response = await client.GetAsync(new Uri($"{EmailsPath(team)}/{email.Id}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEmail_TeammatesDraft_ReplacesContentAndRecipients()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var author = await TestCallers.SeedMemberAsync(_fixture, team);
        var teammate = await TestCallers.SeedMemberAsync(_fixture, team);
        await using var factory = CreateFactory();
        var email = await CreateDraftAsync(factory, team, author, new("amy@external.org", RecipientTypes.To), new("bob@external.org", RecipientTypes.Cc));
        using var client = TestCallers.CreateClient(factory, teammate);
        var action = new UpdateEmailActionDto("Second", "<p>Two</p>", [new("bob@external.org", RecipientTypes.To), new("cat@external.org", RecipientTypes.Bcc)], email.Version);

        using var response = await client.PutAsJsonAsync($"{EmailsPath(team)}/{email.Id}", action, ApiJson.Options, TestContext.Current.CancellationToken);
        var updated = await response.Content.ReadFromJsonAsync<EmailDto>(ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Second", updated!.Subject);
        Assert.Equal(author, updated.AuthorId);
        Assert.Equal(["bob@external.org", "cat@external.org"], updated.Recipients.Select(r => r.Address));
        Assert.Equal([RecipientTypes.To, RecipientTypes.Bcc], updated.Recipients.Select(r => r.Type));
        Assert.NotEqual(email.Version, updated.Version);
    }

    [Fact]
    public async Task UpdateEmail_RecipientsOnlyWithStaleVersion_ReturnsConflict()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team);
        await using var factory = CreateFactory();
        var email = await CreateDraftAsync(factory, team, caller);
        using var client = TestCallers.CreateClient(factory, caller);
        using var first = await client.PutAsJsonAsync(
            $"{EmailsPath(team)}/{email.Id}",
            new UpdateEmailActionDto(email.Subject, email.BodyHtml, [new("amy@external.org", RecipientTypes.To)], email.Version),
            ApiJson.Options,
            TestContext.Current.CancellationToken);

        using var second = await client.PutAsJsonAsync(
            $"{EmailsPath(team)}/{email.Id}",
            new UpdateEmailActionDto(email.Subject, email.BodyHtml, [new("bob@external.org", RecipientTypes.To)], email.Version),
            ApiJson.Options,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task DeleteEmail_TeamDraft_HidesItFromGetAndList()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team);
        await using var factory = CreateFactory();
        var email = await CreateDraftAsync(factory, team, caller);
        using var client = TestCallers.CreateClient(factory, caller);

        using var deleted = await client.DeleteAsync(new Uri($"{EmailsPath(team)}/{email.Id}", UriKind.Relative), TestContext.Current.CancellationToken);
        using var get = await client.GetAsync(new Uri($"{EmailsPath(team)}/{email.Id}", UriKind.Relative), TestContext.Current.CancellationToken);
        var list = await client.GetFromJsonAsync<PaginatedResponseDto<EmailSummaryDto>>(EmailsPath(team), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(0, list!.TotalCount);
    }

    #region Private methods

    private static string EmailsPath(Guid teamId) => $"/api/teams/{teamId}/emails";

    private AndesMailerApiFactory CreateFactory() => new(
        _fixture.ConnectionString,
        _azurite.ConnectionString,
        useTestAuthentication: true,
        configureTestServices: services => services.AddScoped(_ => _directory));

    private async Task<EmailDto> CreateDraftAsync(AndesMailerApiFactory factory, Guid teamId, Guid callerId, params EmailRecipientActionDto[] recipients)
    {
        _directory.FindByEmailsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, DirectoryUser>());
        using var client = TestCallers.CreateClient(factory, callerId);
        using var response = await client.PostAsJsonAsync(EmailsPath(teamId), new CreateEmailActionDto("First", "<p>One</p>", recipients), ApiJson.Options, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<EmailDto>(ApiJson.Options, TestContext.Current.CancellationToken))!;
    }

    #endregion
}
