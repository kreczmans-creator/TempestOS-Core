using System.Text.Json.Serialization;
using Tempest.Calc.Import.Mapping;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>Mirrors the Evidence lifecycle for an instance.</summary>
public enum CalcInstanceStatus
{
    Draft,
    Checked,
    Issued,
    Superseded,
}

/// <summary>A stored output: text for display, SI value and unit for re-use.</summary>
public sealed record CalcOutputSnapshot(string Symbol, string Description, string Text, double? Si, string? Unit);

/// <summary>A stored check row.</summary>
public sealed record CalcCheckSnapshot(string Name, string Demand, string Capacity, double Utilisation, bool Passed);

/// <summary>A stored notice.</summary>
public sealed record CalcNoticeSnapshot(string RuleId, string Severity, string Message);

/// <summary>What Issue freezes alongside the Evidence revision: the rendered sheet text, the template text, the engine version and the test results.</summary>
public sealed record CalcIssueSnapshot(
    string SheetText,
    string TemplateText,
    string EngineVersion,
    int KnownAnswerPassed,
    int KnownAnswerTotal,
    DateTimeOffset TestRunAt,
    string? SheetAttachmentId,
    string? ApplicationVersion);

/// <summary>
/// §3.5: one calculation instance — a template reference, a project, the
/// inputs with provenance, the latest outputs, warnings and blocks, its
/// status, and the Evidence that governs it. Serialised as JSON into the
/// <c>Calc.Instances</c> collection.
/// </summary>
public sealed record CalcInstance(
    Guid Id,
    string TemplateReference,
    string TemplateHash,
    Guid? ProjectId,
    string Name,
    string Revision,
    IReadOnlyList<CalcInputValue> Inputs,
    IReadOnlyList<CalcOutputSnapshot> Outputs,
    IReadOnlyList<CalcCheckSnapshot> Checks,
    IReadOnlyList<CalcNoticeSnapshot> Warnings,
    IReadOnlyList<CalcNoticeSnapshot> Blocks,
    CalcInstanceStatus Status,
    Guid? EvidenceId,
    Guid? RegisterObjectId,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? CheckedBy = null,
    DateTimeOffset? CheckedAt = null,
    DateTimeOffset? IssuedAt = null,
    string? IssueReference = null,
    CalcIssueSnapshot? Issue = null,
    Guid? PredecessorId = null,
    Guid? PackId = null,
    string? OutputsHash = null,
    IReadOnlyDictionary<string, string>? UpstreamHashes = null,
    string? LastRejection = null,
    IReadOnlyList<string>? EditedBy = null,
    IReadOnlyList<string>? RepinnedBy = null,
    IReadOnlyList<CadImportRecord>? CadImports = null)
{
    /// <summary>
    /// Everyone who set up or changed this revision's inputs: its creator and every principal in <see cref="EditedBy"/> (kept
    /// across revisions). The independent check refuses all of them, not only the creator (Red R-A-05).
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<string> Authors => [.. new[] { CreatedBy }.Concat(EditedBy ?? []).Where(a => !string.IsNullOrWhiteSpace(a)).Distinct(StringComparer.Ordinal)];

    [JsonIgnore]
    public bool IsImmutable => Status is CalcInstanceStatus.Issued or CalcInstanceStatus.Superseded;

    [JsonIgnore]
    public string TemplateId => TemplateReference.Split('@')[0];

    [JsonIgnore]
    public string TemplateVersion => TemplateReference.Contains('@', StringComparison.Ordinal) ? TemplateReference.Split('@')[1] : string.Empty;

    /// <summary>The input provenance reference prefix that names another instance as the source of a value.</summary>
    public const string UpstreamPrefix = "calc:";

    /// <summary>The upstream a legacy <c>reference: calc:&lt;id&gt;</c> provenance names, or <see langword="null"/>.</summary>
    public static Guid? LegacyUpstreamOf(CalcProvenance? provenance) =>
        provenance is { Kind: CalcProvenanceKind.Reference, Reference: { } r } && r.StartsWith(UpstreamPrefix, StringComparison.Ordinal)
        && Guid.TryParse(r[UpstreamPrefix.Length..], out var g) && g != Guid.Empty ? g : null;

    /// <summary>Instance ids this instance takes inputs from (provenance "reference: calc:&lt;id&gt;").</summary>
    [JsonIgnore]
    public IReadOnlyList<Guid> UpstreamIds => Inputs
        .Select(i => i.Provenance)
        .Where(p => p is { Kind: CalcProvenanceKind.Reference, Reference: { } r } && r.StartsWith(UpstreamPrefix, StringComparison.Ordinal))
        .Select(p => Guid.TryParse(p!.Reference![UpstreamPrefix.Length..], out var g) ? g : Guid.Empty)
        .Where(g => g != Guid.Empty)
        .Concat(Links.Select(l => l.Link.UpstreamId))
        .Concat(CellLinks.Select(l => l.Link.UpstreamId))
        .Distinct()
        .ToList();

    /// <summary>The inputs bound to an output of another calculation (Addendum A, L1), with their pins.</summary>
    [JsonIgnore]
    public IReadOnlyList<(CalcInputValue Input, CalcLink Link)> Links => Inputs
        .Select(i => (Input: i, Link: CalcLink.From(i.Provenance)))
        .Where(x => x.Link is not null)
        .Select(x => (x.Input, x.Link!))
        .ToList();

    /// <summary>
    /// The table cells bound to an output of another calculation (ADR-0168 amendment 2), by table symbol, zero-based row
    /// and column key. <see cref="Links"/> is unchanged and holds top-level inputs only.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<(string Symbol, int Row, string Column, CalcLink Link)> CellLinks => Inputs
        .Where(i => i.Rows is not null)
        .SelectMany(i => i.Rows!.Select((row, r) => (i.Symbol, Row: r, row)))
        .Where(x => x.row is not null)
        .SelectMany(x => x.row.Cells
            .Where(c => c.Value is not null)
            .OrderBy(c => c.Key, StringComparer.Ordinal)
            .Select(c => (x.Symbol, x.Row, Column: c.Key, Link: CalcLink.From(c.Value.Provenance))))
        .Where(x => x.Link is not null)
        .Select(x => (x.Symbol, x.Row, x.Column, x.Link!))
        .ToList();

    /// <summary>Whether any input or table cell is bound to another calculation's output.</summary>
    [JsonIgnore]
    public bool HasLinks => Links.Count > 0 || CellLinks.Count > 0;
}

/// <summary>Why an instance is stale, if it is.</summary>
public sealed record CalcStaleness(bool IsStale, IReadOnlyList<string> Reasons)
{
    public static CalcStaleness Fresh { get; } = new(false, []);
}
