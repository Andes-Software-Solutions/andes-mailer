using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Entity.Emails.Templates;
using Andes.Mailer.Repository.Blob;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Emails.Attachments;
using Andes.Mailer.Service.Emails.Attachments.Models;
using Andes.Mailer.Service.Options;
using Andes.Mailer.Unit.Test.TestInfrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Andes.Mailer.Unit.Test.Service.Emails.Attachments;

public sealed class AttachmentUploadProcessorTests : IAsyncDisposable
{
    private readonly MailerDbFixture _fixture = new(concurrent: true);
    private readonly TempDirectory _directory = new();
    private readonly IAttachmentStore _store = Substitute.For<IAttachmentStore>();
    private readonly ServiceProvider _services;
    private readonly IOptions<AttachmentUploadOptions> _options;
    private readonly AttachmentStagingArea _staging;
    private readonly AttachmentUploadChannel _channel;
    private readonly List<byte[]> _uploaded = [];

    public AttachmentUploadProcessorTests()
    {
        _services = new ServiceCollection().AddScoped(_ => _fixture.CreateContext()).BuildServiceProvider();
        _options = Microsoft.Extensions.Options.Options.Create(new AttachmentUploadOptions { StagingPath = _directory.Path, MaxConcurrency = 2 });
        _staging = new AttachmentStagingArea(_options);
        _channel = new AttachmentUploadChannel(_options);
        _store.UploadAsync(Arg.Any<string>(), Arg.Do<Stream>(s => _uploaded.Add(ReadAll(s))), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Recovery compares staged files' real write times with the processor's clock, so the clock starts at the real present.
        _fixture.Time.SetUtcNow(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Process_QueuedAttachment_UploadsStagedFileAndMarksUploaded()
    {
        var attachment = await AddPendingAsync(staged: true);
        using var processor = CreateProcessor();
        await processor.StartAsync(TestContext.Current.CancellationToken);

        await _channel.EnqueueAsync(new StagedAttachmentJob(AttachmentOwners.Email, attachment.Id), TestContext.Current.CancellationToken);
        var settled = await WaitForStatusAsync(attachment.Id, AttachmentStatuses.Uploaded);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AttachmentStatuses.Uploaded, settled.StatusId);
        Assert.Equal([TestFiles.Pdf], _uploaded);
        await _store.Received(1).UploadAsync(attachment.BlobName, Arg.Any<Stream>(), "application/pdf", Arg.Any<CancellationToken>());
        Assert.False(_staging.Exists(attachment.Id));
    }

    [Fact]
    public async Task Process_StoreFailsEveryAttempt_MarksFailedCleansUpAndKeepsProcessing()
    {
        var failing = await AddPendingAsync(staged: true);
        var next = await AddPendingAsync(staged: true);
        _store.UploadAsync(failing.BlobName, Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("storage unreachable"));
        using var processor = CreateProcessor(maxUploadAttempts: 2);
        await processor.StartAsync(TestContext.Current.CancellationToken);

        await _channel.EnqueueAsync(new StagedAttachmentJob(AttachmentOwners.Email, failing.Id), TestContext.Current.CancellationToken);
        await _channel.EnqueueAsync(new StagedAttachmentJob(AttachmentOwners.Email, next.Id), TestContext.Current.CancellationToken);
        var failed = await WaitForStatusAsync(failing.Id, AttachmentStatuses.Failed);
        var uploaded = await WaitForStatusAsync(next.Id, AttachmentStatuses.Uploaded);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(failed.FailureReason);
        Assert.DoesNotContain("storage unreachable", failed.FailureReason, StringComparison.Ordinal);
        Assert.Equal(AttachmentStatuses.Uploaded, uploaded.StatusId);
        Assert.False(_staging.Exists(failing.Id));
        await _store.Received(2).UploadAsync(failing.BlobName, Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());

        // The write may have landed even though it reported a failure, so the blob is removed too.
        await _store.Received(1).DeleteIfExistsAsync(failing.BlobName, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Process_TransientStoreFailure_KeepsStagedFileAndRetriesToUploaded()
    {
        var attachment = await AddPendingAsync(staged: true);
        _store.UploadAsync(attachment.BlobName, Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new IOException("container not found yet"), _ => Task.CompletedTask);
        using var processor = CreateProcessor();
        await processor.StartAsync(TestContext.Current.CancellationToken);

        await _channel.EnqueueAsync(new StagedAttachmentJob(AttachmentOwners.Email, attachment.Id), TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => _store.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(IAttachmentStore.UploadAsync)));
        var stagedAfterFailure = _staging.Exists(attachment.Id);
        var uploaded = await WaitForStatusAsync(attachment.Id, AttachmentStatuses.Uploaded);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(stagedAfterFailure);
        Assert.Equal(AttachmentStatuses.Uploaded, uploaded.StatusId);
        Assert.False(_staging.Exists(attachment.Id));
        await _store.DidNotReceive().DeleteIfExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Process_DatabaseFaultBeforeUpload_RetriesToUploaded()
    {
        var attachment = await AddPendingAsync(staged: true);
        var calls = 0;
        await using var flaky = new ServiceCollection()
            .AddScoped(_ => Interlocked.Increment(ref calls) == 1 ? throw new InvalidOperationException("database unavailable") : _fixture.CreateContext())
            .BuildServiceProvider();
        var options = Microsoft.Extensions.Options.Options.Create(new AttachmentUploadOptions { StagingPath = _directory.Path, MaxConcurrency = 1, RecoverOnStartup = false });
        using var processor = new AttachmentUploadProcessor(flaky.GetRequiredService<IServiceScopeFactory>(), _channel, _staging, _store, _fixture.Time, options, NullLogger<AttachmentUploadProcessor>.Instance);
        await processor.StartAsync(TestContext.Current.CancellationToken);

        await _channel.EnqueueAsync(new StagedAttachmentJob(AttachmentOwners.Email, attachment.Id), TestContext.Current.CancellationToken);
        var uploaded = await WaitForStatusAsync(attachment.Id, AttachmentStatuses.Uploaded);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AttachmentStatuses.Uploaded, uploaded.StatusId);
        Assert.True(calls >= 2);
    }

    [Fact]
    public async Task Process_StagedFileMissing_FailsWithoutRetry()
    {
        var attachment = await AddPendingAsync(staged: false);
        using var processor = CreateProcessor();
        await processor.StartAsync(TestContext.Current.CancellationToken);

        await _channel.EnqueueAsync(new StagedAttachmentJob(AttachmentOwners.Email, attachment.Id), TestContext.Current.CancellationToken);
        var failed = await WaitForStatusAsync(attachment.Id, AttachmentStatuses.Failed);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AttachmentStatuses.Failed, failed.StatusId);
        await _store.DidNotReceive().UploadAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Process_SameIdQueuedTwice_UploadsOnce()
    {
        var attachment = await AddPendingAsync(staged: true);
        var uploadStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseUpload = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _store.UploadAsync(attachment.BlobName, Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                uploadStarted.TrySetResult();
                return releaseUpload.Task;
            });
        using var processor = CreateProcessor();
        await processor.StartAsync(TestContext.Current.CancellationToken);

        await _channel.EnqueueAsync(new StagedAttachmentJob(AttachmentOwners.Email, attachment.Id), TestContext.Current.CancellationToken);
        await uploadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await _channel.EnqueueAsync(new StagedAttachmentJob(AttachmentOwners.Email, attachment.Id), TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        releaseUpload.SetResult();
        var uploaded = await WaitForStatusAsync(attachment.Id, AttachmentStatuses.Uploaded);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AttachmentStatuses.Uploaded, uploaded.StatusId);
        await _store.Received(1).UploadAsync(attachment.BlobName, Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Process_RemovedBeforeUpload_SkipsItAndDeletesStagedFile()
    {
        var attachment = await AddPendingAsync(staged: true);
        await using (var ctx = _fixture.CreateContext())
        {
            ctx.Remove(await ctx.EmailAttachments.SingleAsync(a => a.Id == attachment.Id, TestContext.Current.CancellationToken));
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var processor = CreateProcessor();
        await processor.StartAsync(TestContext.Current.CancellationToken);
        await _channel.EnqueueAsync(new StagedAttachmentJob(AttachmentOwners.Email, attachment.Id), TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => !_staging.Exists(attachment.Id));
        await processor.StopAsync(TestContext.Current.CancellationToken);

        await _store.DidNotReceive().UploadAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Start_InterruptedUploads_RequeuesStagedOnesAndFailsTheRest()
    {
        var staged = await AddPendingAsync(staged: true);
        var lost = await AddPendingAsync(staged: false);
        var orphan = Guid.NewGuid();
        await using (var file = new MemoryStream(TestFiles.Pdf))
        {
            await _staging.WriteAsync(orphan, file, TestContext.Current.CancellationToken);
        }

        _fixture.Time.Advance(TimeSpan.FromMinutes(1));
        using var processor = CreateProcessor();

        await processor.StartAsync(TestContext.Current.CancellationToken);
        var uploaded = await WaitForStatusAsync(staged.Id, AttachmentStatuses.Uploaded);
        var failed = await WaitForStatusAsync(lost.Id, AttachmentStatuses.Failed);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AttachmentStatuses.Uploaded, uploaded.StatusId);
        Assert.Equal(AttachmentStatuses.Failed, failed.StatusId);
        Assert.False(_staging.Exists(orphan));
    }

    [Fact]
    public async Task Start_RecoveryOff_LeavesInterruptedUploadsAlone()
    {
        var lost = await AddPendingAsync(staged: false);
        _fixture.Time.Advance(TimeSpan.FromMinutes(1));
        var options = Microsoft.Extensions.Options.Options.Create(new AttachmentUploadOptions { StagingPath = _directory.Path, RecoverOnStartup = false });
        using var processor = new AttachmentUploadProcessor(_services.GetRequiredService<IServiceScopeFactory>(), _channel, _staging, _store, _fixture.Time, options, NullLogger<AttachmentUploadProcessor>.Instance);

        await processor.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        await using var ctx = _fixture.CreateContext();
        Assert.Equal(AttachmentStatuses.Pending, (await ctx.EmailAttachments.SingleAsync(a => a.Id == lost.Id, TestContext.Current.CancellationToken)).StatusId);
    }

    [Fact]
    public async Task Process_QueuedTemplateAttachment_UploadsStagedFileAndMarksUploaded()
    {
        var attachment = await AddPendingTemplateAttachmentAsync(staged: true);
        using var processor = CreateProcessor();
        await processor.StartAsync(TestContext.Current.CancellationToken);

        await _channel.EnqueueAsync(new StagedAttachmentJob(AttachmentOwners.Template, attachment.Id), TestContext.Current.CancellationToken);
        var settled = await WaitForTemplateStatusAsync(attachment.Id, AttachmentStatuses.Uploaded);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AttachmentStatuses.Uploaded, settled.StatusId);
        Assert.Equal([TestFiles.Pdf], _uploaded);
        await _store.Received(1).UploadAsync(attachment.BlobName, Arg.Any<Stream>(), "application/pdf", Arg.Any<CancellationToken>());
        Assert.False(_staging.Exists(attachment.Id));
    }

    [Fact]
    public async Task Start_InterruptedTemplateUploads_RequeuesStagedOnesAndFailsLostOnes()
    {
        var staged = await AddPendingTemplateAttachmentAsync(staged: true);
        var lost = await AddPendingTemplateAttachmentAsync(staged: false);
        _fixture.Time.Advance(TimeSpan.FromMinutes(1));
        using var processor = CreateProcessor();

        await processor.StartAsync(TestContext.Current.CancellationToken);
        var uploaded = await WaitForTemplateStatusAsync(staged.Id, AttachmentStatuses.Uploaded);
        var failed = await WaitForTemplateStatusAsync(lost.Id, AttachmentStatuses.Failed);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AttachmentStatuses.Uploaded, uploaded.StatusId);
        Assert.Equal(AttachmentStatuses.Failed, failed.StatusId);
        Assert.NotNull(failed.FailureReason);
    }

    [Fact]
    public async Task Process_TemplateStoreFailsEveryAttempt_MarksFailedAndDeletesTheBlob()
    {
        var attachment = await AddPendingTemplateAttachmentAsync(staged: true);
        _store.UploadAsync(attachment.BlobName, Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("storage unreachable"));
        using var processor = CreateProcessor(maxUploadAttempts: 2);
        await processor.StartAsync(TestContext.Current.CancellationToken);

        await _channel.EnqueueAsync(new StagedAttachmentJob(AttachmentOwners.Template, attachment.Id), TestContext.Current.CancellationToken);
        var failed = await WaitForTemplateStatusAsync(attachment.Id, AttachmentStatuses.Failed);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(failed.FailureReason);
        Assert.False(_staging.Exists(attachment.Id));
        await _store.Received(2).UploadAsync(attachment.BlobName, Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _store.Received(1).DeleteIfExistsAsync(attachment.BlobName, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Process_TemplateDeactivatedBeforeUpload_SkipsItAndDeletesStagedFile()
    {
        var attachment = await AddPendingTemplateAttachmentAsync(staged: true);
        await using (var ctx = _fixture.CreateContext())
        {
            ctx.Remove(await ctx.EmailTemplates.SingleAsync(t => t.Id == attachment.TemplateId, TestContext.Current.CancellationToken));
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var processor = CreateProcessor();
        await processor.StartAsync(TestContext.Current.CancellationToken);
        await _channel.EnqueueAsync(new StagedAttachmentJob(AttachmentOwners.Template, attachment.Id), TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => !_staging.Exists(attachment.Id));
        await processor.StopAsync(TestContext.Current.CancellationToken);

        Assert.False(_staging.Exists(attachment.Id));
        await _store.DidNotReceive().UploadAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        _fixture.Dispose();
        _directory.Dispose();
    }

    #region Private methods

    private static byte[] ReadAll(Stream stream)
    {
        using var copy = new MemoryStream();
        stream.CopyTo(copy);

        return copy.ToArray();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 200 && !condition(); attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(25), TestContext.Current.CancellationToken);
        }
    }

    private AttachmentUploadProcessor CreateProcessor(int? maxUploadAttempts = null)
    {
        var options = maxUploadAttempts is { } attempts
            ? Microsoft.Extensions.Options.Options.Create(new AttachmentUploadOptions { StagingPath = _directory.Path, MaxConcurrency = 2, MaxUploadAttempts = attempts })
            : _options;

        return new(_services.GetRequiredService<IServiceScopeFactory>(), _channel, _staging, _store, _fixture.Time, options, NullLogger<AttachmentUploadProcessor>.Instance);
    }

    private async Task<EmailAttachment> AddPendingAsync(bool staged)
    {
        var sender = await _fixture.AddUserAsync($"{Guid.NewGuid():N}@contoso.com");
        var id = Guid.NewGuid();
        var attachment = new EmailAttachment
        {
            Id = id,
            FileName = "report.pdf",
            DocumentTypeId = DocumentTypes.Pdf,
            ContentType = "application/pdf",
            SizeBytes = TestFiles.Pdf.Length,
            BlobName = $"email/{id:N}",
            StatusId = AttachmentStatuses.Pending,
        };
        var team = await _fixture.AddTeamAsync();
        await _fixture.AddEmailAsync(team.Id, sender.Id, EmailStatuses.Draft, attachment);
        if (staged)
        {
            await using var file = new MemoryStream(TestFiles.Pdf);
            await _staging.WriteAsync(id, file, TestContext.Current.CancellationToken);
        }

        return attachment;
    }

    private async Task<EmailTemplateAttachment> AddPendingTemplateAttachmentAsync(bool staged)
    {
        var admin = await _fixture.AddUserAsync($"{Guid.NewGuid():N}@contoso.com");
        var id = Guid.NewGuid();
        var attachment = new EmailTemplateAttachment
        {
            Id = id,
            FileName = "report.pdf",
            DocumentTypeId = DocumentTypes.Pdf,
            ContentType = "application/pdf",
            SizeBytes = TestFiles.Pdf.Length,
            BlobName = $"templates/{id:N}",
            StatusId = AttachmentStatuses.Pending,
        };
        var team = await _fixture.AddTeamAsync();
        await _fixture.AddTemplateAsync(team.Id, admin.Id, null, attachment);
        if (staged)
        {
            await using var file = new MemoryStream(TestFiles.Pdf);
            await _staging.WriteAsync(id, file, TestContext.Current.CancellationToken);
        }

        return attachment;
    }

    private async Task<EmailTemplateAttachment> WaitForTemplateStatusAsync(Guid id, AttachmentStatuses status)
    {
        EmailTemplateAttachment? attachment = null;
        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var ctx = _fixture.CreateContext();
            attachment = await ctx.EmailTemplateAttachments.AsNoTracking().SingleAsync(a => a.Id == id, TestContext.Current.CancellationToken);
            if (attachment.StatusId == status)
            {
                return attachment;
            }

            _fixture.Time.Advance(TimeSpan.FromSeconds(5));
            await Task.Delay(TimeSpan.FromMilliseconds(25), TestContext.Current.CancellationToken);
        }

        return attachment!;
    }

    private async Task<EmailAttachment> WaitForStatusAsync(Guid id, AttachmentStatuses status)
    {
        EmailAttachment? attachment = null;
        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var ctx = _fixture.CreateContext();
            attachment = await ctx.EmailAttachments.AsNoTracking().SingleAsync(a => a.Id == id, TestContext.Current.CancellationToken);
            if (attachment.StatusId == status)
            {
                return attachment;
            }

            // Retries wait on the fake clock, so time moves only as the test polls.
            _fixture.Time.Advance(TimeSpan.FromSeconds(5));
            await Task.Delay(TimeSpan.FromMilliseconds(25), TestContext.Current.CancellationToken);
        }

        return attachment!;
    }

    #endregion
}
