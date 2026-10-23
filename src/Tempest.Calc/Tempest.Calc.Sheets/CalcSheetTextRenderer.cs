using System.Globalization;
using System.Text;
using Tempest.Calc.Engine.Values;

namespace Tempest.Calc.Sheets;

/// <summary>
/// Renders a sheet as deterministic plain text: the golden-file form, the
/// text the search index holds, and the "Show working" form in the UI.
/// Every user string is written as-is in text; the PDF and HTML renderers
/// escape for their own targets. Timestamps and hashes are written on
/// lines that <see cref="Normalise"/> can strip for golden comparison.
/// </summary>
public static class CalcSheetTextRenderer
{
    public static string Render(CalcSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var sb = new StringBuilder();
        var h = sheet.Header;

        sb.Append("CALCULATION SHEET — ").Append(Status(h.Status)).AppendLine();
        sb.Append("Project: ").AppendLine(Clean(h.Project));
        sb.Append("Calc ID: ").Append(Clean(h.CalcId)).Append("    Revision: ").AppendLine(Clean(h.Revision));
        sb.Append("Title: ").AppendLine(Clean(h.Title));
        sb.Append("Author: ").Append(Clean(h.Author)).Append("    Authored: ").AppendLine(Date(h.AuthoredAt));
        sb.Append("Checker: ").Append(Clean(h.Checker ?? "—")).Append("    Checked: ").AppendLine(Date(h.CheckedAt));
        sb.Append("Issued: ").Append(Date(h.IssuedAt));

        if (h.IssueReference is { Length: > 0 })
            sb.Append("    Issue: ").Append(Clean(h.IssueReference));

        if (h.Client is { Length: > 0 })
            sb.Append("    Client: ").Append(Clean(h.Client));

        sb.AppendLine().AppendLine();

        if (sheet.PackIndex is { Count: > 0 } index)
        {
            sb.AppendLine("PACK INDEX");

            foreach (var row in index)
                sb.Append(CultureInfo.InvariantCulture, $"  {row.Number}. {Clean(row.CalcId)} — {Clean(row.Title)} — rev {Clean(row.Revision)} — {Clean(row.Status)}{(row.Governing is null ? string.Empty : " — " + Clean(row.Governing))}").AppendLine();

            sb.AppendLine();
        }

        if (sheet.Revisions is { Count: > 0 } revisions)
        {
            sb.AppendLine("REVISIONS");

            foreach (var r in revisions)
                sb.Append(CultureInfo.InvariantCulture, $"  {Clean(r.Revision)}  {Date(r.Date)}  {Clean(r.Author)}  {Clean(r.Description)}").AppendLine();

            sb.AppendLine();
        }

        sb.AppendLine("1. SCOPE AND METHOD");
        sb.AppendLine(Clean(sheet.Scope));
        sb.AppendLine(Clean(sheet.Method));

        foreach (var c in sheet.ClauseRefs)
            sb.Append("  Ref: ").Append(Clean(c.Standard)).Append(c.Clause is { Length: > 0 } and not "—" ? " " + Clean(c.Clause) : string.Empty).Append(" — ").AppendLine(Clean(c.Title));

        sb.AppendLine();
        sb.AppendLine("2. INPUTS");
        sb.AppendLine("  Symbol | Description | Value | Unit | Source | Valid range");

        foreach (var i in sheet.Inputs)
            sb.Append("  ").Append(Clean(i.Symbol)).Append(" | ").Append(Clean(i.Description)).Append(" | ").Append(Clean(i.Value)).Append(" | ").Append(Clean(i.Unit)).Append(" | ").Append(Clean(i.Source)).Append(" | ").AppendLine(Clean(i.ValidRange));

        sb.AppendLine();
        sb.AppendLine("3. ASSUMPTIONS");

        for (var n = 0; n < sheet.Assumptions.Count; n++)
        {
            var a = sheet.Assumptions[n];
            sb.Append(CultureInfo.InvariantCulture, $"  A{n + 1}. {Clean(a.Statement)}");

            if (a.Basis is { Length: > 0 })
                sb.Append(" (").Append(Clean(a.Basis)).Append(')');

            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("4. WORKING");

        foreach (var s in sheet.Working)
        {
            sb.Append(CultureInfo.InvariantCulture, $"  [{s.Ordinal}] {Clean(s.Symbol)} = {Clean(s.Formula)}");

            if (s.Clause is { Length: > 0 })
                sb.Append("    « ").Append(Clean(s.Clause));

            sb.AppendLine();

            if (!string.Equals(s.Substitution, s.Formula, StringComparison.Ordinal))
                sb.Append("      = ").AppendLine(Clean(s.Substitution));

            sb.Append("      = ").AppendLine(Clean(s.ResultText));

            if (s.Description is { Length: > 0 })
                sb.Append("        ").AppendLine(Clean(s.Description));
        }

        sb.AppendLine();
        sb.AppendLine("5. RESULTS");

        foreach (var o in sheet.Outputs)
            sb.Append("  ").Append(Clean(o.Symbol)).Append(" = ").Append(Clean(o.ValueText)).Append("    ").AppendLine(Clean(o.Description));

        sb.AppendLine();
        sb.AppendLine("6. CHECKS (governing first)");

        if (sheet.Checks.Count == 0)
            sb.AppendLine("  No pass/fail checks in this calculation.");

        foreach (var c in sheet.Checks)
        {
            var u = double.IsInfinity(c.Utilisation) ? "—" : CalcValue.FormatNumber(c.Utilisation, 3);
            sb.Append("  ").Append(c.Passed ? "PASS" : "FAIL").Append("  ").Append(Clean(c.Name)).Append(": ").Append(Clean(c.DemandText)).Append(" vs ").Append(Clean(c.CapacityText)).Append("  utilisation ").Append(u);

            if (c.Clause is { Length: > 0 })
                sb.Append("  « ").Append(Clean(c.Clause));

            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("7. APPLICABILITY");

        if (sheet.Warnings.Count == 0 && sheet.Blocks.Count == 0)
            sb.AppendLine("  No warnings.");

        foreach (var b in sheet.Blocks)
            sb.Append("  BLOCK   ").Append(Clean(b.RuleId)).Append(": ").AppendLine(Clean(b.Message));

        foreach (var w in sheet.Warnings)
            sb.Append("  WARNING ").Append(Clean(w.RuleId)).Append(": ").AppendLine(Clean(w.Message));

        sb.AppendLine();
        sb.AppendLine("8. VERIFICATION");
        var f = sheet.Footer;
        sb.Append("  Template: ").AppendLine(Clean(f.TemplateReference));
        sb.Append("  Content hash: ").AppendLine(Clean(f.ContentHash));
        sb.Append("  Engine: ").AppendLine(Clean(f.EngineVersion));
        sb.Append(CultureInfo.InvariantCulture, $"  Known-answer cases passed: {f.KnownAnswerPassed}/{f.KnownAnswerTotal}").AppendLine();
        sb.Append("  Test run: ").AppendLine(Date(f.TestRunAt, withTime: true));

        if (f.ApplicationVersion is { Length: > 0 })
            sb.Append("  Application: ").AppendLine(Clean(f.ApplicationVersion));

        return sb.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    /// <summary>Strips the lines that legitimately change between runs (dates, hashes, engine and application versions) so a golden file compares content only.</summary>
    public static string Normalise(string rendered)
    {
        ArgumentNullException.ThrowIfNull(rendered);

        var lines = rendered.Split('\n');
        var kept = new List<string>(lines.Length);

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');

            if (line.StartsWith("  Content hash: ", StringComparison.Ordinal) || line.StartsWith("  Test run: ", StringComparison.Ordinal)
                || line.StartsWith("  Engine: ", StringComparison.Ordinal) || line.StartsWith("  Application: ", StringComparison.Ordinal)
                || line.StartsWith("Author: ", StringComparison.Ordinal) || line.StartsWith("Checker: ", StringComparison.Ordinal) || line.StartsWith("Issued: ", StringComparison.Ordinal))
            {
                continue;
            }

            kept.Add(line);
        }

        return string.Join("\n", kept);
    }

    private static string Status(CalcSheetStatus s) => s.ToString().ToUpperInvariant();

    private static string Date(DateTimeOffset? d, bool withTime = false) => d is null ? "—" : d.Value.UtcDateTime.ToString(withTime ? "yyyy-MM-dd HH:mm 'UTC'" : "yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Control characters never reach the text: they cannot forge a line of the sheet or break the index.</summary>
    public static string Clean(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return string.Empty;

        var sb = new StringBuilder(s.Length);

        foreach (var c in s)
            sb.Append(char.IsControl(c) ? ' ' : c);

        return sb.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
