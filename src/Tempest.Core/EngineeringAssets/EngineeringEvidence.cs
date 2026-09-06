using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringAssets;

/// <summary>What kind of thing is being offered as engineering evidence.</summary>
/// <remarks>
/// A separate vocabulary from `P07`'s <see cref="BusinessGovernance.BusinessEvidenceKind"/>
/// on purpose. The two records have the same shape and materially
/// different meanings: an insurance certificate is not engineering
/// evidence and a test report is not a business record. Sharing the
/// mechanism while keeping the vocabulary distinct is the same call
/// `ADR-0132` made for commercial data.
/// </remarks>
public enum EngineeringEvidenceKind
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>A physical or functional test and its results.</summary>
    TestReport,

    /// <summary>An inspection or measurement record.</summary>
    InspectionRecord,

    /// <summary>An analysis — hand, FEA, CFD or otherwise.</summary>
    AnalysisReport,

    /// <summary>A calculation the platform itself performed and recorded.</summary>
    CalculationRecord,

    /// <summary>A drawing, model or other design definition.</summary>
    DesignDefinition,

    /// <summary>A certificate of conformity, material certificate or calibration certificate.</summary>
    Certificate,

    /// <summary>A published standard or specification.</summary>
    PublishedStandard,

    /// <summary>A supplier's or subcontractor's own declaration.</summary>
    SupplierDeclaration,

    /// <summary>Minutes, a note, or correspondence.</summary>
    Correspondence,

    /// <summary>A record held inside TempestOS.</summary>
    InternalRecord,

    /// <summary>
    /// A named engineer's judgement, with nothing else behind it.
    /// </summary>
    /// <remarks>
    /// A legitimate and frequently necessary kind of evidence, and the
    /// weakest. Recorded as its own kind so it can never be mistaken for
    /// a measurement.
    /// </remarks>
    EngineeringJudgement,

    /// <summary>Something else.</summary>
    Other
}

/// <summary>
/// One thing offered in support of an engineering claim, and where to
/// find it.
/// </summary>
/// <remarks>
/// <para>
/// Evidence that cannot be located is the failure this type exists to
/// make visible. "We tested it" with no test report, no document Id and
/// no reference is a recollection, and <see cref="IsLocatable"/> says so
/// without deciding what to do about it.
/// </para>
/// <para>
/// <see cref="Pin"/> rather than a bare record Id where the evidence is
/// another governed record, so the evidence keeps pointing at the
/// revision that was actually relied on.
/// </para>
/// </remarks>
/// <param name="Kind">What sort of evidence this is.</param>
/// <param name="Description">What it shows, in plain words. Required.</param>
/// <param name="DocumentId">The engineering document holding it, where one does. <see langword="null"/> otherwise.</param>
/// <param name="Reference">An external reference — a report number, a drawing number. <see langword="null"/> where there is none.</param>
/// <param name="Pin">The exact record revision, where the evidence is another governed record. <see langword="null"/> otherwise.</param>
/// <param name="PrincipalId">Whose judgement it is, where the kind is <see cref="EngineeringEvidenceKind.EngineeringJudgement"/>. <see langword="null"/> otherwise.</param>
/// <param name="RecordedOn">When the evidence was recorded. <see langword="null"/> where unrecorded.</param>
public sealed record EngineeringEvidence(
    EngineeringEvidenceKind Kind,
    string Description,
    Guid? DocumentId = null,
    string? Reference = null,
    ReferencePin? Pin = null,
    string? PrincipalId = null,
    DateOnly? RecordedOn = null)
{
    /// <summary>What the evidence shows.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("Engineering evidence must say what it shows.", nameof(Description))
        : Description.Trim();

    /// <summary>Whether somebody could actually go and find this.</summary>
    public bool IsLocatable => DocumentId is not null || Pin is not null || !string.IsNullOrWhiteSpace(Reference);

    /// <summary>Whether this is somebody's judgement rather than an observation.</summary>
    public bool IsJudgement => Kind == EngineeringEvidenceKind.EngineeringJudgement;

    /// <summary>Whether the evidence is independent of the person asserting the claim.</summary>
    /// <remarks>
    /// Judgement and internal records are not; a test report, an
    /// inspection, a certificate or a published standard is. Reported
    /// rather than acted on: a design verified entirely on internal
    /// judgement may be perfectly sound and is a different kind of
    /// evidence from one verified by test.
    /// </remarks>
    public bool IsIndependent => Kind is EngineeringEvidenceKind.TestReport
        or EngineeringEvidenceKind.InspectionRecord
        or EngineeringEvidenceKind.Certificate
        or EngineeringEvidenceKind.PublishedStandard
        or EngineeringEvidenceKind.SupplierDeclaration;

    /// <summary>Evidence held in an engineering document.</summary>
    /// <exception cref="ArgumentException"><paramref name="documentId"/> is empty.</exception>
    public static EngineeringEvidence FromDocument(EngineeringEvidenceKind kind, string description, Guid documentId) =>
        documentId == Guid.Empty
            ? throw new ArgumentException("Evidence held in a document must name the document.", nameof(documentId))
            : new EngineeringEvidence(kind, description, DocumentId: documentId);

    /// <summary>Evidence that is another governed record at a stated revision.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="pin"/> is <see langword="null"/>.</exception>
    public static EngineeringEvidence FromRecord(string description, ReferencePin pin)
    {
        ArgumentNullException.ThrowIfNull(pin);

        return new EngineeringEvidence(EngineeringEvidenceKind.InternalRecord, description, Pin: pin);
    }

    /// <summary>A named engineer's judgement.</summary>
    /// <exception cref="ArgumentException"><paramref name="principalId"/> is null, empty, or whitespace.</exception>
    public static EngineeringEvidence FromJudgement(string description, string principalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalId);

        return new EngineeringEvidence(
            EngineeringEvidenceKind.EngineeringJudgement,
            description,
            PrincipalId: principalId.Trim());
    }
}
