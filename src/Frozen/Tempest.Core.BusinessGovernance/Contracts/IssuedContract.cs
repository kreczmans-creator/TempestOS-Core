using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Contracts;

/// <summary>Where a contract stands commercially.</summary>
/// <remarks>
/// <para>
/// Deliberately separate from
/// <see cref="ReferenceValidationState"/>, which answers a different
/// question. The reference-data lifecycle asks "may work rely on this
/// record?"; this asks "what is the commercial position?". A contract
/// record can be Released as a record — accurate, checked, complete —
/// while the contract itself is still in negotiation.
/// </para>
/// <para>
/// Conflating the two is how a system comes to report a draft as a signed
/// contract, so `P07` keeps governance state and domain status apart in
/// every work package.
/// </para>
/// </remarks>
public enum ContractStatus
{
    /// <summary>Being written. Not sent to the other party.</summary>
    Draft,

    /// <summary>Sent, and under discussion.</summary>
    InNegotiation,

    /// <summary>Agreed in substance, awaiting signature.</summary>
    AwaitingSignature,

    /// <summary>Signed by all parties and in force.</summary>
    Executed,

    /// <summary>Signed, and its term has run out.</summary>
    Expired,

    /// <summary>Signed, and ended early by one party or by agreement.</summary>
    Terminated,

    /// <summary>Abandoned before signature.</summary>
    Lapsed,

    /// <summary>Replaced by a later contract, which the record names.</summary>
    Superseded
}

/// <summary>Reasoning over <see cref="ContractStatus"/>.</summary>
public static class ContractStatuses
{
    /// <summary>Every status, in the order a report should present them.</summary>
    public static IReadOnlyList<ContractStatus> All { get; } =
    [
        ContractStatus.Draft, ContractStatus.InNegotiation, ContractStatus.AwaitingSignature, ContractStatus.Executed,
        ContractStatus.Expired, ContractStatus.Terminated, ContractStatus.Lapsed, ContractStatus.Superseded,
    ];

    /// <summary>Whether the contract has been signed by all parties at some point.</summary>
    /// <remarks>
    /// True for a contract that has since expired or terminated: it was
    /// still executed, and obligations that survive termination still bind.
    /// </remarks>
    public static bool HasBeenExecuted(ContractStatus status) =>
        status is ContractStatus.Executed or ContractStatus.Expired or ContractStatus.Terminated or ContractStatus.Superseded;

    /// <summary>Whether the contract binds the organisation today.</summary>
    public static bool IsBinding(ContractStatus status) => status == ContractStatus.Executed;

    /// <summary>
    /// Whether any revenue under this contract may be treated as
    /// contracted rather than merely expected.
    /// </summary>
    /// <remarks>
    /// The one place C1 and C6 meet. An opportunity is not revenue; a
    /// contract in negotiation is not revenue; a signed contract is
    /// contracted revenue. Nothing else counts.
    /// </remarks>
    public static bool SupportsContractedRevenue(ContractStatus status) => IsBinding(status);
}

/// <summary>
/// A party to a contract.
/// </summary>
/// <remarks>
/// Deliberately minimal. `P04` — Business OS — owns organisations and
/// contacts as operational entities with addresses, relationships and
/// histories; `P07` needs only enough to say who a contract binds, and
/// building a second organisation model here would guarantee the two
/// diverged. <see cref="ExternalOrganisationId"/> is the seam: when `P04`
/// exists, it carries that identifier and this stays as it is.
/// </remarks>
/// <param name="LegalName">The party's name as the contract states it. Required.</param>
/// <param name="Role">The capacity in which they contract — Client, Supplier, Subcontractor. Required.</param>
/// <param name="RegisteredNumber">A company number or equivalent registration. <see langword="null"/> if not recorded.</param>
/// <param name="SignatoryName">Who signed for them. <see langword="null"/> where nobody has.</param>
/// <param name="ExternalOrganisationId">The identifier this party carries in whatever system owns organisations. <see langword="null"/> where none does yet.</param>
public sealed record ContractParty(
    string LegalName,
    string Role,
    string? RegisteredNumber = null,
    string? SignatoryName = null,
    string? ExternalOrganisationId = null)
{
    /// <summary>The party's name as the contract states it.</summary>
    public string LegalName { get; } = string.IsNullOrWhiteSpace(LegalName)
        ? throw new ArgumentException("A contract party must be named as the contract names them.", nameof(LegalName))
        : LegalName.Trim();

    /// <summary>The capacity in which they contract.</summary>
    public string Role { get; } = string.IsNullOrWhiteSpace(Role)
        ? throw new ArgumentException("A contract party must state the capacity they contract in.", nameof(Role))
        : Role.Trim();
}

/// <summary>
/// A contract the organisation has issued or entered into.
/// </summary>
/// <remarks>
/// <para>
/// <b>The template pin is the point of this type.</b> A contract records
/// the exact template revision it was drawn from, so that revising the
/// template afterwards changes nothing about a contract already issued.
/// That guarantee is what C1 exists to give, and it is mechanical: the
/// shared lifecycle refuses to edit a released template in place, and the
/// pin names the revision that was read.
/// </para>
/// <para>
/// <b>Registering a contract is not executing one.</b> The record's
/// <see cref="Status"/> says where the contract stands, and reaching
/// <see cref="ContractStatus.Executed"/> requires a
/// <see cref="BusinessAuthorityKind.CommercialCommitment"/> by a named
/// person — which no part of `P07` grants.
/// </para>
/// </remarks>
public sealed record IssuedContract
{
    /// <summary>The reference the contract is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What the contract is for. Required.</summary>
    public required string Title { get; init; }

    /// <summary>Who it binds. Required, and a contract with fewer than two parties is reported.</summary>
    public required IReadOnlyList<ContractParty> Parties { get; init; }

    /// <summary>The governance every `P07` record carries. Required.</summary>
    public required BusinessGovernanceFacts Governance { get; init; }

    /// <summary>Where the contract stands commercially.</summary>
    public ContractStatus Status { get; init; } = ContractStatus.Draft;

    /// <summary>
    /// The exact template revision this contract was drawn from.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> for a contract drafted from scratch or
    /// supplied by the other party — a real and common case, and one worth
    /// reporting, because a bespoke contract has had none of the review
    /// the template library carries.
    /// </remarks>
    public ReferencePin? TemplatePin { get; init; }

    /// <summary>Where the template was departed from, and why. Never <see langword="null"/>.</summary>
    /// <remarks>
    /// A contract on a reviewed template that quietly changed the
    /// liability clause has the template's provenance and none of its
    /// protection. Departures are recorded so that is visible.
    /// </remarks>
    public IReadOnlyList<TemplateDeparture> Departures { get; init; } = [];

    /// <summary>The commercial substance. <see langword="null"/> where it has not been captured yet.</summary>
    public CommercialTerms? CommercialTerms { get; init; }

    /// <summary>When the contract is in force. <see langword="null"/> before execution.</summary>
    public EffectivePeriod? Term { get; init; }

    /// <summary>When it was signed by the last party to sign. <see langword="null"/> where it has not been.</summary>
    public DateOnly? ExecutedOn { get; init; }

    /// <summary>What is to be handed over. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ContractDeliverable> Deliverables { get; init; } = [];

    /// <summary>What each party has undertaken to do. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ContractObligation> Obligations { get; init; } = [];

    /// <summary>The executed document itself. <see langword="null"/> where the signed copy is not held here.</summary>
    public Guid? ExecutedDocumentId { get; init; }

    /// <summary>The contract this one replaces, where it replaces one. <see langword="null"/> otherwise.</summary>
    public string? SupersedesContractReference { get; init; }

    /// <summary>Anything else about the contract. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether the contract binds the organisation today.</summary>
    public bool IsBinding => ContractStatuses.IsBinding(Status);

    /// <summary>Whether it was drawn from a controlled template at a known revision.</summary>
    public bool IsFromTemplate => TemplatePin is not null;

    /// <summary>Whether the template was departed from.</summary>
    public bool DepartsFromTemplate => Departures.Count > 0;

    /// <summary>Whether the contract has run past its own term as at <paramref name="asAt"/>.</summary>
    /// <remarks>
    /// Reported independently of <see cref="Status"/>, so that a contract
    /// still recorded as Executed whose term ended last quarter shows up
    /// as exactly that: a record nobody updated.
    /// </remarks>
    public bool TermHasEndedBy(DateOnly asAt) => Term?.HasExpiredBy(asAt) ?? false;

    /// <summary>Obligations past their own due date as at <paramref name="asAt"/>.</summary>
    public IReadOnlyList<ContractObligation> OverdueObligations(DateOnly asAt) =>
        Obligations.Where(o => o.IsOverdueAt(asAt)).ToList();

    /// <summary>Obligations that continue after the contract ends.</summary>
    public IReadOnlyList<ContractObligation> SurvivingObligations =>
        Obligations.Where(o => o.SurvivesTermination).ToList();

    /// <summary>Deliverables nobody has accepted.</summary>
    public IReadOnlyList<ContractDeliverable> UnacceptedDeliverables =>
        Deliverables.Where(d => !d.IsAccepted).ToList();

    /// <summary>Every reference-data revision this contract rests on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferencePin> AllPins =>
        new[] { TemplatePin, CommercialTerms?.RateCardPin }
            .OfType<ReferencePin>()
            .Concat(Governance.Evidence.Select(e => e.Pin).OfType<ReferencePin>())
            .Distinct()
            .OrderBy(p => p.Library, StringComparer.Ordinal)
            .ThenBy(p => p.RecordId, StringComparer.Ordinal)
            .ThenBy(p => p.RevisionNumber)
            .ToList();

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
/// A place where an issued contract departs from the template it was drawn
/// from.
/// </summary>
/// <remarks>
/// Recorded per clause, with the reason, and with whether a solicitor
/// looked at it. A departure from a clause the template marks
/// <see cref="ContractClause.RequiresLegalReview"/> and that has no legal
/// review is precisely the finding C1 exists to surface.
/// </remarks>
/// <param name="ClauseReference">The template clause departed from. Required.</param>
/// <param name="Description">What was changed. Required.</param>
/// <param name="Reason">Why. Required.</param>
/// <param name="LegalReviewState">Whether a solicitor considered the departure.</param>
/// <param name="AgreedByPrincipalId">Who agreed to it internally. <see langword="null"/> where nobody is recorded.</param>
public sealed record TemplateDeparture(
    string ClauseReference,
    string Description,
    string Reason,
    DeterminationState LegalReviewState = DeterminationState.NotDetermined,
    string? AgreedByPrincipalId = null)
{
    /// <summary>The template clause departed from.</summary>
    public string ClauseReference { get; } = string.IsNullOrWhiteSpace(ClauseReference)
        ? throw new ArgumentException("A departure must name the clause it departs from.", nameof(ClauseReference))
        : ClauseReference.Trim();

    /// <summary>What was changed.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A departure must say what was changed.", nameof(Description))
        : Description.Trim();

    /// <summary>Why it was changed.</summary>
    public string Reason { get; } = string.IsNullOrWhiteSpace(Reason)
        ? throw new ArgumentException(
            "A departure from a controlled template must say why. A change nobody explained is a change nobody can review.",
            nameof(Reason))
        : Reason.Trim();
}
