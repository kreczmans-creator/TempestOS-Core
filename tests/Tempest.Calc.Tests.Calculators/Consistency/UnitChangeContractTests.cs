using System.Runtime.CompilerServices;
using System.Text.Json;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Consistency;

/// <summary>
/// C-2 phases B and C changed the default display unit of inputs and table columns across the catalogue (a moment in kN·m instead of N·m, a pressure
/// in kPa instead of bar, a power in kW instead of W). A bare stored number is read in the default unit of the version it was recorded on
/// (ADR-0177 review, <see cref="CalcInputUnits"/>), but only when the input declares the change with <see cref="CalcInputSpec.UnitChangedAt"/> and
/// <see cref="CalcInputSpec.UnitWas"/>. <c>DefaultUnitsBaseline.json</c> holds every input's and column's default unit at 02260766, the base of those
/// branches, with the template version it had then; this contract finds every unit that differs from it (through an input's aliases, so a renamed
/// input is still compared) and requires the declaration to be there, to name the old unit, and to read a bare number stored on the base version
/// as the old unit — the same SI the number meant then. It also refuses a declaration that no change of unit backs.
/// </summary>
public sealed class UnitChangeContractTests
{
    private sealed record Baseline(string version, Dictionary<string, string> units);

    private static string BaselinePath([CallerFilePath] string callerFilePath = "") => Path.Combine(Path.GetDirectoryName(callerFilePath)!, "DefaultUnitsBaseline.json");

    private static readonly Lazy<Dictionary<string, Baseline>> Base = new(() => JsonSerializer.Deserialize<Dictionary<string, Baseline>>(File.ReadAllText(BaselinePath()))!);

    /// <summary>Whether two unit texts are the same unit (the same dimension and the same size), however they are spelled.</summary>
    private static bool SameUnit(string a, string b) =>
        string.Equals(a, b, StringComparison.Ordinal) || UnitCatalog.TryParse(a, out var ua, out _) && UnitCatalog.TryParse(b, out var ub, out _) && ua.Dimension == ub.Dimension && Math.Abs(ua.ToSi(1) / ub.ToSi(1) - 1) < 1e-12;

    /// <summary>The baseline unit of an input (or <c>table.column</c>), looked up by its symbol or by any of the input's former symbols.</summary>
    private static string? BaselineUnit(Baseline baseline, CalcInputSpec input, string? columnKey)
    {
        foreach (var name in new[] { input.Symbol }.Concat(input.Aliases ?? []))
        {
            if (baseline.units.TryGetValue(columnKey is null ? name : $"{name}.{columnKey}", out var unit))
                return unit;
        }

        return null;
    }

    private static IEnumerable<(string Symbol, string Now, string? Was, string? ChangedAt, bool Declared, string? BaselineUnit)> Units(CalcTemplate template, Baseline baseline)
    {
        foreach (var i in template.Inputs)
        {
            if (i.Kind is CalcInputKind.Quantity or CalcInputKind.Vector && i.DefaultUnit.Length > 0)
                yield return (i.Symbol, i.DefaultUnit, i.UnitWas, i.UnitChangedAt, i.HasUnitChange, BaselineUnit(baseline, i, null));

            foreach (var c in (i.Columns ?? []).Where(c => c.Kind == CalcInputKind.Quantity && c.DefaultUnit.Length > 0))
                yield return ($"{i.Symbol}.{c.Key}", c.DefaultUnit, c.UnitWas, c.UnitChangedAt, c.HasUnitChange, BaselineUnit(baseline, i, c.Key));
        }
    }

    [Fact]
    public void The_Baseline_Covers_Every_Shipped_Template()
    {
        Assert.Equal(AllCalculators.All.Select(c => c.Template.Id).Order(StringComparer.Ordinal), Base.Value.Keys.Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(AllCalculators.Ids), MemberType = typeof(AllCalculators))]
    public void Every_Input_Or_Column_Whose_Default_Unit_Changed_Since_The_Base_Declares_It_And_Reads_A_Bare_Number_Of_The_Base_Version_In_The_Old_Unit(string id)
    {
        var template = AllCalculators.Get(id).Template;
        var baseline = Base.Value[id];
        var problems = new List<string>();

        foreach (var (symbol, now, was, changedAt, declared, before) in Units(template, baseline))
        {
            if (before is null)
            {
                // A new input, or one with no unit on the base: it has no bare numbers from before to read.
                if (declared)
                    problems.Add($"{symbol} declares a unit change at {changedAt} but the base had no such input (a new input needs no declaration)");

                continue;
            }

            if (SameUnit(before, now))
            {
                if (declared)
                    problems.Add($"{symbol} declares a unit change at {changedAt} from {was}, but its default unit is {now} on the base too");

                continue;
            }

            if (!declared)
            {
                problems.Add($"{symbol}: the default unit was {before} on {id}@{baseline.version} and is {now} now, with no UnitChangedAt/UnitWas; a bare stored number would be read in {now}");
                continue;
            }

            if (was is null || !SameUnit(was, before))
                problems.Add($"{symbol}: UnitWas is '{was}' but the unit on {id}@{baseline.version} was {before}");

            if (CalcTemplateVersion.Compare(changedAt!, baseline.version) <= 0)
                problems.Add($"{symbol}: UnitChangedAt {changedAt} is not after the base version {baseline.version}");

            if (CalcTemplateVersion.Compare(changedAt!, template.Version) > 0)
                problems.Add($"{symbol}: UnitChangedAt {changedAt} is after the template's version {template.Version}");

            // A bare number recorded on the base version reads as the old unit: the SI it meant then. On the current version it reads as the new unit.
            var read = CalcInputUnits.TryResolve(symbol, now, changedAt, was, givenUnit: null, madeOn: baseline.version, out var unitThen, out var refusal);

            if (!read || refusal is not null || !SameUnit(unitThen, before))
                problems.Add($"{symbol}: a bare number stored on {id}@{baseline.version} reads as {unitThen} ({refusal ?? "no refusal"}), not {before}");
            else if (UnitCatalog.TryParse(unitThen, out var then, out _) && UnitCatalog.TryParse(before, out var old, out _) && Math.Abs(then.ToSi(1) - old.ToSi(1)) > 1e-12 * Math.Abs(old.ToSi(1)))
                problems.Add($"{symbol}: a bare 1 stored on the base binds to {then.ToSi(1)} SI, not the {old.ToSi(1)} SI it meant");

            Assert.True(CalcInputUnits.TryResolve(symbol, now, changedAt, was, givenUnit: null, madeOn: template.Version, out var unitNow, out _));
            Assert.True(SameUnit(unitNow, now), $"{symbol}: a bare number on the current version reads as {unitNow}, not {now}");
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}
