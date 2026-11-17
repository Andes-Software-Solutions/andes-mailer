using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Emails;
using Andes.Mailer.Dto.Actions.Emails.Templates;
using Andes.Mailer.Dto.Emails;
using Andes.Mailer.Dto.Emails.Templates;
using Andes.Mailer.Dto.Pagination;
using Andes.Mailer.Integration.Test.TestInfrastructure;
using Andes.Mailer.Service.Users;
using Andes.Mailer.Service.Users.Models;

using Azure.Storage.Blobs;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Andes.Mailer.Integration.Test.Endpoints;

public sealed class EmailTemplateEndpointsIntegrationTests(SqlServerFixture fixture, AzuriteFixture azurite)
{
    private const string AttachmentsContainer = "attachments";

    private static readonly byte[] _pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF\n");

    private readonly SqlServerFixture _fixture = fixture;
    private readonly AzuriteFixture _azurite = azurite;
    private readonly IDirectoryUserService _directory = CreateDirectory();

    [Fact]
    public async Task CreateTemplate_TeamAdministrator_ReturnsCreatedAndReadsBack()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var admin = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, admin);
        var action = new CreateEmailTemplateActionDto(
            " Welcome ",
            "Welcome aboard",
            "<p>Hi</p><script>alert(1)</script>",
            [new("zed@external.org", RecipientTypes.Cc), new("amy@external.org", RecipientTypes.To)]);

        using var response = await client.PostAsJsonAsync(TemplatesPath(team), action, ApiJson.Options, TestContext.Current.CancellationToken);
        var created = await response.Content.ReadFromJsonAsync<EmailTemplateDto>(ApiJson.Options, TestContext.Current.CancellationToken);
        var reloaded = await client.GetFromJsonAsync<EmailTemplateDto>(response.Headers.Location, ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"{TemplatesPath(team)}/{created!.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal("Welcome", reloaded!.Name);
        Assert.Equal("<p>Hi</p>", reloaded.BodyHtml);
        Assert.Equal(admin, reloaded.AuthorId);
        Assert.Equal(
            [new EmailTemplateRecipientDto("amy@external.org", RecipientTypes.To), new EmailTemplateRecipientDto("zed@external.org", RecipientTypes.Cc)],
            reloaded.Recipients);
    }

    [Fact]
    public async Task CreateTemplate_AdministratorOutsideTheTeam_ReturnsCreated()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var response = await client.PostAsJsonAsync(TemplatesPath(team), NewTemplate(), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateTemplate_TeamMember_ReturnsForbidden()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var member = await TestCallers.SeedMemberAsync(_fixture, team);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, member);

        using var response = await client.PostAsJsonAsync(TemplatesPath(team), NewTemplate(), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangeOrDeleteTemplate_TeamMember_ReturnsForbidden()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var admin = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var member = await TestCallers.SeedMemberAsync(_fixture, team);
        await using var factory = CreateFactory();
        using var adminClient = TestCallers.CreateClient(factory, admin);
        using var memberClient = TestCallers.CreateClient(factory, member);
        var template = await CreateTemplateAsync(adminClient, team);

        using var update = await memberClient.PutAsJsonAsync(
            $"{TemplatesPath(team)}/{template.Id}",
            new UpdateEmailTemplateActionDto("Renamed", string.Empty, string.Empty, [], template.Version),
            ApiJson.Options,
            TestContext.Current.CancellationToken);
        using var delete = await memberClient.DeleteAsync(new Uri($"{TemplatesPath(team)}/{template.Id}", UriKind.Relative), TestContext.Current.CancellationToken);
        using var upload = await UploadAsync(memberClient, team, template.Id, "report.pdf", _pdf);

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
    }

    [Fact]
    public async Task ListAndGetTemplate_TeamMember_ReturnsTheTeamsTemplates()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var admin = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var member = await TestCallers.SeedMemberAsync(_fixture, team);
        await using var factory = CreateFactory();
        using var adminClient = TestCallers.CreateClient(factory, admin);
        using var memberClient = TestCallers.CreateClient(factory, member);
        var template = await CreateTemplateAsync(adminClient, team);

        var page = await memberClient.GetFromJsonAsync<PaginatedResponseDto<EmailTemplateSummaryDto>>(TemplatesPath(team), ApiJson.Options, TestContext.Current.CancellationToken);
        var reloaded = await memberClient.GetFromJsonAsync<EmailTemplateDto>($"{TemplatesPath(team)}/{template.Id}", ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(template.Id, Assert.Single(page!.Items).Id);
        Assert.Equal(template.Name, reloaded!.Name);
    }

    [Fact]
    public async Task GetTemplate_CallerNotAMember_ReturnsNotFound()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var admin = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var outsider = await TestCallers.SeedAsync(_fixture);
        await using var factory = CreateFactory();
        using var adminClient = TestCallers.CreateClient(factory, admin);
        using var outsiderClient = TestCallers.CreateClient(factory, outsider);
        var template = await CreateTemplateAsync(adminClient, team);

        using var response = await outsiderClient.GetAsync(new Uri($"{TemplatesPath(team)}/{template.Id}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateTemplate_NameTakenInTheTeam_ReturnsConflict()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var admin = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, admin);
        var template = await CreateTemplateAsync(client, team);

        using var response = await client.PostAsJsonAsync(TemplatesPath(team), NewTemplate(template.Name), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTemplate_TeamAdministrator_HidesItAndFreesItsName()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var admin = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, admin);
        var template = await CreateTemplateAsync(client, team);

        using var delete = await client.DeleteAsync(new Uri($"{TemplatesPath(team)}/{template.Id}", UriKind.Relative), TestContext.Current.CancellationToken);
        using var get = await client.GetAsync(new Uri($"{TemplatesPath(team)}/{template.Id}", UriKind.Relative), TestContext.Current.CancellationToken);
        using var recreate = await client.PostAsJsonAsync(TemplatesPath(team), NewTemplate(template.Name), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.Created, recreate.StatusCode);
    }

    [Fact]
    public async Task CreateEmailFromTemplate_TeamMember_CopiesTheTemplateAndItsStoredDocument()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var admin = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var member = await TestCallers.SeedMemberAsync(_fixture, team);
        await using var factory = CreateFactory();
        using var adminClient = TestCallers.CreateClient(factory, admin);
        using var memberClient = TestCallers.CreateClient(factory, member);
        var template = await CreateTemplateAsync(adminClient, team, new EmailRecipientActionDto("amy@external.org", RecipientTypes.To));
        using (var upload = await UploadAsync(adminClient, team, template.Id, "Terms.pdf", _pdf))
        {
            Assert.Equal(HttpStatusCode.Accepted, upload.StatusCode);
            Assert.Equal(AttachmentStatuses.Uploaded, (await PollUntilSettledAsync(adminClient, upload.Headers.Location!)).Status);
        }

        using var response = await memberClient.PostAsJsonAsync($"{EmailsPath(team)}/from-template", new CreateEmailFromTemplateActionDto(template.Id), ApiJson.Options, TestContext.Current.CancellationToken);
        var email = await response.Content.ReadFromJsonAsync<EmailDto>(ApiJson.Options, TestContext.Current.CancellationToken);
        var attachment = Assert.Single(email!.Attachments);
        var downloaded = await memberClient.GetByteArrayAsync(new Uri($"{EmailsPath(team)}/{email.Id}/attachments/{attachment.Id}/content", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"{EmailsPath(team)}/{email.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(template.Id, email.TemplateId);
        Assert.Equal(member, email.AuthorId);
        Assert.Equal(template.Subject, email.Subject);
        Assert.Equal([new EmailRecipientDto("amy@external.org", RecipientTypes.To, null, null)], email.Recipients);
        Assert.Equal(("Terms.pdf", AttachmentStatuses.Uploaded), (attachment.FileName, attachment.Status));
        Assert.Equal(_pdf, downloaded);
    }

    [Fact]
    public async Task CreateEmailFromTemplate_ThenChangeAndDelete_BehavesLikeAnyDraftAndLeavesTheTemplate()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var admin = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var member = await TestCallers.SeedMemberAsync(_fixture, team);
        await using var factory = CreateFactory();
        using var adminClient = TestCallers.CreateClient(factory, admin);
        using var memberClient = TestCallers.CreateClient(factory, member);
        var template = await CreateTemplateAsync(adminClient, team);
        using var created = await memberClient.PostAsJsonAsync($"{EmailsPath(team)}/from-template", new CreateEmailFromTemplateActionDto(template.Id), ApiJson.Options, TestContext.Current.CancellationToken);
        var email = (await created.Content.ReadFromJsonAsync<EmailDto>(ApiJson.Options, TestContext.Current.CancellationToken))!;

        using var update = await memberClient.PutAsJsonAsync(
            $"{EmailsPath(team)}/{email.Id}",
            new UpdateEmailActionDto("Edited", "<p>Edited</p>", [], email.Version),
            ApiJson.Options,
            TestContext.Current.CancellationToken);
        using var delete = await memberClient.DeleteAsync(new Uri($"{EmailsPath(team)}/{email.Id}", UriKind.Relative), TestContext.Current.CancellationToken);
        var unchanged = await memberClient.GetFromJsonAsync<EmailTemplateDto>($"{TemplatesPath(team)}/{template.Id}", ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(template.Subject, unchanged!.Subject);
    }

    [Fact]
    public async Task CreateEmailFromTemplate_CallerNotAMember_ReturnsNotFound()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var admin = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var outsider = await TestCallers.SeedAsync(_fixture);
        await using var factory = CreateFactory();
        using var adminClient = TestCallers.CreateClient(factory, admin);
        using var outsiderClient = TestCallers.CreateClient(factory, outsider);
        var template = await CreateTemplateAsync(adminClient, team);

        using var response = await outsiderClient.PostAsJsonAsync($"{EmailsPath(team)}/from-template", new CreateEmailFromTemplateActionDto(template.Id), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTemplateAttachment_TeamMemberThenTeamAdministrator_ForbidsThenRemovesIt()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var admin = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var member = await TestCallers.SeedMemberAsync(_fixture, team);
        await using var factory = CreateFactory();
        using var adminClient = TestCallers.CreateClient(factory, admin);
        using var memberClient = TestCallers.CreateClient(factory, member);
        var template = await CreateTemplateAsync(adminClient, team);
        var location = await UploadStoredAsync(adminClient, team, template.Id);

        using var forbidden = await memberClient.DeleteAsync(location, TestContext.Current.CancellationToken);
        using var removed = await adminClient.DeleteAsync(location, TestContext.Current.CancellationToken);
        using var gone = await adminClient.GetAsync(location, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task GetTemplateAttachment_CallerNotAMember_ReturnsNotFound()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var admin = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var outsider = await TestCallers.SeedAsync(_fixture);
        await using var factory = CreateFactory();
        using var adminClient = TestCallers.CreateClient(factory, admin);
        using var outsiderClient = TestCallers.CreateClient(factory, outsider);
        var template = await CreateTemplateAsync(adminClient, team);
        var location = await UploadStoredAsync(adminClient, team, template.Id);

        using var get = await outsiderClient.GetAsync(location, TestContext.Current.CancellationToken);
        using var download = await outsiderClient.GetAsync(new Uri($"{location}/content", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
    }

    [Fact]
    public async Task CreateEmailFromTemplate_TemplatesDocumentMissingFromStorage_ReturnsNotFoundAndSavesNoEmail()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var admin = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, admin);
        var template = await CreateTemplateAsync(client, team);
        await UploadStoredAsync(client, team, template.Id);
        string blobName;
        await using (var ctx = _fixture.CreateContext())
        {
            blobName = await ctx.EmailTemplateAttachments.Where(a => a.TemplateId == template.Id).Select(a => a.BlobName).SingleAsync(TestContext.Current.CancellationToken);
        }

        await new BlobContainerClient(_azurite.ConnectionString, AttachmentsContainer).DeleteBlobAsync(blobName, cancellationToken: TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync($"{EmailsPath(team)}/from-template", new CreateEmailFromTemplateActionDto(template.Id), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var readCtx = _fixture.CreateContext();
        Assert.False(await readCtx.Emails.AnyAsync(e => e.TemplateId == template.Id, TestContext.Current.CancellationToken));
    }

    #region Private methods

    private static async Task<Uri> UploadStoredAsync(HttpClient client, Guid teamId, Guid templateId)
    {
        using var upload = await UploadAsync(client, teamId, templateId, "Terms.pdf", _pdf);
        Assert.Equal(HttpStatusCode.Accepted, upload.StatusCode);
        Assert.Equal(AttachmentStatuses.Uploaded, (await PollUntilSettledAsync(client, upload.Headers.Location!)).Status);

        return upload.Headers.Location!;
    }

    // No recipient is a directory account unless a test says otherwise.
    private static IDirectoryUserService CreateDirectory()
    {
        var directory = Substitute.For<IDirectoryUserService>();
        directory.FindByEmailsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, DirectoryUser>());

        return directory;
    }

    private static string TemplatesPath(Guid teamId) => $"/api/teams/{teamId}/templates";

    private static string EmailsPath(Guid teamId) => $"/api/teams/{teamId}/emails";

    private static CreateEmailTemplateActionDto NewTemplate(string? name = null, params EmailRecipientActionDto[] recipients) =>
        new(name ?? $"Template {Guid.NewGuid():N}", "Monthly report", "<p>Hello</p>", recipients);

    private static async Task<EmailTemplateDto> CreateTemplateAsync(HttpClient client, Guid teamId, params EmailRecipientActionDto[] recipients)
    {
        using var response = await client.PostAsJsonAsync(TemplatesPath(teamId), NewTemplate(null, recipients), ApiJson.Options, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<EmailTemplateDto>(ApiJson.Options, TestContext.Current.CancellationToken))!;
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid teamId, Guid templateId, string fileName, byte[] content)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);

        return await client.PostAsync(new Uri($"{TemplatesPath(teamId)}/{templateId}/attachments", UriKind.Relative), form, TestContext.Current.CancellationToken);
    }

    // The upload runs in the background, so the test polls the way the UI does.
    private static async Task<EmailAttachmentDto> PollUntilSettledAsync(HttpClient client, Uri location)
    {
        for (var attempt = 0; attempt < 120; attempt++)
        {
            var attachment = await client.GetFromJsonAsync<EmailAttachmentDto>(location, ApiJson.Options, TestContext.Current.CancellationToken);
            if (attachment!.Status is AttachmentStatuses.Uploaded or AttachmentStatuses.Failed)
            {
                return attachment;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("The attachment upload did not settle within 30 seconds.");
    }

    private AndesMailerApiFactory CreateFactory() => new(
        _fixture.ConnectionString,
        _azurite.ConnectionString,
        useTestAuthentication: true,
        configureTestServices: services => services.AddScoped(_ => _directory));

    #endregion
}
