using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library.Materials;

/// <summary>Regression tests for the Addendum B security review (SEC-B-02): the licence lock belongs to the record, not to one revision of it.</summary>
public sealed partial class MaterialLibraryTests
{
    [Fact]
    public async Task SEC_B_02_A_Material_Licensed_In_A_Later_Revision_Releases_No_Value_Through_An_Earlier_One()
    {
        var host = Host();
        var project = await host.ProjectAsync();

        // Revision 1 is entered without the licence flag, a calculation takes its values, then the owner corrects the slip in revision 2.
        var rev1 = await SaveAsync(host, Steel(title: "EN steel (entered without the licence flag)"));
        var calc = await NewCalculationAsync(host, project);
        var applied = await host.Service.Materials.ApplyAsync(calc.Id, rev1.Id);
        Assert.True(applied.Succeeded, applied.Reason);
        var rev2 = await SaveAsync(host, rev1 with { LicensedSource = true });
        Assert.Equal(2, rev2.Revision);
        Assert.False(rev1.LicensedSource);

        // Library exports: every revision is refused, the earlier one included.
        foreach (int? revision in new int?[] { null, 1, 2 })
        {
            var csv = await host.Service.Materials.ExportCsvAsync(rev1.Id, revision);
            Assert.Null(csv.Csv);
            Assert.NotNull(csv.Refusal);

            var json = await host.Service.Materials.ExportJsonAsync(rev1.Id, revision);
            Assert.Null(json.Json);
            Assert.NotNull(json.Refusal);

            var record = await host.Service.ExportRecordAsync(rev1.Id, revision);
            Assert.Null(record.Csv);
            Assert.NotNull(record.Refusal);
        }

        // A calculation still pinned to revision 1: its clipboard CSV withholds the applied values, scalars and table rows alike.
        var stored = (await host.Store.FindInstanceAsync(calc.Id))!;
        Assert.Equal((rev1.Id, 1), CalcDataRecord.PinOf(stored.Inputs.First(i => i.Symbol == "E").Provenance));
        var withheld = await host.Service.WithholdLicensedAsync(new CalcInputSet(stored.Inputs));
        Assert.Null(withheld.Find("E")!.Number);
        Assert.Null(withheld.Find("f_y")!.Number);
        Assert.Null(withheld.Find("Es_pts")!.Rows);

        // Its draft sheet shows the value used, never the licensed table rows.
        var sheet = CalcSheetTextRenderer.Render(await host.Service.SheetAsync(stored));
        Assert.Contains("rows withheld (licensed record)", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain("180", sheet, StringComparison.Ordinal);

        // A later revision cannot unlock it again.
        var again = await host.Service.Materials.SaveAsync(rev2 with { LicensedSource = false });
        Assert.True(again.Record is { LicensedSource: true } || again.Refusal is not null);
        Assert.Null((await host.Service.Materials.ExportCsvAsync(rev1.Id, 1)).Csv);
        Assert.Null((await host.Service.Materials.ExportCsvAsync(rev1.Id, 3)).Csv);
    }

    [Fact]
    public async Task SEC_B_02_A_Material_That_Was_Never_Licensed_Still_Exports_Every_Revision()
    {
        var host = Host();
        var rev1 = await SaveAsync(host, Steel(title: "My own steel"));
        var rev2 = await SaveAsync(host, rev1 with { Title = "My own steel, renamed" });

        foreach (var revision in new[] { 1, 2 })
        {
            Assert.NotNull((await host.Service.Materials.ExportCsvAsync(rev1.Id, revision)).Csv);
            Assert.NotNull((await host.Service.Materials.ExportJsonAsync(rev1.Id, revision)).Json);
            Assert.NotNull((await host.Service.ExportRecordAsync(rev1.Id, revision)).Csv);
        }

        Assert.Equal(2, rev2.Revision);
    }
}
