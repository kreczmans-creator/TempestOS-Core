using System.Globalization;
using System.Text;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>How a re-run of a stored calculation on the installed formulas came out.</summary>
public enum CalcRerunOutcome
{
    /// <summary>Every stored output and check came out exactly as stored.</summary>
    Reproduced,

    /// <summary>At least one stored output or check came out differently (or the run was refused): the stored record stands, the installed formulas do not reproduce it.</summary>
    Differs,

    /// <summary>The calculation cannot be re-run here at all (the template is gone, switched off, newer than this build, or nothing was stored to compare).</summary>
    Unavailable,
}

/// <summary>
/// The result of re-running a stored calculation (<see cref="CalcInstanceService.Rerun"/>): what it ran on, whether the stored outputs and checks were
/// reproduced exactly, and the one message that says so in plain English. Nothing is written by a re-run.
/// </summary>
/// <param name="Outcome">Whether the stored record was reproduced.</param>
/// <param name="Message">The sentence to show: names the calculation's version, the version it ran on, and why it could or could not be re-run.</param>
/// <param name="RanOn">The <c>id@version</c> whose formulas ran, or <see langword="null"/> when nothing ran.</param>
/// <param name="Differing">The symbols of the outputs and the names of the checks that did not reproduce (at most the first 20).</param>
/// <param name="OutputsCompared">How many stored outputs were compared.</param>
/// <param name="ChecksCompared">How many stored checks were compared.</param>
public sealed record CalcRerun(CalcRerunOutcome Outcome, string Message, string? RanOn, IReadOnlyList<string> Differing, int OutputsCompared, int ChecksCompared);

/// <summary>What opening a calculation whose own version's formulas are not installed comes to.</summary>
public enum CalcOlderVersionKind
{
    /// <summary>A draft: its inputs are offered as a new sheet on the current version (<see cref="CalcOlderVersionOpening.Upgrade"/>), the draft left as it was.</summary>
    DraftUpgrade,

    /// <summary>A checked, issued or superseded calculation (or a draft with nowhere to go): shown read-only as stored, with the re-run's verdict.</summary>
    StoredRecord,
}

/// <summary>
/// What to show when a calculation is opened whose own version of its template is not installed (ADR-0177): the upgrade of a draft, or the stored record
/// with a sentence on what a re-run on the installed version comes to.
/// </summary>
/// <param name="Instance">The calculation, unchanged.</param>
/// <param name="Kind">Which of the two it is.</param>
/// <param name="Message">The one message to show.</param>
/// <param name="RecordText">The read-only text of the stored record (the issued sheet, or a summary of a checked calculation's stored results); empty for a draft upgrade.</param>
/// <param name="Upgrade">The plan that opens a draft on the current version, for <see cref="CalcOlderVersionKind.DraftUpgrade"/>.</param>
/// <param name="Rerun">The re-run's verdict, for <see cref="CalcOlderVersionKind.StoredRecord"/>.</param>
public sealed record CalcOlderVersionOpening(CalcInstance Instance, CalcOlderVersionKind Kind, string Message, string RecordText, CalcDraftUpgrade? Upgrade, CalcRerun? Rerun);

public sealed partial class CalcInstanceService
{
    /// <summary>The most differing outputs or checks a re-run names.</summary>
    internal const int MaxDifferingListed = 20;

    /// <summary>The most stored rows a summary of a checked calculation's results prints; a longer list says how many more there are.</summary>
    internal const int MaxSummaryRows = 500;

    /// <summary>
    /// What the catalogue makes of a calculation's template reference (ADR-0177): installed as itself, a recorded superseded version, a version the record
    /// does not list, a newer one, switched off or gone.
    /// </summary>
    public CalcVersionResolution ResolveVersion(CalcInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return _catalogue.Resolve(instance.TemplateReference);
    }

    /// <summary>
    /// Re-runs a stored calculation on its own inputs and compares the result with what is stored, writing nothing (DC-14, ADR-0177). It runs on the
    /// calculation's own version when those formulas are installed (the current version, or a frozen copy kept beside it); otherwise on the template's
    /// current version, and then it says <see cref="CalcRerunOutcome.Reproduced"/> only if every stored output and every stored check comes out exactly
    /// as stored. Where it does not, the outcome is <see cref="CalcRerunOutcome.Differs"/> or <see cref="CalcRerunOutcome.Unavailable"/> and the message
    /// says why in a sentence: the stored record is never replaced by a re-run.
    /// </summary>
    public CalcRerun Rerun(CalcInstance instance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var name = $"'{instance.Name}' rev {instance.Revision}";

        if (instance.IsLegacyArchive)
            return Unavailable($"{name} is a migrated legacy record: it has no formulas to re-run.");

        if (instance.Outputs.Count == 0 && instance.Checks.Count == 0)
            return Unavailable($"{name} has no stored outputs or checks to compare a re-run with.");

        var resolution = _catalogue.Resolve(instance.TemplateReference);
        var entry = resolution.Entry ?? (resolution.IsSuperseded ? resolution.Current : null);

        if (entry is null)
        {
            return Unavailable(resolution.State switch
            {
                CalcVersionState.Withheld => $"{name} was made on {instance.TemplateReference}, which this installation has switched off, so it is not re-run here.",
                CalcVersionState.Newer => $"{name} was made on {instance.TemplateReference}, which is newer than the {resolution.Current?.Reference} installed here, so it is not re-run here; open it in a build that has that version.",
                _ => $"{name} was made on {instance.TemplateReference}, a template that is no longer installed, so it cannot be re-run.",
            });
        }

        CalcRunResult result;

        try
        {
            result = CalcRunner.Run(entry.Calculator, instance.InputSet(), _engine, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new CalcRerun(CalcRerunOutcome.Differs, $"{name} cannot be re-run on {entry.Reference}: that version refuses its inputs ({FirstSentence(ex.Message)}). It stays as it was issued.", entry.Reference, [], instance.Outputs.Count, instance.Checks.Count);
        }

        var own = ReferenceEquals(entry, resolution.Entry);
        var ranOn = own ? $"its own version {entry.Reference}" : $"{entry.Reference}, which has replaced {instance.TemplateReference}";

        if (result.IsBlocked)
        {
            return new CalcRerun(CalcRerunOutcome.Differs,
                $"{name} cannot be re-run on {ranOn}: that version blocks it ({FirstSentence(result.Blocks[0].Message)}), so it stays as stored, read-only.{CarryOn(instance, entry, own)}",
                entry.Reference, [], instance.Outputs.Count, instance.Checks.Count);
        }

        var snapshot = Snapshot(instance, result);
        var differing = new List<string>();
        var details = new List<string>();

        foreach (var stored in instance.Outputs)
        {
            var again = snapshot.Outputs.FirstOrDefault(o => string.Equals(o.Symbol, stored.Symbol, StringComparison.Ordinal));

            if (again is null)
            {
                differing.Add(stored.Symbol);
                details.Add($"{stored.Symbol} is no longer given");
            }
            else if (!OutputsAgree(stored, again))
            {
                differing.Add(stored.Symbol);

                // Two numbers that round to the same printed text differ in SI (the value the outputs hash covers), so the sentence shows the SI numbers.
                details.Add(string.Equals(again.Text, stored.Text, StringComparison.Ordinal) && again.Si is { } nowSi && stored.Si is { } wasSi
                    ? $"{stored.Symbol} is {nowSi.ToString("R", CultureInfo.InvariantCulture)} where it was {wasSi.ToString("R", CultureInfo.InvariantCulture)} in SI (both shown as {stored.Text})"
                    : $"{stored.Symbol} is {again.Text} where it was {stored.Text}");
            }
        }

        foreach (var stored in instance.Checks)
        {
            var again = snapshot.Checks.FirstOrDefault(c => string.Equals(c.Name, stored.Name, StringComparison.Ordinal));

            if (again is null)
            {
                differing.Add(stored.Name);
                details.Add($"the check '{stored.Name}' is no longer made");
            }
            else if (again.Passed != stored.Passed || !NumbersAgree(again.Utilisation, stored.Utilisation))
            {
                differing.Add(stored.Name);
                details.Add($"the check '{stored.Name}' reads {CheckVerdict(again)} where it read {CheckVerdict(stored)}");
            }
        }

        // A check the stored record does not have that the version now makes and the calculation fails: not a stored value that moved, but a verdict that would now be refused.
        foreach (var added in snapshot.Checks.Where(c => !c.Passed && !instance.Checks.Any(s => string.Equals(s.Name, c.Name, StringComparison.Ordinal))))
        {
            differing.Add(added.Name);
            details.Add($"it adds the check '{added.Name}', which this calculation does not meet");
        }

        if (differing.Count == 0)
        {
            var counted = $"{Plural(instance.Outputs.Count, "stored output")}{(instance.Checks.Count == 0 ? string.Empty : $" and {Plural(instance.Checks.Count, "stored check")}")}";
            return new CalcRerun(CalcRerunOutcome.Reproduced, $"{name} re-ran on {ranOn}: all {counted} come out exactly as stored.", entry.Reference, [], instance.Outputs.Count, instance.Checks.Count);
        }

        var shown = string.Join("; ", details.Take(3)) + (details.Count > 3 ? $"; and {details.Count - 3} more" : string.Empty);
        return new CalcRerun(CalcRerunOutcome.Differs,
            $"{name} cannot be re-run exactly on {ranOn}: {shown}. It stays as stored, read-only.{CarryOn(instance, entry, own)}",
            entry.Reference, [.. differing.Take(MaxDifferingListed)], instance.Outputs.Count, instance.Checks.Count);
    }

    /// <summary>
    /// What to show when <paramref name="instance"/> is opened and the formulas of its own version are not installed (DC-14): a draft is offered as a
    /// new sheet on the current version with its inputs (the existing upgrade, <see cref="PlanDraftUpgrade"/>); a checked, issued or superseded
    /// calculation is shown as it was stored, read-only, with the verdict of <see cref="Rerun"/>. <see langword="null"/> when there is nothing to
    /// explain: its own version is installed, it is a legacy archive, or its template is switched off (the caller says that).
    /// </summary>
    public CalcOlderVersionOpening? OpenOlderVersion(CalcInstance instance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (instance.IsLegacyArchive || _catalogue.Find(instance.TemplateReference) is not null || _catalogue.FeatureRefusal(instance.TemplateReference) is not null)
            return null;

        if (PlanDraftUpgrade(instance) is { } upgrade)
            return new CalcOlderVersionOpening(instance, CalcOlderVersionKind.DraftUpgrade, upgrade.Message, string.Empty, upgrade, null);

        var resolution = _catalogue.Resolve(instance.TemplateReference);
        var rerun = Rerun(instance, cancellationToken);
        var name = $"'{instance.Name}' rev {instance.Revision}";
        var where = resolution.State switch
        {
            CalcVersionState.Superseded or CalcVersionState.Unrecorded => $"{instance.TemplateReference}, which has been revised to {resolution.Current!.Reference}",
            CalcVersionState.Newer => $"{instance.TemplateReference}, which is newer than the {resolution.Current!.Reference} installed here",
            _ => $"{instance.TemplateReference}, a template that is no longer installed",
        };
        var onward = resolution.IsSuperseded ? instance.Status switch
        {
            CalcInstanceStatus.Issued => $" Revise it to carry its inputs to {resolution.Current!.Reference}.",
            CalcInstanceStatus.Checked => $" Revise it to carry its inputs to {resolution.Current!.Reference}, then check and issue it again.",
            CalcInstanceStatus.Draft => $" Its inputs can be opened as a new sheet on {resolution.Current!.Reference}.",
            _ => string.Empty,
        } : string.Empty;
        var fingerprint = resolution.RecordedHash is { } recorded && !string.Equals(recorded, instance.TemplateHash, StringComparison.Ordinal)
            ? $" Its stored template fingerprint ({CalcTemplateHasher.Short(instance.TemplateHash)}) is not the one recorded for {instance.TemplateReference} ({CalcTemplateHasher.Short(recorded)}): it was made on a build of that version before its content was final."
            : string.Empty;
        var held = instance.Status switch
        {
            CalcInstanceStatus.Issued or CalcInstanceStatus.Superseded => "this is its sheet as it was issued, read-only",
            CalcInstanceStatus.Checked => "its stored results are shown read-only",
            _ => "its stored results are shown read-only",
        };
        var message = $"{name} was made on {where}; {held}. {rerun.Message}{onward}{fingerprint}";
        return new CalcOlderVersionOpening(instance, CalcOlderVersionKind.StoredRecord, message, StoredRecordText(instance), null, rerun);
    }

    /// <summary>
    /// The sentence a refused action gives when the calculation's own version is not installed: what the version became and what to do, or the plain
    /// "no longer registered" when the version is not one the record knows. Names the calculation.
    /// </summary>
    internal string OlderVersionRefusal(CalcInstance instance)
    {
        var resolution = _catalogue.Resolve(instance.TemplateReference);

        // A superseded version, recorded in the manifest or not (made on a build this one has no record of), has a current version to go to.
        return resolution switch
        {
            { IsSuperseded: true, Current: { } current } when instance.Status == CalcInstanceStatus.Draft =>
                $"Calculation '{instance.Name}' was made on {instance.TemplateReference}, which has been revised to {current.Reference}, so it can no longer be edited here; open it to carry its inputs into a new sheet on {current.Reference} (it is left unchanged).",
            { IsSuperseded: true, Current: { } current } =>
                $"Calculation '{instance.Name}' was made on {instance.TemplateReference}, which has been revised to {current.Reference}, so it cannot be issued on that version; revise it to carry its inputs to {current.Reference}, then check and issue it again.",
            { State: CalcVersionState.Newer, Current: { } current } =>
                $"Calculation '{instance.Name}' was made on {instance.TemplateReference}, which is newer than the {current.Reference} installed here; open it in a build that has that version.",
            _ => $"Template '{instance.TemplateReference}' is no longer registered.",
        };
    }

    /// <summary>The read-only text of a stored calculation: the sheet as issued, or for one that was never issued a summary of what it stored.</summary>
    internal static string StoredRecordText(CalcInstance instance)
    {
        if (instance.Issue is { SheetText.Length: > 0 } issue)
            return issue.SheetText;

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"{instance.Name} — rev {instance.Revision} — {instance.Status}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Template {instance.TemplateReference} · fingerprint {CalcTemplateHasher.Short(instance.TemplateHash)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"{Plural(instance.Inputs.Count, "input")} stored (not shown here: open the calculation on its current version to see and edit them).");

        if (instance.CheckedBy is { } checker)
            sb.AppendLine(CultureInfo.InvariantCulture, $"Checked by {WithoutLineBreaks(checker)}{(instance.CheckedAt is { } at ? $" on {at:yyyy-MM-dd}" : string.Empty)}");

        AppendRows(sb, "Outputs", instance.Outputs.Select(o => $"  {WithoutLineBreaks(o.Symbol)} — {WithoutLineBreaks(o.Description)} = {WithoutLineBreaks(o.Text)}"));
        AppendRows(sb, "Checks", instance.Checks.Select(c => $"  {WithoutLineBreaks(c.Name)}: {WithoutLineBreaks(c.Demand)} against {WithoutLineBreaks(c.Capacity)} — {CheckVerdict(c)}"));
        AppendRows(sb, "Warnings", instance.Warnings.Select(w => $"  {WithoutLineBreaks(w.Message)}"));
        AppendRows(sb, "Blocks", instance.Blocks.Select(b => $"  {WithoutLineBreaks(b.Message)}"));
        return sb.ToString();
    }

    private static void AppendRows(StringBuilder sb, string heading, IEnumerable<string> rows)
    {
        var list = rows.Take(MaxSummaryRows + 1).ToList();

        if (list.Count == 0)
            return;

        sb.AppendLine().AppendLine(heading);

        foreach (var row in list.Take(MaxSummaryRows))
            sb.AppendLine(row);

        if (list.Count > MaxSummaryRows)
            sb.AppendLine(CultureInfo.InvariantCulture, $"  … and more (the first {MaxSummaryRows} are listed).");
    }

    private static CalcRerun Unavailable(string message) => new(CalcRerunOutcome.Unavailable, message, null, [], 0, 0);

    private static string CarryOn(CalcInstance instance, CalcCatalogueEntry entry, bool own) =>
        own || instance.Status is CalcInstanceStatus.Superseded or CalcInstanceStatus.Draft ? string.Empty : $" Revise it to carry its inputs to {entry.Reference}.";

    private static string CheckVerdict(CalcCheckSnapshot c) => c.HasUtilisation ? $"{(c.Passed ? "PASS" : "FAIL")} at {c.Utilisation.ToString("R", CultureInfo.InvariantCulture)}" : c.Passed ? "PASS" : "FAIL";

    /// <summary>The same value the output hash covers: the SI number to the last digit, or the text where there is no number.</summary>
    private static bool OutputsAgree(CalcOutputSnapshot a, CalcOutputSnapshot b) =>
        a.Si is { } x && b.Si is { } y ? NumbersAgree(x, y) : a.Si is null && b.Si is null && string.Equals(a.Text, b.Text, StringComparison.Ordinal);

    private static bool NumbersAgree(double a, double b) => a.Equals(b);

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    private static string WithoutLineBreaks(string? text) => (text ?? string.Empty).ReplaceLineEndings(" ");

    private static string FirstSentence(string text)
    {
        var line = WithoutLineBreaks(text).Trim();
        return line.Length <= 200 ? line.TrimEnd('.') : line[..200].TrimEnd('.') + "…";
    }
}
