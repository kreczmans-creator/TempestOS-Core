using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringAssets.CalculationPacks;

/// <summary>How a calculation is actually carried out.</summary>
/// <remarks>
/// Recorded because it decides what the result is worth. A closed-form
/// hand calculation and a finite-element run are both legitimate and are
/// not the same evidence, and a spreadsheet nobody has verified is a
/// third thing again.
/// </remarks>
public enum CalculationMethodKind
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>A closed-form expression evaluated by hand or by TempestOS.</summary>
    ClosedForm,

    /// <summary>A method taken from a published standard or code.</summary>
    StandardMethod,

    /// <summary>An empirical correlation.</summary>
    Empirical,

    /// <summary>A numerical method — finite element, finite volume, iterative solution.</summary>
    Numerical,

    /// <summary>A spreadsheet the organisation maintains.</summary>
    Spreadsheet,

    /// <summary>Third-party analysis software.</summary>
    ExternalSoftware,

    /// <summary>Something else.</summary>
    Other
}

/// <summary>
/// One input a calculation pack takes, and where its value came from.
/// </summary>
/// <remarks>
/// <see cref="SourcePin"/> is what makes the input reproducible. A yield
/// strength taken from the Materials library at revision 2 keeps that
/// pin, so re-reading the pack resolves the figure that was actually
/// used rather than whatever the library says today.
/// </remarks>
/// <param name="Reference">The input's own identifier within the pack. Required.</param>
/// <param name="Description">What it is. Required.</param>
/// <param name="Value">The value as stated, including its unit where it has one. Required.</param>
/// <param name="SourcePin">The record revision the value came from. <see langword="null"/> where it came from elsewhere.</param>
/// <param name="SourceDescription">Where else it came from. <see langword="null"/> where a pin says.</param>
/// <param name="Dimension">The physical dimension, where the input is dimensioned. <see langword="null"/> otherwise.</param>
public sealed record CalculationInput(
    string Reference,
    string Description,
    string Value,
    ReferencePin? SourcePin = null,
    string? SourceDescription = null,
    string? Dimension = null)
{
    /// <summary>The input's own identifier within the pack.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A calculation input must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What it is.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A calculation input must say what it is.", nameof(Description))
        : Description.Trim();

    /// <summary>The value as stated.</summary>
    public string Value { get; } = string.IsNullOrWhiteSpace(Value)
        ? throw new ArgumentException("A calculation input must carry a value.", nameof(Value))
        : Value.Trim();

    /// <summary>Whether the value can be traced to a governed record at a known revision.</summary>
    public bool IsTraceable => SourcePin is not null;

    /// <summary>Whether the input says where its value came from at all.</summary>
    public bool HasStatedSource => SourcePin is not null || !string.IsNullOrWhiteSpace(SourceDescription);
}

/// <summary>One thing a calculation produces.</summary>
/// <param name="Reference">The output's own identifier within the pack. Required.</param>
/// <param name="Description">What it is. Required.</param>
/// <param name="Value">The value produced, including its unit. Required.</param>
/// <param name="Dimension">The physical dimension. <see langword="null"/> where dimensionless.</param>
/// <param name="AcceptanceCriterion">What the value must satisfy. <see langword="null"/> where nothing was stated.</param>
/// <param name="Interpretation">What the number means in engineering terms. <see langword="null"/> where nothing was written.</param>
public sealed record CalculationOutput(
    string Reference,
    string Description,
    string Value,
    string? Dimension = null,
    string? AcceptanceCriterion = null,
    string? Interpretation = null)
{
    /// <summary>The output's own identifier within the pack.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A calculation output must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What it is.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A calculation output must say what it is.", nameof(Description))
        : Description.Trim();

    /// <summary>The value produced.</summary>
    public string Value { get; } = string.IsNullOrWhiteSpace(Value)
        ? throw new ArgumentException("A calculation output must carry a value.", nameof(Value))
        : Value.Trim();

    /// <summary>
    /// Whether anybody stated what the number has to satisfy.
    /// </summary>
    /// <remarks>
    /// An output with no acceptance criterion is a number, not a
    /// conclusion. Reported rather than acted on: plenty of legitimate
    /// intermediate outputs have none.
    /// </remarks>
    public bool HasAcceptanceCriterion => !string.IsNullOrWhiteSpace(AcceptanceCriterion);
}

/// <summary>Something the calculation takes to be true that nobody has established.</summary>
/// <param name="Reference">The assumption's own identifier within the pack. Required.</param>
/// <param name="Statement">What is being assumed. Required.</param>
/// <param name="Justification">Why it is reasonable. <see langword="null"/> where nobody said.</param>
/// <param name="WouldInvalidate">What no longer holds if it is wrong. <see langword="null"/> where nobody said.</param>
public sealed record PackAssumption(
    string Reference,
    string Statement,
    string? Justification = null,
    string? WouldInvalidate = null)
{
    /// <summary>The assumption's own identifier within the pack.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A calculation assumption must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What is being assumed.</summary>
    public string Statement { get; } = string.IsNullOrWhiteSpace(Statement)
        ? throw new ArgumentException("A calculation assumption must say what is being assumed.", nameof(Statement))
        : Statement.Trim();

    /// <summary>Whether anybody said why the assumption is reasonable.</summary>
    public bool IsJustified => !string.IsNullOrWhiteSpace(Justification);
}

/// <summary>
/// How a calculation was carried out, precisely enough to do it again.
/// </summary>
/// <param name="Kind">What sort of method it is.</param>
/// <param name="Description">The method, in words. Required.</param>
/// <param name="GoverningEquations">The equations, as written. Never <see langword="null"/>.</param>
/// <param name="StandardReferences">The standards the method comes from, by `A2` record Id. Never <see langword="null"/>.</param>
/// <param name="ToolName">The software used, where any was. <see langword="null"/> otherwise.</param>
/// <param name="ToolVersion">Its version. <see langword="null"/> where unrecorded.</param>
/// <param name="CalculationDefinitionId">The TempestOS calculation definition used, where the platform performed it. <see langword="null"/> otherwise.</param>
public sealed record CalculationMethod(
    CalculationMethodKind Kind,
    string Description,
    IReadOnlyList<string>? GoverningEquations = null,
    IReadOnlyList<string>? StandardReferences = null,
    string? ToolName = null,
    string? ToolVersion = null,
    string? CalculationDefinitionId = null)
{
    /// <summary>The method, in words.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A calculation method must describe itself.", nameof(Description))
        : Description.Trim();

    /// <summary>The equations, as written.</summary>
    public IReadOnlyList<string> GoverningEquations { get; init; } = GoverningEquations ?? [];

    /// <summary>The standards the method comes from.</summary>
    public IReadOnlyList<string> StandardReferences { get; init; } = StandardReferences ?? [];

    /// <summary>Whether the pack names the tool version, where a tool was used.</summary>
    /// <remarks>
    /// A numerical result from unnamed software at an unnamed version is
    /// not reproducible, and the validation service says so.
    /// </remarks>
    public bool IsToolIdentified =>
        Kind is not (CalculationMethodKind.Numerical or CalculationMethodKind.ExternalSoftware or CalculationMethodKind.Spreadsheet)
        || (!string.IsNullOrWhiteSpace(ToolName) && !string.IsNullOrWhiteSpace(ToolVersion));

    /// <summary>Whether TempestOS itself performed the calculation.</summary>
    public bool IsPlatformCalculation => !string.IsNullOrWhiteSpace(CalculationDefinitionId);
}

/// <summary>
/// A governed engineering calculation, packaged so somebody can
/// understand it and do it again.
/// </summary>
/// <remarks>
/// <para>
/// <b>The pack is not the calculation engine.</b> `Tempest.Core.Calculations`
/// executes calculations and records executions; `E2` records what a
/// calculation <em>was</em> — its inputs and where they came from, its
/// method, its assumptions, its outputs, and who stands behind it. Where
/// the platform performed the arithmetic, the pack names the definition
/// and links the execution records rather than restating the result
/// (`ADR-0137`).
/// </para>
/// <para>
/// <b>Reproducibility is the point.</b> Every input pins the record
/// revision its value came from. A future reader asking "what did this
/// calculation use when it was performed?" gets the answer the pack was
/// built with, not whatever the libraries hold today, and superseding a
/// source raises a warning about the library rather than editing the
/// pack.
/// </para>
/// </remarks>
public sealed record CalculationPack
{
    /// <summary>The reference the pack is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What to call it. Required.</summary>
    public required string Title { get; init; }

    /// <summary>What question it answers. Required.</summary>
    public required string Purpose { get; init; }

    /// <summary>How it was done. Required.</summary>
    public required CalculationMethod Method { get; init; }

    /// <summary>What went in. Never <see langword="null"/>.</summary>
    public IReadOnlyList<CalculationInput> Inputs { get; init; } = [];

    /// <summary>What came out. Never <see langword="null"/>.</summary>
    public IReadOnlyList<CalculationOutput> Outputs { get; init; } = [];

    /// <summary>What it takes to be true. Never <see langword="null"/>.</summary>
    public IReadOnlyList<PackAssumption> Assumptions { get; init; } = [];

    /// <summary>What the pack does not cover. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> Limitations { get; init; } = [];

    /// <summary>The executions of this calculation TempestOS itself recorded, by <c>CalculationRecord</c> Id. Never <see langword="null"/>.</summary>
    /// <remarks>
    /// Ids rather than copies. The execution record is authoritative for
    /// what the engine actually computed, and duplicating its result here
    /// would create two answers that can disagree.
    /// </remarks>
    public IReadOnlyList<Guid> ExecutionRecordIds { get; init; } = [];

    /// <summary>The template this pack was produced from, at the revision worked from. <see langword="null"/> where none was used.</summary>
    public Templates.TemplateUsage? TemplateUsage { get; init; }

    /// <summary>Where and when it applies.</summary>
    public AssetApplicability Applicability { get; init; } = AssetApplicability.Unrestricted;

    /// <summary>Who owns it, who wrote it, who checked it.</summary>
    public AssetGovernanceFacts Governance { get; init; } = new();

    /// <summary>Anything else about it. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Every record revision the pack rests on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferencePin> AllPins =>
        Inputs.Select(i => i.SourcePin)
            .Concat(Governance.Evidence.Select(e => e.Pin))
            .Concat([TemplateUsage?.TemplatePin])
            .OfType<ReferencePin>()
            .Distinct()
            .OrderBy(p => p.Library, StringComparer.Ordinal)
            .ThenBy(p => p.RecordId, StringComparer.Ordinal)
            .ThenBy(p => p.RevisionNumber)
            .ToList();

    /// <summary>Inputs that cannot be traced to a governed record.</summary>
    public IReadOnlyList<CalculationInput> UntraceableInputs => Inputs.Where(i => !i.IsTraceable).ToList();

    /// <summary>Inputs that do not say where their value came from at all.</summary>
    public IReadOnlyList<CalculationInput> UnsourcedInputs => Inputs.Where(i => !i.HasStatedSource).ToList();

    /// <summary>Whether every input can be traced to a governed record at a known revision.</summary>
    public bool IsFullyTraceable => Inputs.Count > 0 && Inputs.All(i => i.IsTraceable);

    /// <summary>
    /// Whether somebody reading only this pack could carry the
    /// calculation out again.
    /// </summary>
    /// <remarks>
    /// Requires inputs, outputs, a stated method, every input sourced,
    /// and — where software was used — a named tool at a named version.
    /// Deliberately strict: the whole purpose of packaging a calculation
    /// is that it survives the person who did it.
    /// </remarks>
    public bool IsReproducible =>
        Inputs.Count > 0
        && Outputs.Count > 0
        && Method.Kind != CalculationMethodKind.Unspecified
        && UnsourcedInputs.Count == 0
        && Method.IsToolIdentified;

    /// <summary>Whether the pack has run past its own effective period as at <paramref name="asAt"/>.</summary>
    public bool IsStaleAt(DateOnly asAt) => Applicability.IsExpiredAt(asAt);

    /// <summary>The case-insensitive key <see cref="Reference"/> is indexed under.</summary>
    public string ReferenceKey => ReferenceKeyFor(Reference);

    /// <summary>The case-insensitive key <paramref name="reference"/> would be indexed under.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    public static string ReferenceKeyFor(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        return reference.Trim().ToUpperInvariant();
    }
}
