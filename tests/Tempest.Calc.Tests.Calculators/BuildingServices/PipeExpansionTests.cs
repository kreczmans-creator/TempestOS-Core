using CsCheck;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.BuildingServices;

public sealed class PipeExpansionTests
{
    private static readonly PipeExpansionCalculator Pipe = new();

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    /// <summary>The first known-answer case (steel 88.9 × 3.2 mm, 50 m, 10 → 80 °C, 6 m offset leg, Sa 100 MPa, K1 100 mm²/m²) with the given inputs replaced.</summary>
    private static CalcInputSet Sheet(params CalcInputValue[] over)
    {
        var set = CalcTemplateHasher.DefaultInputs(Pipe.Template);

        foreach (var v in over)
            set = set.With(v);

        return set;
    }

    private static CalcRunResult Run(params CalcInputValue[] over) => CalcRunner.Run(Pipe, Sheet(over));

    private static CalcInputValue Loop() => CalcInputValue.Choice("layout", PipeExpansionCalculator.UpLoop);

    private static string Screen(CalcRunResult r) => ((TextValue)r.FindOutput("screen")!.Value).Value;

    // ------------------------------------------------------------------ growth and the restrained case

    [Fact]
    public void Steel_50_m_70_K_Grows_42_mm_Whether_The_Temperatures_Or_The_Change_Are_Entered()
    {
        var byTemperature = Run();
        Assert.Equal(0.042, byTemperature.OutputSi("dL"), 12);
        Assert.Equal(70, byTemperature.OutputSi("dT"), 12);

        var byChange = Run(CalcInputValue.Choice("temp_mode", PipeExpansionCalculator.ChangeGiven), Q("dT_in", 70, "K"));
        Assert.Equal(0.042, byChange.OutputSi("dL"), 12);
        Assert.Equal(byTemperature.OutputSi("anchor_force"), byChange.OutputSi("anchor_force"), 9);
    }

    [Fact]
    public void A_Cold_Line_Contracts_By_The_Same_Magnitude_And_Loads_The_Anchors_The_Same()
    {
        var hot = Run();
        var cold = Run(Q("T_install", 80, "degC"), Q("T_operate", 10, "degC"));
        Assert.Equal(-70, cold.OutputSi("dT"), 12);
        Assert.Equal(hot.OutputSi("dL"), cold.OutputSi("dL"), 12);
        Assert.Equal(hot.OutputSi("anchor_force"), cold.OutputSi("anchor_force"), 9);
        Assert.Equal(hot.OutputSi("F_res"), cold.OutputSi("F_res"), 6);
    }

    [Fact]
    public void The_Fully_Restrained_Stress_And_Force_Are_E_Alpha_dT_And_That_Times_The_Metal_Area()
    {
        var r = Run();
        Assert.Equal(210e9 * 12e-6 * 70, r.OutputSi("sigma_res"), 3);
        var area = Math.PI / 4 * (0.0889 * 0.0889 - 0.0825 * 0.0825);
        Assert.Equal(area, r.OutputSi("A_m"), 12);
        Assert.Equal(210e9 * 12e-6 * 70 * area, r.OutputSi("F_res"), 3);
        Assert.Equal(176.4 / 100, r.OutputSi("u_res"), 9);
    }

    // ------------------------------------------------------------------ the guided cantilever

    [Fact]
    public void The_Required_Leg_Is_The_Length_At_Which_The_Root_Stress_Equals_Sa()
    {
        var first = Run();
        var required = first.OutputSi("L_req");
        Assert.Equal(Math.Sqrt(3 * 210e9 * 0.0889 * 0.042 / 100e6), required, 9);

        // Make the available leg exactly the required one: utilisation 1, the root stress is Sa, and the check passes at the limit.
        var at = Run(Q("H", required, "m"));
        Assert.Equal(1, at.OutputSi("u_leg"), 9);
        Assert.Equal(100e6, at.OutputSi("sigma_leg"), 0);
        Assert.Contains(at.Checks, c => c.Name.StartsWith("Available leg", StringComparison.Ordinal) && c.Passed);

        var shorter = Run(Q("H", required * 0.9, "m"));
        Assert.Contains(shorter.Checks, c => c.Name.StartsWith("Available leg", StringComparison.Ordinal) && !c.Passed);
        Assert.Equal(0, shorter.OutputSi("pass"));
    }

    [Fact]
    public void The_Force_And_Moment_Of_The_Leg_Are_12EID_Over_H_Cubed_And_6EID_Over_H_Squared_With_M_Equal_To_F_H_Over_2()
    {
        var r = Run();
        var i = Math.PI / 64 * (Math.Pow(0.0889, 4) - Math.Pow(0.0825, 4));
        Assert.Equal(12 * 210e9 * i * 0.042 / 216, r.OutputSi("anchor_force"), 9);
        Assert.Equal(6 * 210e9 * i * 0.042 / 36, r.OutputSi("anchor_moment"), 9);
        Assert.Equal(r.OutputSi("anchor_force") * 6 / 2, r.OutputSi("anchor_moment"), 9);
        Assert.Equal(r.OutputSi("u_leg") * r.OutputSi("u_leg"), r.OutputSi("u_sigma"), 12);
    }

    [Fact]
    public void A_U_Loop_Gives_Each_Leg_Half_The_Movement_So_It_Needs_The_Offset_Leg_Over_Root_Two_And_Pushes_Less_Hard()
    {
        var offset = Run();
        var loop = Run(Loop());
        Assert.Equal(0.021, loop.OutputSi("Delta"), 12);
        Assert.Equal(offset.OutputSi("L_req") / Math.Sqrt(2), loop.OutputSi("L_req"), 9);
        Assert.Equal(offset.OutputSi("anchor_force") / 2, loop.OutputSi("anchor_force"), 9);
        Assert.Equal(offset.OutputSi("anchor_moment") / 2, loop.OutputSi("anchor_moment"), 9);
        Assert.Contains(Run(Loop(), Q("H", 3, "m")).Checks, c => c.Name.StartsWith("Available leg", StringComparison.Ordinal) && !c.Passed);
    }

    // ------------------------------------------------------------------ screening

    [Fact]
    public void The_Screening_Ratio_Uses_The_Developed_Length_And_The_Anchor_Distance_Of_The_Layout()
    {
        var offset = Run();
        Assert.Equal(56, offset.OutputSi("L_dev"), 12);
        Assert.Equal(Math.Sqrt(50 * 50 + 6 * 6), offset.OutputSi("U_a"), 12);
        Assert.Equal(0.0889 * 0.042 / Math.Pow(56 - Math.Sqrt(2536), 2), offset.OutputSi("scr"), 12);
        Assert.Equal("needs formal stress analysis", Screen(offset));

        var loop = Run(Loop(), Q("H", 3, "m"), Q("K_scr", 120, "mm^2/m^2"));
        Assert.Equal(56, loop.OutputSi("L_dev"), 12);
        Assert.Equal(50, loop.OutputSi("U_a"), 12);
        Assert.Equal(0.0889 * 0.042 / 36, loop.OutputSi("scr"), 12);
        Assert.Equal("PASS", Screen(loop));
    }

    [Fact]
    public void K1_Is_Accepted_In_Any_Consistent_Unit()
    {
        // 100 mm²/m² is 1e-4; the same limit written in in²/ft² is 1e-4·144 = 0.0144.
        var mm = Run(Q("K_scr", 100, "mm^2/m^2"));
        var inch = Run(Q("K_scr", 0.0144, "in^2/ft^2"));
        Assert.Equal(mm.OutputSi("u_scr"), inch.OutputSi("u_scr"), 9);
    }

    [Fact]
    public void Without_A_K1_Record_The_Ratio_Is_Shown_And_Not_Judged_And_The_Sheet_Says_So()
    {
        var spec = Pipe.Template.Inputs.Single(i => i.Symbol == "K_scr");
        Assert.Null(spec.Default);
        Assert.True(spec.IsOptional);

        var r = CalcRunner.Run(Pipe, new CalcInputSet(CalcTemplateHasher.DefaultInputs(Pipe.Template).Values.Where(v => v.Symbol != "K_scr")));
        Assert.False(r.IsBlocked);
        Assert.Contains(r.Warnings, w => w.RuleId == "screening-k1");
        Assert.Null(r.FindOutput("u_scr"));
        Assert.StartsWith("not evaluated", Screen(r), StringComparison.Ordinal);
        Assert.DoesNotContain(r.Checks, c => c.Name.StartsWith("Screening", StringComparison.Ordinal));
        Assert.True(r.OutputSi("scr") > 0);
    }

    // ------------------------------------------------------------------ no allowable ships, blocks, warnings

    [Fact]
    public void Sa_And_K1_Have_No_Default_And_A_Sheet_Without_Sa_Blocks_Saying_What_To_Enter()
    {
        Assert.All(new[] { "Sa", "K_scr" }, symbol => Assert.Null(Pipe.Template.FindInput(symbol)!.Default));
        Assert.Equal(["pipe.allowable-range", "pipe.screening-k1"], Pipe.Template.RecordKinds!.Where(k => k.Symbols.Contains("Sa") || k.Symbols.Contains("K_scr")).Select(k => k.Kind).Order().ToArray());

        var blocked = CalcRunner.Run(Pipe, CalcTemplateHasher.NewSheetInputs(Pipe.Template));
        var block = Assert.Single(blocked.Blocks);
        Assert.Equal("Sa", block.RuleId);
        Assert.StartsWith("Sa: ", block.Message, StringComparison.Ordinal);
        Assert.Contains("enter", block.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_Wall_Of_Half_The_Diameter_Or_No_Temperature_Change_Blocks()
    {
        var wall = Run(Q("t_wall", 45, "mm"));
        Assert.Equal("wall", Assert.Single(wall.Blocks).RuleId);
        Assert.StartsWith("t_wall: ", wall.Blocks[0].Message, StringComparison.Ordinal);

        var same = Run(Q("T_operate", 10, "degC"));
        Assert.StartsWith("T_operate: ", Assert.Single(same.Blocks).Message, StringComparison.Ordinal);

        var none = CalcRunner.Run(Pipe, CalcTemplateHasher.DefaultInputs(Pipe.Template).With(CalcInputValue.Choice("temp_mode", PipeExpansionCalculator.ChangeGiven)));
        Assert.StartsWith("dT_in: ", Assert.Single(none.Blocks).Message, StringComparison.Ordinal);
        Assert.StartsWith("dT_in: ", Assert.Single(Run(CalcInputValue.Choice("temp_mode", PipeExpansionCalculator.ChangeGiven), Q("dT_in", 0, "K")).Blocks).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Leg_Under_Ten_Diameters_Warns_That_The_Beam_Model_Does_Not_Hold()
    {
        Assert.DoesNotContain(Run().Warnings, w => w.RuleId == "short-leg");
        var short_ = Run(Q("H", 0.8, "m"));
        Assert.Contains(short_.Warnings, w => w.RuleId == "short-leg");
    }

    [Fact]
    public void A_Bellows_Thrust_Is_Pressure_Times_Effective_Area_And_Needs_Both()
    {
        var r = Run(Q("P_b", 10, "bar"), Q("A_eff", 50, "cm^2"));
        Assert.Equal(5000, r.OutputSi("F_thrust"), 6);

        var half = Run(Q("P_b", 10, "bar"));
        Assert.Contains(half.Warnings, w => w.RuleId == "bellows-incomplete");
        Assert.Null(half.FindOutput("F_thrust"));
        Assert.Null(Run().FindOutput("F_thrust"));
    }

    // ------------------------------------------------------------------ material mapping and the label

    [Fact]
    public void E_And_Alpha_Take_Their_Values_From_A_Material_And_The_Sheet_Is_Labelled_Screening_Only()
    {
        var t = Pipe.Template;
        Assert.Equal("E", t.FindInput("E")!.MaterialProperty);
        Assert.Equal("alpha", t.FindInput("alpha")!.MaterialProperty);
        var material = Assert.Single(t.RecordKinds!, k => k.Kind == PipeExpansionCalculator.MaterialKind);
        Assert.Equal(["E", "alpha"], material.Symbols);
        Assert.Contains("Screening only — not a piping stress analysis", t.Scope, StringComparison.Ordinal);
        Assert.Contains(t.Assumptions, a => a.Statement.StartsWith("Screening only — not a piping stress analysis", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Ports_Are_Anchor_Force_Anchor_Moment_And_dL()
    {
        Assert.Equal(["anchor_force", "anchor_moment", "dL"], Pipe.Template.OutputPorts!.Select(p => p.Symbol).Order().ToArray());
        var run = Run();
        Assert.All(Pipe.Template.OutputPorts!, p => Assert.NotNull(run.FindOutput(p.Symbol)));
    }

    // ------------------------------------------------------------------ properties

    [Fact]
    public void Property_Growth_Is_Linear_The_Leg_Scales_With_The_Root_Of_The_Movement_And_Moment_Is_Half_Force_Times_Leg()
    {
        Gen.Select(Gen.Double[1, 300], Gen.Double[-40, 60], Gen.Double[5, 30], Gen.Double[0.5, 2.0], Gen.Bool).Sample((len, dt, alpha, scale, loop) =>
        {
            if (Math.Abs(dt) < 1)
                return true;

            var layout = CalcInputValue.Choice("layout", loop ? PipeExpansionCalculator.UpLoop : PipeExpansionCalculator.OffsetLeg);
            var a = Run(layout, CalcInputValue.Choice("temp_mode", PipeExpansionCalculator.ChangeGiven), Q("L", len, "m"), Q("dT_in", dt, "K"), Q("alpha", alpha, "um/(m*K)"));
            var b = Run(layout, CalcInputValue.Choice("temp_mode", PipeExpansionCalculator.ChangeGiven), Q("L", len * scale, "m"), Q("dT_in", dt, "K"), Q("alpha", alpha, "um/(m*K)"));

            if (a.IsBlocked || b.IsBlocked)
                return false;

            var linear = Math.Abs(b.OutputSi("dL") - scale * a.OutputSi("dL")) <= 1e-12 * Math.Abs(b.OutputSi("dL")) + 1e-18;
            var root = Math.Abs(b.OutputSi("L_req") - Math.Sqrt(scale) * a.OutputSi("L_req")) <= 1e-9 * b.OutputSi("L_req");
            var moment = Math.Abs(a.OutputSi("anchor_moment") - a.OutputSi("anchor_force") * 6.0 / 2) <= 1e-9 * a.OutputSi("anchor_moment");
            var stress = Math.Abs(a.OutputSi("u_sigma") - a.OutputSi("u_leg") * a.OutputSi("u_leg")) <= 1e-9 * a.OutputSi("u_sigma");
            return linear && root && moment && stress;
        }, iter: 200);
    }
}
