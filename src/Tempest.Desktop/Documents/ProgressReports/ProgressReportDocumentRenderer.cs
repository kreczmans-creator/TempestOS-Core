using System.Globalization;
using SkiaSharp;

namespace Tempest.Desktop.Documents.ProgressReports;

/// <summary>How a <see cref="ProgressReportMilestoneRow"/> stands against today — the "RAG" (red/amber/green) bucket the brief asks the milestones list to carry (`WP 21.2A`).</summary>
public enum ProgressReportRag
{
    /// <summary>Not yet due, and not due soon.</summary>
    OnTrack,

    /// <summary>Due within the next seven days — the identical window <c>Tempest.Workspace.Projects.ProjectStatusReadModel.AtRiskWithinDays</c> already uses for the Projects dashboard, reused here rather than re-picked.</summary>
    AtRisk,

    /// <summary>Past its own target date.</summary>
    Overdue,
}

/// <summary>One milestone on a <see cref="ProgressReportDocumentModel"/>'s own schedule.</summary>
/// <param name="Title">The milestone's own title.</param>
/// <param name="TargetDate">When it is due.</param>
/// <param name="Rag">Where it stands against today — see <see cref="ProgressReportRag"/>'s own remarks for how this is derived.</param>
public sealed record ProgressReportMilestoneRow(string Title, DateOnly TargetDate, ProgressReportRag Rag);

/// <summary>One risk on a <see cref="ProgressReportDocumentModel"/>'s own risk section, from the project's own governance register.</summary>
/// <param name="Title">The risk's own title.</param>
/// <param name="Status">Where it stands.</param>
/// <param name="Likelihood">How likely, in the team's own scale. <see langword="null"/> when unscored.</param>
/// <param name="Severity">How bad, in the team's own scale. <see langword="null"/> when unscored.</param>
public sealed record ProgressReportRiskRow(string Title, string Status, string? Likelihood, string? Severity);

/// <summary>
/// Everything the progress report document renders (`WP 21.2A`, scope item
/// 2) — RAG summary, progress vs programme, cost position, risks,
/// look-ahead, one section per page (the design system's own
/// <c>ProgressReport.dc.html</c> deck grammar, "one section per slide" —
/// rendered as an A4 landscape document, per this Work Package's own
/// brief).
/// </summary>
/// <param name="ProjectCode">The reported project's own business identifier.</param>
/// <param name="ProjectName">The reported project's own display name.</param>
/// <param name="AsOfDate">The date this report's own figures are current as of.</param>
/// <param name="HealthStatus">The project's own computed status (<c>Tempest.Workspace.Projects.ProjectHealthStatus</c>, resolved to its own display word by the caller) — the RAG summary's own headline.</param>
/// <param name="HealthReason">Why <paramref name="HealthStatus"/> is what it is, in one sentence — the identical reason <c>ProjectStatusReadModel.RuleFor</c> already computes, read here rather than re-derived.</param>
/// <param name="Milestones">Every live milestone, by target date.</param>
/// <param name="LookAheadMilestones">The milestones due in the next four weeks — the brief's own "next four weeks' milestones as the look-ahead".</param>
/// <param name="QuotedHours">The sum of every Hourly line's own hours across this project's Accepted quotations, or <see langword="null"/> when it has none — half of the "commercial snapshot's figures" the brief names, read directly off <c>ProjectStatusRow</c> rather than re-derived from the quotations themselves.</param>
/// <param name="RecordedHours">The sum of every live timesheet entry's own hours recorded against this project — the other half of the commercial snapshot.</param>
/// <param name="Risks">Every live risk and hazard in the project's own governance register.</param>
/// <param name="DeliverablesNote">
/// What this report can honestly say about deliverable-level progress —
/// this Work Package's own kill switch: <c>ProjectStatusRow</c> (the one
/// read this renderer's own caller uses, so opening this button never adds
/// a second, heavier project read alongside the Projects dashboard's own
/// existing one) carries no deliverable-level detail, so this field names
/// that plainly rather than a renderer inventing a percentage nothing
/// backs.
/// </param>
/// <param name="GeneratedAtUtc">When this sheet was generated — supplied, never <see cref="DateTime.Now"/>.</param>
/// <param name="ApplicationVersionText">The running application's own version text.</param>
public sealed record ProgressReportDocumentModel(
    string ProjectCode,
    string ProjectName,
    DateOnly AsOfDate,
    string HealthStatus,
    string HealthReason,
    IReadOnlyList<ProgressReportMilestoneRow> Milestones,
    IReadOnlyList<ProgressReportMilestoneRow> LookAheadMilestones,
    decimal? QuotedHours,
    decimal RecordedHours,
    IReadOnlyList<ProgressReportRiskRow> Risks,
    string DeliverablesNote,
    DateTimeOffset GeneratedAtUtc,
    string ApplicationVersionText)
{
    /// <summary>
    /// Buckets <paramref name="milestones"/> into a <see cref="ProgressReportRag"/>
    /// against <paramref name="today"/> — overdue past its own target date,
    /// at risk within <paramref name="atRiskWithinDays"/> days, else on
    /// track. The identical rule
    /// <c>Tempest.Workspace.Projects.ProjectStatusReadModel.RuleFor</c>
    /// applies per-milestone, simplified here to pure date arithmetic since
    /// this model carries no deliverable-completion detail to weigh
    /// against a milestone the way that rule does (<see cref="DeliverablesNote"/>'s
    /// own remarks) — a disclosed simplification, not a different policy.
    /// </summary>
    public static ProgressReportRag RagFor(DateOnly targetDate, DateOnly today, int atRiskWithinDays = 7)
    {
        if (targetDate < today)
            return ProgressReportRag.Overdue;

        return targetDate.DayNumber - today.DayNumber <= atRiskWithinDays ? ProgressReportRag.AtRisk : ProgressReportRag.OnTrack;
    }
}

/// <summary>Renders a <see cref="ProgressReportDocumentModel"/> as an A4 <b>landscape</b> PDF (`WP 21.2A`, scope item 2) — one section per page, through <see cref="DocumentTemplate"/>'s own landscape geometry.</summary>
public sealed class ProgressReportDocumentRenderer : IDocumentRenderer<ProgressReportDocumentModel>
{
    /// <inheritdoc />
    public string DocumentType => "PROGRESS REPORT";

    /// <inheritdoc />
    public string TemplateName => "progress-report";

    /// <summary>Supplies the organisation identity for a render whose caller passes none.</summary>
    public Func<OrganisationIdentity>? IdentityProvider { get; set; }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Render(ProgressReportDocumentModel model, OrganisationIdentity? identity = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        var orgIdentity = identity ?? IdentityProvider?.Invoke() ?? OrganisationIdentity.TempestDefaults;
        var pages = Layout(model);
        DocumentTemplate.AppendFooters(pages, orgIdentity, FormatFooterDetail(model), landscape: true);

        var metadata = new SKDocumentPdfMetadata
        {
            Title = $"{model.ProjectCode} — Progress Report",
            Author = model.ProjectName,
            Subject = "Progress report",
            Creator = model.ApplicationVersionText,
            Producer = model.ApplicationVersionText,
            Creation = model.GeneratedAtUtc.UtcDateTime,
            Modified = model.GeneratedAtUtc.UtcDateTime,
        };

        return DocumentTemplate.RenderPdf(pages, metadata, landscape: true);
    }

    private static List<DocumentTemplate.PagePlan> Layout(ProgressReportDocumentModel model)
    {
        var state = DocumentTemplate.BeginLayout(landscape: true);
        using var measure = new SKPaint { Typeface = SKTypeface.Default };

        // Slide 1 — RAG summary.
        DocumentTemplate.AddHeaderBand(state, measure, "PROGRESS REPORT", $"{model.ProjectCode}  ·  As of {FormatDate(model.AsOfDate)}");
        DocumentTemplate.AddGap(state, 4f);
        DocumentTemplate.AddWrappedLine(state, measure, $"{model.ProjectCode} — {model.ProjectName}", DocumentTemplate.TitleSize, bold: true, color: DocumentTemplate.Ink900, fontRole: DocumentFontRole.Display);
        DocumentTemplate.AddGap(state, 8f);
        DocumentTemplate.AddHeading(state, measure, "Status");
        DocumentTemplate.AddWrappedLine(state, measure, model.HealthStatus, DocumentTemplate.TitleSize, bold: true, color: RagColour(model.HealthStatus), fontRole: DocumentFontRole.Display);
        DocumentTemplate.AddWrappedLine(state, measure, model.HealthReason, DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddGap(state, 8f);
        DocumentTemplate.AddHeading(state, measure, "Milestones");
        AddMilestoneTable(state, measure, model.Milestones, "No milestones recorded.");

        // Slide 2 — progress vs programme / cost position (commercial snapshot).
        DocumentTemplate.NewPage(state);
        DocumentTemplate.AddHeaderBand(state, measure, "PROGRESS REPORT", $"{model.ProjectCode}  ·  As of {FormatDate(model.AsOfDate)}");
        DocumentTemplate.AddGap(state, 4f);
        DocumentTemplate.AddHeading(state, measure, "Cost position");
        DocumentTemplate.AddWrappedLine(
            state, measure,
            model.QuotedHours is { } quoted
                ? $"Quoted {quoted:0.##}h  •  Recorded {model.RecordedHours:0.##}h  •  Variance {model.RecordedHours - quoted:0.##;+0.##;0}h"
                : $"Recorded {model.RecordedHours:0.##}h (no Accepted quotation to compare against).",
            DocumentTemplate.BodySize, bold: false, fontRole: DocumentFontRole.Mono);
        DocumentTemplate.AddGap(state, 8f);
        DocumentTemplate.AddHeading(state, measure, "Deliverables progress");
        DocumentTemplate.AddWrappedLine(state, measure, model.DeliverablesNote, DocumentTemplate.BodySize, bold: false);

        // Slide 3 — risks.
        DocumentTemplate.NewPage(state);
        DocumentTemplate.AddHeaderBand(state, measure, "PROGRESS REPORT", $"{model.ProjectCode}  ·  As of {FormatDate(model.AsOfDate)}");
        DocumentTemplate.AddGap(state, 4f);
        DocumentTemplate.AddHeading(state, measure, "Risks");
        if (model.Risks.Count == 0)
        {
            DocumentTemplate.AddWrappedLine(state, measure, "No live risks recorded in the governance register.", DocumentTemplate.BodySize, bold: false);
        }
        else
        {
            DocumentTemplate.AddTable(
                state, measure,
                headers: ["Title", "Status", "Likelihood", "Severity"],
                widths: [state.ContentWidth * 0.52f, state.ContentWidth * 0.18f, state.ContentWidth * 0.15f, state.ContentWidth * 0.15f],
                rows: [.. model.Risks.Select(r => new[] { r.Title, r.Status, r.Likelihood ?? "—", r.Severity ?? "—" })],
                columnAligns: [SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Center, SKTextAlign.Center]);
        }

        // Slide 4 — look-ahead.
        DocumentTemplate.NewPage(state);
        DocumentTemplate.AddHeaderBand(state, measure, "PROGRESS REPORT", $"{model.ProjectCode}  ·  As of {FormatDate(model.AsOfDate)}");
        DocumentTemplate.AddGap(state, 4f);
        DocumentTemplate.AddHeading(state, measure, "Look-ahead — next four weeks");
        AddMilestoneTable(state, measure, model.LookAheadMilestones, "No milestones due in the next four weeks.");

        return state.Pages;
    }

    private static void AddMilestoneTable(DocumentTemplate.LayoutState state, SKPaint measure, IReadOnlyList<ProgressReportMilestoneRow> milestones, string emptyText)
    {
        if (milestones.Count == 0)
        {
            DocumentTemplate.AddWrappedLine(state, measure, emptyText, DocumentTemplate.BodySize, bold: false);
            return;
        }

        DocumentTemplate.AddTable(
            state, measure,
            headers: ["Milestone", "Target date", "RAG"],
            widths: [state.ContentWidth * 0.55f, state.ContentWidth * 0.25f, state.ContentWidth * 0.20f],
            rows: [.. milestones.Select(m => new[] { m.Title, m.TargetDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), m.Rag.ToString() })],
            columnAligns: [SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Center]);
    }

    private static SKColor RagColour(string healthStatus) => healthStatus switch
    {
        "Overdue" or "Blocked" => DocumentTemplate.Ink900,
        "AtRisk" or "At risk" => DocumentTemplate.Indigo600,
        _ => DocumentTemplate.Ink900,
    };

    private static string FormatFooterDetail(ProgressReportDocumentModel model) => string.Format(
        CultureInfo.InvariantCulture,
        "{0} · {1} · Generated {2:yyyy-MM-dd HH:mm} UTC",
        model.ApplicationVersionText, model.ProjectCode, model.GeneratedAtUtc.UtcDateTime);

    private static string FormatDate(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
