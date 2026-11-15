using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Emails;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Repository.Blob;
using Andes.Mailer.Repository.Blob.Exceptions;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Emails.Attachments;
using Andes.Mailer.Service.Emails.Attachments.Exceptions;
using Andes.Mailer.Service.Emails.Exceptions;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Service.Options;
using Andes.Mailer.Unit.Test.TestInfrastructure;

using FluentValidation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Andes.Mailer.Unit.Test.Service.Emails.Attachments;

public sealed class AttachmentServiceTests : IDisposable
{
    private const long MaxFileBytes = 64;
    private const long MaxEmailBytes = 100;

    // A file database: one test uploads in parallel, and the in-memory connection is not thread-safe.
    private readonly MailerDbFixture _fixture = new(concurrent: true);
    private readonly TempDirectory _directory = new();
    private readonly IAttachmentStore _store = Substitute.For<IAttachmentStore>();
    private readonly AttachmentQuotaLock _quotaLock = new();
    private readonly IOptions<AttachmentUploadOptions> _options;
    private readonly AttachmentStagingArea _staging;
    private readonly AttachmentUploadChannel _channel;

    public AttachmentServiceTests()
    {
        _options = Microsoft.Extensions.Options.Options.Create(new AttachmentUploadOptions
        {
            StagingPath = _directory.Path,
            MaxFileBytes = MaxFileBytes,
            MaxEmailBytes = MaxEmailBytes,
        });
        _staging = new AttachmentStagingArea(_options);
        _channel = new AttachmentUploadChannel(_options);
    }

    [Fact]
    public async Task StageAsync_Pdf_StagesFileRecordsPendingRowAndQueuesIt()
    {
        var (senderId, emailId) = await AddDraftAsync();
        await using var ctx = _fixture.CreateContext();

        var attachment = await StageAsync(ctx, emailId, senderId, @"C:\Users\ada\Documents\report.pdf", TestFiles.Pdf);

        Assert.Equal("report.pdf", attachment.FileName);
        Assert.Equal(DocumentTypes.Pdf, attachment.DocumentType);
        Assert.Equal("application/pdf", attachment.ContentType);
        Assert.Equal(TestFiles.Pdf.Length, attachment.SizeBytes);
        Assert.Equal(AttachmentStatuses.Pending, attachment.Status);
        Assert.True(_staging.Exists(attachment.Id));
        Assert.Equal(attachment.Id, await FirstQueuedAsync());
        await using var readCtx = _fixture.CreateContext();
        var row = await readCtx.EmailAttachments.SingleAsync(a => a.Id == attachment.Id, TestContext.Current.CancellationToken);
        Assert.Equal($"{emailId:N}/{attachment.Id:N}", row.BlobName);
    }

    [Theory]
    [InlineData("setup.exe")]
    [InlineData("report.pdf ")]
    public async Task StageAsync_ExecutableContent_ThrowsValidationAndStagesNothing(string fileName)
    {
        var (senderId, emailId) = await AddDraftAsync();
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ValidationException>(() => StageAsync(ctx, emailId, senderId, fileName, TestFiles.Executable));

        Assert.Empty(_staging.ListStagedBefore(DateTimeOffset.MaxValue));
        await using var readCtx = _fixture.CreateContext();
        Assert.False(await readCtx.EmailAttachments.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StageAsync_FileOverLimit_ThrowsValidation()
    {
        var (senderId, emailId) = await AddDraftAsync();
        await using var ctx = _fixture.CreateContext();
        byte[] large = [.. TestFiles.Pdf, .. new byte[MaxFileBytes]];

        var exception = await Assert.ThrowsAsync<ValidationException>(() => StageAsync(ctx, emailId, senderId, "big.pdf", large));

        Assert.All(exception.Errors, e => Assert.Equal("file", e.PropertyName));
    }

    [Fact]
    public async Task StageAsync_EmailTotalOverLimit_ThrowsValidation()
    {
        var (senderId, emailId) = await AddDraftAsync(Attachment(MaxEmailBytes - 10, AttachmentStatuses.Uploaded));
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ValidationException>(() => StageAsync(ctx, emailId, senderId, "report.pdf", TestFiles.Pdf));
    }

    [Fact]
    public async Task StageAsync_FailedAttachmentsDoNotCountTowardTotal_Accepts()
    {
        var (senderId, emailId) = await AddDraftAsync(Attachment(MaxEmailBytes, AttachmentStatuses.Failed));
        await using var ctx = _fixture.CreateContext();

        var attachment = await StageAsync(ctx, emailId, senderId, "report.pdf", TestFiles.Pdf);

        Assert.Equal(AttachmentStatuses.Pending, attachment.Status);
    }

    [Fact]
    public async Task StageAsync_SentEmail_ThrowsNotEditable()
    {
        var sender = await _fixture.AddUserAsync("ada@contoso.com");
        var email = await _fixture.AddEmailAsync(sender.Id, EmailStatuses.Sent);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<EmailNotEditableException>(() => StageAsync(ctx, email.Id, sender.Id, "report.pdf", TestFiles.Pdf));
    }

    [Fact]
    public async Task StageAsync_AnotherUsersEmail_ThrowsNotFound()
    {
        var (_, emailId) = await AddDraftAsync();
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() => StageAsync(ctx, emailId, Guid.NewGuid(), "report.pdf", TestFiles.Pdf));
    }

    [Fact]
    public async Task OpenContentAsync_StillPending_ThrowsNotReady()
    {
        var attachment = Attachment(10, AttachmentStatuses.Pending);
        var (senderId, emailId) = await AddDraftAsync(attachment);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<AttachmentNotReadyException>(() =>
            CreateService(ctx).OpenContentAsync(emailId, attachment.Id, senderId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OpenContentAsync_BlobMissing_ThrowsNotFound()
    {
        var attachment = Attachment(10, AttachmentStatuses.Uploaded);
        var (senderId, emailId) = await AddDraftAsync(attachment);
        _store.OpenReadAsync(attachment.BlobName, Arg.Any<CancellationToken>()).ThrowsAsync(new BlobNotFoundException(attachment.BlobName));
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService(ctx).OpenContentAsync(emailId, attachment.Id, senderId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_Uploaded_DeactivatesRowAndDeletesBlob()
    {
        var attachment = Attachment(10, AttachmentStatuses.Uploaded);
        var (senderId, emailId) = await AddDraftAsync(attachment);
        await using var ctx = _fixture.CreateContext();

        await CreateService(ctx).DeleteAsync(emailId, attachment.Id, senderId, TestContext.Current.CancellationToken);

        // Before hiding the row, and again after, for an upload that finished in between.
        await _store.Received(2).DeleteIfExistsAsync(attachment.BlobName, Arg.Any<CancellationToken>());
        await using var readCtx = _fixture.CreateContext();
        var row = await readCtx.EmailAttachments.IgnoreQueryFilters().SingleAsync(a => a.Id == attachment.Id, TestContext.Current.CancellationToken);
        Assert.Equal(_fixture.Time.GetUtcNow(), row.DateDeactivated);
    }

    [Fact]
    public async Task DeleteAsync_StorageFails_LeavesRowVisibleSoTheRemovalCanBeRetried()
    {
        var attachment = Attachment(10, AttachmentStatuses.Uploaded);
        var (senderId, emailId) = await AddDraftAsync(attachment);
        _store.DeleteIfExistsAsync(attachment.BlobName, Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("storage unreachable"));
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<IOException>(() => CreateService(ctx).DeleteAsync(emailId, attachment.Id, senderId, TestContext.Current.CancellationToken));

        await using var readCtx = _fixture.CreateContext();
        Assert.True(await readCtx.EmailAttachments.AnyAsync(a => a.Id == attachment.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_CleanupAfterRemovalFails_StillSucceeds()
    {
        var attachment = Attachment(10, AttachmentStatuses.Uploaded);
        var (senderId, emailId) = await AddDraftAsync(attachment);
        _store.DeleteIfExistsAsync(attachment.BlobName, Arg.Any<CancellationToken>())
            .Returns(_ => Task.CompletedTask, _ => throw new IOException("storage unreachable"));
        await using var ctx = _fixture.CreateContext();

        await CreateService(ctx).DeleteAsync(emailId, attachment.Id, senderId, TestContext.Current.CancellationToken);

        await using var readCtx = _fixture.CreateContext();
        Assert.False(await readCtx.EmailAttachments.AnyAsync(a => a.Id == attachment.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StageAsync_ParallelUploadsOverEmailCap_AcceptsOnlyThoseThatFit()
    {
        var (senderId, emailId) = await AddDraftAsync();
        byte[] content = [.. TestFiles.Pdf, .. new byte[MaxFileBytes - TestFiles.Pdf.Length]];

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(async i =>
        {
            await using var ctx = _fixture.CreateContext();
            try
            {
                await StageAsync(ctx, emailId, senderId, $"part{i}.pdf", content);
                return true;
            }
            catch (ValidationException)
            {
                return false;
            }
        }));

        Assert.Single(results, accepted => accepted);
        await using var readCtx = _fixture.CreateContext();
        Assert.Equal(MaxFileBytes, await readCtx.EmailAttachments.Where(a => a.EmailId == emailId).SumAsync(a => a.SizeBytes, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StageAsync_CancelledMidCopy_LeavesNoStagedFile()
    {
        var (senderId, emailId) = await AddDraftAsync();
        using var cancellation = new CancellationTokenSource();
        await using var ctx = _fixture.CreateContext();
        await using var content = new CancellingStream(TestFiles.Pdf, cancellation);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateService(ctx).StageAsync(emailId, senderId, "report.pdf", TestFiles.Pdf.Length, content, cancellation.Token));

        Assert.Empty(_staging.ListStagedBefore(DateTimeOffset.MaxValue));
    }

    public void Dispose()
    {
        _quotaLock.Dispose();
        _fixture.Dispose();
        _directory.Dispose();
    }

    #region Private methods

    private static EmailAttachment Attachment(long size, AttachmentStatuses status)
    {
        var id = Guid.NewGuid();

        return new EmailAttachment
        {
            Id = id,
            FileName = "existing.pdf",
            DocumentTypeId = DocumentTypes.Pdf,
            ContentType = "application/pdf",
            SizeBytes = size,
            BlobName = $"email/{id:N}",
            StatusId = status,
        };
    }

    private async Task<(Guid SenderId, Guid EmailId)> AddDraftAsync(params EmailAttachment[] attachments)
    {
        var sender = await _fixture.AddUserAsync($"{Guid.NewGuid():N}@contoso.com");
        var email = await _fixture.AddEmailAsync(sender.Id, EmailStatuses.Draft, attachments);

        return (sender.Id, email.Id);
    }

    private AttachmentService CreateService(AndesMailerDbContext ctx) => new(ctx, _store, _staging, _channel, _quotaLock, _fixture.Time, _options, NullLogger<AttachmentService>.Instance);

    private async Task<EmailAttachmentDto> StageAsync(AndesMailerDbContext ctx, Guid emailId, Guid callerId, string fileName, byte[] content)
    {
        using var stream = new MemoryStream(content);

        return await CreateService(ctx).StageAsync(emailId, callerId, fileName, content.Length, stream, TestContext.Current.CancellationToken);
    }

    private async Task<Guid> FirstQueuedAsync()
    {
        await foreach (var id in _channel.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            return id;
        }

        throw new InvalidOperationException("Nothing was queued.");
    }

    #endregion
}
