using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library.Materials;

/// <summary>Regression tests for the Opus review of Addendum B phase B-0 (W3 material library): one test per finding.</summary>
public sealed partial class MaterialLibraryTests
{
    private static CalcValueSource PublicSource(string source, string edition, string reference) => new(source, edition, reference, CalcLicenceClass.Public, string.Empty);

    // ------------------------------------------------------------------ M1

    [Fact]
    public async Task Review_M1_The_Issued_Sheet_Credits_A_Material_Value_To_Its_Own_Source_Not_The_Records()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var steel = await SaveAsync(host, Steel() with
        {
            Values =
            [
                new CalcDataValue("E", 210, "GPa"),
                new CalcDataValue("nu", 0.3),
                new CalcDataValue("f_y", 355, "MPa", Source: PublicSource("Supplier datasheet 9", "rev C", "p. 9, Table 7")),
            ],
        });
        var calc = await NewCalculationAsync(host, project);
        Assert.True((await host.Service.Materials.ApplyAsync(calc.Id, steel.Id)).Succeeded);

        var sheet = await host.Service.SheetAsync((await host.Store.FindInstanceAsync(calc.Id))!);
        var fy = sheet.Inputs.Single(r => r.Symbol == "f_y").Source;
        var e = sheet.Inputs.Single(r => r.Symbol == "E").Source;

        Assert.Contains("f_y: Supplier datasheet 9, rev C, p. 9, Table 7", fy, StringComparison.Ordinal);
        Assert.DoesNotContain("E: Supplier datasheet 9", e, StringComparison.Ordinal);
        Assert.Contains("E: Mill certificate 4711, rev B, p. 4, Table 2", e, StringComparison.Ordinal);   // E has none of its own: the record's
        Assert.StartsWith("record: S355 (my mill certificate) — Mill certificate 4711, rev B, p. 4, Table 2 rev 1", fy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_M1_A_Value_Read_From_A_Temperature_Table_Is_Credited_To_The_Tables_Source()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var steel = await SaveAsync(host, Steel() with
        {
            Values =
            [
                new CalcDataValue("nu", 0.3),
                new CalcDataValue("E(T)", Rows: [TempRow(20, 210), TempRow(100, 200)], Source: PublicSource("Handbook B", "3rd ed.", "Table 12")),
            ],
        });
        var calc = await NewCalculationAsync(host, project);
        Assert.True((await host.Service.Materials.ApplyAsync(calc.Id, steel.Id, temperature: 60, temperatureUnit: "degC", singleValueAtAnyTemperature: true)).Succeeded);

        var sheet = await host.Service.SheetAsync((await host.Store.FindInstanceAsync(calc.Id))!);
        Assert.Contains("E: Handbook B, 3rd ed., Table 12", sheet.Inputs.Single(r => r.Symbol == "E").Source, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ M2, LOW 5

    [Fact]
    public async Task Review_M2_A_Temperature_Cell_With_No_Unit_Is_Refused_Not_Read_As_Kelvin()
    {
        var host = Host();
        var unitless = Row((MaterialProperties.TemperatureColumn, new CalcCell(20)), (MaterialProperties.ValueColumn, new CalcCell(210, "GPa")));
        var other = Row((MaterialProperties.TemperatureColumn, new CalcCell(100)), (MaterialProperties.ValueColumn, new CalcCell(200, "GPa")));

        var (record, refusal) = await host.Service.Materials.SaveAsync(Steel() with { Values = [new CalcDataValue("E(T)", Rows: [unitless, other])] });

        Assert.Null(record);
        Assert.Contains("temperature unit", refusal, StringComparison.Ordinal);
        Assert.Contains("E(T)[1]", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_LOW5_A_Temperature_Always_Comes_With_Its_Unit_In_Every_Call()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var steel = await SaveAsync(host, Steel());
        var calc = await NewCalculationAsync(host, project);

        var read = MaterialLibrary.GetProperty(steel, "E", 20);
        Assert.Null(read.Value);
        Assert.Contains("temperature unit", read.Refusal, StringComparison.Ordinal);
        Assert.Contains("temperature unit", (await host.Service.Materials.GetPropertyAsync(steel.Id, "E", 20)).Refusal, StringComparison.Ordinal);

        var applied = await host.Service.Materials.ApplyAsync(calc.Id, steel.Id, temperature: 20);
        Assert.False(applied.Succeeded);
        Assert.Contains("temperature unit", applied.Reason, StringComparison.Ordinal);

        // With the unit, the two calls agree on what 20 means.
        Assert.Equal(293.15, MaterialLibrary.GetProperty(steel, "E", 20, "degC").Value!.TemperatureKelvin!.Value, 9);
    }

    // ------------------------------------------------------------------ M3

    [Fact]
    public async Task Review_M3_A_Unit_Free_Property_Stays_Fresh_After_It_Is_Applied()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var spring = await SaveAsync(host, Steel(title: "Spring wire") with { Values = [new CalcDataValue("Rm_A", 1500, "MPa"), new CalcDataValue("E", 206, "GPa")] });
        var calc = await NewCalculationAsync(host, project);

        var applied = await host.Service.Materials.ApplyAsync(calc.Id, spring.Id);
        Assert.True(applied.Succeeded, applied.Reason);
        var input = applied.Instance!.Inputs.Single(i => i.Symbol == "Rm_A");
        Assert.Equal((1500d, "MPa"), (input.Number!.Value, input.Unit));
        var staleness = await host.Service.StalenessAsync(applied.Instance);
        Assert.False(staleness.IsStale, string.Join("; ", staleness.Reasons));
        Assert.True((await host.Service.CheckAsync(calc.Id, "J. Reviewer", "Client Co", "OK", Tempest.Core.Evidence.CheckOutcome.Accepted)).Succeeded);

        // The same property entered in another unit fills the same input and is as fresh.
        var inGpa = await SaveAsync(host, Steel(title: "Spring wire, GPa") with { Values = [new CalcDataValue("Rm_A", 1.5, "GPa"), new CalcDataValue("E", 206, "GPa")] });
        var second = await NewCalculationAsync(host, project);
        var appliedGpa = await host.Service.Materials.ApplyAsync(second.Id, inGpa.Id);
        Assert.True(appliedGpa.Succeeded, appliedGpa.Reason);
        Assert.Equal(1500, appliedGpa.Instance!.Inputs.Single(i => i.Symbol == "Rm_A").Number!.Value, 9);
        Assert.False((await host.Service.StalenessAsync(appliedGpa.Instance)).IsStale);
    }

    // ------------------------------------------------------------------ M4

    [Fact]
    public async Task Review_M4_A_Material_File_With_No_Symbol_Or_An_Empty_Entry_Is_Refused_Not_A_Crash()
    {
        var host = Host();
        var source = PublicSource("Datasheet", "A", "p. 1");

        foreach (var values in new[] { [new CalcDataValue(null!, 1, "GPa", Source: source)], [new CalcDataValue("  ", 1, "GPa", Source: source)], new CalcDataValue[] { null! } })
        {
            var document = new MaterialLibrary.MaterialDocument(MaterialLibrary.ExportFormat, "Odd", "Datasheet", "A", "p. 1", "Maker", CalcDataOrigin.UserEntered, values);
            var (record, refusal) = await host.Service.Materials.ImportJsonAsync(CalcInstanceStore.Serialise(document));
            Assert.Null(record);
            Assert.NotNull(refusal);

            var (saved, savedRefusal) = await host.Service.Materials.SaveAsync(Steel() with { Values = values });
            Assert.Null(saved);
            Assert.NotNull(savedRefusal);
        }

        // Any record kind: an empty entry is a refusal, not a NullReferenceException.
        var (generic, genericRefusal) = await host.Service.SaveRecordAsync(new CalcDataRecord(Guid.Empty, "test.other", "Other", 0, "s", "e", "t", "h", "p", false, CalcDataOrigin.UserEntered, [null!], default));
        Assert.Null(generic);
        Assert.Contains("must each be present", genericRefusal, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ M5

    [Fact]
    public async Task Review_M5_A_Licensed_Value_In_Any_Kind_Of_Record_Makes_The_Record_Licensed()
    {
        var host = Host();
        var licensedValue = new CalcValueSource("Standard (own copy)", "2019", "Table 4", CalcLicenceClass.LicensedSource, string.Empty);
        var (record, refusal) = await host.Service.SaveRecordAsync(new CalcDataRecord(Guid.Empty, "test.other", "Factors", 0, "Standard", "2019", "Table 4", "Body", "x", false, CalcDataOrigin.UserEntered,
            [new CalcDataValue("k", 1.2, Source: licensedValue), new CalcDataValue("m", 3)], default));
        Assert.Null(refusal);
        Assert.True(record!.LicensedSource);

        var (csv, exportRefusal) = await host.Service.ExportRecordAsync(record.Id);
        Assert.Null(csv);
        Assert.Contains("never exported", exportRefusal, StringComparison.Ordinal);

        var inputs = new CalcInputSet([new CalcInputValue("k", Number: 1.2, Provenance: CalcInstanceService.RecordProvenance(record))]);
        Assert.Null((await host.Service.WithholdLicensedAsync(inputs)).Find("k")!.Number);

        // Once licensed, a revision cannot release it.
        var (revision, again) = await host.Service.SaveRecordAsync(record with { LicensedSource = false, Values = [new CalcDataValue("k", 1.2), new CalcDataValue("m", 3)] });
        Assert.Null(revision);
        Assert.Contains("stay licensed", again, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ LOW 4

    [Fact]
    public async Task Review_LOW4_An_Exported_Seed_Keeps_Its_Indicative_Flag_On_Import_And_A_User_Save_Cannot_Claim_It()
    {
        var host = Host();
        var seed = (await host.Service.Materials.FindAsync(MaterialSeeds.CarbonSteel))!;

        var exported = (await host.Service.Materials.ExportJsonAsync(seed.Id)).Json!;
        var (imported, refusal) = await host.Service.Materials.ImportJsonAsync(exported);
        Assert.Null(refusal);
        Assert.Equal(CalcDataOrigin.Indicative, imported!.Origin);
        Assert.Contains("(indicative)", imported.Citation, StringComparison.Ordinal);

        // A file cannot claim a stronger origin than "entered by the person who imports it".
        var derived = exported.Replace("\"Indicative\"", "\"Derived\"", StringComparison.Ordinal);
        Assert.NotEqual(exported, derived);
        Assert.Equal(CalcDataOrigin.UserEntered, (await host.Service.Materials.ImportJsonAsync(derived)).Record!.Origin);

        // A user's own save is not "indicative"; a revision of a seed or an imported seed copy may stay so.
        var own = await SaveAsync(host, Steel() with { Origin = CalcDataOrigin.Indicative });
        Assert.Equal(CalcDataOrigin.UserEntered, own.Origin);
        var revised = await SaveAsync(host, imported with { Notes = "revised" });
        Assert.Equal(CalcDataOrigin.Indicative, revised.Origin);
    }

    // ------------------------------------------------------------------ LOW 6

    [Fact]
    public async Task Review_LOW6_A_Temperature_In_The_Values_Own_Reference_Text_Is_Not_Taken_For_The_Temperature_It_Was_Read_At()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var steel = await SaveAsync(host, Steel() with
        {
            Values = [new CalcDataValue("E(T)", Rows: [TempRow(20, 210), TempRow(100, 200), TempRow(300, 180), TempRow(500, 160)], Source: PublicSource("Handbook", "1st", "Table 3 · T=500 K"))],
        });

        // Applied with no temperature to a property that has only a table is refused; with one it cites that temperature, not the text's.
        var calc = await NewCalculationAsync(host, project);
        var applied = await host.Service.Materials.ApplyAsync(calc.Id, steel.Id, temperature: 20, temperatureUnit: "degC");
        Assert.True(applied.Succeeded, applied.Reason);
        var provenance = applied.Instance!.Inputs.Single(i => i.Symbol == "E").Provenance!;
        Assert.Equal(293.15, MaterialLibrary.TemperatureOf(provenance)!.Value.Kelvin, 9);

        // The same reference text on a value applied without a temperature: no temperature is read out of it.
        var reference = new CalcProvenance(CalcProvenanceKind.Reference, $"{steel.Pin} · E · Table 3 · T=500 K", "1st");
        Assert.Null(MaterialLibrary.TemperatureOf(reference));
        Assert.False((await host.Service.StalenessAsync(applied.Instance)).IsStale);
    }

    // ------------------------------------------------------------------ LOW 7

    [Fact]
    public async Task Review_LOW7_A_Materials_Table_Is_Checked_Against_The_Inputs_Columns_And_Row_Limit()
    {
        var host = Host();
        var project = await host.ProjectAsync();

        var strangeColumn = await SaveAsync(host, Steel(title: "Odd columns") with
        {
            Values = [new CalcDataValue(MaterialProperties.SecantCurve, Rows: [Row(("eps", new CalcCell(0.001)), ("Es", new CalcCell(210, "GPa")), ("extra", new CalcCell(1))), Row(("eps", new CalcCell(0.002)), ("Es", new CalcCell(190, "GPa")), ("extra", new CalcCell(1)))])],
        });
        var refused = await host.Service.Materials.ApplyAsync((await NewCalculationAsync(host, project)).Id, strangeColumn.Id);
        Assert.False(refused.Succeeded);
        Assert.Contains("no column 'extra'", refused.Reason, StringComparison.Ordinal);

        var tooMany = await SaveAsync(host, Steel(title: "Long curve") with
        {
            Values = [new CalcDataValue(MaterialProperties.SecantCurve, Rows: [.. Enumerable.Range(1, 51).Select(i => Row(("eps", new CalcCell(i * 0.001)), ("Es", new CalcCell(210 - i * 0.1, "GPa"))))])],
        });
        var long_ = await host.Service.Materials.ApplyAsync((await NewCalculationAsync(host, project)).Id, tooMany.Id);
        Assert.False(long_.Succeeded);
        Assert.Contains("at most 50 rows", long_.Reason, StringComparison.Ordinal);

        var missing = await SaveAsync(host, Steel(title: "No Es") with
        {
            Values = [new CalcDataValue(MaterialProperties.SecantCurve, Rows: [Row(("eps", new CalcCell(0.001)), ("Es", new CalcCell(210, "GPa"))), Row(("eps", new CalcCell(0.002)), ("Es", new CalcCell(200, "GPa")))])],
        });
        Assert.True((await host.Service.Materials.ApplyAsync((await NewCalculationAsync(host, project)).Id, missing.Id)).Succeeded);   // a fitting table still goes across
    }
}
