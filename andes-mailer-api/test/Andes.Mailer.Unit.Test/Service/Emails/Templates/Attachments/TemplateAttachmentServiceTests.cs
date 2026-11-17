using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Emails;
using Andes.Mailer.Entity.Emails.Templates;
using Andes.Mailer.Repository.Blob;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Emails.Attachments;
using Andes.Mailer.Service.Emails.Attachments.Exceptions;
using Andes.Mailer.Service.Emails.Attachments.Models;
using Andes.Mailer.Service.Emails.Templates.Attachments;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Service.Options;
using Andes.Mailer.Unit.Test.TestInfrastructure;

using FluentValidation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Andes.Mailer.Unit.Test.Service.Emails.Templates.Attachments;

public sealed class TemplateAttachmentServiceTests : IDisposable
{
    private const long MaxFileBytes = 64;
    private const long MaxEmailBytes = 40;

    private readonly MailerDbFixture _fixture = new();
    private readonly TempDirectory _directory = new();
    private readonly IAttachmentStore _store = Substitute.For<IAttachmentStore>();
    private readonly AttachmentQuotaLock _quotaLock = new();
    private readonly IOptions<AttachmentUploadOptions> _options;
    private readonly AttachmentStagingArea _staging;
    private readonly AttachmentUploadChannel _channel;

    public TemplateAttachmentServiceTests()
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
    public async Task StageAsync_Pdf_StagesFileRecordsPendingRowUnderTheTemplatePrefixAndQueuesIt()
    {
        var (teamId, templateId) = await AddTemplateAsync();
        await using var ctx = _fixture.CreateContext();

        var attachment = await StageAsync(ctx, teamId, templateId, "report.pdf", TestFiles.Pdf);

        Assert.Equal(AttachmentStatuses.Pending, attachment.Status);
        Assert.True(_staging.Exists(attachment.Id));
        Assert.Equal(new StagedAttachmentJob(AttachmentOwners.Template, attachment.Id), await FirstQueuedAsync());
        await using var readCtx = _fixture.CreateContext();
        var row = await readCtx.EmailTemplateAttachments.SingleAsync(a => a.Id == attachment.Id, TestContext.Current.CancellationToken);
        Assert.Equal($"templates/{templateId:N}/{attachment.Id:N}", row.BlobName);
    }

    [Fact]
    public async Task StageAsync_OverTheTemplatesTotal_ThrowsValidationAndStagesNothing()
    {
        var (teamId, templateId) = await AddTemplateAsync(NewAttachment(AttachmentStatuses.Uploaded, sizeBytes: MaxEmailBytes - 4));
        await using var ctx = _fixture.CreateContext();

        var error = await Assert.ThrowsAsync<ValidationException>(() => StageAsync(ctx, teamId, templateId, "report.pdf", TestFiles.Pdf));

        Assert.Contains(error.Errors, e => e.ErrorMessage.Contains("template's attachments", StringComparison.Ordinal));
        Assert.Empty(_staging.ListStagedBefore(DateTimeOffset.MaxValue));
    }

    [Fact]
    public async Task StageAsync_AnotherTeamsTemplate_ThrowsNotFound()
    {
        var (_, templateId) = await AddTemplateAsync();
        var otherTeam = await _fixture.AddTeamAsync();
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() => StageAsync(ctx, otherTeam.Id, templateId, "report.pdf", TestFiles.Pdf));
    }

    [Fact]
    public async Task OpenContentAsync_StillUploading_ThrowsNotReady()
    {
        var pending = NewAttachment(AttachmentStatuses.Pending);
        var (teamId, templateId) = await AddTemplateAsync(pending);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<AttachmentNotReadyException>(() =>
            CreateService(ctx).OpenContentAsync(teamId, templateId, pending.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OpenContentAsync_Stored_OpensTheTemplatesBlob()
    {
        var stored = NewAttachment(AttachmentStatuses.Uploaded);
        var (teamId, templateId) = await AddTemplateAsync(stored);
        await using var document = new MemoryStream(TestFiles.Pdf);
        _store.OpenReadAsync(stored.BlobName, Arg.Any<CancellationToken>()).Returns(document);
        await using var ctx = _fixture.CreateContext();

        var content = await CreateService(ctx).OpenContentAsync(teamId, templateId, stored.Id, TestContext.Current.CancellationToken);

        Assert.Same(document, content.Content);
        Assert.Equal(stored.FileName, content.FileName);
        Assert.Equal("application/pdf", content.ContentType);
    }

    [Fact]
    public async Task DeleteAsync_Stored_DeletesTheBlobAndDeactivatesTheRow()
    {
        var stored = NewAttachment(AttachmentStatuses.Uploaded);
        var (teamId, templateId) = await AddTemplateAsync(stored);
        await using var ctx = _fixture.CreateContext();

        await CreateService(ctx).DeleteAsync(teamId, templateId, stored.Id, TestContext.Current.CancellationToken);

        await _store.Received().DeleteIfExistsAsync(stored.BlobName, Arg.Any<CancellationToken>());
        await using var readCtx = _fixture.CreateContext();
        var row = await readCtx.EmailTemplateAttachments.IgnoreQueryFilters().SingleAsync(a => a.Id == stored.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(row.DateDeactivated);
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService(readCtx).GetAsync(teamId, templateId, stored.Id, TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        _quotaLock.Dispose();
        _fixture.Dispose();
        _directory.Dispose();
    }

    #region Private methods

    private static EmailTemplateAttachment NewAttachment(AttachmentStatuses status, long? sizeBytes = null)
    {
        var id = Guid.CreateVersion7();

        return new EmailTemplateAttachment
        {
            Id = id,
            FileName = "stored.pdf",
            DocumentTypeId = DocumentTypes.Pdf,
            ContentType = "application/pdf",
            SizeBytes = sizeBytes ?? TestFiles.Pdf.Length,
            BlobName = $"templates/{id:N}",
            StatusId = status,
        };
    }

    private async Task<(Guid TeamId, Guid TemplateId)> AddTemplateAsync(params EmailTemplateAttachment[] attachments)
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync($"{Guid.NewGuid():N}@contoso.com");
        var template = await _fixture.AddTemplateAsync(team.Id, admin.Id, null, attachments);

        return (team.Id, template.Id);
    }

    private TemplateAttachmentService CreateService(AndesMailerDbContext ctx) =>
        new(ctx, new AttachmentLifecycle(_store, _staging, _channel, _quotaLock, _fixture.Time, _options, NullLogger<AttachmentLifecycle>.Instance));

    private async Task<EmailAttachmentDto> StageAsync(AndesMailerDbContext ctx, Guid teamId, Guid templateId, string fileName, byte[] content)
    {
        using var stream = new MemoryStream(content);

        return await CreateService(ctx).StageAsync(teamId, templateId, fileName, content.Length, stream, TestContext.Current.CancellationToken);
    }

    private async Task<StagedAttachmentJob> FirstQueuedAsync()
    {
        await foreach (var job in _channel.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            return job;
        }

        throw new InvalidOperationException("Nothing was queued.");
    }

    #endregion
}
