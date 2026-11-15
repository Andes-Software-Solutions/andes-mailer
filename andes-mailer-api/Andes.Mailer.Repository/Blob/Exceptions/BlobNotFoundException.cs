namespace Andes.Mailer.Repository.Blob.Exceptions;

/// <summary>
/// The exception thrown when a blob that should exist is missing from its container.
/// </summary>
/// <param name="blobName">The name of the missing blob.</param>
public sealed class BlobNotFoundException(string blobName) : Exception($"The blob '{blobName}' does not exist.")
{
    /// <summary>
    /// Gets the name of the missing blob.
    /// </summary>
    public string BlobName { get; } = blobName;
}
