using System.Globalization;
using System.Net;

using Andes.Mailer.Common.Enums;
using Andes.Mailer.Repository.Blob;
using Andes.Mailer.Repository.Blob.Exceptions;
using Andes.Mailer.Service.Emails.Sending.Exceptions;
using Andes.Mailer.Service.Emails.Sending.Models;

using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Graph.Users.Item.Messages.Item.Attachments.CreateUploadSession;
using Microsoft.Identity.Web;
using Microsoft.Kiota.Abstractions;

namespace Andes.Mailer.Service.Emails.Sending;

/// <summary>
/// Creates and sends messages from a team's service mailbox. The only code that sends mail.
/// </summary>
/// <remarks>
/// A message is created as a draft and then sent, rather than sent in one call, so a retry can tell whether an earlier attempt's send
/// went through: once sent, the draft is gone.
/// </remarks>
public interface IMailboxClient
{
    /// <summary>
    /// Creates a draft of the message, with its attachments, in the mailbox.
    /// </summary>
    /// <param name="mailboxId">The Entra object id of the mailbox.</param>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The id of the draft.</returns>
    /// <exception cref="MailboxException">The mailbox could not create the draft.</exception>
    Task<string> CreateDraftAsync(Guid mailboxId, OutgoingMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a draft.
    /// </summary>
    /// <param name="mailboxId">The Entra object id of the mailbox.</param>
    /// <param name="messageId">The id of the draft.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the mail server accepts the message.</returns>
    /// <exception cref="MailboxException">The mailbox could not send the draft.</exception>
    Task SendDraftAsync(Guid mailboxId, string messageId, CancellationToken cancellationToken);

    /// <summary>
    /// Answers whether a draft is still waiting to be sent.
    /// </summary>
    /// <param name="mailboxId">The Entra object id of the mailbox.</param>
    /// <param name="messageId">The id of the draft.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns><see langword="true"/> if the draft is still unsent; <see langword="false"/> if it was sent or no longer exists.</returns>
    /// <exception cref="MailboxException">The mailbox could not be read.</exception>
    Task<bool> DraftExistsAsync(Guid mailboxId, string messageId, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a draft that will not be sent.
    /// </summary>
    /// <param name="mailboxId">The Entra object id of the mailbox.</param>
    /// <param name="messageId">The id of the draft.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the draft is deleted or was already gone.</returns>
    /// <exception cref="MailboxException">The mailbox could not delete the draft.</exception>
    Task DeleteDraftAsync(Guid mailboxId, string messageId, CancellationToken cancellationToken);
}

/// <summary>
/// Creates and sends messages through Microsoft Graph, app-only, from documents held in blob storage.
/// </summary>
/// <param name="graph">The Graph client.</param>
/// <param name="store">The blob storage the attachments are read from.</param>
public sealed class GraphMailboxClient(GraphServiceClient graph, IAttachmentStore store) : IMailboxClient
{
    // Graph takes a file attachment in one request only below 3 MB; larger ones need an upload session.
    private const long UploadSessionThresholdBytes = 3 * 1024 * 1024;

    // Outlook accepts at most 4 MB per upload-session request.
    private const int UploadSliceBytes = 3 * 1024 * 1024;

    // The Exchange code for a message id that does not resolve in a mailbox that exists.
    private const string MessageNotFoundCode = "ErrorItemNotFound";

    private static readonly TimeSpan _defaultThrottleDelay = TimeSpan.FromMinutes(1);

    private readonly GraphServiceClient _graph = graph;
    private readonly IAttachmentStore _store = store;

    /// <inheritdoc/>
    public async Task<string> CreateDraftAsync(Guid mailboxId, OutgoingMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var mailbox = _graph.Users[mailboxId.ToString()];
        var draft = await CallAsync(
            () => mailbox.Messages.PostAsync(
                new Message
                {
                    Subject = message.Subject,
                    Body = new ItemBody { ContentType = BodyType.Html, Content = message.BodyHtml },
                    ToRecipients = [new Recipient { EmailAddress = new EmailAddress { Address = message.RecipientAddress, Name = message.RecipientName } }],
                },
                request => request.Options.WithAppOnly(),
                cancellationToken),
            cancellationToken);
        var messageId = draft?.Id
            ?? throw new MailboxException(MailboxFailureKinds.Transient, "The mail server created the message without an id.");

        try
        {
            foreach (var attachment in message.Attachments)
            {
                await AttachAsync(mailboxId, messageId, attachment, cancellationToken);
            }
        }
        catch
        {
            // The caller never learns this draft's id, so it is removed here; the next attempt starts from a new one.
            await DeleteDraftBestEffortAsync(mailboxId, messageId);
            throw;
        }

        return messageId;
    }

    /// <inheritdoc/>
    public async Task SendDraftAsync(Guid mailboxId, string messageId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        await CallAsync(
            async () =>
            {
                await _graph.Users[mailboxId.ToString()].Messages[messageId].Send.PostAsync(request => request.Options.WithAppOnly(), cancellationToken);

                return true;
            },
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> DraftExistsAsync(Guid mailboxId, string messageId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        try
        {
            var message = await CallAsync(
                () => _graph.Users[mailboxId.ToString()].Messages[messageId].GetAsync(
                    request =>
                    {
                        request.QueryParameters.Select = ["id", "isDraft"];
                        request.Options.WithAppOnly();
                    },
                    cancellationToken),
                cancellationToken);

            return message?.IsDraft == true;
        }
        catch (MailboxException ex) when (IsMessageNotFound(ex))
        {
            // Sending moves the draft to Sent Items under a new id, so the draft's own id no longer resolves. Only a missing message
            // counts: a 404 for a deleted or unlicensed mailbox says nothing about the draft, so it stays a failure and the caller keeps the id.
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteDraftAsync(Guid mailboxId, string messageId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

        try
        {
            await CallAsync(
                async () =>
                {
                    await _graph.Users[mailboxId.ToString()].Messages[messageId].DeleteAsync(request => request.Options.WithAppOnly(), cancellationToken);

                    return true;
                },
                cancellationToken);
        }
        catch (MailboxException ex) when (IsMessageNotFound(ex))
        {
            // Already gone, which is what was asked for.
        }
    }

    #region Private methods

    private static bool IsMessageNotFound(MailboxException exception) =>
        exception.InnerException is ODataError { ResponseStatusCode: (int)HttpStatusCode.NotFound, Error.Code: MessageNotFoundCode };

    private async Task DeleteDraftBestEffortAsync(Guid mailboxId, string messageId)
    {
        try
        {
            await DeleteDraftAsync(mailboxId, messageId, CancellationToken.None);
        }
        catch (MailboxException)
        {
            // Left in the mailbox's Drafts folder; it is never sent.
        }
    }

    private async Task AttachAsync(Guid mailboxId, string messageId, OutgoingAttachment attachment, CancellationToken cancellationToken)
    {
        using var content = new MemoryStream();
        try
        {
            await using var blob = await _store.OpenReadAsync(attachment.BlobName, cancellationToken);
            await blob.CopyToAsync(content, cancellationToken);
        }
        catch (BlobNotFoundException ex)
        {
            throw new MailboxException(MailboxFailureKinds.Permanent, "One of the email's attachments is no longer in storage.", innerException: ex);
        }

        content.Position = 0;
        var attachments = _graph.Users[mailboxId.ToString()].Messages[messageId].Attachments;
        if (content.Length < UploadSessionThresholdBytes)
        {
            await CallAsync(
                () => attachments.PostAsync(
                    new FileAttachment
                    {
                        OdataType = "#microsoft.graph.fileAttachment",
                        Name = attachment.FileName,
                        ContentType = attachment.ContentType,
                        ContentBytes = content.ToArray(),
                    },
                    request => request.Options.WithAppOnly(),
                    cancellationToken),
                cancellationToken);
            return;
        }

        var session = await CallAsync(
            () => attachments.CreateUploadSession.PostAsync(
                new CreateUploadSessionPostRequestBody
                {
                    AttachmentItem = new AttachmentItem
                    {
                        AttachmentType = AttachmentType.File,
                        Name = attachment.FileName,
                        ContentType = attachment.ContentType,
                        Size = content.Length,
                    },
                },
                request => request.Options.WithAppOnly(),
                cancellationToken),
            cancellationToken)
            ?? throw new MailboxException(MailboxFailureKinds.Transient, "The mail server did not open an upload for an attachment.");

        // No request adapter: the upload URL carries its own authorization, and Outlook rejects a request that adds a bearer token to it.
        var upload = new LargeFileUploadTask<FileAttachment>(session, content, UploadSliceBytes);
        var result = await CallAsync(() => upload.UploadAsync(cancellationToken: cancellationToken), cancellationToken);
        if (result is null || !result.UploadSucceeded)
        {
            throw new MailboxException(MailboxFailureKinds.Transient, "An attachment could not be uploaded to the mail server.");
        }
    }

    // One place that turns Graph and network faults into the three outcomes the dispatcher acts on.
    private static async Task<T> CallAsync<T>(Func<Task<T>> call, CancellationToken cancellationToken)
    {
        try
        {
            return await call();
        }
        catch (ODataError ex)
        {
            throw Classify(ex.ResponseStatusCode, ex.Error?.Code, ex.ResponseHeaders, ex);
        }
        catch (ApiException ex)
        {
            throw Classify(ex.ResponseStatusCode, null, ex.ResponseHeaders, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new MailboxException(MailboxFailureKinds.Transient, "The mail server could not be reached.", innerException: ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MailboxException(MailboxFailureKinds.Transient, "The mail server did not answer in time.", innerException: ex);
        }
    }

    private static MailboxException Classify(int status, string? code, IDictionary<string, IEnumerable<string>> headers, Exception exception)
    {
        // The Graph error code names the fault without echoing addresses or content, so it is safe to show the team.
        var detail = string.IsNullOrWhiteSpace(code) ? $"HTTP {status}" : code;

        return status switch
        {
            (int)HttpStatusCode.TooManyRequests or (int)HttpStatusCode.ServiceUnavailable => new MailboxException(
                MailboxFailureKinds.Throttled,
                $"The mail server is throttling the mailbox ({detail}).",
                RetryAfterOf(headers) ?? _defaultThrottleDelay,
                exception),
            (int)HttpStatusCode.RequestTimeout or >= 500 => new MailboxException(
                MailboxFailureKinds.Transient,
                $"The mail server failed ({detail}).",
                innerException: exception),
            >= 400 => new MailboxException(
                MailboxFailureKinds.Permanent,
                $"The mail server rejected the message ({detail}).",
                innerException: exception),
            _ => new MailboxException(
                MailboxFailureKinds.Transient,
                $"The mail server answered unexpectedly ({detail}).",
                innerException: exception),
        };
    }

    private static TimeSpan? RetryAfterOf(IDictionary<string, IEnumerable<string>> headers)
    {
        var value = headers
            .FirstOrDefault(h => string.Equals(h.Key, "Retry-After", StringComparison.OrdinalIgnoreCase))
            .Value?
            .FirstOrDefault();
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return null;
    }

    #endregion
}
