using System.Globalization;
using SkiaSharp;
using Tempest.Calc.Library.Dashboard;
using Tempest.Calc.Sheets;
using Tempest.Desktop.Documents;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// Addendum C, C-4: the verification dashboard as a PDF for the owner's records, drawn through the same
/// <see cref="DocumentTemplate"/> pipeline the calculation sheets and registers use (A4 landscape, the design system's header band, table
/// and footer). Every string is drawn as text, never interpreted, and cut to <see cref="MaxCellLength"/>; at most
/// <see cref="VerificationDashboardService.MaxRows"/> rows are drawn.
/// </summary>
public sealed class VerificationPdfRenderer
{
    /// <summary>The longest text drawn in one cell.</summary>
    public const int MaxCellLength = 160;

    /// <summary>Supplies the organisation identity for the footer (Settings → Organisation).</summary>
    public Func<OrganisationIdentity>? IdentityProvider { get; set; }

    /// <summary>Renders <paramref name="rows"/> (the rows <paramref name="report"/> shows under <paramref name="filterText"/>) as a PDF.</summary>
    public ReadOnlyMemory<byte> Render(VerificationReport report, IReadOnlyList<VerificationRow> rows, string filterText, string applicationVersion)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(rows);
        var identity = IdentityProvider?.Invoke() ?? OrganisationIdentity.TempestDefaults;
        var state = DocumentTemplate.BeginLayout(landscape: true);
        using var measure = new SKPaint { Typeface = SKTypeface.Default };
        var drawn = rows.Take(VerificationDashboardService.MaxRows).ToList();

        DocumentTemplate.AddHeaderBand(state, measure, "VERIFICATION DASHBOARD", $"{drawn.Count} of {report.Rows.Count} row(s)  ·  {report.GeneratedAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        DocumentTemplate.AddGap(state, 4f);
        DocumentTemplate.AddWrappedLine(state, measure, $"Filter: {T(string.IsNullOrWhiteSpace(filterText) ? "all rows" : filterText)}", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddWrappedLine(state, measure,
            $"{drawn.Count(r => r.IsFailing)} failing  ·  {(drawn.Any(r => !r.DefectsKnown) ? "open defects unknown" : $"{drawn.Sum(r => r.OpenDefectCount)} open defect(s) on {drawn.Count(r => r.HasOpenDefects)} row(s)")}  ·  {drawn.Count(r => r.HasUnconfirmedOrSecondary)} with Unconfirmed or Secondary cases  ·  {drawn.Count(r => r.HasMissingRecords)} with records not entered  ·  {drawn.Count(r => r.KatState == KatRunState.NotRun)} not run  ·  {drawn.Count(r => !r.IsRederived)} not re-derived  ·  {drawn.Count(r => r.IsClean)} clean",
            DocumentTemplate.BodySize, bold: false);

        if (report.DataProblem is { Length: > 0 } problem)
            DocumentTemplate.AddWrappedLine(state, measure, $"{T(problem)} The columns that come from documents are empty.", DocumentTemplate.BodySize, bold: true);

        if (report.Truncated || rows.Count > drawn.Count)
            DocumentTemplate.AddWrappedLine(state, measure, $"Only the first {drawn.Count} rows are listed.", DocumentTemplate.BodySize, bold: true);

        DocumentTemplate.AddGap(state, 4f);
        DocumentTemplate.AddRule(state);

        if (drawn.Count == 0)
        {
            DocumentTemplate.AddWrappedLine(state, measure, "No calculator or chain matches this filter.", DocumentTemplate.BodySize, bold: false);
        }
        else
        {
            var w = state.ContentWidth;
            DocumentTemplate.AddTable(
                state, measure, ["Calculator or chain", "Discipline", "Version", "Known answers", "Unconfirmed / Secondary", "Independent re-derivation", "Records not entered", "Open defects", "Manual test"],
                [w * 0.22f, w * 0.09f, w * 0.06f, w * 0.12f, w * 0.07f, w * 0.17f, w * 0.12f, w * 0.06f, w * 0.09f],
                [.. drawn.Select(Cells)],
                [SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left]);

            var open = drawn.Where(r => r.HasOpenDefects).ToList();

            if (drawn.Any(r => !r.DefectsKnown))
            {
                DocumentTemplate.AddGap(state, 6f);
                DocumentTemplate.AddWrappedLine(state, measure, "The open defects are not known: the verification data could not be read, so the Open defects column says unknown, not zero.", DocumentTemplate.BodySize, bold: true);
            }
            else if (open.Count > 0)
            {
                DocumentTemplate.AddGap(state, 6f);
                DocumentTemplate.AddHeading(state, measure, "Open defects");
                DocumentTemplate.AddWrappedLine(state, measure, "Not marked Fixed in docs/testing/v0.26.0-known-defects.md: still open, or waiting for the owner's decision against a licensed standard.", DocumentTemplate.CaptionSize, bold: false);

                foreach (var r in open)
                {
                    var own = r.OpenDefectCount == 0 ? "none of its own" : $"{string.Join(", ", r.OpenDefects.Take(40).Select(d => $"{T(d.Id)} ({T(d.Severity)}, {d.StatusLabel.ToLowerInvariant()})"))}{(r.OpenDefects.Count > 40 ? ", …" : string.Empty)}";
                    var steps = r.StepOpenDefectCount > 0 ? $"; step defects ({r.StepOpenDefectCount}): {string.Join(", ", r.StepOpenDefects.Take(20).Select(d => $"{T(d.Id)} ({T(d.Severity)})"))}{(r.StepOpenDefectCount > 20 ? ", …" : string.Empty)}" : string.Empty;
                    DocumentTemplate.AddWrappedLine(state, measure, $"{T(r.Id)}: {own}{steps}", DocumentTemplate.CaptionSize, bold: false);
                }
            }
        }

        var metadata = new SKDocumentPdfMetadata
        {
            Title = "TempestOS verification dashboard",
            Author = "TempestOS",
            Subject = "Verification dashboard",
            Creator = T(applicationVersion, 80),
            Producer = T(applicationVersion, 80),
            Creation = report.GeneratedAt.UtcDateTime,
            Modified = report.GeneratedAt.UtcDateTime,
        };

        DocumentTemplate.AppendFooters(state.Pages, identity, $"{T(applicationVersion, 80)} · Generated {report.GeneratedAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC", landscape: true);
        return DocumentTemplate.RenderPdf(state.Pages, metadata, landscape: true);
    }

    private static string[] Cells(VerificationRow r) =>
    [
        $"{T(r.Title)} ({T(r.Id)})",
        T(r.Discipline),
        r.Version.Length > 0 ? T(r.Version) : "—",
        T(r.KatText),
        $"{r.Unconfirmed} / {r.Secondary}",
        T($"{r.RederivationResult}: {r.RederivationFindings.Text}"),
        r.MissingRecordKinds.Count == 0 ? "none" : T(string.Join("; ", r.MissingRecordKinds)),
        T(r.OpenDefectsText),
        T(r.ManualTest.Label),
    ];

    private static string T(string? text, int max = MaxCellLength)
    {
        var clean = CalcSheetTextRenderer.Clean(text ?? string.Empty);
        return clean.Length > max ? clean[..max] + "…" : clean;
    }
}
