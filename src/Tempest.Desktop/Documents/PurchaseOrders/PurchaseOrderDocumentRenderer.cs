using System.Globalization;
using SkiaSharp;

namespace Tempest.Desktop.Documents.PurchaseOrders;

/// <summary>One line of a <see cref="PurchaseOrderDocumentModel"/>'s own table.</summary>
/// <param name="Description">What is being ordered.</param>
/// <param name="Quantity">How many, formatted.</param>
/// <param name="UnitRate">The rate one unit costs, formatted (currency and amount).</param>
/// <param name="Amount">The line's own amount, formatted (currency and amount).</param>
public sealed record PurchaseOrderDocumentLineRow(string Description, string Quantity, string UnitRate, string Amount);

/// <summary>
/// Everything the purchase order document renders (`WP 21.2A`, scope item
/// 2) — a model-less renderer against a fixture, per this Work Package's
/// own brief and kill switch: <c>WP 21.3B</c>, which would add a real
/// <c>PurchaseOrder</c> Kind, is a separate branch (<c>wp/21.3B</c>) not
/// merged into this worktree's own base at the time of writing (verified:
/// the only <c>PurchaseOrder</c> class in this tree lives under
/// <c>src/Frozen/</c>, out of scope per <c>brief-common.md</c>'s own "ignore
/// <c>src/Frozen/**</c> entirely"). This renderer, its template mapping and
/// its golden-text test are real and exercised; there is no live "Export
/// PO" button anywhere in the shell, disclosed in this Work Package's own
/// report rather than wired against a model or a UI row that do not exist.
/// </summary>
/// <param name="IssuerName">The consultancy's own principal name.</param>
/// <param name="ProjectCode">The ordering project's own business identifier.</param>
/// <param name="ProjectName">The ordering project's own display name.</param>
/// <param name="Reference">The purchase order's own reference.</param>
/// <param name="Date">When this order was raised.</param>
/// <param name="SupplierName">Who the order is placed with.</param>
/// <param name="DeliveryAddress">Where the goods/services are to be delivered. <see langword="null"/> when none is recorded.</param>
/// <param name="Currency">The currency every line and <paramref name="Total"/> are stated in.</param>
/// <param name="Lines">Every line, in the order the order carries them.</param>
/// <param name="Total">The order's own total, formatted (currency and amount).</param>
/// <param name="Conditions">Free-text purchase-order conditions. <see langword="null"/> when none are recorded.</param>
/// <param name="Status">The order's own status at the moment this sheet is generated.</param>
/// <param name="GeneratedAtUtc">When this sheet was generated — supplied, never <see cref="DateTime.Now"/>.</param>
/// <param name="ApplicationVersionText">The running application's own version text.</param>
public sealed record PurchaseOrderDocumentModel(
    string IssuerName,
    string ProjectCode,
    string ProjectName,
    string Reference,
    DateOnly Date,
    string SupplierName,
    string? DeliveryAddress,
    string Currency,
    IReadOnlyList<PurchaseOrderDocumentLineRow> Lines,
    string Total,
    string? Conditions,
    string Status,
    DateTimeOffset GeneratedAtUtc,
    string ApplicationVersionText);

/// <summary>Renders a <see cref="PurchaseOrderDocumentModel"/> as an A4 PDF (`WP 21.2A`, scope item 2) — supplier and delivery details, itemised lines, totals and conditions — through <see cref="DocumentTemplate"/>.</summary>
public sealed class PurchaseOrderDocumentRenderer : IDocumentRenderer<PurchaseOrderDocumentModel>
{
    /// <inheritdoc />
    public string DocumentType => "PURCHASE ORDER";

    /// <inheritdoc />
    public string TemplateName => "purchase-order";

    /// <summary>Supplies the organisation identity for a render whose caller passes none.</summary>
    public Func<OrganisationIdentity>? IdentityProvider { get; set; }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Render(PurchaseOrderDocumentModel model, OrganisationIdentity? identity = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        var orgIdentity = identity ?? IdentityProvider?.Invoke() ?? OrganisationIdentity.TempestDefaults;
        var pages = Layout(model);
        DocumentTemplate.AppendFooters(pages, orgIdentity, FormatFooterDetail(model));

        var metadata = new SKDocumentPdfMetadata
        {
            Title = $"{model.Reference} — Purchase Order",
            Author = model.IssuerName,
            Subject = "Purchase order",
            Creator = model.ApplicationVersionText,
            Producer = model.ApplicationVersionText,
            Creation = model.GeneratedAtUtc.UtcDateTime,
            Modified = model.GeneratedAtUtc.UtcDateTime,
        };

        return DocumentTemplate.RenderPdf(pages, metadata);
    }

    private static List<DocumentTemplate.PagePlan> Layout(PurchaseOrderDocumentModel model)
    {
        var state = DocumentTemplate.BeginLayout();
        using var measure = new SKPaint { Typeface = SKTypeface.Default };

        DocumentTemplate.AddHeaderBand(
            state, measure, "PURCHASE ORDER",
            $"{model.Reference}  ·  {FormatDate(model.Date)}  ·  {model.Status}");
        DocumentTemplate.AddGap(state, 4f);

        DocumentTemplate.AddWrappedLine(state, measure, model.IssuerName, DocumentTemplate.HeadingSize, bold: true, color: DocumentTemplate.Ink900, fontRole: DocumentFontRole.Display);
        DocumentTemplate.AddWrappedLine(state, measure, $"Project: {model.ProjectCode} — {model.ProjectName}", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddWrappedLine(state, measure, $"Supplier: {model.SupplierName}", DocumentTemplate.BodySize, bold: false);
        if (model.DeliveryAddress is { Length: > 0 } delivery)
            DocumentTemplate.AddWrappedLine(state, measure, $"Deliver to: {delivery}", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddWrappedLine(
            state, measure, $"Reference: {model.Reference}  •  Date: {FormatDate(model.Date)}  •  Status: {model.Status}  •  Currency: {model.Currency}",
            DocumentTemplate.BodySize, bold: false);
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
                rows: [.. model.Lines.Select(l => new[] { l.Description, l.Quantity, l.UnitRate, l.Amount })],
                columnAligns: [SKTextAlign.Left, SKTextAlign.Right, SKTextAlign.Right, SKTextAlign.Right]);
        }
        DocumentTemplate.AddGap(state, 4f);

        DocumentTemplate.AddWrappedLine(state, measure, $"Total {model.Total}", DocumentTemplate.HeadingSize, bold: true, SKTextAlign.Right, DocumentTemplate.Ink900, DocumentFontRole.Display);
        DocumentTemplate.AddGap(state, 10f);

        DocumentTemplate.AddHeading(state, measure, "Conditions");
        DocumentTemplate.AddWrappedLine(
            state, measure, string.IsNullOrWhiteSpace(model.Conditions) ? "No conditions recorded." : model.Conditions, DocumentTemplate.BodySize, bold: false);

        return state.Pages;
    }

    private static string FormatFooterDetail(PurchaseOrderDocumentModel model) => string.Format(
        CultureInfo.InvariantCulture,
        "{0} · {1} · Generated {2:yyyy-MM-dd HH:mm} UTC",
        model.ApplicationVersionText, model.Reference, model.GeneratedAtUtc.UtcDateTime);

    private static string FormatDate(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
