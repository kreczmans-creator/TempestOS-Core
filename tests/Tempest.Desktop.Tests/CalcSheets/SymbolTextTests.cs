using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Tempest.Desktop.CalcSheets;
using Tempest.Desktop.Documents;
using SkiaSharp;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>The owner's ask (2026-10-24): symbols read as an engineer writes them, D_1 as D with a subscript 1.</summary>
public sealed class SymbolTextTests
{
    private static string Show(string text) => string.Concat(SymbolText.Parse(text).Select(s => s.Subscript ? $"[{s.Text}]" : s.Text));

    [Theory]
    [InlineData("D_1", "D[1]")]
    [InlineData("F_M,min = F_M/α_A", "F[M,min] = F[M]/α[A]")]
    [InlineData("u_clamp = F_KR_req / F_KR", "u[clamp] = F[KR,req] / F[KR]")]
    [InlineData("sigma_z_max", "σ[z,max]")]
    [InlineData("Phi = n k_S/(k_S + k_P)", "Φ = n k[S]/(k[S] + k[P])")]
    [InlineData("ρ = (2V_Ed/V_pl,Rd − 1)²", "ρ = (2V[Ed]/V[pl,Rd] − 1)²")]
    [InlineData("h = 1.006 t + W(2501 + 1.86 t) kJ/kg_da", "h = 1.006 t + W(2501 + 1.86 t) kJ/kg[da]")]
    [InlineData("ΔT_tot = P·R_tot", "ΔT[tot] = P·R[tot]")]
    [InlineData("R_p0.2 = 640 MPa. Then F_A.", "R[p0.2] = 640 MPa. Then F[A].")]
    [InlineData("F = √(F_∥² + F_⊥² + F_n²)", "F = √(F[∥]² + F[⊥]² + F[n]²)")] // the fillet weld's forces (shots review)
    [InlineData("√(σ_⊥² + 3(τ_⊥² + τ_∥²))", "√(σ[⊥]² + 3(τ[⊥]² + τ[∥]²))")]
    public void A_Subscript_Follows_The_First_Underscore_And_A_Second_Reads_As_A_Comma(string text, string expected) =>
        Assert.Equal(expected, Show(text));

    [Theory]
    [InlineData("L = 4 m")]
    [InlineData("mech.bolted_joint@1.1.1")]
    [InlineData("input.F_A")]
    [InlineData("The alphabet and a theta-less sentence")]
    public void Plain_Text_References_And_Rule_Ids_Are_Left_As_They_Are(string text)
    {
        Assert.False(SymbolText.HasSymbols(text));
        Assert.Equal(text, Show(text));
    }

    [AvaloniaFact]
    public void A_Block_Sets_The_Subscript_Smaller_And_Low_And_Keeps_The_Text_As_Entered()
    {
        var block = new TextBlock { FontSize = 14 };
        SymbolText.Set(block, "D_1 = 12 mm");

        var inlines = block.Inlines!.ToList();
        Assert.Equal(["D", " = 12 mm"], inlines.OfType<Run>().Select(r => r.Text));
        var subscript = (TextBlock)((InlineUIContainer)inlines[1]).Child;
        Assert.Equal("1", subscript.Text);
        Assert.True(subscript.FontSize < 14, "the subscript is set smaller");
        Assert.True(((TranslateTransform)subscript.RenderTransform!).Y > 0, "the subscript is set below the line");
        Assert.Equal("D_1 = 12 mm", SymbolText.Raw(block));

        SymbolText.Set(block, "L = 4 m");
        Assert.Equal("L = 4 m", block.Text);
        Assert.Equal("L = 4 m", SymbolText.Raw(block));
    }

    [Fact]
    public void On_The_Sheet_A_Run_That_Asks_Is_Split_Into_Its_Base_And_A_Lowered_Smaller_Subscript()
    {
        var texts = new List<DocumentTemplate.TextRun>
        {
            new(10, 20, "D_1 = 12 mm", 9.5f, false, SKTextAlign.Left, SKColors.Black, DocumentFontRole.Body, Symbols: true),
            new(10, 30, "CALC_001", 9.5f, false, SKTextAlign.Left, SKColors.Black),
        };

        DocumentTemplate.TypesetSymbols(texts);

        Assert.Equal(["D", "1", " = 12 mm", "CALC_001"], texts.Select(t => t.Text));
        Assert.True(texts[1].Size < texts[0].Size && texts[1].Y > texts[0].Y, "the subscript is set smaller and lower");
        Assert.True(texts[0].X < texts[1].X && texts[1].X < texts[2].X);
    }
}
