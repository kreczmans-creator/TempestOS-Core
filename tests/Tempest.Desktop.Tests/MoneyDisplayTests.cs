using Tempest.Core.BusinessGovernance;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 19.10P` (D1) — the rehearsal's own finding: the Home dashboard's
/// Commercial snapshot rendered <c>Money.ToString()</c> directly
/// ("0.00 GBP"), where §7c's own quoted text is "£0.00". These pin
/// <see cref="MoneyDisplay.Format"/> itself; the Desktop views that call it
/// are covered by their own journey tests.
/// </summary>
public sealed class MoneyDisplayTests
{
    [Fact]
    public void Format_Gbp_UsesThePoundSign()
    {
        Assert.Equal("£0.00", MoneyDisplay.Format(Money.Zero(new CurrencyCode("GBP"))));
        Assert.Equal("£1,234.56", MoneyDisplay.Format(new Money(1234.56m, new CurrencyCode("GBP"))));
    }

    [Fact]
    public void Format_Eur_UsesTheEuroSign() =>
        Assert.Equal("€1,234.56", MoneyDisplay.Format(new Money(1234.56m, new CurrencyCode("EUR"))));

    [Fact]
    public void Format_Usd_UsesTheDollarSign() =>
        Assert.Equal("$1,234.56", MoneyDisplay.Format(new Money(1234.56m, new CurrencyCode("USD"))));

    [Fact]
    public void Format_AnyOtherCurrency_UsesTheIsoCode() =>
        Assert.Equal("1,234.56 CHF", MoneyDisplay.Format(new Money(1234.56m, new CurrencyCode("CHF"))));

    [Fact]
    public void Format_AlwaysShowsTwoDecimalPlaces_EvenForAWholeAmount() =>
        Assert.Equal("£100.00", MoneyDisplay.Format(new Money(100m, new CurrencyCode("GBP"))));

    [Fact]
    public void Format_UsesAThousandsSeparator_RegardlessOfMachineLocale() =>
        Assert.Equal("£1,234,567.89", MoneyDisplay.Format(new Money(1234567.89m, new CurrencyCode("GBP"))));

    [Fact]
    public void Format_DoesNotChange_MoneyToString()
    {
        // `Money.ToString()` itself must stay exactly as Core and its own
        // tests/audit rows already rely on it — this formatter is a
        // Desktop-side addition, not a replacement.
        var money = new Money(0m, new CurrencyCode("GBP"));

        Assert.Equal("0.00 GBP", money.ToString());
        Assert.Equal("£0.00", MoneyDisplay.Format(money));
    }
}
