namespace Tempest.Core.CommercialIntelligence;

/// <summary>
/// The range of order quantities a commercial figure applies to.
/// </summary>
/// <remarks>
/// <para>
/// <b>A price without a quantity basis is not a price.</b> "£40 per
/// bracket" means one thing at a quantity of five and something entirely
/// different at five thousand, and a commercial library that records the
/// number without the band has recorded a rumour. Every cost and every
/// lead time in `P03` carries one.
/// </para>
/// <para>
/// The band is inclusive at both ends, and an absent upper bound means
/// genuinely unbounded — "50 and above" — never "nobody wrote it down".
/// Where the upper bound is unknown, the band should not be recorded at
/// all and the record reports as incomplete.
/// </para>
/// </remarks>
/// <param name="Minimum">The smallest quantity the figure applies to, inclusive.</param>
/// <param name="Maximum">The largest, inclusive. <see langword="null"/> for genuinely unbounded.</param>
public sealed record QuantityBand(int Minimum, int? Maximum) : IComparable<QuantityBand>
{
    /// <summary>The smallest quantity the figure applies to.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="Minimum"/> is less than one.</exception>
    public int Minimum { get; } = Minimum < 1
        ? throw new ArgumentOutOfRangeException(nameof(Minimum), Minimum, "A quantity band starts at one. A price for zero components is not a price.")
        : Minimum;

    /// <summary>The largest quantity the figure applies to.</summary>
    /// <exception cref="ArgumentException"><paramref name="Maximum"/> is below <paramref name="Minimum"/>.</exception>
    public int? Maximum { get; } = Maximum is { } max && max < Minimum
        ? throw new ArgumentException($"A quantity band cannot end ({max}) below where it starts ({Minimum}).", nameof(Maximum))
        : Maximum;

    /// <summary>A band covering exactly one quantity.</summary>
    public static QuantityBand Exactly(int quantity) => new(quantity, quantity);

    /// <summary>A band with no upper limit.</summary>
    public static QuantityBand From(int minimum) => new(minimum, null);

    /// <summary>A band covering any quantity.</summary>
    public static QuantityBand Any { get; } = new(1, null);

    /// <summary>Whether the band has no upper limit.</summary>
    public bool IsUnbounded => Maximum is null;

    /// <summary>Whether <paramref name="quantity"/> falls in the band.</summary>
    public bool Contains(int quantity) => quantity >= Minimum && (Maximum is not { } max || quantity <= max);

    /// <summary>Whether two bands share at least one quantity — the check that finds a library pricing the same quantity twice.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    public bool Overlaps(QuantityBand other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var startsBeforeOtherEnds = other.Maximum is not { } otherMax || Minimum <= otherMax;
        var endsAfterOtherStarts = Maximum is not { } max || max >= other.Minimum;

        return startsBeforeOtherEnds && endsAfterOtherStarts;
    }

    /// <summary>How specific the band is, so the tightest applicable band can be preferred over a catch-all.</summary>
    /// <remarks>
    /// <see langword="null"/> for an unbounded band, which is by
    /// definition less specific than any bounded one.
    /// </remarks>
    public int? Width => Maximum is { } max ? max - Minimum + 1 : null;

    /// <summary>Orders bands by where they start, then by how specific they are.</summary>
    public int CompareTo(QuantityBand? other)
    {
        if (other is null)
            return 1;

        var byStart = Minimum.CompareTo(other.Minimum);

        if (byStart != 0)
            return byStart;

        return (Width, other.Width) switch
        {
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
            var (mine, theirs) => mine!.Value.CompareTo(theirs!.Value),
        };
    }

    /// <inheritdoc />
    public override string ToString() => Maximum is { } max
        ? Minimum == max ? $"{Minimum}" : $"{Minimum}–{max}"
        : $"{Minimum}+";
}
