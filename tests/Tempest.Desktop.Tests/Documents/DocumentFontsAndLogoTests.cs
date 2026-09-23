using Tempest.Desktop.Documents;

namespace Tempest.Desktop.Tests.Documents;

/// <summary>
/// The design system's three type families and the horizontal navy lockup,
/// embedded as <c>Tempest.Desktop</c> resources (`WP 21.2A`, closing `WP
/// 20.10G`'s own disclosed gap) — that every resource genuinely loads
/// through <see cref="DocumentFonts"/>/<see cref="DocumentLogo"/>, no
/// running Avalonia application needed (plain <c>[Fact]</c>, not
/// <c>[AvaloniaFact]</c> — proving the point directly, not merely
/// asserting it in a doc comment).
/// </summary>
public sealed class DocumentFontsAndLogoTests
{
    [Fact]
    public void ChakraPetch_LoadsFromTheEmbeddedResource() => Assert.True(DocumentFonts.DisplayLoaded);

    [Fact]
    public void SpaceMono_LoadsFromTheEmbeddedResource() => Assert.True(DocumentFonts.MonoLoaded);

    /// <summary>
    /// `Inter-Variable.ttf` genuinely loads as an <c>SKTypeface</c>
    /// (verifying the resource itself, independent of whether SkiaSharp's
    /// PDF backend can draw extractable text with it — see
    /// <c>DocumentFonts.BodyTypeface</c>'s own remarks for why it is not
    /// used to draw with).
    /// </summary>
    [Fact]
    public void InterVariable_LoadsFromTheEmbeddedResource() => Assert.True(DocumentFonts.InterVariableLoaded);

    [Fact]
    public void HorizontalNavyLockup_DecodesFromTheEmbeddedResource()
    {
        var logo = DocumentLogo.HorizontalNavy;

        Assert.NotNull(logo);
        Assert.True(logo!.Width > 0);
        Assert.True(logo.Height > 0);
    }

    [Theory]
    [InlineData(DocumentFontRole.Display, false)]
    [InlineData(DocumentFontRole.Display, true)]
    [InlineData(DocumentFontRole.Mono, false)]
    [InlineData(DocumentFontRole.Mono, true)]
    [InlineData(DocumentFontRole.Body, false)]
    [InlineData(DocumentFontRole.Body, true)]
    public void For_EveryRoleAndWeight_ReturnsANonNullTypeface(DocumentFontRole role, bool bold) =>
        Assert.NotNull(DocumentFonts.For(role, bold));
}
