using System.Reflection;
using SkiaSharp;

namespace Tempest.Desktop.Documents;

/// <summary>
/// The three roles a run of document text plays, per the design system's
/// own convention (`docs/design/system/readme.md`: display face for
/// wordmarks and headings, prose face for running text, mono face for
/// "machine data" — a numeric or otherwise right/centre-aligned table
/// column, and the header band's own reference line). Every
/// <see cref="DocumentTemplate.TextRun"/> carries one.
/// </summary>
public enum DocumentFontRole
{
    /// <summary>Running body text — Inter.</summary>
    Body,

    /// <summary>Wordmark, eyebrow labels, headings, table header captions — Chakra Petch.</summary>
    Display,

    /// <summary>Numeric/machine-data readouts, the header band's reference line — Space Mono.</summary>
    Mono,
}

/// <summary>
/// The Tempest Engineering Design System's three type families
/// (`WP 21.2A`, closing `WP 20.10G`'s own disclosed gap — see
/// <see cref="DocumentTemplate"/>'s class remarks), embedded as
/// <c>Tempest.Desktop</c> resources (<c>Documents/Assets/Fonts/*.ttf</c>,
/// `THIRD-PARTY-NOTICES.md`) and loaded once, through
/// <see cref="SKTypeface.FromStream(System.IO.Stream)"/> — no running
/// Avalonia application, no <c>IAssetLoader</c>, so a plain xunit renderer
/// test needs no <c>AvaloniaFact</c>.
/// </summary>
/// <remarks>
/// One representative weight per family — Regular for Inter and Space
/// Mono, Bold for the two roles (<see cref="DocumentFontRole.Display"/>,
/// <see cref="DocumentFontRole.Mono"/>) a document renderer draws bold —
/// mirrors <see cref="DocumentTemplate"/>'s own pre-existing
/// <c>SKPaint.FakeBoldText</c> synthetic-bold convention for every other
/// weight a renderer might ask for (italic, semibold): never faked away
/// entirely, simply not embedded when nothing in this codebase's own
/// document grammar draws it. A load failure (a corrupt resource, an
/// unsupported platform font stack) falls back to
/// <see cref="SKTypeface.Default"/> per family, exactly as
/// <see cref="DocumentTemplate"/> used unconditionally before this Work
/// Package — every renderer keeps working, just without the embedded face,
/// disclosed via <see cref="DisplayLoaded"/>/<see cref="BodyLoaded"/>/<see cref="MonoLoaded"/>.
/// </remarks>
public static class DocumentFonts
{
    private static readonly Lazy<SKTypeface> DisplayRegularTypeface = new(() => Load("ChakraPetch-Regular.ttf"));
    private static readonly Lazy<SKTypeface> DisplayBoldTypeface = new(() => Load("ChakraPetch-Bold.ttf"));
    private static readonly Lazy<SKTypeface> MonoRegularTypeface = new(() => Load("SpaceMono-Regular.ttf"));
    private static readonly Lazy<SKTypeface> MonoBoldTypeface = new(() => Load("SpaceMono-Bold.ttf"));

    /// <summary>
    /// Deliberately still <see cref="SKTypeface.Default"/> — <c>Inter-Variable.ttf</c>
    /// (the only Inter file the design system export carries; there is no
    /// static weight to embed instead) is genuinely embedded and loadable
    /// (see the licensing note in <c>THIRD-PARTY-NOTICES.md</c>), but this
    /// Work Package's own testing found SkiaSharp's PDF backend does not
    /// embed a variable-format <c>SKTypeface</c> as real, extractable PDF
    /// text at all when it is set on the drawing <c>SKPaint</c> — no
    /// <c>Tj</c>/<c>TJ</c> operator for that run appears in the content
    /// stream, and no corresponding font object appears among the PDF's
    /// own embedded fonts, confirmed empirically before this decision (the
    /// same "proven empirically" discipline <c>PdfTextExtractor</c>'s own
    /// remarks already apply to the Default-face case `WP 19.5B`
    /// established). <see cref="DocumentTemplate"/>'s own original
    /// rationale for staying on the platform default face — "a formal,
    /// regenerated document's legibility does not depend on brand type" —
    /// still holds for prose text specifically, so Body renders with it
    /// rather than shipping either invisible-to-search-and-screen-reader
    /// text or an untested static substitute this Work Package's brief
    /// does not name.
    /// </summary>
    private static readonly Lazy<SKTypeface> BodyTypeface = new(() => SKTypeface.Default);

    /// <summary>
    /// <c>Inter-Variable.ttf</c>'s own resource, loaded (never used to
    /// draw — see <see cref="BodyTypeface"/>'s own remarks) purely so
    /// <c>FontResourcesLoadTests</c> can verify the resource itself is
    /// genuinely embedded and genuinely decodes, independent of whether
    /// SkiaSharp's PDF backend can draw text with it.
    /// </summary>
    private static readonly Lazy<SKTypeface> InterVariableTypeface = new(() => Load("Inter-Variable.ttf"));

    /// <summary>Whether Chakra Petch actually loaded from the embedded resource (as opposed to falling back to <see cref="SKTypeface.Default"/>).</summary>
    public static bool DisplayLoaded => !ReferenceEquals(DisplayRegularTypeface.Value, SKTypeface.Default);

    /// <summary>Whether Space Mono actually loaded from the embedded resource.</summary>
    public static bool MonoLoaded => !ReferenceEquals(MonoRegularTypeface.Value, SKTypeface.Default);

    /// <summary>Whether <c>Inter-Variable.ttf</c>'s own resource loaded — see <see cref="InterVariableTypeface"/>'s own remarks for why it is never actually drawn with.</summary>
    public static bool InterVariableLoaded => !ReferenceEquals(InterVariableTypeface.Value, SKTypeface.Default);

    /// <summary>The typeface a <see cref="DocumentTemplate.TextRun"/> should draw with, by its own <see cref="DocumentFontRole"/> and <see cref="DocumentTemplate.TextRun.Bold"/> flag.</summary>
    public static SKTypeface For(DocumentFontRole role, bool bold) => role switch
    {
        DocumentFontRole.Display => bold ? DisplayBoldTypeface.Value : DisplayRegularTypeface.Value,
        DocumentFontRole.Mono => bold ? MonoBoldTypeface.Value : MonoRegularTypeface.Value,
        _ => BodyTypeface.Value,
    };

    /// <summary>
    /// Loads <paramref name="fileName"/> from this assembly's own embedded
    /// resources (<c>Documents/Assets/Fonts/&lt;fileName&gt;</c> at
    /// build time) — matched by suffix against
    /// <see cref="Assembly.GetManifestResourceNames"/> rather than a
    /// hard-coded, fully-qualified resource name, since the exact name
    /// MSBuild's default <c>&lt;EmbeddedResource&gt;</c> generator produces
    /// (root namespace, folder separators as dots) is an implementation
    /// detail this class would rather not depend on verbatim.
    /// </summary>
    private static SKTypeface Load(string fileName)
    {
        var assembly = typeof(DocumentFonts).Assembly;
        var resourceName = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith(fileName, StringComparison.Ordinal));
        if (resourceName is null)
            return SKTypeface.Default;

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return SKTypeface.Default;

        return SKTypeface.FromStream(stream) ?? SKTypeface.Default;
    }
}
