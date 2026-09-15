using System.Globalization;
using SkiaSharp;

namespace Tempest.Desktop.Documents.Timesheets;

/// <summary>One entry on a <see cref="TimesheetDocumentModel"/>'s own table — one <c>TimesheetEntry</c>, already resolved and formatted by the caller.</summary>
/// <param name="Date">The day the work was done.</param>
/// <param name="ProjectName">The project the time was recorded against.</param>
/// <param name="TaskDescription">What the work was.</param>
/// <param name="Hours">How many hours, formatted.</param>
/// <param name="Billable">Whether this time is billable to the client.</param>
public sealed record TimesheetDocumentRow(DateOnly Date, string ProjectName, string TaskDescription, decimal Hours, bool Billable);

/// <summary>
/// Everything the weekly timesheet document renders (`WP 21.2A`, scope item
/// 2, from a principal's own week of <c>TimesheetEntry</c> rows) — a
/// project's or a person's week: hours by project and day, totals, and an
/// approval block (the design system's own <c>Timesheet.dc.html</c>
/// grammar).
/// </summary>
/// <param name="PrincipalName">Whose week this is.</param>
/// <param name="WeekStart">The Monday this timesheet covers.</param>
/// <param name="Rows">Every entry recorded in the week, in the order the caller resolved them.</param>
/// <param name="WeekTotalHours">The week's own total hours, formatted.</param>
/// <param name="BillableHours">The week's own billable hours, formatted.</param>
/// <param name="GeneratedAtUtc">When this sheet was generated — supplied, never <see cref="DateTime.Now"/>.</param>
/// <param name="ApplicationVersionText">The running application's own version text.</param>
public sealed record TimesheetDocumentModel(
    string PrincipalName,
    DateOnly WeekStart,
    IReadOnlyList<TimesheetDocumentRow> Rows,
    string WeekTotalHours,
    string BillableHours,
    DateTimeOffset GeneratedAtUtc,
    string ApplicationVersionText);

/// <summary>Renders a <see cref="TimesheetDocumentModel"/> as an A4 PDF (`WP 21.2A`, scope item 2) — hours by project and day, totals, and a signed approval block — through <see cref="DocumentTemplate"/>.</summary>
/// <remarks>
/// <b>No real approver.</b> <c>TimesheetEntry</c> carries no approval field
/// of its own (`WP 19.0A`, `ADR-0150`) — this renderer's own signature rows
/// are blank lines for a signature and a date, exactly as
/// <c>IssueSheetRenderer.AddSignatureRow</c>'s own "Issued — —" row is
/// blank where nothing is recorded, never a fabricated name.
/// </remarks>
public sealed class TimesheetDocumentRenderer : IDocumentRenderer<TimesheetDocumentModel>
{
    /// <inheritdoc />
    public string DocumentType => "TIMESHEET";

    /// <inheritdoc />
    public string TemplateName => "timesheet";

    /// <summary>Supplies the organisation identity for a render whose caller passes none.</summary>
    public Func<OrganisationIdentity>? IdentityProvider { get; set; }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Render(TimesheetDocumentModel model, OrganisationIdentity? identity = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        var orgIdentity = identity ?? IdentityProvider?.Invoke() ?? OrganisationIdentity.TempestDefaults;
        var pages = Layout(model);
        DocumentTemplate.AppendFooters(pages, orgIdentity, FormatFooterDetail(model));

        var metadata = new SKDocumentPdfMetadata
        {
            Title = $"{model.PrincipalName} — Timesheet {model.WeekStart:yyyy-MM-dd}",
            Author = model.PrincipalName,
            Subject = "Timesheet",
            Creator = model.ApplicationVersionText,
            Producer = model.ApplicationVersionText,
            Creation = model.GeneratedAtUtc.UtcDateTime,
            Modified = model.GeneratedAtUtc.UtcDateTime,
        };

        return DocumentTemplate.RenderPdf(pages, metadata);
    }

    private static List<DocumentTemplate.PagePlan> Layout(TimesheetDocumentModel model)
    {
        var state = DocumentTemplate.BeginLayout();
        using var measure = new SKPaint { Typeface = SKTypeface.Default };
        var weekEnd = model.WeekStart.AddDays(6);

        DocumentTemplate.AddHeaderBand(
            state, measure, "TIMESHEET",
            $"{FormatDate(model.WeekStart)} – {FormatDate(weekEnd)}  ·  {model.PrincipalName}");
        DocumentTemplate.AddGap(state, 4f);

        DocumentTemplate.AddWrappedLine(state, measure, model.PrincipalName, DocumentTemplate.HeadingSize, bold: true, color: DocumentTemplate.Ink900, fontRole: DocumentFontRole.Display);
        DocumentTemplate.AddWrappedLine(state, measure, $"Week: {FormatDate(model.WeekStart)} – {FormatDate(weekEnd)}", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddWrappedLine(
            state, measure, $"Total {model.WeekTotalHours}h  •  Billable {model.BillableHours}h", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddGap(state, 4f);
        DocumentTemplate.AddRule(state);

        DocumentTemplate.AddHeading(state, measure, "Hours");
        if (model.Rows.Count == 0)
        {
            DocumentTemplate.AddWrappedLine(state, measure, "No time recorded this week.", DocumentTemplate.BodySize, bold: false);
        }
        else
        {
            var rows = model.Rows
                .OrderBy(r => r.Date)
                .ThenBy(r => r.ProjectName, StringComparer.Ordinal)
                .Select(r => new[]
                {
                    r.Date.ToString("ddd yyyy-MM-dd", CultureInfo.InvariantCulture),
                    r.ProjectName,
                    r.TaskDescription,
                    r.Hours.ToString("0.##", CultureInfo.InvariantCulture),
                    r.Billable ? "Billable" : "Non-billable",
                })
                .ToList();

            DocumentTemplate.AddTable(
                state, measure,
                headers: ["Date", "Project", "Task", "Hours", "Billable"],
                widths: [state.ContentWidth * 0.14f, state.ContentWidth * 0.22f, state.ContentWidth * 0.36f, state.ContentWidth * 0.12f, state.ContentWidth * 0.16f],
                rows: rows,
                columnAligns: [SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Right, SKTextAlign.Center]);
        }
        DocumentTemplate.AddGap(state, 4f);

        DocumentTemplate.AddWrappedLine(
            state, measure, $"Week total {model.WeekTotalHours}h  ·  Billable {model.BillableHours}h", DocumentTemplate.HeadingSize, bold: true, SKTextAlign.Right, DocumentTemplate.Ink900, DocumentFontRole.Display);
        DocumentTemplate.AddGap(state, 10f);

        DocumentTemplate.AddHeading(state, measure, "Approval");
        AddSignatureRow(state, "Prepared by");
        AddSignatureRow(state, "Approved by");

        return state.Pages;
    }

    /// <summary>A blank signature row (role label, a rule to sign against, a date field) — mirrors <c>IssueSheetRenderer.AddSignatureRow</c>'s own shape, blank rather than fabricated (this class's own remarks).</summary>
    private static void AddSignatureRow(DocumentTemplate.LayoutState state, string role)
    {
        DocumentTemplate.AddGap(state, 14f);
        DocumentTemplate.EnsureSpace(state, 30f);
        state.Page.Rules.Add(new DocumentTemplate.RuleRun(DocumentTemplate.ContentLeft, DocumentTemplate.ContentLeft + 260f, state.Y, 0.75f, DocumentTemplate.Hairline));
        state.Y += 4f;

        var baseline = state.Y + DocumentTemplate.BodySize;
        state.Page.Texts.Add(new DocumentTemplate.TextRun(DocumentTemplate.ContentLeft, baseline, $"{role} — signature", DocumentTemplate.BodySize, false, SKTextAlign.Left, DocumentTemplate.Slate700));
        state.Page.Texts.Add(new DocumentTemplate.TextRun(
            state.ContentRight, baseline, "Date:", DocumentTemplate.BodySize, false, SKTextAlign.Right, DocumentTemplate.Slate700));
        state.Y += DocumentTemplate.BodySize * DocumentTemplate.LineLeading;
    }

    private static string FormatFooterDetail(TimesheetDocumentModel model) => string.Format(
        CultureInfo.InvariantCulture,
        "{0} · Week of {1:yyyy-MM-dd} · Generated {2:yyyy-MM-dd HH:mm} UTC",
        model.ApplicationVersionText, model.WeekStart, model.GeneratedAtUtc.UtcDateTime);

    private static string FormatDate(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
