using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using static Tempest.Calc.Tests.Library.RecordImport.RecordImportTestSupport;

namespace Tempest.Calc.Tests.Library.RecordImport;

/// <summary>
/// DC-07 S5 (owner decision DC-21): the load combinations sheet takes its National Annex factors from an <c>en1990.factors</c> record entered through the
/// C-3 record tooling (template 15), never from values typed into the sheet. With <c>factor_set</c> = National Annex record and no record applied the sheet
/// refuses naming <c>factor_set</c>; a record imported and applied through the service fills the five inputs with its pin, the sheet runs and prints the set
/// name and the record's citation. The numbers here are invented; nothing from a National Annex is typed.
/// </summary>
public sealed class LoadCombinationsFactorSetTests
{
    private const string Header = "gamma_G_sup[-],gamma_G_inf[-],gamma_Q[-],xi[-],category[text],psi_0[-],psi_1[-],psi_2[-]";

    private static IReadOnlyList<CalcInputValue> Inputs(string factorSet) =>
    [
        LoadCombinationsCalculator.Actions(("Dead load", LoadCombinationsCalculator.Permanent, 10), ("Office imposed", "B office", 5)) with { Provenance = CalcProvenance.Drawing("GA-001", "A") },
        CalcInputValue.Choice("method", LoadCombinationsCalculator.Method610ab, CalcProvenance.Specification("Project basis")),
        CalcInputValue.Choice("factor_set", factorSet, CalcProvenance.Specification("Project basis")),
    ];

    [Fact]
    public async Task A_National_Annex_Record_Imported_Through_The_Tooling_And_Applied_Fills_The_Factor_Set_And_The_Sheet_Prints_It()
    {
        var host = Host();
        var project = await host.ProjectAsync();

        // The set chosen with no record applied: refused in one sentence naming factor_set, and the sheet says which inputs want the record.
        var created = await host.Service.CreateAsync(LoadCombinationsCalculator.Id, project, "Office floor beam", Inputs(LoadCombinationsCalculator.FactorSetNationalAnnex));
        var refusal = Assert.Single(created.Blocks);
        Assert.Equal("factor-set", refusal.RuleId);
        Assert.StartsWith("factor_set:", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("gamma_G_sup, gamma_G_inf, gamma_Q, xi, psi", refusal.Message, StringComparison.Ordinal);

        // The record: template 15, invented values, the first row carrying the four partial factors and every row a psi category.
        var outcome = await host.Service.RecordImport.CommitAsync(Request("15", Csv(Header, "1.4,1,1.6,0.9,B office,0.6,0.4,0.2", ",,,,wind,0.5,0.2,0"),
            new RecordProvenanceInput("National Annex factors (test)", "NA to EN 1990 (test)", "2005", "A1:2009", "Tables NA.A1.1 and NA.A1.2(B)", "Own copy (test)", true)));
        Assert.True(outcome.Committed, outcome.Refusal + " " + Describe(outcome.Preview));
        var record = Assert.Single(outcome.Records);
        Assert.Equal("en1990.factors", record.Kind);
        Assert.Equal(["gamma_G_inf", "gamma_G_sup", "gamma_Q", "psi", "xi"], record.Values.Select(v => v.Symbol).Order(StringComparer.Ordinal));

        var applied = await host.Service.ApplyRecordAsync(created.Id, record.Id);
        Assert.True(applied.Succeeded, applied.Reason);
        var instance = applied.Instance!;
        Assert.All(LoadCombinationsCalculator.FactorSymbols, s => Assert.Equal((record.Id, 1), CalcDataRecord.PinOf(instance.Inputs.Single(i => i.Symbol == s).Provenance)));
        Assert.Empty(instance.Blocks);

        // 6.10a: 1.4×10 + 1.6×0.6×5 = 18.8; 6.10b: 0.9×1.4×10 + 1.6×5 = 20.6 governs.
        Assert.Equal(20.6, instance.Outputs.Single(o => o.Symbol == "E_d_max").Si!.Value / 1e3, 9);
        var set = instance.Outputs.Single(o => o.Symbol == "factor_set_used").Text.Trim('"');
        Assert.StartsWith("National Annex record", set, StringComparison.Ordinal);
        Assert.Contains("Tables NA.A1.1 and NA.A1.2(B)", set, StringComparison.Ordinal);
        Assert.DoesNotContain("record:", set, StringComparison.Ordinal);

        var sheet = (await host.Service.RunAsync(created.Id))!.SheetText;
        Assert.Contains("National Annex record", sheet, StringComparison.Ordinal);
        Assert.Contains("record: National Annex factors (test) — NA to EN 1990 (test), 2005; A1:2009, Tables NA.A1.1 and NA.A1.2(B) (licensed source) rev 1", sheet, StringComparison.Ordinal);
        Assert.DoesNotContain("factor_set:", sheet, StringComparison.Ordinal);

        // The licensed values are withheld from an export, as any record's are.
        var withheld = await host.Service.WithholdLicensedAsync(new CalcInputSet(instance.Inputs));
        Assert.Null(withheld.Find("xi")!.Number);
    }

    [Fact]
    public async Task The_Recommended_Set_Runs_With_Or_Without_A_Record_And_A_Calculation_Saved_Without_The_Choice_Reads_It()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var recommended = await host.Service.CreateAsync(LoadCombinationsCalculator.Id, project, "Office floor beam", Inputs(LoadCombinationsCalculator.FactorSetRecommended));
        Assert.Empty(recommended.Blocks);
        Assert.StartsWith("EN recommended", recommended.Outputs.Single(o => o.Symbol == "factor_set_used").Text.Trim('"'), StringComparison.Ordinal);
        // 6.10a: 1.35×10 + 1.5×0.7×5 = 18.75; 6.10b: 0.85×1.35×10 + 1.5×5 = 18.975 governs (the recommended defaults).
        Assert.Equal(18.975, recommended.Outputs.Single(o => o.Symbol == "E_d_max").Si!.Value / 1e3, 9);

        // A calculation made without factor_set (as every one saved on 1.2.0 was) reads the default and gives the same values.
        var old = await host.Service.CreateAsync(LoadCombinationsCalculator.Id, project, "Office floor beam (old)", Inputs(LoadCombinationsCalculator.FactorSetRecommended).Where(i => i.Symbol != "factor_set"));
        Assert.Empty(old.Blocks);
        Assert.Null(old.Inputs.SingleOrDefault(i => i.Symbol == "factor_set"));
        Assert.Equal(18.975, old.Outputs.Single(o => o.Symbol == "E_d_max").Si!.Value / 1e3, 9);
        Assert.StartsWith("EN recommended", old.Outputs.Single(o => o.Symbol == "factor_set_used").Text.Trim('"'), StringComparison.Ordinal);
    }
}
