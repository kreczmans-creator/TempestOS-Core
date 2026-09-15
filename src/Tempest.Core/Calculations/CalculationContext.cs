using System.Text;
using System.Text.Json;

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
    /// <summary>
    /// The default limit on how many intermediate results a single
    /// execution may record, when the engine constructing this context
    /// does not configure one explicitly (`TD-22`).
    /// </summary>
    /// <remarks>
    /// A guess, disclosed as one: no built-in definition records more than a
    /// handful of intermediates (the six engineering definitions record at
    /// most five each), so 200 leaves two orders of magnitude of headroom
    /// for a legitimately step-heavy calculation while still refusing a
    /// definition stuck in a loop.
    /// </remarks>
    public const int DefaultMaxIntermediateResultCount = 200;

    /// <summary>
    /// The default limit, in bytes, on the total estimated size of every
    /// intermediate result a single execution may record, when the engine
    /// constructing this context does not configure one explicitly (`TD-22`).
    /// </summary>
    /// <remarks>
    /// A guess, disclosed as one: 1 MiB is roughly four orders of magnitude
    /// larger than a typical recorded intermediate (a name and a boxed
    /// quantity serialise to well under a hundred bytes), leaving generous
    /// headroom while still bounding a definition that records something
    /// genuinely unbounded, such as a large string built in a loop.
    /// </remarks>
    public const long DefaultMaxIntermediateResultTotalSizeBytes = 1024 * 1024;

    private readonly List<CalculationIntermediateResult> _intermediateResults = [];
    private readonly List<CalculationConstraintCheck> _constraintChecks = [];
    private readonly List<string> _referencedMaterialIds = [];
    private readonly string _definitionId;
    private readonly int _maxIntermediateResultCount;
    private readonly long _maxIntermediateResultTotalSizeBytes;
    private long _intermediateResultTotalSizeBytes;

    /// <summary>
    /// Initialises a new instance of the <see cref="CalculationContext"/>
    /// class with no definition identity and the default bound — for a
    /// caller (typically a test, or a property-based proof) that
    /// constructs a definition's own <see cref="ICalculationDefinition{TInput, TResult}.Calculate"/>
    /// directly, outside <see cref="ICalculationEngine.ExecuteAsync{TInput, TResult}"/>.
    /// </summary>
    public CalculationContext()
        : this(definitionId: "(unspecified)")
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="CalculationContext"/>
    /// class for one execution of <paramref name="definitionId"/>, bounded
    /// as given (`TD-22`).
    /// </summary>
    /// <param name="definitionId">The definition this context's own recorded values will belong to — named by <see cref="CalculationBoundExceededException"/> if a bound is exceeded.</param>
    /// <param name="maxIntermediateResultCount">The most intermediate results this execution may record before <see cref="RecordIntermediate"/> refuses further ones.</param>
    /// <param name="maxIntermediateResultTotalSizeBytes">The most total estimated bytes this execution's own intermediate results may occupy before <see cref="RecordIntermediate"/> refuses further ones.</param>
    /// <exception cref="ArgumentException"><paramref name="definitionId"/> is empty or consists only of whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxIntermediateResultCount"/> or <paramref name="maxIntermediateResultTotalSizeBytes"/> is not positive.</exception>
    public CalculationContext(
        string definitionId,
        int maxIntermediateResultCount = DefaultMaxIntermediateResultCount,
        long maxIntermediateResultTotalSizeBytes = DefaultMaxIntermediateResultTotalSizeBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionId);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxIntermediateResultCount, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxIntermediateResultTotalSizeBytes, 0);

        _definitionId = definitionId;
        _maxIntermediateResultCount = maxIntermediateResultCount;
        _maxIntermediateResultTotalSizeBytes = maxIntermediateResultTotalSizeBytes;
    }

    /// <summary>Records a named intermediate value computed while producing the final result.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or consists only of whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="CalculationBoundExceededException">
    /// Recording <paramref name="value"/> would exceed this context's own
    /// configured count or total-size bound (`TD-22`) — refused before it is
    /// added, so <see cref="IntermediateResults"/> never exceeds the bound
    /// even transiently.
    /// </exception>
    public void RecordIntermediate(string name, object value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);

        if (_intermediateResults.Count >= _maxIntermediateResultCount)
            throw new CalculationBoundExceededException(_definitionId, CalculationBoundKind.IntermediateResultCount, _maxIntermediateResultCount);

        var entry = new CalculationIntermediateResult(name, value);
        var entrySizeBytes = EstimateSizeBytes(entry);

        if (_intermediateResultTotalSizeBytes + entrySizeBytes > _maxIntermediateResultTotalSizeBytes)
            throw new CalculationBoundExceededException(_definitionId, CalculationBoundKind.IntermediateResultTotalSize, _maxIntermediateResultTotalSizeBytes);

        _intermediateResultTotalSizeBytes += entrySizeBytes;
        _intermediateResults.Add(entry);
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

    /// <summary>
    /// A cheap, deterministic estimate of how many bytes recording
    /// <paramref name="entry"/> costs — its own name, plus its value
    /// JSON-serialized exactly as <see cref="CalculationEngine.ExecuteAsync{TInput, TResult}"/>
    /// will actually persist it, so the bound tracks real storage rather
    /// than a heuristic unrelated to it.
    /// </summary>
    private static long EstimateSizeBytes(CalculationIntermediateResult entry)
    {
        try
        {
            return Encoding.UTF8.GetByteCount(entry.Name) + JsonSerializer.SerializeToUtf8Bytes(entry.Value).LongLength;
        }
        catch (NotSupportedException)
        {
            // A value type System.Text.Json cannot serialize is vanishingly
            // rare for a boxed quantity/primitive/string, but refusing to
            // record it here — rather than letting an unrelated exception
            // surface as the failure — is not this bound's job; fall back to
            // a conservative flat estimate instead.
            return Encoding.UTF8.GetByteCount(entry.Name) + 256;
        }
    }
}
