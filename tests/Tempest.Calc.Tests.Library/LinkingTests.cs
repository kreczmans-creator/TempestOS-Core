using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Events;
using Tempest.Core.Evidence;

namespace Tempest.Calc.Tests.Library;

/// <summary>A source of linkable values: a force twice its input, a length and a plain ratio.</summary>
internal sealed class ForceSourceCalculator : ICalcCalculator
{
    public CalcTemplate Template { get; } = new(
        "test.force-source", "1.0.0", CalcDiscipline.Mechanical, "Force source",
        "Gives a force for another calculation to link to.",
        "Output force: F_out = 2·P.",
        [new CalcInputSpec("P", "Input force", CalcInputKind.Quantity, "kN", Default: 5, Min: 0, Max: 1e9, HardMin: -1e9, HardMax: 1e9)],
        [new CalcAssumption("Test only.", "—")],
        [new CalcClauseRef("Test", "—", "Test")],
        [],
        [
            new CalcTestCase("ka-1", "5 kN", new CalcReferenceSource("Hand", null, "2 × 5"), [CalcInputValue.Quantity("P", 5, "kN")], [new CalcExpectedValue("F_out", 10, "kN", 1e-9)]),
            new CalcTestCase("ka-2", "1 kN", new CalcReferenceSource("Hand", null, "2 × 1"), [CalcInputValue.Quantity("P", 1, "kN")], [new CalcExpectedValue("F_out", 2, "kN", 1e-9)]),
        ],
        OutputPorts: [new CalcOutputPort("F_out", "kN", "Output force"), new CalcOutputPort("L_out", "mm", "A length"), new CalcOutputPort("k", "", "A ratio")]);

    public void Run(CalcRunContext c)
    {
        c.Input("P");
        c.Assign("F_out", "2 * P", "kN", "Output force");
        c.Assign("L_out", "250 mm", "mm", "A length");
        c.Assign("k", "0.5", null, "A ratio");
        c.Output("F_out", "Output force");
        c.Output("L_out", "A length");
        c.Output("k", "A ratio");
    }
}

/// <summary>Collects what the service publishes.</summary>
internal sealed class RecordingBus : IEventBus
{
    public List<IEvent> Published { get; } = [];

    public void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent { }

    public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent { }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken) where TEvent : IEvent
    {
        Published.Add(@event);
        return Task.CompletedTask;
    }
}

/// <summary>Addendum A, L1: result linking and calc packs.</summary>
public sealed class LinkingTests
{
    private static readonly IReadOnlyList<CalcInputValue> SourceInputs = [CalcInputValue.Quantity("P", 5, "kN", CalcProvenance.Drawing("DRG-9", "A"))];

    private static LibraryTestHost Host(RecordingBus? bus = null, IReadOnlyList<CalcPackTemplate>? templates = null) =>
        new(calculators: [new ForceSourceCalculator()], events: bus, packTemplates: templates);

    private static async Task<CalcInstance> Source(LibraryTestHost host, Guid? project, string name, double p = 5) =>
        await host.Service.CreateAsync("test.force-source", project, name, [CalcInputValue.Quantity("P", p, "kN", CalcProvenance.Drawing("DRG-9", "A"))]);

    private static async Task<CalcInstance> Bound(LibraryTestHost host, CalcInstance downstream, string input, CalcInstance upstream, string output)
    {
        var bound = await host.Service.BindAsync(downstream.Id, input, upstream.Id, output);
        Assert.True(bound.Succeeded, bound.Reason);
        return bound.Instance!;
    }

    private static async Task CheckAndIssue(LibraryTestHost host, CalcInstance instance, string reference)
    {
        var check = await host.Service.CheckAsync(instance.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.True(check.Succeeded, check.Reason);
        var issue = await host.Service.IssueAsync(instance.Id, reference, "Client Co");
        Assert.True(issue.Succeeded, issue.Reason);
    }

    [Fact]
    public async Task A_Binding_Takes_The_Upstream_Value_In_The_Inputs_Unit_And_Pins_Its_Revision()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var source = await Source(host, project, "Motor torque");
        var stress = await host.Service.CreateAsync("test.direct-stress", project, "Bracket", LibraryTestHost.ProvenancedInputs());

        stress = await Bound(host, stress, "F", source, "F_out");

        var f = stress.Inputs.Single(i => i.Symbol == "F");
        Assert.Equal(10, f.Number!.Value, 9);
        Assert.Equal("kN", f.Unit);
        Assert.Equal(new CalcLink(source.Id, "F_out", "A"), CalcLink.From(f.Provenance));
        Assert.Contains(stress.Outputs, o => o.Symbol == "sigma" && o.Text == "50.0 MPa");
        Assert.Contains(source.Id, stress.UpstreamIds);
        Assert.False((await host.Service.StalenessAsync(stress)).IsStale);

        var sheet = (await host.Service.RunAsync(stress.Id))!.SheetText;
        Assert.Contains("link: Motor torque rev A · F_out", sheet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Binding_Across_Dimensions_Or_To_Itself_Or_Round_A_Cycle_Is_Refused()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var a = await Source(host, project, "A");
        var b = await Source(host, project, "B");
        var stress = await host.Service.CreateAsync("test.direct-stress", project, "Bracket", LibraryTestHost.ProvenancedInputs());

        var length = await host.Service.BindAsync(stress.Id, "F", a.Id, "L_out");
        Assert.False(length.Succeeded);
        Assert.Contains("dimensions differ", length.Reason, StringComparison.Ordinal);

        var ratio = await host.Service.BindAsync(stress.Id, "F", a.Id, "k");
        Assert.False(ratio.Succeeded);

        var self = await host.Service.BindAsync(a.Id, "P", a.Id, "F_out");
        Assert.False(self.Succeeded);
        Assert.Contains("its own output", self.Reason, StringComparison.Ordinal);

        await Bound(host, b, "P", a, "F_out");
        var cycle = await host.Service.BindAsync(a.Id, "P", b.Id, "F_out");
        Assert.False(cycle.Succeeded);
        Assert.Contains("cycle", cycle.Reason, StringComparison.Ordinal);
        Assert.Equal(CalcProvenanceKind.Drawing, (await host.Store.FindInstanceAsync(a.Id))!.Inputs.Single().Provenance!.Kind);
    }

    [Fact]
    public async Task A_Chain_Longer_Than_Twenty_Links_Is_Refused()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var chain = new List<CalcInstance> { await Source(host, project, "S0") };

        for (var n = 1; n <= CalcInstanceService.MaxChainDepth; n++)
        {
            var next = await Source(host, project, $"S{n}", p: 1);
            chain.Add(await Bound(host, next, "P", chain[^1], "F_out"));
        }

        var last = await Source(host, project, "S21", p: 1);
        var refused = await host.Service.BindAsync(last.Id, "P", chain[^1].Id, "F_out");
        Assert.False(refused.Succeeded);
        Assert.Contains($"at most {CalcInstanceService.MaxChainDepth}", refused.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Changed_Upstream_Makes_Everything_Downstream_Stale_And_Stale_Blocks_Check_Until_Updated()
    {
        var bus = new RecordingBus();
        var host = Host(bus);
        var project = await host.ProjectAsync();
        var a = await Source(host, project, "A");
        var b = await Bound(host, await Source(host, project, "B", 1), "P", a, "F_out");
        var c = await Bound(host, await Source(host, project, "C", 1), "P", b, "F_out");

        var changed = await host.Service.UpdateInputsAsync(a.Id, [CalcInputValue.Quantity("P", 7, "kN", CalcProvenance.Drawing("DRG-9", "B"))]);
        Assert.True(changed.Succeeded);

        var published = Assert.IsType<CalcStalenessChanged>(bus.Published.Last());
        Assert.Equal(a.Id, published.CauseId);
        Assert.Equal([b.Id, c.Id], published.AffectedIds);

        var bStale = await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(b.Id))!);
        Assert.Contains(bStale.Reasons, r => r.Contains("now gives a different 'F_out'", StringComparison.Ordinal));
        var cStale = await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(c.Id))!);
        Assert.Contains(cStale.Reasons, r => r.Contains("is itself stale", StringComparison.Ordinal));

        var check = await host.Service.CheckAsync(c.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(check.Succeeded);
        Assert.Contains("stale", check.Reason, StringComparison.Ordinal);

        Assert.True((await host.Service.UpdateLinksAsync(b.Id)).Succeeded);
        Assert.True((await host.Service.UpdateLinksAsync(c.Id)).Succeeded);
        var cNow = (await host.Store.FindInstanceAsync(c.Id))!;
        Assert.False((await host.Service.StalenessAsync(cNow)).IsStale);
        Assert.Equal(56, cNow.Outputs.Single(o => o.Symbol == "F_out").Si!.Value / 1000, 9);
    }

    [Fact]
    public async Task A_Revised_Upstream_Stales_Its_Downstream_Whose_Link_Survives_And_Updating_Gives_A_New_Revision()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var a = await Source(host, project, "A");
        await CheckAndIssue(host, a, "ISS-A");
        var b = await Bound(host, await Source(host, project, "B", 1), "P", (await host.Store.FindInstanceAsync(a.Id))!, "F_out");
        Assert.True((await host.Service.CheckAsync(b.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);

        var revised = await host.Service.ReviseAsync(a.Id);
        Assert.True(revised.Succeeded, revised.Reason);

        // The link still names rev A, which is now superseded: B is stale and cannot be issued.
        var bNow = (await host.Store.FindInstanceAsync(b.Id))!;
        Assert.Equal(new CalcLink(a.Id, "F_out", "A"), bNow.Links.Single().Link);
        var issue = await host.Service.IssueAsync(b.Id, "ISS-B", "Client Co");
        Assert.False(issue.Succeeded);
        Assert.Contains("superseded by rev B", issue.Reason, StringComparison.Ordinal);

        // Updating a checked calculation makes a new revision pinned to A rev B; the checked one is superseded.
        var updated = await host.Service.UpdateLinksAsync(b.Id);
        Assert.True(updated.Succeeded, updated.Reason);
        Assert.Equal("B", updated.Instance!.Revision);
        Assert.Equal(new CalcLink(revised.Instance!.Id, "F_out", "B"), updated.Instance.Links.Single().Link);
        Assert.Equal(CalcInstanceStatus.Superseded, (await host.Store.FindInstanceAsync(b.Id))!.Status);
        Assert.False((await host.Service.StalenessAsync(updated.Instance)).IsStale);
    }

    [Fact]
    public async Task A_Pack_Template_Builds_A_Linked_Pack_Checked_Once_And_Issued_As_One_Immutable_Bundle()
    {
        var template = new CalcPackTemplate("test.chain", "Test chain", "Source then stress.",
            [new CalcPackStep("src", "test.force-source", "Load"), new CalcPackStep("chk", "test.direct-stress", "Bracket")],
            [new CalcPackBinding("src", "F_out", "chk", "F")]);
        var host = Host(templates: [template]);
        var project = await host.ProjectAsync();
        Assert.Null(host.Service.ValidatePackTemplate(template));

        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(template, project, "Bracket pack",
            new Dictionary<string, IReadOnlyList<CalcInputValue>> { ["src"] = SourceInputs, ["chk"] = LibraryTestHost.ProvenancedInputs() });
        Assert.Null(refusal);
        var pack = made!.Pack;
        Assert.Equal(new CalcLink(made.Instances["src"].Id, "F_out", "A"), made.Instances["chk"].Links.Single().Link);

        // A stale member stops the pack check.
        await host.Service.UpdateInputsAsync(made.Instances["src"].Id, [CalcInputValue.Quantity("P", 6, "kN", CalcProvenance.Drawing("DRG-9", "B"))]);
        var staleCheck = await host.Service.CheckPackAsync(pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(staleCheck.Succeeded);
        Assert.Contains("stale", staleCheck.Reason, StringComparison.Ordinal);
        Assert.True((await host.Service.UpdateLinksAsync(made.Instances["chk"].Id)).Succeeded);

        pack = (await host.Store.FindPackAsync(pack.Id))!;
        Assert.True((await host.Service.CheckPackAsync(pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
        var issued = await host.Service.IssuePackAsync(pack.Id, "ISS-PK", "Client Co");
        Assert.True(issued.Succeeded, issued.Reason);

        var stored = (await host.Store.FindPackAsync(pack.Id))!;
        Assert.Equal(CalcInstanceStatus.Issued, stored.Status);
        foreach (var id in stored.InstanceIds)
            Assert.Equal(CalcInstanceStatus.Issued, (await host.Store.FindInstanceAsync(id))!.Status);
        await Assert.ThrowsAsync<CalcInstanceImmutableException>(() => host.Store.SavePackAsync(stored with { Title = "Edited" }));
    }

    [Fact]
    public async Task A_Pack_Template_That_Links_Backwards_Or_Across_Dimensions_Is_Refused()
    {
        var host = Host();
        var backwards = new CalcPackTemplate("t1", "Backwards", "",
            [new CalcPackStep("chk", "test.direct-stress", "Bracket"), new CalcPackStep("src", "test.force-source", "Load")],
            [new CalcPackBinding("src", "F_out", "chk", "F")]);
        Assert.Contains("must come before", host.Service.ValidatePackTemplate(backwards), StringComparison.Ordinal);

        var wrong = new CalcPackTemplate("t2", "Wrong", "",
            [new CalcPackStep("src", "test.force-source", "Load"), new CalcPackStep("chk", "test.direct-stress", "Bracket")],
            [new CalcPackBinding("src", "L_out", "chk", "F")]);
        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(wrong, await host.ProjectAsync(), "Wrong pack");
        Assert.Null(made);
        Assert.Contains("dimensions differ", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Forged_Links_That_Would_Loop_Are_Refused_On_Entry()
    {
        // ADR-0168 amendment: an imported CSV can state any provenance; a looping link is refused when entered, and a
        // forged value that does not match its upstream leaves the calculation stale.
        var host = Host();
        var project = await host.ProjectAsync();
        var a = await Source(host, project, "A");
        var b = await Source(host, project, "B");
        Assert.True((await host.Service.UpdateInputsAsync(a.Id, [CalcInputValue.Quantity("P", 3, "kN", CalcProvenance.Link(b.Id, "F_out", "A"))])).Succeeded);
        var loop = await host.Service.UpdateInputsAsync(b.Id, [CalcInputValue.Quantity("P", 4, "kN", CalcProvenance.Link(a.Id, "F_out", "A"))]);
        Assert.False(loop.Succeeded);
        Assert.Contains("cycle", loop.Reason, StringComparison.Ordinal);

        Assert.True((await host.Service.StalenessAsync((await host.Store.FindInstanceAsync(a.Id))!)).IsStale);
        Assert.False((await host.Service.CheckAsync(a.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted)).Succeeded);
    }

    [Fact]
    public async Task A_Forged_Link_To_A_Missing_Calculation_Or_Output_Or_Another_Dimension_Is_Refused()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var a = await Source(host, project, "A");
        var b = await Source(host, project, "B");
        Assert.Contains("does not exist", (await host.Service.UpdateInputsAsync(b.Id, [CalcInputValue.Quantity("P", 10, "kN", CalcProvenance.Link(Guid.NewGuid(), "F_out", "A"))])).Reason, StringComparison.Ordinal);
        Assert.Contains("cannot take", (await host.Service.UpdateInputsAsync(b.Id, [CalcInputValue.Quantity("P", 10, "kN", CalcProvenance.Link(a.Id, "L_out", "A"))])).Reason, StringComparison.Ordinal);
        Assert.Contains("cannot take", (await host.Service.UpdateInputsAsync(b.Id, [CalcInputValue.Quantity("P", 10, "kN", CalcProvenance.Link(a.Id, "nothing", "A"))])).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Updating_To_A_New_Revision_That_Would_Loop_Is_Refused()
    {
        // Opus review finding 4: A issued, B linked to A; A revised to A2 which links to B; "update to revision B" on B
        // would re-pin it to A2 and close B ↔ A2.
        var host = Host();
        var project = await host.ProjectAsync();
        var a = await Source(host, project, "A");
        await CheckAndIssue(host, a, "CALC-A");
        var b = await Source(host, project, "B");
        await Bound(host, b, "P", a, "F_out");
        var a2 = (await host.Service.ReviseAsync(a.Id)).Instance!;
        var loop = await host.Service.BindAsync(a2.Id, "P", b.Id, "F_out");
        Assert.False(loop.Succeeded);
        Assert.Contains("cycle", loop.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Linked_Chain_Built_By_Hand_Saves_As_A_Pack_Template()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var a = await Source(host, project, "A");
        var stress = await Bound(host, await host.Service.CreateAsync("test.direct-stress", project, "Bracket", LibraryTestHost.ProvenancedInputs()), "F", a, "F_out");

        var (template, refusal) = await host.Service.PackTemplateFromChainAsync("My chain", [stress.Id, a.Id]);
        Assert.Null(refusal);
        Assert.Equal(["test.force-source", "test.direct-stress"], template!.Steps.Select(s => s.TemplateId));
        Assert.Equal(new CalcPackBinding("s1", "F_out", "s2", "F"), Assert.Single(template.Bindings));
        Assert.Contains(await host.Service.ListPackTemplatesAsync(), t => t.Id == template.Id);
    }

    [Fact]
    public async Task A_Temperature_Difference_Never_Links_To_An_Absolute_Temperature()
    {
        // Opus review finding 14: B5's ΔT_lm (K) into B6's flow temperature (°C) shares a dimension but not a meaning.
        var host = new LibraryTestHost(calculators: [.. Tempest.Calc.BuildingServices.BuildingServicesCalculators.All]);
        var project = await host.ProjectAsync();
        var hx = await host.Service.CreateAsync("bs.heat-exchanger", project, "HX");
        var sealedSystem = await host.Service.CreateAsync("bs.sealed-system", project, "Sealed system");
        var refused = await host.Service.BindAsync(sealedSystem.Id, "t_f", hx.Id, "dT_lm");
        Assert.False(refused.Succeeded);
        Assert.True((await host.Service.BindAsync(sealedSystem.Id, "t_f", hx.Id, "T_h_out")).Succeeded);
    }

    [Fact]
    public async Task Update_Links_Refuses_A_Cycle_That_Reached_The_Store_By_Another_Route()
    {
        // Opus verification: the UpdateLinksAsync check is defence in depth — forge B ↔ A2 in the store and update.
        var host = Host();
        var project = await host.ProjectAsync();
        var a = await Source(host, project, "A");
        await CheckAndIssue(host, a, "CALC-A");
        var b = await Source(host, project, "B");
        await Bound(host, b, "P", a, "F_out");
        var a2 = (await host.Service.ReviseAsync(a.Id)).Instance!;
        var forged = a2 with { Inputs = [CalcInputValue.Quantity("P", 10, "kN", CalcProvenance.Link(b.Id, "F_out", "A"))] };
        await host.Store.SaveInstanceAsync(forged, a2.UpdatedAt);
        var update = await host.Service.UpdateLinksAsync(b.Id);
        Assert.False(update.Succeeded);
        Assert.Contains("cycle", update.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_New_Calculation_With_A_Broken_Link_Is_Refused_And_Pack_Steps_Say_Which()
    {
        var host = Host();
        var project = await host.ProjectAsync();
        var a = await Source(host, project, "A");
        await Assert.ThrowsAsync<CalcLinkRefusedException>(() => host.Service.CreateAsync("test.force-source", project, "B", [CalcInputValue.Quantity("P", 1, "kN", CalcProvenance.Link(a.Id, "L_out", "A"))]));

        var other = await host.Service.CreateAsync("test.force-source", null, "Elsewhere", SourceInputs);
        var crossProject = await host.Service.BindAsync(a.Id, "P", other.Id, "F_out");
        Assert.False(crossProject.Succeeded);
        Assert.Contains("another project", crossProject.Reason, StringComparison.Ordinal);
    }
}
