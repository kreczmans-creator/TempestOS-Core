using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Development;

/// <summary>
/// Where an opportunity has got to.
/// </summary>
/// <remarks>
/// <para>
/// A closed list, not free text. Free-text stages make a pipeline
/// impossible to total, impossible to compare between quarters, and
/// trivially gameable — "nearly there" is not a stage.
/// </para>
/// <para>
/// The stages describe what has actually happened, not how anybody feels
/// about it. <see cref="Qualified"/> means the organisation has
/// established there is a real requirement, a budget and a decision-maker;
/// <see cref="Proposal"/> means a proposal has been sent, not that one is
/// being written.
/// </para>
/// </remarks>
public enum PipelineStage
{
    /// <summary>Somebody or something has been identified as a possibility. No contact has been made.</summary>
    Identified,

    /// <summary>Contact has been made and acknowledged.</summary>
    Contacted,

    /// <summary>A real requirement, a budget and a decision-maker have been established.</summary>
    Qualified,

    /// <summary>A specific piece of work has been scoped and discussed.</summary>
    Opportunity,

    /// <summary>A proposal has been sent.</summary>
    Proposal,

    /// <summary>Terms are being negotiated.</summary>
    Negotiation,

    /// <summary>Won: a contract exists, or is agreed and awaiting signature.</summary>
    Won,

    /// <summary>Lost to a competitor, to a decision not to proceed, or to silence.</summary>
    Lost,

    /// <summary>Real, but nothing is happening and nothing is expected to soon.</summary>
    Dormant
}

/// <summary>Reasoning over <see cref="PipelineStage"/>.</summary>
public static class PipelineStages
{
    /// <summary>Every stage, in the order a pipeline report should present them.</summary>
    public static IReadOnlyList<PipelineStage> All { get; } =
    [
        PipelineStage.Identified, PipelineStage.Contacted, PipelineStage.Qualified, PipelineStage.Opportunity,
        PipelineStage.Proposal, PipelineStage.Negotiation, PipelineStage.Won, PipelineStage.Lost, PipelineStage.Dormant,
    ];

    /// <summary>The stages an opportunity is still live at.</summary>
    public static IReadOnlyList<PipelineStage> Open { get; } =
    [
        PipelineStage.Identified, PipelineStage.Contacted, PipelineStage.Qualified, PipelineStage.Opportunity,
        PipelineStage.Proposal, PipelineStage.Negotiation,
    ];

    /// <summary>Whether the opportunity is still live.</summary>
    public static bool IsOpen(PipelineStage stage) => Open.Contains(stage);

    /// <summary>Whether the opportunity has finished, one way or the other.</summary>
    public static bool IsClosed(PipelineStage stage) => stage is PipelineStage.Won or PipelineStage.Lost;

    /// <summary>How far through the pipeline a stage is, for ordering. Closed stages sort last.</summary>
    public static int Order(PipelineStage stage) => stage switch
    {
        PipelineStage.Identified => 0,
        PipelineStage.Contacted => 1,
        PipelineStage.Qualified => 2,
        PipelineStage.Opportunity => 3,
        PipelineStage.Proposal => 4,
        PipelineStage.Negotiation => 5,
        PipelineStage.Dormant => 6,
        PipelineStage.Won => 7,
        PipelineStage.Lost => 8,
        _ => 0,
    };
}

/// <summary>
/// How real a sum of money is.
/// </summary>
/// <remarks>
/// <b>The distinction C6 exists to enforce.</b> Potential revenue is
/// somebody's estimate of work that has not been won. Contracted revenue
/// is backed by a signed contract. Realised revenue has been invoiced and
/// paid. A pipeline that totals all three into one figure is how a
/// business talks itself into hiring against money that does not exist.
/// </remarks>
public enum RevenueReality
{
    /// <summary>Estimated against an opportunity that has not been won. Not revenue.</summary>
    Potential,

    /// <summary>Backed by a signed contract, and not yet delivered or invoiced.</summary>
    Contracted,

    /// <summary>Delivered and invoiced, and not yet paid.</summary>
    Invoiced,

    /// <summary>Paid.</summary>
    Realised
}

/// <summary>
/// Something that happened with a prospect or client.
/// </summary>
/// <remarks>
/// Recorded because a pipeline without a history is a list of guesses. The
/// operational surface for logging interactions belongs to `P04`; what is
/// kept here is the governed trail behind a stage change.
/// </remarks>
/// <param name="Date">When it happened.</param>
/// <param name="Summary">What happened. Required.</param>
/// <param name="RecordedByPrincipalId">Who recorded it. Required.</param>
/// <param name="ResultingStage">The stage the opportunity moved to, where it moved. <see langword="null"/> where nothing changed.</param>
public sealed record OpportunityInteraction(
    DateOnly Date,
    string Summary,
    string RecordedByPrincipalId,
    PipelineStage? ResultingStage = null)
{
    /// <summary>What happened.</summary>
    public string Summary { get; } = string.IsNullOrWhiteSpace(Summary)
        ? throw new ArgumentException("An interaction must say what happened.", nameof(Summary))
        : Summary.Trim();

    /// <summary>Who recorded it.</summary>
    public string RecordedByPrincipalId { get; } = string.IsNullOrWhiteSpace(RecordedByPrincipalId)
        ? throw new ArgumentException("An interaction must say who recorded it.", nameof(RecordedByPrincipalId))
        : RecordedByPrincipalId.Trim();
}

/// <summary>
/// A piece of work the organisation might win.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the governed structure behind business development, not a
/// CRM.</b> `P04` — Business OS — will own the operational surface:
/// activity feeds, reminders, email integration, contact management,
/// dashboards. What `P07` establishes is the record such a surface must
/// not be free to contradict — explicit stages, a value that knows how
/// real it is, an owner, and a history behind every stage change.
/// </para>
/// <para>
/// The organisation and contact are held as names and an external
/// identifier rather than as entities, for the same reason
/// <see cref="Contracts.ContractParty"/> is: building a second
/// organisation model here would guarantee it diverged from `P04`'s.
/// </para>
/// </remarks>
public sealed record Opportunity
{
    /// <summary>The reference the opportunity is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What the work is. Required.</summary>
    public required string Title { get; init; }

    /// <summary>Who it is with, as the organisation refers to them. Required.</summary>
    public required string OrganisationName { get; init; }

    /// <summary>The governance every `P07` record carries. Required.</summary>
    public required BusinessGovernanceFacts Governance { get; init; }

    /// <summary>Where it has got to.</summary>
    public PipelineStage Stage { get; init; } = PipelineStage.Identified;

    /// <summary>The identifier the organisation carries in whatever system owns organisations. <see langword="null"/> where none does yet.</summary>
    public string? ExternalOrganisationId { get; init; }

    /// <summary>Who the organisation deals with there. <see langword="null"/> where nobody is named.</summary>
    public string? ContactName { get; init; }

    /// <summary>How the opportunity arose — a referral, an approach, an existing client, a tender portal. <see langword="null"/> if not recorded.</summary>
    public string? LeadSource { get; init; }

    /// <summary>What the work is estimated to be worth. <see langword="null"/> where nobody has estimated it.</summary>
    public Money? EstimatedValue { get; init; }

    /// <summary>How real that figure is.</summary>
    /// <remarks>
    /// Defaults to <see cref="RevenueReality.Potential"/> and stays there
    /// until a contract exists. Nothing in `P07` moves it on its own.
    /// </remarks>
    public RevenueReality ValueReality { get; init; } = RevenueReality.Potential;

    /// <summary>
    /// The organisation's own estimate of the chance of winning, as a
    /// proportion.
    /// </summary>
    /// <remarks>
    /// Recorded as a judgement, and never used to produce a single
    /// "weighted pipeline" number that gets treated as revenue. See
    /// <see cref="IPipelineService"/> for what is and is not reported.
    /// </remarks>
    public decimal? WinProbability { get; init; }

    /// <summary>When the work is expected to be decided. <see langword="null"/> where nobody knows.</summary>
    public DateOnly? ExpectedDecisionDate { get; init; }

    /// <summary>When the work is expected to start. <see langword="null"/> where nobody knows.</summary>
    public DateOnly? ExpectedStartDate { get; init; }

    /// <summary>What service the work would be. <see langword="null"/> where it has not been scoped that far.</summary>
    public string? ServiceCode { get; init; }

    /// <summary>The contract, once one exists. <see langword="null"/> until then.</summary>
    /// <remarks>
    /// The seam between C6 and C1, and the only thing that makes revenue
    /// contracted rather than potential.
    /// </remarks>
    public string? ContractReference { get; init; }

    /// <summary>The next thing somebody must do. <see langword="null"/> where nothing is planned — itself the most useful thing to report.</summary>
    public string? NextAction { get; init; }

    /// <summary>When the next action is due. <see langword="null"/> where none is planned.</summary>
    public DateOnly? NextActionDue { get; init; }

    /// <summary>What has happened, most recent last. Never <see langword="null"/>.</summary>
    public IReadOnlyList<OpportunityInteraction> Interactions { get; init; } = [];

    /// <summary>Why it was won or lost. <see langword="null"/> while it is still open.</summary>
    public string? Outcome { get; init; }

    /// <summary>Anything else about the opportunity. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether the opportunity is still live.</summary>
    public bool IsOpen => PipelineStages.IsOpen(Stage);

    /// <summary>The chance of winning, as a proportion.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="WinProbability"/> is outside 0–1.</exception>
    public decimal? WinProbabilityChecked => WinProbability is { } p && (p < 0m || p > 1m)
        ? throw new ArgumentOutOfRangeException(nameof(WinProbability), p, "A win probability must be between 0 and 1.")
        : WinProbability;

    /// <summary>When anything last happened. <see langword="null"/> where nothing ever has.</summary>
    public DateOnly? LastInteractionDate =>
        Interactions.Count == 0 ? null : Interactions.Max(i => i.Date);

    /// <summary>Whether nothing has happened for <paramref name="days"/> and the opportunity is still open.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="days"/> is negative.</exception>
    public bool IsStaleAt(DateOnly asAt, int days)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(days);

        if (!IsOpen)
            return false;

        return LastInteractionDate is not { } last || last.AddDays(days) < asAt;
    }

    /// <summary>Whether the next action is past its own date.</summary>
    public bool NextActionIsOverdueAt(DateOnly asAt) => NextActionDue is { } due && due < asAt;

    /// <summary>
    /// Whether the opportunity claims revenue that is more real than its
    /// stage supports.
    /// </summary>
    /// <remarks>
    /// The check that keeps a pipeline honest. Revenue is contracted only
    /// when the opportunity is Won and names a contract; anything else
    /// claiming to be contracted, invoiced or realised is a figure
    /// somebody promoted without the paperwork.
    /// </remarks>
    public bool OverstatesRevenue =>
        ValueReality != RevenueReality.Potential
        && (Stage != PipelineStage.Won || string.IsNullOrWhiteSpace(ContractReference));

    /// <summary>Every reference-data revision the opportunity rests on. Never <see langword="null"/>.</summary>
    public IReadOnlyList<ReferencePin> AllPins =>
        Governance.Evidence.Select(e => e.Pin)
            .OfType<ReferencePin>()
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
