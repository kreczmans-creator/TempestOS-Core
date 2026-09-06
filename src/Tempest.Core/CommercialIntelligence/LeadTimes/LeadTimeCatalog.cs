using Tempest.Core.CommercialIntelligence.Suppliers;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Manufacturing;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.CommercialIntelligence.LeadTimes;

/// <summary>A deterministic filter over the lead-time library.</summary>
public sealed record LeadTimeQuery
{
    /// <summary>Matches any record whose reference or description contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>The commercial question being asked. <see langword="null"/> to leave every dimension open.</summary>
    public CommercialEnquiry? Enquiry { get; init; }

    /// <summary>Matches any of these kinds. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<LeadTimeKind> Kinds { get; init; } = [];

    /// <summary>Matches only figures a supplier is actually bound by. <see langword="null"/> to match any.</summary>
    public bool? IsSupplierCommitment { get; init; }

    /// <summary>Matches records whose typical figure is no longer than this. <see langword="null"/> for no ceiling.</summary>
    /// <remarks>
    /// A record whose unit cannot be compared with the ceiling's is
    /// excluded rather than admitted: a working-day figure does not
    /// satisfy a calendar-week bound, and guessing a five-day week here
    /// would be the conversion `ADR-0133` refuses.
    /// </remarks>
    public LeadTimeDuration? NoLongerThan { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The commercial lead-time library.</summary>
public interface ILeadTimeCatalog : IReferenceDataCatalog<LeadTimeRecord>
{
    /// <summary>Returns the record registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<LeadTimeRecord>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered record matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<LeadTimeRecord>>> SearchAsync(LeadTimeQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every released record that applies to <paramref name="enquiry"/>,
    /// strongest claim first.
    /// </summary>
    /// <remarks>
    /// Ordered by <see cref="LeadTimeKind"/> so a commitment outranks a
    /// quotation, which outranks an observed history, which outranks
    /// somebody's estimate. A caller taking the head therefore gets the
    /// firmest thing anybody has said — and can still see the rest,
    /// because the weaker figures are frequently the more realistic ones.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="enquiry"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<LeadTimeRecord>>> FindApplicableAsync(
        CommercialEnquiry enquiry,
        CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="ILeadTimeCatalog"/> implementation.</summary>
public sealed class LeadTimeCatalog : ReferenceDataCatalog<LeadTimeRecord>, ILeadTimeCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every lead-time record's own backing document carries.</summary>
    public const string LeadTimeDocumentKind = "CommercialLeadTime";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>leadTimeId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "CommercialLeadTimes.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each record reference to the <c>leadTimeId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "CommercialLeadTimes.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="LeadTimeCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own lead-time records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public LeadTimeCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "CommercialLeadTimes";

    /// <inheritdoc />
    public override string DocumentKind => LeadTimeDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<LeadTimeRecord>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(LeadTimeRecord.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<LeadTimeRecord>>> SearchAsync(
        LeadTimeQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReferenceRecord<LeadTimeRecord>>> FindApplicableAsync(
        CommercialEnquiry enquiry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(enquiry);

        var applicable = await FilterAsync(
            record => record.ValidationState == ReferenceValidationState.Released
                      && record.Definition.AppliesTo(enquiry),
            cancellationToken).ConfigureAwait(false);

        return applicable
            .OrderByDescending(r => LeadTimeKinds.Strength(r.Definition.Kind))
            .ThenBy(r => r.Definition.Applicability.Quantities?.Width ?? int.MaxValue)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(LeadTimeRecord definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(LeadTimeRecord definition) => $"Lead-time reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<LeadTimeRecord> record, LeadTimeQuery query)
    {
        var leadTime = record.Definition;

        if (query.TextContains is { } text
            && !leadTime.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !leadTime.Description.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Enquiry is { } enquiry && !leadTime.AppliesTo(enquiry))
            return false;

        if (query.Kinds.Count > 0 && !query.Kinds.Contains(leadTime.Kind))
            return false;

        if (query.IsSupplierCommitment is { } binding && leadTime.IsSupplierCommitment != binding)
            return false;

        if (query.NoLongerThan is { } ceiling
            && (!leadTime.Typical.IsComparableWith(ceiling) || leadTime.Typical.CompareTo(ceiling) > 0))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>The diagnostic codes D3's validation service reports.</summary>
public static class LeadTimeValidationRules
{
    /// <summary>The record does not say where its figure came from, so what it commits anybody to is unknown.</summary>
    public const string KindMustBeStated = "TEMPEST-CIL-001";

    /// <summary>The figure does not say what unit it is in.</summary>
    public const string UnitMustBeStated = "TEMPEST-CIL-002";

    /// <summary>The figure is zero, which no real process achieves.</summary>
    public const string LeadTimeIsZero = "TEMPEST-CIL-003";

    /// <summary>The minimum, typical and maximum figures contradict each other, or cannot be compared.</summary>
    public const string BoundsAreInconsistent = "TEMPEST-CIL-004";

    /// <summary>A historical figure does not say how many orders it is drawn from.</summary>
    public const string HistoricalNeedsObservationCount = "TEMPEST-CIL-005";

    /// <summary>A historical figure is drawn from too few orders to mean much.</summary>
    public const string HistoricalSampleIsSmall = "TEMPEST-CIL-006";

    /// <summary>A quoted or committed figure names no supplier.</summary>
    public const string SupplierFigureNeedsSupplier = "TEMPEST-CIL-007";

    /// <summary>The record names a supplier the supplier database does not hold.</summary>
    public const string SupplierMustResolve = "TEMPEST-CIL-008";

    /// <summary>The record names an `A7` process the manufacturing library does not hold.</summary>
    public const string ProcessMustResolve = "TEMPEST-CIL-009";

    /// <summary>A quoted or committed figure names no quotation or order it came from.</summary>
    public const string SourceDocumentMissing = "TEMPEST-CIL-010";

    /// <summary>The figure states neither what it assumes nor what it excludes.</summary>
    public const string ConditionsNotStated = "TEMPEST-CIL-011";

    /// <summary>The figure is implausibly long.</summary>
    public const string LeadTimeIsImplausible = "TEMPEST-CIL-012";

    /// <summary>Another record covers the same supplier, process, quantities and kind.</summary>
    public const string OverlappingLeadTimeRecords = "TEMPEST-CIL-013";

    /// <summary>Two lead-time records share one reference.</summary>
    public const string DuplicateLeadTimeReference = "TEMPEST-CIL-014";
}
