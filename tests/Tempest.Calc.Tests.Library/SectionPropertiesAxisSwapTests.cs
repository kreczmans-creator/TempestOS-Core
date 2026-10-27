using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;
using Tempest.Core.Evidence;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// C-2 phase B renamed the section properties into the EN 1993 axes, and <c>W_el_y</c> and <c>W_pl_y</c> kept their symbols while changing from the
/// moduli about the vertical axis of the drawing (the minor axis of an upright I-section) to the major-axis moduli (<c>struct.section-properties</c>
/// 1.2.0). The old symbols of the minor-axis moduli are therefore taken, so <c>W_el_z</c> and <c>W_pl_z</c> can carry no alias, and a link stored
/// under <c>W_el_y</c> from a calculation made on 1.1.1 would otherwise bind the major-axis value silently (243.5 → 1957.5 cm³ on the UB example).
/// The ports declare the change (ADR-0177 review), so a link, an imported link and a pack binding from such a calculation are refused with the sentence,
/// an existing one is stale and never moved, and a link under the old major-axis symbol <c>W_el_x</c> keeps its meaning.
/// </summary>
public sealed class SectionPropertiesAxisSwapTests
{
    private const string Id = SectionPropertiesCalculator.Id;

    private const string OldReference = Id + "@1.1.1";

    private const string WelChanged = "W_el_y meant the elastic section modulus about the vertical axis of the drawing (the minor axis of an upright I-section) on " + OldReference + "; since " + Id + "@1.2.0 it means the elastic section modulus about the EN 1993 y-y axis (the major axis of an upright I-section)";

    private static IReadOnlyList<CalcInputValue> WithDrawing(IEnumerable<CalcInputValue> inputs) => [.. inputs.Select(v => v with { Provenance = CalcProvenance.Drawing("DRG-SEC-1", "A") })];

    /// <summary>A checked section record as a calculation made on 1.1.1 stored it: the same outputs under the same symbols, the template reference of that version.</summary>
    private static async Task<(LibraryTestHost Host, Guid? Project, CalcInstance Section)> SectionMadeOn111Async()
    {
        var host = new LibraryTestHost(calculators: [new SectionPropertiesCalculator(), new MemberChecksCalculator()]);
        var project = await host.ProjectAsync();
        var created = await host.Service.CreateAsync(Id, project, "Floor beam section", WithDrawing(new SectionPropertiesCalculator().Template.TestCases[0].Inputs));
        Assert.Empty(created.Blocks);
        var checkedOne = await host.Service.CheckAsync(created.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.True(checkedOne.Succeeded, checkedOne.Reason);
        var stored = (await host.Store.FindInstanceAsync(created.Id))!;
        await host.Store.SaveInstanceAsync(stored with { TemplateReference = OldReference });
        var section = (await host.Store.FindInstanceAsync(created.Id))!;
        Assert.Equal("1.1.1", section.TemplateVersion);
        return (host, project, section);
    }

    [Fact]
    public void The_Two_Ports_Declare_The_Change_Of_Meaning_At_1_2_0_And_The_Minor_Axis_Ports_Carry_No_Alias()
    {
        var template = new SectionPropertiesCalculator().Template;

        foreach (var symbol in new[] { "W_el_y", "W_pl_y" })
        {
            var port = template.FindPort(symbol)!;
            Assert.Equal("1.2.0", port.MeaningChangedAt);
            Assert.Contains("minor axis", port.MeaningWas, StringComparison.Ordinal);
            Assert.Contains("major axis", port.MeaningNow, StringComparison.Ordinal);
            Assert.True(port.MeaningChangedSince(symbol, "1.1.1"));
            Assert.False(port.MeaningChangedSince(symbol, "1.2.0"));
            Assert.False(port.MeaningChangedSince(symbol.Replace("_y", "_x", StringComparison.Ordinal), "1.1.1"), "the old major-axis symbol is an alias and keeps its meaning");
        }

        Assert.Null(template.FindPort("W_el_z")!.Aliases);
        Assert.Null(template.FindPort("W_pl_z")!.Aliases);
        Assert.Equal(WelChanged, template.FindPort("W_el_y")!.MeaningChangeText(Id, OldReference));
    }

    [Fact]
    public async Task A_Link_To_W_el_y_From_A_Section_Made_On_1_1_1_Is_Refused_By_Symbol_By_Name_And_On_Import_And_The_Old_Major_Axis_Symbol_Still_Binds()
    {
        var (host, project, section) = await SectionMadeOn111Async();
        var member = await host.Service.CreateAsync(MemberChecksCalculator.Id, project, "Floor beam member");
        var refusal = $"'Floor beam section' rev {section.Revision} was made on {OldReference}, where {WelChanged}, so 'W_el_y' is not linked from it: revise 'Floor beam section' onto {Id}@1.2.1 and link to that revision.";

        var bySymbol = await host.Service.BindAsync(member.Id, "W_el_y", section.Id, "W_el_y");
        Assert.False(bySymbol.Succeeded);
        Assert.Equal(refusal, bySymbol.Reason);

        var byName = await host.Service.BindAsync(member.Id, "W_el_y", section.Id, "section.modulus_elastic_y");
        Assert.False(byName.Succeeded);
        Assert.Equal(refusal.Replace("'W_el_y' is not linked", "'section.modulus_elastic_y' is not linked", StringComparison.Ordinal), byName.Reason);

        var plastic = await host.Service.BindAsync(member.Id, "W_pl_y", section.Id, "W_pl_y");
        Assert.False(plastic.Succeeded);
        Assert.Contains("W_pl_y meant the plastic section modulus about the vertical axis of the drawing", plastic.Reason, StringComparison.Ordinal);

        // The CSV and inputs.json paths arrive as inputs with a link in their provenance: refused the same way.
        var imported = await host.Service.UpdateInputsAsync(member.Id, [CalcInputValue.Quantity("W_el_y", 243.5, "cm^3", CalcProvenance.Link(section.Id, "W_el_y", section.Revision))]);
        Assert.False(imported.Succeeded);
        Assert.Equal(refusal, imported.Reason);
        Assert.Empty((await host.Store.FindInstanceAsync(member.Id))!.Links);

        // W_el_x always meant the major-axis modulus: a link under it from the same old record binds the major-axis value through the alias.
        var oldMajor = await host.Service.BindAsync(member.Id, "W_el_y", section.Id, "W_el_x");
        Assert.True(oldMajor.Succeeded, oldMajor.Reason);
        Assert.Equal(section.Outputs.Single(o => o.Symbol == "W_el_y").Si!.Value * 1e6, oldMajor.Instance!.Inputs.Single(i => i.Symbol == "W_el_y").Number!.Value, 3);
        Assert.Equal(1957.5, oldMajor.Instance.Inputs.Single(i => i.Symbol == "W_el_y").Number!.Value, 0);
    }

    [Fact]
    public async Task A_Link_Stored_Under_W_el_y_From_A_Section_Made_On_1_1_1_Is_Stale_And_Never_Moved()
    {
        var (host, project, section) = await SectionMadeOn111Async();

        // The form such a link has in a calculation saved before 1.2.0: the old symbol in the provenance, the minor-axis value it took.
        var saved = CalcInputValue.Quantity("W_el_y", 243.5, "cm^3", CalcProvenance.Link(section.Id, "W_el_y", section.Revision));
        var refusal = $"'Floor beam section' rev {section.Revision} was made on {OldReference}, where {WelChanged}, so 'W_el_y' is not linked from it: revise 'Floor beam section' onto {Id}@1.2.1 and link to that revision.";

        // Creating a calculation with such a link today is refused outright; one saved before 1.2.0 sits in the store with the link already in it.
        var creating = await Assert.ThrowsAsync<CalcLinkRefusedException>(() => host.Service.CreateAsync(MemberChecksCalculator.Id, project, "Floor beam member (old link)", [saved]));
        Assert.Equal(refusal, creating.Message);
        var created = await host.Service.CreateAsync(MemberChecksCalculator.Id, project, "Floor beam member (old link)");
        await host.Store.SaveInstanceAsync(created with { Inputs = [saved] });
        var member = (await host.Store.FindInstanceAsync(created.Id))!;
        Assert.Equal("W_el_y", member.Links.Single().Link.OutputSymbol);

        Assert.Contains(refusal, (await host.Service.StalenessAsync(member)).Reasons);
        var updated = await host.Service.UpdateLinksAsync(member.Id);
        Assert.False(updated.Succeeded);
        Assert.Equal(refusal, updated.Reason);
        Assert.Equal(243.5, (await host.Store.FindInstanceAsync(member.Id))!.Inputs.Single(i => i.Symbol == "W_el_y").Number);
    }

    [Fact]
    public void A_Pack_Binding_Of_W_el_y_Saved_From_A_Section_Step_On_1_1_1_Is_Refused_And_One_From_1_2_0_Is_Not()
    {
        var host = new LibraryTestHost(calculators: [new SectionPropertiesCalculator(), new MemberChecksCalculator()]);
        CalcPackTemplate Chain(string? sectionVersion, string output = "W_el_y") => new("t", "Section and member", "",
            [new CalcPackStep("s1", Id, "Section", TemplateVersion: sectionVersion), new CalcPackStep("s2", MemberChecksCalculator.Id, "Member")],
            [new CalcPackBinding("s1", output, "s2", "W_el_y")]);

        Assert.Null(host.Service.ValidatePackTemplate(Chain(null)));
        Assert.Null(host.Service.ValidatePackTemplate(Chain("1.2.0")));
        Assert.Null(host.Service.ValidatePackTemplate(Chain("1.1.1", output: "W_el_x")));
        Assert.Equal($"Pack template 'Section and member': step 's1' was saved from {OldReference}, where {WelChanged}, so the link s1.W_el_y → s2.W_el_y cannot be made; save the chain again from calculations on {Id}@1.2.1.",
            host.Service.ValidatePackTemplate(Chain("1.1.1")));
    }
}
