using Tempest.Core.ReferenceData;

namespace Tempest.Core.Knowledge.WorkedExamples;

/// <summary>What one step of a worked example does.</summary>
public enum WorkedStepKind
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Setting out what is known and what is wanted.</summary>
    Setup,

    /// <summary>Stating something taken to be true.</summary>
    Assumption,

    /// <summary>Choosing an approach, and saying why.</summary>
    MethodChoice,

    /// <summary>Doing arithmetic.</summary>
    Calculation,

    /// <summary>Looking something up.</summary>
    Lookup,

    /// <summary>Checking a result against something independent.</summary>
    Check,

    /// <summary>Saying what the number means.</summary>
    Interpretation,

    /// <summary>Reaching the answer.</summary>
    Conclusion
}

/// <summary>
/// One step of a worked example.
/// </summary>
/// <remarks>
/// <see cref="Reasoning"/> is what separates a worked example from an
/// answer sheet. A reader can follow arithmetic without learning
/// anything; what teaches is why this step and not another
/// (`ADR-0141`).
/// </remarks>
/// <param name="Reference">The step's own identifier within the example. Required.</param>
/// <param name="Kind">What the step does.</param>
/// <param name="Description">What is being done. Required.</param>
/// <param name="Reasoning">Why it is being done this way. <see langword="null"/> where nothing was written.</param>
/// <param name="Expression">The working, as written. <see langword="null"/> where the step is not arithmetic.</param>
/// <param name="Result">What the step produced, with its unit. <see langword="null"/> where it produced no value.</param>
/// <param name="SourcePin">The record revision a looked-up value came from. <see langword="null"/> otherwise.</param>
public sealed record WorkedStep(
    string Reference,
    WorkedStepKind Kind,
    string Description,
    string? Reasoning = null,
    string? Expression = null,
    string? Result = null,
    ReferencePin? SourcePin = null)
{
    /// <summary>The step's own identifier within the example.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A worked step must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What is being done.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A worked step must say what is being done.", nameof(Description))
        : Description.Trim();

    /// <summary>Whether the step explains itself rather than merely happening.</summary>
    public bool IsExplained => !string.IsNullOrWhiteSpace(Reasoning);

    /// <summary>Whether a looked-up value can be traced to a governed record.</summary>
    public bool IsTraceable => SourcePin is not null;
}

/// <summary>
/// A complete engineering problem, worked through so somebody can follow
/// it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The reasoning is the content.</b> A worked example whose steps show
/// arithmetic without saying why teaches a reader to reproduce the
/// arithmetic, which is the one thing they did not need a worked example
/// for.
/// </para>
/// <para>
/// <b>Not a second calculation engine.</b> Where the platform performed
/// the arithmetic, the example links the `E2` calculation pack and the
/// execution records rather than restating results, on the same reasoning
/// `ADR-0137` sets out for `P05`.
/// </para>
/// <para>
/// A worked example is designed to become an Academy artefact: `F2`
/// activities cite one by reference, so the same example serves a lesson,
/// a challenge's model answer and a standalone reference without being
/// copied into any of them.
/// </para>
/// </remarks>
public sealed record WorkedExample
{
    /// <summary>The reference the example is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What to call it. Required.</summary>
    public required string Title { get; init; }

    /// <summary>The problem being solved. Required.</summary>
    public required string ProblemStatement { get; init; }

    /// <summary>What is given, with units. Never <see langword="null"/>.</summary>
    public IReadOnlyList<WorkedValue> Inputs { get; init; } = [];

    /// <summary>What is taken to be true. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> Assumptions { get; init; } = [];

    /// <summary>How it is solved, in a sentence. <see langword="null"/> where nothing was written.</summary>
    public string? MethodSummary { get; init; }

    /// <summary>The working, in order. Never <see langword="null"/>.</summary>
    public IReadOnlyList<WorkedStep> Steps { get; init; } = [];

    /// <summary>The answer, with its unit. <see langword="null"/> where the example reaches none.</summary>
    public WorkedValue? Result { get; init; }

    /// <summary>
    /// What the answer means in engineering terms.
    /// </summary>
    /// <remarks>
    /// Required in substance. "48 MPa" is a number; "48 MPa against an
    /// allowable of 120, so comfortable, and the deflection will govern
    /// before the stress does" is engineering.
    /// </remarks>
    public string? Interpretation { get; init; }

    /// <summary>How the answer was checked against something independent. <see langword="null"/> where it was not.</summary>
    public string? Verification { get; init; }

    /// <summary>What a reader should take away, beyond the answer. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> TeachingPoints { get; init; } = [];

    /// <summary>Mistakes a reader is likely to make here. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> CommonMistakes { get; init; } = [];

    /// <summary>The `E2` calculation pack holding the platform's own version. <see langword="null"/> where none does.</summary>
    public string? CalculationPackReference { get; init; }

    /// <summary>Where the problem or method came from. Never <see langword="null"/>.</summary>
    public IReadOnlyList<KnowledgeCitation> References { get; init; } = [];

    /// <summary>Where it applies and who it is for.</summary>
    public KnowledgeApplicability Applicability { get; init; } = KnowledgeApplicability.Unrestricted;

    /// <summary>Where it came from and who has checked it.</summary>
    public KnowledgeProvenance Provenance { get; init; } = new();

    /// <summary>Anything else about it. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Steps that show working without saying why.</summary>
    public IReadOnlyList<WorkedStep> UnexplainedSteps => Steps.Where(s => !s.IsExplained).ToList();

    /// <summary>Every record revision a looked-up value came from. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferencePin> AllPins =>
        Steps.Select(s => s.SourcePin)
            .Concat(Provenance.SourcePins)
            .OfType<ReferencePin>()
            .Distinct()
            .OrderBy(p => p.Library, StringComparer.Ordinal)
            .ThenBy(p => p.RecordId, StringComparer.Ordinal)
            .ThenBy(p => p.RevisionNumber)
            .ToList();

    /// <summary>Whether the example reaches an answer and says what it means.</summary>
    public bool IsComplete =>
        Steps.Count > 0
        && Result is not null
        && !string.IsNullOrWhiteSpace(Interpretation);

    /// <summary>
    /// Whether the example teaches rather than merely demonstrating.
    /// </summary>
    /// <remarks>
    /// Requires that every step explains itself and that the example says
    /// what a reader should take away. An example that is complete but
    /// not instructive is a correct answer nobody learns from.
    /// </remarks>
    public bool IsInstructive =>
        IsComplete
        && UnexplainedSteps.Count == 0
        && TeachingPoints.Count > 0;

    /// <summary>Whether the answer was checked against something independent.</summary>
    public bool IsVerified => !string.IsNullOrWhiteSpace(Verification);

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

/// <summary>
/// A named quantity in a worked example, with its unit written out.
/// </summary>
/// <remarks>
/// <see cref="Unit"/> is a separate field rather than part of the value
/// text, so a validation service can ask whether a quantity carries one.
/// Unit errors are the commonest way an engineering calculation goes
/// wrong, and a teaching example that omits units teaches the habit.
/// </remarks>
/// <param name="Symbol">What it is called in the working — <c>"M"</c>, <c>"sigma"</c>. Required.</param>
/// <param name="Description">What it is. Required.</param>
/// <param name="Value">The value, as written. Required.</param>
/// <param name="Unit">Its unit. <see langword="null"/> where the quantity is genuinely dimensionless.</param>
/// <param name="IsDimensionless">Whether the quantity has no unit by nature, rather than by omission.</param>
public sealed record WorkedValue(
    string Symbol,
    string Description,
    string Value,
    string? Unit = null,
    bool IsDimensionless = false)
{
    /// <summary>What it is called in the working.</summary>
    public string Symbol { get; } = string.IsNullOrWhiteSpace(Symbol)
        ? throw new ArgumentException("A worked value must carry a symbol.", nameof(Symbol))
        : Symbol.Trim();

    /// <summary>What it is.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A worked value must say what it is.", nameof(Description))
        : Description.Trim();

    /// <summary>The value, as written.</summary>
    public string Value { get; } = string.IsNullOrWhiteSpace(Value)
        ? throw new ArgumentException("A worked value must carry a value.", nameof(Value))
        : Value.Trim();

    /// <summary>Whether the quantity states a unit, or is explicitly dimensionless.</summary>
    public bool HasStatedUnit => IsDimensionless || !string.IsNullOrWhiteSpace(Unit);
}
