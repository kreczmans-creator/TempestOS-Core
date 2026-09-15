using System.Globalization;
using SkiaSharp;

namespace Tempest.Desktop.Documents.DrawingRegisters;

/// <summary>One row of a <see cref="DrawingRegisterDocumentModel"/>'s own register.</summary>
/// <param name="Number">The document's own business identifier, where it has one.</param>
/// <param name="Title">The document's own title.</param>
/// <param name="Revision">The document's own current revision number.</param>
/// <param name="Status">Where the document stands in its own lifecycle.</param>
/// <param name="IssueHistory">Every revision's own date, newest first, formatted — the document's own "issue history".</param>
public sealed record DrawingRegisterRow(string Number, string Title, string Revision, string Status, string IssueHistory);

/// <summary>
/// Everything the drawing register document renders (`WP 21.2A`, scope item
/// 2, from a project's own Documents area) — number, title, revision,
/// status, issue history, one row per document — the design system's own
/// <c>DrawingRegister.dc.html</c> grammar: A4 landscape.
/// </summary>
/// <param name="ProjectCode">The register's own project business identifier.</param>
/// <param name="ProjectName">The register's own project display name.</param>
/// <param name="Rows">Every document in the project, in the order the caller resolved them.</param>
/// <param name="GeneratedAtUtc">When this sheet was generated — supplied, never <see cref="DateTime.Now"/>.</param>
/// <param name="ApplicationVersionText">The running application's own version text.</param>
public sealed record DrawingRegisterDocumentModel(
    string ProjectCode,
    string ProjectName,
    IReadOnlyList<DrawingRegisterRow> Rows,
    DateTimeOffset GeneratedAtUtc,
    string ApplicationVersionText);

/// <summary>Renders a <see cref="DrawingRegisterDocumentModel"/> as an A4 <b>landscape</b> PDF (`WP 21.2A`, scope item 2) — through <see cref="DocumentTemplate"/>'s own landscape geometry (<see cref="DocumentTemplate.LayoutState.Landscape"/>), added this Work Package specifically for this renderer and the progress report.</summary>
public sealed class DrawingRegisterDocumentRenderer : IDocumentRenderer<DrawingRegisterDocumentModel>
{
    /// <inheritdoc />
    public string DocumentType => "DRAWING REGISTER";

    /// <inheritdoc />
    public string TemplateName => "drawing-register";

    /// <summary>Supplies the organisation identity for a render whose caller passes none.</summary>
    public Func<OrganisationIdentity>? IdentityProvider { get; set; }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Render(DrawingRegisterDocumentModel model, OrganisationIdentity? identity = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        var orgIdentity = identity ?? IdentityProvider?.Invoke() ?? OrganisationIdentity.TempestDefaults;
        var pages = Layout(model);
        DocumentTemplate.AppendFooters(pages, orgIdentity, FormatFooterDetail(model), landscape: true);

        var metadata = new SKDocumentPdfMetadata
        {
            Title = $"{model.ProjectCode} — Drawing Register",
            Author = model.ProjectName,
            Subject = "Drawing register",
            Creator = model.ApplicationVersionText,
            Producer = model.ApplicationVersionText,
            Creation = model.GeneratedAtUtc.UtcDateTime,
            Modified = model.GeneratedAtUtc.UtcDateTime,
        };

        return DocumentTemplate.RenderPdf(pages, metadata, landscape: true);
    }

    private static List<DocumentTemplate.PagePlan> Layout(DrawingRegisterDocumentModel model)
    {
        var state = DocumentTemplate.BeginLayout(landscape: true);
        using var measure = new SKPaint { Typeface = SKTypeface.Default };

        DocumentTemplate.AddHeaderBand(state, measure, "DRAWING REGISTER", $"{model.ProjectCode}  ·  {model.Rows.Count} document(s)");
        DocumentTemplate.AddGap(state, 4f);

        DocumentTemplate.AddWrappedLine(state, measure, $"Project: {model.ProjectCode} — {model.ProjectName}", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddGap(state, 4f);
        DocumentTemplate.AddRule(state);

        if (model.Rows.Count == 0)
        {
            DocumentTemplate.AddWrappedLine(state, measure, "No documents or drawings recorded in this project.", DocumentTemplate.BodySize, bold: false);
        }
        else
        {
            DocumentTemplate.AddTable(
                state, measure,
                headers: ["Number", "Title", "Revision", "Status", "Issue history"],
                widths: [state.ContentWidth * 0.14f, state.ContentWidth * 0.36f, state.ContentWidth * 0.10f, state.ContentWidth * 0.14f, state.ContentWidth * 0.26f],
                rows: [.. model.Rows.Select(r => new[] { r.Number, r.Title, r.Revision, r.Status, r.IssueHistory })],
                columnAligns: [SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Center, SKTextAlign.Left, SKTextAlign.Left]);
        }

        return state.Pages;
    }

    private static string FormatFooterDetail(DrawingRegisterDocumentModel model) => string.Format(
        CultureInfo.InvariantCulture,
        "{0} · {1} · Generated {2:yyyy-MM-dd HH:mm} UTC",
        model.ApplicationVersionText, model.ProjectCode, model.GeneratedAtUtc.UtcDateTime);
}
