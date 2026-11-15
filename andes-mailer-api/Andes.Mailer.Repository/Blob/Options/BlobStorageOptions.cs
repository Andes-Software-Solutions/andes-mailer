using FluentValidation;

namespace Andes.Mailer.Repository.Blob.Options;

/// <summary>
/// Holds the settings of the Azure Blob Storage account.
/// </summary>
public sealed class BlobStorageOptions
{
    /// <summary>
    /// The configuration section these options bind to.
    /// </summary>
    public const string SectionName = "BlobStorage";

    /// <summary>
    /// Gets the connection string of the storage account.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Gets the name of the container that holds email attachments. The default is <c>attachments</c>.
    /// </summary>
    public string AttachmentsContainer { get; init; } = "attachments";
}

internal sealed class BlobStorageOptionsValidator : AbstractValidator<BlobStorageOptions>
{
    public BlobStorageOptionsValidator()
    {
        RuleFor(o => o.ConnectionString).NotEmpty();

        // The service's container naming rule, checked here so a bad name fails at startup rather than on the first upload.
        RuleFor(o => o.AttachmentsContainer)
            .NotEmpty()
            .Length(3, 63)
            .Matches("^[a-z0-9](?!.*--)[a-z0-9-]*[a-z0-9]$")
            .WithMessage("'AttachmentsContainer' must be 3 to 63 lowercase letters, digits, or single hyphens, starting and ending with a letter or digit.");
    }
}
