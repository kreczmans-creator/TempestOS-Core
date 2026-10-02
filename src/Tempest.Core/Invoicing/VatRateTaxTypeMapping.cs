using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing.Xero.Settings;

namespace Tempest.Core.Invoicing;

/// <summary>
/// Which side of the ledger a line sits on (`v0.24.0` X1): Xero keeps an
/// "output" tax type for sales and an "input" one for purchases, and a tax
/// rate says which it may be applied to (<c>CanApplyToRevenue</c>,
/// <c>CanApplyToExpenses</c>).
/// </summary>
public enum VatTaxDirection
{
    /// <summary>A sales line: a quote or a sales invoice (<c>ACCREC</c>) — the output tax types.</summary>
    Sales,

    /// <summary>A purchase line: a bill (<c>ACCPAY</c>) or a purchase order — the input tax types.</summary>
    Purchases,
}

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
/// <para>
/// <b>`v0.24.0` X1 (D6): the input side, and Xero's own list.</b> Purchases
/// (bills, purchase orders) map to Xero's UK input tax types —
/// <c>INPUT2</c>, <c>RRINPUT</c>, <c>ZERORATEDINPUT</c>,
/// <c>EXEMPTINPUT</c>, <c>NONE</c> — through <see cref="TryMap(VatRate, VatTaxDirection, out string?)"/>.
/// These are only the defaults: Xero is the source of truth for tax rates,
/// so before a line is pushed the code is checked against the last reading
/// of Xero's own <c>GET TaxRates</c> (<see cref="FindTaxTypeProblem"/>): it
/// must exist, be <c>ACTIVE</c>, and apply to the line's side — otherwise
/// the push is Blocked with the reason, and a line can never carry a code
/// Xero lacks. A non-UK organisation chooses its own code per rate in
/// Settings (<c>Tempest.Core.Invoicing.Xero.Settings.XeroTaxTypeResolver</c>).
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

    private static readonly IReadOnlyDictionary<VatRate, string> XeroInput = new Dictionary<VatRate, string>
    {
        [VatRate.Standard] = "INPUT2",
        [VatRate.Reduced] = "RRINPUT",
        [VatRate.Zero] = "ZERORATEDINPUT",
        [VatRate.Exempt] = "EXEMPTINPUT",
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
    /// Maps <paramref name="rate"/> to Xero's default tax type for
    /// <paramref name="direction"/>: the output type for
    /// <see cref="VatTaxDirection.Sales"/> (identical to
    /// <see cref="TryMap(VatRate, out string?)"/>), the input type for
    /// <see cref="VatTaxDirection.Purchases"/>. <see langword="false"/> (with
    /// <paramref name="taxType"/> <see langword="null"/>) for a rate or a
    /// direction this table carries no row for.
    /// </summary>
    /// <param name="rate">The line's VAT rate.</param>
    /// <param name="direction">Whether the line is a sale or a purchase.</param>
    /// <param name="taxType">The mapped Xero tax type.</param>
    public static bool TryMap(VatRate rate, VatTaxDirection direction, out string? taxType)
    {
        switch (direction)
        {
            case VatTaxDirection.Sales:
                return Xero.TryGetValue(rate, out taxType);
            case VatTaxDirection.Purchases:
                return XeroInput.TryGetValue(rate, out taxType);
            default:
                taxType = null;
                return false;
        }
    }

    /// <summary>
    /// Why Xero cannot take <paramref name="taxType"/> on a
    /// <paramref name="direction"/> line, judged against
    /// <paramref name="available"/> (the last reading of Xero's own
    /// <c>GET TaxRates</c>); <see langword="null"/> when it can. The code
    /// must exist (compared ignoring case, as Xero does), be <c>ACTIVE</c>,
    /// and be applicable to the side — <c>CanApplyToRevenue</c> for sales,
    /// <c>CanApplyToExpenses</c> for purchases.
    /// </summary>
    /// <param name="taxType">The tax type a line would carry.</param>
    /// <param name="direction">Whether the line is a sale or a purchase.</param>
    /// <param name="available">Every tax rate Xero holds, every status.</param>
    /// <returns>The Blocked reason (<c>"Xero has no active tax rate {code}."</c>, or that it cannot apply to the side), or <see langword="null"/>.</returns>
    public static string? FindTaxTypeProblem(string? taxType, VatTaxDirection direction, IEnumerable<XeroTaxRate> available)
    {
        ArgumentNullException.ThrowIfNull(available);

        if (string.IsNullOrWhiteSpace(taxType))
            return "No Xero tax type is chosen for this line.";

        var rate = available.FirstOrDefault(r => string.Equals(r.TaxType, taxType, StringComparison.OrdinalIgnoreCase));
        if (rate is null || !string.Equals(rate.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            return NoActiveTaxRateReason(taxType);

        var applies = direction == VatTaxDirection.Sales ? rate.CanApplyToRevenue : rate.CanApplyToExpenses;
        return applies
            ? null
            : $"Xero's tax rate {rate.TaxType} ({rate.Name}) cannot be used on {(direction == VatTaxDirection.Sales ? "sales" : "purchase")} lines.";
    }

    /// <summary>The Blocked reason for a tax type Xero does not hold as an active rate (design §8): <c>"Xero has no active tax rate {code}."</c>.</summary>
    /// <param name="taxType">The tax type.</param>
    public static string NoActiveTaxRateReason(string taxType) => $"Xero has no active tax rate {taxType}.";

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
