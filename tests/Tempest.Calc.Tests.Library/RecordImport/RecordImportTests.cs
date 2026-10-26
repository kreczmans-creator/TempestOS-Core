using System.Security.Cryptography;
using System.Text;
using Tempest.Calc.Import;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using static Tempest.Calc.Tests.Library.RecordImport.RecordImportTestSupport;

namespace Tempest.Calc.Tests.Library.RecordImport;

/// <summary>
/// Addendum C, C-3: the record import. Every number in these files is invented for the test (they match no published table); the point is the
/// mechanism: the hardened reader, the column mapping, the unit check, the row errors, the all-or-nothing commit, the provenance stamp and the
/// export lock, which the import leaves exactly as it was.
/// </summary>
public sealed class RecordImportTests
{
    private const string Ampacity = "CSA[mm2],AWG[-],I_t[A]";
    private const string AmbientFactors = "medium[text],insulation[text],theta_a[degC],C_a[-]";

    // ------------------------------------------------------------------
    // The happy path and the provenance stamp
    // ------------------------------------------------------------------

    [Fact]
    public async Task An_Ampacity_Table_Is_Imported_As_One_Record_With_Its_Provenance_Stamped_And_Licensed_By_Default()
    {
        var host = Host();
        var bytes = Csv(Ampacity, "1.5,,15", "2.5,,20", "4,,27");
        var request = Request("04", bytes, Provenance(amendment: "A2:2022"));

        var preview = await host.Service.RecordImport.PreviewAsync(request);
        Assert.True(preview.CanCommit, Describe(preview));
        Assert.Equal("cable.ampacity", Assert.Single(preview.Planned).Kind);
        Assert.Equal(3, preview.Planned[0].Rows);
        Assert.Equal(["CSA", "AWG", "I_t"], preview.Columns.Select(c => c.Symbol));
        Assert.All(preview.Columns, c => Assert.Equal(RecordColumnRole.TableColumn, c.Role));
        Assert.Empty(await host.Store.ListRecordsAsync());

        var before = DateTimeOffset.UtcNow;
        var outcome = await host.Service.RecordImport.CommitAsync(request);
        Assert.True(outcome.Committed, outcome.Refusal);
        var record = Assert.Single(outcome.Records);
        var stored = (await host.Store.FindRecordAsync(record.Id))!;

        Assert.Equal(1, stored.Revision);
        Assert.Equal("cable.ampacity", stored.Kind);
        Assert.Equal("Test table", stored.Title);
        Assert.Equal("Test Wiring Rules", stored.SourceTitle);
        Assert.Equal("2018; A2:2022", stored.Edition);
        Assert.Equal("Table 4D2A", stored.TableOrClause);
        Assert.Equal("Own copy (test)", stored.LicenceHolder);
        Assert.Equal("test.engineer", stored.EnteredBy);
        Assert.True(stored.LicensedSource, "licensed_source is on by default");
        Assert.True(stored.IsLicensed());
        Assert.InRange(stored.CreatedAt, before.AddSeconds(-5), DateTimeOffset.UtcNow.AddSeconds(5));

        var value = Assert.Single(stored.Values);
        Assert.Equal("ampacity", value.Symbol);
        Assert.Equal(3, value.Rows!.Count);
        Assert.Equal(1.5, value.Rows[0].Cells["CSA"].Number);
        Assert.Equal("mm^2", value.Rows[0].Cells["CSA"].Unit);
        Assert.Equal("A", value.Rows[2].Cells["I_t"].Unit);
        Assert.DoesNotContain("AWG", value.Rows[0].Cells.Keys);

        // Each value carries its own stamp: source, edition and amendment, table, licence, who, when, and the file's SHA-256.
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        Assert.Equal(sha, value.Source!.EvidenceSha256);
        Assert.Equal(CalcLicenceClass.LicensedSource, value.Source.Licence);
        Assert.Equal("test.engineer", value.Source.EnteredBy);
        Assert.Equal("2018; A2:2022", value.Source.Edition);
        Assert.Equal("Table 4D2A", value.Source.Reference);
        Assert.Contains("test.csv", stored.Notes, StringComparison.Ordinal);
        Assert.Contains(sha, stored.Notes, StringComparison.Ordinal);
        Assert.Contains("04-bs7671.ampacity", stored.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Commit_Writes_The_Whole_Set_In_One_Transaction()
    {
        var host = Host();
        var before = host.Failing.BodiesCompleted;
        var outcome = await host.Service.RecordImport.CommitAsync(Request("06", Csv(AmbientFactors, "air,PVC,20,0.99", "air,PVC,30,0.9", "air,PVC,40,0.8")));

        Assert.True(outcome.Committed, outcome.Refusal);
        Assert.Equal(3, outcome.Records.Count);
        Assert.Equal(before + 1, host.Failing.BodiesCompleted);
        Assert.Equal(3, (await host.Store.ListRecordsAsync("cable.correction-factors")).Count);
    }

    [Fact]
    public async Task A_Table_Template_With_Scalar_Columns_Reads_Them_From_The_First_Row_And_Keeps_The_Curve_Rows()
    {
        var host = Host();
        var file = Csv("I2t_let[A2*s],I_cn[kA],t[s],I[A]", "50000,10,0.1,500", ",,1,200", ",,10,100");
        var outcome = await host.Service.RecordImport.CommitAsync(Request("02", file, Provenance("Synthetic fuse")));

        Assert.True(outcome.Committed, outcome.Refusal);
        var record = Assert.Single(outcome.Records);
        Assert.Equal(50000, record.Values.Single(v => v.Symbol == "I2t_let").Number);
        Assert.Equal("A^2*s", record.Values.Single(v => v.Symbol == "I2t_let").Unit);
        Assert.Equal(10, record.Values.Single(v => v.Symbol == "I_cn").Number);
        Assert.Equal(3, record.Values.Single(v => v.Symbol == "curve").Rows!.Count);
    }

    [Fact]
    public async Task A_Per_Row_Template_Saves_One_Record_Per_Row_And_Keeps_The_Labels_In_The_Title_And_The_Extras_As_Stored_Values()
    {
        var host = Host();
        var outcome = await host.Service.RecordImport.CommitAsync(Request("06", Csv(AmbientFactors, "air,PVC,20,0.99", "air,PVC,30,0.9"), Provenance("Ambient (test)")));

        Assert.True(outcome.Committed, outcome.Refusal);
        Assert.Equal(["Ambient (test) [air, PVC, theta_a 20 degC]", "Ambient (test) [air, PVC, theta_a 30 degC]"], outcome.Records.Select(r => r.Title).Order());
        var first = outcome.Records.Single(r => r.Title.Contains("theta_a 20", StringComparison.Ordinal));
        Assert.Equal(0.99, first.Values.Single(v => v.Symbol == "C_a").Number);
        Assert.Equal(20, first.Values.Single(v => v.Symbol == "theta_a").Number);
        Assert.Equal("degC", first.Values.Single(v => v.Symbol == "theta_a").Unit);
        Assert.Equal("PVC", first.Values.Single(v => v.Symbol == "insulation").Text);
    }

    [Fact]
    public async Task A_Record_Of_A_Scalar_Template_Holds_Its_Values_With_The_Units_As_Typed_And_Applies_To_A_Calculation()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var outcome = await host.Service.RecordImport.CommitAsync(Request("24", Csv("designation[text],C[kN],C_0[kN]", "TEST-1,20.3,11.2"), Provenance("Test bearing ratings")));
        Assert.True(outcome.Committed, outcome.Refusal);

        var calc = await host.Service.CreateAsync("mech.bearing-life", project, "Pump bearing");
        var applied = await host.Service.ApplyRecordAsync(calc.Id, outcome.Records[0].Id);
        Assert.True(applied.Succeeded, applied.Reason);
        var c = applied.Instance!.Inputs.Single(i => i.Symbol == "C");
        Assert.Equal(20.3, c.Number);
        Assert.Equal("kN", c.Unit);
        Assert.Equal(outcome.Records[0].Id, CalcDataRecord.PinOf(c.Provenance)!.Value.Id);
    }

    [Fact]
    public async Task A_Table_Record_Applies_To_The_Calculator_That_Needs_It_And_Clears_The_Blocking_Notice()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var ampacity = await host.Service.RecordImport.CommitAsync(Request("04", Csv(Ampacity, "1.5,,15", "2.5,,20", "4,,27", "6,,34"), Provenance("Synthetic ampacity")));
        Assert.True(ampacity.Committed, ampacity.Refusal);

        var calc = await host.Service.CreateAsync("elec.conductor-sizing", project, "Feeder");
        var applied = await host.Service.ApplyRecordAsync(calc.Id, ampacity.Records[0].Id);
        Assert.True(applied.Succeeded, applied.Reason);
        var input = applied.Instance!.Inputs.Single(i => i.Symbol == "ampacity");
        Assert.Equal(4, input.Rows!.Count);
        Assert.Equal(ampacity.Records[0].Id, CalcDataRecord.PinOf(input.Provenance)!.Value.Id);
    }

    [Fact]
    public async Task A_Material_Is_Imported_Per_Row_And_A_Temperature_Table_Is_Added_To_It_As_Its_Next_Revision()
    {
        var host = Host();
        var materials = Csv("material[text],E[GPa],G[GPa],nu[-],rho[kg/m3],alpha[1/K],lambda[W/(m*K)],f_y[MPa],f_u[MPa],Rm_A[MPa],Rm_m[-],eps_perm_once[-],eps_perm_repeated[-]",
            "Test alloy,200,77,0.3,7800,,,,,,,,", "Test plastic,3,,0.4,1200,,,,,,,0.05,0.02");
        var made = await host.Service.RecordImport.CommitAsync(Request("62", materials, Provenance(title: string.Empty)));
        Assert.True(made.Committed, made.Refusal);
        Assert.Equal(["Test alloy", "Test plastic"], made.Records.Select(r => r.Title).Order());
        Assert.Equal(1, made.Records[0].Revision);
        Assert.True(made.Records.All(r => r.IsLicensed()));

        var tables = Csv("material[text],property[text],T[degC],v[-],v_unit[text]", "Test alloy,E,20,200,GPa", "Test alloy,E,100,195,GPa", "Test alloy,E,200,185,GPa");
        var added = await host.Service.RecordImport.CommitAsync(Request("63", tables, Provenance(title: string.Empty)));
        Assert.True(added.Committed, added.Refusal);
        var alloy = Assert.Single(added.Records);
        Assert.Equal(2, alloy.Revision);
        Assert.Equal(made.Records.Single(r => r.Title == "Test alloy").Id, alloy.Id);
        Assert.Equal(["E", "E(T)", "G", "nu", "rho"], alloy.Values.Select(v => v.Symbol).Order(StringComparer.Ordinal));
        Assert.Equal(1, (await host.Store.FindRecordAsync(alloy.Id, 1))!.Revision);
        Assert.DoesNotContain(alloy.Values, v => v.Symbol == "E(T)" && v.Rows is null);

        var (reading, refusal) = MaterialLibrary.GetProperty(alloy, "E", 150, "degC");
        Assert.Null(refusal);
        Assert.InRange(reading!.Si, 189.9e9, 190.1e9);
    }

    [Fact]
    public async Task A_Table_Typed_On_The_Screen_Has_No_File_Hash_And_Says_It_Was_Typed()
    {
        var host = Host();
        var outcome = await host.Service.RecordImport.CommitAsync(Request("47", Csv("I_n[A]", "6", "10", "16"), Provenance("Typed ratings"), fileName: null));

        Assert.True(outcome.Committed, outcome.Refusal);
        var record = Assert.Single(outcome.Records);
        Assert.Null(record.Values[0].Source!.EvidenceSha256);
        Assert.Contains("Typed on the records screen", record.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Number_Format_Is_Declared_Never_Guessed_And_A_Decimal_Comma_File_Is_Read_Under_It()
    {
        var host = Host();
        var file = Utf8("CSA[mm2];AWG[-];I_t[A]\r\n2,5;;20,5\r\n4;;27\r\n");

        var wrong = await host.Service.RecordImport.PreviewAsync(Request("04", file));
        Assert.False(wrong.CanCommit);
        Assert.Contains(wrong.Errors, e => e.Message.Contains("decimal mark", StringComparison.Ordinal) && e.Line == 2);
        Assert.Contains(wrong.Warnings, w => w.Message.Contains("semicolon", StringComparison.Ordinal) || w.Message.Contains(";", StringComparison.Ordinal));

        var right = await host.Service.RecordImport.CommitAsync(Request("04", file) with { Number = NumberProfile.Comma });
        Assert.True(right.Committed, right.Refusal);
        Assert.Equal(2.5, right.Records[0].Values[0].Rows![0].Cells["CSA"].Number);
        Assert.Equal(20.5, right.Records[0].Values[0].Rows![0].Cells["I_t"].Number);
    }

    // ------------------------------------------------------------------
    // Malformed files
    // ------------------------------------------------------------------

    [Fact]
    public async Task A_Malformed_Csv_Is_Refused_Whole_With_The_Reason_And_The_Line_And_Nothing_Is_Saved()
    {
        var host = Host();
        var service = host.Service.RecordImport;

        var openQuote = await service.CommitAsync(Request("04", Utf8("CSA[mm2],AWG[-],I_t[A]\r\n1.5,,15\r\n\"2.5,,20\r\n")));
        Assert.False(openQuote.Committed);
        Assert.Contains("never closed", openQuote.Refusal, StringComparison.Ordinal);
        Assert.Contains("line 3", openQuote.Refusal, StringComparison.Ordinal);

        var strayQuote = await service.PreviewAsync(Request("04", Utf8("CSA[mm2],AWG[-],I_t[A]\r\n1.5,,1\"5\r\n")));
        Assert.Contains("quote appears inside an unquoted field", strayQuote.Refusal, StringComparison.Ordinal);

        var utf16 = await service.PreviewAsync(Request("04", Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("CSA[mm2],AWG[-],I_t[A]\r\n")).ToArray()));
        Assert.Contains("UTF-16", utf16.Refusal, StringComparison.Ordinal);

        var control = await service.PreviewAsync(Request("04", Utf8("CSA[mm2],AWG[-],I_t[A]\r\n1.5,,1\u00005\r\n")));
        Assert.Contains("control character", control.Refusal, StringComparison.Ordinal);

        var empty = await service.PreviewAsync(Request("04", []));
        Assert.Contains("empty", empty.Refusal, StringComparison.Ordinal);

        Assert.Empty(await host.Store.ListRecordsAsync());
    }

    [Fact]
    public async Task A_Row_With_The_Wrong_Number_Of_Fields_Is_An_Error_On_Its_Line_And_Blocks_The_Commit()
    {
        var host = Host();
        var preview = await host.Service.RecordImport.PreviewAsync(Request("04", Csv(Ampacity, "1.5,,15", "2.5,20", "4,,27")));

        Assert.False(preview.CanCommit);
        var row = preview.Rows.Single(r => r.HasError);
        Assert.Equal(3, row.Line);
        Assert.Contains("2 field(s) and the header has 3", row.Issues[0].Message, StringComparison.Ordinal);
        Assert.Empty(preview.Planned);
    }

    [Fact]
    public async Task A_Cell_That_Is_Not_A_Number_Is_A_Row_Error_And_A_Cell_That_Starts_Like_A_Formula_Is_Refused()
    {
        var host = Host();
        var service = host.Service.RecordImport;

        var text = await service.PreviewAsync(Request("04", Csv(Ampacity, "1.5,,15", "2.5,,twenty")));
        Assert.Contains(text.Errors, e => e.Line == 3 && e.Column == "I_t");

        var na = await service.PreviewAsync(Request("04", Csv(Ampacity, "1.5,,n/a")));
        Assert.Contains(na.Errors, e => e.Column == "I_t");

        var formula = await service.PreviewAsync(Request("06", Csv(AmbientFactors, "=1+1,PVC,20,0.99")));
        Assert.Contains(formula.Errors, e => e.Column == "medium" && e.Message.Contains("formula", StringComparison.Ordinal));

        var minus = await service.PreviewAsync(Request("06", Csv(AmbientFactors, "-air,PVC,20,0.99")));
        Assert.Contains(minus.Errors, e => e.Column == "medium");
    }

    [Fact]
    public async Task A_Header_That_Is_Not_A_Column_Of_The_Template_Is_An_Error_Until_It_Is_Mapped_Or_Left_Out()
    {
        var host = Host();
        var service = host.Service.RecordImport;
        var file = Csv("Area[mm2],AWG[-],I_t[A],comment[text]", "1.5,,15,first");

        var raw = await service.PreviewAsync(Request("04", file));
        Assert.False(raw.CanCommit);
        Assert.Contains(raw.Errors, e => e.Column == "Area" && e.Message.Contains("not a column of template", StringComparison.Ordinal));
        Assert.Contains(raw.Errors, e => e.Column == "comment");
        Assert.Contains(raw.Errors, e => e.Column == "CSA" && e.Message.Contains("no column", StringComparison.Ordinal) || e.Message.Contains("CSA", StringComparison.Ordinal));

        var mapped = Request("04", file) with { ColumnMap = new Dictionary<string, string?> { ["Area"] = "CSA", ["comment"] = null } };
        var preview = await service.PreviewAsync(mapped);
        Assert.True(preview.CanCommit, Describe(preview));
        Assert.Equal(RecordColumnRole.Ignored, preview.Columns.Single(c => c.Symbol == "comment").Role);
        Assert.Equal("ampacity.CSA", preview.Columns.Single(c => c.Symbol == "Area").Target);
        Assert.True((await service.CommitAsync(mapped)).Committed);
    }

    [Fact]
    public async Task A_Header_Named_Twice_Or_Mapped_Twice_Is_An_Error()
    {
        var host = Host();
        var service = host.Service.RecordImport;

        var twice = await service.PreviewAsync(Request("04", Csv("CSA[mm2],CSA[mm2],I_t[A]", "1,2,3")));
        Assert.Contains(twice.Errors, e => e.Message.Contains("twice", StringComparison.Ordinal));

        var mappedTwice = await service.PreviewAsync(Request("04", Csv("a[mm2],b[mm2],I_t[A]", "1,2,3")) with { ColumnMap = new Dictionary<string, string?> { ["a"] = "CSA", ["b"] = "CSA" } });
        Assert.Contains(mappedTwice.Errors, e => e.Message.Contains("Two columns", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------
    // Units
    // ------------------------------------------------------------------

    [Fact]
    public async Task A_Unit_That_Measures_Something_Else_Is_A_Mismatch_And_Blocks_The_Import()
    {
        var host = Host();
        var service = host.Service.RecordImport;

        var preview = await service.PreviewAsync(Request("04", Csv("CSA[mm2],AWG[-],I_t[V]", "1.5,,15")));
        Assert.False(preview.CanCommit);
        var column = preview.Columns.Single(c => c.Symbol == "I_t");
        Assert.Equal(RecordUnitCheck.Mismatch, column.Unit);
        Assert.Contains("does not measure the same quantity", column.UnitMessage, StringComparison.Ordinal);
        Assert.Contains(preview.Errors, e => e.Column == "I_t");
        Assert.Equal(RecordUnitCheck.Match, preview.Columns.Single(c => c.Symbol == "CSA").Unit);

        var outcome = await service.CommitAsync(Request("04", Csv("CSA[mm2],AWG[-],I_t[V]", "1.5,,15")));
        Assert.False(outcome.Committed);
        Assert.Empty(await host.Store.ListRecordsAsync());
    }

    [Fact]
    public async Task A_Missing_Or_Unknown_Unit_And_A_Unit_On_A_Plain_Number_Or_A_Text_Column_Are_Refused()
    {
        var host = Host();
        var service = host.Service.RecordImport;

        var missing = await service.PreviewAsync(Request("04", Csv("CSA[mm2],AWG[-],I_t", "1.5,,15")));
        Assert.Equal(RecordUnitCheck.Missing, missing.Columns.Single(c => c.Symbol == "I_t").Unit);
        Assert.Contains("not read as SI", missing.Columns.Single(c => c.Symbol == "I_t").UnitMessage, StringComparison.Ordinal);

        var unknown = await service.PreviewAsync(Request("04", Csv("CSA[mm2],AWG[-],I_t[furlong]", "1.5,,15")));
        Assert.Equal(RecordUnitCheck.Unknown, unknown.Columns.Single(c => c.Symbol == "I_t").Unit);

        var onNumber = await service.PreviewAsync(Request("06", Csv("medium[text],insulation[text],theta_a[degC],C_a[kg]", "air,PVC,20,0.99")));
        Assert.Equal(RecordUnitCheck.Mismatch, onNumber.Columns.Single(c => c.Symbol == "C_a").Unit);

        var onText = await service.PreviewAsync(Request("06", Csv("medium[mm],insulation[text],theta_a[degC],C_a[-]", "air,PVC,20,0.99")));
        Assert.Equal(RecordUnitCheck.Mismatch, onText.Columns.Single(c => c.Symbol == "medium").Unit);

        var numberAsText = await service.PreviewAsync(Request("04", Csv("CSA[mm2],AWG[-],I_t[text]", "1.5,,15")));
        Assert.Equal(RecordUnitCheck.Mismatch, numberAsText.Columns.Single(c => c.Symbol == "I_t").Unit);
    }

    [Fact]
    public async Task A_Unit_Of_The_Same_Kind_Is_Kept_As_Typed_And_Judged_In_The_Calculators_Unit()
    {
        var host = Host();
        var service = host.Service.RecordImport;

        // 15000 mA is 15 A: inside what E4 takes. 5 mA is 0.005 A: below its 0.1 A limit, a slip in the unit.
        var inRange = await service.CommitAsync(Request("04", Csv("CSA[mm2],AWG[-],I_t[mA]", "1.5,,15000", "2.5,,20000")));
        Assert.True(inRange.Committed, inRange.Refusal);
        var cell = inRange.Records[0].Values[0].Rows![0].Cells["I_t"];
        Assert.Equal(15000, cell.Number);
        Assert.Equal("mA", cell.Unit);
        Assert.Equal(RecordUnitCheck.Convertible, inRange.Preview.Columns.Single(c => c.Symbol == "I_t").Unit);
        Assert.Contains(inRange.Preview.Warnings, w => w.Column == "I_t" && w.Message.Contains("kept as typed", StringComparison.Ordinal));

        var slip = await service.PreviewAsync(Request("04", Csv("CSA[mm2],AWG[-],I_t[mA]", "1.5,,5"), Provenance("Slip")));
        Assert.Contains(slip.Errors, e => e.Column == "I_t" && e.Message.Contains("outside what", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_Unit_Written_The_Track_P_Way_Is_Read_As_The_Engine_Writes_It()
    {
        var host = Host();
        var preview = await host.Service.RecordImport.PreviewAsync(Request("01", Csv("material[text],rho_20[ohm*mm],alpha[1/K],Q_c[J/(K*mm3)],beta[K],theta_i[degC],theta_f[degC]", "Test copper,1.7241e-5,0.00393,0.00345,234.5,70,160")));

        Assert.True(preview.CanCommit, Describe(preview));
        Assert.Equal("J/(K*mm^3)", preview.Columns.Single(c => c.Symbol == "Q_c").ExpectedUnit);
        Assert.All(preview.Columns, c => Assert.True(c.Unit is RecordUnitCheck.Match or RecordUnitCheck.NotApplicable, c.UnitMessage));

        // The same constant in ohm*mm2/m is the same dimension: it is accepted, not refused, and judged in the unit the calculator takes.
        var other = await host.Service.RecordImport.PreviewAsync(Request("01", Csv("material[text],rho_20[ohm*mm2/m],alpha[1/K],Q_c[J/(K*mm3)],beta[K],theta_i[degC],theta_f[degC]", "Test copper,0.01724,0.00393,0.00345,234.5,70,160")));
        Assert.Equal(RecordUnitCheck.Convertible, other.Columns.Single(c => c.Symbol == "rho_20").Unit);
        Assert.DoesNotContain(other.Errors, e => e.Column == "rho_20");
    }

    // ------------------------------------------------------------------
    // Row checks: positive, ranges, monotonic tables, duplicates
    // ------------------------------------------------------------------

    [Fact]
    public async Task Positive_Required_And_Range_Checks_Name_The_Row_And_The_Column()
    {
        var host = Host();
        var service = host.Service.RecordImport;

        var zero = await service.PreviewAsync(Request("04", Csv(Ampacity, "1.5,,0", "2.5,,")));
        Assert.Contains(zero.Errors, e => e.Line == 2 && e.Column == "I_t" && e.Message.Contains("larger than zero", StringComparison.Ordinal));
        Assert.Contains(zero.Errors, e => e.Line == 3 && e.Column == "I_t" && e.Message.Contains("blank", StringComparison.Ordinal));

        var neither = await service.PreviewAsync(Request("04", Csv(Ampacity, ",,15")));
        Assert.Contains(neither.Errors, e => e.Message.Contains("at least one is needed", StringComparison.Ordinal));

        // Beyond what E4 itself accepts (5000 A) is an error; inside it but unusual is a warning.
        var tooBig = await service.PreviewAsync(Request("04", Csv(Ampacity, "1.5,,9000")));
        Assert.Contains(tooBig.Errors, e => e.Column == "I_t" && e.Message.Contains("outside what", StringComparison.Ordinal));

        var factor = await service.PreviewAsync(Request("07", Csv("arrangement[text],n_circuits[-],C_g[-]", "bunched,2,1.2")));
        Assert.Contains(factor.Errors, e => e.Column == "C_g" && e.Message.Contains("above 1", StringComparison.Ordinal));

        // A specific heat of 100 J/(kg*K) is inside what B5 takes (1 to 100000) but far from any fluid: a warning, not an error.
        var unusual = await service.PreviewAsync(Request("16", Csv("fluid_h[text],t_mean_h[degC],cp_h[J/(kg*K)],fluid_c[text],t_mean_c[degC],cp_c[J/(kg*K)]", "Water,60,100,Water,10,4180")));
        Assert.True(unusual.CanCommit, Describe(unusual));
        Assert.Contains(unusual.Warnings, w => w.Column == "cp_h" && w.Message.Contains("usual range", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_Table_That_Runs_The_Wrong_Way_Is_An_Error_On_The_Row_That_Breaks_It()
    {
        var host = Host();
        var service = host.Service.RecordImport;

        // Ambient factors fall as the ambient temperature rises: 20 degC 0.8 then 30 degC 0.9 is the wrong way round.
        var wrong = await service.PreviewAsync(Request("06", Csv(AmbientFactors, "air,PVC,20,0.8", "air,PVC,30,0.9", "air,PVC,40,0.7")));
        Assert.False(wrong.CanCommit);
        var error = Assert.Single(wrong.Errors);
        Assert.Equal(3, error.Line);
        Assert.Equal("C_a", error.Column);
        Assert.Contains("must fall as theta_a rises", error.Message, StringComparison.Ordinal);

        // The order of the rows does not matter, only the pairing: a separate group is judged on its own.
        var fine = await service.PreviewAsync(Request("06", Csv(AmbientFactors, "air,PVC,40,0.7", "air,PVC,20,0.9", "ground,PVC,20,0.5", "ground,PVC,30,0.6")));
        Assert.Single(fine.Errors, e => e.Column == "C_a" && e.Line == 5);

        var duplicate = await service.PreviewAsync(Request("06", Csv(AmbientFactors, "air,PVC,20,0.9", "AIR,pvc,20,0.8")));
        Assert.Contains(duplicate.Errors, e => e.Line == 3 && e.Message.Contains("repeats line 2", StringComparison.Ordinal));

        var order = await service.PreviewAsync(Request("08", Csv("length_in_insulation[mm],C_i[-]", "100,0.8", "50,0.9")));
        Assert.Contains(order.Errors, e => e.Column == "length_in_insulation" && e.Message.Contains("must rise down the rows", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_Warning_Does_Not_Stop_An_Import_But_An_Error_Does_And_Row_Counts_Are_Held()
    {
        var host = Host();
        var service = host.Service.RecordImport;

        // CSA and I_t not rising together is a warning only.
        var odd = await service.PreviewAsync(Request("04", Csv(Ampacity, "1.5,,30", "2.5,,20")));
        Assert.True(odd.CanCommit, Describe(odd));
        Assert.Contains(odd.Warnings, w => w.Column == "I_t");

        var rows = Enumerable.Range(1, 61).Select(i => $"{i},,{10 + i}").ToArray();
        var tooMany = await service.PreviewAsync(Request("04", Csv(Ampacity, rows)));
        Assert.Contains(tooMany.AllIssues, i => i.Severity == RecordSeverity.Error && i.Message.Contains("at most 60", StringComparison.Ordinal));

        var twelve = await service.PreviewAsync(Request("45", Csv("month[text],theta_e[degC],RH_e[%],days[d]", "Jan,5,80,31")));
        Assert.Contains(twelve.Errors, e => e.Message.Contains("exactly 12", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------
    // All or nothing
    // ------------------------------------------------------------------

    [Fact]
    public async Task One_Bad_Row_Among_Good_Ones_Saves_Nothing()
    {
        var host = Host();
        var file = Csv(AmbientFactors, "air,PVC,20,0.99", "air,PVC,30,0.9", "air,PVC,40,oops");
        var service = host.Service.RecordImport;

        var preview = await service.PreviewAsync(Request("06", file));
        Assert.False(preview.CanCommit);
        Assert.Empty(preview.Planned);
        Assert.Equal(4, Assert.Single(preview.Errors).Line);

        var outcome = await service.CommitAsync(Request("06", file));
        Assert.False(outcome.Committed);
        Assert.StartsWith("Nothing was saved", outcome.Refusal, StringComparison.Ordinal);
        Assert.Empty(outcome.Records);
        Assert.Empty(await host.Store.ListRecordsAsync());

        // Mend the row and the same file commits whole.
        var mended = await service.CommitAsync(Request("06", Csv(AmbientFactors, "air,PVC,20,0.99", "air,PVC,30,0.9", "air,PVC,40,0.8")));
        Assert.True(mended.Committed, mended.Refusal);
        Assert.Equal(3, (await host.Store.ListRecordsAsync()).Count);
    }

    [Fact]
    public async Task A_Failed_Write_Rolls_The_Whole_Import_Back()
    {
        var host = Host();
        var request = Request("06", Csv(AmbientFactors, "air,PVC,20,0.99", "air,PVC,30,0.9", "air,PVC,40,0.8"));

        host.Failing.FailNextCommit = true;
        var failed = await host.Service.RecordImport.CommitAsync(request);

        Assert.False(failed.Committed);
        Assert.Contains("Nothing was saved", failed.Refusal, StringComparison.Ordinal);
        Assert.Empty(failed.Records);
        Assert.Empty(await host.Backing.ReadAllAsync(CalcInstanceStore.RecordCollection));
        Assert.Empty(await host.Store.ListRecordsAsync());

        var retried = await host.Service.RecordImport.CommitAsync(request);
        Assert.True(retried.Committed, retried.Refusal);
        Assert.Equal(3, (await host.Backing.ReadAllAsync(CalcInstanceStore.RecordCollection)).Count);
    }

    [Fact]
    public async Task A_Revision_Taken_Between_Preview_And_Commit_Rolls_The_Whole_Import_Back()
    {
        var host = Host();
        var store = host.Store;
        var first = await host.Service.RecordImport.CommitAsync(Request("51", Csv("grout[text],C_fd[-]", "Test grout,0.2"), Provenance("Grout friction")));
        Assert.True(first.Committed, first.Refusal);

        // A batch that repeats a revision of one record (the second write of the same key) is refused as a whole.
        var one = first.Records[0];
        var other = one with { Id = Guid.NewGuid(), Title = "Another grout" };
        await Assert.ThrowsAsync<CalcInstanceImmutableException>(() => store.SaveRecordsAsync([other, one]));
        Assert.Single(await store.ListRecordsAsync("joint.friction"));
    }

    // ------------------------------------------------------------------
    // Revisions and titles
    // ------------------------------------------------------------------

    [Fact]
    public async Task A_Title_That_Exists_Is_Refused_Unless_The_Import_Is_Its_Next_Revision()
    {
        var host = Host();
        var service = host.Service.RecordImport;
        var first = await service.CommitAsync(Request("04", Csv(Ampacity, "1.5,,15", "2.5,,20"), Provenance("Method C ampacity")));
        Assert.True(first.Committed, first.Refusal);

        var again = Request("04", Csv(Ampacity, "1.5,,16", "2.5,,21"), Provenance("Method C ampacity"));
        var refused = await service.PreviewAsync(again);
        Assert.False(refused.CanCommit);
        Assert.Contains(refused.Errors, e => e.Message.Contains("exists already (revision 1)", StringComparison.Ordinal));

        var revision = await service.CommitAsync(again with { AsRevision = true });
        Assert.True(revision.Committed, revision.Refusal);
        var next = Assert.Single(revision.Records);
        Assert.Equal(2, next.Revision);
        Assert.Equal(first.Records[0].Id, next.Id);
        Assert.Equal(15, (await host.Store.FindRecordAsync(next.Id, 1))!.Values[0].Rows![0].Cells["I_t"].Number);
        Assert.Equal(16, (await host.Store.FindRecordAsync(next.Id))!.Values[0].Rows![0].Cells["I_t"].Number);
    }

    [Fact]
    public async Task A_Revision_Of_A_Licensed_Record_Stays_Licensed_Even_If_The_New_Import_Says_Otherwise()
    {
        var host = Host();
        var service = host.Service.RecordImport;
        var first = await service.CommitAsync(Request("04", Csv(Ampacity, "1.5,,15"), Provenance("Method C ampacity", licensed: true)));
        Assert.True(first.Committed, first.Refusal);

        var second = await service.CommitAsync(Request("04", Csv(Ampacity, "1.5,,16"), Provenance("Method C ampacity", licensed: false)) with { AsRevision = true });

        Assert.False(second.Committed);
        Assert.Contains("licensed source", second.Preview.Errors[0].Message, StringComparison.Ordinal);
        Assert.Single(await host.Store.ListRecordsAsync("cable.ampacity"));
    }

    [Fact]
    public async Task Two_Rows_That_Make_The_Same_Title_Are_Refused()
    {
        var host = Host();
        var preview = await host.Service.RecordImport.PreviewAsync(Request("51", Csv("grout[text],C_fd[-]", "Same,0.2", "same,0.3")));

        Assert.False(preview.CanCommit);
        Assert.Contains(preview.AllIssues, i => i.Message.Contains("repeats", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------
    // Provenance
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("source")]
    [InlineData("edition")]
    [InlineData("table")]
    [InlineData("holder")]
    [InlineData("title")]
    public async Task A_Record_Without_Its_Provenance_Is_Refused_And_Nothing_Is_Saved(string missing)
    {
        var host = Host();
        var provenance = Provenance() with
        {
            SourceTitle = missing == "source" ? " " : "Source",
            Edition = missing == "edition" ? string.Empty : "2018",
            TableOrClause = missing == "table" ? string.Empty : "Table 1",
            LicenceHolder = missing == "holder" ? string.Empty : "Me",
            Title = missing == "title" ? string.Empty : "Title",
        };

        var outcome = await host.Service.RecordImport.CommitAsync(Request("47", Csv("I_n[A]", "6"), provenance));

        Assert.False(outcome.Committed);
        Assert.Contains("The provenance needs its", outcome.Refusal, StringComparison.Ordinal);
        Assert.Empty(await host.Store.ListRecordsAsync());
    }

    [Fact]
    public async Task The_Amendment_May_Be_Blank_And_An_Indicative_Origin_Is_Refused()
    {
        var host = Host();
        var blank = await host.Service.RecordImport.CommitAsync(Request("47", Csv("I_n[A]", "6"), Provenance(amendment: string.Empty)));
        Assert.True(blank.Committed, blank.Refusal);
        Assert.Equal("2018", blank.Records[0].Edition);

        var indicative = await host.Service.RecordImport.PreviewAsync(Request("47", Csv("I_n[A]", "6"), Provenance("Other") with { Origin = CalcDataOrigin.Indicative }));
        Assert.Contains(indicative.Errors, e => e.Message.Contains("indicative", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Track_P_Provenance_File_Is_Read_And_A_Bad_Row_Is_Named()
    {
        var text = "kind,title,source_title,edition,amendment,table_or_clause,licence_holder,entered_by,licensed_source,origin,notes\r\n"
            + "cable.ampacity,Method C [4d2a],Test Wiring Rules,2018,A2:2022,Table 4D2A,Own copy,someone,,UserEntered,Reference 30 degC\r\n"
            + "cable.ampacity,Method B,Test Wiring Rules,2018,,Table 4D5A,Own copy,someone,false,Derived,\r\n"
            + "cable.ampacity,Bad flag,Test Wiring Rules,2018,,Table 4D5A,Own copy,someone,maybe,UserEntered,\r\n"
            + "cable.ampacity,Bad origin,Test Wiring Rules,2018,,Table 4D5A,Own copy,someone,true,Indicative,\r\n"
            + "cable.ampacity,,Test Wiring Rules,2018,,Table 4D5A,Own copy,someone,true,UserEntered,\r\n";
        var rows = RecordImportService.ReadProvenanceFile(Utf8(text), out var problems);

        Assert.Equal(2, rows.Count);
        Assert.True(rows[0].Provenance.LicensedSource, "licensed_source defaults on");
        Assert.Equal("2018; A2:2022", rows[0].Provenance.EditionAndAmendment);
        Assert.Equal("Reference 30 degC", rows[0].Provenance.Notes);
        Assert.False(rows[1].Provenance.LicensedSource);
        Assert.Equal(CalcDataOrigin.Derived, rows[1].Provenance.Origin);
        Assert.Equal(3, problems.Count);
        Assert.Contains("Line 4", problems[0], StringComparison.Ordinal);

        var none = RecordImportService.ReadProvenanceFile(Utf8("kind,title\r\n"), out var missing);
        Assert.Empty(none);
        Assert.Contains("no column", missing[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_Provenance_File_Track_P_Ships_Reads_Without_Rows_And_Every_Template_Writes_A_Header_Only_File()
    {
        var shipped = System.IO.File.ReadAllBytes(Path.Combine(TemplateFolder, "_provenance.csv"));
        var rows = RecordImportService.ReadProvenanceFile(shipped, out var problems);
        Assert.Empty(rows);
        Assert.Empty(problems);

        var host = Host();
        var blank = RecordImportService.BlankTemplateCsv(RecordTemplates.Find("04")!);
        Assert.Equal("CSA[mm2],AWG[-],I_t[A]\r\n", blank);
        var preview = await host.Service.RecordImport.PreviewAsync(Request("04", Utf8(blank)));
        Assert.Contains(preview.Errors, e => e.Message.Contains("no data rows", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------
    // The export lock is unchanged
    // ------------------------------------------------------------------

    [Fact]
    public async Task An_Imported_Record_Is_Licensed_By_Default_And_Its_Values_Never_Leave_Through_An_Export()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var imported = await host.Service.RecordImport.CommitAsync(Request("04", Csv(Ampacity, "1.5,,15", "2.5,,20"), Provenance("Licensed ampacity")));
        Assert.True(imported.Committed, imported.Refusal);
        var record = imported.Records[0];

        var (csv, refusal) = await host.Service.ExportRecordAsync(record.Id);
        Assert.Null(csv);
        Assert.Contains("never exported", refusal, StringComparison.Ordinal);

        var calc = await host.Service.CreateAsync("elec.conductor-sizing", project, "Feeder");
        var applied = await host.Service.ApplyRecordAsync(calc.Id, record.Id);
        Assert.True(applied.Succeeded, applied.Reason);
        var withheld = await host.Service.WithholdLicensedAsync(new CalcInputSet(applied.Instance!.Inputs));
        var ampacity = withheld.Find("ampacity")!;
        Assert.Null(ampacity.Rows);
        Assert.Contains("licensed value withheld", ampacity.Provenance!.Reference, StringComparison.Ordinal);

        // A copy of the import in any later revision stays locked.
        var revised = await host.Service.RecordImport.CommitAsync(Request("04", Csv(Ampacity, "1.5,,16"), Provenance("Licensed ampacity", licensed: false)) with { AsRevision = true });
        Assert.False(revised.Committed);
        Assert.True(await host.Store.IsLicensedInAnyRevisionAsync(record.Id));
    }

    [Fact]
    public async Task A_Record_Imported_As_Unlicensed_Exports_As_Before_And_The_Import_Adds_No_Export_Path()
    {
        var host = Host();
        var imported = await host.Service.RecordImport.CommitAsync(Request("51", Csv("grout[text],C_fd[-]", "Test grout,0.2"), Provenance("Open grout", licensed: false)));
        Assert.True(imported.Committed, imported.Refusal);
        Assert.False(imported.Records[0].IsLicensed());

        var (csv, refusal) = await host.Service.ExportRecordAsync(imported.Records[0].Id);
        Assert.Null(refusal);
        Assert.Contains("C_fd,0.2", csv, StringComparison.Ordinal);

        // The importer reads and saves; nothing on it writes a record's values out (its only output file is a template's header row).
        var methods = typeof(RecordImportService).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly).Select(m => m.Name);
        Assert.DoesNotContain(methods, n => n.Contains("Export", StringComparison.Ordinal) || n.Contains("Write", StringComparison.Ordinal));
        Assert.Equal("grout[text],C_fd[-]\r\n", RecordImportService.BlankTemplateCsv(RecordTemplates.Find("51")!));
    }

    [Fact]
    public async Task A_Licensed_Material_Added_To_By_An_Import_Never_Exports()
    {
        var host = Host();
        var made = await host.Service.RecordImport.CommitAsync(Request("62", Csv("material[text],E[GPa],nu[-]", "Locked alloy,200,0.3"), Provenance(string.Empty)));
        Assert.True(made.Committed, made.Refusal);

        var (csv, refusal) = await host.Service.Materials.ExportCsvAsync(made.Records[0].Id);
        Assert.Null(csv);
        Assert.Contains("licensed", refusal, StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // Materials: refusals
    // ------------------------------------------------------------------

    [Fact]
    public async Task A_Temperature_Table_For_A_Material_That_Is_Not_In_The_Library_Or_With_The_Wrong_Unit_Is_Refused()
    {
        var host = Host();
        var service = host.Service.RecordImport;

        var absent = await service.PreviewAsync(Request("63", Csv("material[text],property[text],T[degC],v[-],v_unit[text]", "Nobody,E,20,200,GPa", "Nobody,E,100,190,GPa"), Provenance(string.Empty)));
        Assert.Contains(absent.Errors, e => e.Message.Contains("No material called 'Nobody'", StringComparison.Ordinal));

        await service.CommitAsync(Request("62", Csv("material[text],E[GPa]", "Wrong unit alloy,200"), Provenance(string.Empty)));
        var wrong = await service.PreviewAsync(Request("63", Csv("material[text],property[text],T[degC],v[-],v_unit[text]", "Wrong unit alloy,E,20,200,mm", "Wrong unit alloy,E,100,190,mm"), Provenance(string.Empty)));
        Assert.Contains(wrong.Errors, e => e.Column == "v_unit");

        var single = await service.PreviewAsync(Request("63", Csv("material[text],property[text],T[degC],v[-],v_unit[text]", "Wrong unit alloy,E,20,200,GPa"), Provenance(string.Empty)));
        Assert.Contains(single.Errors, e => e.Message.Contains("at least two rows", StringComparison.Ordinal));

        var unrising = await service.PreviewAsync(Request("63", Csv("material[text],property[text],T[degC],v[-],v_unit[text]", "Wrong unit alloy,E,100,200,GPa", "Wrong unit alloy,E,20,190,GPa"), Provenance(string.Empty)));
        Assert.Contains(unrising.AllIssues, i => i.Severity == RecordSeverity.Error && i.Message.Contains("rise strictly", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_Material_Value_Outside_Its_Range_Names_The_Row()
    {
        var host = Host();
        var preview = await host.Service.RecordImport.PreviewAsync(Request("62", Csv("material[text],E[GPa],nu[-]", "Fine,200,0.3", "Bad,200,0.7"), Provenance(string.Empty)));

        Assert.False(preview.CanCommit);
        var error = Assert.Single(preview.Errors);
        Assert.Equal(3, error.Line);
        Assert.Contains("nu", error.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // More templates, built end to end
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("13", "gamma_M0[-]", "1.1", "ec3-1-1.national-annex")]
    [InlineData("14", "gamma_M2[-],gamma_M0[-],k_e1[-],k_e2[-],k_p1[-],k_p2[-],gamma_M1[-]", "1.25,1,1.2,1.2,2.2,2.4,", "ec3.national-annex")]
    [InlineData("20", "alpha_a0[-],alpha_a[-],alpha_b[-],alpha_c[-],alpha_d[-],lambda_LT0[-],beta_LT[-]", "0.13,0.21,0.34,0.49,0.76,0.4,0.75", "ec3.ltb-factors")]
    [InlineData("12", "grade[text],t_min[mm],t_max[mm],f_y[MPa],f_u[MPa]", "TEST,0,16,275,410", "steel.strengths")]
    [InlineData("28", "emitter[text],n[-]", "Panel,1.3", "emitter.rating")]
    [InlineData("33", "H[-],p[bar],gap[mm]", "70,100,0.2", "oring.extrusion")]
    [InlineData("45", "month[text],theta_e[degC],RH_e[%],days[d]", "", "climate.monthly")]
    [InlineData("55", "V[L]", "1", "accumulator.sizes")]
    [InlineData("56", "element[text],name[text],d[mm],lambda[W/(m*K)],mu[-],timber[-]", "wall,Brick,100,0.7,10,0", "material.conductivity")]
    [InlineData("66", "mounting[text],K_m[-]", "Pinned,1", "cylinder.mounting")]
    [InlineData("74", "pipe[text],OD[mm],t_wall[mm]", "DN50,60.3,3.6", "pipe.section")]
    [InlineData("81", "bundle[text],T_grip[N],P_lim[N/m],k_n[-]", "Three-core,5000,500,0.5", "cable.pulling-limit")]
    public async Task Templates_Of_Every_Shape_Are_Imported_With_Invented_Values(string id, string header, string row, string kind)
    {
        var host = Host();
        var lines = id == "45"
            ? new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }.Select((m, i) => $"{m},{5 + i},{80 - i},{30}").ToArray()
            : [row];

        var outcome = await host.Service.RecordImport.CommitAsync(Request(id, Csv(header, lines), Provenance($"Invented {id}")));

        Assert.True(outcome.Committed, outcome.Refusal + " " + Describe(outcome.Preview));
        Assert.All(outcome.Records, r => Assert.Equal(kind, r.Kind));
        Assert.All(outcome.Records, r => Assert.True(r.LicensedSource));
        Assert.Single(outcome.Records);
    }

    // ------------------------------------------------------------------
    // What a calculator is missing
    // ------------------------------------------------------------------

    [Fact]
    public async Task A_Calculator_Shows_Which_Required_Records_Are_Missing_Until_They_Are_Imported()
    {
        var host = Host();
        var service = host.Service.RecordImport;

        var missing = await service.MissingRequiredAsync("elec.conductor-sizing");
        Assert.Equal(["cable.ampacity", "device.ratings"], missing.Select(m => m.Kind).Order(StringComparer.Ordinal));
        Assert.All(missing, m => Assert.NotEmpty(m.Templates));
        Assert.Contains(missing.Single(m => m.Kind == "cable.ampacity").Templates, t => t.Id == "04");
        Assert.Contains(missing.Single(m => m.Kind == "cable.ampacity").Templates, t => t.Id == "10");

        Assert.True((await service.CommitAsync(Request("04", Csv(Ampacity, "1.5,,15")))).Committed);
        Assert.Equal(["device.ratings"], (await service.MissingRequiredAsync("elec.conductor-sizing")).Select(m => m.Kind));

        Assert.True((await service.CommitAsync(Request("47", Csv("I_n[A]", "6", "10"), Provenance("Ratings")))).Committed);
        Assert.Empty(await service.MissingRequiredAsync("elec.conductor-sizing"));

        var all = await service.RequirementsAsync("elec.conductor-sizing");
        Assert.Equal(RecordNeed.Recommended, all.Single(r => r.Kind == "cable.correction-factors").Need);
        Assert.True(all.Single(r => r.Kind == "cable.correction-factors").IsMissing);
        Assert.Equal(1, all.Single(r => r.Kind == "cable.ampacity").Count);
    }

    [Fact]
    public async Task A_Record_Counts_As_Present_Only_If_It_Holds_A_Value_The_Calculator_Takes()
    {
        var host = Host();
        var service = host.Service.RecordImport;

        // S7 reads gamma_M1 from ec3.national-annex; a record that holds only the S6 factors does not satisfy it.
        Assert.True((await service.CommitAsync(Request("14", Csv("gamma_M2[-],gamma_M0[-],k_e1[-],k_e2[-],k_p1[-],k_p2[-],gamma_M1[-]", "1.25,1,1.2,1.2,2.2,2.4,"), Provenance("S6 factors")))).Committed);

        var s4 = await service.RequirementsAsync("struct.connection-ec3");
        var s7 = await service.RequirementsAsync("struct.ltb-interaction");
        Assert.True(s4.Single(r => r.Kind == "ec3.national-annex").Present);
        Assert.False(s7.Single(r => r.Kind == "ec3.national-annex").Present);

        // The generic seed materials ship, so a material is never missing.
        Assert.True(s7.Single(r => r.Kind == "material.properties").Present);
        Assert.Empty(await service.RequirementsAsync("no.such@1"));
    }

    [Fact]
    public async Task The_Records_Screens_List_Counts_The_Kinds_And_What_Needs_Them()
    {
        var host = Host();
        var service = host.Service.RecordImport;
        Assert.True((await service.CommitAsync(Request("04", Csv(Ampacity, "1.5,,15"), Provenance("Licensed")))).Committed);
        Assert.True((await service.CommitAsync(Request("04", Csv(Ampacity, "1.5,,15"), Provenance("Open", licensed: false)))).Committed);

        var types = await service.ListTypesAsync();
        var ampacity = types.Single(t => t.Kind == "cable.ampacity");

        Assert.Equal(2, ampacity.Count);
        Assert.Equal(1, ampacity.Licensed);
        Assert.Contains("Conductor sizing and overload coordination", ampacity.RequiredBy);
        Assert.Contains(ampacity.Templates, t => t.Id == "10");
        Assert.Equal(types.Select(t => t.Title).Order(StringComparer.Ordinal), types.Select(t => t.Title));
        Assert.DoesNotContain(types, t => t.Kind.StartsWith("lifting.", StringComparison.Ordinal));
        Assert.True(types.Single(t => t.Kind == "material.properties").Count > 0);
    }
}
