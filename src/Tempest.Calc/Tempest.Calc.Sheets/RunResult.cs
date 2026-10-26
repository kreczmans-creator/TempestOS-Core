using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;

namespace Tempest.Calc.Sheets;

/// <summary>
/// A named output of a run. <see cref="ValueText"/> is the value as the sheet shows it, at the decimals of its quantity type
/// (<see cref="CalcDisplay"/>, C-2 rows 29 and 30); the value itself is never rounded. <see cref="Decimals"/> overrides the decimals for
/// one output where the template justifies it. <see cref="DisplayText"/> is the text a run settled on once it knew every output (the
/// solver-noise rule needs them all); an output built by hand has none and shows its own.
/// </summary>
public sealed record CalcOutput(string Symbol, string Description, CalcValue Value, int? Decimals = null, string? DisplayText = null)
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
    public string UtilisationText => !HasUtilisation || double.IsInfinity(Utilisation) ? "—" : CalcDisplay.Utilisation(Utilisation);
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
        var value = CalcInputBinder.Bind(spec, Inputs.Find(symbol));

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

    /// <summary>The rows of a table input (empty when absent).</summary>
    public IReadOnlyList<CalcRow> Rows(string symbol) => Inputs.Find(symbol)?.Rows ?? [];

    public CalcStep Assign(string symbol, string expression, string? unit = null, string? description = null, string? clause = null) => Session.Assign(symbol, expression, unit, description, clause);

    public CalcStep Record(string symbol, string formula, string substitution, CalcValue result, string? description = null, string? clause = null) => Session.Record(symbol, formula, substitution, result, description, clause);

    public ScalarValue Scalar(string symbol) => Session.GetScalar(symbol);

    public double Si(string symbol) => Session.GetScalar(symbol).Si;

    /// <summary>Records an output. <paramref name="decimals"/> overrides the decimals of its quantity type for this output only; use it where the template can justify it.</summary>
    public void Output(string symbol, string description, int? decimals = null) => _outputs.Add(new CalcOutput(symbol, description, Session.Get(symbol), decimals));

    public void Output(string symbol, string description, CalcValue value, int? decimals = null) => _outputs.Add(new CalcOutput(symbol, description, value, decimals));

    /// <summary>Records a utilisation check: demand/capacity, passed when utilisation ≤ 1 (within a hair of rounding).</summary>
    public CalcCheck Check(string name, string demandSymbol, string capacitySymbol, string? clause = null)
    {
        var d = Session.GetScalar(demandSymbol);
        var c = Session.GetScalar(capacitySymbol);

        if (d.Dimension != c.Dimension)
            throw new Engine.CalcDimensionException($"Check '{name}' compares [{d.Dimension}] with [{c.Dimension}].");

        var u = c.Si == 0 ? double.PositiveInfinity : Math.Abs(d.Si) / Math.Abs(c.Si);
        var check = new CalcCheck(name, CalcDisplay.Format(d), CalcDisplay.Format(c), u, u <= 1.0 + 1e-9, demandSymbol, clause);
        _checks.Add(check);
        return check;
    }

    /// <summary>Records a check from an already computed utilisation.</summary>
    public CalcCheck CheckUtilisation(string name, string utilisationSymbol, string? clause = null, double limit = 1.0)
    {
        var u = Session.GetScalar(utilisationSymbol);

        if (!u.Dimension.IsDimensionless)
            throw new Engine.CalcDimensionException($"Check '{name}' needs a dimensionless utilisation.");

        // A negative utilisation means the capacity is negative or inverted (a negative NPSH available): never a pass (G-03).
        // Written as "u = 1.719 vs u ≤ 1" so a ratio is never read as a demand quantity (Black B-18).
        var check = new CalcCheck(name, $"{utilisationSymbol} = {CalcDisplay.Utilisation(u.Si)}", $"{utilisationSymbol} ≤ {CalcValue.FormatNumber(limit)}", u.Si / limit, u.Si >= 0 && u.Si <= limit + 1e-9, utilisationSymbol, clause);
        _checks.Add(check);
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
        // The text of each output is settled once every output is known: the solver-noise rule compares a value with the largest of its dimension.
        var texts = CalcDisplay.Texts(_outputs);
        var outputs = _outputs.Select((o, i) => o with { DisplayText = texts[i] }).ToList();
        return new CalcRunResult(Session.Steps, outputs, _checks, _notices, _series, _assumptions);
    }
}

/// <summary>The computing half of a calculator: given validated inputs, writes steps, outputs, checks and notices into the context.</summary>
public interface ICalcCalculator
{
    CalcTemplate Template { get; }

    /// <summary>Runs the calculation. Throws <see cref="CalcException"/> on a failed evaluation; <see cref="CalcRunner"/> turns that into a block.</summary>
    void Run(CalcRunContext context);
}
