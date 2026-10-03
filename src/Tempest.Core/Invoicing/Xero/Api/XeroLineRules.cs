using System.Globalization;

namespace Tempest.Core.Invoicing.Xero.Api;

/// <summary>
/// The rules every line item TempestOS writes to Xero follows — quotes,
/// sales invoices, purchase orders and bills alike (`v0.24.0` review M7,
/// m19), so the same TempestOS line reads the same in every Xero document.
/// </summary>
/// <remarks>
/// <para>
/// <b>Unit amounts</b> are sent at up to <see cref="XeroAccountingApi.UnitAmountDecimalPlaces"/>
/// places with <c>?unitdp=4</c> (<see cref="XeroAccountingApi"/> adds it), and
/// Xero computes each line's amount as <c>quantity × unit amount</c> rounded
/// to the penny — exactly <see cref="LineAmount"/>. A unit amount with more
/// places than four is rounded to four only when that leaves the line amount
/// (and so every total) unchanged; otherwise the line is refused with a
/// reason, never sent to come out differently in Xero.
/// </para>
/// <para>
/// <b>Descriptions</b> longer than Xero's <see cref="MaximumDescriptionLength"/>
/// are shortened, ending in <see cref="ShortenedMarker"/>, so the Xero
/// document itself says the full text is in TempestOS.
/// </para>
/// </remarks>
public static class XeroLineRules
{
    /// <summary>Xero's limit on a line item's <c>Description</c> (4,000 characters).</summary>
    public const int MaximumDescriptionLength = 4000;

    /// <summary>What a shortened description ends with.</summary>
    public const string ShortenedMarker = "… [shortened for Xero; full text in TempestOS]";

    /// <summary>A line's amount as Xero computes it: <c>quantity × unit amount</c>, rounded to two places (half away from zero).</summary>
    /// <param name="quantity">The quantity.</param>
    /// <param name="unitAmount">The unit amount.</param>
    public static decimal LineAmount(decimal quantity, decimal unitAmount) =>
        Math.Round(quantity * unitAmount, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The unit amount to send for a line: <paramref name="unitAmount"/>
    /// itself when it has at most four decimal places; rounded to four when
    /// that leaves <see cref="LineAmount"/> unchanged; otherwise
    /// <see langword="null"/> with <paramref name="problem"/> saying why.
    /// </summary>
    /// <param name="quantity">The line's quantity.</param>
    /// <param name="unitAmount">The line's unit amount as TempestOS holds it.</param>
    /// <param name="problem">Why the unit amount cannot be sent; <see langword="null"/> when it can.</param>
    public static decimal? FitUnitAmount(decimal quantity, decimal unitAmount, out string? problem)
    {
        var places = XeroAccountingApi.UnitAmountDecimalPlaces;
        var fitted = Math.Round(unitAmount, places, MidpointRounding.AwayFromZero);
        if (fitted == unitAmount || LineAmount(quantity, fitted) == LineAmount(quantity, unitAmount))
        {
            problem = null;
            return fitted;
        }

        problem = string.Create(
            CultureInfo.InvariantCulture,
            $"the unit price {unitAmount} has more than {places} decimal places, and Xero (which keeps {places}) would make the line {LineAmount(quantity, fitted):0.00} instead of {LineAmount(quantity, unitAmount):0.00}; round the unit price to {places} places in TempestOS");
        return null;
    }

    /// <summary>
    /// The description to send: <paramref name="description"/> (or
    /// <c>"(no description)"</c> when blank), shortened to
    /// <see cref="MaximumDescriptionLength"/> characters ending in
    /// <see cref="ShortenedMarker"/> when longer.
    /// </summary>
    /// <param name="description">The line's description.</param>
    /// <param name="shortened">Whether it was shortened.</param>
    public static string FitDescription(string? description, out bool shortened)
    {
        var text = string.IsNullOrWhiteSpace(description) ? "(no description)" : description;
        shortened = text.Length > MaximumDescriptionLength;
        if (!shortened)
            return text;

        var keep = MaximumDescriptionLength - ShortenedMarker.Length;

        // Never split a surrogate pair.
        if (char.IsHighSurrogate(text[keep - 1]))
            keep--;

        return string.Concat(text.AsSpan(0, keep).TrimEnd(), ShortenedMarker);
    }

    /// <summary><see cref="FitDescription(string?, out bool)"/>, discarding whether it was shortened.</summary>
    /// <param name="description">The line's description.</param>
    public static string FitDescription(string? description) => FitDescription(description, out _);
}
