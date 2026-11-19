using System.Net;

using Andes.Mailer.Common.Enums;
using Andes.Mailer.Repository.Blob;
using Andes.Mailer.Repository.Blob.Exceptions;
using Andes.Mailer.Service.Emails.Sending;
using Andes.Mailer.Service.Emails.Sending.Exceptions;
using Andes.Mailer.Service.Emails.Sending.Models;
using Andes.Mailer.Unit.Test.TestInfrastructure;

using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Authentication;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Andes.Mailer.Unit.Test.Service.Emails.Sending;

public sealed class GraphMailboxClientTests
{
    private const string MessageId = "AAMkAD-draft";
    private static readonly Guid _mailboxId = Guid.NewGuid();

    private readonly IAttachmentStore _store = Substitute.For<IAttachmentStore>();

    [Fact]
    public async Task CreateDraftAsync_NoAttachments_PostsHtmlDraftToTheOneRecipient()
    {
        using var handler = new ScriptedHttpMessageHandler(_ => ScriptedHttpMessageHandler.Json(HttpStatusCode.Created, $$"""{ "id": "{{MessageId}}" }"""));

        var id = await CreateClient(handler).CreateDraftAsync(_mailboxId, Message(), TestContext.Current.CancellationToken);

        Assert.Equal(MessageId, id);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith($"/users/{_mailboxId}/messages", request.Uri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"subject\":\"Monthly report\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"contentType\":\"html\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"address\":\"ada@contoso.com\"", request.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("ccRecipients", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateDraftAsync_SmallAttachment_PostsItAsAFileAttachment()
    {
        _store.OpenReadAsync("emails/one", Arg.Any<CancellationToken>()).Returns(_ => new MemoryStream([1, 2, 3]));
        using var handler = new ScriptedHttpMessageHandler(_ => ScriptedHttpMessageHandler.Json(HttpStatusCode.Created, $$"""{ "id": "{{MessageId}}" }"""));

        await CreateClient(handler).CreateDraftAsync(_mailboxId, Message(new OutgoingAttachment("report.pdf", "application/pdf", 3, "emails/one")), TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.Requests.Count);
        var attach = handler.Requests[1];
        Assert.EndsWith($"/messages/{MessageId}/attachments", attach.Uri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#microsoft.graph.fileAttachment", attach.Body, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"report.pdf\"", attach.Body, StringComparison.Ordinal);
        Assert.Contains($"\"contentBytes\":\"{Convert.ToBase64String([1, 2, 3])}\"", attach.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateDraftAsync_AttachmentOfThreeMegabytes_OpensAnUploadSession()
    {
        const int size = 3 * 1024 * 1024;
        _store.OpenReadAsync("emails/big", Arg.Any<CancellationToken>()).Returns(_ => new MemoryStream(new byte[size]));
        using var handler = new ScriptedHttpMessageHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/createUploadSession", StringComparison.OrdinalIgnoreCase)
            // An unreachable upload URL: the upload itself goes to Outlook outside the Graph client, so only the session request is checked.
            ? ScriptedHttpMessageHandler.Json(HttpStatusCode.Created, """{ "uploadUrl": "http://127.0.0.1:1/upload", "expirationDateTime": "2030-01-01T00:00:00Z", "nextExpectedRanges": ["0-"] }""")
            : request.Method == HttpMethod.Delete
                ? ScriptedHttpMessageHandler.Empty(HttpStatusCode.NoContent)
                : ScriptedHttpMessageHandler.Json(HttpStatusCode.Created, $$"""{ "id": "{{MessageId}}" }"""));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            CreateClient(handler).CreateDraftAsync(_mailboxId, Message(new OutgoingAttachment("big.pdf", "application/pdf", size, "emails/big")), TestContext.Current.CancellationToken));

        var session = Assert.Single(handler.Requests, r => r.Uri.AbsolutePath.EndsWith("/createUploadSession", StringComparison.OrdinalIgnoreCase));
        Assert.Contains($"\"size\":{size}", session.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(handler.Requests, r => r.Uri.AbsolutePath.EndsWith($"/messages/{MessageId}/attachments", StringComparison.OrdinalIgnoreCase));

        // The upload failed, so the draft the caller never learns about is removed.
        Assert.Contains(handler.Requests, r => r.Method == HttpMethod.Delete && r.Uri.AbsolutePath.EndsWith($"/messages/{MessageId}", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateDraftAsync_AttachmentMissingFromStorage_ThrowsPermanentAndDeletesTheDraft()
    {
        _store.OpenReadAsync("emails/gone", Arg.Any<CancellationToken>()).ThrowsAsync(new BlobNotFoundException("emails/gone"));
        using var handler = new ScriptedHttpMessageHandler(request => request.Method == HttpMethod.Delete
            ? ScriptedHttpMessageHandler.Empty(HttpStatusCode.NoContent)
            : ScriptedHttpMessageHandler.Json(HttpStatusCode.Created, $$"""{ "id": "{{MessageId}}" }"""));

        var exception = await Assert.ThrowsAsync<MailboxException>(() =>
            CreateClient(handler).CreateDraftAsync(_mailboxId, Message(new OutgoingAttachment("gone.pdf", "application/pdf", 3, "emails/gone")), TestContext.Current.CancellationToken));

        Assert.Equal(MailboxFailureKinds.Permanent, exception.Kind);
        Assert.Equal(HttpMethod.Delete, handler.Requests[^1].Method);
    }

    [Fact]
    public async Task SendDraftAsync_Accepted_PostsToTheDraftsSendAction()
    {
        using var handler = new ScriptedHttpMessageHandler(_ => ScriptedHttpMessageHandler.Empty(HttpStatusCode.Accepted));

        await CreateClient(handler).SendDraftAsync(_mailboxId, MessageId, TestContext.Current.CancellationToken);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith($"/users/{_mailboxId}/messages/{MessageId}/send", request.Uri.AbsolutePath, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("""{ "id": "AAMkAD-draft", "isDraft": true }""", true)]
    [InlineData("""{ "id": "AAMkAD-draft", "isDraft": false }""", false)]
    public async Task DraftExistsAsync_MessageFound_AnswersWhetherItIsStillADraft(string json, bool expected)
    {
        using var handler = new ScriptedHttpMessageHandler(_ => ScriptedHttpMessageHandler.Json(HttpStatusCode.OK, json));

        var exists = await CreateClient(handler).DraftExistsAsync(_mailboxId, MessageId, TestContext.Current.CancellationToken);

        Assert.Equal(expected, exists);
    }

    [Fact]
    public async Task DraftExistsAsync_MessageNotFound_ReturnsFalse()
    {
        using var handler = new ScriptedHttpMessageHandler(_ => ScriptedHttpMessageHandler.Json(HttpStatusCode.NotFound, """{ "error": { "code": "ErrorItemNotFound", "message": "Not found." } }"""));

        var exists = await CreateClient(handler).DraftExistsAsync(_mailboxId, MessageId, TestContext.Current.CancellationToken);

        Assert.False(exists);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, MailboxFailureKinds.Throttled)]
    [InlineData(HttpStatusCode.ServiceUnavailable, MailboxFailureKinds.Throttled)]
    [InlineData(HttpStatusCode.InternalServerError, MailboxFailureKinds.Transient)]
    [InlineData(HttpStatusCode.RequestTimeout, MailboxFailureKinds.Transient)]
    [InlineData(HttpStatusCode.BadRequest, MailboxFailureKinds.Permanent)]
    [InlineData(HttpStatusCode.Forbidden, MailboxFailureKinds.Permanent)]
    public async Task SendDraftAsync_GraphError_ClassifiesTheFailure(HttpStatusCode status, MailboxFailureKinds expected)
    {
        using var handler = new ScriptedHttpMessageHandler(_ => ScriptedHttpMessageHandler.Json(status, """{ "error": { "code": "SomeCode", "message": "ada@contoso.com was rejected" } }"""));

        var exception = await Assert.ThrowsAsync<MailboxException>(() => CreateClient(handler).SendDraftAsync(_mailboxId, MessageId, TestContext.Current.CancellationToken));

        Assert.Equal(expected, exception.Kind);
        Assert.Contains("SomeCode", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ada@contoso.com", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendDraftAsync_ThrottledWithRetryAfter_CarriesTheWait()
    {
        using var handler = new ScriptedHttpMessageHandler(_ =>
        {
            var response = ScriptedHttpMessageHandler.Json(HttpStatusCode.TooManyRequests, """{ "error": { "code": "ApplicationThrottled", "message": "Slow down." } }""");
            response.Headers.Add("Retry-After", "17");

            return response;
        });

        var exception = await Assert.ThrowsAsync<MailboxException>(() => CreateClient(handler).SendDraftAsync(_mailboxId, MessageId, TestContext.Current.CancellationToken));

        Assert.Equal(MailboxFailureKinds.Throttled, exception.Kind);
        Assert.Equal(TimeSpan.FromSeconds(17), exception.RetryAfter);
    }

    #region Private methods

    private static OutgoingMessage Message(params OutgoingAttachment[] attachments) =>
        new("Monthly report", "<p>Hello</p>", "ada@contoso.com", "Ada Lovelace", attachments);

    // The Graph client owns the HttpClient it is given, so each test's client lives only as long as the test.
    private GraphMailboxClient CreateClient(ScriptedHttpMessageHandler handler) =>
        new(new GraphServiceClient(new HttpClient(handler, disposeHandler: false), new AnonymousAuthenticationProvider()), _store);

    #endregion
}
