using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Service.Emails.Attachments.Models;

/// <summary>
/// Describes a received file that passed the name and type checks, for the caller to build its attachment row from.
/// </summary>
/// <param name="Id">The new attachment's id, which also names its staged file.</param>
/// <param name="FileName">The file name without any directory.</param>
/// <param name="DocumentType">One of the enumeration values that specifies the kind of document.</param>
/// <param name="ContentType">The media type the document is served with.</param>
/// <param name="SizeBytes">The size the request declared; replaced by what reached the disk once the file is staged.</param>
public sealed record StagedFile(Guid Id, string FileName, DocumentTypes DocumentType, string ContentType, long SizeBytes);
