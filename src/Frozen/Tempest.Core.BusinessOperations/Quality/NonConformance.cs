using Tempest.Core.EngineeringAssets;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessOperations.Quality;

/// <summary>What kind of thing failed to conform.</summary>
public enum NonConformanceKind
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>A part or product does not meet its specification.</summary>
    Product,

    /// <summary>A process was not followed, or does not work.</summary>
    Process,

    /// <summary>A document is wrong, missing or out of date.</summary>
    Documentation,

    /// <summary>Something bought in does not conform.</summary>
    Supplied,

    /// <summary>The management system itself does not do what it says.</summary>
    System,

    /// <summary>Something else.</summary>
    Other
}

/// <summary>How serious a non-conformance is.</summary>
public enum NonConformanceSeverity
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>A remark; nothing must change.</summary>
    Observation,

    /// <summary>Something to put right, with no wider consequence.</summary>
    Minor,

    /// <summary>Something that affects fitness for purpose or the system's integrity.</summary>
    Major,

    /// <summary>Something that could affect safety or has escaped to a customer.</summary>
    Critical
}

/// <summary>What is to be done with the non-conforming thing itself.</summary>
/// <remarks>
/// Distinct from the corrective action. The disposition decides what
/// happens to <em>this</em> part; the corrective action decides what
/// happens so there is no next one. Conflating them is how a business
/// reworks the same fault for years (`ADR-0142`).
/// </remarks>
public enum DispositionKind
{
    /// <summary>Nobody has decided yet.</summary>
    Undecided,

    /// <summary>Put it right so it conforms.</summary>
    Rework,

    /// <summary>Bring it closer to conforming without fully conforming.</summary>
    Repair,

    /// <summary>Accept it as it is, against a stated justification.</summary>
    UseAsIs,

    /// <summary>Send it back.</summary>
    ReturnToSupplier,

    /// <summary>Scrap it.</summary>
    Scrap,

    /// <summary>Use it for something else it does suit.</summary>
    Regrade
}

/// <summary>What was decided about the non-conforming thing, and by whom.</summary>
/// <remarks>
/// <see cref="UseAsIs"/> and <see cref="DispositionKind.Repair"/> are
/// concessions — the thing does not conform and is being used anyway —
/// and both need somebody accountable and a stated justification.
/// Validation treats an unjustified concession as an error.
/// </remarks>
/// <param name="Kind">What is to be done.</param>
/// <param name="DecidedByPrincipalId">Who decided. Required.</param>
/// <param name="DecidedOn">When. <see langword="null"/> where unrecorded.</param>
/// <param name="Justification">Why, which a concession must carry. <see langword="null"/> otherwise.</param>
/// <param name="CustomerApprovalReference">Where the customer had to agree, the record of their doing so. <see langword="null"/> otherwise.</param>
public sealed record Disposition(
    DispositionKind Kind,
    string DecidedByPrincipalId,
    DateOnly? DecidedOn = null,
    string? Justification = null,
    string? CustomerApprovalReference = null)
{
    /// <summary>Who decided.</summary>
    public string DecidedByPrincipalId { get; } = string.IsNullOrWhiteSpace(DecidedByPrincipalId)
        ? throw new ArgumentException(
            "A disposition must name the person who decided it. TempestOS records dispositions and makes none.",
            nameof(DecidedByPrincipalId))
        : DecidedByPrincipalId.Trim();

    /// <summary>Whether the thing is being used despite not conforming.</summary>
    public bool IsConcession => Kind is DispositionKind.UseAsIs or DispositionKind.Repair;

    /// <summary>Whether a concession states why it is acceptable.</summary>
    public bool IsJustified => !IsConcession || !string.IsNullOrWhiteSpace(Justification);
}

/// <summary>Whether an action stops a recurrence or prevents a first occurrence.</summary>
public enum QualityActionKind
{
    /// <summary>Stops this from happening again.</summary>
    Corrective,

    /// <summary>Stops something similar happening at all.</summary>
    Preventive
}

/// <summary>Something done so the problem does not happen again.</summary>
/// <remarks>
/// Distinct from the disposition, and from `P05`'s review action. This
/// one addresses a <em>cause</em>; a disposition addresses a
/// <em>part</em>.
/// </remarks>
/// <param name="Reference">The action's own identifier within the record. Required.</param>
/// <param name="Description">What is to be done. Required.</param>
/// <param name="Kind">Whether it stops a recurrence or prevents a first occurrence.</param>
/// <param name="AddressesCauseReferences">The causes it addresses. Never <see langword="null"/>.</param>
/// <param name="Facts">Who owns it, and where it stands.</param>
/// <param name="EffectivenessEvidence">What shows it worked. Never <see langword="null"/>.</param>
/// <param name="EffectivenessReviewedOn">When somebody checked it worked. <see langword="null"/> until they do.</param>
public sealed record QualityAction(
    string Reference,
    string Description,
    QualityActionKind Kind = QualityActionKind.Corrective,
    IReadOnlyList<string>? AddressesCauseReferences = null,
    OperationalFacts? Facts = null,
    IReadOnlyList<EngineeringEvidence>? EffectivenessEvidence = null,
    DateOnly? EffectivenessReviewedOn = null)
{
    /// <summary>The action's own identifier within the record.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A quality action must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What is to be done.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A quality action must say what is to be done.", nameof(Description))
        : Description.Trim();

    /// <summary>The causes it addresses.</summary>
    public IReadOnlyList<string> AddressesCauseReferences { get; init; } = AddressesCauseReferences ?? [];

    /// <summary>Who owns it, and where it stands.</summary>
    public OperationalFacts Facts { get; init; } = Facts ?? new OperationalFacts();

    /// <summary>What shows it worked.</summary>
    public IReadOnlyList<EngineeringEvidence> EffectivenessEvidence { get; init; } = EffectivenessEvidence ?? [];

    /// <summary>
    /// Whether somebody has confirmed the action actually worked.
    /// </summary>
    /// <remarks>
    /// Closed and effective are different things, and the gap between
    /// them is where a business closes the same non-conformance twice —
    /// the same distinction `P06`'s `F4` draws for lessons.
    /// </remarks>
    public bool IsVerifiedEffective =>
        EffectivenessReviewedOn is not null && EffectivenessEvidence.Any(e => e.IsLocatable);

    /// <summary>Whether it still needs somebody's attention.</summary>
    public bool IsOutstanding => Facts.IsOutstanding;
}

/// <summary>
/// Something that did not conform, and what the business did about it.
/// </summary>
/// <remarks>
/// <para>
/// Three separate things, kept apart deliberately: the
/// <b>non-conformance</b> (what was wrong), the <b>disposition</b> (what
/// happens to the affected item), and the <b>corrective action</b> (what
/// happens so there is no next one). A business that conflates the second
/// and third reworks the same fault for years (`ADR-0142`).
/// </para>
/// <para>
/// <b>No compliance claim.</b> `WP04.5` gives a business somewhere to
/// record non-conformances in a shape that resembles what quality
/// standards ask for. It does not assert conformity to any standard,
/// certify anything, or claim the business's system meets ISO 9001 or any
/// other scheme.
/// </para>
/// </remarks>
public sealed record NonConformance
{
    /// <summary>The reference the non-conformance is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What was wrong. Required.</summary>
    public required string Description { get; init; }

    /// <summary>What was expected instead. Required.</summary>
    public required string RequirementNotMet { get; init; }

    /// <summary>What kind of thing failed.</summary>
    public NonConformanceKind Kind { get; init; } = NonConformanceKind.Unspecified;

    /// <summary>How serious it is.</summary>
    public NonConformanceSeverity Severity { get; init; } = NonConformanceSeverity.Unspecified;

    /// <summary>What is affected — a part number, a batch, a document. <see langword="null"/> where unrecorded.</summary>
    public string? AffectedItem { get; init; }

    /// <summary>How many are affected. <see langword="null"/> where not counted, or not countable.</summary>
    public int? AffectedQuantity { get; init; }

    /// <summary>How it was found. <see langword="null"/> where unrecorded.</summary>
    public string? DetectedBy { get; init; }

    /// <summary>The supplier it came from, where it did. <see langword="null"/> otherwise.</summary>
    public PartyReference? Supplier { get; init; }

    /// <summary>The purchase order it arrived on, by reference. <see langword="null"/> where none.</summary>
    public string? PurchaseOrderReference { get; init; }

    /// <summary>What caused it. Never <see langword="null"/>.</summary>
    /// <remarks>
    /// Reuses `P06`'s <see cref="Knowledge.Lessons.FailureCause"/>, which
    /// already separates a suspected cause from an established one and
    /// carries its own evidence. A second cause model would be a second
    /// answer to the same question.
    /// </remarks>
    public IReadOnlyList<Knowledge.Lessons.FailureCause> Causes { get; init; } = [];

    /// <summary>What is to be done with the affected item. <see langword="null"/> until somebody decides.</summary>
    public Disposition? Disposition { get; init; }

    /// <summary>What is being done so it does not happen again. Never <see langword="null"/>.</summary>
    public IReadOnlyList<QualityAction> Actions { get; init; } = [];

    /// <summary>What supports the account. Never <see langword="null"/>.</summary>
    public IReadOnlyList<EngineeringEvidence> Evidence { get; init; } = [];

    /// <summary>Governed records this rests on, at the revisions relied on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferencePin> SourcePins { get; init; } = [];

    /// <summary>The `P06` lesson record raised from it, by reference. <see langword="null"/> where none was.</summary>
    /// <remarks>
    /// The seam from an operational failure to organisational memory.
    /// `P04` records what went wrong on the day; `P06`'s `F4` records what
    /// the business learned, and the two are separate because most
    /// non-conformances teach nothing new.
    /// </remarks>
    public string? LessonReference { get; init; }

    /// <summary>Who owns it, and where it stands.</summary>
    public OperationalFacts Facts { get; init; } = new();

    /// <summary>Anything else about it. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>The causes somebody judged root causes.</summary>
    public IReadOnlyList<Knowledge.Lessons.FailureCause> RootCauses => Causes.Where(c => c.IsRootCause).ToList();

    /// <summary>The corrective actions.</summary>
    public IReadOnlyList<QualityAction> CorrectiveActions => Actions.Where(a => a.Kind == QualityActionKind.Corrective).ToList();

    /// <summary>The preventive actions.</summary>
    public IReadOnlyList<QualityAction> PreventiveActions => Actions.Where(a => a.Kind == QualityActionKind.Preventive).ToList();

    /// <summary>Actions still needing somebody's attention.</summary>
    public IReadOnlyList<QualityAction> OutstandingActions => Actions.Where(a => a.IsOutstanding).ToList();

    /// <summary>Root causes nothing addresses.</summary>
    public IReadOnlyList<Knowledge.Lessons.FailureCause> UnaddressedRootCauses =>
        RootCauses
            .Where(c => !Actions.Any(a => a.AddressesCauseReferences.Contains(c.Reference, StringComparer.OrdinalIgnoreCase)))
            .ToList();

    /// <summary>Whether the affected item has been dealt with.</summary>
    public bool IsDisposed => Disposition is { Kind: not DispositionKind.Undecided };

    /// <summary>Whether the thing is being used despite not conforming.</summary>
    public bool IsConcession => Disposition?.IsConcession ?? false;

    /// <summary>
    /// Whether the record is genuinely finished: the item dealt with,
    /// every root cause addressed, and every action shown to have worked.
    /// </summary>
    /// <remarks>
    /// Deliberately stricter than <see cref="OperationalState.Closed"/>.
    /// Somebody can close a record; whether the problem is actually
    /// solved is a different question, and this is the one worth asking.
    /// </remarks>
    public bool IsGenuinelyResolved =>
        IsDisposed
        && RootCauses.Count > 0
        && UnaddressedRootCauses.Count == 0
        && Actions.Count > 0
        && Actions.All(a => a.IsVerifiedEffective);

    /// <summary>Whether the record is marked closed while the problem is not actually solved.</summary>
    public bool IsClosedButUnresolved => Facts.State == OperationalState.Closed && !IsGenuinelyResolved;

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
