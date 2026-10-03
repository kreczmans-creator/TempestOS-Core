using System.Globalization;
using Tempest.Core.Invoicing.Xero.Api;

namespace Tempest.Core.Invoicing.Xero.Sync;

/// <summary>
/// `v0.24.0` review M6: a file that cannot be attached — larger than
/// <see cref="XeroDocumentFile.MaximumSizeInBytes"/>, or refused by Xero
/// itself — never holds its record's queue. Every attachment handler
/// (quotes, invoices, purchase orders, bills) finishes such an upload as
/// <see cref="XeroPushOutcome.NothingToDo"/> with a reason, and records that
/// reason on the record's link as <see cref="XeroLink.AttachmentNote"/>, so
/// the badge still says the file is not in Xero while later edits to the
/// record go through. The same file is never queued again (the outbox
/// de-duplicates against the finished entry); a new file is, and a
/// successful upload clears the note.
/// </summary>
public static class XeroAttachmentRefusal
{
    /// <summary>The start of every <see cref="XeroLink.AttachmentNote"/>.</summary>
    public const string NotePrefix = "File not attached in Xero";

    /// <summary>Why <paramref name="file"/> is too large to attach, or <see langword="null"/> when it is not.</summary>
    /// <param name="file">The file.</param>
    public static string? TooLarge(XeroDocumentFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return file.Content.Length > XeroDocumentFile.MaximumSizeInBytes
            ? $"'{file.FileName}' is {Megabytes(file.Content.Length)}; TempestOS attaches files up to {Megabytes(XeroDocumentFile.MaximumSizeInBytes)}. Attach a smaller copy in TempestOS, or add it in Xero by hand"
            : null;
    }

    /// <summary>
    /// Whether a failed upload is a refusal of the file itself — Xero's (or
    /// the API client's own size check's) <see cref="ConnectorOutcome.Rejected"/>
    /// — rather than the record being gone in Xero (a 404) or a TempestOS
    /// safety rule (D3/D4/D7), which stay Failed for the operator to act on.
    /// Outages, lost answers and re-authorisation are never a refusal.
    /// </summary>
    /// <param name="outcome">The call's outcome.</param>
    /// <param name="notFound">Whether Xero answered 404.</param>
    /// <param name="reason">The call's reason.</param>
    public static bool IsFileRefusal(ConnectorOutcome outcome, bool notFound, string? reason) =>
        outcome == ConnectorOutcome.Rejected
        && !notFound
        && !(reason?.StartsWith(XeroAccountingApi.BlockedReasonPrefix, StringComparison.Ordinal) ?? false);

    /// <summary>The note saved on the link for a refusal: <see cref="NotePrefix"/>, then why.</summary>
    /// <param name="reason">Why.</param>
    public static string Note(string? reason) =>
        $"{NotePrefix}: {(string.IsNullOrWhiteSpace(reason) ? "Xero refused it" : reason.Trim().TrimEnd('.'))}. The record itself is in Xero; later changes still sync.";

    /// <summary>
    /// Finishes an upload that cannot be made: saves <paramref name="link"/>
    /// with <see cref="XeroLink.AttachmentNote"/> set (the file's name and hash
    /// stay those of the last file actually attached) and answers
    /// <see cref="XeroPushOutcome.NothingToDo"/> with the note as its reason.
    /// </summary>
    /// <param name="links">The link store.</param>
    /// <param name="link">The record's link.</param>
    /// <param name="reason">Why the file is not attached.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    public static async Task<XeroPushResult> FinishAsync(IXeroLinkStore links, XeroLink link, string? reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(link);

        var note = Note(reason);
        var updated = link with { AttachmentNote = note };
        await links.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
        return new XeroPushResult(XeroPushOutcome.NothingToDo, note, Link: updated);
    }

    private static string Megabytes(long bytes) =>
        (bytes / (1024m * 1024m)).ToString("0.#", CultureInfo.InvariantCulture) + " MB";
}
