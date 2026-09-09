namespace Tempest.Core.CommercialIntelligence.LeadTimes;

/// <summary>
/// What the organisation knows about how long one thing takes, from one
/// source, under stated conditions.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no universal lead time.</b> How long a part takes depends
/// on the supplier, the process, the quantity, the material, whether
/// tooling exists, how busy the supplier is, where it is going and when
/// you asked. A library that records "6 weeks" against a process has
/// recorded a number that will be wrong for most enquiries and cannot be
/// shown to be wrong for any of them.
/// </para>
/// <para>
/// Every record therefore carries the same
/// <see cref="CommercialApplicability"/> a cost does, plus the one thing
/// a cost does not need: a <see cref="LeadTimeKind"/> saying whether this
/// is somebody's estimate, a supplier's general claim, an observed
/// history, a specific quotation, a contractual commitment, or what one
/// order actually took.
/// </para>
/// </remarks>
public sealed record LeadTimeRecord
{
    /// <summary>The reference the record is known by. Required.</summary>
    public required string Reference { get; init; }

    /// <summary>What the figure is for, in plain terms. Required.</summary>
    public required string Description { get; init; }

    /// <summary>Where the figure came from, and therefore what it commits anybody to. Required in substance.</summary>
    public LeadTimeKind Kind { get; init; } = LeadTimeKind.Unspecified;

    /// <summary>The figure to plan against. Required.</summary>
    public required LeadTimeDuration Typical { get; init; }

    /// <summary>The best it has ever been, or the best the supplier offers. <see langword="null"/> where nobody recorded one.</summary>
    public LeadTimeDuration? Minimum { get; init; }

    /// <summary>The worst it has been, or the worst the supplier warns of. <see langword="null"/> where nobody recorded one.</summary>
    public LeadTimeDuration? Maximum { get; init; }

    /// <summary>Where and when the figure applies, and to what. Required.</summary>
    public required CommercialApplicability Applicability { get; init; }

    /// <summary>Where the figure came from.</summary>
    public CommercialSource Source { get; init; } = CommercialSource.Unrecorded;

    /// <summary>How many orders a historical figure is drawn from. <see langword="null"/> where the figure is not historical.</summary>
    /// <remarks>
    /// A historical average over two orders and one over forty are
    /// different kinds of evidence, and a record that does not say which
    /// it is invites the reader to assume the second.
    /// </remarks>
    public int? ObservationCount { get; init; }

    /// <summary>What the figure assumes — tooling exists, material is in stock, the order is placed by Thursday. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> Assumptions { get; init; } = [];

    /// <summary>What the figure excludes — carriage, inspection, customer approval time. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> Excludes { get; init; } = [];

    /// <summary>Human-readable commentary alongside the structured figures. <see langword="null"/> if none.</summary>
    /// <remarks>
    /// Deliberately secondary. "Usually two to three weeks, longer over
    /// the summer shutdown" is worth keeping, and it is not the
    /// machine-readable value: <see cref="Typical"/>,
    /// <see cref="Minimum"/> and <see cref="Maximum"/> are.
    /// </remarks>
    public string? Commentary { get; init; }

    /// <summary>The quotation or order this figure belongs to, where it belongs to one. <see langword="null"/> otherwise.</summary>
    public string? SourceDocumentReference { get; init; }

    /// <summary>Anything else about the figure. <see langword="null"/> if nothing.</summary>
    public string? Notes { get; init; }

    /// <summary>Whether the supplier is actually bound by the figure.</summary>
    public bool IsSupplierCommitment => LeadTimeKinds.IsSupplierCommitment(Kind);

    /// <summary>Whether the figure records something that happened rather than something expected.</summary>
    public bool IsObserved => LeadTimeKinds.IsObserved(Kind);

    /// <summary>Whether the record's own validity has run out as at <paramref name="asAt"/>.</summary>
    public bool IsStaleAt(DateOnly asAt) => Applicability.IsStaleAt(asAt);

    /// <summary>Whether the three figures are consistent with each other.</summary>
    /// <remarks>
    /// <see langword="true"/> where no bounds are recorded, and
    /// <see langword="false"/> where a bound is stated in a unit that
    /// cannot be compared with the typical figure — a minimum in working
    /// days against a typical in weeks is not a tighter bound, it is an
    /// unanswerable comparison.
    /// </remarks>
    public bool BoundsAreConsistent
    {
        get
        {
            if (Minimum is { } min && (!min.IsComparableWith(Typical) || min.CompareTo(Typical) > 0))
                return false;

            if (Maximum is { } max && (!max.IsComparableWith(Typical) || max.CompareTo(Typical) < 0))
                return false;

            if (Minimum is { } lower && Maximum is { } upper
                && (!lower.IsComparableWith(upper) || lower.CompareTo(upper) > 0))
                return false;

            return true;
        }
    }

    /// <summary>Whether this record applies to <paramref name="enquiry"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="enquiry"/> is <see langword="null"/>.</exception>
    public bool AppliesTo(CommercialEnquiry enquiry) => Applicability.AppliesTo(enquiry);

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
/// What one supplier promised against what they actually did.
/// </summary>
/// <remarks>
/// The comparison D3 exists to make possible. A supplier whose quoted
/// lead time is consistently optimistic is a different commercial
/// proposition from one whose quote holds, and neither is visible unless
/// the promise and the outcome are recorded as separate facts and then
/// put side by side.
/// </remarks>
/// <param name="SupplierReference">The supplier compared.</param>
/// <param name="Promised">What they said, and of what kind.</param>
/// <param name="Actual">What happened.</param>
/// <param name="PromisedKind">Which kind of promise it was — a quotation binds differently from a general claim.</param>
public sealed record LeadTimePerformance(
    string SupplierReference,
    LeadTimeDuration Promised,
    LeadTimeDuration Actual,
    LeadTimeKind PromisedKind)
{
    /// <summary>The supplier compared.</summary>
    public string SupplierReference { get; } = string.IsNullOrWhiteSpace(SupplierReference)
        ? throw new ArgumentException("A lead-time comparison must name the supplier it is about.", nameof(SupplierReference))
        : SupplierReference.Trim();

    /// <summary>Whether the two figures can be compared at all.</summary>
    public bool IsComparable => Promised.IsComparableWith(Actual);

    /// <summary>
    /// How much longer the work took than promised, in the promise's own
    /// units.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> where the two are not comparable — a
    /// promise in working days against an outcome in calendar weeks
    /// cannot be subtracted without a calendar.
    /// </remarks>
    public decimal? Overrun => IsComparable
        ? Actual.Unit == Promised.Unit
            ? Actual.Amount - Promised.Amount
            : (decimal)(Actual.ToElapsed()!.Value.ConvertTo(UnitsAndQuantities.DurationUnits.Day).Value
                        - Promised.ToElapsed()!.Value.ConvertTo(UnitsAndQuantities.DurationUnits.Day).Value)
        : null;

    /// <summary>Whether the work took longer than promised.</summary>
    public bool? WasLate => Overrun is { } overrun ? overrun > 0m : null;

    /// <summary>Whether the promise was one the supplier was actually bound by.</summary>
    /// <remarks>
    /// A missed commitment is a contractual matter; a missed general
    /// claim is a reason to plan differently. Reporting them as the same
    /// thing overstates one and understates the other.
    /// </remarks>
    public bool WasBinding => LeadTimeKinds.IsSupplierCommitment(PromisedKind);
}
