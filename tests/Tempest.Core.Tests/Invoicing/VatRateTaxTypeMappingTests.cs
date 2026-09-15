using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing;

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

    private static InvoiceRequestLine Line(VatRate rate, string description = "Engineering time") =>
        new("TimesheetEntry", Guid.NewGuid(), description, 5m, new Money(100m, CurrencyCode.Gbp), new Money(500m, CurrencyCode.Gbp), rate);
}
