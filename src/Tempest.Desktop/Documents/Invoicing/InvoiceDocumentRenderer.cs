using System.Globalization;
using SkiaSharp;

namespace Tempest.Desktop.Documents.Invoicing;

/// <summary>One line of an <see cref="InvoiceDocumentModel"/>'s own table — every value already resolved and formatted by the caller, the identical "the renderer reads a flat model, draws it" discipline `QuotationSheetLineRow` already established (`WP 21.2A`, scope item 2).</summary>
/// <param name="Description">What the line is.</param>
/// <param name="Quantity">Billable hours, formatted, or <see langword="null"/> for a fixed-price line.</param>
/// <param name="UnitRate">The rate one unit bills at, formatted (currency and amount), or <see langword="null"/> for a fixed-price line.</param>
/// <param name="Amount">The line's own amount, formatted (currency and amount).</param>
public sealed record InvoiceDocumentLineRow(string Description, string? Quantity, string? UnitRate, string Amount);

/// <summary>
/// Everything the invoice document renders (`WP 21.2A`, scope item 2, from
/// an <see cref="Tempest.Core.Invoicing.InvoiceRequest"/>) — an immutable
/// snapshot built once by the caller, the identical discipline
/// <c>QuotationSheetModel</c>/<c>IssueSheetModel</c> already establish. Net
/// only: `WP 21.3B` (VAT) is not in this Work Package's own base — see
/// <see cref="InvoiceDocumentRenderer"/>'s own remarks.
/// </summary>
/// <param name="IssuerName">The consultancy's own principal name.</param>
/// <param name="ProjectCode">The billed project's own business identifier.</param>
/// <param name="ProjectName">The billed project's own display name.</param>
/// <param name="Client">The client this invoice is raised against, resolved to a display name where one exists.</param>
/// <param name="Reference">The invoice request's own reference.</param>
/// <param name="PurchaseOrderReference">The project's own purchase-order reference, where one is recorded. <see langword="null"/> otherwise.</param>
/// <param name="IssueDate">When this invoice was raised — <c>IssuedDate</c> once the connector reports one, else the request's own <c>CreatedAt</c> date.</param>
/// <param name="DueDate">When this invoice falls due (<c>DueOn</c>). <see langword="null"/> while still Draft.</param>
/// <param name="PaymentTermsDisplay">The payment terms this request was raised under, in words (e.g. "30 days").</param>
/// <param name="Currency">The currency every line and <paramref name="Total"/> are stated in.</param>
/// <param name="Lines">Every line, in the order the request carries them.</param>
/// <param name="Total">The invoice's own total, formatted (currency and amount) — net; no VAT line (see this model's own remarks).</param>
/// <param name="Status">The request's own status at the moment this sheet is generated.</param>
/// <param name="GeneratedAtUtc">When this sheet was generated — supplied, never <see cref="DateTime.Now"/>.</param>
/// <param name="ApplicationVersionText">The running application's own version text.</param>
public sealed record InvoiceDocumentModel(
    string IssuerName,
    string ProjectCode,
    string ProjectName,
    string Client,
    string Reference,
    string? PurchaseOrderReference,
    DateOnly IssueDate,
    DateOnly? DueDate,
    string PaymentTermsDisplay,
    string Currency,
    IReadOnlyList<InvoiceDocumentLineRow> Lines,
    string Total,
    string Status,
    DateTimeOffset GeneratedAtUtc,
    string ApplicationVersionText);

/// <summary>
/// Renders an <see cref="InvoiceDocumentModel"/> as an A4 PDF (`WP 21.2A`,
/// scope item 2) — itemised lines, a totals line, payment terms, and a
/// "Payment details" section from Settings → Organisation's own bank
/// fields (added this Work Package) — through <see cref="DocumentTemplate"/>,
/// the identical grammar <c>QuotationSheetRenderer</c>/<c>IssueSheetRenderer</c>
/// already share.
/// </summary>
/// <remarks>
/// <b>Net only, not net/VAT/gross.</b> The brief this Work Package
/// implements against names <c>WP 21.3B</c> as the Work Package that adds
/// <c>VatRate</c>/<c>VatAmount</c> to <c>InvoiceRequestLine</c>, "running
/// tonight — if not in your base, render... net only when it does not [carry
/// VAT fields]." <c>InvoiceRequestLine</c> in this worktree's own base
/// carries neither field (verified directly against the source before
/// writing this renderer), so every line and the total below are net
/// figures — this renderer's own report discloses the same.
/// </remarks>
public sealed class InvoiceDocumentRenderer : IDocumentRenderer<InvoiceDocumentModel>
{
    /// <inheritdoc />
    public string DocumentType => "INVOICE";

    /// <inheritdoc />
    public string TemplateName => "invoice";

    /// <summary>Supplies the organisation identity for a render whose caller passes none — set by the composer to Settings → Organisation, mirroring <c>QuotationSheetRenderer.IdentityProvider</c>.</summary>
    public Func<OrganisationIdentity>? IdentityProvider { get; set; }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Render(InvoiceDocumentModel model, OrganisationIdentity? identity = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        var orgIdentity = identity ?? IdentityProvider?.Invoke() ?? OrganisationIdentity.TempestDefaults;
        var pages = Layout(model, orgIdentity);
        DocumentTemplate.AppendFooters(pages, orgIdentity, FormatFooterDetail(model));

        var metadata = new SKDocumentPdfMetadata
        {
            Title = $"{model.Reference} — Invoice",
            Author = model.IssuerName,
            Subject = "Invoice",
            Creator = model.ApplicationVersionText,
            Producer = model.ApplicationVersionText,
            Creation = model.GeneratedAtUtc.UtcDateTime,
            Modified = model.GeneratedAtUtc.UtcDateTime,
        };

        return DocumentTemplate.RenderPdf(pages, metadata);
    }

    private static List<DocumentTemplate.PagePlan> Layout(InvoiceDocumentModel model, OrganisationIdentity identity)
    {
        var state = DocumentTemplate.BeginLayout();
        using var measure = new SKPaint { Typeface = SKTypeface.Default };

        DocumentTemplate.AddHeaderBand(
            state, measure, "INVOICE",
            $"{model.Reference}  ·  {FormatDate(model.IssueDate)}  ·  {model.Status}");
        DocumentTemplate.AddGap(state, 4f);

        DocumentTemplate.AddWrappedLine(state, measure, model.IssuerName, DocumentTemplate.HeadingSize, bold: true, color: DocumentTemplate.Ink900, fontRole: DocumentFontRole.Display);
        DocumentTemplate.AddWrappedLine(state, measure, $"Project: {model.ProjectCode} — {model.ProjectName}", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddWrappedLine(state, measure, $"Client: {model.Client}", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddWrappedLine(
            state, measure,
            $"Reference: {model.Reference}  •  Date: {FormatDate(model.IssueDate)}  •  Payment terms: {model.PaymentTermsDisplay}"
                + (model.DueDate is { } due ? $"  •  Due: {FormatDate(due)}" : string.Empty),
            DocumentTemplate.BodySize, bold: false);
        if (model.PurchaseOrderReference is { Length: > 0 } po)
            DocumentTemplate.AddWrappedLine(state, measure, $"Purchase order: {po}", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddWrappedLine(state, measure, $"Status: {model.Status}  •  Currency: {model.Currency}", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddGap(state, 4f);
        DocumentTemplate.AddRule(state);

        DocumentTemplate.AddHeading(state, measure, "Lines");
        if (model.Lines.Count == 0)
        {
            DocumentTemplate.AddWrappedLine(state, measure, "No lines recorded.", DocumentTemplate.BodySize, bold: false);
        }
        else
        {
            DocumentTemplate.AddTable(
                state, measure,
                headers: ["Description", "Quantity", "Rate", "Amount"],
                widths: [state.ContentWidth * 0.52f, state.ContentWidth * 0.12f, state.ContentWidth * 0.18f, state.ContentWidth * 0.18f],
                rows: [.. model.Lines.Select(l => new[] { l.Description, l.Quantity ?? "—", l.UnitRate ?? "—", l.Amount })],
                columnAligns: [SKTextAlign.Left, SKTextAlign.Right, SKTextAlign.Right, SKTextAlign.Right]);
        }
        DocumentTemplate.AddGap(state, 4f);

        // `WP 21.2A`: net only — this model's own remarks explain why.
        DocumentTemplate.AddWrappedLine(state, measure, $"Total (net) {model.Total}", DocumentTemplate.HeadingSize, bold: true, SKTextAlign.Right, DocumentTemplate.Ink900, DocumentFontRole.Display);
        DocumentTemplate.AddGap(state, 10f);

        DocumentTemplate.AddHeading(state, measure, "Payment terms");
        DocumentTemplate.AddWrappedLine(
            state, measure,
            model.DueDate is { } dueTerms
                ? $"{model.PaymentTermsDisplay} — due {FormatDate(dueTerms)}."
                : $"{model.PaymentTermsDisplay}.",
            DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddGap(state, 8f);

        DocumentTemplate.AddHeading(state, measure, "Payment details");
        if (identity.HasBankDetails)
        {
            if (identity.BankAccountName is { Length: > 0 } accountName)
                DocumentTemplate.AddWrappedLine(state, measure, $"Account name: {accountName}", DocumentTemplate.BodySize, bold: false);
            if (identity.BankSortCode is { Length: > 0 } sortCode)
                DocumentTemplate.AddWrappedLine(state, measure, $"Sort code: {sortCode}", DocumentTemplate.BodySize, bold: false, fontRole: DocumentFontRole.Mono);
            if (identity.BankAccountNumber is { Length: > 0 } accountNumber)
                DocumentTemplate.AddWrappedLine(state, measure, $"Account number: {accountNumber}", DocumentTemplate.BodySize, bold: false, fontRole: DocumentFontRole.Mono);
            if (identity.BankIban is { Length: > 0 } iban)
                DocumentTemplate.AddWrappedLine(state, measure, $"IBAN: {iban}", DocumentTemplate.BodySize, bold: false, fontRole: DocumentFontRole.Mono);
        }
        else
        {
            DocumentTemplate.AddWrappedLine(state, measure, "No bank details recorded — set them in Settings → Organisation.", DocumentTemplate.BodySize, bold: false);
        }

        return state.Pages;
    }

    private static string FormatFooterDetail(InvoiceDocumentModel model) => string.Format(
        CultureInfo.InvariantCulture,
        "{0} · {1} · Generated {2:yyyy-MM-dd HH:mm} UTC",
        model.ApplicationVersionText, model.Reference, model.GeneratedAtUtc.UtcDateTime);

    private static string FormatDate(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
