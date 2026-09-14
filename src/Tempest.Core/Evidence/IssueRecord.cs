namespace Tempest.Core.Evidence;

/// <summary>A record of a piece of evidence being issued to the client (`ADR-0148`).</summary>
/// <param name="IssueReference">The issue's own reference, as the consultancy names it.</param>
/// <param name="Revision">The revision issued.</param>
/// <param name="Client">The client the evidence was issued to.</param>
/// <param name="DateUtc">When the evidence was issued.</param>
/// <param name="IssueSheetAttachmentId">
/// The rendered issue sheet's own attachment id — <see langword="null"/>
/// until `WP 18.2B` adds the renderer. Issuing is not refused for its
/// absence: the issue record itself is what makes evidence Issued; the
/// PDF is a rendering of it, added later.
/// </param>
public sealed record IssueRecord(
    string IssueReference,
    string Revision,
    string Client,
    DateTimeOffset DateUtc,
    Guid? IssueSheetAttachmentId);
