using System.Globalization;
using System.Text;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;

namespace Tempest.Calc.Tests.Library.Materials;

/// <summary>A calculator whose inputs take their values from a material (Addendum B, W3): E, ν, ρ, α, f_y and the secant-modulus points.</summary>
internal sealed class MaterialUsingCalculator : ICalcCalculator
{
    private static readonly IReadOnlyList<CalcColumnSpec> EsColumns = [new("eps", "Strain", CalcInputKind.Number, string.Empty), new("Es", "Secant modulus", CalcInputKind.Quantity, "GPa")];

    public CalcTemplate Template { get; } = new(
        "test.material-user", "1.0.0", CalcDiscipline.Mechanical, "Material user",
        "Takes its elastic constants from a material.",
        "Shear modulus: G = E / (2 (1 + ν)).",
        [
            new CalcInputSpec("E", "Young's modulus", CalcInputKind.Quantity, "GPa", Min: 1, Max: 1000, HardMin: 0, HardMax: 10000, MaterialProperty: "E"),
            new CalcInputSpec("nu", "Poisson's ratio", CalcInputKind.Number, Min: 0, Max: 0.49, HardMin: -0.99, HardMax: 0.499, MaterialProperty: "nu"),
            new CalcInputSpec("rho", "Density", CalcInputKind.Quantity, "kg/m^3", IsOptional: true, HardMin: 0, MaterialProperty: "rho"),
            new CalcInputSpec("alpha", "Thermal expansion", CalcInputKind.Quantity, "1/K", IsOptional: true, MaterialProperty: "alpha"),
            new CalcInputSpec("f_y", "Yield strength", CalcInputKind.Quantity, "MPa", IsOptional: true, HardMin: 0, MaterialProperty: "f_y"),
            new CalcInputSpec("Es_pts", "Secant modulus points", CalcInputKind.Table, Columns: EsColumns, MaxRows: 50, IsOptional: true, MaterialProperty: MaterialProperties.SecantCurve),
        ],
        [new CalcAssumption("Test only.", "—")],
        [new CalcClauseRef("Test", "—", "Test")],
        [],
        [
            new CalcTestCase("ka-1", "Steel", new CalcReferenceSource("Hand", null, "210 / 2.6"), [CalcInputValue.Quantity("E", 210, "GPa"), CalcInputValue.Of("nu", 0.3)], [new CalcExpectedValue("G", 80.76923076923077, "GPa", 1e-9)]),
            new CalcTestCase("ka-2", "Aluminium", new CalcReferenceSource("Hand", null, "70 / 2.66"), [CalcInputValue.Quantity("E", 70, "GPa"), CalcInputValue.Of("nu", 0.33)], [new CalcExpectedValue("G", 26.31578947368421, "GPa", 1e-9)]),
        ],
        RecordKinds: [MaterialLibrary.RecordKind(["E", "nu", "rho", "alpha", "f_y", "Es_pts"])]);

    public void Run(CalcRunContext c)
    {
        c.Input("E");
        c.Input("nu");

        foreach (var optional in new[] { "rho", "alpha", "f_y" })
        {
            if (c.Has(optional))
                c.Input(optional);
        }

        if (c.Has("Es_pts"))
        {
            var top = c.Rows("Es_pts").Select(r => r.Cells.TryGetValue("Es", out var cell) && cell.Number is { } n ? UnitCatalog.Parse(string.IsNullOrEmpty(cell.Unit) ? "GPa" : cell.Unit).ToSi(n) : 0).DefaultIfEmpty(0).Max();
            c.Record("Es_max", "max(Es)", top.ToString("G6", CultureInfo.InvariantCulture) + " Pa", ScalarValue.Of(top, "Pa"), "Largest secant modulus");
        }

        c.Assign("G", "E / (2 * (1 + nu))", "Pa", "Shear modulus");
        c.Output("G", "Shear modulus");
    }
}

/// <summary>Addendum B, W3 — the material property library on the reference records.</summary>
public sealed class MaterialLibraryTests
{
    private static LibraryTestHost Host() => new(calculators: [new MaterialUsingCalculator()]);

    private static CalcRow Row(params (string Key, CalcCell Cell)[] cells) => CalcRow.Of(cells);

    private static CalcRow TempRow(double celsius, double gpa) => Row((MaterialProperties.TemperatureColumn, new CalcCell(celsius, "degC")), (MaterialProperties.ValueColumn, new CalcCell(gpa, "GPa")));

    private static CalcDataRecord Steel(bool licensed = false, double e = 210, string title = "S355 (my mill certificate)", Guid? id = null) => new(
        id ?? Guid.Empty, MaterialLibrary.Kind, title, 0, "Mill certificate 4711", "rev B", "p. 4, Table 2", "Test Steelworks", "ignored", licensed, CalcDataOrigin.UserEntered,
        [
            new CalcDataValue("E", e, "GPa"),
            new CalcDataValue("nu", 0.3),
            new CalcDataValue("rho", 7850, "kg/m^3"),
            new CalcDataValue("f_y", 355, "MPa", Source: new CalcValueSource("Mill certificate 4711", "rev B", "p. 5, Table 3", licensed ? CalcLicenceClass.LicensedSource : CalcLicenceClass.Public, string.Empty, null, MaterialLibrary.Sha256Hex(Encoding.UTF8.GetBytes("abc")))),
            new CalcDataValue("E(T)", Rows: [TempRow(20, 210), TempRow(100, 200), TempRow(200, 190)]),
            new CalcDataValue(MaterialProperties.SecantCurve, Rows:
            [
                Row(("eps", new CalcCell(0.001)), ("Es", new CalcCell(210, "GPa"))),
                Row(("eps", new CalcCell(0.002)), ("Es", new CalcCell(180, "GPa"))),
            ]),
            new CalcDataValue(MaterialProperties.FrictionPairs, Rows: [Row(("pair", new CalcCell(Text: "steel / steel, dry")), ("mu", new CalcCell(0.6)))]),
        ],
        default);

    private static async Task<CalcDataRecord> SaveAsync(LibraryTestHost host, CalcDataRecord draft)
    {
        var (record, refusal) = await host.Service.Materials.SaveAsync(draft);
        Assert.Null(refusal);
        return record!;
    }

    private static int _counter;

    private static Task<CalcInstance> NewCalculationAsync(LibraryTestHost host, Guid? project) =>
        host.Service.CreateAsync("test.material-user", project, $"Beam {Interlocked.Increment(ref _counter)}",
        [
            CalcInputValue.Quantity("E", 1, "GPa", CalcProvenance.Specification("typed")),
            CalcInputValue.Of("nu", 0.3, CalcProvenance.Specification("typed")),
        ]);

    // ------------------------------------------------------------------ seeds

    [Fact]
    public async Task The_Seed_Materials_Are_Generic_Unlicensed_And_Flagged_Indicative()
    {
        var host = Host();
        var materials = await host.Service.Materials.ListAsync();
        Assert.Equal(4, materials.Count);
        Assert.All(materials, m =>
        {
            Assert.Equal(CalcDataOrigin.Indicative, m.Origin);
            Assert.False(m.LicensedSource);
            Assert.Contains("(indicative)", m.Citation, StringComparison.Ordinal);
            Assert.Equal(1, m.Revision);
            Assert.All(m.Values, v =>
            {
                Assert.Equal(CalcLicenceClass.Public, v.Source!.Licence);
                Assert.Equal(MaterialSeeds.SeedPrincipal, v.Source.EnteredBy);
            });
        });

        var steel = await host.Service.Materials.FindByTitleAsync("carbon steel (generic)");
        Assert.NotNull(steel);
        Assert.Equal(MaterialSeeds.CarbonSteel, steel.Id);
        Assert.Equal(210e9, MaterialLibrary.GetProperty(steel, "E").Value!.Si, 3);
        Assert.Equal(0.3, MaterialLibrary.GetProperty(steel, "nu").Value!.Si, 12);
        Assert.Equal(7850, MaterialLibrary.GetProperty(steel, "rho").Value!.Si, 9);
        Assert.Equal(12e-6, MaterialLibrary.GetProperty(steel, "alpha").Value!.Si, 15);

        foreach (var title in new[] { "Aluminium alloy (generic)", "Copper (generic)" })
            Assert.NotNull(await host.Service.Materials.FindByTitleAsync(title));

        // Seeding is safe to repeat: no second revision appears.
        await host.Service.Materials.EnsureSeededAsync();
        Assert.All(await host.Service.Materials.ListAsync(), m => Assert.Equal(1, m.Revision));
        Assert.Single(await host.Service.Materials.ListAsync("copper"));
    }

    [Fact]
    public async Task No_Standard_Or_Catalogue_Table_Is_Seeded_Only_Single_Generic_Values()
    {
        var host = Host();

        foreach (var material in await host.Service.Materials.ListAsync())
        {
            Assert.All(material.Values, v => Assert.Null(v.Rows));
            Assert.DoesNotContain(material.Values, v => v.Symbol is "f_y" or "f_u" or "Rm_A" or "Rm_m" || v.Symbol.EndsWith(MaterialProperties.TemperatureSuffix, StringComparison.Ordinal));
        }
    }

    // ------------------------------------------------------------------ round trip

    [Fact]
    public async Task A_Material_Round_Trips_With_Every_Value_Carrying_Its_Source_Stamp_And_Evidence_Hash()
    {
        var host = Host();
        var saved = await SaveAsync(host, Steel());
        Assert.Equal(1, saved.Revision);
        Assert.False(saved.LicensedSource);

        var read = (await host.Service.Materials.FindAsync(saved.Id))!;
        Assert.Equal(saved.Values.Count, read.Values.Count);
        Assert.Equal(CalcInstanceStore.Serialise(saved), CalcInstanceStore.Serialise(read));

        // A value with no source of its own takes the record's; one with its own keeps it. Both are stamped with the person and the time.
        var e = read.Values.Single(v => v.Symbol == "E").Source!;
        Assert.Equal(("Mill certificate 4711", "rev B", "p. 4, Table 2", CalcLicenceClass.Public), (e.Source, e.Edition, e.Reference, e.Licence));
        Assert.Equal(host.Principal, e.EnteredBy);
        Assert.NotNull(e.Date);

        var fy = read.Values.Single(v => v.Symbol == "f_y").Source!;
        Assert.Equal("p. 5, Table 3", fy.Reference);
        Assert.Equal(host.Principal, fy.EnteredBy);
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", fy.EvidenceSha256);

        // The raw document names the licence class the way the data model does.
        var licensed = await SaveAsync(host, Steel(licensed: true, title: "Licensed steel"));
        var raw = CalcInstanceStore.Serialise(licensed);
        Assert.Contains("\"licensed_source\"", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("\"public\"", raw, StringComparison.Ordinal);
        Assert.Contains("\"public\"", CalcInstanceStore.Serialise(saved), StringComparison.Ordinal);
    }

    [Fact]
    public void The_Evidence_Hash_Is_A_Real_Sha256()
    {
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", MaterialLibrary.Sha256Hex(Encoding.UTF8.GetBytes("abc")));
    }

    [Fact]
    public async Task A_Revision_Stamps_Only_The_Values_It_Changes()
    {
        var host = Host();
        host.Principal = "a.engineer";
        var first = await SaveAsync(host, Steel());
        await Task.Delay(20);

        host.Principal = "b.engineer";
        var second = await SaveAsync(host, first with { Values = [.. first.Values.Select(v => v.Symbol == "E" ? v with { Number = 200 } : v)] });

        Assert.Equal(2, second.Revision);
        Assert.Equal("b.engineer", second.Values.Single(v => v.Symbol == "E").Source!.EnteredBy);
        Assert.Equal("a.engineer", second.Values.Single(v => v.Symbol == "rho").Source!.EnteredBy);
        Assert.Equal(first.Values.Single(v => v.Symbol == "rho").Source!.Date, second.Values.Single(v => v.Symbol == "rho").Source!.Date);
        Assert.True(second.Values.Single(v => v.Symbol == "E").Source!.Date > first.Values.Single(v => v.Symbol == "E").Source!.Date);
        Assert.Equal(210, (await host.Store.FindRecordAsync(first.Id, 1))!.Values.Single(v => v.Symbol == "E").Number);
    }

    [Fact]
    public async Task A_Material_Is_Checked_Before_It_Is_Saved()
    {
        var host = Host();
        async Task<string?> Refusal(CalcDataRecord draft) => (await host.Service.Materials.SaveAsync(draft)).Refusal;
        CalcDataRecord With(params CalcDataValue[] values) => Steel() with { Values = values };

        Assert.Contains("not a material property", await Refusal(With(new CalcDataValue("E_mod", 210, "GPa"))), StringComparison.Ordinal);
        Assert.Contains("does not measure Young's modulus", await Refusal(With(new CalcDataValue("E", 210, "kg"))), StringComparison.Ordinal);
        Assert.Contains("give the unit", await Refusal(With(new CalcDataValue("E", 210))), StringComparison.Ordinal);
        Assert.Contains("must be larger than 0", await Refusal(With(new CalcDataValue("E", -1, "GPa"))), StringComparison.Ordinal);
        Assert.Contains("smaller than 0.5", await Refusal(With(new CalcDataValue("nu", 0.5))), StringComparison.Ordinal);
        Assert.Contains("rise strictly", await Refusal(With(new CalcDataValue("E(T)", Rows: [TempRow(100, 200), TempRow(20, 210)]))), StringComparison.Ordinal);
        Assert.Contains("at least two rows", await Refusal(With(new CalcDataValue("E(T)", Rows: [TempRow(20, 210)]))), StringComparison.Ordinal);
        Assert.Contains("not a temperature unit", await Refusal(With(new CalcDataValue("E(T)", Rows: [Row((MaterialProperties.TemperatureColumn, new CalcCell(20, "mm")), (MaterialProperties.ValueColumn, new CalcCell(210, "GPa"))), TempRow(100, 200)]))), StringComparison.Ordinal);
        Assert.Contains("name the pair", await Refusal(With(new CalcDataValue("mu_pairs", Rows: [Row(("mu", new CalcCell(0.5)))]))), StringComparison.Ordinal);
        Assert.Contains("64 hexadecimal", await Refusal(With(new CalcDataValue("E", 210, "GPa", Source: new CalcValueSource("a", "b", "c", CalcLicenceClass.Public, "d", null, "not-a-hash")))), StringComparison.Ordinal);
        Assert.Contains("source title", await Refusal(Steel() with { SourceTitle = " " }), StringComparison.Ordinal);

        // A symbol that exists only as a temperature table cannot be asked without one, and the library keeps its kind.
        var saved = await SaveAsync(host, Steel());
        Assert.Contains("keeps its kind", (await host.Service.SaveRecordAsync(saved with { Kind = "other.kind" })).Refusal, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ units and temperature

    [Fact]
    public void Units_Are_Converted_Through_SI_And_A_Wrong_Kind_Of_Unit_Is_Refused()
    {
        Assert.Equal(210_000, MaterialLibrary.Convert(210, "GPa", "MPa").Value!.Value, 6);
        Assert.Equal(210e9, MaterialLibrary.ToSi(210, "GPa").Value!.Value, 3);
        Assert.Equal(7850, MaterialLibrary.Convert(7.85, "g/cm^3", "kg/m^3").Value!.Value, 9);
        Assert.Equal(293.15, MaterialLibrary.Convert(20, "degC", "K").Value!.Value, 9);
        Assert.Equal(68, MaterialLibrary.Convert(20, "degC", "degF").Value!.Value, 9);

        var wrong = MaterialLibrary.Convert(1, "MPa", "kg");
        Assert.Null(wrong.Value);
        Assert.Contains("do not measure the same thing", wrong.Refusal, StringComparison.Ordinal);
        Assert.Contains("not understood", MaterialLibrary.Convert(1, "furlongs", "m").Refusal, StringComparison.Ordinal);
        Assert.NotNull(MaterialLibrary.Convert(double.NaN, "m", "mm").Refusal);

        var steel = MaterialLibrary.GetProperty(new CalcDataRecord(Guid.NewGuid(), MaterialLibrary.Kind, "x", 1, "s", "e", "t", "h", "p", false, CalcDataOrigin.UserEntered,
            [new CalcDataValue("E", 210, "GPa", Source: new CalcValueSource("s", "e", "t", CalcLicenceClass.Public, "p"))], default), "E").Value!;
        Assert.Equal("Pa", steel.SiUnit);
        Assert.Equal(210_000, steel.In("MPa").Value!.Value, 6);
        Assert.Contains("do not measure the same thing", steel.In("kg").Refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Temperature_Table_Is_Read_Linearly_Between_Rows_At_Its_Ends_And_Refused_Outside_Them()
    {
        var host = Host();
        var steel = await SaveAsync(host, Steel());

        double E(double t, string unit = "degC") => MaterialLibrary.GetProperty(steel, "E", t, unit).Value!.Si;

        Assert.Equal(205e9, E(60), 3);                 // midway between 20 °C (210) and 100 °C (200)
        Assert.Equal(195e9, E(150), 3);                // midway between 100 °C and 200 °C
        Assert.Equal(210e9, E(20), 3);                 // the first row
        Assert.Equal(190e9, E(200), 3);                // the last row
        Assert.Equal(200e9, E(100), 3);                // an interior row
        Assert.Equal(205e9, E(333.15, "K"), 3);        // the same temperature in kelvin
        Assert.Equal(205e9, E(140, "degF"), 3);        // and in °F

        var basis = MaterialLibrary.GetProperty(steel, "E", 60, "degC").Value!;
        Assert.Equal("interpolated between 20 and 100 degC", basis.Basis);
        Assert.Equal("tabulated at 100 degC", MaterialLibrary.GetProperty(steel, "E", 100, "degC").Value!.Basis);
        Assert.Equal(333.15, basis.TemperatureKelvin!.Value, 9);

        var below = MaterialLibrary.GetProperty(steel, "E", 10, "degC");
        Assert.Null(below.Value);
        Assert.Contains("tabulated from 20 to 200 degC; 10 degC is outside that range and is not extrapolated", below.Refusal, StringComparison.Ordinal);
        var above = MaterialLibrary.GetProperty(steel, "E", 250, "degC");
        Assert.Null(above.Value);
        Assert.Contains("250 degC is outside that range", above.Refusal, StringComparison.Ordinal);
        Assert.Contains("not extrapolated", MaterialLibrary.GetProperty(steel, "E", 600, "K").Refusal, StringComparison.Ordinal);
        Assert.Contains("not a temperature unit", MaterialLibrary.GetProperty(steel, "E", 60, "MPa").Refusal, StringComparison.Ordinal);

        // No temperature: the single value; a single value has no temperature to be read at; an absent property is said to be absent.
        Assert.Equal(210e9, MaterialLibrary.GetProperty(steel, "E").Value!.Si, 3);
        Assert.Contains("single value with no temperature table", MaterialLibrary.GetProperty(steel, "rho", 60, "degC").Refusal, StringComparison.Ordinal);
        Assert.Contains("does not state alpha", MaterialLibrary.GetProperty(steel, "alpha").Refusal, StringComparison.Ordinal);
        Assert.Contains("not a material property", MaterialLibrary.GetProperty(steel, "Young").Refusal, StringComparison.Ordinal);

        // A property given only against temperature needs one.
        var onlyTable = await SaveAsync(host, Steel(title: "Table only") with { Values = [new CalcDataValue("E(T)", Rows: [TempRow(20, 210), TempRow(100, 200)])] });
        Assert.Contains("say at which temperature", MaterialLibrary.GetProperty(onlyTable, "E").Refusal, StringComparison.Ordinal);
        Assert.Equal(205e9, MaterialLibrary.GetProperty(onlyTable, "E", 60, "degC").Value!.Si, 3);
    }

    // ------------------------------------------------------------------ apply, pin, stale

    [Fact]
    public async Task Applying_A_Material_Fills_The_Inputs_In_Their_Own_Units_And_Pins_The_Revision()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var steel = await SaveAsync(host, Steel());
        var calc = await NewCalculationAsync(host, project);

        var applied = await host.Service.Materials.ApplyAsync(calc.Id, steel.Id);
        Assert.True(applied.Succeeded, applied.Reason);
        var inputs = applied.Instance!.Inputs.ToDictionary(i => i.Symbol);

        Assert.Equal(210, inputs["E"].Number);
        Assert.Equal("GPa", inputs["E"].Unit);
        Assert.Equal(0.3, inputs["nu"].Number);
        Assert.Null(inputs["nu"].Unit);
        Assert.Equal(7850, inputs["rho"].Number);
        Assert.Equal(355, inputs["f_y"].Number);
        Assert.Equal("MPa", inputs["f_y"].Unit);
        Assert.False(inputs.ContainsKey("alpha"));    // the material does not state it; the input stays empty
        Assert.Equal(2, inputs["Es_pts"].Rows!.Count);

        Assert.Equal((steel.Id, 1), CalcDataRecord.PinOf(inputs["E"].Provenance));
        Assert.Contains("· E ·", inputs["E"].Provenance!.Reference, StringComparison.Ordinal);
        Assert.Contains("p. 4, Table 2", inputs["E"].Provenance!.Reference, StringComparison.Ordinal);
        Assert.Contains("p. 5, Table 3", inputs["f_y"].Provenance!.Reference, StringComparison.Ordinal);
        Assert.Equal("rev B", inputs["E"].Provenance!.Revision);
        Assert.DoesNotContain(applied.Instance.Warnings, w => w.RuleId == "record.E");
        Assert.False((await host.Service.StalenessAsync(applied.Instance)).IsStale);

        var run = (await host.Service.RunAsync(calc.Id))!;
        Assert.Equal(210e9 / 2.6, run.Instance.Outputs.Single(o => o.Symbol == "G").Si!.Value, 3);
        Assert.Contains("record: S355 (my mill certificate) — Mill certificate 4711, rev B, p. 4, Table 2 rev 1", run.SheetText, StringComparison.Ordinal);

        // A material is applied through the ordinary record entry too (the worksheet form's records panel).
        Assert.Contains(await host.Service.RecordsForAsync("test.material-user"), r => r.Id == steel.Id);
        var viaPanel = await NewCalculationAsync(host, project);
        Assert.True((await host.Service.ApplyRecordAsync(viaPanel.Id, steel.Id)).Succeeded);
        Assert.Contains((await host.Service.RecordsForAsync("test.material-user")), r => r.Id == MaterialSeeds.CarbonSteel);

        // A generic seed fills the same inputs, citing itself as indicative.
        var generic = await host.Service.Materials.ApplyAsync((await NewCalculationAsync(host, project)).Id, MaterialSeeds.CarbonSteel);
        Assert.True(generic.Succeeded, generic.Reason);
        Assert.Equal(210, generic.Instance!.Inputs.Single(i => i.Symbol == "E").Number);
        Assert.Equal(12e-6, generic.Instance.Inputs.Single(i => i.Symbol == "alpha").Number!.Value, 15);
        Assert.Contains("(indicative)", (await host.Service.RunAsync(generic.Instance.Id))!.SheetText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Later_Revision_Of_The_Material_Makes_A_Calculation_Pinned_To_The_Earlier_One_Stale()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var steel = await SaveAsync(host, Steel());
        var calc = await NewCalculationAsync(host, project);
        Assert.True((await host.Service.Materials.ApplyAsync(calc.Id, steel.Id)).Succeeded);
        Assert.False((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(calc.Id))!)).IsStale);

        var second = await SaveAsync(host, steel with { Values = [.. steel.Values.Select(v => v.Symbol == "E" ? v with { Number = 205 } : v)] });
        Assert.Equal(2, second.Revision);

        var stale = await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(calc.Id))!);
        Assert.True(stale.IsStale);
        Assert.Contains(stale.Reasons, r => r.Contains("revision 2 has been entered since", StringComparison.Ordinal));
        Assert.False((await host.Service.CheckAsync(calc.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);

        // The calculation still holds revision 1 until the material is applied again, which pins revision 2.
        Assert.Equal(210, (await host.Store.FindInstanceAsync(calc.Id))!.Inputs.Single(i => i.Symbol == "E").Number);
        var again = await host.Service.Materials.ApplyAsync(calc.Id, steel.Id);
        Assert.True(again.Succeeded, again.Reason);
        Assert.Equal(205, again.Instance!.Inputs.Single(i => i.Symbol == "E").Number);
        Assert.Equal((steel.Id, 2), CalcDataRecord.PinOf(again.Instance.Inputs.Single(i => i.Symbol == "E").Provenance));
        Assert.False((await host.Service.StalenessAsync(again.Instance)).IsStale);

        // A pin to an earlier revision applies that revision, not the latest.
        var back = await host.Service.Materials.ApplyAsync(calc.Id, steel.Id, revision: 1);
        Assert.Equal(210, back.Instance!.Inputs.Single(i => i.Symbol == "E").Number);
        Assert.True((await host.Service.StalenessAsync(back.Instance)).IsStale);
    }

    [Fact]
    public async Task Staleness_Spreads_From_An_Edited_Material_To_Every_Calculation_That_Took_Values_From_It()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var steel = await SaveAsync(host, Steel());
        var a = await NewCalculationAsync(host, project);
        var b = await NewCalculationAsync(host, project);
        var unrelated = await NewCalculationAsync(host, project);
        Assert.True((await host.Service.Materials.ApplyAsync(a.Id, steel.Id)).Succeeded);
        Assert.True((await host.Service.Materials.ApplyAsync(b.Id, steel.Id, temperature: 60, singleValueAtAnyTemperature: true)).Succeeded);

        await SaveAsync(host, steel with { Values = [.. steel.Values.Select(v => v.Symbol == "rho" ? v with { Number = 7800 } : v)] });

        var all = await host.Service.StalenessForProjectAsync(project);
        Assert.True(all[a.Id].IsStale);
        Assert.True(all[b.Id].IsStale);
        Assert.False(all[unrelated.Id].IsStale);
    }

    [Fact]
    public async Task Typing_A_Value_By_Hand_Drops_The_Pin_And_Changing_An_Applied_Value_Is_Caught()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var steel = await SaveAsync(host, Steel());
        var calc = await NewCalculationAsync(host, project);
        var applied = (await host.Service.Materials.ApplyAsync(calc.Id, steel.Id)).Instance!;

        // A value changed after it was applied no longer matches the material it still cites.
        var tampered = applied.Inputs.Select(i => i.Symbol == "E" ? i with { Number = 250 } : i).ToList();
        var edited = await host.Service.UpdateInputsAsync(calc.Id, tampered);
        Assert.True(edited.Succeeded, edited.Reason);
        var mismatch = await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(calc.Id))!);
        Assert.Contains(mismatch.Reasons, r => r.Contains("no longer matches", StringComparison.Ordinal));

        // Unpinned: the same value typed by hand carries its own provenance, is not stale, and says it is not from a record.
        var unpinned = applied.Inputs.Select(i => i.Symbol == "E" ? CalcInputValue.Quantity("E", 250, "GPa", CalcProvenance.Specification("typed by hand")) : i).ToList();
        var result = await host.Service.UpdateInputsAsync(calc.Id, unpinned);
        Assert.True(result.Succeeded, result.Reason);
        Assert.Null(CalcDataRecord.PinOf(result.Instance!.Inputs.Single(i => i.Symbol == "E").Provenance));
        Assert.Contains(result.Instance.Warnings, w => w.RuleId == "record.E");
        var rest = await host.Service.StalenessAsync(result.Instance);
        Assert.DoesNotContain(rest.Reasons, r => r.Contains("'E'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_Material_Is_Read_At_A_Temperature_Cited_In_The_Input_And_Refused_Outside_The_Table()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var steel = await SaveAsync(host, Steel());
        var calc = await NewCalculationAsync(host, project);

        // ν, ρ and f_y have no temperature table: applying at a temperature is refused unless their single values are said to stand for it.
        var refused = await host.Service.Materials.ApplyAsync(calc.Id, steel.Id, temperature: 60);
        Assert.False(refused.Succeeded);
        Assert.Contains("single value with no temperature table", refused.Reason, StringComparison.Ordinal);

        var applied = await host.Service.Materials.ApplyAsync(calc.Id, steel.Id, temperature: 60, temperatureUnit: "degC", singleValueAtAnyTemperature: true);
        Assert.True(applied.Succeeded, applied.Reason);
        var e = applied.Instance!.Inputs.Single(i => i.Symbol == "E");
        Assert.Equal(205, e.Number!.Value, 9);
        Assert.Equal("GPa", e.Unit);
        Assert.Contains("T=333.15 K", e.Provenance!.Reference, StringComparison.Ordinal);
        Assert.Equal(333.15, MaterialLibrary.TemperatureOf(e.Provenance)!.Value.Kelvin, 9);
        Assert.Contains("· single value", applied.Instance.Inputs.Single(i => i.Symbol == "rho").Provenance!.Reference, StringComparison.Ordinal);
        Assert.False((await host.Service.StalenessAsync(applied.Instance)).IsStale);   // an interpolated value still matches its material

        var outside = await host.Service.Materials.ApplyAsync(calc.Id, steel.Id, temperature: 500, temperatureUnit: "degC", singleValueAtAnyTemperature: true);
        Assert.False(outside.Succeeded);
        Assert.Contains("not extrapolated", outside.Reason, StringComparison.Ordinal);
        Assert.Contains("not a temperature unit", (await host.Service.Materials.ApplyAsync(calc.Id, steel.Id, temperature: 60, temperatureUnit: "MPa")).Reason, StringComparison.Ordinal);

        // A record that is not a material has no temperature to be read at.
        var other = await host.Service.SaveRecordAsync(new CalcDataRecord(Guid.Empty, "test.other", "Other", 0, "s", "e", "t", "h", "p", false, CalcDataOrigin.UserEntered, [new CalcDataValue("k", 1)], default));
        Assert.False((await host.Service.Materials.ApplyAsync(calc.Id, other.Record!.Id, temperature: 60)).Succeeded);
    }

    [Fact]
    public async Task A_Material_That_Does_Not_Fit_The_Calculation_Is_Refused_With_A_Reason()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var calc = await NewCalculationAsync(host, project);
        var onlyMu = await SaveAsync(host, Steel(title: "Friction only") with { Values = [new CalcDataValue("mu_pairs", Rows: [Row(("pair", new CalcCell(Text: "a / b")), ("mu", new CalcCell(0.3)))])] });

        var refused = await host.Service.Materials.ApplyAsync(calc.Id, onlyMu.Id);
        Assert.False(refused.Succeeded);
        Assert.Contains("does not state", refused.Reason, StringComparison.Ordinal);

        Assert.False((await host.Service.Materials.ApplyAsync(calc.Id, Guid.NewGuid())).Succeeded);
        Assert.Null(await host.Service.Materials.FindAsync(Guid.NewGuid()));
        Assert.Contains("No material", (await host.Service.Materials.GetPropertyAsync(Guid.NewGuid(), "E")).Refusal, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ the export lock

    [Fact]
    public async Task One_Licensed_Value_Makes_The_Whole_Material_Licensed_And_Its_Revisions_Stay_So()
    {
        var host = Host();
        var mixed = await SaveAsync(host, Steel() with
        {
            Values =
            [
                new CalcDataValue("E", 210, "GPa"),
                new CalcDataValue("f_y", 355, "MPa", Source: new CalcValueSource("EN standard (own copy)", "2019", "Table 7", CalcLicenceClass.LicensedSource, string.Empty)),
            ],
        });

        Assert.True(mixed.LicensedSource);
        Assert.All(mixed.Values, v => Assert.Equal(CalcLicenceClass.LicensedSource, v.Source!.Licence));

        var attempt = await host.Service.Materials.SaveAsync(mixed with { LicensedSource = false, Values = [.. mixed.Values.Select(v => v with { Source = v.Source! with { Licence = CalcLicenceClass.Public } })] });
        Assert.Null(attempt.Record);
        Assert.Contains("stay licensed", attempt.Refusal, StringComparison.Ordinal);

        var inputs = new[] { new CalcInputValue("E", Number: 210, Unit: "GPa", Provenance: MaterialLibrary.ProvenanceFor(mixed, "E", mixed.Values[0].Source!, null, "stated")) };
        var (copy, _) = await host.Service.SaveRecordFromInputsAsync(Steel() with { Title = "Copy", Kind = MaterialLibrary.Kind }, inputs);
        Assert.True(copy!.LicensedSource);
    }

    [Fact]
    public async Task Licensed_Values_Never_Leave_Through_A_Material_Export_And_Cannot_Be_Re_Imported()
    {
        var host = Host();
        var licensed = await SaveAsync(host, Steel(licensed: true, title: "Licensed steel"));
        var open = await SaveAsync(host, Steel(title: "Open steel"));

        foreach (var export in new[] { await host.Service.Materials.ExportCsvAsync(licensed.Id), (Csv: (string?)null, Refusal: (await host.Service.ExportRecordAsync(licensed.Id)).Refusal) })
        {
            Assert.Null(export.Csv);
            Assert.Contains("never exported", export.Refusal, StringComparison.Ordinal);
        }

        var json = await host.Service.Materials.ExportJsonAsync(licensed.Id);
        Assert.Null(json.Json);
        Assert.Contains("never exported", json.Refusal, StringComparison.Ordinal);
        Assert.Contains("never exported", (await host.Service.Materials.ExportJsonAsync(licensed.Id, 1)).Refusal, StringComparison.Ordinal);

        // A public material exports with the provenance of each value, and reads back as a new material.
        var csv = (await host.Service.Materials.ExportCsvAsync(open.Id)).Csv!;
        Assert.Contains("property,value,unit,text,source,edition,reference,licence,entered_by,date,evidence_sha256", csv, StringComparison.Ordinal);
        Assert.Contains("E,210,GPa,,Mill certificate 4711,rev B,\"p. 4, Table 2\",public,test.engineer,", csv, StringComparison.Ordinal);
        Assert.Contains("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", csv, StringComparison.Ordinal);
        Assert.Contains("E(T)[]", csv, StringComparison.Ordinal);

        var exported = (await host.Service.Materials.ExportJsonAsync(open.Id)).Json!;
        var (imported, refusal) = await host.Service.Materials.ImportJsonAsync(exported);
        Assert.Null(refusal);
        Assert.NotEqual(open.Id, imported!.Id);
        Assert.Equal(1, imported.Revision);
        Assert.Equal(CalcDataOrigin.UserEntered, imported.Origin);
        Assert.Equal(open.Values.Count, imported.Values.Count);
        Assert.Equal(210, imported.Values.Single(v => v.Symbol == "E").Number);

        // A file that claims a licensed value, or carries a withheld one, or is not a material, is refused.
        var smuggled = exported.Replace("\"public\"", "\"licensed_source\"", StringComparison.Ordinal);
        Assert.Contains("licensed", (await host.Service.Materials.ImportJsonAsync(smuggled)).Refusal, StringComparison.Ordinal);
        Assert.Contains("withheld", (await host.Service.Materials.ImportJsonAsync(exported.Replace("Open steel", CalcCsv.WithheldMarker, StringComparison.Ordinal))).Refusal, StringComparison.Ordinal);
        Assert.Contains("not a material file", (await host.Service.Materials.ImportJsonAsync(exported.Replace(MaterialLibrary.ExportFormat, "other/1", StringComparison.Ordinal))).Refusal, StringComparison.Ordinal);
        Assert.Contains("not valid", (await host.Service.Materials.ImportJsonAsync("{ not json")).Refusal, StringComparison.Ordinal);
        Assert.NotNull((await host.Service.Materials.ImportJsonAsync(" ")).Refusal);
    }

    [Fact]
    public async Task Values_Applied_From_A_Licensed_Material_Are_Withheld_From_Exports_And_Issued_Sheets()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var licensed = await SaveAsync(host, Steel(licensed: true, title: "Licensed steel"));
        var calc = await NewCalculationAsync(host, project);
        var applied = await host.Service.Materials.ApplyAsync(calc.Id, licensed.Id);
        Assert.True(applied.Succeeded, applied.Reason);

        var withheld = await host.Service.WithholdLicensedAsync(new CalcInputSet(applied.Instance!.Inputs));
        Assert.All(withheld.Values, v => Assert.True(v.Number is null && v.Rows is null && v.Text is null, $"{v.Symbol} left with its value"));
        Assert.All(withheld.Values, v => Assert.Contains(CalcCsv.WithheldMarker, v.Provenance!.Reference, StringComparison.Ordinal));
        Assert.All(withheld.Values, v => Assert.Equal((licensed.Id, 1), CalcDataRecord.PinOf(v.Provenance)));

        var csv = CalcCsv.ExportInputs(new MaterialUsingCalculator().Template, withheld);
        var fields = csv.Split('\n').SelectMany(line => line.Split(',')).Select(f => f.Trim().Trim('"')).ToList();
        Assert.DoesNotContain("210", fields);
        Assert.DoesNotContain("355", fields);
        Assert.DoesNotContain("0.001", fields);

        // Re-import: what was withheld is not in the file and the calculation cannot run on it.
        var back = CalcCsv.ImportInputs(new MaterialUsingCalculator().Template, csv);
        Assert.DoesNotContain(back.Inputs.Values, v => v.Number is 210 or 355);

        // The sheet of a draft and of the issued calculation show the value used, never the licensed table.
        var preview = (await host.Service.RunAsync(calc.Id))!.SheetText;
        Assert.Contains("rows withheld (licensed record)", preview, StringComparison.Ordinal);
        Assert.Contains("(licensed source)", preview, StringComparison.Ordinal);
        Assert.True((await host.Service.CheckAsync(calc.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        Assert.True((await host.Service.IssueAsync(calc.Id, "ISS-1", "Client Co")).Succeeded);
        var issued = (await host.Store.FindInstanceAsync(calc.Id))!.Issue!.SheetText;
        Assert.Contains("rows withheld (licensed record); the values used appear in the working", issued, StringComparison.Ordinal);
        Assert.Contains(licensed.Pin, issued, StringComparison.Ordinal);
        Assert.Contains("Es_max", issued, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Library_Is_The_Reference_Record_Store_Not_A_Second_One()
    {
        var host = Host();
        var steel = await SaveAsync(host, Steel());

        Assert.Equal(steel.Id, Assert.Single(await host.Store.ListRecordsAsync(MaterialLibrary.Kind), r => r.Title == steel.Title).Id);
        Assert.Contains(await host.Service.Materials.ListAsync(), m => m.Id == steel.Id);

        // A record of another kind is not a material, and the template's own kinds decide which records its panel lists.
        var other = await host.Service.SaveRecordAsync(new CalcDataRecord(Guid.Empty, "test.other", "Other", 0, "s", "e", "t", "h", "p", false, CalcDataOrigin.UserEntered, [new CalcDataValue("k", 1)], default));
        Assert.Null(await host.Service.Materials.FindAsync(other.Record!.Id));
        Assert.DoesNotContain(await host.Service.Materials.ListAsync(), m => m.Id == other.Record.Id);
    }

    [Fact]
    public void A_Template_That_Maps_An_Input_To_A_Material_Property_Has_Its_Own_Hash_And_Older_Templates_Keep_Theirs()
    {
        var with = new MaterialUsingCalculator().Template;
        var without = with with { Inputs = [.. with.Inputs.Select(i => i with { MaterialProperty = null })] };
        Assert.NotEqual(CalcTemplateHasher.CanonicalText(with, []), CalcTemplateHasher.CanonicalText(without, []));
        Assert.DoesNotContain("material=", CalcTemplateHasher.CanonicalText(without, []), StringComparison.Ordinal);
    }
}
