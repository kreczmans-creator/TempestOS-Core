using Tempest.Core.BusinessGovernance;

namespace Tempest.Core.CommercialIntelligence;

/// <summary>
/// How firmly a monetary figure is known.
/// </summary>
/// <remarks>
/// <b>Unknown is not zero.</b> The single most damaging thing a
/// commercial library can do is let an absent cost total as nothing,
/// producing an estimate that looks complete and is short by whatever
/// nobody priced. <see cref="Unknown"/> exists so that absence propagates
/// into every total that touches it.
/// </remarks>
public enum CostCertainty
{
    /// <summary>Nobody has priced this. Not zero, and not free.</summary>
    Unknown,

    /// <summary>Somebody's judgement, with no source behind it.</summary>
    Estimated,

    /// <summary>Known to lie between two values, and no more precisely than that.</summary>
    Ranged,

    /// <summary>Taken from a supplier's own quotation.</summary>
    Quoted,

    /// <summary>Exact and evidenced — an invoice, a published price list, an agreed rate.</summary>
    Exact
}

/// <summary>
/// A monetary figure that knows how well it is known.
/// </summary>
/// <remarks>
/// <para>
/// The type that stops a commercial estimate manufacturing precision. An
/// exact figure carries one amount; a ranged figure carries two and stays
/// a range through every subsequent addition; an unknown figure carries
/// none and makes every total containing it unknown too.
/// </para>
/// <para>
/// Arithmetic is deliberately limited to what is defensible: adding two
/// figures, and scaling one by a quantity. There is no averaging, no
/// midpoint-of-range shortcut, and no way to coerce a range into a single
/// number — because choosing which end of a range to believe is a
/// commercial judgement, not an operation.
/// </para>
/// </remarks>
public sealed record CostFigure
{
    private CostFigure(CostCertainty certainty, Money? lowest, Money? highest)
    {
        Certainty = certainty;
        Lowest = lowest;
        Highest = highest;
    }

    /// <summary>How firmly the figure is known.</summary>
    public CostCertainty Certainty { get; }

    /// <summary>The lowest the figure could be. <see langword="null"/> where it is unknown.</summary>
    public Money? Lowest { get; }

    /// <summary>The highest the figure could be. <see langword="null"/> where it is unknown.</summary>
    public Money? Highest { get; }

    /// <summary>Whether nobody has priced this.</summary>
    public bool IsUnknown => Certainty == CostCertainty.Unknown;

    /// <summary>Whether the figure is a single value rather than a range.</summary>
    public bool IsSingleValued => Lowest is not null && Lowest == Highest;

    /// <summary>The currency, where the figure has one. <see langword="null"/> for an unknown figure.</summary>
    public CurrencyCode? Currency => Lowest?.Currency;

    /// <summary>A figure nobody has priced.</summary>
    /// <remarks>
    /// Carries no currency, deliberately: an unknown amount in pounds and
    /// an unknown amount in euros are the same absence of information.
    /// </remarks>
    public static CostFigure Unknown { get; } = new(CostCertainty.Unknown, null, null);

    /// <summary>An exact, evidenced figure.</summary>
    public static CostFigure Exact(Money amount) => Single(CostCertainty.Exact, amount);

    /// <summary>A figure taken from a supplier's quotation.</summary>
    public static CostFigure Quoted(Money amount) => Single(CostCertainty.Quoted, amount);

    /// <summary>Somebody's judgement, with no source behind it.</summary>
    public static CostFigure Estimated(Money amount) => Single(CostCertainty.Estimated, amount);

    /// <summary>A figure known only to lie between two values.</summary>
    /// <exception cref="CurrencyMismatchException">The two bounds are in different currencies.</exception>
    /// <exception cref="ArgumentException"><paramref name="highest"/> is below <paramref name="lowest"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Either bound is negative.</exception>
    public static CostFigure Range(Money lowest, Money highest)
    {
        if (lowest.Currency != highest.Currency)
            throw new CurrencyMismatchException(lowest.Currency, highest.Currency);

        RequireNotNegative(lowest);
        RequireNotNegative(highest);

        if (highest < lowest)
            throw new ArgumentException($"A cost range cannot end ({highest}) below where it starts ({lowest}).", nameof(highest));

        return lowest == highest
            ? new CostFigure(CostCertainty.Estimated, lowest, highest)
            : new CostFigure(CostCertainty.Ranged, lowest, highest);
    }

    /// <summary>
    /// The two figures added.
    /// </summary>
    /// <remarks>
    /// <b>Unknown wins.</b> Adding a known cost to an unknown one gives an
    /// unknown total, because the total genuinely is unknown; returning
    /// the known part would report a number that is certainly too small.
    /// Adding two ranges gives a range, and the certainty of a sum is the
    /// weakest of its parts.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    /// <exception cref="CurrencyMismatchException">The two figures are in different currencies.</exception>
    public static CostFigure operator +(CostFigure left, CostFigure right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.IsUnknown || right.IsUnknown)
            return Unknown;

        return new CostFigure(
            Weaker(left.Certainty, right.Certainty),
            left.Lowest!.Value + right.Lowest!.Value,
            left.Highest!.Value + right.Highest!.Value);
    }

    /// <summary>The figure scaled by a quantity.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="factor"/> is negative.</exception>
    public static CostFigure operator *(CostFigure figure, decimal factor)
    {
        ArgumentNullException.ThrowIfNull(figure);
        ArgumentOutOfRangeException.ThrowIfNegative(factor);

        return figure.IsUnknown
            ? Unknown
            : new CostFigure(figure.Certainty, figure.Lowest!.Value * factor, figure.Highest!.Value * factor);
    }

    /// <summary>Sums <paramref name="figures"/>, and reports unknown if any of them is.</summary>
    /// <remarks>
    /// An empty sequence sums to zero in <paramref name="currency"/>, not
    /// to unknown: nothing to pay is a real answer, and it is different
    /// from nobody having priced anything.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="figures"/> is <see langword="null"/>.</exception>
    /// <exception cref="CurrencyMismatchException">A figure is in a different currency.</exception>
    public static CostFigure Sum(IEnumerable<CostFigure> figures, CurrencyCode currency)
    {
        ArgumentNullException.ThrowIfNull(figures);

        var total = Exact(Money.Zero(currency));

        foreach (var figure in figures)
        {
            if (figure.IsUnknown)
                return Unknown;

            if (figure.Currency != currency)
                throw new CurrencyMismatchException(currency, figure.Currency!.Value);

            total += figure;
        }

        return total;
    }

    /// <inheritdoc />
    public override string ToString() => Certainty switch
    {
        CostCertainty.Unknown => "unknown",
        CostCertainty.Ranged => $"{Lowest}–{Highest} ({Certainty})",
        _ => $"{Lowest} ({Certainty})",
    };

    private static CostFigure Single(CostCertainty certainty, Money amount)
    {
        RequireNotNegative(amount);

        return new CostFigure(certainty, amount, amount);
    }

    private static void RequireNotNegative(Money amount)
    {
        if (amount.IsNegative)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "A cost cannot be negative. A credit is not a cost.");
    }

    private static CostCertainty Weaker(CostCertainty left, CostCertainty right) =>
        (CostCertainty)Math.Min((int)left, (int)right);
}
