namespace Tempest.Core.Verification;

/// <summary>
/// A recorder a caller populates with explicit criteria, evidence, and
/// links before calling <see cref="IVerificationService.RecordAsync"/> —
/// mirrors <c>Calculations.CalculationContext</c>'s own shape, adapted for
/// a caller-supplied, not framework-dispatched, recording flow: nothing
/// here is executed by this framework, only carried into the resulting
/// <see cref="IVerificationRecord"/> unchanged.
/// </summary>
public sealed class VerificationContext
{
    private readonly List<VerificationCriterion> _criteria = [];
    private readonly List<VerificationEvidenceEntry> _evidence = [];
    private readonly List<Guid> _linkedDocumentIds = [];
    private readonly List<Guid> _linkedCalculationRecordIds = [];
    private readonly List<string> _referencedMaterialIds = [];

    /// <summary>Records whether one explicit criterion held.</summary>
    /// <exception cref="ArgumentException"><paramref name="description"/> is empty or consists only of whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="description"/> is <see langword="null"/>.</exception>
    public void RecordCriterion(string description, bool isSatisfied, string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        _criteria.Add(new VerificationCriterion(description, isSatisfied, detail));
    }

    /// <summary>Records one piece of supporting evidence.</summary>
    /// <exception cref="ArgumentException"><paramref name="description"/> is empty or consists only of whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="description"/> is <see langword="null"/>.</exception>
    public void RecordEvidence(string description, string? reference = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        _evidence.Add(new VerificationEvidenceEntry(description, reference));
    }

    /// <summary>
    /// Links this verification to another existing
    /// <c>EngineeringData.IEngineeringDocument</c> beyond the subject
    /// itself (e.g. a governing standard). Validated: a non-existent
    /// <paramref name="documentId"/> causes
    /// <see cref="IVerificationService.RecordAsync"/> to throw
    /// <see cref="EngineeringData.EngineeringDocumentNotFoundException"/>.
    /// </summary>
    public void LinkDocument(Guid documentId) => _linkedDocumentIds.Add(documentId);

    /// <summary>
    /// Links this verification to an existing calculation execution record
    /// (a <c>Calculations.CalculationRecord{TResult}.Id</c>, itself an
    /// <c>EngineeringData.IEngineeringDocument</c> Id — no compile-time
    /// dependency on <c>Tempest.Core.Calculations</c> is required to
    /// reference one). Validated the same way as
    /// <see cref="LinkDocument"/>.
    /// </summary>
    public void LinkCalculationRecord(Guid calculationRecordId) => _linkedCalculationRecordIds.Add(calculationRecordId);

    /// <summary>
    /// Records that this verification referenced a material by Id (e.g. a
    /// <c>materialId</c> registered through <c>Materials.IMaterialCatalog</c>).
    /// This framework does not itself resolve or validate the reference —
    /// mirroring <c>Calculations.CalculationContext.ReferenceMaterial</c>'s
    /// own identical, disclosed precedent — since this framework has no
    /// dependency on Materials.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="materialId"/> is empty or consists only of whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="materialId"/> is <see langword="null"/>.</exception>
    public void ReferenceMaterial(string materialId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(materialId);

        _referencedMaterialIds.Add(materialId);
    }

    /// <summary>Every criterion recorded so far, oldest first.</summary>
    public IReadOnlyList<VerificationCriterion> Criteria => _criteria;

    /// <summary>Every piece of evidence recorded so far, oldest first.</summary>
    public IReadOnlyList<VerificationEvidenceEntry> Evidence => _evidence;

    /// <summary>Every additional linked document Id recorded so far, oldest first.</summary>
    public IReadOnlyList<Guid> LinkedDocumentIds => _linkedDocumentIds;

    /// <summary>Every linked calculation record Id recorded so far, oldest first.</summary>
    public IReadOnlyList<Guid> LinkedCalculationRecordIds => _linkedCalculationRecordIds;

    /// <summary>Every referenced material Id recorded so far, oldest first.</summary>
    public IReadOnlyList<string> ReferencedMaterialIds => _referencedMaterialIds;
}
