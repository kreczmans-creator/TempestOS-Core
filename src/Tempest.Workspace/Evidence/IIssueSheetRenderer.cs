namespace Tempest.Workspace.Evidence;

/// <summary>
/// Renders one piece of issued evidence's own issue sheet as a PDF
/// (`ADR-0148`, `WP 18.2B`, Product Owner 2026-09-09: SkiaSharp).
/// </summary>
/// <remarks>
/// The sheet is regenerated from <see cref="IssueSheetModel"/> and never
/// edited: it carries no state of its own between calls, and rendering the
/// same model twice yields byte-identical bytes (a fixed
/// <see cref="IssueSheetModel.GeneratedAtUtc"/> stands in for the clock,
/// so nothing about the render is time-of-day dependent). The PDF is
/// exactly a rendering of the record that produced <see cref="IssueSheetModel"/>,
/// never a second source of truth for it.
/// </remarks>
public interface IIssueSheetRenderer
{
    /// <summary>Renders <paramref name="model"/> as a PDF.</summary>
    ReadOnlyMemory<byte> Render(IssueSheetModel model);
}
