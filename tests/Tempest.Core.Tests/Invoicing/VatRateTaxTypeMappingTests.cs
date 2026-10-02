using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Settings;

namespace Tempest.Core.Tests.Invoicing;

/// <summary>
/// `WP 21.3B`: the connector seam's own VAT rate → tax type mapping, and
/// the refusal it drives when a line's own rate cannot be expressed —
/// <see cref="FakeInvoicingConnector"/> models the identical refusal a
/// real Xero call would answer (<c>XeroConnectorTests</c>'s own coverage
/// of the real HTTP body), so this is exercised here without any HTTP
/// stubbing at all.
/// </summary>
public sealed class VatRateTaxTypeMappingTests
{
    [Theory]
    [InlineData(VatRate.Standard, "OUTPUT2")]
    [InlineData(VatRate.Reduced, "RROUTPUT")]
    [InlineData(VatRate.Zero, "ZERORATEDOUTPUT")]
    [InlineData(VatRate.Exempt, "EXEMPTOUTPUT")]
    [InlineData(VatRate.OutOfScope, "NONE")]
    public void TryMap_MapsEveryDeclaredRate_ToItsOwnXeroTaxType(VatRate rate, string expectedTaxType)
    {
        Assert.True(VatRateTaxTypeMapping.TryMap(rate, out var taxType));
        Assert.Equal(expectedTaxType, taxType);
    }

    /// <summary>A value outside the enum's own declared range (a corrupted stored value, or a future rate added without a matching row here) — the defensive branch "refuse a send whose rate the connector cannot express" actually needs.</summary>
    [Fact]
    public void TryMap_AnUndeclaredValue_ReturnsFalse()
    {
        var undeclared = (VatRate)99;

        Assert.False(VatRateTaxTypeMapping.TryMap(undeclared, out var taxType));
        Assert.Null(taxType);
    }

    [Fact]
    public void FindUnmappableReason_EveryLineMaps_ReturnsNull()
    {
        var lines = new[]
        {
            Line(VatRate.Standard),
            Line(VatRate.Reduced),
            Line(VatRate.OutOfScope),
        };

        Assert.Null(VatRateTaxTypeMapping.FindUnmappableReason(lines));
    }

    [Fact]
    public void FindUnmappableReason_OneLineDoesNotMap_NamesItsOwnDescription()
    {
        var badLine = Line((VatRate)99, description: "Unusual line");
        var lines = new[] { Line(VatRate.Standard), badLine };

        var reason = VatRateTaxTypeMapping.FindUnmappableReason(lines);

        Assert.NotNull(reason);
        Assert.Contains("Unusual line", reason, StringComparison.Ordinal);
        Assert.Contains("99", reason, StringComparison.Ordinal);
    }

    /// <summary>The refusal itself, end to end, through a connector — never a thrown exception (`ADR-0151`), exactly like any other rejection this seam answers.</summary>
    [Fact]
    public async Task FakeInvoicingConnector_RefusesACreateWhoseLineCarriesAnUnmappableRate()
    {
        var connector = new FakeInvoicingConnector();
        var badLine = Line((VatRate)99, description: "Bad rate line");
        var snapshot = new InvoiceRequestSnapshot(
            Guid.NewGuid(), "ORG-1", "Org One Ltd", "PO-1", CurrencyCode.Gbp, [badLine], badLine.Amount);

        var result = await connector.CreateDraftInvoiceAsync(snapshot, Guid.NewGuid().ToString());

        Assert.Equal(ConnectorOutcome.Rejected, result.Outcome);
        Assert.Contains("Bad rate line", result.Reason, StringComparison.Ordinal);

        // Refused before ever recording the call as a create that could
        // later be found by reference — the identical "nothing happened"
        // shape a real Xero 400/422 leaves behind.
        Assert.DoesNotContain(connector.Calls, c => c.Member == nameof(FakeInvoicingConnector.CreateDraftInvoiceAsync));
    }

    [Fact]
    public async Task FakeInvoicingConnector_EveryDeclaredRate_CreatesSuccessfully()
    {
        foreach (var rate in Enum.GetValues<VatRate>())
        {
            var connector = new FakeInvoicingConnector();
            var line = Line(rate);
            var snapshot = new InvoiceRequestSnapshot(Guid.NewGuid(), "ORG-1", "Org One Ltd", "PO-1", CurrencyCode.Gbp, [line], line.Amount);

            var result = await connector.CreateDraftInvoiceAsync(snapshot, Guid.NewGuid().ToString());

            Assert.True(result.Outcome == ConnectorOutcome.Ok, $"{rate} was refused: {result.Reason}");
        }
    }

    // ------------------------------------------------- `v0.24.0` X1 (D6, design §8)

    [Theory]
    [InlineData(VatRate.Standard, "INPUT2")]
    [InlineData(VatRate.Reduced, "RRINPUT")]
    [InlineData(VatRate.Zero, "ZERORATEDINPUT")]
    [InlineData(VatRate.Exempt, "EXEMPTINPUT")]
    [InlineData(VatRate.OutOfScope, "NONE")]
    public void TryMap_Purchases_MapsEveryDeclaredRate_ToXerosInputTaxType(VatRate rate, string expectedTaxType)
    {
        Assert.True(VatRateTaxTypeMapping.TryMap(rate, VatTaxDirection.Purchases, out var taxType));
        Assert.Equal(expectedTaxType, taxType);
    }

    [Fact]
    public void TryMap_Sales_IsTheOriginalOutputTable()
    {
        foreach (var rate in Enum.GetValues<VatRate>())
        {
            Assert.True(VatRateTaxTypeMapping.TryMap(rate, out var original));
            Assert.True(VatRateTaxTypeMapping.TryMap(rate, VatTaxDirection.Sales, out var sales));
            Assert.Equal(original, sales);
        }
    }

    [Fact]
    public void TryMap_AnUndeclaredRateOrDirection_ReturnsFalse()
    {
        Assert.False(VatRateTaxTypeMapping.TryMap((VatRate)99, VatTaxDirection.Purchases, out var byRate));
        Assert.Null(byRate);
        Assert.False(VatRateTaxTypeMapping.TryMap(VatRate.Standard, (VatTaxDirection)7, out var byDirection));
        Assert.Null(byDirection);
    }

    [Theory]
    [InlineData("OUTPUT2", VatTaxDirection.Sales, null)]
    [InlineData("output2", VatTaxDirection.Sales, null)]
    [InlineData("NONE", VatTaxDirection.Sales, null)]
    [InlineData("NONE", VatTaxDirection.Purchases, null)]
    [InlineData("INPUT2", VatTaxDirection.Purchases, null)]
    [InlineData("OUTPUT9", VatTaxDirection.Sales, "Xero has no active tax rate OUTPUT9.")]
    [InlineData("OUTPUT", VatTaxDirection.Sales, "Xero has no active tax rate OUTPUT.")]
    [InlineData("RRINPUT", VatTaxDirection.Purchases, "Xero has no active tax rate RRINPUT.")]
    [InlineData("INPUT2", VatTaxDirection.Sales, "Xero's tax rate INPUT2 (20% (VAT on Expenses)) cannot be used on sales lines.")]
    [InlineData("OUTPUT2", VatTaxDirection.Purchases, "Xero's tax rate OUTPUT2 (20% (VAT on Income)) cannot be used on purchase lines.")]
    [InlineData("", VatTaxDirection.Sales, "No Xero tax type is chosen for this line.")]
    public void FindTaxTypeProblem_ChecksTheCodeAgainstXerosOwnRates(string taxType, VatTaxDirection direction, string? expected)
    {
        XeroTaxRate[] xero =
        [
            new("OUTPUT2", "20% (VAT on Income)", 20m, "ACTIVE", true, false),
            new("INPUT2", "20% (VAT on Expenses)", 20m, "ACTIVE", false, true),
            new("NONE", "No VAT", 0m, "ACTIVE", true, true),
            new("OUTPUT", "17.5% (VAT on Income)", 17.5m, "DELETED", true, false),
            new("RRINPUT", "5% (VAT on Expenses)", 5m, "ARCHIVED", false, true),
        ];

        Assert.Equal(expected, VatRateTaxTypeMapping.FindTaxTypeProblem(taxType, direction, xero));
    }

    private static InvoiceRequestLine Line(VatRate rate, string description = "Engineering time") =>
        new("TimesheetEntry", Guid.NewGuid(), description, 5m, new Money(100m, CurrencyCode.Gbp), new Money(500m, CurrencyCode.Gbp), rate);
}
