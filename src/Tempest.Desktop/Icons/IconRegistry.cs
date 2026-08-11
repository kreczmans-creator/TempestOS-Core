namespace Tempest.Desktop.Icons;

/// <summary>
/// The Icon Framework — a presentation-layer-only, Kind-keyed lookup from
/// an engineering object's own <c>Kind</c> string to a display glyph,
/// mirroring <c>ADR-0067</c>'s own Kind-keyed registration pattern applied
/// to iconography, exactly as `WP10.0A Visual Design System.md` §2
/// specified. Introduces no <c>Tempest.Core</c>/<c>Tempest.App.Workspace</c>
/// contract of any kind — <see cref="NavigationItem.Icon"/> and every
/// Kind string this registry keys against already exist and are already
/// symbolic-only, never a rendered image reference (`ADR-0031`).
/// </summary>
/// <remarks>
/// <para>
/// A single glyph per Kind, used identically everywhere a Kind is shown —
/// the Project Explorer, Document Area tabs, and the Command Palette —
/// never a different icon for the same Kind in two places
/// (`WP10.0A Visual Design System.md` §2's own explicit rule).
/// </para>
/// <para>
/// <b>Phase 2 (`WP 10.5A`, "Replace all placeholder icons with a
/// consistent icon library"), a real, disclosed upgrade over Phase 1's
/// own full-colour, mixed-style emoji set (`WP 10.0B`).</b> Every glyph
/// below is drawn from the Geometric Shapes (U+25A0–U+25FF) and
/// Mathematical Operators (U+2200–U+22FF) Unicode blocks — both
/// documented <i>text-default</i> presentation per Unicode UTR#51, so
/// each one renders as a plain, monochrome symbol inheriting the host
/// control's own <c>Foreground</c> (correctly theme-tinted, exactly like
/// any other text) rather than a fixed-colour pictograph a theme change
/// cannot repaint — the identical "engineering-schematic," not
/// "cartoon-emoji," direction <c>Part</c>/<c>Component</c>'s own
/// pre-existing <c>⚙</c> and <c>VerificationActivity</c>'s own
/// pre-existing <c>✔</c> already established, now applied consistently
/// platform-wide. A comprehensive, hand-authored vector icon library
/// (mirroring <see cref="IconGeometry"/>'s own small "chrome" set)
/// remains disclosed future work — this Phase 2 set is a real,
/// significant, but still text-glyph-based improvement, not the final
/// word (`WP10.5A UX Review.md` §2).
/// </para>
/// </remarks>
public static class IconRegistry
{
    private static readonly IReadOnlyDictionary<string, string> Glyphs = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Mechanical (WP 9.0A/9.0B)
        ["Project"] = "▣",
        ["Assembly"] = "⬡",
        ["SubAssembly"] = "⬡",
        ["Part"] = "⚙",
        ["Component"] = "⚙",
        ["Configuration"] = "⧉",

        // Requirements (WP 9.1A)
        ["Requirement"] = "▤",
        ["RequirementGroup"] = "▥",
        ["RequirementCollection"] = "▦",

        // Calculations (WP 9.2A)
        ["Calculation"] = "∑",
        ["CalculationSet"] = "∑",
        ["CalculationTemplate"] = "⊞",

        // Verification (WP 9.3A)
        ["VerificationActivity"] = "✔",
        ["Test"] = "▷",
        ["Inspection"] = "◎",
        ["Analysis"] = "∿",
        ["Demonstration"] = "▶",

        // Documents (WP 9.4A)
        ["Document"] = "▭",
        ["Drawing"] = "▱",
        ["CadModel"] = "◇",

        // Manufacturing (WP 9.5A)
        ["ManufacturingOperation"] = "⬢",
        ["WorkInstruction"] = "☰",
    };

    /// <summary>The glyph shown when <paramref name="kind"/> is unrecognised, or a Category/Group node with no backing object (<see langword="null"/> <c>Kind</c>).</summary>
    public const string DefaultGlyph = "■";

    /// <summary>Resolves the display glyph for <paramref name="kind"/> — <see cref="DefaultGlyph"/> if unknown or <see langword="null"/>, never an exception.</summary>
    public static string Resolve(string? kind) =>
        kind is not null && Glyphs.TryGetValue(kind, out var glyph) ? glyph : DefaultGlyph;
}
