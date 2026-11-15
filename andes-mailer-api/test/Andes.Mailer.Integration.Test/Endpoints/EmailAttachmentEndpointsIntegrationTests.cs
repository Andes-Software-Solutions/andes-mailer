using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Emails;
using Andes.Mailer.Dto.Emails;
using Andes.Mailer.Integration.Test.TestInfrastructure;
using Andes.Mailer.Repository.Blob;

using Azure.Storage.Blobs;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Andes.Mailer.Integration.Test.Endpoints;

public sealed class EmailAttachmentEndpointsIntegrationTests(SqlServerFixture fixture, AzuriteFixture azurite)
{
    private const string EmailsPath = "/api/emails";
    private const string AttachmentsContainer = "attachments";

    private static readonly byte[] _pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj << /Type /Catalog >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF\n");

    private readonly SqlServerFixture _fixture = fixture;
    private readonly AzuriteFixture _azurite = azurite;

    [Fact]
    public async Task UploadAttachment_Pdf_StoresInBackgroundAndDownloadsSameBytes()
    {
        var caller = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);
        var email = await CreateDraftAsync(client);

        using var response = await UploadAsync(client, email.Id, "Q3 report.pdf", _pdf);
        var accepted = await response.Content.ReadFromJsonAsync<EmailAttachmentDto>(ApiJson.Options, TestContext.Current.CancellationToken);
        var stored = await PollUntilSettledAsync(client, response.Headers.Location!);
        var downloaded = await client.GetByteArrayAsync(new Uri($"{response.Headers.Location}/content", UriKind.Relative), TestContext.Current.CancellationToken);
        var reloaded = await client.GetFromJsonAsync<EmailDto>($"{EmailsPath}/{email.Id}", ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(AttachmentStatuses.Pending, accepted!.Status);
        Assert.Equal(DocumentTypes.Pdf, accepted.DocumentType);
        Assert.Equal("application/pdf", accepted.ContentType);
        Assert.Equal(_pdf.Length, accepted.SizeBytes);
        Assert.Equal(AttachmentStatuses.Uploaded, stored.Status);
        Assert.Equal(_pdf, downloaded);
        Assert.Equal(("Q3 report.pdf", AttachmentStatuses.Uploaded), (reloaded!.Attachments.Single().FileName, reloaded.Attachments.Single().Status));
    }

    [Fact]
    public async Task UploadAttachment_TeamAdministrator_IsAccepted()
    {
        var caller = await TestCallers.SeedAsync(_fixture, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);
        var email = await CreateDraftAsync(client);

        using var response = await UploadAsync(client, email.Id, "slide.png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00]);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task UploadAttachment_WithoutCanAttachDocuments_ReturnsForbiddenProblem()
    {
        var caller = await TestCallers.SeedAsync(_fixture);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);
        var email = await CreateDraftAsync(client);

        using var response = await UploadAsync(client, email.Id, "report.pdf", _pdf);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(nameof(Permissions.CanAttachDocuments), problem!.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("report.pdf", "MZ\u0090\u0000 executable")]
    [InlineData("notes.txt", "plain text")]
    [InlineData("deck.pptx", "%PDF-1.7 not a zip")]
    public async Task UploadAttachment_TypeNotAcceptedOrContentMismatch_ReturnsValidationProblem(string fileName, string content)
    {
        var caller = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);
        var email = await CreateDraftAsync(client);

        using var response = await UploadAsync(client, email.Id, fileName, Encoding.Latin1.GetBytes(content));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        var reloaded = await client.GetFromJsonAsync<EmailDto>($"{EmailsPath}/{email.Id}", ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("file", problem!.Errors.Keys);
        Assert.Empty(reloaded!.Attachments);
    }

    [Fact]
    public async Task UploadAttachment_AnotherUsersDraft_ReturnsNotFound()
    {
        var owner = await TestCallers.SeedAsync(_fixture);
        var other = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        await using var factory = CreateFactory();
        using var ownerClient = TestCallers.CreateClient(factory, owner);
        var email = await CreateDraftAsync(ownerClient);
        using var otherClient = TestCallers.CreateClient(factory, other);

        using var response = await UploadAsync(otherClient, email.Id, "report.pdf", _pdf);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteAttachment_Uploaded_RemovesRowAndBlob()
    {
        var caller = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);
        var email = await CreateDraftAsync(client);
        using var upload = await UploadAsync(client, email.Id, "report.pdf", _pdf);
        var location = upload.Headers.Location!;
        var stored = await PollUntilSettledAsync(client, location);
        var blob = new BlobServiceClient(_azurite.ConnectionString)
            .GetBlobContainerClient(AttachmentsContainer)
            .GetBlobClient($"{email.Id:N}/{stored.Id:N}");
        Assert.True((await blob.ExistsAsync(TestContext.Current.CancellationToken)).Value);

        using var deleted = await client.DeleteAsync(location, TestContext.Current.CancellationToken);
        using var get = await client.GetAsync(location, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.False((await blob.ExistsAsync(TestContext.Current.CancellationToken)).Value);
    }

    [Fact]
    public async Task DeleteAttachment_WhileUploading_HidesRowAndDeletesBlobOnceUploadFinishes()
    {
        var store = Substitute.For<IAttachmentStore>();
        var uploadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseUpload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.UploadAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                uploadStarted.TrySetResult();
                return releaseUpload.Task;
            });
        var caller = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        await using var factory = new AndesMailerApiFactory(
            _fixture.ConnectionString,
            _azurite.ConnectionString,
            useTestAuthentication: true,
            configureTestServices: services => services.AddSingleton(store));
        using var client = TestCallers.CreateClient(factory, caller);
        var email = await CreateDraftAsync(client);
        using var upload = await UploadAsync(client, email.Id, "report.pdf", _pdf);
        var accepted = await upload.Content.ReadFromJsonAsync<EmailAttachmentDto>(ApiJson.Options, TestContext.Current.CancellationToken);
        await uploadStarted.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        using var deleted = await client.DeleteAsync(upload.Headers.Location, TestContext.Current.CancellationToken);
        releaseUpload.SetResult();
        var blobName = $"{email.Id:N}/{accepted!.Id:N}";
        for (var attempt = 0; attempt < 120 && store.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IAttachmentStore.DeleteIfExistsAsync)) < 3; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        }

        using var get = await client.GetAsync(upload.Headers.Location, TestContext.Current.CancellationToken);
        await using var ctx = _fixture.CreateContext();
        var row = await ctx.EmailAttachments.IgnoreQueryFilters().AsNoTracking().SingleAsync(a => a.Id == accepted.Id, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.NotNull(row.DateDeactivated);
        Assert.NotEqual(AttachmentStatuses.Uploaded, row.StatusId);

        // Twice by the removal (before and after hiding the row), once by the processor when its save found the row changed under it.
        await store.Received(3).DeleteIfExistsAsync(blobName, Arg.Any<CancellationToken>());
    }

    #region Private methods

    private static async Task<EmailDto> CreateDraftAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(EmailsPath, new CreateEmailActionDto("Report", "<p>Attached.</p>", []), ApiJson.Options, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<EmailDto>(ApiJson.Options, TestContext.Current.CancellationToken))!;
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid emailId, string fileName, byte[] content)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", fileName);

        return await client.PostAsync(new Uri($"{EmailsPath}/{emailId}/attachments", UriKind.Relative), form, TestContext.Current.CancellationToken);
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

    private AndesMailerApiFactory CreateFactory() => new(_fixture.ConnectionString, _azurite.ConnectionString, useTestAuthentication: true);

    #endregion
}
