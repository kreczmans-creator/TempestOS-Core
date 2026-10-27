using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// ADR-0177 review: a symbol that came to mean something else at a template version (an axis swapped, a width that became a depth) is declared
/// with <see cref="CalcInputSpec.MeaningChangedAt"/> or <see cref="CalcOutputPort.MeaningChangedAt"/> and both meanings in words. A value stored
/// under that symbol by a calculation made before that version is not carried, linked or bound: the draft upgrade leaves it out, Revise refuses,
/// a link from such a revision is refused and an existing one is reported stale and never moved, and a pack binding saved from such a version is
/// refused. Every sentence names the symbol, what it meant and what it means now. And an old record holding one input under two of its names with
/// different values carries neither, both named, instead of the direct symbol winning silently.
/// </summary>
public sealed class MeaningChangeTests
{
    private const string SectionId = "test.section";
    private const string MemberId = "test.member";

    /// <summary>The upstream: a section's modulus W_y. 1.1.0 gives it about the minor axis; 1.2.0 gives it about the major axis and says so.</summary>
    private sealed class Section(string version, bool major) : ICalcCalculator
    {
        public CalcTemplate Template { get; } = new(SectionId, version, CalcDiscipline.Structural, "Section", "", "",
            [new CalcInputSpec("b", "Width", CalcInputKind.Quantity, "mm", Default: 100), new CalcInputSpec("h", "Depth", CalcInputKind.Quantity, "mm", Default: 200)],
            [], [], [], [],
            OutputPorts:
            [
                new CalcOutputPort("W_y", "mm^3", "Section modulus about y-y", "section.modulus_y",
                    MeaningChangedAt: major ? "1.2.0" : null,
                    MeaningWas: major ? "the section modulus about the minor axis" : null,
                    MeaningNow: major ? "the section modulus about the major axis" : null),
            ]);

        public void Run(CalcRunContext c)
        {
            c.Input("b");
            c.Input("h");
            c.Assign("W_y", major ? "b * h * h / 6" : "h * b * b / 6", "mm^3", "Section modulus");
            c.Output("W_y", "Section modulus");
        }
    }

    /// <summary>
    /// The downstream: a plate taking a modulus W and a dimension d. 1.1.0 calls the modulus <c>W_old</c> and d is the plate's width; 1.2.0 renames
    /// the modulus <c>W</c> (alias <c>W_old</c>, C-2) and makes d the plate's depth, saying so.
    /// </summary>
    private sealed class Member(string version, string modulus, string? alias, bool depth) : ICalcCalculator
    {
        public static Member V110 => new("1.1.0", "W_old", null, false);

        public static Member V120 => new("1.2.0", "W", "W_old", true);

        public CalcTemplate Template { get; } = new(MemberId, version, CalcDiscipline.Structural, "Member", "", "",
            [
                new CalcInputSpec(modulus, "Section modulus", CalcInputKind.Quantity, "mm^3", Default: 1000, Aliases: alias is null ? null : [alias]),
                new CalcInputSpec("d", depth ? "Depth of the plate" : "Width of the plate", CalcInputKind.Quantity, "mm", Default: 10, IsOptional: true,
                    MeaningChangedAt: depth ? "1.2.0" : null,
                    MeaningWas: depth ? "the width of the plate" : null,
                    MeaningNow: depth ? "the depth of the plate" : null),
            ],
            [], [], [], []);

        public void Run(CalcRunContext c)
        {
            c.Input(modulus);
            c.Input("d");
            c.Assign("s", $"{modulus} / d", "mm^2", "Area");
            c.Output("s", "Area");
        }
    }

    private const string DChanged = "d meant the width of the plate on test.member@1.1.0; since test.member@1.2.0 it means the depth of the plate";
    private const string WyChanged = "W_y meant the section modulus about the minor axis on test.section@1.1.0; since test.section@1.2.0 it means the section modulus about the major axis";

    private static TemplateVersionHistory Recorded(params string[] references) => new(references.Select(r => new KeyValuePair<string, string>(r, new string('a', 64))));

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit, CalcProvenance.Assumed("Test value."));

    /// <summary>A second service over the same store, evidence and context as <paramref name="host"/>, with another catalogue: the build after a template was revised.</summary>
    private static CalcInstanceService LaterBuild(LibraryTestHost host, params ICalcCalculator[] calculators) =>
        new(new CalcCatalogue(calculators, history: Recorded($"{SectionId}@1.1.0", $"{SectionId}@1.2.0", $"{MemberId}@1.1.0", $"{MemberId}@1.2.0")), host.Store, host.Evidence, host.Context, renderer: null, applicationVersion: "TempestOS later build") { PrincipalOverride = () => host.Principal };

    private static async Task<CalcInstance> CheckedAsync(LibraryTestHost host, Guid? project, string template, string name, params CalcInputValue[] inputs)
    {
        var created = await host.Service.CreateAsync(template, project, name, inputs);
        Assert.Empty(created.Blocks);
        var checkedOne = await host.Service.CheckAsync(created.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.True(checkedOne.Succeeded, checkedOne.Reason);
        return (await host.Store.FindInstanceAsync(created.Id))!;
    }

    private static async Task<CalcInstance> IssuedAsync(LibraryTestHost host, Guid? project, string template, string name, params CalcInputValue[] inputs)
    {
        var created = await CheckedAsync(host, project, template, name, inputs);
        var issued = await host.Service.IssueAsync(created.Id, $"ISS-{name}", "Client Co");
        Assert.True(issued.Succeeded, issued.Reason);
        return (await host.Store.FindInstanceAsync(created.Id))!;
    }

    private static void AssertUnchanged(CalcInstance? expected, CalcInstance? actual) =>
        Assert.Equal(CalcInstanceStore.Serialise(expected), CalcInstanceStore.Serialise(actual));

    [Fact]
    public void A_Meaning_Change_Is_Declared_At_A_Version_The_Template_Has_Reached_With_Both_Meanings_Said()
    {
        var member = Member.V120.Template;
        var d = member.Inputs.Single(i => i.Symbol == "d");
        Assert.Null(Record.Exception(() => new CalcCatalogue([Member.V120])));

        static CalcInputSpec With(CalcInputSpec spec, string? at = "1.2.0", string? was = "w", string? now = "n") => spec with { MeaningChangedAt = at, MeaningWas = was, MeaningNow = now };
        var later = Assert.Throws<ArgumentException>(() => new CalcCatalogue([new Replaced(member with { Inputs = [member.Inputs[0], With(d, at: "1.3.0")] })]));
        Assert.Contains("'d' says its meaning changed at version '1.3.0', which is not a version of the template up to 1.2.0", later.Message, StringComparison.Ordinal);
        Assert.Contains("not a version", Assert.Throws<ArgumentException>(() => new CalcCatalogue([new Replaced(member with { Inputs = [member.Inputs[0], With(d, at: "v1")] })])).Message, StringComparison.Ordinal);
        Assert.Contains("but not what it meant before and what it means now", Assert.Throws<ArgumentException>(() => new CalcCatalogue([new Replaced(member with { Inputs = [member.Inputs[0], With(d, now: "")] })])).Message, StringComparison.Ordinal);

        // The same for an output port.
        var section = new Section("1.2.0", major: true).Template;
        var port = section.OutputPorts![0];
        Assert.Contains("'W_y' says its meaning changed at version '2.0.0'", Assert.Throws<ArgumentException>(() => new CalcCatalogue([new Replaced(section with { OutputPorts = [port with { MeaningChangedAt = "2.0.0" }] }, new Section("1.2.0", true))])).Message, StringComparison.Ordinal);
        Assert.Contains("'W_y' says its meaning changed at 1.2.0 but not what", Assert.Throws<ArgumentException>(() => new CalcCatalogue([new Replaced(section with { OutputPorts = [port with { MeaningWas = " " }] }, new Section("1.2.0", true))])).Message, StringComparison.Ordinal);

        // A declared change is part of the version's content hash; a template that declares none hashes as before.
        var declared = new CalcCatalogue([Member.V120]).Get(MemberId).ContentHash;
        var undeclared = new CalcCatalogue([new Replaced(member with { Inputs = [member.Inputs[0], With(d, at: null, was: null, now: null)] })]).Get(MemberId).ContentHash;
        Assert.NotEqual(declared, undeclared);
        Assert.Equal(undeclared, new CalcCatalogue([new Replaced(member with { Inputs = [member.Inputs[0], With(d, at: null, was: null, now: null)] })]).Get(MemberId).ContentHash);
    }

    [Fact]
    public async Task An_Old_Draft_Does_Not_Carry_An_Input_Whose_Meaning_Changed_And_Carries_One_Stored_Under_Its_Alias()
    {
        var host = new LibraryTestHost(calculators: [Member.V120]);
        var draft = await host.Service.CreateAsync(MemberId, await host.ProjectAsync(), "Plate", [Q("W", 1200, "mm^3"), Q("d", 20, "mm")]);
        var older = draft with { TemplateReference = $"{MemberId}@1.1.0", Inputs = [Q("W_old", 1200, "mm^3"), Q("d", 20, "mm")] };

        var plan = host.Service.PlanDraftUpgrade(older)!;

        Assert.Equal(1200, plan.Inputs.Find("W")!.Number);
        Assert.Null(plan.Inputs.Find("W_old"));
        Assert.Null(plan.Inputs.Find("d"));
        Assert.Equal(["d"], plan.Dropped);
        Assert.Contains($"{DChanged}, so its stored value was not carried: enter it afresh.", plan.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("no longer has", plan.Message, StringComparison.Ordinal);

        // A draft made at or after the change carries d: the meaning it stored is the meaning it has.
        var current = host.Service.PlanDraftUpgrade(older with { TemplateReference = $"{MemberId}@1.2.0", TemplateHash = "stale" });
        Assert.Null(current);
        var same = new CalcInstanceService(new CalcCatalogue([new Member("1.3.0", "W", "W_old", true)], history: Recorded($"{MemberId}@1.2.0", $"{MemberId}@1.3.0")), host.Store, host.Evidence, host.Context, renderer: null, applicationVersion: "next") { PrincipalOverride = () => host.Principal };
        var carried = same.PlanDraftUpgrade(draft)!;
        Assert.Equal(20, carried.Inputs.Find("d")!.Number);
        Assert.Empty(carried.Dropped);
    }

    [Fact]
    public async Task An_Old_Draft_Holding_An_Input_Under_Two_Of_Its_Names_With_Different_Values_Carries_Neither_And_Names_Both()
    {
        var host = new LibraryTestHost(calculators: [Member.V120]);
        var draft = await host.Service.CreateAsync(MemberId, await host.ProjectAsync(), "Plate", [Q("W", 1200, "mm^3")]);
        var twice = draft with { TemplateReference = $"{MemberId}@1.1.0", Inputs = [Q("W_old", 1200, "mm^3"), Q("W", 1500, "mm^3"), Q("gone", 1, "mm")] };

        var plan = host.Service.PlanDraftUpgrade(twice)!;

        Assert.Null(plan.Inputs.Find("W"));
        Assert.Null(plan.Inputs.Find("W_old"));
        Assert.Equal(["W_old", "W", "gone"], plan.Dropped);
        Assert.Contains("W was stored as 'W_old' and as 'W' with different values, so neither was carried: enter it afresh.", plan.Message, StringComparison.Ordinal);
        Assert.Contains("1 input the new version no longer has (gone) was left out.", plan.Message, StringComparison.Ordinal);

        // The same value under both names (12 cm^3 is 12000 mm^3) is one input, carried once under its current symbol.
        var agreed = host.Service.PlanDraftUpgrade(twice with { Inputs = [Q("W_old", 12, "cm^3"), Q("W", 12000, "mm^3")] })!;
        Assert.Equal(12000, agreed.Inputs.Find("W")!.Number);
        Assert.Single(agreed.Inputs.Values);
        Assert.Empty(agreed.Dropped);
        Assert.DoesNotContain("neither", agreed.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_Issued_Calculation_Is_Not_Revised_Onto_A_Version_Where_An_Input_Came_To_Mean_Something_Else()
    {
        var host = new LibraryTestHost(calculators: [Member.V110]);
        var project = await host.ProjectAsync();
        var issued = await IssuedAsync(host, project, MemberId, "Plate", Q("W_old", 1200, "mm^3"), Q("d", 20, "mm"));
        var later = LaterBuild(host, Member.V120);

        var revised = await later.ReviseAsync(issued.Id);

        Assert.False(revised.Succeeded);
        Assert.Equal($"Calculation 'Plate' cannot be revised onto {MemberId}@1.2.0: {DChanged}, so its stored value cannot be carried. Start a new calculation on {MemberId}@1.2.0 and enter d afresh; the other inputs can be read from the stored sheet.", revised.Reason);
        AssertUnchanged(issued, await host.Store.FindInstanceAsync(issued.Id));

        // Without d (it is optional) the revision carries W_old as W; under both names with different values it is refused, with the same value once.
        var withoutD = await IssuedAsync(host, project, MemberId, "Plate 2", Q("W_old", 1200, "mm^3"));
        var carried = await later.ReviseAsync(withoutD.Id);
        Assert.True(carried.Succeeded, carried.Reason);
        Assert.Equal($"{MemberId}@1.2.0", carried.Instance!.TemplateReference);
        Assert.Equal(1200, Assert.Single(carried.Instance.Inputs, i => i.Symbol == "W").Number);

        // (A checked record can be rewritten by the store where an issued one cannot, and a checked calculation whose template moved on is stale, so it revises.)
        var both = await CheckedAsync(host, project, MemberId, "Plate 3", Q("W_old", 1200, "mm^3"));
        await host.Store.SaveInstanceAsync(both with { Inputs = [Q("W_old", 1200, "mm^3"), Q("W", 1500, "mm^3")] });
        var conflicting = await later.ReviseAsync(both.Id);
        Assert.False(conflicting.Succeeded);
        Assert.Equal("Calculation 'Plate 3' cannot be revised: W: the input is given as 'W' and as 'W_old' with different values. Keep one.", conflicting.Reason);
        Assert.Equal(CalcInstanceStatus.Checked, (await host.Store.FindInstanceAsync(both.Id))!.Status);

        var agreed = await CheckedAsync(host, project, MemberId, "Plate 4", Q("W_old", 1200, "mm^3"));
        await host.Store.SaveInstanceAsync(agreed with { Inputs = [Q("W_old", 1200, "mm^3"), Q("W", 1200, "mm^3")] });
        var once = await later.ReviseAsync(agreed.Id);
        Assert.True(once.Succeeded, once.Reason);
        Assert.Equal(1200, Assert.Single(once.Instance!.Inputs, i => i.Symbol == "W").Number);
        Assert.DoesNotContain(once.Instance.Inputs, i => i.Symbol == "W_old");
    }

    [Fact]
    public async Task A_Link_From_A_Revision_Made_Before_The_Ports_Meaning_Changed_Is_Refused_And_An_Existing_One_Is_Stale_And_Never_Moved()
    {
        var host = new LibraryTestHost(calculators: [new Section("1.1.0", major: false), Member.V120]);
        var project = await host.ProjectAsync();
        var section = await IssuedAsync(host, project, SectionId, "Beam section", Q("b", 100, "mm"), Q("h", 200, "mm"));
        var plate = await host.Service.CreateAsync(MemberId, project, "Plate", [Q("W", 1, "mm^3"), Q("d", 20, "mm")]);
        var linked = await host.Service.BindAsync(plate.Id, "W", section.Id, "W_y");
        Assert.True(linked.Succeeded, linked.Reason);
        var minor = linked.Instance!.Inputs.Single(i => i.Symbol == "W").Number;
        Assert.Equal(200 * 100 * 100 / 6.0, minor!.Value, 6);

        // The next build says W_y came to mean the major-axis modulus at 1.2.0.
        var later = LaterBuild(host, new Section("1.2.0", major: true), Member.V120);
        var refusal = $"'Beam section' rev A was made on {SectionId}@1.1.0, where {WyChanged}, so 'W_y' is not linked from it: revise 'Beam section' onto {SectionId}@1.2.0 and link to that revision.";

        // A new link from the old revision is refused, by symbol and by public name alike; nothing is bound.
        var another = await later.CreateAsync(MemberId, project, "Plate 2", [Q("W", 1, "mm^3"), Q("d", 20, "mm")]);
        var bySymbol = await later.BindAsync(another.Id, "W", section.Id, "W_y");
        Assert.False(bySymbol.Succeeded);
        Assert.Equal(refusal, bySymbol.Reason);
        var byName = await later.BindAsync(another.Id, "W", section.Id, "section.modulus_y");
        Assert.False(byName.Succeeded);
        Assert.Equal(refusal.Replace("'W_y' is not linked", "'section.modulus_y' is not linked", StringComparison.Ordinal), byName.Reason);
        Assert.Empty((await host.Store.FindInstanceAsync(another.Id))!.Links);

        // So is an input that arrives with such a link in its provenance (an import, a hand-written draft).
        var imported = await later.UpdateInputsAsync(another.Id, [Q("W", 1, "mm^3") with { Provenance = CalcProvenance.Link(section.Id, "W_y", "A") }, Q("d", 20, "mm")]);
        Assert.False(imported.Succeeded);
        Assert.Equal(refusal, imported.Reason);

        // The link made before the change is reported stale, and "update to the current revision" refuses while the upstream is still that revision.
        Assert.Contains(refusal, (await later.StalenessAsync(linked.Instance)).Reasons);
        var updated = await later.UpdateLinksAsync(plate.Id);
        Assert.False(updated.Succeeded);
        Assert.Equal(refusal, updated.Reason);

        // Once the section is revised onto 1.2.0, where W_y means the major-axis modulus, the link is not moved to the new revision: that would bind
        // a different quantity under the same name. It stays pinned to rev A with the value it took, and the sentence says what to do.
        var revised = await later.ReviseAsync(section.Id);
        Assert.True(revised.Succeeded, revised.Reason);
        Assert.Equal($"{SectionId}@1.2.0", revised.Instance!.TemplateReference);
        var moved = await later.UpdateLinksAsync(plate.Id);
        Assert.False(moved.Succeeded);
        Assert.Equal($"'W' took 'W_y' from 'Beam section' rev A, made on {SectionId}@1.1.0, where {WyChanged}, so the link is not moved to rev B: link 'W' by hand to the output of rev B that gives what it takes.", moved.Reason);
        var still = (await host.Store.FindInstanceAsync(plate.Id))!;
        Assert.Equal(section.Id, Assert.Single(still.Links).Link.UpstreamId);
        Assert.Equal(minor, still.Inputs.Single(i => i.Symbol == "W").Number);

        // A link to the revision made at the new version binds the major-axis modulus, as its name now says.
        var fresh = await later.BindAsync(another.Id, "W", revised.Instance.Id, "W_y");
        Assert.True(fresh.Succeeded, fresh.Reason);
        Assert.Equal(100 * 200 * 200 / 6.0, fresh.Instance!.Inputs.Single(i => i.Symbol == "W").Number!.Value, 6);
    }

    [Fact]
    public async Task A_Pack_Binding_Or_Example_Saved_From_A_Version_Where_The_Symbol_Meant_Something_Else_Is_Refused_Not_Rebound()
    {
        var host = new LibraryTestHost(calculators: [new Section("1.2.0", major: true), Member.V120]);
        CalcPackTemplate Chain(string? sectionVersion, string? memberVersion = null, string input = "W", IReadOnlyList<CalcInputValue>? example = null) => new("t", "Chain", "",
            [new CalcPackStep("s1", SectionId, "Section", TemplateVersion: sectionVersion), new CalcPackStep("s2", MemberId, "Member", Example: example, TemplateVersion: memberVersion)],
            [new CalcPackBinding("s1", "W_y", "s2", input)]);

        // A template composed in code, or saved from the current version, is as before.
        Assert.Null(host.Service.ValidatePackTemplate(Chain(null)));
        Assert.Null(host.Service.ValidatePackTemplate(Chain("1.2.0", "1.2.0")));

        // The output's meaning changed after the version the step was saved from: the binding is refused, never rebound to the new meaning.
        Assert.Equal($"Pack template 'Chain': step 's1' was saved from {SectionId}@1.1.0, where {WyChanged}, so the link s1.W_y → s2.W cannot be made; save the chain again from calculations on {SectionId}@1.2.0.",
            host.Service.ValidatePackTemplate(Chain("1.1.0")));

        // The same for the input the binding feeds, and for an example of it.
        Assert.Equal($"Pack template 'Chain': step 's2' was saved from {MemberId}@1.1.0, where {DChanged}, so the link s1.W_y → s2.d cannot be made; save the chain again from calculations on {MemberId}@1.2.0.",
            host.Service.ValidatePackTemplate(Chain(null, "1.1.0", input: "d")));
        Assert.Equal($"Pack template 'Chain': step 's2' was saved from {MemberId}@1.1.0, where {DChanged}, so its example for 'd' cannot be used; save the chain again from calculations on {MemberId}@1.2.0.",
            host.Service.ValidatePackTemplate(Chain(null, "1.1.0", example: [Q("d", 20, "mm")])));
        Assert.Null(host.Service.ValidatePackTemplate(Chain(null, "1.1.0", example: [Q("W_old", 1200, "mm^3")])));

        // A chain saved from calculations on the old version is refused on the build that declares the change, and a template it saved earlier is refused when listed.
        var earlier = new LibraryTestHost(calculators: [new Section("1.1.0", major: false), Member.V120]);
        var project = await earlier.ProjectAsync();
        var section = await IssuedAsync(earlier, project, SectionId, "Beam section", Q("b", 100, "mm"), Q("h", 200, "mm"));
        var plate = await earlier.Service.CreateAsync(MemberId, project, "Plate", [Q("W", 1, "mm^3"), Q("d", 20, "mm")]);
        Assert.True((await earlier.Service.BindAsync(plate.Id, "W", section.Id, "W_y")).Succeeded);
        var (saved, savedRefusal) = await earlier.Service.PackTemplateFromChainAsync("Beam and plate", [section.Id, plate.Id]);
        Assert.Null(savedRefusal);
        Assert.Equal("1.1.0", saved!.Steps[0].TemplateVersion);
        Assert.Equal("1.2.0", saved.Steps[1].TemplateVersion);

        var later = LaterBuild(earlier, new Section("1.2.0", major: true), Member.V120);
        var expected = $"Pack template 'Beam and plate': step 's1' was saved from {SectionId}@1.1.0, where {WyChanged}, so the link s1.W_y → s2.W cannot be made; save the chain again from calculations on {SectionId}@1.2.0.";
        var (again, againRefusal) = await later.PackTemplateFromChainAsync("Beam and plate", [section.Id, plate.Id]);
        Assert.Null(again);
        Assert.Equal(expected, againRefusal);
        var listed = Assert.Single(await later.ListPackTemplatesAsync(), t => t.Id == saved.Id);
        Assert.Equal(expected, later.ValidatePackTemplate(listed));
        var (made, refusal) = await later.InstantiatePackTemplateAsync(listed, project, "Beam and plate again");
        Assert.Null(made);
        Assert.Equal(expected, refusal);
    }

    private sealed class Replaced(CalcTemplate template, ICalcCalculator? inner = null) : ICalcCalculator
    {
        private readonly ICalcCalculator _inner = inner ?? Member.V120;

        public CalcTemplate Template { get; } = template;

        public void Run(CalcRunContext context) => _inner.Run(context);
    }
}
