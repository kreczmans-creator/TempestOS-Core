using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Development;

/// <summary>The pipeline at one stage: how many, and worth how much.</summary>
/// <param name="Stage">The stage.</param>
/// <param name="Count">How many opportunities sit at it.</param>
/// <param name="EstimatedValue">The total estimated value of those that carry one.</param>
/// <param name="WithoutEstimate">How many carry no estimate, and so are absent from the total.</param>
public sealed record PipelineStageSummary(PipelineStage Stage, int Count, Money EstimatedValue, int WithoutEstimate);

/// <summary>
/// The organisation's pipeline position.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no single pipeline value on this report, and that is
/// deliberate.</b> Adding potential to contracted revenue produces a
/// number that is neither, and multiplying estimates by win probabilities
/// produces a number that describes no possible future — the "weighted
/// pipeline" that has persuaded many a business to hire against money it
/// never won.
/// </para>
/// <para>
/// What is reported instead is each figure with its reality stated:
/// contracted revenue, which is backed by signed contracts; potential
/// revenue by stage, which is not; and the opportunities whose records do
/// not support the figures they carry.
/// </para>
/// </remarks>
/// <param name="AsAt">The date the position was taken at.</param>
/// <param name="Currency">The currency every figure is stated in.</param>
/// <param name="ByStage">The open pipeline, stage by stage.</param>
/// <param name="ContractedValue">The value of opportunities that are Won and name a contract. The only figure here backed by an obligation.</param>
/// <param name="PotentialValue">The value of everything still open. Not revenue.</param>
/// <param name="OverstatingRevenue">Opportunities claiming revenue more real than their stage supports.</param>
/// <param name="Stale">Open opportunities nothing has happened on.</param>
/// <param name="OverdueActions">Open opportunities whose next action is past its date.</param>
/// <param name="WonInPeriod">Opportunities won within the window asked about.</param>
/// <param name="LostInPeriod">Opportunities lost within the window asked about.</param>
public sealed record PipelinePosition(
    DateOnly AsAt,
    CurrencyCode Currency,
    IReadOnlyList<PipelineStageSummary> ByStage,
    Money ContractedValue,
    Money PotentialValue,
    IReadOnlyList<string> OverstatingRevenue,
    IReadOnlyList<string> Stale,
    IReadOnlyList<string> OverdueActions,
    IReadOnlyList<string> WonInPeriod,
    IReadOnlyList<string> LostInPeriod)
{
    /// <summary>How many open opportunities there are.</summary>
    public int OpenCount => ByStage.Where(s => PipelineStages.IsOpen(s.Stage)).Sum(s => s.Count);

    /// <summary>
    /// The proportion of decided opportunities that were won, over the
    /// window asked about.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> where nothing was decided: a win rate over
    /// no decisions is not a number.
    /// </remarks>
    public decimal? WinRate =>
        WonInPeriod.Count + LostInPeriod.Count == 0
            ? null
            : (decimal)WonInPeriod.Count / (WonInPeriod.Count + LostInPeriod.Count);

    /// <summary>Whether anything needs somebody's attention.</summary>
    public bool HasFindings => OverstatingRevenue.Count > 0 || Stale.Count > 0 || OverdueActions.Count > 0;
}

/// <summary>
/// Reports the pipeline position.
/// </summary>
/// <remarks>
/// <b>Not a CRM, and not a forecast.</b> The service totals what the
/// records say, keeping potential and contracted revenue apart, and never
/// produces a weighted figure. `P04` will own the operational surface;
/// `P05` and the finance package own what happens to a figure once it is
/// contracted.
/// </remarks>
public interface IPipelineService
{
    /// <summary>Reports the pipeline as at <paramref name="asAt"/>.</summary>
    /// <param name="asAt">The date to take the position at.</param>
    /// <param name="currency">The currency to total in. Opportunities in another currency are counted but excluded from totals.</param>
    /// <param name="decidedSince">The start of the window for the won/lost counts. <see langword="null"/> to count everything ever decided.</param>
    /// <param name="cancellationToken">A token to observe while awaiting.</param>
    Task<PipelinePosition> ReportAsync(
        DateOnly asAt,
        CurrencyCode currency,
        DateOnly? decidedSince = null,
        CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IPipelineService"/> implementation.</summary>
public sealed class PipelineService : IPipelineService
{
    private readonly IOpportunityCatalog _opportunities;

    /// <summary>Initialises a new instance of the <see cref="PipelineService"/> class.</summary>
    /// <param name="opportunities">The pipeline.</param>
    /// <exception cref="ArgumentNullException"><paramref name="opportunities"/> is <see langword="null"/>.</exception>
    public PipelineService(IOpportunityCatalog opportunities)
    {
        ArgumentNullException.ThrowIfNull(opportunities);

        _opportunities = opportunities;
    }

    /// <inheritdoc />
    public async Task<PipelinePosition> ReportAsync(
        DateOnly asAt,
        CurrencyCode currency,
        DateOnly? decidedSince = null,
        CancellationToken cancellationToken = default)
    {
        var records = await _opportunities.ListAsync(cancellationToken).ConfigureAwait(false);

        var byStage = new Dictionary<PipelineStage, (int Count, decimal Value, int WithoutEstimate)>();
        var overstating = new List<string>();
        var stale = new List<string>();
        var overdue = new List<string>();
        var won = new List<string>();
        var lost = new List<string>();
        var contracted = 0m;
        var potential = 0m;

        foreach (var record in records.Where(r => r.ValidationState != ReferenceValidationState.Superseded))
        {
            var opportunity = record.Definition;

            var entry = byStage.GetValueOrDefault(opportunity.Stage);
            var counted = opportunity.EstimatedValue is { } value && value.Currency == currency;

            byStage[opportunity.Stage] = (
                entry.Count + 1,
                entry.Value + (counted ? opportunity.EstimatedValue!.Value.Amount : 0m),
                entry.WithoutEstimate + (counted ? 0 : 1));

            if (opportunity.OverstatesRevenue)
                overstating.Add(opportunity.Reference);

            if (opportunity.IsStaleAt(asAt, OpportunityValidationService.StaleAfterDays))
                stale.Add(opportunity.Reference);

            if (opportunity.IsOpen && opportunity.NextActionIsOverdueAt(asAt))
                overdue.Add(opportunity.Reference);

            if (counted)
            {
                // Contracted only where the record actually supports it:
                // Won, naming a contract, and marked as contracted or
                // beyond. Everything else that is still open is potential,
                // and the two are never added together.
                if (opportunity.Stage == PipelineStage.Won
                    && !string.IsNullOrWhiteSpace(opportunity.ContractReference)
                    && opportunity.ValueReality != RevenueReality.Potential)
                    contracted += opportunity.EstimatedValue!.Value.Amount;
                else if (opportunity.IsOpen)
                    potential += opportunity.EstimatedValue!.Value.Amount;
            }

            if (PipelineStages.IsClosed(opportunity.Stage) && DecidedInWindow(opportunity, decidedSince))
            {
                if (opportunity.Stage == PipelineStage.Won)
                    won.Add(opportunity.Reference);
                else
                    lost.Add(opportunity.Reference);
            }
        }

        var summaries = PipelineStages.All
            .Where(byStage.ContainsKey)
            .Select(stage =>
            {
                var (count, value, withoutEstimate) = byStage[stage];

                return new PipelineStageSummary(stage, count, new Money(value, currency), withoutEstimate);
            })
            .ToList();

        return new PipelinePosition(
            asAt,
            currency,
            summaries,
            new Money(contracted, currency),
            new Money(potential, currency),
            Sorted(overstating),
            Sorted(stale),
            Sorted(overdue),
            Sorted(won),
            Sorted(lost));
    }

    private static bool DecidedInWindow(Opportunity opportunity, DateOnly? since)
    {
        if (since is not { } from)
            return true;

        // The decision date is taken from the last recorded interaction:
        // that is when somebody actually recorded the outcome. An
        // opportunity closed with no interaction cannot be dated, and is
        // deliberately left out of the win-rate rather than guessed at.
        return opportunity.LastInteractionDate is { } last && last >= from;
    }

    private static IReadOnlyList<string> Sorted(List<string> values) =>
        values.OrderBy(v => v, StringComparer.Ordinal).ToList();
}
