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
/// template also lists the input's symbol in a record kind of that name (Addendum B, W3). <see cref="PressureBasis"/> names the
/// basis of a pressure input so the sheet prints "bar a" or "bar g" beside it. <see cref="Aliases"/> are the symbols this input was
/// called before it was renamed (C-2): an inputs file, a CSV, a saved draft or a pack link that still uses one is read as this input
/// (<see cref="CalcInputAliases"/>); nothing writes an alias. <see cref="MeaningChangedAt"/> is the template version at which this symbol
/// came to mean something else (an axis swapped, a width that became a depth), with <see cref="MeaningWas"/> and <see cref="MeaningNow"/>
/// saying what in plain words: a value stored under this symbol by a calculation made on an earlier version is not carried, linked or bound
/// (ADR-0177 review); a value stored under an alias keeps its meaning, since an alias is declared precisely because the old symbol is this input.
/// <see cref="UnitChangedAt"/> is the template version at which <see cref="DefaultUnit"/> changed, with <see cref="UnitWas"/> the unit before it: a
/// bare number (no unit of its own) stored by a calculation made before that version is read in the old unit, never the new one, and one on a
/// record that carries no version is refused (<see cref="CalcInputUnits"/>). A value with a unit of its own is unaffected.
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
    string? MaterialProperty = null,
    string? PressureBasis = null,
    string? ReferenceLibrary = null,
    string? ReferenceProperty = null,
    string? ReferenceDimension = null,
    IReadOnlyList<string>? Aliases = null,
    string? MeaningChangedAt = null,
    string? MeaningWas = null,
    string? MeaningNow = null,
    string? UnitChangedAt = null,
    string? UnitWas = null)
{
    /// <summary>Whether this input declares that its meaning changed at a version (<see cref="MeaningChangedAt"/>).</summary>
    public bool HasMeaningChange => MeaningChangedAt is { Length: > 0 };

    /// <summary>The column of this table input named <paramref name="keyOrAlias"/>: by its key, else by a key it was called before (<see cref="CalcColumnSpec.Aliases"/>); <see langword="null"/> for no such column.</summary>
    public CalcColumnSpec? ResolveColumn(string keyOrAlias) =>
        Columns?.FirstOrDefault(c => string.Equals(c.Key, keyOrAlias, StringComparison.Ordinal)) ?? Columns?.FirstOrDefault(c => c.Matches(keyOrAlias));

    /// <summary>Whether any column of this input has an alias.</summary>
    public bool HasColumnAliases => Columns is { Count: > 0 } columns && columns.Any(c => c.Aliases is { Count: > 0 });

    /// <summary>Whether this input declares that its default unit changed at a version (<see cref="UnitChangedAt"/>, <see cref="UnitWas"/>).</summary>
    public bool HasUnitChange => UnitChangedAt is { Length: > 0 };

    /// <summary>
    /// Whether a value stored under <paramref name="givenSymbol"/> by a calculation made on template version <paramref name="version"/> means
    /// something else now: the symbol is this input's own (not an alias, which keeps its meaning), the input declares a meaning change, and the
    /// version is older than the change.
    /// </summary>
    public bool MeaningChangedSince(string givenSymbol, string version) =>
        HasMeaningChange && string.Equals(givenSymbol, Symbol, StringComparison.Ordinal) && CalcTemplateVersion.Compare(version, MeaningChangedAt!) < 0;

    /// <summary>"h meant the width on id@1.1.0; since id@1.2.0 it means the depth", for a sentence that refuses to carry the old value.</summary>
    public string MeaningChangeText(string templateId, string oldReference) =>
        CalcTemplateVersion.MeaningChangeText(Symbol, templateId, oldReference, MeaningChangedAt, MeaningWas, MeaningNow);

    /// <summary>A pressure that is always gauge (above the atmosphere): <see cref="PressureBasis"/> "g".</summary>
    public const string BasisGauge = "g";

    /// <summary>A pressure that is always absolute: <see cref="PressureBasis"/> "a".</summary>
    public const string BasisAbsolute = "a";

    /// <summary>
    /// The basis label printed beside this pressure on the sheet: "g" (gauge) or "a" (absolute), or <see langword="null"/> for an
    /// input that is not a pressure, a pressure difference, or a pressure with no stated basis. <see cref="PressureBasis"/> is "g",
    /// "a", or "@symbol" when the basis follows the choice input of that name ("Gauge" or "Absolute"); <paramref name="choiceOf"/>
    /// returns that choice. The label only names the basis; it converts nothing.
    /// </summary>
    public string? BasisLabel(Func<string, string?> choiceOf)
    {
        ArgumentNullException.ThrowIfNull(choiceOf);

        if (PressureBasis is not { Length: > 0 } basis)
            return null;

        if (basis[0] != '@')
            return basis;

        return choiceOf(basis[1..]) switch
        {
            { } c when c.Equals("Absolute", StringComparison.OrdinalIgnoreCase) => BasisAbsolute,
            { } c when c.Equals("Gauge", StringComparison.OrdinalIgnoreCase) => BasisGauge,
            _ => null,
        };
    }

    /// <summary>Whether a released reference record (materials, fasteners, bearings) can fill this input with a pinned provenance (ADR-0167 §3).</summary>
    public bool IsReferenceFillable => ReferenceLibrary is { Length: > 0 } && ReferenceProperty is { Length: > 0 };

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

/// <summary>Template versions are dotted digits ("1.2.0"); this compares them part by part, a missing part reading as 0.</summary>
public static class CalcTemplateVersion
{
    /// <summary>Negative when <paramref name="a"/> is the earlier version, zero when the same, positive when the later.</summary>
    public static int Compare(string a, string b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var pa = a.Split('.').Select(x => int.TryParse(x, out var n) ? n : 0).ToArray();
        var pb = b.Split('.').Select(x => int.TryParse(x, out var n) ? n : 0).ToArray();

        for (var i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            var x = i < pa.Length ? pa[i] : 0;
            var y = i < pb.Length ? pb[i] : 0;

            if (x != y)
                return x.CompareTo(y);
        }

        return 0;
    }

    /// <summary>Whether <paramref name="version"/> is dotted digits, as a template version must be.</summary>
    public static bool IsWellFormed(string? version) =>
        version is { Length: > 0 and <= 32 } && version.All(c => char.IsAsciiDigit(c) || c == '.');

    /// <summary>"W_el_y meant the elastic modulus about the minor axis on id@1.1.0; since id@1.2.0 it means the elastic modulus about the major axis".</summary>
    internal static string MeaningChangeText(string symbol, string templateId, string oldReference, string? changedAt, string? was, string? now) =>
        $"{symbol} meant {was} on {oldReference}; since {templateId}@{changedAt} it means {now}";
}

/// <summary>
/// One column of a <see cref="CalcInputKind.Table"/> input. <paramref name="UnitChangedAt"/> and <paramref name="UnitWas"/> are as on
/// <see cref="CalcInputSpec"/>: a bare cell stored before that version is read in the old unit (<see cref="CalcInputUnits"/>). <paramref name="Aliases"/>
/// are the keys this column was called before it was renamed (C-2), as <see cref="CalcInputSpec.Aliases"/> are for an input: a saved table, a CSV or an
/// inputs file whose rows still use one is read as this column (<see cref="CalcInputAliases"/>, <see cref="CalcInputSpec.ResolveColumn"/>); nothing writes an alias.
/// </summary>
public sealed record CalcColumnSpec(string Key, string Label, CalcInputKind Kind, string DefaultUnit = "", IReadOnlyList<string>? Choices = null, double? Min = null, double? Max = null, double? HardMin = null, double? HardMax = null, bool IsOptional = false, string? UnitChangedAt = null, string? UnitWas = null, IReadOnlyList<string>? Aliases = null)
{
    /// <summary>Whether this column declares that its default unit changed at a version.</summary>
    public bool HasUnitChange => UnitChangedAt is { Length: > 0 };

    /// <summary>Whether <paramref name="keyOrAlias"/> is this column's key or one of the keys it was called before.</summary>
    public bool Matches(string keyOrAlias) =>
        string.Equals(Key, keyOrAlias, StringComparison.Ordinal) || Aliases is { Count: > 0 } aliases && aliases.Contains(keyOrAlias, StringComparer.Ordinal);
}

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
/// <summary>
/// What an applicability rule says about the result when it fires (C-2, declared by the template, never found by matching words). A rule is
/// <see cref="Conservative"/> when the result stays on the safe side or exact, or the notice only reports a convention, an unused input or a limitation
/// that does not make a pass less safe. It is <see cref="Unconservative"/> when the sheet can still show a pass although the result may be on the unsafe
/// side (a demand understated, a capacity overstated, a check not made, a method used outside its range). The contract test requires an unconservative
/// rule to be a block or to be paired with a check that fails when it fires; the others are held by an allow-list that only shrinks.
/// </summary>
public enum CalcRuleEffect
{
    /// <summary>The result is on the safe side, exact, or the notice does not change what a pass means.</summary>
    Conservative,

    /// <summary>The result may be on the unsafe side while the sheet passes.</summary>
    Unconservative,
}

/// <summary>An applicability rule: its id, what it says, whether it blocks or warns, and its <see cref="CalcRuleEffect"/> (declared on every rule; it is not part of the content hash).</summary>
public sealed record CalcApplicabilityRule(string Id, string Description, CalcNoticeSeverity Severity, CalcRuleEffect Effect);

/// <summary>Whether a known-answer reference value could be confirmed against its source.</summary>
public enum CalcReferenceStatus
{
    Confirmed,
    Unconfirmed,
}

/// <summary>
/// Where a known-answer value comes from: title, edition and the page or example. <paramref name="Secondary"/> marks a value
/// taken from a secondary source (a solutions manual, website, forum, course notes or vendor verification) rather than the
/// primary publication (Addendum B §B4); such a case is listed for the owner to confirm.
/// </summary>
public sealed record CalcReferenceSource(string Title, string? Edition, string Location, bool Secondary = false);

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
    bool IndependentCheck = false,
    string? FeatureFlag = null,
    bool RequiresAttestation = false,
    bool RequiresSecondCheck = false)
{
    /// <summary>"id@version", the reference every instance carries.</summary>
    public string Reference => $"{Id}@{Version}";

    public CalcInputSpec? FindInput(string symbol) => Inputs.FirstOrDefault(i => string.Equals(i.Symbol, symbol, StringComparison.Ordinal));

    /// <summary>The input named <paramref name="symbolOrAlias"/>: by its symbol, else by a symbol it was called before (<see cref="CalcInputSpec.Aliases"/>).</summary>
    public CalcInputSpec? ResolveInput(string symbolOrAlias) =>
        FindInput(symbolOrAlias) ?? Inputs.FirstOrDefault(i => i.Aliases is { Count: > 0 } aliases && aliases.Contains(symbolOrAlias, StringComparer.Ordinal));

    /// <summary>The output port named <paramref name="nameOrSymbol"/>: by its public name, its output symbol or an alias (<see cref="CalcOutputPort.Matches"/>), or <see langword="null"/>.</summary>
    public CalcOutputPort? FindPort(string nameOrSymbol) => OutputPorts?.FirstOrDefault(p => p.Matches(nameOrSymbol));

    /// <summary>
    /// Whether Check and Issue demand more than an ordinary template does (Addendum B, S8): a competent-person attestation, a
    /// second independent check, or both. A template that sets none of <see cref="RequiresAttestation"/> and
    /// <see cref="RequiresSecondCheck"/> is governed exactly as before.
    /// </summary>
    public bool HasExtraGovernance => RequiresAttestation || RequiresSecondCheck;
}

/// <summary>
/// The optional features a template can be gated behind (<see cref="CalcTemplate.FeatureFlag"/>). A gated template is composed
/// into the build but is withheld from the catalogue until its flag is switched on in the installation's configuration
/// (ADR-0173). Every flag is off by default.
/// </summary>
public static class CalcFeatures
{
    /// <summary>Lifting lugs and spreader beams (S8): LOLER/PUWER territory, off until the owner has completed the review checklist.</summary>
    public const string Lifting = "lifting";

    /// <summary>Every known feature flag.</summary>
    public static IReadOnlyList<string> All { get; } = [Lifting];
}

/// <summary>
/// The inputs a template that <see cref="CalcTemplate.RequiresAttestation"/> declares for the competent person's attestation
/// (ADR-0173). They are ordinary text inputs, so they print on the sheet like any other input; the Library refuses Check and Issue
/// while they are blank or name the calculation's own author.
/// </summary>
public static class CalcAttestation
{
    public const string NameSymbol = "attest_name";
    public const string QualificationSymbol = "attest_qualification";
    public const string StatementSymbol = "attest_statement";

    /// <summary>The shortest statement accepted: a bare "ok" is not an attestation.</summary>
    public const int MinStatementLength = 20;

    /// <summary>The three input symbols, in sheet order.</summary>
    public static IReadOnlyList<string> Symbols { get; } = [NameSymbol, QualificationSymbol, StatementSymbol];
}
