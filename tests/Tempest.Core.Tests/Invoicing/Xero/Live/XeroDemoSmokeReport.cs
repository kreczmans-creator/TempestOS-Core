using System.Globalization;
using System.Text;

namespace Tempest.Core.Tests.Invoicing.Xero.Live;

/// <summary>One step of the Demo Company smoke journey and how it went.</summary>
/// <param name="Id">The step id (<c>S01</c>…), matching the runbook's <c>XL-</c> steps.</param>
/// <param name="Title">What the step checks.</param>
/// <param name="Passed">Whether it passed.</param>
/// <param name="Detail">What was seen (ids, statuses, counts) — never a token.</param>
public sealed record XeroSmokeStep(string Id, string Title, bool Passed, string Detail);

/// <summary>
/// One of the design's open items the smoke run confirms on the real Demo
/// Company (§2 scopes, §3 attachments, X5 key lifetime, X2 contacts).
/// </summary>
/// <param name="Key">The item (<c>F1</c>…).</param>
/// <param name="Question">What the design assumed or left open.</param>
/// <param name="Observed">What Xero actually did on this run.</param>
/// <param name="Verdict"><c>CONFIRMED</c> (the design's assumption holds), <c>DIFFERS</c> (it does not — raise it before release) or <c>NOT RUN</c>.</param>
public sealed record XeroSmokeFinding(string Key, string Question, string Observed, string Verdict);

/// <summary>A record the run made, with a link for the Product Owner to open it in Xero.</summary>
/// <param name="Kind">Quote, invoice, purchase order, bill or contact.</param>
/// <param name="Number">Its number (or name).</param>
/// <param name="XeroId">Xero's id.</param>
/// <param name="Status">Its last status read back.</param>
/// <param name="Link">A Xero web link to it.</param>
public sealed record XeroSmokeRecord(string Kind, string Number, string XeroId, string Status, string Link);

/// <summary>Everything a smoke run saw: steps, the open items, the records and whether it refused to write.</summary>
public sealed class XeroDemoSmokeReport
{
    private readonly List<XeroSmokeStep> _steps = [];
    private readonly List<XeroSmokeFinding> _findings = [];
    private readonly List<XeroSmokeRecord> _records = [];

    /// <summary>The verdict for an open item the run confirmed.</summary>
    public const string Confirmed = "CONFIRMED";

    /// <summary>The verdict for an open item where Xero did not behave as the design assumed.</summary>
    public const string Differs = "DIFFERS";

    /// <summary>The verdict for an open item this run did not exercise.</summary>
    public const string NotRun = "NOT RUN";

    /// <summary>The steps, in order.</summary>
    public IReadOnlyList<XeroSmokeStep> Steps => _steps;

    /// <summary>The open items.</summary>
    public IReadOnlyList<XeroSmokeFinding> Findings => _findings;

    /// <summary>The records made or linked.</summary>
    public IReadOnlyList<XeroSmokeRecord> Records => _records;

    /// <summary>Whether the run stopped before writing because the organisation is not the Demo Company (D7).</summary>
    public bool RefusedToWrite { get; internal set; }

    /// <summary>The organisation's name as Xero reported it.</summary>
    public string? OrganisationName { get; internal set; }

    /// <summary>The run's stamp (part of every number it made).</summary>
    public string Stamp { get; internal set; } = string.Empty;

    /// <summary>Whether every step passed.</summary>
    public bool Passed => _steps.Count > 0 && _steps.All(s => s.Passed);

    /// <summary>The steps that failed.</summary>
    public IReadOnlyList<XeroSmokeStep> Failures => [.. _steps.Where(s => !s.Passed)];

    /// <summary>Records a step.</summary>
    /// <param name="step">The step.</param>
    internal void Add(XeroSmokeStep step) => _steps.Add(step);

    /// <summary>Records an open item.</summary>
    /// <param name="finding">The item.</param>
    internal void Add(XeroSmokeFinding finding)
    {
        _findings.RemoveAll(f => f.Key == finding.Key);
        _findings.Add(finding);
    }

    /// <summary>Records (or updates) a record made or linked.</summary>
    /// <param name="record">The record.</param>
    internal void Add(XeroSmokeRecord record)
    {
        _records.RemoveAll(r => r.XeroId == record.XeroId);
        _records.Add(record);
    }

    /// <summary>The report as Markdown — what the script prints and saves for the runbook.</summary>
    public string ToMarkdown()
    {
        var text = new StringBuilder();
        text.AppendLine("# TempestOS v0.24.0 - Xero Demo Company smoke run");
        text.AppendLine();
        text.Append("- Organisation: ").AppendLine(OrganisationName ?? "(not read)");
        text.Append("- Stamp: ").AppendLine(Stamp);
        text.Append("- Result: ").AppendLine(RefusedToWrite ? "REFUSED - not the Demo Company; nothing was written" : Passed ? "PASSED" : "FAILED");
        text.AppendLine();
        text.AppendLine("## Steps");
        text.AppendLine();
        text.AppendLine("| Step | Check | Result | Detail |");
        text.AppendLine("|---|---|---|---|");
        foreach (var step in _steps)
            text.Append("| ").Append(step.Id).Append(" | ").Append(Cell(step.Title)).Append(" | ").Append(step.Passed ? "pass" : "FAIL").Append(" | ").Append(Cell(step.Detail)).AppendLine(" |");

        text.AppendLine();
        text.AppendLine("## Open items confirmed on the Demo Company");
        text.AppendLine();
        text.AppendLine("| Item | Design assumption | Observed | Verdict |");
        text.AppendLine("|---|---|---|---|");
        foreach (var finding in _findings)
            text.Append("| ").Append(finding.Key).Append(" | ").Append(Cell(finding.Question)).Append(" | ").Append(Cell(finding.Observed)).Append(" | ").Append(finding.Verdict).AppendLine(" |");

        text.AppendLine();
        text.AppendLine("## Records in Xero (open each to inspect)");
        text.AppendLine();
        text.AppendLine("| Kind | Number | Status | Link |");
        text.AppendLine("|---|---|---|---|");
        foreach (var record in _records)
            text.Append("| ").Append(record.Kind).Append(" | ").Append(Cell(record.Number)).Append(" | ").Append(record.Status).Append(" | ").Append(record.Link).AppendLine(" |");

        return text.ToString();
    }

    /// <summary>A short plain-text summary, one line per step, for the test output.</summary>
    public string ToConsoleText()
    {
        var text = new StringBuilder();
        foreach (var step in _steps)
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"[{(step.Passed ? "PASS" : "FAIL")}] {step.Id} {step.Title} - {step.Detail}"));
        foreach (var finding in _findings)
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"[{finding.Verdict}] {finding.Key} {finding.Question} - {finding.Observed}"));
        foreach (var record in _records)
            text.AppendLine(string.Create(CultureInfo.InvariantCulture, $"[XERO] {record.Kind} {record.Number} ({record.Status}): {record.Link}"));
        return text.ToString();
    }

    private static string Cell(string value) => value.Replace("|", "/", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
