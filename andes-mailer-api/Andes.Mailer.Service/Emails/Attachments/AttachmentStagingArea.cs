using Andes.Mailer.Service.Options;

using Microsoft.Extensions.Options;

namespace Andes.Mailer.Service.Emails.Attachments;

/// <summary>
/// Holds received attachment files on local disk until the background upload stores them.
/// </summary>
/// <remarks>
/// Local disk ties a pending upload to this instance, which is safe only while the API runs as a single instance.
/// </remarks>
/// <param name="options">The upload settings that name the staging directory.</param>
public sealed class AttachmentStagingArea(IOptions<AttachmentUploadOptions> options)
{
    private readonly string _directory = options.Value.ResolvedStagingPath;

    /// <summary>
    /// Writes a received file to the staging area.
    /// </summary>
    /// <param name="attachmentId">The id of the attachment the file belongs to.</param>
    /// <param name="content">The file, read from its current position to the end.</param>
    /// <param name="cancellationToken">A token that cancels the copy.</param>
    /// <returns>The number of bytes written.</returns>
    public async Task<long> WriteAsync(Guid attachmentId, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        Directory.CreateDirectory(_directory);
        await using var file = new FileStream(PathOf(attachmentId), FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);

        return file.Length;
    }

    /// <summary>
    /// Reads the leading bytes of a staged file.
    /// </summary>
    /// <param name="attachmentId">The id of the attachment the file belongs to.</param>
    /// <param name="count">The number of bytes to read.</param>
    /// <param name="cancellationToken">A token that cancels the read.</param>
    /// <returns>The bytes, fewer than <paramref name="count"/> if the file is shorter.</returns>
    public async Task<byte[]> ReadHeaderAsync(Guid attachmentId, int count, CancellationToken cancellationToken)
    {
        await using var file = OpenRead(attachmentId);
        var buffer = new byte[count];
        var read = await file.ReadAtLeastAsync(buffer, count, throwOnEndOfStream: false, cancellationToken);

        return buffer[..read];
    }

    /// <summary>
    /// Opens a staged file for reading.
    /// </summary>
    /// <param name="attachmentId">The id of the attachment the file belongs to.</param>
    /// <returns>A stream over the file; the caller disposes it.</returns>
    public Stream OpenRead(Guid attachmentId) =>
        new FileStream(PathOf(attachmentId), FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);

    /// <summary>
    /// Determines whether a staged file exists.
    /// </summary>
    /// <param name="attachmentId">The id of the attachment the file belongs to.</param>
    /// <returns><see langword="true"/> if the file is staged; otherwise, <see langword="false"/>.</returns>
    public bool Exists(Guid attachmentId) => File.Exists(PathOf(attachmentId));

    /// <summary>
    /// Deletes a staged file if it exists.
    /// </summary>
    /// <param name="attachmentId">The id of the attachment the file belongs to.</param>
    public void Delete(Guid attachmentId) => File.Delete(PathOf(attachmentId));

    /// <summary>
    /// Lists the attachments whose file was staged before a moment.
    /// </summary>
    /// <param name="before">The moment; files written at or after it are left out, so a request still staging is never disturbed.</param>
    /// <returns>The attachment ids; files whose name is not an id are skipped.</returns>
    public IReadOnlyList<Guid> ListStagedBefore(DateTimeOffset before)
    {
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        return
        [
            .. new DirectoryInfo(_directory)
                .EnumerateFiles()
                .Where(f => f.LastWriteTimeUtc < before.UtcDateTime)
                .Select(f => Guid.TryParseExact(f.Name, "N", out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty),
        ];
    }

    #region Private methods

    // The id alone, never the client's file name, so nothing the client sends can steer the path.
    private string PathOf(Guid attachmentId) => Path.Combine(_directory, attachmentId.ToString("N"));

    #endregion
}
