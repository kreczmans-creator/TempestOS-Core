using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>
/// Renders a migrated legacy record (<see cref="CalcInstance.IsLegacyArchive"/>)
/// as a calculation sheet: the inputs as entered, the recorded intermediates
/// and results, the constraint checks, and the migration notes as
/// assumptions. There is no working: the legacy modules recorded values, not
/// formulae (ADR-0167), and the sheet says so in its method line.
/// </summary>
public static class LegacyArchiveSheet
{
    public const string MethodText = "Archive of a legacy calculation record: inputs, recorded intermediates, results and constraint checks as the module wrote them. The module recorded values, not formulae, so no working is shown; re-enter the inputs in the matching worksheet template for a sheet with working.";

    /// <summary>A recorded number keeps its unit when the worksheet engine knows the symbol; anything else is shown as the text the module wrote.</summary>
    private static CalcValue ValueOf(CalcOutputSnapshot output)
    {
        if (output.Si is { } value)
        {
            try
            {
                return ScalarValue.Of(value, output.Unit ?? string.Empty);
            }
            catch (CalcUnitException)
            {
                // The legacy unit symbol is not one the engine spells; fall through to text.
            }
        }

        return new TextValue(output.Text);
    }

    public static CalcSheet Build(CalcInstance instance, string project, string author, string engineVersion, string? applicationVersion, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (!instance.IsLegacyArchive)
            throw new ArgumentException("Only a migrated legacy record renders as an archive sheet.", nameof(instance));

        var legacyId = instance.TemplateReference[CalcInstance.LegacyReferencePrefix.Length..].Split('@')[0];
        var header = new CalcSheetHeader(project, instance.Name, $"Legacy calculation {legacyId}", instance.Revision, CalcSheetStatus.Draft, author, null, instance.CreatedAt, null, null);

        var inputs = instance.Inputs.Select(i => new CalcSheetInputRow(
            i.Symbol, string.Empty, i.ValueText, i.Unit ?? string.Empty, "migrated (manual)", "as recorded")).ToList();

        var assumptions = (instance.Notes ?? []).Select(n => new CalcAssumption(n, null)).ToList();
        var outputs = instance.Outputs.Select(o => new CalcOutput(o.Symbol, o.Description, ValueOf(o))).ToList();
        var checks = instance.Checks.Select(c => new CalcCheck(c.Name, c.Demand, c.Capacity, c.Utilisation, c.Passed)).ToList();
        var footer = new CalcSheetFooter(instance.TemplateReference, "legacy", engineVersion, 0, 0, now, applicationVersion);

        return new CalcSheet(
            header,
            $"Migrated legacy calculation record '{legacyId}' (ADR-0167).",
            MethodText,
            [],
            inputs,
            assumptions,
            [],
            checks,
            [new CalcNotice("legacy.archive", CalcNoticeSeverity.Warning, "This sheet is an archive of a legacy record; it cannot be checked or issued. Re-enter the inputs in the matching worksheet template.")],
            [],
            outputs,
            footer);
    }
}
