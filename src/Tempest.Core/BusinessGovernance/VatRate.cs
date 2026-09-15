namespace Tempest.Core.BusinessGovernance;

/// <summary>
/// A closed vocabulary of VAT treatments a <c>Tempest.Core.Quotations.QuotationLine</c>
/// or <c>Tempest.Core.Invoicing.InvoiceRequestLine</c> carries (`WP 21.3B`).
/// Not a tax engine: TempestOS computes no liability and posts nothing to
/// Xero or any other accounts system — this is the figure the invoice
/// request already sends, and what the consultant later matches in Xero
/// (Product Owner guard, "Not ERP, not PLM"). The consultant's own default
/// for a new line is picked in Settings → Organisation identity; this
/// enum's own default — <see cref="OutOfScope"/>, first in declaration
/// order — is deliberately the value a line with no stored rate at all
/// reads back as, so that every line recorded before this Work Package
/// changes value by exactly nothing.
/// </summary>
public enum VatRate
{
    /// <summary>
    /// No VAT rate applies at all — outside the scope of VAT entirely, not
    /// the same legal category as <see cref="Exempt"/> or
    /// <see cref="Zero"/>, and the value <see cref="OutOfScope"/> is because
    /// it is the type's own default (declared first): a line persisted
    /// before this Work Package, which carries no <see cref="VatRate"/> at
    /// all in its stored state, reads back as this and not as some other,
    /// invented rate.
    /// </summary>
    OutOfScope,

    /// <summary>The standard rate — 20%.</summary>
    Standard,

    /// <summary>The reduced rate — 5%.</summary>
    Reduced,

    /// <summary>Zero-rated — 0%, but still within the scope of VAT (unlike <see cref="OutOfScope"/>).</summary>
    Zero,

    /// <summary>Exempt from VAT — 0%, a different legal category again from <see cref="Zero"/> or <see cref="OutOfScope"/>.</summary>
    Exempt,
}

/// <summary>Reads <see cref="VatRate"/> as the figures a line's own VAT amount and a screen actually need.</summary>
public static class VatRateExtensions
{
    /// <summary>The fraction of the net amount <paramref name="rate"/> charges — <c>0.20</c> for <see cref="VatRate.Standard"/>, <c>0.05</c> for <see cref="VatRate.Reduced"/>, <c>0</c> for every other value.</summary>
    public static decimal Percentage(this VatRate rate) => rate switch
    {
        VatRate.Standard => 0.20m,
        VatRate.Reduced => 0.05m,
        VatRate.Zero => 0m,
        VatRate.Exempt => 0m,
        VatRate.OutOfScope => 0m,
        _ => 0m,
    };

    /// <summary>The words a line's own VAT rate drop-down and every screen showing a rate use.</summary>
    public static string DisplayName(this VatRate rate) => rate switch
    {
        VatRate.Standard => "Standard (20%)",
        VatRate.Reduced => "Reduced (5%)",
        VatRate.Zero => "Zero-rated",
        VatRate.Exempt => "Exempt",
        VatRate.OutOfScope => "Out of scope",
        _ => rate.ToString(),
    };
}
