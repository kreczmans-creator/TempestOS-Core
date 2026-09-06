using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Operating;

/// <summary>
/// Something the operating model rests on that nobody has established.
/// </summary>
/// <param name="Code">The assumption's own identifier. Required.</param>
/// <param name="Statement">What is being assumed. Required.</param>
/// <param name="State">How firmly it is established.</param>
/// <param name="Source">Where it came from. <see langword="null"/> if nobody said.</param>
/// <param name="WouldInvalidate">What would no longer hold if it were wrong. <see langword="null"/> if not stated.</param>
public sealed record OperatingAssumption(
    string Code,
    string Statement,
    DeterminationState State = DeterminationState.Assumed,
    string? Source = null,
    string? WouldInvalidate = null)
{
    /// <summary>The assumption's own identifier.</summary>
    public string Code { get; } = string.IsNullOrWhiteSpace(Code)
        ? throw new ArgumentException("An operating assumption must carry its own code.", nameof(Code))
        : Code.Trim();

    /// <summary>What is being assumed.</summary>
    public string Statement { get; } = string.IsNullOrWhiteSpace(Statement)
        ? throw new ArgumentException("An operating assumption must say what is being assumed.", nameof(Statement))
        : Statement.Trim();
}

/// <summary>Something that limits the organisation from doing more.</summary>
/// <param name="Code">The constraint's own identifier. Required.</param>
/// <param name="Kind">What kind of limit it is.</param>
/// <param name="Description">What the limit is. Required.</param>
/// <param name="BindsAt">What level of demand it starts to bite at, in the model's own unit. <see langword="null"/> where nobody has worked that out.</param>
/// <param name="ReliefRoute">What would relieve it — a hire, a purchase, a subcontract, an accreditation. <see langword="null"/> where nothing obvious would.</param>
/// <param name="ReliefCost">What relieving it would cost. <see langword="null"/> where unknown.</param>
/// <param name="OwnerPrincipalId">Who is answerable for it. <see langword="null"/> where nobody is — itself worth reporting.</param>
public sealed record OperatingConstraint(
    string Code,
    ConstraintKind Kind,
    string Description,
    decimal? BindsAt = null,
    string? ReliefRoute = null,
    Money? ReliefCost = null,
    string? OwnerPrincipalId = null)
{
    /// <summary>The constraint's own identifier.</summary>
    public string Code { get; } = string.IsNullOrWhiteSpace(Code)
        ? throw new ArgumentException("A constraint must carry its own code.", nameof(Code))
        : Code.Trim();

    /// <summary>What the limit is.</summary>
    public string Description { get; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("A constraint must say what limits the organisation.", nameof(Description))
        : Description.Trim();

    /// <summary>Whether anybody has worked out what would relieve it.</summary>
    public bool HasReliefRoute => !string.IsNullOrWhiteSpace(ReliefRoute);
}

/// <summary>
/// One view of how the organisation would operate: what it has, what
/// limits it, what it is assuming, and what it has agreed to look at.
/// </summary>
/// <remarks>
/// <para>
/// Scenario-based, for the same reason C5 is. "Current state",
/// "conservative", "two-hire scale case" are different futures, and the
/// organisation plans by comparing them rather than by having one. Every
/// assumption stays identifiable as an assumption.
/// </para>
/// <para>
/// The model answers the questions C7 exists for: what capacity is there,
/// what limits growth, what capability is missing, and what conditions
/// would justify hiring, subcontracting or buying. It answers none of
/// them by deciding.
/// </para>
/// </remarks>
public sealed record OperatingScenario
{
    /// <summary>The reference the model is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What this view of the organisation is called. Required.</summary>
    public required string Name { get; init; }

    /// <summary>What it represents, and why anybody would look at it. Required.</summary>
    public required string Purpose { get; init; }

    /// <summary>The period the model describes. Required.</summary>
    public required FinancialPeriodLabel Period { get; init; }

    /// <summary>The governance every `P07` record carries. Required.</summary>
    public required BusinessGovernanceFacts Governance { get; init; }

    /// <summary>What the organisation can do. Never <see langword="null"/>.</summary>
    public IReadOnlyList<OperatingCapability> Capabilities { get; init; } = [];

    /// <summary>What it has to do it with. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ResourceCapacity> Resources { get; init; } = [];

    /// <summary>What limits it. Never <see langword="null"/>.</summary>
    public IReadOnlyList<OperatingConstraint> Constraints { get; init; } = [];

    /// <summary>What the model rests on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<OperatingAssumption> Assumptions { get; init; } = [];

    /// <summary>What the organisation has agreed to look at, and when. Never <see langword="null"/>.</summary>
    public IReadOnlyList<DecisionGate> Gates { get; init; } = [];

    /// <summary>The demand the model is sized against, in days of chargeable work. <see langword="null"/> where the model describes supply only.</summary>
    public decimal? DemandDaysPerPeriod { get; init; }

    /// <summary>Anything else about the model. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether this is the view the organisation is actually operating to.</summary>
    /// <remarks>
    /// Requires a named person's approval, for the same reason a planning
    /// case does: an operating model is what hiring and investment
    /// decisions get made against.
    /// </remarks>
    public bool IsCurrentModel => Governance.HasAuthority(BusinessAuthorityKind.InternalApproval);

    /// <summary>The days of chargeable work the model's committed resources actually provide.</summary>
    /// <remarks>Committed resources only: a plan sized on people nobody has hired is a plan, not a capacity.</remarks>
    public decimal CommittedProductiveDays => Resources.Where(r => r.IsCommitted).Sum(r => r.ProductiveDays);

    /// <summary>The days including resources that are planned but not yet secured.</summary>
    public decimal PlannedProductiveDays => Resources.Sum(r => r.ProductiveDays);

    /// <summary>
    /// How much of the committed capacity the modelled demand would take
    /// up.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> where there is no demand figure or no
    /// capacity: a utilisation against nothing is not a number, and
    /// reporting zero or infinity would be worse than reporting nothing.
    /// </remarks>
    public decimal? DemandAgainstCommittedCapacity =>
        DemandDaysPerPeriod is { } demand && CommittedProductiveDays > 0m
            ? demand / CommittedProductiveDays
            : null;

    /// <summary>Whether the modelled demand exceeds the capacity actually committed.</summary>
    public bool DemandExceedsCommittedCapacity =>
        DemandDaysPerPeriod is { } demand && demand > CommittedProductiveDays;

    /// <summary>Capabilities exactly one person holds.</summary>
    public IReadOnlyList<OperatingCapability> KeyPersonCapabilities =>
        Capabilities.Where(c => c.IsSinglePointOfFailure).ToList();

    /// <summary>Capabilities the organisation sells and does not have.</summary>
    public IReadOnlyList<OperatingCapability> MissingCapabilities =>
        Capabilities.Where(c => c.IsSoldButNotHeld).ToList();

    /// <summary>Assumptions nobody has established.</summary>
    public IReadOnlyList<OperatingAssumption> UnestablishedAssumptions =>
        Assumptions.Where(a => !DeterminationStates.IsEstablished(a.State)).ToList();

    /// <summary>The gates whose condition is met as at <paramref name="asAt"/>, and which are therefore prompting somebody.</summary>
    public IReadOnlyList<DecisionGate> TriggeredGates(DateOnly asAt) =>
        Gates.Where(g => g.StatusAt(asAt) == GateStatus.ConditionMet).ToList();

    /// <summary>The gates that cannot say anything as at <paramref name="asAt"/>, because nothing has been measured or the measurement is stale.</summary>
    public IReadOnlyList<DecisionGate> UninformativeGates(DateOnly asAt) =>
        Gates.Where(g => g.StatusAt(asAt) is GateStatus.NotMeasured or GateStatus.MeasurementStale).ToList();

    /// <summary>Returns the gate registered under <paramref name="code"/>, or <see langword="null"/> if the model has none.</summary>
    public DecisionGate? FindGate(string code) =>
        Gates.FirstOrDefault(g => string.Equals(g.Code, code, StringComparison.OrdinalIgnoreCase));

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
/// The period an operating model describes.
/// </summary>
/// <remarks>
/// A label and a period, deliberately mirroring
/// <see cref="Finance.FinancialPeriod"/> without depending on it: an
/// operating model and a financial scenario are compared by a person
/// reading both, and coupling C7 to C5 for a label would make neither
/// usable alone.
/// </remarks>
/// <param name="Label">What the period is called. Required.</param>
/// <param name="Period">The days it covers. Required.</param>
public sealed record FinancialPeriodLabel(string Label, EffectivePeriod Period)
{
    /// <summary>What the period is called.</summary>
    public string Label { get; } = string.IsNullOrWhiteSpace(Label)
        ? throw new ArgumentException("An operating period must be named.", nameof(Label))
        : Label.Trim();

    /// <summary>The days it covers.</summary>
    public EffectivePeriod Period { get; } = Period ?? throw new ArgumentNullException(nameof(Period));

    /// <inheritdoc />
    public override string ToString() => Label;
}
