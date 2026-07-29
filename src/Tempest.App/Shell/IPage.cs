namespace Tempest.App.Shell;

/// <summary>
/// Something the Shell can render into the Content Region.
/// </summary>
/// <remarks>
/// Deliberately minimal (ADR-0035): a page is exactly one behaviour — render
/// plain text to a writer. No colour, theming, ANSI styling, or interactive
/// control is part of this contract; this is a minimum viable Shell, not a
/// finished user interface.
/// </remarks>
public interface IPage
{
    /// <summary>
    /// Renders this page's content.
    /// </summary>
    /// <param name="writer">The writer to render into.</param>
    void Render(TextWriter writer);
}
