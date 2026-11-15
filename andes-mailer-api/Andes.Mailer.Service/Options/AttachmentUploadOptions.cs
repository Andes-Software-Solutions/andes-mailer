using FluentValidation;

namespace Andes.Mailer.Service.Options;

/// <summary>
/// Holds the limits and the staging settings of attachment uploads.
/// </summary>
public sealed class AttachmentUploadOptions
{
    /// <summary>
    /// The configuration section these options bind to.
    /// </summary>
    public const string SectionName = "AttachmentUpload";

    private const long Megabyte = 1024 * 1024;

    /// <summary>
    /// Gets the directory that holds received files until the background upload stores them.
    /// </summary>
    /// <value>An absolute path, or empty for <c>andes-mailer/attachments</c> under the system temporary directory.</value>
    public string StagingPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the largest file accepted, in bytes. The default is 25 MB.
    /// </summary>
    public long MaxFileBytes { get; init; } = 25 * Megabyte;

    /// <summary>
    /// Gets the largest total of all of an email's attachments, in bytes. The default is 25 MB.
    /// </summary>
    public long MaxEmailBytes { get; init; } = 25 * Megabyte;

    /// <summary>
    /// Gets the number of uploads that run at once. The default is 4.
    /// </summary>
    public int MaxConcurrency { get; init; } = 4;

    /// <summary>
    /// Gets the number of received files that can wait for an upload before new requests wait too. The default is 256.
    /// </summary>
    public int QueueCapacity { get; init; } = 256;

    /// <summary>
    /// Gets the number of times an upload is tried before it is marked failed. The default is 6.
    /// </summary>
    /// <remarks>
    /// Retries wait 2 seconds, then twice as long each time up to 1 minute, so the default covers about a minute of storage being
    /// unreachable, including the moments after startup before the container exists.
    /// </remarks>
    public int MaxUploadAttempts { get; init; } = 6;

    /// <summary>
    /// Gets a value indicating whether the processor settles uploads the last shutdown interrupted when it starts. The default is <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Recovery assumes this instance staged every pending upload, so it fails any whose file it cannot find. Turn it off for any instance
    /// that shares the database with another one, or it fails the other instance's uploads in flight.
    /// </remarks>
    public bool RecoverOnStartup { get; init; } = true;

    /// <summary>
    /// Gets the staging directory with the default applied.
    /// </summary>
    public string ResolvedStagingPath =>
        string.IsNullOrWhiteSpace(StagingPath) ? Path.Join(Path.GetTempPath(), "andes-mailer", "attachments") : StagingPath;
}

/// <summary>
/// Validates <see cref="AttachmentUploadOptions"/>. Public because the Api composition root registers it.
/// </summary>
public sealed class AttachmentUploadOptionsValidator : AbstractValidator<AttachmentUploadOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AttachmentUploadOptionsValidator"/> class.
    /// </summary>
    public AttachmentUploadOptionsValidator()
    {
        RuleFor(o => o.StagingPath)
            .Must(p => string.IsNullOrWhiteSpace(p) || Path.IsPathFullyQualified(p))
            .WithMessage("'StagingPath' must be empty or an absolute path.");
        RuleFor(o => o.MaxFileBytes).GreaterThan(0);
        RuleFor(o => o.MaxEmailBytes).GreaterThanOrEqualTo(o => o.MaxFileBytes);
        RuleFor(o => o.MaxConcurrency).InclusiveBetween(1, 64);
        RuleFor(o => o.QueueCapacity).InclusiveBetween(1, 10_000);
        RuleFor(o => o.MaxUploadAttempts).InclusiveBetween(1, 20);
    }
}
