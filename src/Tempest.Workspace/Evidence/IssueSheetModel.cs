using Tempest.Core.Evidence;

namespace Tempest.Workspace.Evidence;

/// <summary>
/// One citation row as the issue sheet renders it: what
/// <see cref="Tempest.Core.Evidence.EvidenceCitation"/> carries, flattened
/// to the four columns the sheet's own citations table shows
/// (`ADR-0148`, `WP 18.2B`).
/// </summary>
/// <param name="Library">The library the cited record belongs to, exactly as that catalogue names itself.</param>
/// <param name="RecordId">The cited record's own identity within that library.</param>
/// <param name="Revision">The revision cited — pinned at citation time, never the record's current revision.</param>
/// <param name="SourceCitationSnapshot">The cited record's own source citation, as it stood when cited — <see langword="null"/> when the record carried none (`WP 18.0B`).</param>
public sealed record IssueSheetCitationRow(string Library, string RecordId, int Revision, string? SourceCitationSnapshot);

/// <summary>
/// What <see cref="IssueSheetModel.From"/> needs that
/// <see cref="Tempest.Core.Evidence.Evidence"/> itself does not know
/// (`ADR-0148`, `WP 18.2B`) — the project's own fields (a tag on the
/// Evidence's own <c>ParentId</c>, never dereferenced by this Kind),
/// display names resolved from identity ids the Evidence only holds as
/// opaque strings, the running application's own version, the persistence
/// store's own write sequence at the moment of issue, and the timestamp
/// the sheet is generated at.
/// </summary>
/// <param name="ProjectCode">The evidence's own project's business identifier.</param>
/// <param name="ProjectName">The evidence's own project's display name.</param>
/// <param name="AuthorDisplayName">The author's own display name, resolved from <see cref="IEvidenceRecord.AuthorIdentityId"/>.</param>
/// <param name="CheckerPrincipalDisplayName">
/// The checker's own display name, resolved from
/// <see cref="CheckRecord.CheckerIdentityId"/> when the independence rule
/// was on and the checker was therefore a second signed-in principal;
/// <see langword="null"/> when the check was entered by hand and carries
/// no principal of its own.
/// </param>
/// <param name="ApplicationVersionText">The running application's own version text — <c>"TempestOS &lt;version&gt; (&lt;commit&gt;)"</c>.</param>
/// <param name="StoreSequence">The persistence store's own write sequence at the moment of issue.</param>
/// <param name="GeneratedAtUtc">When the sheet is generated — supplied by the caller, never read from the clock by <see cref="IssueSheetModel.From"/> or by the renderer, so a re-render of the same record is a re-render of the same moment.</param>
public sealed record IssueSheetContext(
    string ProjectCode,
    string ProjectName,
    string AuthorDisplayName,
    string? CheckerPrincipalDisplayName,
    string ApplicationVersionText,
    long StoreSequence,
    DateTimeOffset GeneratedAtUtc);

/// <summary>
/// Everything the issue sheet renders — an immutable snapshot built once,
/// from one issued revision of one piece of <see cref="Evidence"/>, by
/// <see cref="From"/> (`ADR-0148`, `WP 18.2B`). Carries no behaviour of its
/// own beyond that builder: the renderer reads it, draws it, and the sheet
/// is never edited — a re-render of the same model is byte-identical to
/// the first.
/// </summary>
/// <param name="ProjectCode">The project's own business identifier.</param>
/// <param name="ProjectName">The project's own display name.</param>
/// <param name="Client">Who the evidence was issued to (<see cref="IssueRecord.Client"/>) — not a fact the project itself carries (`D-028`: not ERP, not PLM, no client field on the object model).</param>
/// <param name="EvidenceReference">The evidence's own business identifier, or <see langword="null"/> when none was assigned.</param>
/// <param name="Title">The evidence's own display name.</param>
/// <param name="Revision">The revision issued (<see cref="IssueRecord.Revision"/>).</param>
/// <param name="IssueReference">The issue's own reference (<see cref="IssueRecord.IssueReference"/>).</param>
/// <param name="IssueDateUtc">When the evidence was issued (<see cref="IssueRecord.DateUtc"/>) — not itself in the brief's own prose field list, but present on <see cref="IssueRecord"/> and the plain reading of "the issue sheet" needing the date the Issue form's own fourth field actually collected.</param>
/// <param name="Classification">What kind of engineering record this evidence is.</param>
/// <param name="AuthorDisplayName">The author's own display name (resolved by the caller; the Evidence itself only knows an identity id).</param>
/// <param name="AuthorDateUtc">When this evidence was first authored — stable across revisions.</param>
/// <param name="CheckerName">The checker's name, as typed.</param>
/// <param name="CheckerOrganisation">The checker's organisation, as typed.</param>
/// <param name="CheckerPrincipalDisplayName">The checker's own display name, when the checker was a second signed-in principal; <see langword="null"/> otherwise.</param>
/// <param name="CheckDateUtc">When the check was recorded.</param>
/// <param name="Outcome">What the checker concluded.</param>
/// <param name="Citations">Every governed reference record this evidence stood on, at the revision cited.</param>
/// <param name="DeclaredFigures">Every named, typed figure declared against this evidence.</param>
/// <param name="GeneratedAtUtc">When this sheet was generated — supplied, never <see cref="DateTime.Now"/>.</param>
/// <param name="ApplicationVersionText">The running application's own version text.</param>
/// <param name="StoreSequence">The persistence store's own write sequence at the moment of issue.</param>
public sealed record IssueSheetModel(
    string ProjectCode,
    string ProjectName,
    string Client,
    string? EvidenceReference,
    string Title,
    string Revision,
    string IssueReference,
    DateTimeOffset IssueDateUtc,
    EvidenceClassification Classification,
    string AuthorDisplayName,
    DateTimeOffset AuthorDateUtc,
    string CheckerName,
    string CheckerOrganisation,
    string? CheckerPrincipalDisplayName,
    DateTimeOffset CheckDateUtc,
    CheckOutcome Outcome,
    IReadOnlyList<IssueSheetCitationRow> Citations,
    IReadOnlyList<DeclaredFigure> DeclaredFigures,
    DateTimeOffset GeneratedAtUtc,
    string ApplicationVersionText,
    long StoreSequence)
{
    /// <summary>
    /// Builds the sheet's own model from one issued <paramref name="evidence"/>
    /// and what <paramref name="context"/> supplies about it.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="evidence"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="evidence"/> has not been issued, or (which the status machine never permits without) has not been checked.</exception>
    public static IssueSheetModel From(Tempest.Core.Evidence.Evidence evidence, IssueSheetContext context)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(context);

        if (evidence.Issue is not { } issue)
            throw new InvalidOperationException($"Evidence '{evidence.Id}' has not been issued; an issue sheet can only be rendered for issued evidence.");
        if (evidence.Check is not { } check)
            throw new InvalidOperationException($"Evidence '{evidence.Id}' has not been checked; an issue sheet can only be rendered for checked, issued evidence.");

        return new IssueSheetModel(
            context.ProjectCode,
            context.ProjectName,
            issue.Client,
            evidence.Identifier,
            evidence.DisplayName,
            issue.Revision,
            issue.IssueReference,
            issue.DateUtc,
            evidence.Classification,
            context.AuthorDisplayName,
            evidence.CreatedAt,
            check.CheckerName,
            check.CheckerOrganisation,
            context.CheckerPrincipalDisplayName,
            check.DateUtc,
            check.Outcome,
            [.. evidence.Citations.Select(c => new IssueSheetCitationRow(c.Pin.Library, c.Pin.RecordId, c.Pin.RevisionNumber, c.SourceCitationSnapshot))],
            [.. evidence.DeclaredFigures],
            context.GeneratedAtUtc,
            context.ApplicationVersionText,
            context.StoreSequence);
    }
}
