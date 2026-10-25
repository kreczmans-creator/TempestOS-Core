using Tempest.Calc.FluidPower;
using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;
using static Tempest.Calc.Tests.Library.Chains.ChainKit;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// Addendum B, §B7 chain 5: the actuated linkage M9 → F1 → F2 from the built-in pack template. A slider-crank ejector is to be replaced by a hydraulic cylinder: the linkage's peak slide speed and peak slide-direction force
/// are the extend speed the cylinder must reach and the load it must carry, and the cylinder's swept volume and supply pressure size an emergency-stroke accumulator. <b>The M9 → F1 link is a kinematic proxy</b> (DB-04): a
/// cylinder-actuated linkage runs at about constant cylinder speed, not at constant crank speed, so the crank's peaks are what the cylinder must match, not what it would reproduce through the stroke. Every number is
/// recomputed in python3 (a 400 000-point scan of the slider for its peak speed; closed form for the rest).
/// </summary>
public sealed class ActuatedLinkageChainTests
{
    private static LibraryTestHost Host() => new(
        calculators: [.. MechanicalCalculators.All, .. FluidPowerCalculators.All],
        packTemplates: CalcPackTemplates.All);

    private static Task<CalcPackInstantiation> InstantiateAsync(LibraryTestHost host) =>
        ChainKit.InstantiateAsync(host, CalcPackTemplates.ActuatedLinkage, "Ejector cylinder", ChainInputs.ActuatedLinkage(host));

    [Fact]
    public async Task Values_Flow_Down_Every_Link_Of_The_Chain_With_Their_Units()
    {
        var host = Host();
        Assert.Null(host.Service.ValidatePackTemplate(CalcPackTemplates.ActuatedLinkage));
        var made = await InstantiateAsync(host);
        var (m9, f1, f2) = (made.Instances["m9"], made.Instances["f1"], made.Instances["f2"]);
        Assert.Equal(3, made.Pack.InstanceIds.Count);

        // M9: a slider-crank, crank 100 mm, rod 300 mm, in line, 30 rpm (ω = π rad/s), a 40 kg slide against a 3 kN load that always opposes the motion.
        // Closed forms: stroke 2r = 200 mm; peak slide acceleration r ω² (1 + r/l) = 1.31595 m/s² at the outer dead centre; the slide-direction force there m a + F_L = 40 · 1.31595 + 3000 = 3052.638 N.
        // The peak slide speed has no closed form (a scan of dx/dθ · ω, 400 000 points): 0.3313248 m/s, a little above the crank-pin speed r ω = 0.31416 m/s.
        var omega = Math.PI;
        Assert.Equal(0.2, Si(m9, "stroke"), 12);
        Assert.Equal(0.2, Si(m9, "stroke_closed"), 12);
        Assert.Equal(0.1 * omega * omega * (1 + 0.1 / 0.3), Si(m9, "peak_accel"), 9);
        Assert.Equal(1.315947253478581, Si(m9, "peak_accel"), 9);
        Assert.Equal(40 * 0.1 * omega * omega * (1 + 0.1 / 0.3) + 3000, Si(m9, "follower_force"), 6);
        Assert.Equal(3052.637890139143, Si(m9, "follower_force"), 6);
        Assert.Equal(0.3313247851555061, Si(m9, "output_motion"), 9);
        Assert.True(Si(m9, "output_motion") > 0.1 * omega, "the slide peaks a little faster than the crank pin");
        Assert.Equal(30, Si(m9, "n_in") * 60 / (2 * Math.PI), 9);
        Assert.Equal(3, m9.Checks.Count);
        Assert.All(m9.Checks, c => Assert.True(c.Passed, c.Name));

        // M9 → F1: the peak slide speed (m/s into mm/s) is the required extend speed, and the peak slide force (N into kN) is the load to push and, conservatively, to pull; the flow field is omitted, so the speed gives it.
        Assert.Equal(["F_pull", "F_push", "v_req"], f1.Links.Select(l => l.Input.Symbol).Order(StringComparer.Ordinal));
        Assert.Equal(Si(m9, "output_motion"), InputSi(f1, "v_req"), 9);
        Assert.Equal(331.3247851555061, f1.Inputs.Single(i => i.Symbol == "v_req").Number!.Value, 6);
        Assert.Equal("mm/s", f1.Inputs.Single(i => i.Symbol == "v_req").Unit);
        Assert.Equal(Si(m9, "follower_force"), InputSi(f1, "F_push"), 6);
        Assert.Equal(Si(m9, "follower_force"), InputSi(f1, "F_pull"), 6);
        Assert.Equal(3.052637890139143, f1.Inputs.Single(i => i.Symbol == "F_push").Number!.Value, 9);
        Assert.Equal("kN", f1.Inputs.Single(i => i.Symbol == "F_push").Unit);
        Assert.DoesNotContain(f1.Inputs, i => i.Symbol == "Q");
        Assert.Equal("output_motion", f1.Links.Single(l => l.Input.Symbol == "v_req").Link.OutputSymbol);
        Assert.Equal("follower_force", f1.Links.Single(l => l.Input.Symbol == "F_push").Link.OutputSymbol);

        // The cylinder's stroke is the slide's (the linkage has no stroke port, so it is typed and guarded).
        Assert.Equal(Si(m9, "stroke"), InputSi(f1, "stroke"), 12);

        // F1: 40/22 at 100 bar. Q = v_req A_b/η_vc = 0.3313248 · 1.256637·10⁻³/0.98 = 25.4911 L/min; F_ext = p A_b η_m = 11.938 kN (retract 8.327 kN) cover the 3.053 kN peak force with room to spare;
        // v_ext is the required speed; ΔV = A_b · 0.2 m = 0.25133 L; the cap-port line carries Q A_b/A_a = 36.546 L/min at 3.029 m/s (16 mm line); the rod, judged at the stall thrust p A_b = 12.566 kN, has u = 0.282.
        var areaBore = Math.PI * 0.04 * 0.04 / 4;
        Assert.Equal(areaBore, Si(f1, "A_b"), 15);
        Assert.Equal(Si(m9, "output_motion") * areaBore / 0.98, Si(f1, "Q"), 12);
        Assert.Equal(25.491122718287865, Si(f1, "Q") * 60000, 6);
        Assert.Equal(Si(m9, "output_motion"), Si(f1, "v_ext"), 12);
        Assert.Equal(11938.052083641212, Si(f1, "F_ext"), 6);
        Assert.Equal(8326.791328339747, Si(f1, "F_ret"), 6);
        Assert.Equal(0.25570653141329414, Si(f1, "u_push"), 9);
        Assert.Equal(0.3666043461122496, Si(f1, "u_pull"), 9);
        Assert.Equal(0.6036373038199684, Si(f1, "t_ext"), 8);
        Assert.Equal(0.421037019414428, Si(f1, "t_ret"), 8);
        Assert.Equal(areaBore * 0.2, Si(f1, "dV"), 12);
        Assert.Equal(36.54641249933744, Si(f1, "Q_prs") * 60000, 6);
        Assert.Equal(3.029449063304679, Si(f1, "v_prs"), 8);
        Assert.Equal(1.2408623363295963, Si(f1, "v_rtn"), 8);
        Assert.Equal(0.3380865154648022, Si(f1, "v_suc"), 8);
        Assert.Equal(100e5 * areaBore, Si(f1, "F_stall"), 6);
        Assert.Equal(155961.72487963346, Si(f1, "F_cr"), 4);
        Assert.Equal(0.282006993601804, Si(f1, "u_buck"), 9);
        Assert.Equal(6, f1.Checks.Count);
        Assert.All(f1.Checks, c => Assert.True(c.Passed, c.Name));
        Assert.DoesNotContain(f1.Warnings, w => w.RuleId == "v-req-q");

        // F1 → F2: the swept volume of the larger stroke (0.25133 L) and the supply pressure (100 bar gauge, so the accumulator's pressures stay on the Gauge basis) size the emergency-stroke accumulator:
        // V0 = f_T ΔV/[(p0/p1)^(1/n) − (p0/p2)^(1/n)] on absolute pressures, p0 = 50, p1 = 60, p2 = 100 bar gauge, n = 1.4, 10 to 50 °C: 1.0779 L.
        Assert.Equal(["dV", "p_2"], f2.Links.Select(l => l.Input.Symbol).Order(StringComparer.Ordinal));
        Assert.Equal(Si(f1, "dV"), InputSi(f2, "dV"), 15);
        Assert.Equal(Si(f1, "p"), InputSi(f2, "p_2"), 6);
        Assert.Equal("Gauge", f2.Inputs.Single(i => i.Symbol == "p_basis").Text);
        Assert.DoesNotContain(f2.Warnings, w => w.RuleId == "link-basis");
        Assert.Equal(1.0778782868400335e-3, Si(f2, "V0"), 12);
        Assert.Equal(0.9290013052720334, Si(f2, "u_r0"), 9);
        Assert.Equal(0.564966835088001, Si(f2, "u_rp"), 9);
        Assert.Equal(2, f2.Checks.Count);
        Assert.All(f2.Checks, c => Assert.True(c.Passed, c.Name));

        // The lowest working pressure (60 bar) still finishes the stroke under the peak force: 3.0526 kN/(A_b · 0.95) = 25.6 bar.
        Assert.True(InputSi(f2, "p_1") > Si(m9, "follower_force") / (areaBore * 0.95));
        Assert.Equal(25.57, Si(m9, "follower_force") / (areaBore * 0.95) / 1e5, 2);
        Assert.All(made.Instances.Values, i => Assert.Empty(i.Blocks));
    }

    [Fact]
    public async Task Every_Binding_Of_The_Template_Is_A_Link_From_The_Output_It_Names()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var template = CalcPackTemplates.ActuatedLinkage;

        Assert.Equal(["m9", "f1", "f2"], template.Steps.Select(s => s.Key));
        Assert.Equal(
            [("m9", "output_motion", "f1", "v_req"), ("m9", "follower_force", "f1", "F_push"), ("m9", "follower_force", "f1", "F_pull"), ("f1", "dV", "f2", "dV"), ("f1", "p", "f2", "p_2")],
            template.Bindings.Select(b => (b.FromStep, b.Output, b.ToStep, b.Input)));
        Assert.Contains(template.Steps, s => s.Key == "f1" && s.Omit is ["Q"]);

        foreach (var b in template.Bindings)
        {
            var link = CalcLink.From(made.Instances[b.ToStep].Inputs.Single(i => i.Symbol == b.Input).Provenance);
            Assert.NotNull(link);
            Assert.Equal(made.Instances[b.FromStep].Id, link!.UpstreamId);
            Assert.Equal(b.Output, link.OutputSymbol);
        }

        // Nothing else is linked, and the proxy is said where it matters: on the pack's description and on the cylinder input it feeds.
        Assert.Equal(template.Bindings.Count, made.Instances.Values.Sum(i => i.Links.Count));
        Assert.Empty(made.Instances.Values.SelectMany(i => i.CellLinks));
        Assert.Contains("kinematic proxy", template.Description, StringComparison.Ordinal);
        Assert.Contains("kinematic proxy", host.Catalogue.Get("fp.cylinder").Template.FindInput("v_req")!.Description, StringComparison.Ordinal);
        Assert.Contains("kinematic proxy", host.Catalogue.Get("mech.mechanism").Template.OutputPorts!.Single(p => p.Symbol == "output_motion").Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_Faster_Crank_Makes_The_Cylinder_And_The_Accumulator_Stale_Refuse_The_Check_And_The_Issue_And_Updating_In_Order_Carries_The_Values_Through()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        var f1Before = made.Instances["f1"];
        var f2Before = made.Instances["f2"];

        // 45 rpm: the peak speed rises by 1.5 and the peak acceleration (so the inertia force) by 2.25.
        await ChainKit.ReviseAndUpdateAsync(host, made, "m9", "n_in", 1.5, ["f1", "f2"]);

        var m9 = await Fresh(host, made, "m9");
        var (f1, f2) = (await Fresh(host, made, "f1"), await Fresh(host, made, "f2"));

        Assert.Equal(45, Si(m9, "n_in") * 60 / (2 * Math.PI), 9);
        Assert.Equal(0.4969871777332593, Si(m9, "output_motion"), 9);
        Assert.Equal(3118.435252813072, Si(m9, "follower_force"), 6);

        // ... and the update carried each one to the cylinder: the flow follows the speed (38.237 L/min), the loads follow the force, the line velocity rises (4.544 m/s, still inside the 5 m/s limit).
        Assert.Equal(Si(m9, "output_motion"), InputSi(f1, "v_req"), 9);
        Assert.Equal(Si(m9, "follower_force"), InputSi(f1, "F_push"), 6);
        Assert.Equal(38.23668407743181, Si(f1, "Q") * 60000, 6);
        Assert.Equal(1.5 * Si(f1Before, "Q"), Si(f1, "Q"), 12);
        Assert.Equal(0.4024248692133122, Si(f1, "t_ext"), 6);
        Assert.Equal(54.81961874900618, Si(f1, "Q_prs") * 60000, 6);
        Assert.Equal(4.5441735949570194, Si(f1, "v_prs"), 6);
        Assert.Equal(0.2612180974722236, Si(f1, "u_push"), 9);
        Assert.Equal(0.37450623293508756, Si(f1, "u_pull"), 9);
        Assert.All(f1.Checks, c => Assert.True(c.Passed, c.Name));

        // The accumulator takes the stroke volume and the pressure, which a faster crank does not move: it is stale (its upstream was revised) and the update re-pins it to the same sizing.
        Assert.Equal(Si(f2Before, "V0"), Si(f2, "V0"), 12);
        Assert.Equal(Si(f1, "dV"), InputSi(f2, "dV"), 15);
        Assert.All(new[] { f1, f2 }, i => Assert.Empty(i.Blocks));
    }

    [Fact]
    public async Task While_Stale_The_Pack_Check_And_Issue_Are_Refused_With_Reasons_That_Name_The_Stale_Step()
    {
        var host = Host();
        var made = await InstantiateAsync(host);
        await AuthoredByAnotherAsync(host, made);
        var m9 = made.Instances["m9"];

        var revised = await host.Service.UpdateInputsAsync(m9.Id, Scale("n_in", 1.5)(m9.Inputs));
        Assert.True(revised.Succeeded, revised.Reason);

        foreach (var key in new[] { "f1", "f2" })
            Assert.True((await host.Service.StalenessAsync(await Fresh(host, made, key))).IsStale, $"{key} should be stale");

        var f1Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "f1"))).Reasons;
        Assert.Contains(f1Reasons, r => r.Contains("'v_req'", StringComparison.Ordinal) && r.Contains("different 'output_motion'", StringComparison.Ordinal));
        Assert.Contains(f1Reasons, r => r.Contains("'F_push'", StringComparison.Ordinal) && r.Contains("different 'follower_force'", StringComparison.Ordinal));
        Assert.Contains(f1Reasons, r => r.Contains("'F_pull'", StringComparison.Ordinal) && r.Contains("different 'follower_force'", StringComparison.Ordinal));

        // The accumulator takes nothing from the linkage: it is stale only because the cylinder, which it is linked to, is.
        var f2Reasons = (await host.Service.StalenessAsync(await Fresh(host, made, "f2"))).Reasons;
        Assert.Contains(f2Reasons, r => r.Contains("'Replacement cylinder', which 'dV' is linked to, is itself stale", StringComparison.Ordinal));

        host.Principal = "j.reviewer";
        var check = await host.Service.CheckPackAsync(made.Pack.Id, "J. Reviewer", "Client Co", "OK", CheckOutcome.Accepted);
        Assert.False(check.Succeeded);
        Assert.Contains("Pack member 'Replacement cylinder' is stale", check.Reason, StringComparison.Ordinal);
        Assert.Contains("Update it before checking the pack", check.Reason, StringComparison.Ordinal);

        var issue = await host.Service.IssuePackAsync(made.Pack.Id, "EJT-STALE", "Client Co");
        Assert.False(issue.Succeeded);
        Assert.Contains("check it before issuing", issue.Reason, StringComparison.Ordinal);
        Assert.NotEqual(CalcInstanceStatus.Issued, (await host.Store.FindPackAsync(made.Pack.Id))!.Status);
    }

    [Fact]
    public async Task A_Pack_Checked_While_Current_Is_Refused_At_The_Issue_Staleness_Gate_When_The_Load_Fed_To_The_Linkage_Moves_On_And_Every_Downstream_Step_Is_Stale()
    {
        var host = Host();
        var made = await InstantiateAsync(host);

        // The linkage takes its slide load from a cylinder circuit outside the pack, which becomes a new revision after the check: the linkage is stale with no pack member touched, and so are the cylinder and the
        // accumulator; the pack is still Checked, and Issue is refused naming the linkage.
        // A 40/22 cylinder at 30 bar (3.58 kN) outside the pack presses on the slide: its extend force becomes the slide's load, which is then 19 % above the typed 3 kN; the cylinder in the pack still covers the new peak force.
        var press = ChainInputs.ActuatedLinkage(host)["f1"].Select(v => v.Symbol == "p" ? CalcInputValue.Quantity("p", 30, "bar") with { Provenance = CalcProvenance.Assumed("Outside press cylinder: supply pressure.") } : v).Concat(
        [
            CalcInputValue.Quantity("Q", 36, "L/min") with { Provenance = CalcProvenance.Assumed("Outside press cylinder: circuit flow.") },
            CalcInputValue.Quantity("F_push", 3, "kN") with { Provenance = CalcProvenance.Assumed("Outside press cylinder: push load.") },
            CalcInputValue.Quantity("F_pull", 2, "kN") with { Provenance = CalcProvenance.Assumed("Outside press cylinder: pull load.") },
        ]).ToList();
        await ChainProofs.AnOutsideRevisionAfterTheCheckMakesTheWholeChainStaleAndRefusesIssueAsync(host, made, "m9", "F_load", "fp.cylinder", press, "F_ext", ["f1", "f2"]);
    }

    [Fact]
    public async Task The_Chain_Is_Checked_And_Issued_As_One_Bundle_With_Every_Link_On_The_Sheet()
    {
        var host = Host();
        var made = await InstantiateAsync(host);

        var sheet = await CheckAndIssueAsync(host, made, CalcPackTemplates.ActuatedLinkage, "EJT-001", "Client Co", "Checked against SPEC-EJT-02.");

        Assert.Contains("link: Ejector slider-crank rev A · output_motion", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Ejector slider-crank rev A · follower_force", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Replacement cylinder rev A · dV", sheet, StringComparison.Ordinal);
        Assert.Contains("link: Replacement cylinder rev A · p", sheet, StringComparison.Ordinal);

        // The checker is independent: the principal who checked and issued the pack is not among the authors of any step.
        foreach (var key in made.Instances.Keys)
        {
            var member = await Fresh(host, made, key);
            Assert.NotEmpty(member.Authors);
            Assert.DoesNotContain(host.Principal, member.Authors);
        }
    }

    [Fact]
    public async Task The_Chain_Is_Created_As_It_Stands_And_Every_Input_Is_An_Example_To_Replace_Or_A_Link()
    {
        var host = Host();
        var template = CalcPackTemplates.ActuatedLinkage;
        Assert.All(template.Steps, s => Assert.NotEmpty(s.Example ?? []));

        var (made, refusal) = await host.Service.InstantiatePackTemplateAsync(template, await host.ProjectAsync(), "Ejector cylinder", null);

        Assert.True(refusal is null, refusal);
        Assert.Equal(3, made!.Pack.InstanceIds.Count);
        Assert.All(made.Instances.Values, i => Assert.Empty(i.Blocks));
        Assert.All(made.Instances.Values, i => Assert.All(i.Inputs, input =>
            Assert.True(input.Provenance is { Kind: CalcProvenanceKind.Link } || (input.Provenance?.Kind == CalcProvenanceKind.Assumption && input.Provenance.Reference == CalcInstanceService.ExampleBasis), $"{i.Name}.{input.Symbol}: {input.Provenance}")));
    }
}
