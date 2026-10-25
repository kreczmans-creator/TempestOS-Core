using Tempest.Calc.Sheets;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>The owner's ask (2026-10-24): AMA citations on the issued sheet, the references listed at its very end.</summary>
public sealed class CalcBibliographyTests
{
    private static readonly CalcClauseRef[] BoltedJoint =
    [
        new("VDI 2230 Part 1:2015", "§5.1–§5.5", "Systematic calculation"),
        new("ISO 898-1:2013", "§9.1.6", "Tensile stress area"),
        new("Shigley's Mechanical Engineering Design", "§8-5, §8-7", "Joint stiffness"),
    ];

    [Fact]
    public void Works_Are_Numbered_In_Order_Of_First_Citation_And_Each_Is_Listed_Once()
    {
        var register = new CalcBibliography.Register(BoltedJoint);

        Assert.Equal([1, 2, 3], BoltedJoint.Select(r => register.Cite(r).Number));
        Assert.Equal(new CalcBibliography.Citation(1, "R8"), register.Cite("VDI 2230 R8"));
        Assert.Equal(new CalcBibliography.Citation(3, "§7-4"), register.Cite("Shigley §7-4"));
        Assert.Equal(3, register.References.Count);
        Assert.StartsWith("Budynas RG, Nisbett JK. Shigley's Mechanical Engineering Design.", register.References[2].Entry, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Step_Names_Its_Work_By_Author_Standard_Or_A_Bare_Clause()
    {
        var register = new CalcBibliography.Register(
        [
            new("BS EN 1993-1-1", "6.2.5", "Bending moment"),
            new("EN 1993-1-8:2005", "§3.6, §3.8", "Fasteners"),
            new("Çengel & Cimbala, Fluid Mechanics", "Ch. 8", "Pipe flow"),
            new("Çengel & Ghajar, Heat and Mass Transfer", "§15-2", "Networks"),
        ]);

        Assert.Equal(new CalcBibliography.Citation(1, "6.2.6"), register.Cite("6.2.6"));
        Assert.Equal(new CalcBibliography.Citation(2, "Table 3.4"), register.Cite("EN 1993-1-8 Table 3.4"));
        Assert.Contains("Ghajar", register.References[register.Cite("Çengel §15-2").Number - 1].Entry, StringComparison.Ordinal);

        // A work the method does not list is still cited and listed, from the registry or the step's own words.
        var lug = register.Cite("ASME BTH-1 Ch. 3");
        Assert.Equal("Ch. 3", lug.Locator);
        Assert.Contains("Design of Below-the-Hook Lifting Devices", register.References[lug.Number - 1].Entry, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Citation_Is_A_Superscript_Number_With_Its_Locator()
    {
        Assert.Equal("¹²", CalcBibliography.Superscript(12));
        Assert.Equal("³(R8)", CalcBibliography.Format(new CalcBibliography.Citation(3, "R8")));
        Assert.Equal("⁴", CalcBibliography.Format(new CalcBibliography.Citation(4, null)));
    }
}
