using Tempest.Core.BusinessGovernance;
using Tempest.Core.CommercialIntelligence.Costs;
using Tempest.Core.CommercialIntelligence.LeadTimes;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence.Estimating;

/// <summary>One thing to be estimated, and the enquiry that decides which records apply to it.</summary>
/// <param name="Reference">The line reference the resulting estimate line will carry. Required.</param>
/// <param name="Description">What is being estimated. Required.</param>
/// <param name="Kind">What part of the job it covers.</param>
/// <param name="Enquiry">The commercial question — process, supplier, quantity, region, date. Required.</param>
/// <param name="Quantity">How many units the line covers.</param>
public sealed record EstimateRequestItem(
    string Reference,
    string Description,
    EstimateLineKind Kind,
    CommercialEnquiry Enquiry,
    decimal Quantity = 1m)
{
    /// <summary>The commercial question the item is priced against.</summary>
    public CommercialEnquiry Enquiry { get; } = Enquiry ?? throw new ArgumentNullException(nameof(Enquiry));
}

/// <summary>What was asked of the estimating service, and under what conditions.</summary>
/// <param name="Reference">The reference the resulting estimate will carry. Required.</param>
/// <param name="Subject">What is being estimated overall. Required.</param>
/// <param name="Currency">The currency the estimate is to be stated in. Required.</param>
/// <param name="Items">The things to be priced. Required.</param>
public sealed record EstimateRequest(
    string Reference,
    string Subject,
    CurrencyCode Currency,
    IReadOnlyList<EstimateRequestItem> Items)
{
    /// <summary>The things to be priced.</summary>
    public IReadOnlyList<EstimateRequestItem> Items { get; } = Items ?? throw new ArgumentNullException(nameof(Items));

    /// <summary>How many of the subject the estimate covers.</summary>
    public int Quantity { get; init; } = 1;

    /// <summary>Who is preparing it. <see langword="null"/> where the caller did not say.</summary>
    public string? PreparedByPrincipalId { get; init; }

    /// <summary>How long the estimate is meant to hold. <see langword="null"/> where nobody said.</summary>
    public EffectivePeriod? Validity { get; init; }

    /// <summary>Assumptions the caller wishes recorded alongside any the service derives. Never <see langword="null"/>.</summary>
    public IReadOnlyList<EstimateAssumption> Assumptions { get; init; } = [];
}

/// <summary>Why an estimating run could not price something.</summary>
/// <param name="ItemReference">The item that could not be priced.</param>
/// <param name="Reason">What was missing, in plain words.</param>
public sealed record EstimateGap(string ItemReference, string Reason);

/// <summary>What an estimating run produced, and what it could not.</summary>
/// <remarks>
/// The gaps are the point. A service that quietly omitted what it could
/// not price would produce a total that is certainly too small and looks
/// complete; this one produces an unpriced line and says why.
/// </remarks>
/// <param name="Estimate">The estimate, complete with any unpriced lines.</param>
/// <param name="Gaps">What could not be priced from the libraries, and why. Never <see langword="null"/>.</param>
public sealed record EstimateResult(CostEstimate Estimate, IReadOnlyList<EstimateGap> Gaps)
{
    /// <summary>What could not be priced from the libraries.</summary>
    public IReadOnlyList<EstimateGap> Gaps { get; } = Gaps ?? [];

    /// <summary>Whether every item was priced from a governed record.</summary>
    public bool IsComplete => Gaps.Count == 0;
}

/// <summary>How a historical estimate compares with what the libraries say today.</summary>
/// <param name="Reference">The estimate reproduced.</param>
/// <param name="Reproduces">Whether every pinned source still resolves to the figure the estimate used.</param>
/// <param name="Divergences">Where the current library differs from what was pinned. Never <see langword="null"/>.</param>
/// <param name="UnresolvedPins">Pins whose records the libraries no longer hold. Never <see langword="null"/>.</param>
public sealed record EstimateReproduction(
    string Reference,
    bool Reproduces,
    IReadOnlyList<string> Divergences,
    IReadOnlyList<ReferencePin> UnresolvedPins)
{
    /// <summary>Where the current library differs from what was pinned.</summary>
    public IReadOnlyList<string> Divergences { get; } = Divergences ?? [];

    /// <summary>Pins whose records the libraries no longer hold.</summary>
    public IReadOnlyList<ReferencePin> UnresolvedPins { get; } = UnresolvedPins ?? [];
}

/// <summary>Builds estimates from the commercial libraries, and checks that old ones still say what they said.</summary>
public interface IEstimatingService
{
    /// <summary>
    /// Prices <paramref name="request"/> from the cost and lead-time
    /// libraries, pinning every record it reads.
    /// </summary>
    /// <remarks>
    /// Reads only <em>released</em> records: a draft cost has not been
    /// checked by anybody and must not silently reach a customer-facing
    /// number. Where nothing applies, the line is produced unpriced and a
    /// gap is reported.
    /// </remarks>
    /// <param name="request">What to estimate.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    Task<EstimateResult> BuildAsync(EstimateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-reads every record a historical estimate pinned and reports
    /// where the libraries have moved since.
    /// </summary>
    /// <remarks>
    /// The estimate is never altered. This answers "would we estimate the
    /// same today?", which is a different question from "what did we
    /// estimate?" — and the second must keep its answer whatever the
    /// first turns out to be.
    /// </remarks>
    /// <param name="estimate">The estimate to reproduce.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <exception cref="ArgumentNullException"><paramref name="estimate"/> is <see langword="null"/>.</exception>
    Task<EstimateReproduction> ReproduceAsync(CostEstimate estimate, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IEstimatingService"/> implementation.</summary>
/// <remarks>
/// <para>
/// Deterministic and read-only: it registers nothing, and given the same
/// libraries in the same state it produces the same estimate every time.
/// Persisting the result is the caller's decision, made through
/// <see cref="ICostEstimateCatalog"/>.
/// </para>
/// <para>
/// It also does not decide anything. Where several cost records apply, it
/// takes the one the cost library ranks first and records the pin, so a
/// person reading the estimate can see exactly which figure was used and
/// disagree with it.
/// </para>
/// </remarks>
public sealed class EstimatingService : IEstimatingService
{
    private readonly IProcessCostCatalog _costs;
    private readonly ILeadTimeCatalog? _leadTimes;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="EstimatingService"/> class.</summary>
    /// <param name="costs">The cost library estimates are priced from.</param>
    /// <param name="leadTimes">The lead-time library, for attaching a lead time to each line. Optional.</param>
    /// <param name="timeProvider">The clock the preparation date is taken from. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="costs"/> is <see langword="null"/>.</exception>
    public EstimatingService(IProcessCostCatalog costs, ILeadTimeCatalog? leadTimes = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(costs);

        _costs = costs;
        _leadTimes = leadTimes;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<EstimateResult> BuildAsync(EstimateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lines = new List<EstimateLine>(request.Items.Count);
        var gaps = new List<EstimateGap>();

        foreach (var item in request.Items)
        {
            var (line, gap) = await PriceAsync(item, request.Currency, cancellationToken).ConfigureAwait(false);

            lines.Add(line);

            if (gap is not null)
                gaps.Add(gap);
        }

        var estimate = new CostEstimate
        {
            Reference = request.Reference,
            Subject = request.Subject,
            Currency = request.Currency,
            Quantity = request.Quantity,
            Lines = lines,
            Assumptions = request.Assumptions,
            PreparedByPrincipalId = request.PreparedByPrincipalId,
            PreparedOn = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime),
            Validity = request.Validity
        };

        return new EstimateResult(estimate, gaps);
    }

    /// <inheritdoc />
    public async Task<EstimateReproduction> ReproduceAsync(CostEstimate estimate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(estimate);

        var divergences = new List<string>();
        var unresolved = new List<ReferencePin>();

        foreach (var pin in estimate.AllPins)
        {
            if (!string.Equals(pin.Library, _costs.LibraryName, StringComparison.Ordinal))
                continue;

            var record = await _costs.FindAsync(pin.RecordId, cancellationToken).ConfigureAwait(false);

            if (record is null)
            {
                unresolved.Add(pin);
                continue;
            }

            if (record.RevisionNumber != pin.RevisionNumber)
                divergences.Add(
                    $"Cost record '{pin.RecordId}' was pinned at revision {pin.RevisionNumber} and now stands at "
                    + $"revision {record.RevisionNumber}.");

            if (record.ValidationState == ReferenceValidationState.Superseded)
                divergences.Add($"Cost record '{pin.RecordId}' has since been superseded.");
        }

        return new EstimateReproduction(
            estimate.Reference,
            divergences.Count == 0 && unresolved.Count == 0,
            divergences,
            unresolved);
    }

    private async Task<(EstimateLine Line, EstimateGap? Gap)> PriceAsync(
        EstimateRequestItem item,
        CurrencyCode currency,
        CancellationToken cancellationToken)
    {
        var applicable = await _costs.FindApplicableAsync(item.Enquiry, cancellationToken).ConfigureAwait(false);

        var chosen = applicable.FirstOrDefault(r => r.Definition.Currency == currency);

        if (chosen is null)
        {
            var reason = applicable.Count == 0
                ? "No released cost record applies to this enquiry."
                : $"Cost records apply but none is stated in {currency}, and TempestOS does not convert currencies.";

            return (Unpriced(item), new EstimateGap(item.Reference, reason));
        }

        var leadTime = await FindLeadTimeAsync(item, cancellationToken).ConfigureAwait(false);

        var line = new EstimateLine(
            item.Reference,
            item.Kind,
            item.Description,
            item.Quantity,
            chosen.Definition.Cost,
            SourcePins: [ReferencePin.For(_costs.LibraryName, chosen), .. leadTime.Pins],
            LeadTime: leadTime.Duration);

        return (line, null);
    }

    private async Task<(LeadTimeDuration? Duration, IReadOnlyList<ReferencePin> Pins)> FindLeadTimeAsync(
        EstimateRequestItem item,
        CancellationToken cancellationToken)
    {
        if (_leadTimes is null)
            return (null, []);

        var applicable = await _leadTimes.FindApplicableAsync(item.Enquiry, cancellationToken).ConfigureAwait(false);
        var chosen = applicable.FirstOrDefault();

        return chosen is null
            ? (null, [])
            : (chosen.Definition.Typical, [ReferencePin.For(_leadTimes.LibraryName, chosen)]);
    }

    private static EstimateLine Unpriced(EstimateRequestItem item) =>
        new(item.Reference, item.Kind, item.Description, item.Quantity, CostFigure.Unknown);
}
