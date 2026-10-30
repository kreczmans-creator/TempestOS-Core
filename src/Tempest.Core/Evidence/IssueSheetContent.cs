namespace Tempest.Core.Evidence;

/// <summary>The rendered issue sheet handed to <see cref="IEvidenceService.IssueInOneTransactionAsync"/>: rendered before the transaction, written inside it.</summary>
/// <param name="AttachmentId">The id the attachment will carry, when the caller needs to know it before the commit (a snapshot written in the same transaction); a fresh id otherwise.</param>
public sealed record IssueSheetContent(string FileName, string ContentType, ReadOnlyMemory<byte> Content, Guid? AttachmentId = null);
