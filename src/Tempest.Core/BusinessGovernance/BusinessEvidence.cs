using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance;

/// <summary>What kind of thing supports a business assertion.</summary>
public enum BusinessEvidenceKind
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>An executed contract, deed or other signed instrument.</summary>
    ExecutedDocument,

    /// <summary>An insurance policy, schedule or certificate.</summary>
    InsuranceDocument,

    /// <summary>Written advice from a solicitor or other legal adviser.</summary>
    LegalAdvice,

    /// <summary>Accounts, a return, or written advice from an accountant.</summary>
    AccountingRecord,

    /// <summary>An invoice, receipt, statement or bank record.</summary>
    FinancialRecord,

    /// <summary>A quotation, proposal or written offer, from a supplier or to a client.</summary>
    Quotation,

    /// <summary>Correspondence — an email trail, a letter, a minuted conversation.</summary>
    Correspondence,

    /// <summary>Published guidance from a government body, regulator or standards organisation.</summary>
    PublishedGuidance,

    /// <summary>A registration, filing or certificate held by a registry.</summary>
    Registration,

    /// <summary>A person's own judgement, recorded and attributed.</summary>
    RecordedJudgement,

    /// <summary>A TempestOS record at a stated revision.</summary>
    InternalRecord,

    /// <summary>Something else, described in the reference.</summary>
    Other
}

/// <summary>
/// What supports a business assertion, and where to find it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The point of this type is that it can be absent.</b> A record
/// asserting insurance coverage with no evidence, or IP ownership with no
/// evidence, is exactly the record `P07` must be able to identify — and it
/// can only identify it if evidence is a first-class, countable thing
/// rather than a sentence somebody may or may not have typed into a notes
/// field.
/// </para>
/// <para>
/// Evidence points outward. Most of it lives in documents, registries and
/// other people's systems, so this type records enough to find it —
/// what kind, who issued it, its own reference, its date — without
/// pretending to hold it. Where the evidence <i>is</i> in TempestOS,
/// <see cref="DocumentId"/> names the document and <see cref="Pin"/> names
/// the exact revision.
/// </para>
/// </remarks>
/// <param name="Kind">What kind of evidence this is.</param>
/// <param name="Description">What it says, in the recorder's own words. Required.</param>
/// <param name="Issuer">Who produced it — an insurer, a solicitor, a registry, a client. <see langword="null"/> if not recorded.</param>
/// <param name="Reference">The evidence's own identifier — a policy number, a document reference, a registration number. <see langword="null"/> if it has none.</param>
/// <param name="DocumentDate">The date the evidence itself bears. <see langword="null"/> if undated or not recorded.</param>
/// <param name="DocumentId">The TempestOS document holding it, where it is held here. <see langword="null"/> where the evidence is external.</param>
/// <param name="Pin">The exact record revision, where the evidence is another governed record. <see langword="null"/> otherwise.</param>
public sealed record BusinessEvidence(
    BusinessEvidenceKind Kind,
    string Description,
    string? Issuer = null,
    string? Reference = null,
    DateOnly? DocumentDate = null,
    Guid? DocumentId = null,
    ReferencePin? Pin = null)
{
    /// <summary>What the evidence says, in the recorder's own words.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("Evidence must say what it evidences. A bare reference nobody can interpret is not evidence.", nameof(Description))
        : Description.Trim();

    /// <summary>
    /// Whether the evidence can actually be retrieved and checked by
    /// somebody reading this record.
    /// </summary>
    /// <remarks>
    /// Evidence that is neither held in TempestOS nor identified by an
    /// external reference is an assertion with a description attached.
    /// It is still worth recording, and it is not the same thing.
    /// </remarks>
    public bool IsLocatable => DocumentId is not null || Pin is not null || !string.IsNullOrWhiteSpace(Reference);

    /// <summary>Evidence held as a TempestOS document.</summary>
    /// <exception cref="ArgumentException"><paramref name="description"/> is blank.</exception>
    public static BusinessEvidence FromDocument(BusinessEvidenceKind kind, string description, Guid documentId) =>
        new(kind, description, DocumentId: documentId);

    /// <summary>Evidence that is another governed record, at a stated revision.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="pin"/> is <see langword="null"/>.</exception>
    public static BusinessEvidence FromRecord(string description, ReferencePin pin)
    {
        ArgumentNullException.ThrowIfNull(pin);

        return new BusinessEvidence(BusinessEvidenceKind.InternalRecord, description, Reference: pin.ToString(), Pin: pin);
    }

    /// <summary>A person's own judgement, attributed to them.</summary>
    /// <exception cref="ArgumentException"><paramref name="principalId"/> is blank.</exception>
    public static BusinessEvidence FromJudgement(string description, string principalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalId);

        return new BusinessEvidence(BusinessEvidenceKind.RecordedJudgement, description, Issuer: principalId.Trim());
    }
}
