namespace Tempest.Core.Calculations;

/// <summary>
/// A fresh, per-execution recorder an
/// <see cref="ICalculationDefinition{TInput, TResult}"/> uses to declare
/// intermediate results, constraint outcomes, and referenced materials
/// while computing its own final result.
/// </summary>
/// <remarks>
/// <see cref="ICalculationEngine.ExecuteAsync{TInput, TResult}"/>
/// constructs a brand new instance for every execution — never shared
/// across executions, never ambient, never retained by the definition
/// itself beyond the single <see cref="ICalculationDefinition{TInput, TResult}.Calculate"/>
/// call it is passed to. This keeps <c>Calculate</c> a pure function in
/// every sense that matters: no I/O, no shared mutable state, and no
/// hidden side channel a caller cannot see — every value a definition
/// records here appears directly in the resulting
/// <see cref="CalculationRecord{TResult}"/>.
/// </remarks>
public sealed class CalculationContext
{
    private readonly List<CalculationIntermediateResult> _intermediateResults = [];
    private readonly List<CalculationConstraintCheck> _constraintChecks = [];
    private readonly List<string> _referencedMaterialIds = [];

    /// <summary>Records a named intermediate value computed while producing the final result.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or consists only of whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    public void RecordIntermediate(string name, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);

        _intermediateResults.Add(new CalculationIntermediateResult(name, value));
    }

    /// <summary>Records whether one declared constraint held for this execution's own actual input.</summary>
    /// <exception cref="ArgumentException"><paramref name="description"/> is empty or consists only of whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="description"/> is <see langword="null"/>.</exception>
    public void RecordConstraintCheck(string description, bool isSatisfied, string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        _constraintChecks.Add(new CalculationConstraintCheck(description, isSatisfied, detail));
    }

    /// <summary>
    /// Records that this execution referenced a material by Id (e.g. a
    /// <c>materialId</c> registered through <c>Tempest.Core.Materials.IMaterialCatalog</c>).
    /// This framework does not itself resolve or validate the reference —
    /// mirroring how <c>DocumentReference.RelationshipKind</c> is an open,
    /// unvalidated string elsewhere in this platform — since this
    /// framework has no dependency on Materials.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="materialId"/> is empty or consists only of whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="materialId"/> is <see langword="null"/>.</exception>
    public void ReferenceMaterial(string materialId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materialId);

        _referencedMaterialIds.Add(materialId);
    }

    /// <summary>Every intermediate result recorded so far, oldest first.</summary>
    public IReadOnlyList<CalculationIntermediateResult> IntermediateResults => _intermediateResults;

    /// <summary>Every constraint check recorded so far, oldest first.</summary>
    public IReadOnlyList<CalculationConstraintCheck> ConstraintChecks => _constraintChecks;

    /// <summary>Every material Id referenced so far, oldest first.</summary>
    public IReadOnlyList<string> ReferencedMaterialIds => _referencedMaterialIds;
}
