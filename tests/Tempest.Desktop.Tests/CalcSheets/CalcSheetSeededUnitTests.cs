using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// desktop.calc-sheet-view-01: a seeded quantity whose unit is valid but not in the pick list kept its number and took the default unit, so it silently changed scale.
/// desktop.calc-sheet-view-02 and its Low remainder: a seed whose unit is unknown (kn) or of another dimension (mm for a load) fell back to the default unit and ran; the form now keeps the seed's own unit, so the engine refuses it.
/// </summary>
public sealed class CalcSheetSeededUnitTests
{
    private static (CalcSheetView View, CalcCatalogue Catalogue) NewView()
    {
        var catalogue = new CalcCatalogue([new TestWorksheetCalculator()]);
        var view = new CalcSheetView();
        view.InputsChanged += inputs => view.ShowRun(CalcRunner.Run(catalogue.Get("test.desktop-stress").Calculator, inputs));
        view.ShowCatalogue(catalogue.Current());
        view.SelectTemplate("test.desktop-stress");
        return (view, catalogue);
    }

    [Fact]
    public void A_Unit_Outside_The_Common_List_Is_Valid_For_The_Dimension_And_Not_Listed_Today()
    {
        // The units named in the finding: none is in the common list, all parse, and each has the dimension of its default unit.
        Assert.True(UnitCatalog.TryParse("daN", out var daN, out _));
        Assert.Equal(UnitCatalog.Parse("kN").Dimension, daN.Dimension);
        Assert.True(UnitCatalog.TryParse("N*mm", out var nmm, out _));
        Assert.Equal(UnitCatalog.Parse("N*m").Dimension, nmm.Dimension);
        Assert.DoesNotContain("daN", CalcSheetView.UnitChoices(new CalcInputSpec("F", "Axial load", CalcInputKind.Quantity, "kN")));
        Assert.DoesNotContain("N*mm", CalcSheetView.UnitChoices(new CalcInputSpec("T", "Torque", CalcInputKind.Quantity, "N*m")));
    }

    [Fact]
    public void A_Seeded_Unit_Of_The_Right_Dimension_Is_Offered_And_Kept_With_Its_Own_Number()
    {
        Assert.Contains("N*mm", CalcSheetView.UnitChoices(new CalcInputSpec("T", "Torque", CalcInputKind.Quantity, "N*m"), "N*mm"));
        Assert.Contains("kN*mm", CalcSheetView.UnitChoices(new CalcInputSpec("T", "Torque", CalcInputKind.Quantity, "N*m"), "kN*mm"));
        Assert.Contains("MW", CalcSheetView.UnitChoices(new CalcInputSpec("P", "Power", CalcInputKind.Quantity, "kW"), "MW"));

        // A unit of another dimension or an unknown unit is not offered (the engine refuses those).
        Assert.DoesNotContain("mm", CalcSheetView.UnitChoices(new CalcInputSpec("T", "Torque", CalcInputKind.Quantity, "N*m"), "mm"));
        Assert.DoesNotContain("zz", CalcSheetView.UnitChoices(new CalcInputSpec("T", "Torque", CalcInputKind.Quantity, "N*m"), "zz"));
    }

    [AvaloniaFact]
    public void A_Seeded_Load_In_Decanewtons_Shows_Its_Number_In_Decanewtons_And_Runs_As_30_Kilonewtons()
    {
        var (view, _) = NewView();
        view.ApplyImportedInputs(new CalcInputSet([CalcInputValue.Quantity("F", 3000, "daN"), CalcInputValue.Quantity("A", 150, "mm^2")]));

        Assert.Equal("3000", ((TextBox)view.FieldControl("F")!).Text);
        var read = view.ReadForm();
        Assert.Equal(3000, read.Find("F")!.Number);
        Assert.Equal("daN", read.Find("F")!.Unit);

        // 3000 daN = 30 kN: the same stress as the 30 kN case of the import test (30e3 N / 150 mm^2 = 200 MPa), not 3000 kN (20 000 MPa).
        Assert.Equal(30e3, UnitCatalog.Parse("daN").ToSi(3000) , 6);
    }

    [AvaloniaTheory]
    [InlineData("kn")]
    [InlineData("mm")]
    [InlineData("zz")]
    public void A_Seeded_Unit_That_The_Engine_Cannot_Use_Is_Kept_And_Refused_Not_Replaced_By_The_Default(string seedUnit)
    {
        var (view, catalogue) = NewView();
        view.ApplyImportedInputs(new CalcInputSet([CalcInputValue.Quantity("F", 30, seedUnit), CalcInputValue.Quantity("A", 150, "mm^2")]));

        var read = view.ReadForm();
        Assert.Equal(seedUnit, read.Find("F")!.Unit);
        Assert.Equal(30, read.Find("F")!.Number);

        var result = CalcRunner.Run(catalogue.Get("test.desktop-stress").Calculator, read);
        Assert.True(result.IsBlocked);
        Assert.Contains(result.Blocks, b => b.Message.Contains('F', StringComparison.Ordinal));
    }
}
