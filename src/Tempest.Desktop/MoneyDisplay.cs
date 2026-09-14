using System.Globalization;
using Tempest.Core.BusinessGovernance;

namespace Tempest.Desktop;

/// <summary>
/// Renders a <see cref="Money"/> amount the way a person reads one
/// (`WP 19.10P`, D1: the rehearsal's own "Quotes: 0 open, 0.00 GBP total
/// value." finding against §7c's own quoted "£0.00") — a currency symbol
/// before the amount for the three currencies TempestOS's own fixtures and
/// clients actually use, the ISO code for anything else, always two decimal
/// places with a thousands separator, in the invariant culture so the same
/// figure renders identically regardless of the machine's own locale.
/// </summary>
/// <remarks>
/// <see cref="Money.ToString"/> itself is deliberately untouched — Core's
/// own audit rows and every existing test asserting that exact
/// "1234.56 GBP" shape keep working unchanged. This is the one Desktop-side
/// formatter every surface a person looks at an amount on calls instead.
/// </remarks>
public static class MoneyDisplay
{
    /// <summary>Formats <paramref name="money"/> for display: <c>"£1,234.56"</c>, <c>"€1,234.56"</c>, <c>"$1,234.56"</c>, or <c>"1,234.56 XYZ"</c> for any other currency.</summary>
    public static string Format(Money money)
    {
        var amount = money.Amount.ToString("N2", CultureInfo.InvariantCulture);
        var symbol = SymbolFor(money.Currency.ToString());

        return symbol is null ? $"{amount} {money.Currency}" : $"{symbol}{amount}";
    }

    private static string? SymbolFor(string isoCurrencyCode) => isoCurrencyCode switch
    {
        "GBP" => "£",
        "EUR" => "€",
        "USD" => "$",
        _ => null,
    };
}
