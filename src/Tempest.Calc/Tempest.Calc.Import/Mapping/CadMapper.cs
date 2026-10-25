using System.Globalization;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Import.Mapping;

public enum CadRowStatus
{
    /// <summary>In the profile and applicable: the input will take the value.</summary>
    Mapped,

    /// <summary>In the profile but not applicable (unit mismatch, an expression, a duplicate, an unknown input). Nothing is applied while any row is refused.</summary>
    Refused,

    /// <summary>In the file but not in the profile: reported, not applied.</summary>
    Unmapped,

    /// <summary>An expression in the file that the profile does not map: shown read-only, never evaluated.</summary>
    Expression,

    /// <summary>In the profile but not in the file.</summary>
    Missing,
}

/// <summary>One row of the preview: the CAD name, what happens to it, the value as read and a message.</summary>
public sealed record CadMappedRow(string CadName, CadRowStatus Status, string? Target, string Value, string Message, string Location);

/// <summary>
/// The outcome of mapping one parse through a profile onto a template: every row, the template's inputs with the mapped
/// values applied (a copy; the draft is not touched), the snapshot to remember, and the reasons Apply is refused.
/// </summary>
public sealed record CadMappingResult(
    IReadOnlyList<CadMappedRow> Rows,
    IReadOnlyList<CalcInputValue> Inputs,
    IReadOnlyList<CadImportedParameter> Snapshot,
    IReadOnlyList<string> Refusals)
{
    public bool CanApply => Refusals.Count == 0 && Rows.Any(r => r.Status == CadRowStatus.Mapped);

    public int Count(CadRowStatus status) => Rows.Count(r => r.Status == status);
}

/// <summary>
/// Maps parsed CAD parameters onto a template's inputs through a profile. Units are checked by dimension against the input's
/// own unit: a mismatch, a value with no unit, an expression, a text where a number is needed, an unknown input or a name
/// assigned twice is refused with a message, and while anything is refused nothing is applied. A name in the file that the
/// profile does not list is reported as unmapped and an expression the profile does not list as read-only, never dropped
/// silently. Nothing is evaluated.
/// </summary>
public static class CadMapper
{
    private const int MaxTextChars = 200;

    public static CadMappingResult Map(CadParseResult parse, CadMappingProfile profile, CalcTemplate template, IReadOnlyList<CalcInputValue> current, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(current);

        var refusals = new List<string>();
        var rows = new List<CadMappedRow>();
        var snapshot = new List<CadImportedParameter>();

        foreach (var problem in profile.Validate())
            refusals.Add(problem);

        if (profile.System is { } system && system != parse.Source.System)
            refusals.Add($"The profile '{profile.Label}' is for {CadImportProvenance.SystemLabel(system)} files, but this file is {CadImportProvenance.SystemLabel(parse.Source.System)}.");

        if (profile.TemplateId is { Length: > 0 } templateId && !string.Equals(templateId, template.Id, StringComparison.Ordinal))
            refusals.Add($"The profile '{profile.Label}' is for the template {templateId}, not {template.Id}.");

        foreach (var notice in parse.Notices.Where(n => n.Severity == CalcNoticeSeverity.Block))
            refusals.Add(notice.Message);

        if (refusals.Count > 0)
            return new CadMappingResult(rows, current, snapshot, refusals);

        var comparer = profile.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var entries = profile.Entries.ToDictionary(e => CadMappingProfile.Normalise(e.CadName), comparer);
        var seen = new HashSet<string>(comparer);
        var counts = parse.Parameters.GroupBy(p => CadMappingProfile.Normalise(p.Name), comparer).ToDictionary(g => g.Key, g => g.ToList(), comparer);
        var working = current.ToDictionary(v => v.Symbol, StringComparer.Ordinal);
        var order = current.Select(v => v.Symbol).ToList();

        foreach (var p in parse.Parameters)
        {
            var key = CadMappingProfile.Normalise(p.Name);

            if (!entries.TryGetValue(key, out var entry))
            {
                rows.Add(new CadMappedRow(p.Name, p.Kind == CadParameterKind.Expression ? CadRowStatus.Expression : CadRowStatus.Unmapped, null, Show(p), p.Kind == CadParameterKind.Expression ? "Expression: shown read-only, never evaluated or imported." : "Not in the profile; not imported.", p.Location));
                snapshot.Add(Snap(p, null));
                continue;
            }

            seen.Add(key);

            if (counts[key].Count > 1)
            {
                var message = $"'{p.Name}' is assigned {counts[key].Count} times in the file ({string.Join(", ", counts[key].Select(x => x.Location))}), so there is no single value to import.";
                rows.Add(new CadMappedRow(p.Name, CadRowStatus.Refused, entry.Target, Show(p), message, p.Location));
                refusals.Add(message);
                snapshot.Add(Snap(p, null));
                continue;
            }

            var (problem, applied) = Apply(p, entry, template, working, order, parse.Source, profile, at);

            if (problem is not null)
            {
                var message = $"{p.Name} ({p.Location}): {problem}";
                rows.Add(new CadMappedRow(p.Name, CadRowStatus.Refused, entry.Target, Show(p), problem, p.Location));
                refusals.Add(message);
                snapshot.Add(Snap(p, null));
            }
            else
            {
                rows.Add(new CadMappedRow(p.Name, CadRowStatus.Mapped, entry.Target, applied!, $"→ {entry.Target}", p.Location));
                snapshot.Add(Snap(p, entry.Target));
            }
        }

        foreach (var entry in profile.Entries)
        {
            if (seen.Contains(CadMappingProfile.Normalise(entry.CadName)))
                continue;

            var message = entry.Required
                ? $"The profile requires '{entry.CadName}', but the file does not hold it."
                : $"'{entry.CadName}' is in the profile but not in the file; '{entry.Target}' is unchanged.";
            rows.Add(new CadMappedRow(entry.CadName, CadRowStatus.Missing, entry.Target, string.Empty, message, string.Empty));

            if (entry.Required)
                refusals.Add(message);
        }

        if (refusals.Count == 0 && !rows.Any(r => r.Status == CadRowStatus.Mapped))
            refusals.Add("None of the names in the file is in the profile, so there is nothing to import.");

        var result = order.Select(s => working[s]).ToList();
        return new CadMappingResult(rows, result, snapshot, refusals);
    }

    // ------------------------------------------------------------------

    private static (string? Problem, string? Shown) Apply(CadParameter p, CadMappingEntry entry, CalcTemplate template, Dictionary<string, CalcInputValue> working, List<string> order, CadSourceInfo source, CadMappingProfile profile, DateTimeOffset at)
    {
        if (p.Kind == CadParameterKind.Expression)
            return ($"is an expression ({Clip(p.Text)}). Expressions are shown read-only and are never evaluated or mapped to an input as a number; remove it from the profile, or have the CAD system export its value.", null);

        if (!CadTarget.TryParse(entry.Target, out var target))
            return ($"the target '{Clip(entry.Target)}' is not valid.", null);

        if (template.FindInput(target.Symbol) is not { } spec)
            return ($"'{target.Symbol}' is not an input of {template.Id}.", null);

        var provenance = CadImportProvenance.Create(source, profile, p.Name, at);

        if (!target.IsCell)
        {
            if (spec.Kind is CalcInputKind.Table or CalcInputKind.Vector)
                return ($"'{target.Symbol}' is a {spec.Kind.ToString().ToLowerInvariant()}; map a cell of an existing row as {target.Symbol}[0].column.", null);

            working.TryGetValue(target.Symbol, out var existing);
            var (problem, value, shown) = ConvertValue(p, entry, spec.Kind, spec.DefaultUnit, spec.Choices, spec.Symbol, existing?.Unit);

            if (problem is not null)
                return (problem, null);

            if (existing is null)
                order.Add(target.Symbol);

            working[target.Symbol] = new CalcInputValue(target.Symbol, value!.Number, value.Unit, value.Text, value.Flag, Provenance: provenance);
            return (null, shown);
        }

        if (spec.Kind != CalcInputKind.Table)
            return ($"'{target.Symbol}' is not a table, so '{entry.Target}' is not a cell.", null);

        if (spec.Columns?.FirstOrDefault(c => string.Equals(c.Key, target.Column, StringComparison.Ordinal)) is not { } column)
            return ($"'{target.Symbol}' has no column '{target.Column}'.", null);

        if (!working.TryGetValue(target.Symbol, out var table) || table.Rows is null || target.Row!.Value >= table.Rows.Count)
            return ($"'{target.Symbol}' has {(working.TryGetValue(target.Symbol, out var t2) ? t2.Rows?.Count ?? 0 : 0)} row(s); there is no row {target.Row} to set. Add the row to the calculation first.", null);

        if (column.Kind is CalcInputKind.Table or CalcInputKind.Vector)
            return ($"column '{target.Column}' cannot take a single value.", null);

        var row = table.Rows[target.Row!.Value];
        row.Cells.TryGetValue(target.Column!, out var oldCell);
        var (cellProblem, cell, cellShown) = ConvertValue(p, entry, column.Kind, column.DefaultUnit, column.Choices, $"{target.Symbol}.{target.Column}", oldCell?.Unit);

        if (cellProblem is not null)
            return (cellProblem, null);

        var cells = new Dictionary<string, CalcCell>(row.Cells, StringComparer.Ordinal)
        {
            [target.Column!] = new CalcCell(cell!.Number, cell.Unit, cell.Text, cell.Flag, provenance),
        };
        var rows = table.Rows.ToList();
        rows[target.Row!.Value] = new CalcRow(cells);
        working[target.Symbol] = table with { Rows = rows };
        return (null, cellShown);
    }

    private sealed record Converted(double? Number, string? Unit, string? Text, bool? Flag);

    private static (string? Problem, Converted? Value, string? Shown) ConvertValue(CadParameter p, CadMappingEntry entry, CalcInputKind kind, string defaultUnit, IReadOnlyList<string>? choices, string label, string? existingUnit)
    {
        switch (kind)
        {
            case CalcInputKind.Quantity:
            case CalcInputKind.Number:
                return ConvertNumber(p, entry, kind, defaultUnit, label, existingUnit);

            case CalcInputKind.Boolean:
                return p.Kind == CadParameterKind.Boolean && p.Flag is { } flag
                    ? (null, new Converted(null, null, null, flag), flag ? "yes" : "no")
                    : ($"is {KindWord(p)}, but '{label}' is a yes/no input.", null, null);

            case CalcInputKind.Choice:
            {
                if (p.Kind != CadParameterKind.Text || p.Text is null)
                    return ($"is {KindWord(p)}, but '{label}' takes one of its listed choices.", null, null);

                var match = choices?.FirstOrDefault(c => string.Equals(c, p.Text.Trim(), StringComparison.OrdinalIgnoreCase));

                return match is null
                    ? ($"'{Clip(p.Text)}' is not one of the choices of '{label}' ({string.Join(", ", (choices ?? []).Take(8))}).", null, null)
                    : (null, new Converted(null, null, match, null), "\"" + match + "\"");
            }

            case CalcInputKind.Text:
                if (p.Kind != CadParameterKind.Text || p.Text is null)
                    return ($"is {KindWord(p)}, but '{label}' is a text input.", null, null);

                return p.Text.Length > MaxTextChars
                    ? ($"the text is longer than {MaxTextChars} characters.", null, null)
                    : (null, new Converted(null, null, p.Text, null), "\"" + p.Text + "\"");

            default:
                return ($"'{label}' cannot take a single imported value.", null, null);
        }
    }

    private static (string? Problem, Converted? Value, string? Shown) ConvertNumber(CadParameter p, CadMappingEntry entry, CalcInputKind kind, string defaultUnit, string label, string? existingUnit)
    {
        if (p.Kind != CadParameterKind.Number || p.Number is not { } number)
            return ($"is {KindWord(p)}, but '{label}' takes a number{(defaultUnit.Length > 0 ? " in " + defaultUnit : string.Empty)}.", null, null);

        if (p.UnitProblem is not null)
            return (p.UnitProblem + " It cannot be mapped.", null, null);

        // The unit the input is shown in: the one the person chose, if it is of the right kind; else the template's default.
        var targetText = kind == CalcInputKind.Number ? string.Empty : defaultUnit;
        var dimensioned = false;

        if (targetText.Length > 0)
        {
            if (!CadUnits.TryParseEngine(targetText, out var defaultParsed))
                return ($"the unit '{targetText}' of '{label}' is not one the engine knows.", null, null);

            dimensioned = true;

            if (existingUnit is { Length: > 0 } && CadUnits.TryParseEngine(existingUnit, out var existingParsed) && existingParsed.Dimension == defaultParsed.Dimension && CadUnits.IsAngle(existingUnit) == CadUnits.IsAngle(targetText))
                targetText = existingUnit;
        }

        var fromText = p.Unit;
        var assumed = false;

        if (fromText is null && entry.AssumedUnit is { Length: > 0 } assumedText)
        {
            if (!CadUnits.TryResolve(assumedText, out var assumedSymbol, out _))
                return ($"the assumed unit '{Clip(assumedText)}' is not one the engine knows.", null, null);

            fromText = assumedSymbol.Length == 0 ? null : assumedSymbol;
            assumed = true;
        }

        var targetIsAngle = CadUnits.IsAngle(targetText);

        if (!dimensioned || targetIsAngle)
        {
            // A dimensionless target: a plain number, a ratio (% and ppm are fractions) or an angle.
            if (fromText is null)
            {
                if (targetIsAngle)
                    return ($"has no unit, but '{label}' is an angle in {targetText}. State the angle unit in the file, or set the assumed unit (deg or rad) in the profile.", null, null);

                return (null, new Converted(number, null, null, null), Engine.Values.CalcValue.FormatNumber(number, 8));
            }

            if (!CadUnits.TryParseEngine(fromText, out var from))
                return ($"the unit '{fromText}' is not one the engine knows.", null, null);

            if (!from.Dimension.IsDimensionless || CadUnits.IsAngle(fromText) != targetIsAngle)
                return ($"is in '{fromText}', but '{label}' takes {(targetIsAngle ? "an angle (" + targetText + ")" : "a plain number")}. The units are not the same kind of quantity, so it is refused.", null, null);

            if (targetIsAngle)
            {
                CadUnits.TryParseEngine(targetText, out var angleTarget);
                var converted = angleTarget.FromSi(from.ToSi(number));
                return Finish(converted, targetText, label);
            }

            return Finish(from.ToSi(number), string.Empty, label, noUnit: true);
        }

        if (fromText is null)
            return ($"has no unit, but '{label}' is in {targetText}. State the unit in the file, or set the assumed unit in the profile.", null, null);

        if (!CadUnits.TryParseEngine(fromText, out var fromUnit))
            return ($"the unit '{fromText}' is not one the engine knows.", null, null);

        CadUnits.TryParseEngine(targetText, out var toUnit);

        if (fromUnit.Dimension != toUnit.Dimension)
            return ($"is in '{fromText}', but '{label}' takes {targetText}. The units are not the same kind of quantity, so it is refused.", null, null);

        var value = string.Equals(fromText, targetText, StringComparison.Ordinal) ? number : toUnit.FromSi(fromUnit.ToSi(number));
        var shown = Finish(value, targetText, label);

        if (assumed && shown.Problem is null)
            return (null, shown.Value, shown.Shown + $" (unit {fromText} assumed by the profile)");

        return shown;
    }

    private static (string? Problem, Converted? Value, string? Shown) Finish(double value, string unit, string label, bool noUnit = false)
    {
        if (!double.IsFinite(value))
            return ($"does not convert to a finite number for '{label}'.", null, null);

        var text = Engine.Values.CalcValue.FormatNumber(value, 8) + (unit.Length > 0 ? " " + unit : string.Empty);
        return (null, new Converted(value, noUnit || unit.Length == 0 ? null : unit, null, null), text);
    }

    private static string KindWord(CadParameter p) => p.Kind switch
    {
        CadParameterKind.Number => "a number",
        CadParameterKind.Text => "a text",
        CadParameterKind.Boolean => "a yes/no value",
        _ => "an expression",
    };

    private static string Show(CadParameter p) => p.Kind switch
    {
        CadParameterKind.Number => p.Number is { } n ? Engine.Values.CalcValue.FormatNumber(n, 8) + (p.Unit is { Length: > 0 } u ? " " + u : p.RawUnit is { Length: > 0 } r ? " " + r : string.Empty) : "—",
        CadParameterKind.Boolean => p.Flag == true ? "yes" : "no",
        CadParameterKind.Text => "\"" + Clip(p.Text) + "\"",
        _ => "= " + Clip(p.Text),
    };

    private static CadImportedParameter Snap(CadParameter p, string? target)
    {
        double? si = null;

        if (p.Number is { } n)
        {
            si = n;

            if (p.Unit is { Length: > 0 } unit && CadUnits.TryParseEngine(unit, out var parsed))
                si = parsed.ToSi(n);
        }

        return new CadImportedParameter(p.Name, p.Kind, target, p.Number, p.Unit, si, p.Text, p.Flag);
    }

    private static string Clip(string? s) => s is null ? string.Empty : s.Length > 60 ? s[..60] + "…" : s;
}
