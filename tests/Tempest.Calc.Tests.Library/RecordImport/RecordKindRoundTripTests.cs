using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using static Tempest.Calc.Tests.Library.RecordImport.RecordImportTestSupport;

namespace Tempest.Calc.Tests.Library.RecordImport;

/// <summary>
/// DC-10 (ADR-0174): the six record kinds Track P proposed and Track S built (templates 03, 05, 19, 42, 43, 44), and the eight Track S templates
/// (83 to 90) for the kinds that had none. Each goes round the whole loop: template header → import → record → applied to the calculator that
/// declares the kind → exported (unlicensed, so the export lock lets it out) with its values. Every number below is invented for the test.
/// </summary>
public sealed class RecordKindRoundTripTests
{
    /// <summary>Imports a file of the template (unlicensed, so it can be exported), applies the first record to a new calculation of <paramref name="calculator"/>, and exports it.</summary>
    private static async Task<(RecordImportOutcome Outcome, CalcInstance Applied, string Csv)> RoundTripAsync(LibraryTestHost host, string templateId, string calculator, string title, params string[] lines)
    {
        var template = RecordTemplates.Find(templateId)!;
        var outcome = await host.Service.RecordImport.CommitAsync(Request(templateId, Csv(template.Header, lines), Provenance(title, licensed: false)));
        Assert.True(outcome.Committed, outcome.Refusal + " " + Describe(outcome.Preview));
        Assert.All(outcome.Records, r => Assert.Equal(template.Kind, r.Kind));

        var project = await host.ProjectAsync();
        var calc = await host.Service.CreateAsync(calculator, project, "Round trip " + templateId);
        var applied = await host.Service.ApplyRecordAsync(calc.Id, outcome.Records[0].Id);
        Assert.True(applied.Succeeded, applied.Reason);

        var (csv, refusal) = await host.Service.ExportRecordAsync(outcome.Records[0].Id);
        Assert.Null(refusal);
        return (outcome, applied.Instance!, csv!);
    }

    [Fact]
    public async Task Template_03_Maximum_Loop_Impedances_Make_One_Table_Record_That_E5_Checks_Z_s_Against()
    {
        var host = Host();
        var (outcome, applied, csv) = await RoundTripAsync(host, "03", "elec.fault-protection", "Max Zs (test)", "Type B,0.4,32,1.37", "Type B,0.4,40,1.1", "Type B,5,32,2.2");
        var record = Assert.Single(outcome.Records);
        var table = Assert.Single(record.Values);
        Assert.Equal("Z_s_max_table", table.Symbol);
        Assert.Equal(3, table.Rows!.Count);
        Assert.Equal("Type B", table.Rows[0].Get("device")!.Text);
        Assert.Equal(1.37, table.Rows[0].Get("Z_s_max")!.Number);
        Assert.Equal("ohm", table.Rows[0].Get("Z_s_max")!.Unit);
        Assert.Contains("Z_s_max=1.37", csv, StringComparison.Ordinal);

        // Applied to the fault-protection sheet (32 A type B, t_req 0.4 s): the row is read and Z_s is checked against it.
        Assert.Equal(3, applied.Inputs.Single(i => i.Symbol == "Z_s_max_table").Rows!.Count);
        var run = await host.Service.RunAsync(applied);
        Assert.Equal(1.37, run.Result.OutputSi("Z_s_max_tab"), 9);
        Assert.Contains(run.Result.Checks, c => c.Name == "Loop impedance against the tabulated maximum");

        // A rating the table lacks is refused with the reason.
        var wrongRating = await host.Service.RecordImport.CommitAsync(Request("03", Csv(RecordTemplates.Find("03")!.Header, "Type B,0.4,32,1.37", "Type B,0.4,20,1.37"), Provenance("Max Zs rising", licensed: false)));
        Assert.False(wrongRating.Committed);
        Assert.Contains(wrongRating.Preview.AllIssues, i => i.Message.Contains("Z_s_max must fall as I_n rises", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Template_05_Voltage_Drop_Figures_Make_One_Table_Record_That_E3_Reads_The_Figure_From()
    {
        var host = Host();
        var (outcome, applied, csv) = await RoundTripAsync(host, "05", "elec.voltage-drop", "mV/A/m (test)", "copper,10,4.4,,,,3.8,,3.8", "copper,16,2.8,,,,2.4,,2.4", "copper,25,1.75,,,,1.5,,1.5");
        var table = Assert.Single(Assert.Single(outcome.Records).Values);
        Assert.Equal("mVAm_table", table.Symbol);
        Assert.Equal(3, table.Rows!.Count);
        Assert.Equal("mV/A/m", table.Rows[1].Get("z_3ph")!.Unit);
        Assert.Null(table.Rows[1].Get("z_1ph"));
        Assert.Contains("csa=16", csv, StringComparison.Ordinal);

        // On the mV/A/m route with A_c 16 mm² the sheet reads 2.4 from the row and gives the ka-mvam numbers.
        var inputs = applied.Inputs.Where(i => i.Symbol is not ("mVAm" or "A_c" or "data")).ToList();
        inputs.AddRange([CalcInputValue.Choice("data", Tempest.Calc.Electrical.VoltageDropCalculator.DataMvAm), CalcInputValue.Quantity("A_c", 16, "mm^2"), CalcInputValue.Quantity("I", 63, "A"), CalcInputValue.Quantity("L", 50, "m"), CalcInputValue.Quantity("V_n", 400, "V"), CalcInputValue.Of("n_par", 1)]);
        var updated = await host.Service.UpdateInputsAsync(applied.Id, inputs, applied.UpdatedAt);
        Assert.True(updated.Succeeded, updated.Reason);
        var run = await host.Service.RunAsync(updated.Instance!);
        Assert.Equal(2.4, run.Result.OutputSi("mVAm"), 9);
        Assert.Equal(7.56, run.Result.OutputSi("dV"), 9);

        // The figures must fall as the cross-section rises: a rising one is refused with both rows named.
        var rising = await host.Service.RecordImport.CommitAsync(Request("05", Csv(RecordTemplates.Find("05")!.Header, "copper,10,4.4,,,,3.8,,3.8", "copper,16,2.8,,,,2.4,,4.1"), Provenance("mV/A/m rising", licensed: false)));
        Assert.False(rising.Committed);
        Assert.Contains(rising.Preview.AllIssues, i => i.Message.Contains("z_3ph must fall as csa rises", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Template_19_Concrete_Strengths_Make_One_Record_Per_Class_That_S6_Takes_f_ck_From()
    {
        var host = Host();
        var (outcome, applied, csv) = await RoundTripAsync(host, "19", "struct.base-plate", "Concrete (test)", "class A,22,27", "class B,33,41");
        Assert.Equal(2, outcome.Records.Count);
        var first = outcome.Records.Single(r => r.Title.Contains("class A", StringComparison.Ordinal));
        Assert.Equal(22, first.Values.Single(v => v.Symbol == "f_ck").Number);
        Assert.Equal("MPa", first.Values.Single(v => v.Symbol == "f_ck").Unit);
        Assert.Equal(27, first.Values.Single(v => v.Symbol == "f_ck_cube").Number);   // stored, never applied
        Assert.Contains("f_ck,22,MPa", csv, StringComparison.Ordinal);
        var fck = applied.Inputs.Single(i => i.Symbol == "f_ck");
        Assert.Equal(22, fck.Number);
        Assert.Equal(outcome.Records[0].Id, CalcDataRecord.PinOf(fck.Provenance)!.Value.Id);
        Assert.DoesNotContain(applied.Inputs, i => i.Symbol == "f_ck_cube");

        // A cube strength under the cylinder strength is refused.
        var swapped = await host.Service.RecordImport.CommitAsync(Request("19", Csv(RecordTemplates.Find("19")!.Header, "class C,30,25"), Provenance("Concrete swapped", licensed: false)));
        Assert.False(swapped.Committed);
    }

    [Fact]
    public async Task Templates_42_And_43_Fill_T_int_And_n_Through_The_Column_Map()
    {
        // Track P wrote the columns as theta_i and n_ach; B4 takes T_int and n. The map binds them without renaming the file's header.
        var host = Host();
        var (temperatures, appliedT, csvT) = await RoundTripAsync(host, "42", "bs.room-heat-loss", "Room temperatures (test)", "Lounge,21", "Bathroom,22");
        Assert.Equal(2, temperatures.Records.Count);
        var lounge = temperatures.Records.Single(r => r.Title.Contains("Lounge", StringComparison.Ordinal));
        Assert.Equal("T_int", Assert.Single(lounge.Values, v => v.Number is not null).Symbol);
        Assert.Equal(21, lounge.Values.Single(v => v.Symbol == "T_int").Number);
        Assert.Equal("degC", lounge.Values.Single(v => v.Symbol == "T_int").Unit);
        Assert.Contains("T_int,21,degC", csvT, StringComparison.Ordinal);
        Assert.Equal(21, appliedT.Inputs.Single(i => i.Symbol == "T_int").Number);
        Assert.NotNull(CalcDataRecord.PinOf(appliedT.Inputs.Single(i => i.Symbol == "T_int").Provenance));

        var (rates, appliedN, csvN) = await RoundTripAsync(host, "43", "bs.room-heat-loss", "Air change rates (test)", "Kitchen,1.5");
        var kitchen = Assert.Single(rates.Records);
        Assert.Equal(1.5, kitchen.Values.Single(v => v.Symbol == "n").Number);
        Assert.Equal("1/h", kitchen.Values.Single(v => v.Symbol == "n").Unit);
        Assert.Contains("n,1.5,1/h", csvN, StringComparison.Ordinal);
        Assert.Equal(1.5, appliedN.Inputs.Single(i => i.Symbol == "n").Number);

        // The calculator's limits judge the mapped column: a rate above B4's hard limit is refused as the calculator would refuse it.
        var tooHigh = await host.Service.RecordImport.CommitAsync(Request("43", Csv(RecordTemplates.Find("43")!.Header, "Plant room,5"), Provenance("Air change too high", licensed: false)));
        Assert.False(tooHigh.Committed);
        Assert.Contains(tooHigh.Preview.AllIssues, i => i.Message.StartsWith("n_ach: the value is outside what", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Template_44_Ground_U_Values_Make_One_Record_Per_Row_With_U_g_Applied_And_The_Rest_Stored()
    {
        var host = Host();
        var (outcome, applied, csv) = await RoundTripAsync(host, "44", "bs.room-heat-loss", "Ground floor U (test)", "slab,1.5,4,1,0.4", "slab,1.5,8,1,0.3");
        Assert.Equal(2, outcome.Records.Count);
        var first = outcome.Records.Single(r => r.Title.EndsWith("row 1]", StringComparison.Ordinal));
        Assert.Equal(0.4, first.Values.Single(v => v.Symbol == "U_g").Number);
        Assert.Equal("W/(m^2*K)", first.Values.Single(v => v.Symbol == "U_g").Unit);
        Assert.Equal(4, first.Values.Single(v => v.Symbol == "B_prime").Number);
        Assert.Equal("slab", first.Values.Single(v => v.Symbol == "construction").Text);
        Assert.Contains("U_g,0.4,W/(m^2*K)", csv, StringComparison.Ordinal);
        Assert.Equal(0.4, applied.Inputs.Single(i => i.Symbol == "U_g").Number);
        Assert.DoesNotContain(applied.Inputs, i => i.Symbol is "B_prime" or "lambda_g" or "R_f");

        // Within one construction and insulation the U-value must fall as the characteristic dimension rises.
        var rising = await host.Service.RecordImport.CommitAsync(Request("44", Csv(RecordTemplates.Find("44")!.Header, "slab,1.5,4,1,0.3", "slab,1.5,8,1,0.4"), Provenance("Ground U rising", licensed: false)));
        Assert.False(rising.Committed);
        Assert.Contains(rising.Preview.AllIssues, i => i.Message.Contains("U_g must fall as B_prime rises", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("83", "elec.motor-sizing", "Motors (test)", "M-test,0.00021,2.1,6.3,3000,6000", "J_motor", 0.00021, "kg*m^2")]
    [InlineData("84", "elec.motor-start", "Cables (test)", "4 mm2 (test),4.61,0.09", "R_km", 4.61, "ohm/km")]
    [InlineData("85", "elec.motor-start", "Starting (test)", "M-test,6.5,0.28", "I_LR_r", 6.5, null)]
    [InlineData("86", "elec.motor-start", "Starters (test)", "SS-test,3.2,", "k_soft", 3.2, null)]
    [InlineData("87", "elec.motor-start", "Transformers (test)", "TX-test,4.5,5.1", "u_k", 4.5, "%")]
    public async Task Templates_83_To_87_Make_One_Record_Per_Row_For_The_Motor_Kinds(string templateId, string calculator, string title, string row, string symbol, double value, string? unit)
    {
        var host = Host();
        var (outcome, applied, csv) = await RoundTripAsync(host, templateId, calculator, title, row);
        var record = Assert.Single(outcome.Records);
        var stored = record.Values.Single(v => v.Symbol == symbol);
        Assert.Equal(value, stored.Number);
        Assert.Equal(unit, stored.Unit);
        Assert.Contains(symbol + "," + value.ToString(System.Globalization.CultureInfo.InvariantCulture), csv, StringComparison.Ordinal);
        var input = applied.Inputs.Single(i => i.Symbol == symbol);
        Assert.Equal(value, input.Number);
        Assert.Equal(record.Id, CalcDataRecord.PinOf(input.Provenance)!.Value.Id);
    }

    [Fact]
    public async Task Template_86_Needs_One_Of_The_Two_Limits_And_Template_83_Refuses_A_Rated_Speed_Above_The_Maximum()
    {
        var host = Host();
        var neither = await host.Service.RecordImport.CommitAsync(Request("86", Csv(RecordTemplates.Find("86")!.Header, "SS-test,,"), Provenance("Starter empty", licensed: false)));
        Assert.False(neither.Committed);
        var fast = await host.Service.RecordImport.CommitAsync(Request("83", Csv(RecordTemplates.Find("83")!.Header, "M-test,0.00021,2.1,6.3,7000,6000"), Provenance("Motor fast", licensed: false)));
        Assert.False(fast.Committed);
        Assert.Contains(fast.Preview.AllIssues, i => i.Message.Contains("n_rated", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("88", "Lifting factors (test)", "Cat A,Class 1,2.2,1.1", "Nd", 2.2)]
    [InlineData("89", "Lifting allowables (test)", "approach A,1.8,2.4,1.25", "n_u", 2.4)]
    [InlineData("90", "Lifting limits (test)", "owner procedure,20,1.25", "k_proof", 1.25)]
    public async Task Templates_88_To_90_Fill_The_Lifting_Kinds_When_The_Lifting_Flag_Is_On(string templateId, string title, string row, string symbol, double value)
    {
        var host = HostWithLifting();
        var (outcome, applied, csv) = await RoundTripAsync(host, templateId, Tempest.Calc.Structural.LiftingLugCalculator.Id, title, row);
        var record = Assert.Single(outcome.Records);
        Assert.Equal(value, record.Values.Single(v => v.Symbol == symbol).Number);
        Assert.Contains(symbol + "," + value.ToString(System.Globalization.CultureInfo.InvariantCulture), csv, StringComparison.Ordinal);
        Assert.Equal(value, applied.Inputs.Single(i => i.Symbol == symbol).Number);

        if (templateId == "88")
        {
            // The category and class are scalars of the kind (the labels the sheet prints), stored as text and applied with the factors.
            Assert.Equal("Cat A", record.Values.Single(v => v.Symbol == "design_category").Text);
            Assert.Equal("Cat A", applied.Inputs.Single(i => i.Symbol == "design_category").Text);
        }
    }

    [Fact]
    public async Task A_Licensed_Table_Record_Of_A_New_Kind_Fills_The_Sheet_And_Its_Values_Never_Leave_Through_An_Export()
    {
        // The same loop as the unlicensed round trips, on a record imported as licensed (the default): the sheet reads the table, the export
        // refuses the record, and the withheld input set carries no row and says why. The values are invented.
        var host = Host();
        var template = RecordTemplates.Find("03")!;
        var outcome = await host.Service.RecordImport.CommitAsync(Request("03", Csv(template.Header, "Type B,0.4,32,1.37", "Type B,0.4,40,1.1"), Provenance("Max Zs (licensed test)")));
        Assert.True(outcome.Committed, outcome.Refusal + " " + Describe(outcome.Preview));
        var record = Assert.Single(outcome.Records);
        Assert.True(record.LicensedSource);

        var project = await host.ProjectAsync();
        var calc = await host.Service.CreateAsync("elec.fault-protection", project, "Licensed round trip 03");
        var applied = await host.Service.ApplyRecordAsync(calc.Id, record.Id);
        Assert.True(applied.Succeeded, applied.Reason);
        var run = await host.Service.RunAsync(applied.Instance!);
        Assert.Equal(1.37, run.Result.OutputSi("Z_s_max_tab"), 9);

        var (csv, refusal) = await host.Service.ExportRecordAsync(record.Id);
        Assert.Null(csv);
        Assert.Contains("never exported", refusal, StringComparison.Ordinal);

        var withheld = await host.Service.WithholdLicensedAsync(new CalcInputSet(applied.Instance!.Inputs));
        var table = withheld.Find("Z_s_max_table")!;
        Assert.Null(table.Rows);
        Assert.Contains("licensed value withheld", table.Provenance!.Reference, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Schemas_Of_The_New_Kinds_Carry_The_Units_And_Columns_The_Calculators_Take()
    {
        var schemas = HostWithLifting().Service.RecordImport.Schemas();

        var zs = schemas["bs7671.max-zs"];
        var zsTable = Assert.Single(zs.Symbols);
        Assert.True(zsTable.IsTable);
        Assert.Equal(["device", "t_disc", "I_n", "Z_s_max"], zsTable.Columns.Select(c => c.Key));
        Assert.Equal("ohm", zsTable.Column("Z_s_max")!.Unit);
        Assert.Equal("elec.fault-protection", Assert.Single(zs.Users).TemplateId);

        var drop = schemas["bs7671.voltage-drop"];
        var dropTable = Assert.Single(drop.Symbols);
        Assert.Equal(9, dropTable.Columns.Count);
        Assert.All(dropTable.Columns.Where(c => c.Key is not ("conductor" or "csa")), c => Assert.Equal("mV/A/m", c.Unit));

        Assert.Equal("MPa", Assert.Single(schemas["concrete.strengths"].Symbols).Unit);
        Assert.Equal("degC", Assert.Single(schemas["room.internal-temperatures"].Symbols).Unit);
        Assert.Equal("1/h", Assert.Single(schemas["room.air-change-rates"].Symbols).Unit);
        Assert.Equal("W/(m^2*K)", Assert.Single(schemas["ground.u-values"].Symbols).Unit);
        Assert.Equal(["J_motor", "T_rated", "T_peak", "n_rated", "n_max"], schemas["motor.rating"].Symbols.Select(s => s.Symbol));
        Assert.Equal(["R_km", "X_km"], schemas["cable.impedance"].Symbols.Select(s => s.Symbol));
        Assert.Equal(["I_LR_r", "cos_phi_st"], schemas["motor.start"].Symbols.Select(s => s.Symbol));
        Assert.Equal(["k_soft", "k_vfd"], schemas["starter.limits"].Symbols.Select(s => s.Symbol));
        Assert.Equal(["u_k", "XR"], schemas["transformer.data"].Symbols.Select(s => s.Symbol));
        Assert.Equal(["design_category", "service_class", "Nd", "k_svc"], schemas["lifting.design-factors"].Symbols.Select(s => s.Symbol));
        Assert.Equal(["n_y", "n_u", "k_b"], schemas["lifting.allowable-stress"].Symbols.Select(s => s.Symbol));
        Assert.Equal(["dish_max", "k_proof"], schemas["lifting.limits"].Symbols.Select(s => s.Symbol));
    }
}
