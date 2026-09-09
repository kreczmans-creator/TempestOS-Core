namespace Tempest.Core.BusinessGovernance.Contracts;

/// <summary>How work under a contract is charged.</summary>
public enum ChargingBasis
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>Charged by time actually spent, at an agreed rate.</summary>
    TimeAndMaterials,

    /// <summary>A single price for a defined scope, whatever it takes.</summary>
    FixedPrice,

    /// <summary>A recurring fee for availability or a standing allocation.</summary>
    Retainer,

    /// <summary>A capped time-and-materials arrangement — charged by time, but not beyond a stated ceiling.</summary>
    CappedTimeAndMaterials,

    /// <summary>Paid against defined milestones rather than elapsed time.</summary>
    Milestone,

    /// <summary>Charged per unit delivered.</summary>
    PerUnit,

    /// <summary>No charge — a pilot, a goodwill piece, an internal engagement.</summary>
    NoCharge
}

/// <summary>When payment falls due.</summary>
public enum PaymentTrigger
{
    /// <summary>Not stated.</summary>
    Unspecified,

    /// <summary>On invoice, at the stated number of days.</summary>
    OnInvoice,

    /// <summary>On acceptance of a deliverable.</summary>
    OnAcceptance,

    /// <summary>On reaching a milestone.</summary>
    OnMilestone,

    /// <summary>At fixed intervals, regardless of progress.</summary>
    Periodic,

    /// <summary>Before work starts.</summary>
    InAdvance,

    /// <summary>On completion of the whole engagement.</summary>
    OnCompletion
}

/// <summary>
/// One payment term: what triggers a payment, how much, and how long the
/// other party then has.
/// </summary>
/// <param name="Trigger">What makes the payment fall due.</param>
/// <param name="Description">What the term says, in the contract's own words. Required.</param>
/// <param name="Amount">The amount, where the term states one. <see langword="null"/> where it is a percentage, a rate or unstated.</param>
/// <param name="PercentageOfTotal">The share of the contract value, where the term states one. <see langword="null"/> otherwise.</param>
/// <param name="DaysToPay">How many days the payer has once the payment falls due. <see langword="null"/> where the term does not say — itself worth reporting.</param>
/// <param name="MilestoneReference">The milestone this is tied to, where the trigger is one. <see langword="null"/> otherwise.</param>
public sealed record PaymentTerm(
    PaymentTrigger Trigger,
    string Description,
    Money? Amount = null,
    decimal? PercentageOfTotal = null,
    int? DaysToPay = null,
    string? MilestoneReference = null)
{
    /// <summary>What the term says, in the contract's own words.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A payment term must say what it requires.", nameof(Description))
        : Description.Trim();

    /// <summary>The share of the contract value this term covers.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="PercentageOfTotal"/> is outside 0–100.</exception>
    public decimal? PercentageOfTotal { get; } = PercentageOfTotal is { } percentage && (percentage < 0m || percentage > 100m)
        ? throw new ArgumentOutOfRangeException(nameof(PercentageOfTotal), percentage, "A payment term's share of the total must be between 0 and 100 per cent.")
        : PercentageOfTotal;

    /// <summary>How many days the payer has.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="DaysToPay"/> is negative.</exception>
    public int? DaysToPay { get; } = DaysToPay is { } days && days < 0
        ? throw new ArgumentOutOfRangeException(nameof(DaysToPay), days, "A payment period cannot be negative.")
        : DaysToPay;

    /// <summary>Whether the term states an amount or a share, rather than leaving the sum to be worked out elsewhere.</summary>
    public bool StatesAValue => Amount is not null || PercentageOfTotal is not null;
}

/// <summary>
/// One thing a party has undertaken to do.
/// </summary>
/// <remarks>
/// Obligations are separated from clauses because they behave differently:
/// a clause is wording, an obligation is something somebody must actually
/// do by a date, and it is obligations — not clauses — that a business
/// needs to be reminded about. <see cref="DueBy"/> is what makes
/// "what do we owe, and when?" answerable.
/// </remarks>
/// <param name="Reference">The obligation's own identifier. Required.</param>
/// <param name="Description">What must be done. Required.</param>
/// <param name="OwedBy">Which party owes it. Required.</param>
/// <param name="OwedTo">Which party is owed it. Required.</param>
/// <param name="DueBy">When it must be done by. <see langword="null"/> where the obligation is continuing rather than dated.</param>
/// <param name="ClauseReference">The clause it comes from. <see langword="null"/> if it is not traced to one.</param>
/// <param name="IsContinuing">Whether the obligation runs for the life of the contract rather than being discharged once.</param>
/// <param name="SurvivesTermination">Whether it continues after the contract ends — confidentiality and IP obligations usually do.</param>
public sealed record ContractObligation(
    string Reference,
    string Description,
    string OwedBy,
    string OwedTo,
    DateOnly? DueBy = null,
    string? ClauseReference = null,
    bool IsContinuing = false,
    bool SurvivesTermination = false)
{
    /// <summary>The obligation's own identifier.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("An obligation must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What must be done.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("An obligation must say what must be done.", nameof(Description))
        : Description.Trim();

    /// <summary>Which party owes it.</summary>
    public string OwedBy { get; } = string.IsNullOrWhiteSpace(OwedBy)
        ? throw new ArgumentException("An obligation must say who owes it.", nameof(OwedBy))
        : OwedBy.Trim();

    /// <summary>Which party is owed it.</summary>
    public string OwedTo { get; } = string.IsNullOrWhiteSpace(OwedTo)
        ? throw new ArgumentException("An obligation must say who is owed it.", nameof(OwedTo))
        : OwedTo.Trim();

    /// <summary>Whether the obligation is past its own date as at <paramref name="asAt"/>.</summary>
    public bool IsOverdueAt(DateOnly asAt) => DueBy is { } due && due < asAt;
}

/// <summary>
/// What is to be handed over, and what makes it acceptable.
/// </summary>
/// <remarks>
/// Acceptance criteria are held with the deliverable rather than as free
/// text in a scope clause, because "was this accepted, and against what?"
/// is a question that arises long after the contract was read. An empty
/// criteria list is a contract that can be argued about, and validation
/// says so.
/// </remarks>
/// <param name="Reference">The deliverable's own identifier. Required.</param>
/// <param name="Description">What is to be handed over. Required.</param>
/// <param name="AcceptanceCriteria">What makes it acceptable. Never <see langword="null"/>; empty is a real and reportable state.</param>
/// <param name="DueBy">When it is due. <see langword="null"/> where the contract does not date it.</param>
/// <param name="AcceptanceState">Whether it has been accepted.</param>
/// <param name="AcceptedOn">When it was accepted. <see langword="null"/> where it has not been.</param>
/// <param name="AcceptedByPrincipalId">Who accepted it. <see langword="null"/> where nobody has.</param>
public sealed record ContractDeliverable(
    string Reference,
    string Description,
    IReadOnlyList<string>? AcceptanceCriteria = null,
    DateOnly? DueBy = null,
    DeterminationState AcceptanceState = DeterminationState.NotDetermined,
    DateOnly? AcceptedOn = null,
    string? AcceptedByPrincipalId = null)
{
    /// <summary>The deliverable's own identifier.</summary>
    public string Reference { get; } = string.IsNullOrWhiteSpace(Reference)
        ? throw new ArgumentException("A deliverable must carry its own reference.", nameof(Reference))
        : Reference.Trim();

    /// <summary>What is to be handed over.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A deliverable must say what is to be handed over.", nameof(Description))
        : Description.Trim();

    /// <summary>What makes it acceptable.</summary>
    public IReadOnlyList<string> AcceptanceCriteria { get; init; } = AcceptanceCriteria ?? [];

    /// <summary>Whether anybody stated what would make this acceptable.</summary>
    public bool HasAcceptanceCriteria => AcceptanceCriteria.Count > 0;

    /// <summary>
    /// Whether the deliverable has been accepted by a named person on a
    /// stated date.
    /// </summary>
    /// <remarks>
    /// All three conditions, not just the state flag. An acceptance with
    /// nobody's name against it is not an acceptance anybody can rely on.
    /// </remarks>
    public bool IsAccepted =>
        AcceptanceState == DeterminationState.Recorded
        && AcceptedOn is not null
        && !string.IsNullOrWhiteSpace(AcceptedByPrincipalId);
}

/// <summary>
/// The commercial substance of a contract or template: what is charged,
/// how it is paid, and what may change afterwards.
/// </summary>
/// <remarks>
/// Held apart from the clause list because these are the terms a business
/// reports on — value, charging basis, payment timing, liability cap —
/// while the clause list is what the contract says. The two describe the
/// same document from different ends, and conflating them makes the
/// commercial questions unanswerable without reading the wording.
/// </remarks>
public sealed record CommercialTerms
{
    /// <summary>How work is charged.</summary>
    public ChargingBasis Basis { get; init; } = ChargingBasis.Unspecified;

    /// <summary>The contract value, where one is stated. <see langword="null"/> for an uncapped time-and-materials engagement.</summary>
    public Money? ContractValue { get; init; }

    /// <summary>The ceiling beyond which work stops without further authority. <see langword="null"/> where there is none.</summary>
    public Money? Ceiling { get; init; }

    /// <summary>The rate-card revision the rates come from, where they come from one.</summary>
    /// <remarks>
    /// A pin, not a copy. This is what lets a contract signed last year be
    /// read against the rates that actually applied when it was signed,
    /// after the rate card has moved on twice.
    /// </remarks>
    public Tempest.Core.ReferenceData.ReferencePin? RateCardPin { get; init; }

    /// <summary>When and how payment falls due. Never <see langword="null"/>.</summary>
    public IReadOnlyList<PaymentTerm> PaymentTerms { get; init; } = [];

    /// <summary>Whether expenses are recoverable, and on what basis. <see langword="null"/> where the contract does not say.</summary>
    public string? ExpensesTreatment { get; init; }

    /// <summary>The limit of liability the contract states. <see langword="null"/> where it states none — a materially different position.</summary>
    public Money? LiabilityCap { get; init; }

    /// <summary>What the contract says about liability that is not capped. <see langword="null"/> if it does not say.</summary>
    public string? UncappedLiabilities { get; init; }

    /// <summary>How the scope or price may be changed after signature. <see langword="null"/> where the contract has no change mechanism.</summary>
    public string? ChangeControlMechanism { get; init; }

    /// <summary>Insurance the contract requires a party to carry. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> InsuranceRequirements { get; init; } = [];

    /// <summary>Whether the contract states a limit of liability at all.</summary>
    public bool HasLiabilityCap => LiabilityCap is not null;

    /// <summary>Whether the contract says how it may be changed.</summary>
    public bool HasChangeControl => !string.IsNullOrWhiteSpace(ChangeControlMechanism);

    /// <summary>
    /// Whether the payment terms account for the whole contract value.
    /// </summary>
    /// <remarks>
    /// Only meaningful where every term states a percentage; returns
    /// <see langword="null"/> otherwise rather than guessing, because a
    /// mixture of fixed sums and percentages cannot be totalled without
    /// knowing the contract value, and inventing that is not this type's
    /// business.
    /// </remarks>
    public decimal? PaymentPercentageTotal =>
        PaymentTerms.Count > 0 && PaymentTerms.All(t => t.PercentageOfTotal is not null)
            ? PaymentTerms.Sum(t => t.PercentageOfTotal!.Value)
            : null;
}
