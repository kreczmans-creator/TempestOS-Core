namespace Tempest.Desktop.Documents;

/// <summary>
/// One small contract every document renderer satisfies (`WP 21.2A`,
/// scope item 1) — the quotation sheet and the issue sheet
/// (`QuotationSheetRenderer`/`IssueSheetRenderer`, `WP 20.10G`) alongside
/// this Work Package's own six new renderers — so a caller (this Work
/// Package's own <see cref="DocumentExporter"/>, and, per the brief,
/// `WP 21.2B`'s own traces and later reports) can render any of them the
/// same way: read the model, call <see cref="Render"/>, save the bytes.
/// </summary>
/// <typeparam name="TModel">The flat, already-resolved model this renderer draws — never the live domain object itself, mirroring every renderer this codebase already has (`QuotationSheetModel`, `IssueSheetModel`).</typeparam>
public interface IDocumentRenderer<in TModel>
{
    /// <summary>The document's own type, upper case, exactly as it is drawn into the header band's eyebrow (e.g. <c>"INVOICE"</c>, <c>"QUOTATION"</c>).</summary>
    string DocumentType { get; }

    /// <summary>
    /// This renderer's own mapped template folder name under
    /// <c>docs/design/templates/</c> (e.g. <c>"invoice"</c>,
    /// <c>"cost-estimate"</c>) — what <see cref="DocumentExporter"/> uses
    /// to name the exported file (<c>&lt;reference&gt;-&lt;TemplateName&gt;.pdf</c>)
    /// and what <c>DesignSystemTemplateFidelityTests</c> checks really
    /// exists on disk.
    /// </summary>
    string TemplateName { get; }

    /// <summary>Renders <paramref name="model"/> as a PDF, over <paramref name="identity"/>'s own footer identity — a renderer's own <c>IdentityProvider</c> (Settings → Organisation, threaded at composition) when the caller supplies none.</summary>
    ReadOnlyMemory<byte> Render(TModel model, OrganisationIdentity? identity = null);
}
