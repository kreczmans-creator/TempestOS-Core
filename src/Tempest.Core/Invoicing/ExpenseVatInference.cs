using Tempest.Core.BusinessGovernance;

namespace Tempest.Core.Invoicing;

/// <summary>
/// The one rule for which <see cref="VatRate"/> an expense's receipt shows
/// (`v0.24.0` review n4): an expense records its net and VAT amounts, never
/// a rate, and both its Xero bill (<c>XeroPurchasingMapper</c>) and its
/// recharge line on a client invoice (<see cref="InvoicingService"/>) need
/// one. Shared so the two never disagree for the same receipt.
/// </summary>
public static class ExpenseVatInference
{
    /// <summary>
    /// No VAT recorded is <see cref="VatRate.OutOfScope"/> (Xero's
    /// <c>NONE</c>, "No VAT"); VAT on a zero (or negative) net is
    /// <see cref="VatRate.Standard"/>; otherwise whichever of the reduced
    /// (5%) and standard (20%) rates the VAT is nearer to, a tie going to
    /// standard. VAT that was recorded always maps to a rate that charges
    /// VAT, however odd the ratio — the recorded figures themselves stay on
    /// the expense (and, on a bill, are sent as its <c>TaxAmount</c>).
    /// </summary>
    /// <param name="netAmount">The net amount.</param>
    /// <param name="vatAmount">The VAT amount.</param>
    public static VatRate Infer(decimal netAmount, decimal vatAmount)
    {
        if (vatAmount <= 0m)
            return VatRate.OutOfScope;

        if (netAmount <= 0m)
            return VatRate.Standard;

        var ratio = vatAmount / netAmount;
        return Math.Abs(ratio - VatRate.Reduced.Percentage()) < Math.Abs(ratio - VatRate.Standard.Percentage())
            ? VatRate.Reduced
            : VatRate.Standard;
    }
}
