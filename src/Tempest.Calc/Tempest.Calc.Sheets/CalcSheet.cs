using Tempest.Calc.Engine;

namespace Tempest.Calc.Sheets;

/// <summary>The lifecycle status printed as the sheet's watermark (mirrors the Evidence statuses).</summary>
public enum CalcSheetStatus
{
    Draft,
    Checked,
    Issued,
    Superseded,
}

/// <summary>§3.4 item 1: the sheet header.</summary>
public sealed record CalcSheetHeader(
    string Project,
    string CalcId,
    string Title,
    string Revision,
    CalcSheetStatus Status,
    string Author,
    string? Checker,
    DateTimeOffset? AuthoredAt,
    DateTimeOffset? CheckedAt,
    DateTimeOffset? IssuedAt,
    string? Client = null,
    string? IssueReference = null);

/// <summary>§3.4 item 3: one row of the inputs table.</summary>
public sealed record CalcSheetInputRow(string Symbol, string Description, string Value, string Unit, string Source, string ValidRange);

/// <summary>§3.4 item 8: the verification footer.</summary>
public sealed record CalcSheetFooter(string TemplateReference, string ContentHash, string EngineVersion, int KnownAnswerPassed, int KnownAnswerTotal, DateTimeOffset? TestRunAt, string? ApplicationVersion = null, string? FeatureNote = null);

/// <summary>A row of a pack's revision table.</summary>
public sealed record CalcRevisionRow(string Revision, DateTimeOffset Date, string Author, string Description);

/// <summary>An entry of a pack index.</summary>
public sealed record CalcPackIndexRow(int Number, string CalcId, string Title, string Revision, string Status, string? Governing);

/// <summary>
/// One rendered calculation sheet: exactly the §3.4 shape, in order.
/// Built by <see cref="CalcSheetBuilder"/>; rendered to text by
/// <see cref="CalcSheetTextRenderer"/> and to PDF through
/// <see cref="ICalcSheetRenderer"/>.
/// </summary>
public sealed record CalcSheet(
    CalcSheetHeader Header,
    string Scope,
    string Method,
    IReadOnlyList<CalcClauseRef> ClauseRefs,
    IReadOnlyList<CalcSheetInputRow> Inputs,
    IReadOnlyList<CalcAssumption> Assumptions,
    IReadOnlyList<CalcStep> Working,
    IReadOnlyList<CalcCheck> Checks,
    IReadOnlyList<CalcNotice> Warnings,
    IReadOnlyList<CalcNotice> Blocks,
    IReadOnlyList<CalcOutput> Outputs,
    CalcSheetFooter Footer,
    IReadOnlyList<CalcRevisionRow>? Revisions = null,
    IReadOnlyList<CalcPackIndexRow>? PackIndex = null,
    string? DiagramKey = null,
    CalcInputSet? InputValues = null)
{
    public CalcCheck? Governing => Checks.Count == 0 ? null : Checks.OrderByDescending(c => c.Utilisation).First();
}

/// <summary>The seam a PDF (or any other) renderer implements. The Desktop implements it over the existing document pipeline.</summary>
public interface ICalcSheetRenderer
{
    ReadOnlyMemory<byte> Render(CalcSheet sheet);

    /// <summary>Renders several sheets as one pack with an index and a revision table.</summary>
    ReadOnlyMemory<byte> RenderPack(CalcSheet cover, IReadOnlyList<CalcSheet> sheets);
}
