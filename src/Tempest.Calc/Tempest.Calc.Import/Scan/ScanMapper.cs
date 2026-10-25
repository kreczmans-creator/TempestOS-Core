using System.Globalization;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Import.Cad;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Import.Scan;

public enum ScanRowStatus
{
    /// <summary>In the profile and applicable: the input takes the fitted value.</summary>
    Mapped,

    /// <summary>In the profile but not applicable (too few parts, a unit mismatch, an unknown input, several stages). Nothing is applied while any row is refused.</summary>
    Refused,

    /// <summary>In the tables but not in the profile: reported, not applied.</summary>
    Unmapped,

    /// <summary>In the profile but not in the tables.</summary>
    Missing,
}

/// <summary>One row of the preview: the feature and stage, what happens to it, the value and a message.</summary>
public sealed record ScanMappedRow(string Feature, string Stage, ScanRowStatus Status, string? Target, string Value, string Message);

/// <summary>
/// The outcome of pushing the fitted features of one analysis into a template: every row, the template's inputs with the fitted values applied (a copy;
/// the draft is not touched), the snapshot to remember, the reasons Apply is refused, and the warnings the statistics raised for the features that were pushed.
/// </summary>
public sealed record ScanMappingResult(
    IReadOnlyList<ScanMappedRow> Rows,
    IReadOnlyList<CalcInputValue> Inputs,
    IReadOnlyList<ScanImportedFeature> Snapshot,
    IReadOnlyList<string> Refusals,
    IReadOnlyList<string> Warnings)
{
    public bool CanApply => Refusals.Count == 0 && Rows.Any(r => r.Status == ScanRowStatus.Mapped);

    public int Count(ScanRowStatus status) => Rows.Count(r => r.Status == status);
}

/// <summary>
/// Pushes fitted normal distributions into the inputs of a calculation, through a scan profile, with the values' units checked by dimension.
/// <list type="bullet">
/// <item><description><b>Tolerance-stack contributor</b> (<c>contributors[2]</c>, the M1 table): the row keeps its name and direction. Its nominal becomes the scan's nominal (kept if the
/// scan states none); its mean shift becomes the fitted mean minus that nominal; its band becomes ±kσ about the shifted mean, with the stack's own k, so the stack reads back exactly σ;
/// its distribution becomes normal.</description></item>
/// <item><description><b>Measured dimension</b> (<c>measured{D_g}</c>, the F3 table): the row for that dimension takes the fitted mean and σ (and the normal distribution); the row is added if there is none.</description></item>
/// <item><description><b>A single input</b> (<c>D</c>, <c>D:sigma</c>): the mean (or the stated statistic).</description></item>
/// </list>
/// A feature with fewer than two parts, a unit of another kind, a table without the needed columns, an unknown input or a feature measured at several stages
/// (without the profile naming one) is refused with a message, and while anything is refused nothing is applied. A feature in the tables that the profile does not list is
/// reported as unmapped, never dropped silently.
/// </summary>
public static class ScanMapper
{
    public static ScanMappingResult Map(ScanAnalysis analysis, CalcTemplate template, IReadOnlyList<CalcInputValue> current, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(current);

        var profile = analysis.Profile;
        var refusals = new List<string>();
        var rows = new List<ScanMappedRow>();
        var snapshot = new List<ScanImportedFeature>();
        var warnings = new List<string>();

        foreach (var problem in profile.Validate())
            refusals.Add(problem);

        if (profile.TemplateId is { Length: > 0 } templateId && !string.Equals(templateId, template.Id, StringComparison.Ordinal))
            refusals.Add($"The profile '{profile.Label}' is for the template {templateId}, not {template.Id}.");

        foreach (var notice in analysis.Data.Notices.Where(n => n.Severity == CalcNoticeSeverity.Block))
            refusals.Add(notice.Message);

        if (!CadUnits.TryResolve(profile.Unit, out var unitSymbol, out var scanUnit) || unitSymbol.Length == 0)
            refusals.Add($"The profile's unit '{profile.Unit}' is not a unit the engine knows.");

        if (refusals.Count > 0)
            return new ScanMappingResult(rows, current, snapshot, refusals, warnings);

        var comparer = profile.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var working = current.ToDictionary(v => v.Symbol, StringComparer.Ordinal);
        var order = current.Select(v => v.Symbol).ToList();
        var matched = new HashSet<int>();
        var context = new Context(analysis, template, working, order, scanUnit, unitSymbol, at);

        foreach (var entry in profile.Features)
        {
            var feature = ScanProfile.Normalise(entry.Feature);
            var candidates = new List<int>();

            for (var i = 0; i < analysis.Stats.Count; i++)
            {
                var s = analysis.Stats[i];

                if (!string.Equals(ScanProfile.Normalise(s.Feature), feature, comparer))
                    continue;

                if (entry.Stage is not null && !string.Equals(ScanProfile.Normalise(s.Stage), ScanProfile.Normalise(entry.Stage), comparer))
                    continue;

                candidates.Add(i);
            }

            if (candidates.Count == 0)
            {
                var message = entry.Required
                    ? $"The profile requires '{entry.Feature}'{(entry.Stage is null ? string.Empty : " at stage '" + entry.Stage + "'")}, but the tables do not hold it."
                    : $"'{entry.Feature}' is in the profile but not in the tables; '{entry.Target}' is unchanged.";
                rows.Add(new ScanMappedRow(entry.Feature, entry.Stage ?? string.Empty, ScanRowStatus.Missing, entry.Target, string.Empty, message));

                if (entry.Required)
                    refusals.Add(message);

                continue;
            }

            foreach (var c in candidates)
                matched.Add(c);

            if (candidates.Count > 1)
            {
                var stages = string.Join(", ", candidates.Select(i => "'" + analysis.Stats[i].StageLabel + "'"));
                var message = $"'{entry.Feature}' is measured at {candidates.Count} stages ({stages}); name the stage in the profile entry so one population is fitted.";
                rows.Add(new ScanMappedRow(entry.Feature, string.Empty, ScanRowStatus.Refused, entry.Target, string.Empty, message));
                refusals.Add(message);
                continue;
            }

            var stat = analysis.Stats[candidates[0]];
            context.Pushed.Clear();
            var (problemText, shown) = Apply(context, stat, entry);

            if (problemText is not null)
            {
                var message = $"{stat.Feature} ({stat.StageLabel}): {problemText}";
                rows.Add(new ScanMappedRow(stat.Feature, stat.Stage, ScanRowStatus.Refused, entry.Target, Describe(stat, unitSymbol), problemText));
                refusals.Add(message);
                continue;
            }

            rows.Add(new ScanMappedRow(stat.Feature, stat.Stage, ScanRowStatus.Mapped, entry.Target, shown!, $"→ {entry.Target}"));
            snapshot.Add(Snap(stat, entry.Target, unitSymbol, context.Pushed.ToList()));

            foreach (var w in stat.Warnings)
                warnings.Add($"{stat.Feature} ({stat.StageLabel}): {w}");
        }

        for (var i = 0; i < analysis.Stats.Count; i++)
        {
            if (matched.Contains(i))
                continue;

            var s = analysis.Stats[i];
            rows.Add(new ScanMappedRow(s.Feature, s.Stage, ScanRowStatus.Unmapped, null, Describe(s, unitSymbol), "Not in the profile; not imported."));
            snapshot.Add(Snap(s, null, unitSymbol));
        }

        if (refusals.Count == 0 && !rows.Any(r => r.Status == ScanRowStatus.Mapped))
            refusals.Add(profile.Features.Count == 0
                ? "The profile maps no feature to an input, so there is nothing to import (the statistics are shown)."
                : "None of the profile's features is in the tables, so there is nothing to import.");

        var result = order.Select(s => working[s]).ToList();
        return new ScanMappingResult(rows, result, snapshot, refusals, warnings);
    }

    // ------------------------------------------------------------------

    private sealed record Context(ScanAnalysis Analysis, CalcTemplate Template, Dictionary<string, CalcInputValue> Working, List<string> Order, Unit Scan, string ScanSymbol, DateTimeOffset At)
    {
        public List<ScanPushedValue> Pushed { get; } = [];
    }

    private static (string? Problem, string? Shown) Apply(Context c, ScanFeatureStats stat, ScanFeatureEntry entry)
    {
        if (!stat.Computable)
            return ($"cannot be fitted: {(stat.Warnings.Count > 0 ? stat.Warnings[^1] : "too few usable parts")}", null);

        if (!ScanTarget.TryParse(entry.Target, out var target))
            return ($"the target '{Clip(entry.Target)}' is not valid.", null);

        if (c.Template.FindInput(target.Symbol) is not { } spec)
            return ($"'{target.Symbol}' is not an input of {c.Template.Id}.", null);

        var profile = c.Analysis.Profile;
        var settings = c.Analysis.Settings;
        var provenance = ScanImportProvenance.Create(
            new ScanProvenanceSource(profile.SoftwareLabel, settings.SoftwareVersion, stat.Stage.Length == 0 ? null : stat.Stage, stat.SerialSummary, settings.Alignment, stat.SetSha256),
            profile, stat.Feature, c.At);
        var shownText = Describe(stat, c.ScanSymbol);

        switch (target.Kind)
        {
            case ScanTargetKind.Scalar:
                return ApplyScalar(c, stat, target, spec, provenance, shownText);
            case ScanTargetKind.StackRow:
                return ApplyStackRow(c, stat, target, spec, provenance, shownText);
            default:
                return ApplyMeasured(c, stat, target, spec, provenance, shownText);
        }
    }

    private static (string? Problem, string? Shown) ApplyScalar(Context c, ScanFeatureStats stat, ScanTarget target, CalcInputSpec spec, CalcProvenance provenance, string shown)
    {
        if (spec.Kind is not (CalcInputKind.Quantity or CalcInputKind.Number))
            return ($"'{target.Symbol}' is a {spec.Kind.ToString().ToLowerInvariant()} and takes a statistic only when it is a number or a quantity; map a row of a table as {target.Symbol}[0] or {target.Symbol}{{key}}.", null);

        c.Working.TryGetValue(target.Symbol, out var existing);
        var dimensioned = spec.Kind == CalcInputKind.Quantity && spec.DefaultUnit.Length > 0;
        string? targetText = null;
        Unit targetUnit = Unit.One;

        if (dimensioned)
        {
            if (!CadUnits.TryParseEngine(spec.DefaultUnit, out var defaultUnit))
                return ($"the unit '{spec.DefaultUnit}' of '{target.Symbol}' is not one the engine knows.", null);

            targetText = spec.DefaultUnit;
            targetUnit = defaultUnit;

            if (existing?.Unit is { Length: > 0 } eu && CadUnits.TryParseEngine(eu, out var existingUnit) && existingUnit.Dimension == defaultUnit.Dimension && CadUnits.IsAngle(eu) == CadUnits.IsAngle(spec.DefaultUnit))
            {
                targetText = eu;
                targetUnit = existingUnit;
            }
        }

        var kindProblem = DimensionProblem(c, targetUnit, targetText, target.Symbol, dimensioned);

        if (kindProblem is not null)
            return (kindProblem, null);

        double value;

        switch (target.Stat)
        {
            case ScanStat.Mean:
                value = Convert(c.Scan, targetUnit, stat.Mean);
                break;
            case ScanStat.Sigma:
                value = Scale(c.Scan, targetUnit, stat.Sigma);
                break;
            case ScanStat.Min:
                value = Convert(c.Scan, targetUnit, stat.Min);
                break;
            case ScanStat.Max:
                value = Convert(c.Scan, targetUnit, stat.Max);
                break;
            default:
                if (stat.Nominal is not { } nominal)
                    return ("the table states no nominal for this feature, so its nominal cannot be pushed.", null);

                value = Convert(c.Scan, targetUnit, nominal);
                break;
        }

        if (!double.IsFinite(value))
            return ($"does not convert to a finite number for '{target.Symbol}'.", null);

        if (existing is null)
            c.Order.Add(target.Symbol);

        c.Working[target.Symbol] = new CalcInputValue(target.Symbol, value, dimensioned ? targetText : null, Provenance: provenance);
        c.Pushed.Add(new ScanPushedValue(target.Symbol, value, dimensioned ? targetText : null, null));
        return (null, $"{Format(value)}{(dimensioned ? " " + targetText : string.Empty)} ({target.Stat.ToString().ToLowerInvariant()} of {shown})");
    }

    private static (string? Problem, string? Shown) ApplyStackRow(Context c, ScanFeatureStats stat, ScanTarget target, CalcInputSpec spec, CalcProvenance provenance, string shown)
    {
        if (spec.Kind != CalcInputKind.Table)
            return ($"'{target.Symbol}' is not a table, so '{target}' is not a row.", null);

        var columns = spec.Columns ?? [];
        CalcColumnSpec? Col(string key) => columns.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.Ordinal));

        if (Col("nominal") is not { } nominalCol || Col("upper") is not { } upperCol || Col("lower") is not { } lowerCol)
            return ($"'{target.Symbol}' has no nominal, upper and lower columns, so it does not take a fitted contributor; map a measured-dimension table as {target.Symbol}{{key}} instead.", null);

        if (!c.Working.TryGetValue(target.Symbol, out var table) || table.Rows is null || target.Row!.Value >= table.Rows.Count)
            return ($"'{target.Symbol}' has {(c.Working.TryGetValue(target.Symbol, out var t2) ? t2.Rows?.Count ?? 0 : 0)} row(s); there is no row {target.Row} to set. Add the contributor to the calculation first.", null);

        var row = table.Rows[target.Row!.Value];

        if (row.Cells.TryGetValue("kind", out var kindCell) && kindCell.Text is { Length: > 0 } kindText && !string.Equals(kindText, "size", StringComparison.OrdinalIgnoreCase))
            return ($"row {target.Row} is a {kindText} contributor; a fitted size distribution is for a size contributor.", null);

        var unitProblem = CellUnit(c, nominalCol, row, out var tu, out var tuText);

        if (unitProblem is not null)
            return (unitProblem, null);

        // The cells that take the band must be in the same dimension; read each one's unit (the stack's columns are all lengths).
        if (!ColumnUnit(upperCol, row, out var upperUnit, out var upperText) || !ColumnUnit(lowerCol, row, out var lowerUnit, out var lowerText))
            return ("the band columns have no unit the engine knows.", null);

        var k = 3.0;

        if (c.Working.TryGetValue("k", out var kValue) && kValue.Number is { } kn)
            k = kn;
        else if (c.Template.FindInput("k")?.Default is { } kd)
            k = kd;

        if (!(k > 0) || !double.IsFinite(k))
            return ("the stack's standard deviations per half-band (k) is not a positive number.", null);

        var meanT = Convert(c.Scan, tu, stat.Mean);
        double nominalT;
        var setNominal = false;

        if (stat.Nominal is { } scanNominal && !stat.DeviationOnly)
        {
            nominalT = Convert(c.Scan, tu, scanNominal);
            setNominal = true;
        }
        else if (stat.DeviationOnly)
        {
            // Deviations from nominal: the population is the deviation itself, and the contributor's nominal stays as it is.
            if (!row.Cells.TryGetValue("nominal", out var oldNominal) || oldNominal.Number is null)
                return ("the table states only deviations and the contributor has no nominal to add them to.", null);

            nominalT = oldNominal.Number.Value;
            meanT = nominalT + Scale(c.Scan, tu, stat.Mean);
        }
        else
        {
            if (!row.Cells.TryGetValue("nominal", out var oldNominal) || oldNominal.Number is null)
                return ("the table states no nominal and the contributor has none.", null);

            nominalT = oldNominal.Number.Value;
        }

        var shift = meanT - nominalT;
        var half = k * Scale(c.Scan, tu, stat.Sigma);
        var cells = new Dictionary<string, CalcCell>(row.Cells, StringComparer.Ordinal);

        if (setNominal)
            cells["nominal"] = new CalcCell(nominalT, tuText, Provenance: provenance);

        cells["upper"] = new CalcCell(Scale(tu, upperUnit, half), upperText, Provenance: provenance);
        cells["lower"] = new CalcCell(-Scale(tu, lowerUnit, half), lowerText, Provenance: provenance);

        if (Col("shift") is { } shiftCol)
        {
            if (!ColumnUnit(shiftCol, row, out var shiftUnit, out var shiftText))
                return ("the shift column has no unit the engine knows.", null);

            cells["shift"] = new CalcCell(Scale(tu, shiftUnit, shift), shiftText, Provenance: provenance);
        }
        else if (Math.Abs(shift) > 1e-12 * Math.Max(1, Math.Abs(meanT)))
        {
            return ($"'{target.Symbol}' has no mean-shift column, but the fitted mean differs from the nominal by {Format(shift)} {tuText}; add a shift column or push the statistics into single inputs.", null);
        }

        if (Col("distribution") is { } distCol)
        {
            var normal = distCol.Choices?.FirstOrDefault(x => string.Equals(x, "normal", StringComparison.OrdinalIgnoreCase));

            if (normal is null)
                return ($"the distribution column of '{target.Symbol}' does not offer a normal distribution.", null);

            cells["distribution"] = new CalcCell(Text: normal, Provenance: provenance);
        }

        foreach (var key in new[] { "nominal", "upper", "lower", "shift", "distribution" })
        {
            if (cells.TryGetValue(key, out var written) && ReferenceEquals(written.Provenance, provenance))
                c.Pushed.Add(new ScanPushedValue($"{target.Symbol}[{target.Row.Value}].{key}", written.Number, written.Number is null ? null : written.Unit, written.Text));
        }

        var newRows = table.Rows.ToList();
        newRows[target.Row.Value] = new CalcRow(cells);
        c.Working[target.Symbol] = table with { Rows = newRows };

        return (null, $"mean {Format(meanT)} {tuText}, ±{k.ToString("0.##", CultureInfo.InvariantCulture)}σ band {Format(half)} {tuText}, shift {Format(shift)} {tuText} ({shown})");
    }

    private static (string? Problem, string? Shown) ApplyMeasured(Context c, ScanFeatureStats stat, ScanTarget target, CalcInputSpec spec, CalcProvenance provenance, string shown)
    {
        if (spec.Kind != CalcInputKind.Table)
            return ($"'{target.Symbol}' is not a table, so '{target}' is not a measured row.", null);

        var columns = spec.Columns ?? [];
        CalcColumnSpec? Col(string key) => columns.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.Ordinal));

        if (Col("dim") is not { } dimCol || Col("mean") is not { } meanCol || Col("sigma") is not { } sigmaCol)
            return ($"'{target.Symbol}' has no dim, mean and sigma columns, so it does not take a measured dimension; map a tolerance-stack table as {target.Symbol}[0] instead.", null);

        var key = target.Key!;

        if (dimCol.Choices is { Count: > 0 } choices && !choices.Contains(key, StringComparer.Ordinal))
            return ($"'{key}' is not one of the dimensions of '{target.Symbol}' ({string.Join(", ", choices.Take(10))}).", null);

        if (stat.DeviationOnly)
            return ("the table states only deviations, so there is no measured dimension to push (map a nominal and the actual value).", null);

        c.Working.TryGetValue(target.Symbol, out var table);
        var rows = (table?.Rows ?? []).ToList();
        var index = rows.FindIndex(r => string.Equals(r.Get("dim")?.Text, key, StringComparison.Ordinal));
        var row = index >= 0 ? rows[index] : CalcRow.Of();

        if (index < 0 && rows.Count >= spec.MaxRows)
            return ($"'{target.Symbol}' already has {rows.Count} rows, the most it takes.", null);

        var unitProblem = CellUnit(c, meanCol, row, out var tu, out var tuText);

        if (unitProblem is not null)
            return (unitProblem, null);

        if (!ColumnUnit(sigmaCol, row, out var sigmaUnit, out var sigmaText))
            return ("the sigma column has no unit the engine knows.", null);

        var cells = new Dictionary<string, CalcCell>(row.Cells, StringComparer.Ordinal)
        {
            ["dim"] = new CalcCell(Text: key),
            ["mean"] = new CalcCell(Convert(c.Scan, tu, stat.Mean), tuText, Provenance: provenance),
            ["sigma"] = new CalcCell(Scale(c.Scan, sigmaUnit, stat.Sigma), sigmaText, Provenance: provenance),
        };

        if (Col("dist") is { } distCol)
        {
            var normal = distCol.Choices?.FirstOrDefault(x => string.Equals(x, "normal", StringComparison.OrdinalIgnoreCase));

            if (normal is null)
                return ($"the distribution column of '{target.Symbol}' does not offer a normal distribution.", null);

            cells["dist"] = new CalcCell(Text: normal, Provenance: provenance);
        }

        var newRow = new CalcRow(cells);

        foreach (var column in new[] { "mean", "sigma", "dist" })
        {
            if (cells.TryGetValue(column, out var written) && ReferenceEquals(written.Provenance, provenance))
                c.Pushed.Add(new ScanPushedValue($"{target.Symbol}{{{key}}}.{column}", written.Number, written.Number is null ? null : written.Unit, written.Text));
        }

        if (index >= 0)
            rows[index] = newRow;
        else
            rows.Add(newRow);

        if (table is null)
            c.Order.Add(target.Symbol);

        c.Working[target.Symbol] = table is null
            ? new CalcInputValue(target.Symbol, Rows: rows, Provenance: provenance)
            : table with { Rows = rows };

        return (null, $"{key}: mean {Format(cells["mean"].Number!.Value)} {tuText}, σ {Format(cells["sigma"].Number!.Value)} {sigmaText} ({shown})");
    }

    // ------------------------------------------------------------------

    /// <summary>The unit a table cell is written in: the existing cell's unit if it is of the right kind, else the column's default.</summary>
    private static string? CellUnit(Context c, CalcColumnSpec column, CalcRow row, out Unit unit, out string text)
    {
        unit = Unit.One;
        text = string.Empty;

        if (!ColumnUnit(column, row, out unit, out text))
            return $"the unit of column '{column.Key}' is not one the engine knows.";

        return DimensionProblem(c, unit, text, column.Key, dimensioned: true);
    }

    private static bool ColumnUnit(CalcColumnSpec column, CalcRow row, out Unit unit, out string text)
    {
        unit = Unit.One;
        text = column.DefaultUnit;

        if (column.DefaultUnit.Length == 0 || !CadUnits.TryParseEngine(column.DefaultUnit, out var defaultUnit))
            return false;

        unit = defaultUnit;

        if (row.Cells.TryGetValue(column.Key, out var cell) && cell.Unit is { Length: > 0 } eu && CadUnits.TryParseEngine(eu, out var existing) && existing.Dimension == defaultUnit.Dimension && CadUnits.IsAngle(eu) == CadUnits.IsAngle(column.DefaultUnit))
        {
            unit = existing;
            text = eu;
        }

        return true;
    }

    private static string? DimensionProblem(Context c, Unit target, string? targetText, string label, bool dimensioned)
    {
        if (!dimensioned)
        {
            if (!c.Scan.Dimension.IsDimensionless || CadUnits.IsAngle(c.ScanSymbol))
                return $"is in {c.ScanSymbol}, but '{label}' takes a plain number. The units are not the same kind of quantity, so it is refused.";

            return null;
        }

        if (c.Scan.Dimension != target.Dimension || CadUnits.IsAngle(c.ScanSymbol) != CadUnits.IsAngle(targetText))
            return $"is in {c.ScanSymbol}, but '{label}' takes {targetText}. The units are not the same kind of quantity, so it is refused.";

        return null;
    }

    private static double Convert(Unit from, Unit to, double value) => to.FromSi(from.ToSi(value));

    /// <summary>A spread or width converted between two scale units (no offset).</summary>
    private static double Scale(Unit from, Unit to, double value) => value * from.Factor / to.Factor;

    private static ScanImportedFeature Snap(ScanFeatureStats s, string? target, string unit, IReadOnlyList<ScanPushedValue>? pushed = null) =>
        new(s.Feature, s.Stage, target, s.Count, s.Mean, s.Sigma, s.Cpk, s.Ppk, s.Nominal, s.SetSha256, unit, pushed ?? []);

    private static string Describe(ScanFeatureStats s, string unit) =>
        s.Computable
            ? string.Create(CultureInfo.InvariantCulture, $"n = {s.Count}, mean {Format(s.Mean)} {unit}, σ {Format(s.Sigma)} {unit}{(s.Cpk is { } cpk ? ", Cpk " + cpk.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty)}")
            : string.Create(CultureInfo.InvariantCulture, $"n = {s.Count}, not computable");

    private static string Format(double v) => Engine.Values.CalcValue.FormatNumber(v, 8);

    private static string Clip(string? s) => s is null ? string.Empty : s.Length > 60 ? s[..60] + "…" : s;
}
