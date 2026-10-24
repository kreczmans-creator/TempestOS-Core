using Tempest.Calc.Engine.Units;

namespace Tempest.Calc.Sheets;

/// <summary>The five calculation disciplines of `v0.26.0` (ADR-0163 names the first four; Manufacturing has no calculator yet; Fluid Power is ADR-0170). New members go at the end: the name, not the position, is in the template hash.</summary>
public enum CalcDiscipline
{
    Mechanical,
    Structural,
    Electrical,
    BuildingServices,
    FluidPower,
}

/// <summary>How an input is entered.</summary>
public enum CalcInputKind
{
    /// <summary>A number with a unit of the declared dimension.</summary>
    Quantity,

    /// <summary>A dimensionless number (a factor, a count, a ratio).</summary>
    Number,

    /// <summary>One of the declared choices.</summary>
    Choice,

    /// <summary>A yes/no switch.</summary>
    Boolean,

    /// <summary>Short free text (a designation).</summary>
    Text,

    /// <summary>A list of numbers sharing the declared dimension (a curve, a table column).</summary>
    Vector,

    /// <summary>A table of rows, each a small record of numbers and texts (contributors, bolts, segments, nodes).</summary>
    Table,
}

/// <summary>
/// One input of a template: what it is, what dimension it carries, its
/// default and its soft and hard limits. Soft limits warn; hard limits
/// block. Limits are in <see cref="DefaultUnit"/>. <see cref="MaterialProperty"/> names the material property
/// (<c>E</c>, <c>rho</c>, <c>f_y</c>, <c>E(T)</c>, …) this input takes from a <c>material.properties</c> record; the
/// template also lists the input's symbol in a record kind of that name (Addendum B, W3).
/// </summary>
public sealed record CalcInputSpec(
    string Symbol,
    string Description,
    CalcInputKind Kind,
    string DefaultUnit = "",
    double? Default = null,
    double? Min = null,
    double? Max = null,
    double? HardMin = null,
    double? HardMax = null,
    IReadOnlyList<string>? Choices = null,
    string? DefaultChoice = null,
    bool IsOptional = false,
    string? Group = null,
    IReadOnlyList<CalcColumnSpec>? Columns = null,
    int MaxRows = 500,
    string? MaterialProperty = null)
{
    /// <summary>The dimension this input measures, from its default unit.</summary>
    public Dimension Dimension => Kind is CalcInputKind.Quantity or CalcInputKind.Vector ? UnitCatalog.Parse(DefaultUnit).Dimension : Dimension.None;

    /// <summary>Whether this input takes a unit at all.</summary>
    public bool IsDimensioned => Kind is CalcInputKind.Quantity or CalcInputKind.Vector && DefaultUnit.Length > 0;

    /// <summary>The valid range written for the inputs table: "10 … 500 mm", "≥ 0", "any".</summary>
    public string RangeText
    {
        get
        {
            var unit = DefaultUnit.Length > 0 ? " " + DefaultUnit : string.Empty;
            var lo = Min ?? HardMin;
            var hi = Max ?? HardMax;

            if (lo is null && hi is null)
                return "any";

            if (lo is null)
                return $"≤ {Format(hi!.Value)}{unit}";

            if (hi is null)
                return $"≥ {Format(lo.Value)}{unit}";

            return $"{Format(lo.Value)} … {Format(hi.Value)}{unit}";
        }
    }

    private static string Format(double v) => Engine.Values.CalcValue.FormatNumber(v, 4);

    /// <summary>
    /// What to enter next when this input is empty, in plain words and without the symbol: for a table the rows and the columns it
    /// needs ("enter at least one row of 'Shaft segments': x_end (End position, m), d (Diameter, mm)"), for anything else the value
    /// and its unit. A new sheet that opens blocked on an empty input shows this.
    /// </summary>
    public string NextStepText()
    {
        if (Kind == CalcInputKind.Table)
        {
            var required = (Columns ?? []).Where(c => !c.IsOptional).Select(c => c.DefaultUnit.Length > 0 ? $"{c.Key} ({c.Label}, {c.DefaultUnit})" : $"{c.Key} ({c.Label})").ToList();
            var columns = required.Count > 0 ? $": fill in {string.Join(", ", required)}" : string.Empty;
            return $"enter at least one row of '{Description}' (the table takes up to {MaxRows} rows){columns}.";
        }

        var unit = DefaultUnit.Length > 0 && Kind is CalcInputKind.Quantity or CalcInputKind.Vector ? $" in {DefaultUnit}" : string.Empty;
        return $"enter '{Description}'{unit}; it has no default value.";
    }
}

/// <summary>One column of a <see cref="CalcInputKind.Table"/> input.</summary>
public sealed record CalcColumnSpec(string Key, string Label, CalcInputKind Kind, string DefaultUnit = "", IReadOnlyList<string>? Choices = null, double? Min = null, double? Max = null, double? HardMin = null, double? HardMax = null, bool IsOptional = false);

/// <summary>A standard cited by number and title only (never its text).</summary>
public sealed record CalcClauseRef(string Standard, string Clause, string Title);

/// <summary>A numbered assumption with the basis it rests on.</summary>
public sealed record CalcAssumption(string Statement, string? Basis = null);

/// <summary>How a notice affects the sheet: a warning is printed and the sheet still issues; a block stops issue.</summary>
public enum CalcNoticeSeverity
{
    Warning,
    Block,
}

/// <summary>A declared applicability rule: what the calculator checks and what happens when the check fails.</summary>
public sealed record CalcApplicabilityRule(string Id, string Description, CalcNoticeSeverity Severity);

/// <summary>Whether a known-answer reference value could be confirmed against its source.</summary>
public enum CalcReferenceStatus
{
    Confirmed,
    Unconfirmed,
}

/// <summary>Where a known-answer value comes from: title, edition and the page or example.</summary>
public sealed record CalcReferenceSource(string Title, string? Edition, string Location);

/// <summary>One expected output of a known-answer case, in the given unit, within a relative tolerance.</summary>
public sealed record CalcExpectedValue(string Symbol, double Value, string Unit, double RelativeTolerance = 0.01);

/// <summary>A known-answer test case: inputs, the reference they come from, and what the reference says the answers are.</summary>
public sealed record CalcTestCase(
    string Id,
    string Title,
    CalcReferenceSource Source,
    IReadOnlyList<CalcInputValue> Inputs,
    IReadOnlyList<CalcExpectedValue> Expected,
    CalcReferenceStatus Status = CalcReferenceStatus.Confirmed);

/// <summary>
/// The declarative half of a calculator (§3.5): identity and version,
/// inputs with limits, assumptions, clause references, applicability
/// rules and known-answer cases. The content hash is computed over this
/// record and the formulas of a run on its defaults
/// (<see cref="CalcTemplateHasher"/>).
/// </summary>
public sealed record CalcTemplate(
    string Id,
    string Version,
    CalcDiscipline Discipline,
    string Title,
    string Scope,
    string Method,
    IReadOnlyList<CalcInputSpec> Inputs,
    IReadOnlyList<CalcAssumption> Assumptions,
    IReadOnlyList<CalcClauseRef> ClauseRefs,
    IReadOnlyList<CalcApplicabilityRule> ApplicabilityRules,
    IReadOnlyList<CalcTestCase> TestCases,
    string? DiagramKey = null,
    string? Category = null,
    IReadOnlyList<CalcOutputPort>? OutputPorts = null,
    IReadOnlyList<CalcRecordKind>? RecordKinds = null,
    bool IndependentCheck = false)
{
    /// <summary>"id@version", the reference every instance carries.</summary>
    public string Reference => $"{Id}@{Version}";

    public CalcInputSpec? FindInput(string symbol) => Inputs.FirstOrDefault(i => string.Equals(i.Symbol, symbol, StringComparison.Ordinal));
}
