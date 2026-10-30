using System.Text.RegularExpressions;
using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;

namespace Tempest.Calc.Sheets;

/// <summary>
/// What an output is checked against, declared by the template that records it (C2-21). The display follows the kind, never the symbol or the
/// description of the output: a utilisation is shown to two decimals and rounded up when it fails; a value that must be at least a limit is
/// rounded down, so neither ever appears on the passing side of a limit its check fails.
/// </summary>
public enum CalcOutputKind
{
    /// <summary>An ordinary result: shown at the decimals of its quantity type.</summary>
    Value,

    /// <summary>A utilisation (demand over capacity, checked against a limit): 2 decimals, rounded up above its limit. An output a utilisation check is made on is this kind without being flagged.</summary>
    Utilisation,

    /// <summary>A safety factor, a margin or another value that must be at least a target (<c>n_f</c> against <c>n_target</c>): rounded down, so 1.4999 against 1.5 never prints 1.50.</summary>
    AtLeast,
}

/// <summary>
/// A named output of a run. <see cref="ValueText"/> is the value as the sheet shows it, at the decimals of its quantity type
/// (<see cref="CalcDisplay"/>, C-2 rows 29 and 30); the value itself is never rounded. <see cref="Decimals"/> overrides the decimals for
/// one output where the template justifies it. <see cref="Kind"/> is what the template says the output is checked against.
/// <see cref="NoiseProne"/> is set by a template for a solver residual, an equilibrium sum or a computed zero: only such an output is shown as 0
/// when it is solver noise (C2-20). <see cref="DisplayText"/> is the text a run settled on for a noise-prone output that came out as noise, or for a
/// residual below its stated floor ("below 1e-12"); an output built by hand has none and shows its own. <see cref="Rounded"/> is the direction the run settled on from the checks made on the output (C2-21): the
/// demand of a failing check is rounded away from its limit and its capacity toward it, a utilisation by the verdict of its check; it is <see langword="null"/>
/// for an output no failing check is made on.
/// </summary>
public sealed record CalcOutput(string Symbol, string Description, CalcValue Value, int? Decimals = null, string? DisplayText = null, CalcOutputKind Kind = CalcOutputKind.Value, bool NoiseProne = false, CalcDisplay.Rounding? Rounded = null, bool StrictRounding = false)
{
    public string ValueText => DisplayText ?? CalcDisplay.Format(this);
}

/// <summary>
/// One check: demand against capacity, the utilisation, and whether it passed. The governing check is the one with the highest utilisation.
/// <paramref name="HasUtilisation"/> is false for a yes/no check (a fit class, a separation test): it has no ratio, so a sheet prints
/// PASS or FAIL and no utilisation (C-2, row 3).
/// </summary>
public sealed record CalcCheck(string Name, string DemandText, string CapacityText, double Utilisation, bool Passed, string? StepSymbol = null, string? Clause = null, bool HasUtilisation = true)
{
    /// <summary>The utilisation as a sheet prints it: 2 decimals, or "—" for a yes/no check.</summary>
    public string UtilisationText => !HasUtilisation || double.IsInfinity(Utilisation) ? "—" : CalcDisplay.Utilisation(Utilisation, Passed);
}

/// <summary>What a notice is: an applicability rule that fired, or the plain note that an input was not entered and its default was used.</summary>
public enum CalcNoticeKind
{
    /// <summary>A warning or block raised by an applicability rule or by input validation.</summary>
    Rule,

    /// <summary>An input left empty that has a default (C-2, row 4): listed apart from the warnings, so the notes do not bury the ones that matter.</summary>
    DefaultUsed,
}

/// <summary>A warning (soft) or a block (hard) raised by an applicability rule or by input validation.</summary>
public sealed record CalcNotice(string RuleId, CalcNoticeSeverity Severity, string Message, CalcNoticeKind Kind = CalcNoticeKind.Rule)
{
    /// <summary>Whether this is the note that a default stood in for an empty input.</summary>
    public bool IsDefaultUsed => Kind == CalcNoticeKind.DefaultUsed;
}

/// <summary>Data a form or a sheet can draw: a named series of (x, y) points with units.</summary>
public sealed record CalcSeries(string Name, string XLabel, string XUnit, string YLabel, string YUnit, IReadOnlyList<(double X, double Y)> Points);

/// <summary>
/// Everything one run produced. A run never throws to its caller: a
/// refused input or a failed evaluation becomes a block notice and the
/// result carries no outputs.
/// </summary>
public sealed record CalcRunResult(
    IReadOnlyList<CalcStep> Steps,
    IReadOnlyList<CalcOutput> Outputs,
    IReadOnlyList<CalcCheck> Checks,
    IReadOnlyList<CalcNotice> Notices,
    IReadOnlyList<CalcSeries> Series,
    IReadOnlyList<CalcAssumption> RunAssumptions)
{
    public static CalcRunResult Blocked(IReadOnlyList<CalcNotice> notices) => new([], [], [], notices, [], []);

    public bool IsBlocked => Notices.Any(n => n.Severity == CalcNoticeSeverity.Block);

    /// <summary>Every warning, the default-value notes included (see <see cref="EngineeringWarnings"/> and <see cref="DefaultNotices"/>).</summary>
    public IReadOnlyList<CalcNotice> Warnings => Notices.Where(n => n.Severity == CalcNoticeSeverity.Warning).ToList();

    /// <summary>The warnings that are not default-value notes: what an engineer must read.</summary>
    public IReadOnlyList<CalcNotice> EngineeringWarnings => Notices.Where(n => n.Severity == CalcNoticeSeverity.Warning && !n.IsDefaultUsed).ToList();

    /// <summary>The notes that an empty input took its default (C-2, row 4).</summary>
    public IReadOnlyList<CalcNotice> DefaultNotices => Notices.Where(n => n.IsDefaultUsed).ToList();

    public IReadOnlyList<CalcNotice> Blocks => Notices.Where(n => n.Severity == CalcNoticeSeverity.Block).ToList();

    /// <summary>The check with the highest utilisation, or <see langword="null"/> with no checks.</summary>
    public CalcCheck? Governing => Checks.Count == 0 ? null : Checks.OrderByDescending(c => c.Utilisation).First();

    /// <summary>True when every check passed and nothing blocked; <see langword="null"/> when there are no checks.</summary>
    public bool? Passed => Checks.Count == 0 ? null : Checks.All(c => c.Passed) && !IsBlocked;

    public CalcOutput? FindOutput(string symbol) => Outputs.FirstOrDefault(o => string.Equals(o.Symbol, symbol, StringComparison.Ordinal));

    /// <summary>The SI value of a scalar output, for Monte Carlo and tornado sweeps.</summary>
    public double OutputSi(string symbol) => FindOutput(symbol)?.Value is ScalarValue s ? s.Si : throw new CalcEvaluationException($"'{symbol}' is not a scalar output of this run.");
}

/// <summary>
/// The collector a calculator writes into while it runs. It owns the
/// engine session, so every step is recorded, and it gathers outputs,
/// checks, notices and series.
/// </summary>
public sealed class CalcRunContext
{
    private readonly List<CalcOutput> _outputs = [];
    private readonly List<CalcCheck> _checks = [];
    private readonly List<CalcNotice> _notices = [];
    private readonly List<CalcSeries> _series = [];
    private readonly List<CalcAssumption> _assumptions = [];
    private readonly Dictionary<string, CalcOutputKind> _kinds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _noiseProne = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _residualFloors = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failingDemands = new(StringComparer.Ordinal);
    private readonly HashSet<string> _failingCapacities = new(StringComparer.Ordinal);
    private readonly HashSet<string> _strictSymbols = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _utilisationVerdicts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Name, double Utilisation)> _resultSymbols = new(StringComparer.Ordinal);

    public CalcRunContext(ICalcSession session, CalcInputSet inputs, CalcTemplate template, CancellationToken cancellationToken)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
        Template = template ?? throw new ArgumentNullException(nameof(template));
        CancellationToken = cancellationToken;
    }

    public ICalcSession Session { get; }

    public CalcInputSet Inputs { get; }

    public CalcTemplate Template { get; }

    public CancellationToken CancellationToken { get; }

    /// <summary>The block message for a table that has no rows: its symbol, then what to enter (see <see cref="CalcInputSpec.NextStepText"/>).</summary>
    public string EnterRows(string symbol) => $"{symbol}: " + (Template.FindInput(symbol)?.NextStepText() ?? "enter at least one row.");

    public IReadOnlyList<CalcOutput> Outputs => _outputs;

    public IReadOnlyList<CalcCheck> Checks => _checks;

    public IReadOnlyList<CalcNotice> Notices => _notices;

    /// <summary>Defines an input symbol in the session from the input set, recording it as an input step. Returns the value.</summary>
    public CalcValue Input(string symbol)
    {
        var spec = Template.FindInput(symbol) ?? throw new CalcEvaluationException($"'{symbol}' is not an input of {Template.Id}.");
        var value = CalcInputBinder.Bind(spec, Inputs.Find(symbol), Inputs.MadeOn);

        // A pressure entered in bar is shown with its basis ("90 bar a"): the label changes the text, never the number.
        if (value is ScalarValue { DisplayUnit.Symbol: "bar" } pressure && spec.BasisLabel(ChoiceOf) is { } basis)
            value = pressure.In(UnitCatalog.Parse("bar " + basis));

        Session.Define(symbol, value, spec.Description);
        return value;
    }

    private string? ChoiceOf(string symbol) => Inputs.Find(symbol)?.Text ?? Template.FindInput(symbol)?.DefaultChoice;

    /// <summary>The scalar SI value of an input, defining it in the session on first use.</summary>
    public double InputSi(string symbol) => (Session.Find(symbol) ?? Input(symbol)) is ScalarValue s ? s.Si : throw new CalcEvaluationException($"'{symbol}' is not a single number.");

    /// <summary>The chosen text of a choice or text input.</summary>
    public string InputText(string symbol) => (Session.Find(symbol) ?? Input(symbol)) is TextValue t ? t.Value : throw new CalcEvaluationException($"'{symbol}' is not a text.");

    /// <summary>The flag of a boolean input.</summary>
    public bool InputFlag(string symbol) => (Session.Find(symbol) ?? Input(symbol)) is BoolValue b ? b.Value : throw new CalcEvaluationException($"'{symbol}' is not a yes/no input.");

    /// <summary>Whether an optional input was given.</summary>
    public bool Has(string symbol) => Inputs.Find(symbol) is { } v && (v.Number is not null || v.Text is not null || v.Flag is not null || v.Vector is not null || v.Rows is not null);

    /// <summary>Whether the person recorded a basis for an input (an assumption, a specification, a drawing, a link ...): a value typed with no provenance is not deliberate.</summary>
    public bool HasRecordedBasis(string symbol) => Inputs.Find(symbol)?.Provenance is { Kind: not CalcProvenanceKind.Manual };

    /// <summary>The rows of a table input (empty when absent).</summary>
    public IReadOnlyList<CalcRow> Rows(string symbol) => Inputs.Find(symbol)?.Rows ?? [];

    public CalcStep Assign(string symbol, string expression, string? unit = null, string? description = null, string? clause = null) => Session.Assign(symbol, expression, unit, description, clause);

    public CalcStep Record(string symbol, string formula, string substitution, CalcValue result, string? description = null, string? clause = null) => Session.Record(symbol, formula, substitution, result, description, clause);

    public ScalarValue Scalar(string symbol) => Session.GetScalar(symbol);

    public double Si(string symbol) => Session.GetScalar(symbol).Si;

    /// <summary>
    /// Records an output. <paramref name="decimals"/> overrides the decimals of its quantity type for this output only; use it where the template can
    /// justify it. <paramref name="kind"/> declares what the output is checked against (a utilisation, a value that must be at least a target); an output a
    /// utilisation check is made on is a utilisation without it. <paramref name="noiseProne"/> declares a solver residual, an equilibrium sum or a computed
    /// zero, which is shown as 0 when it is solver noise.
    /// </summary>
    public void Output(string symbol, string description, int? decimals = null, CalcOutputKind kind = CalcOutputKind.Value, bool noiseProne = false) =>
        _outputs.Add(new CalcOutput(symbol, description, Session.Get(symbol), decimals, null, kind, noiseProne));

    public void Output(string symbol, string description, CalcValue value, int? decimals = null, CalcOutputKind kind = CalcOutputKind.Value, bool noiseProne = false) =>
        _outputs.Add(new CalcOutput(symbol, description, value, decimals, null, kind, noiseProne));

    /// <summary>
    /// Declares that the outputs of these symbols are utilisations (demand over capacity, checked against a limit): they are shown to two decimals and rounded
    /// up when they exceed it. An output a utilisation check is made on needs no declaration. Declare by what the output is, not by how its symbol is spelt (C2-21).
    /// </summary>
    public void MarkUtilisation(params string[] symbols)
    {
        foreach (var symbol in symbols)
            _kinds[symbol] = CalcOutputKind.Utilisation;
    }

    /// <summary>Declares that the outputs of these symbols are values that must be at least a target (a safety factor, a margin): they are rounded down for display (C2-21).</summary>
    public void MarkAtLeast(params string[] symbols)
    {
        foreach (var symbol in symbols)
            _kinds[symbol] = CalcOutputKind.AtLeast;
    }

    /// <summary>
    /// Declares that the outputs of these symbols are solver residuals, equilibrium sums or computed zeros (a radial stress at a free surface, a reaction moment
    /// at a pinned support): one that comes out more than nine orders of magnitude below the largest working value of its dimension is solver noise and is shown as
    /// 0. An output nobody declared is never shown as 0 for being small (C2-20).
    /// </summary>
    public void MarkNoiseProne(params string[] symbols)
    {
        foreach (var symbol in symbols)
            _noiseProne.Add(symbol);
    }

    /// <summary>
    /// Declares that the output of <paramref name="symbol"/> is a residual (a loop-closure or self-consistency error, whose whole purpose is to be near zero)
    /// and states its noise floor, in the unit the output is shown in: a residual below the floor is shown as "below" the floor ("below 1e-12", "below
    /// 1e-10 mm"), in its row and in its working, never with the digits of its round-off, which differ between platforms (C2-20). A residual at or above
    /// its floor is shown as it is. The value is not changed. Use <see cref="CalcDisplay.ResidualText"/> for a check line that quotes it.
    /// </summary>
    public void MarkResidual(string symbol, double floor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        if (!double.IsFinite(floor) || floor <= 0)
            throw new ArgumentOutOfRangeException(nameof(floor), floor, "A residual's floor is a positive number.");

        _residualFloors[symbol] = floor;
    }

    /// <summary>
    /// Records a utilisation check: demand/capacity, passed when utilisation ≤ 1 (within a hair of rounding). A failing check shows its demand rounded up and
    /// its capacity rounded down, so the two never print as equal or in the passing order (C2-21). <paramref name="resultSymbol"/> names the output the sheet
    /// marks as governing when this check governs (the utilisation the demand and the capacity give); it is the demand when it is not given, so a check
    /// whose demand is an input (a required life against the life achieved) can still mark the utilisation output it belongs to. A result symbol the
    /// calculator never records as an output is emitted by the run itself, as a utilisation output holding this check's ratio (the calculator's own
    /// value of that symbol when it computed one), so the sheet's governing line always has an output row to point to (C-2 integration).
    /// </summary>
    public CalcCheck Check(string name, string demandSymbol, string capacitySymbol, string? clause = null, double tolerance = 1e-9, string? resultSymbol = null)
    {
        var d = Session.GetScalar(demandSymbol);
        var c = Session.GetScalar(capacitySymbol);

        if (d.Dimension != c.Dimension)
            throw new Engine.CalcDimensionException($"Check '{name}' compares [{d.Dimension}] with [{c.Dimension}].");

        var u = c.Si == 0 ? double.PositiveInfinity : Math.Abs(d.Si) / Math.Abs(c.Si);
        var passed = u <= 1.0 + tolerance;
        var demandText = CalcDisplay.Format(d, mode: passed ? CalcDisplay.Rounding.Nearest : CalcDisplay.Rounding.AwayFromZero);
        var capacityText = CalcDisplay.Format(c, mode: passed ? CalcDisplay.Rounding.Nearest : CalcDisplay.Rounding.TowardZero);
        var strict = false;

        // The clean-number snap must not erase a failure: when the two texts of a failing check read as passing (the demand no larger than the capacity), both are rounded
        // strictly in their directions, whatever their distance from a clean number.
        if (!passed && CalcDisplay.LeadingNumber(demandText) is { } shownDemand && CalcDisplay.LeadingNumber(capacityText) is { } shownCapacity && Math.Abs(shownDemand) <= Math.Abs(shownCapacity))
        {
            strict = true;
            demandText = CalcDisplay.Format(d, mode: CalcDisplay.Rounding.AwayFromZero, strict: true);
            capacityText = CalcDisplay.Format(c, mode: CalcDisplay.Rounding.TowardZero, strict: true);
        }

        var check = new CalcCheck(name, demandText, capacityText, u, passed, resultSymbol ?? demandSymbol, clause);
        _checks.Add(check);

        // An output named as the result of this check is its utilisation: it is shown by this check's verdict, as an output of a utilisation check is (C2-31).
        if (resultSymbol is not null)
        {
            _kinds.TryAdd(resultSymbol, CalcOutputKind.Utilisation);
            _utilisationVerdicts[resultSymbol] = _utilisationVerdicts.TryGetValue(resultSymbol, out var earlier) ? earlier && passed : passed;

            // The first check naming the symbol gives the emitted output its name and ratio; a later one on the same symbol keeps the larger ratio.
            if (!_resultSymbols.TryGetValue(resultSymbol, out var first) || u > first.Utilisation)
                _resultSymbols[resultSymbol] = (name, u);
        }

        // The Results rows of the two quantities must not show the passing side either: the demand of a failing check is shown rounded up and its capacity rounded down (C2-21).
        if (!passed)
        {
            _failingDemands.Add(demandSymbol);
            _failingCapacities.Add(capacitySymbol);

            if (strict)
            {
                _strictSymbols.Add(demandSymbol);
                _strictSymbols.Add(capacitySymbol);
            }
        }

        return check;
    }

    /// <summary>
    /// Records a check from an already computed utilisation. The symbol is a utilisation from now on (an output of that symbol is shown as one). The
    /// utilisation is shown rounded up whenever it exceeds <paramref name="limit"/> (within <paramref name="tolerance"/>), not only above 1.
    /// </summary>
    public CalcCheck CheckUtilisation(string name, string utilisationSymbol, string? clause = null, double limit = 1.0, double tolerance = 1e-9)
    {
        var u = Session.GetScalar(utilisationSymbol);

        if (!u.Dimension.IsDimensionless)
            throw new Engine.CalcDimensionException($"Check '{name}' needs a dimensionless utilisation.");

        _kinds.TryAdd(utilisationSymbol, CalcOutputKind.Utilisation);

        // A negative utilisation means the capacity is negative or inverted (a negative NPSH available): never a pass (G-03).
        // Written as "u = 1.719 vs u ≤ 1" so a ratio is never read as a demand quantity (Black B-18).
        var passed = u.Si >= 0 && u.Si <= limit + tolerance;
        var check = new CalcCheck(name, $"{utilisationSymbol} = {CalcDisplay.Utilisation(u.Si, passed, limit)}", $"{utilisationSymbol} ≤ {CalcValue.FormatNumber(limit)}", u.Si / limit, passed, utilisationSymbol, clause);
        _checks.Add(check);

        // An output of this symbol is shown by this check's verdict, not by a threshold of its own.
        _utilisationVerdicts[utilisationSymbol] = _utilisationVerdicts.TryGetValue(utilisationSymbol, out var earlier) ? earlier && passed : passed;
        return check;
    }

    /// <summary>Records a pass/fail check that is not a ratio (a separation check, a sign change).</summary>
    public CalcCheck CheckCondition(string name, bool passed, string demandText, string capacityText, string? stepSymbol = null, string? clause = null)
    {
        var check = new CalcCheck(name, demandText, capacityText, passed ? 0.0 : double.PositiveInfinity, passed, stepSymbol, clause, HasUtilisation: false);
        _checks.Add(check);
        return check;
    }

    public void Warn(string ruleId, string message) => _notices.Add(new CalcNotice(ruleId, CalcNoticeSeverity.Warning, message));

    public void Block(string ruleId, string message) => _notices.Add(new CalcNotice(ruleId, CalcNoticeSeverity.Block, message));

    public void Series(CalcSeries series) => _series.Add(series);

    public void Assume(string statement, string? basis = null) => _assumptions.Add(new CalcAssumption(statement, basis));

    internal CalcRunResult ToResult()
    {
        // The kind of an output is what its template declared, or a utilisation when a utilisation check was made on its symbol. A noise-prone output
        // that is solver noise carries the text "0": it is compared with the largest working value of its own dimension in this run, and nothing else.
        var steps = Session.Steps;
        var outputs = new List<CalcOutput>(_outputs.Count);

        // What the working shows for a symbol whose output the run settled as noise or as a residual below its floor: the step's result text, and the number
        // a later step's substitution shows as 0 (C2-20). The working and the Results row then say the same, and no digit of round-off is printed in either.
        var working = new Dictionary<string, (CalcValue Value, string Text, string? Number)>(StringComparer.Ordinal);

        foreach (var o in _outputs)
        {
            var settled = o.Kind == CalcOutputKind.Value && _kinds.TryGetValue(o.Symbol, out var declared) ? o with { Kind = declared } : o;

            // The direction the failing checks of the run give this output (C2-21): the verdict of a utilisation check, the demand of a failing check up, its capacity down.
            if (settled.Rounded is null && settled.Value is ScalarValue { IsFinite: true } scalar)
            {
                if (settled.Kind == CalcOutputKind.Utilisation && _utilisationVerdicts.TryGetValue(settled.Symbol, out var verdict))
                    settled = settled with { Rounded = verdict ? CalcDisplay.Rounding.Nearest : scalar.Si < 0 ? CalcDisplay.Rounding.Down : CalcDisplay.Rounding.Up };
                else if (_failingDemands.Contains(settled.Symbol) && !_failingCapacities.Contains(settled.Symbol))
                    settled = settled with { Rounded = CalcDisplay.Rounding.AwayFromZero, StrictRounding = _strictSymbols.Contains(settled.Symbol) };
                else if (_failingCapacities.Contains(settled.Symbol) && !_failingDemands.Contains(settled.Symbol))
                    settled = settled with { Rounded = CalcDisplay.Rounding.TowardZero, StrictRounding = _strictSymbols.Contains(settled.Symbol) };
            }

            var noiseProne = settled.NoiseProne || _noiseProne.Contains(settled.Symbol);

            if (_residualFloors.TryGetValue(settled.Symbol, out var floor))
            {
                // A residual is never zeroed: below its stated floor it is shown as below the floor, in its row and in its working.
                if (settled.Value is ScalarValue { IsFinite: true } residual && Math.Abs(residual.Displayed) < floor)
                {
                    settled = settled with { DisplayText = CalcDisplay.BelowText(floor, residual) };
                    working[settled.Symbol] = (residual, settled.DisplayText, null);
                }
            }
            else if (noiseProne && settled.Value is ScalarValue { IsFinite: true } s && s.Si != 0)
            {
                // The comparison is with the working values of this run (inputs and steps) of the same dimension and unit, never with another output as such.
                if (CalcDisplay.IsNoise(s, LargestWorkingValue(steps, s.Dimension, s.DisplayUnit)))
                {
                    settled = settled with { DisplayText = CalcDisplay.ZeroText(s) };
                    working[settled.Symbol] = (s, settled.DisplayText, CalcValue.FormatNumber(Math.Abs(s.Displayed), Session.SignificantFigures));
                }
            }
            else if (noiseProne && settled.Value is VectorValue v)
            {
                // A table of a noise-prone quantity: an entry more than nine orders of magnitude below the largest working value of its dimension is shown as 0.
                var below = CalcDisplay.NoiseFraction * LargestWorkingValue(steps, v.Dimension, v.DisplayUnit);

                if (v.Si.Any(x => x != 0 && Math.Abs(x) < below))
                {
                    settled = settled with { DisplayText = v.Format(5, below) };
                    working[settled.Symbol] = (v, v.Format(Session.SignificantFigures, below), null);
                }
            }

            outputs.Add(settled);
        }

        // A check's result symbol that no output carries is emitted here as a utilisation output, so the governing line of the sheet always names an
        // output row (CalcSheet.GoverningOutputSymbol): the calculator's own value of the symbol when it computed one (the same ratio), else the check's.
        foreach (var (symbol, (name, utilisation)) in _resultSymbols)
        {
            if (outputs.Any(o => string.Equals(o.Symbol, symbol, StringComparison.Ordinal)))
                continue;

            var value = Session.Find(symbol) as ScalarValue ?? (double.IsFinite(utilisation) ? new ScalarValue(utilisation) : null);

            if (value is not { IsFinite: true })
                continue;

            var description = steps.LastOrDefault(t => string.Equals(t.Symbol, symbol, StringComparison.Ordinal))?.Description is { Length: > 0 } own
                ? own
                : "Utilisation of " + char.ToLowerInvariant(name[0]) + name[1..];
            var verdict = !_utilisationVerdicts.TryGetValue(symbol, out var passed) || passed;
            outputs.Add(new CalcOutput(symbol, description, value, null, null, CalcOutputKind.Utilisation, false, verdict ? CalcDisplay.Rounding.Nearest : value.Si < 0 ? CalcDisplay.Rounding.Down : CalcDisplay.Rounding.Up));
        }

        return new CalcRunResult(Shown(steps, working), outputs, _checks, _notices, _series, _assumptions);
    }

    /// <summary>The largest magnitude among the working values of the run (scalar steps and the entries of tables) of one dimension and, for a plain number, one unit.</summary>
    private static double LargestWorkingValue(IReadOnlyList<CalcStep> steps, Dimension dimension, Unit unit)
    {
        var largest = 0.0;

        foreach (var t in steps)
        {
            if (t.Result.Dimension != dimension || (dimension.IsDimensionless && t.Result.DisplayUnit.Symbol != unit.Symbol))
                continue;

            IEnumerable<double> values = t.Result switch
            {
                ScalarValue r => [r.Si],
                VectorValue v => v.Si,
                _ => [],
            };

            foreach (var x in values)
            {
                if (double.IsFinite(x))
                    largest = Math.Max(largest, Math.Abs(x));
            }
        }

        return largest;
    }

    /// <summary>
    /// The steps as the sheet shows them: a step of a symbol settled as noise or as a residual below its floor shows the text its output shows, and a later
    /// step's substitution shows such a noise value as 0. Only text changes; every step keeps its value.
    /// </summary>
    private static IReadOnlyList<CalcStep> Shown(IReadOnlyList<CalcStep> steps, Dictionary<string, (CalcValue Value, string Text, string? Number)> working)
    {
        if (working.Count == 0)
            return steps;

        var shown = new List<CalcStep>(steps.Count);
        var zeroed = new List<Regex>();

        foreach (var step in steps)
        {
            var current = step;

            foreach (var number in zeroed)
                current = current with { Substitution = number.Replace(current.Substitution, "0") };

            if (working.TryGetValue(step.Symbol, out var w) && (ReferenceEquals(step.Result, w.Value) || step.Result.Equals(w.Value)))
            {
                current = current with { ResultText = w.Text };

                // The number as a substitution prints it, alone (not part of a longer number), with a sign written against it.
                if (w.Number is { } n)
                    zeroed.Add(new Regex(@"(?<![\w.])[-−]?" + Regex.Escape(n) + @"(?![\w.])", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)));
            }

            shown.Add(current);
        }

        return shown;
    }
}

/// <summary>The computing half of a calculator: given validated inputs, writes steps, outputs, checks and notices into the context.</summary>
public interface ICalcCalculator
{
    CalcTemplate Template { get; }

    /// <summary>Runs the calculation. Throws <see cref="CalcException"/> on a failed evaluation; <see cref="CalcRunner"/> turns that into a block.</summary>
    void Run(CalcRunContext context);
}
