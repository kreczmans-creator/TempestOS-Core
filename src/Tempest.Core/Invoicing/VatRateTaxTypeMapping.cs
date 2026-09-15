using Tempest.Core.BusinessGovernance;

namespace Tempest.Core.Invoicing;

/// <summary>
/// Maps this platform's own five-value <see cref="VatRate"/> vocabulary to
/// the tax type an accounting connector's own line item expects (`WP
/// 21.3B`). <see cref="FakeInvoicingConnector"/> and
/// <c>Tempest.Core.Invoicing.Xero.XeroConnector</c> both consult this table
/// before building a line — never converting or inventing a value, only
/// mapping or refusing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Xero's own vocabulary, not this platform's invention.</b> Xero
/// distinguishes an "output" (sales) tax type from an "input" (purchase)
/// one; every value here is the output side, since <see cref="IInvoicingConnector.CreateDraftInvoiceAsync"/>
/// only ever raises an outbound, sales invoice. <c>OUTPUT2</c> (20%),
/// <c>RROUTPUT</c> (5% reduced rate) and <c>ZERORATEDOUTPUT</c> (0%, still
/// within the scope of VAT) are Xero's own UK organisation tax types;
/// <c>EXEMPTOUTPUT</c> is Xero's own "exempt income" type; <c>NONE</c> —
/// Xero's own "No VAT" type, excluded from the VAT return entirely — is
/// the closest honest match for <see cref="VatRate.OutOfScope"/> that
/// Xero's own vocabulary actually offers: not a perfect legal synonym (Xero
/// draws no "out of scope of VAT" category of its own), disclosed here
/// rather than asserted as an exact equivalence.
/// </para>
/// <para>
/// <b>Every one of the five declared <see cref="VatRate"/> values maps.</b>
/// <see cref="TryMap"/> can still answer <see langword="false"/> — for a
/// value outside the enum's own declared range (a corrupted stored value,
/// or a future rate added to <see cref="VatRate"/> without a matching row
/// added here) — so that "refuse a send whose rate the connector cannot
/// express, with the reason" (`WP 21.3B`'s own brief) is real, tested
/// behaviour and not a branch no input can ever reach.
/// </para>
/// </remarks>
public static class VatRateTaxTypeMapping
{
    private static readonly IReadOnlyDictionary<VatRate, string> Xero = new Dictionary<VatRate, string>
    {
        [VatRate.Standard] = "OUTPUT2",
        [VatRate.Reduced] = "RROUTPUT",
        [VatRate.Zero] = "ZERORATEDOUTPUT",
        [VatRate.Exempt] = "EXEMPTOUTPUT",
        [VatRate.OutOfScope] = "NONE",
    };

    /// <summary>
    /// Maps <paramref name="rate"/> to the Xero tax type
    /// <paramref name="taxType"/> — <see langword="true"/> and the mapped
    /// value for every declared <see cref="VatRate"/>; <see langword="false"/>
    /// (with <paramref name="taxType"/> <see langword="null"/>) for a value
    /// this table carries no row for.
    /// </summary>
    public static bool TryMap(VatRate rate, out string? taxType) => Xero.TryGetValue(rate, out taxType);

    /// <summary>
    /// The refusal reason <see cref="IInvoicingConnector.CreateDraftInvoiceAsync"/>
    /// answers with when <paramref name="line"/>'s own <see cref="VatRate"/>
    /// cannot be mapped — shared wording so <see cref="FakeInvoicingConnector"/>
    /// and <c>XeroConnector</c> refuse identically.
    /// </summary>
    public static string UnmappableReason(InvoiceRequestLine line) =>
        $"Line '{line.Description}' carries VAT rate '{line.VatRate}', which this connector has no tax type for.";

    /// <summary>
    /// The first line in <paramref name="lines"/> whose own <see cref="VatRate"/>
    /// this table cannot map, as a ready-to-return refusal reason; <see langword="null"/>
    /// when every line maps.
    /// </summary>
    public static string? FindUnmappableReason(IEnumerable<InvoiceRequestLine> lines)
    {
        foreach (var line in lines)
        {
            if (!TryMap(line.VatRate, out _))
                return UnmappableReason(line);
        }

        return null;
    }
}
