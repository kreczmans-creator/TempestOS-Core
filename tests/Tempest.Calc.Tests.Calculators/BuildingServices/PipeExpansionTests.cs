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
        Assert.Equal(12 * 210e9 * i * 0.042 / 216, r.OutputSi("F_leg"), 9);
        Assert.Equal(6 * 210e9 * i * 0.042 / 36, r.OutputSi("anchor_moment"), 9);
        Assert.Equal(r.OutputSi("F_leg") * 6 / 2, r.OutputSi("anchor_moment"), 9);
        Assert.Equal(r.OutputSi("u_leg") * r.OutputSi("u_leg"), r.OutputSi("u_sigma"), 12);
    }

    [Fact]
    public void A_U_Loop_Gives_Each_Leg_Half_The_Movement_So_It_Needs_The_Offset_Leg_Over_Root_Two_And_Pushes_Less_Hard()
    {
        var offset = Run();
        var loop = Run(Loop());
        Assert.Equal(0.021, loop.OutputSi("Delta"), 12);
        Assert.Equal(offset.OutputSi("L_req") / Math.Sqrt(2), loop.OutputSi("L_req"), 9);
        Assert.Equal(offset.OutputSi("F_leg") / 2, loop.OutputSi("anchor_force"), 9);
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
        Assert.Equal(12e-6 * 70 * Math.Sqrt(2536), offset.OutputSi("Y_scr"), 12);
        Assert.Equal(0.0889 * 12e-6 * 70 * Math.Sqrt(2536) / Math.Pow(56 - Math.Sqrt(2536), 2), offset.OutputSi("scr"), 12);
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

        // M4: with the leg met and no K1 the sheet does not pass, whatever the leg says.
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Available leg", StringComparison.Ordinal) && c.Passed);
        Assert.Equal(0, r.OutputSi("pass"));
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

    // ------------------------------------------------------------------ review fixes (version 1.1.0)

    private static CalcInputValue Bellows() => CalcInputValue.Choice("layout", PipeExpansionCalculator.AxialBellows);

    [Fact]
    public void H1_A_Square_L_Bend_Has_A_Screening_Ratio_Root_Two_Times_The_One_From_The_Run_Growth_Alone()
    {
        // 10 m x 10 m: U = 10*sqrt(2), L_dev - U = (2 - sqrt(2))*10, and Y is the free growth between the anchors, alpha*dT*U = sqrt(2)*alpha*dT*L.
        var r = Run(Q("L", 10, "m"), Q("H", 10, "m"));
        var u = 10 * Math.Sqrt(2);
        var denominator = Math.Pow(20 - u, 2);
        Assert.Equal(12e-6 * 70 * u, r.OutputSi("Y_scr"), 12);
        Assert.Equal(0.0889 * 12e-6 * 70 * u / denominator, r.OutputSi("scr"), 12);
        Assert.Equal(30.776371797566e-6, r.OutputSi("scr"), 12);

        var oldForm = 0.0889 * r.OutputSi("dL") / denominator;
        Assert.Equal(Math.Sqrt(2), r.OutputSi("scr") / oldForm, 12);

        // The U-loop has U = L, so Y = dL and its ratio is unchanged.
        var loop = Run(Loop(), Q("H", 3, "m"));
        Assert.Equal(loop.OutputSi("dL"), loop.OutputSi("Y_scr"), 12);
    }

    [Fact]
    public void M1_The_Run_Of_An_Offset_Leg_Bends_For_The_Growth_Of_The_Leg_And_Is_Checked_And_Reported()
    {
        // A "leg" longer than the run: 20 m off a 4 m run. The leg's growth, 12e-6*70*20 = 16.8 mm, bends the 4 m run.
        var r = Run(Q("L", 4, "m"), Q("H", 20, "m"));
        Assert.Equal(0.0168, r.OutputSi("Delta_run"), 12);
        Assert.Equal(3.0674380189337165, r.OutputSi("L_req_run"), 9);
        Assert.Equal(0.7668595047334291, r.OutputSi("u_run"), 9);
        Assert.Equal(523.9468918797689, r.OutputSi("F_run"), 6);
        Assert.Equal(0.8383150270076302, r.OutputSi("F_along_run"), 9);
        Assert.Equal(523.9468918797689, r.OutputSi("F_along_leg"), 6);
        Assert.Equal(523.9475625313613, r.OutputSi("anchor_force"), 6);
        Assert.Equal(1047.8937837595379, r.OutputSi("anchor_moment"), 6);
        Assert.True(r.OutputSi("F_run") > r.OutputSi("F_leg"));
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Run against", StringComparison.Ordinal) && c.Passed);

        // A 2 m run is too short for it: the check fails, though the long leg is fine.
        var short_ = Run(Q("L", 2, "m"), Q("H", 20, "m"));
        Assert.Contains(short_.Checks, c => c.Name.StartsWith("Run against", StringComparison.Ordinal) && !c.Passed);
        Assert.Contains(short_.Checks, c => c.Name.StartsWith("Available leg", StringComparison.Ordinal) && c.Passed);
        Assert.Equal(0, short_.OutputSi("pass"));

        // The ordinary case (long run, short leg) is governed by the leg, and a U-loop has no run check.
        var ordinary = Run();
        Assert.Equal(388.1088171438129, ordinary.OutputSi("anchor_force"), 6);
        Assert.Equal(Math.Sqrt(Math.Pow(ordinary.OutputSi("F_leg"), 2) + Math.Pow(ordinary.OutputSi("F_run"), 2)), ordinary.OutputSi("anchor_force"), 9);
        Assert.Null(Run(Loop()).FindOutput("u_run"));
    }

    [Fact]
    public void M2_The_Axial_Bellows_Layout_Skips_The_Leg_And_The_Screening_And_Anchors_At_Thrust_Plus_Spring()
    {
        var r = Run(Bellows(), Q("P_b", 6, "bar"), Q("A_eff", 80, "cm^2"), Q("k_b", 120, "N/mm"), Q("dL_rated", 50, "mm"));
        Assert.False(r.IsBlocked);
        Assert.Equal(4800, r.OutputSi("F_thrust"), 6);
        Assert.Equal(5040, r.OutputSi("F_spring"), 6);
        Assert.Equal(9840, r.OutputSi("anchor_force"), 6);
        Assert.Equal(0, r.OutputSi("anchor_moment"), 12);
        Assert.Equal(0.84, r.OutputSi("u_bellows"), 12);
        Assert.Equal(1, r.OutputSi("pass"));
        Assert.Null(r.FindOutput("L_req"));
        Assert.Null(r.FindOutput("scr"));
        Assert.DoesNotContain(r.Checks, c => c.Name.StartsWith("Available leg", StringComparison.Ordinal) || c.Name.StartsWith("Screening", StringComparison.Ordinal));
        Assert.Equal("not applicable: the bellows absorbs the movement", Screen(r));

        // The port follows the bellows; no Sa is needed (the default sheet has one, the bellows ignores a missing one).
        var noSa = CalcRunner.Run(Pipe, new CalcInputSet(Sheet(Bellows(), Q("P_b", 6, "bar"), Q("A_eff", 80, "cm^2")).Values.Where(v => v.Symbol != "Sa")));
        Assert.False(noSa.IsBlocked);
        Assert.Equal(4800, noSa.OutputSi("anchor_force"), 6);
        Assert.Null(noSa.FindOutput("F_spring"));

        // Without a rating nothing is checked, so the sheet does not pass.
        Assert.Equal(0, noSa.OutputSi("pass"));
    }

    [Fact]
    public void M2_The_Axial_Bellows_Needs_Both_The_Pressure_And_The_Effective_Area_And_A_Movement_Over_Its_Rating_Fails()
    {
        var onlyArea = Run(Bellows(), Q("A_eff", 80, "cm^2"));
        Assert.StartsWith("P_b: ", Assert.Single(onlyArea.Blocks).Message, StringComparison.Ordinal);
        var onlyPressure = Run(Bellows(), Q("P_b", 6, "bar"));
        Assert.StartsWith("A_eff: ", Assert.Single(onlyPressure.Blocks).Message, StringComparison.Ordinal);
        Assert.Contains("enter", onlyPressure.Blocks[0].Message, StringComparison.OrdinalIgnoreCase);

        var over = Run(Bellows(), Q("P_b", 6, "bar"), Q("A_eff", 80, "cm^2"), Q("dL_rated", 30, "mm"));
        Assert.Equal(1.4, over.OutputSi("u_bellows"), 12);
        Assert.Equal(0, over.OutputSi("pass"));
        var k1 = Assert.Single(over.Warnings, w => w.RuleId == "k1-unused");
        Assert.StartsWith("K_scr: ", k1.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(over.Warnings, w => w.RuleId == "bellows-unused");
    }

    [Fact]
    public void M2_Bellows_Inputs_With_A_Leg_Or_Loop_Layout_Are_Not_Used_And_Say_So()
    {
        var r = Run(Q("P_b", 6, "bar"), Q("A_eff", 80, "cm^2"));
        Assert.StartsWith("P_b: ", Assert.Single(r.Warnings, w => w.RuleId == "bellows-unused").Message, StringComparison.Ordinal);
        Assert.StartsWith("A_eff: ", Assert.Single(Run(Q("A_eff", 80, "cm^2")).Warnings, w => w.RuleId == "bellows-unused").Message, StringComparison.Ordinal);
        Assert.Null(r.FindOutput("F_thrust"));
        Assert.Equal(r.OutputSi("F_leg"), r.OutputSi("F_along_run"), 9);
        Assert.DoesNotContain(Run().Warnings, w => w.RuleId == "bellows-unused");
    }

    [Fact]
    public void M3_E_Is_The_Reference_Modulus_For_The_Stress_Check_And_E_Hot_Only_Changes_The_Reactions()
    {
        var cold = Run();
        var hot = Run(Q("E_hot", 190, "GPa"));
        Assert.Equal(cold.OutputSi("L_req"), hot.OutputSi("L_req"), 12);
        Assert.Equal(cold.OutputSi("sigma_leg"), hot.OutputSi("sigma_leg"), 6);
        Assert.Equal(cold.OutputSi("sigma_res"), hot.OutputSi("sigma_res"), 6);
        Assert.Equal(cold.OutputSi("u_leg"), hot.OutputSi("u_leg"), 12);
        Assert.Equal(351.14606510460703, hot.OutputSi("F_leg"), 6);
        Assert.Equal(351.14607265392596, hot.OutputSi("anchor_force"), 6);
        Assert.Equal(1053.438195313821, hot.OutputSi("anchor_moment"), 6);
        Assert.Equal(137503.4389435462, hot.OutputSi("F_res"), 3);
        Assert.Equal(cold.OutputSi("anchor_force") * 190 / 210, hot.OutputSi("anchor_force"), 9);
        Assert.Equal(cold.OutputSi("F_res") * 190 / 210, hot.OutputSi("F_res"), 3);
        Assert.Null(Pipe.Template.FindInput("E_hot")!.MaterialProperty);
        Assert.True(Pipe.Template.FindInput("E_hot")!.IsOptional);
    }

    [Fact]
    public void M4_Without_A_K1_The_Sheet_Does_Not_Pass_Even_When_Every_Check_Made_Is_Met()
    {
        var r = CalcRunner.Run(Pipe, new CalcInputSet(Sheet(Q("H", 8, "m")).Values.Where(v => v.Symbol != "K_scr")));
        Assert.All(r.Checks, c => Assert.True(c.Passed));
        Assert.Equal(0, r.OutputSi("pass"));
        Assert.Contains("not evaluated", Screen(r), StringComparison.Ordinal);

        var withK1 = Run(Q("H", 8, "m"), Q("K_scr", 200, "mm^2/m^2"));
        Assert.Equal(1, withK1.OutputSi("pass"));
    }

    [Fact]
    public void L1_The_Section_Growth_And_Restrained_Force_Are_Reported_Even_When_Sa_Is_Missing()
    {
        var r = CalcRunner.Run(Pipe, new CalcInputSet(Sheet().Values.Where(v => v.Symbol != "Sa")));
        Assert.Equal("Sa", Assert.Single(r.Blocks).RuleId);
        Assert.Equal(0.042, r.OutputSi("dL"), 12);
        Assert.Equal(151977.48514813, r.OutputSi("F_res"), 3);
        Assert.NotNull(r.FindOutput("A_m"));
        Assert.Null(r.FindOutput("L_req"));
    }

    [Fact]
    public void The_Leg_Has_No_Default_A_Leg_Layout_Without_It_Blocks_And_An_Axial_Bellows_Does_Not_Need_It()
    {
        Assert.Null(Pipe.Template.FindInput("H")!.Default);
        var noLeg = CalcRunner.Run(Pipe, new CalcInputSet(Sheet().Values.Where(v => v.Symbol != "H")));
        var block = Assert.Single(noLeg.Blocks);
        Assert.Equal("H", block.RuleId);
        Assert.StartsWith("H: ", block.Message, StringComparison.Ordinal);
        Assert.Contains("enter", block.Message, StringComparison.OrdinalIgnoreCase);

        var bellows = CalcRunner.Run(Pipe, new CalcInputSet(Sheet(Bellows(), Q("P_b", 6, "bar"), Q("A_eff", 80, "cm^2")).Values.Where(v => v.Symbol != "H")));
        Assert.False(bellows.IsBlocked);
        Assert.DoesNotContain(bellows.Warnings, w => w.Message.Contains("not entered", StringComparison.Ordinal));
    }

    [Fact]
    public void N1_The_Anchor_Force_Is_The_Resultant_Of_The_Force_Along_The_Run_And_The_Force_Along_The_Leg()
    {
        // A square 10 m x 10 m L-bend: the leg and the run are equal cantilevers, the two components are equal and perpendicular.
        var r = Run(Q("L", 10, "m"), Q("H", 10, "m"));
        Assert.Equal(r.OutputSi("F_leg"), r.OutputSi("F_run"), 9);
        Assert.Equal(r.OutputSi("F_along_run"), r.OutputSi("F_along_leg"), 9);
        Assert.Equal(Math.Sqrt(2) * r.OutputSi("F_leg"), r.OutputSi("anchor_force"), 9);
        Assert.Equal(23.71112961470716, r.OutputSi("anchor_force"), 9);
        Assert.Equal(r.OutputSi("M_leg"), r.OutputSi("anchor_moment"), 9);

        // A U-loop has only the component along the run.
        var loop = Run(Loop(), Q("H", 3, "m"));
        Assert.Equal(loop.OutputSi("F_leg"), loop.OutputSi("anchor_force"), 9);
        Assert.Equal(0, loop.OutputSi("F_along_leg"), 12);
    }

    [Fact]
    public void N2_A_Z_Bend_Has_Two_Runs_In_Series_Each_Checked_As_Its_Own_Cantilever()
    {
        var z = Run(Q("L", 30, "m"), Q("L2", 20, "m"));
        Assert.False(z.IsBlocked);
        Assert.Equal(50, z.OutputSi("L_tot"), 12);
        Assert.Equal(0.042, z.OutputSi("dL"), 12);
        Assert.Equal(35000, z.OutputSi("S_run"), 9);
        Assert.Equal(0.28742229497404465, z.OutputSi("F_run"), 9);
        Assert.Equal(241950.24000000008, z.OutputSi("sigma_run"), 3);
        Assert.Equal(161300.16000000003, z.OutputSi("sigma_run2"), 3);
        Assert.Equal(0.049188437665776706, z.OutputSi("u_run"), 12);
        Assert.Equal(0.04016219117528326, z.OutputSi("u_run2"), 12);
        Assert.Equal(4.31133442461067, z.OutputSi("M_run"), 9);
        Assert.Equal(2.8742229497404463, z.OutputSi("M_run2"), 9);
        Assert.Equal(388.108915228184, z.OutputSi("anchor_force"), 6);
        Assert.Equal(1164.3264263994865, z.OutputSi("anchor_moment"), 6);

        // Each run is its own check; the stiffer (shorter) run carries the lower stress, and the stresses scale with the run lengths.
        Assert.Contains(z.Checks, c => c.Name.StartsWith("First run", StringComparison.Ordinal) && c.Passed);
        Assert.Contains(z.Checks, c => c.Name.StartsWith("Second run", StringComparison.Ordinal) && c.Passed);
        Assert.Equal(30.0 / 20.0, z.OutputSi("sigma_run") / z.OutputSi("sigma_run2"), 12);

        // A Z-bend whose total is the same L-bend's spacing has the same leg, growth and screening.
        var l = Run();
        Assert.Equal(l.OutputSi("L_req"), z.OutputSi("L_req"), 12);
        Assert.Equal(l.OutputSi("scr"), z.OutputSi("scr"), 12);
        Assert.Equal(l.OutputSi("F_leg"), z.OutputSi("F_leg"), 12);

        // A stiff short second run takes almost nothing; a run that is too short for the leg's growth fails its own check.
        var shortSecond = Run(Q("L", 30, "m"), Q("L2", 3, "m"), Q("H", 20, "m"));
        Assert.Equal(0.1021968483085257, shortSecond.OutputSi("u_run"), 9);
        Assert.Equal(0.032317481034566733, shortSecond.OutputSi("u_run2"), 9);
        var tooShort = Run(Q("L", 3, "m"), Q("L2", 2, "m"), Q("H", 40, "m"));
        Assert.Contains(tooShort.Checks, c => c.Name.StartsWith("First run", StringComparison.Ordinal) && !c.Passed);
        Assert.Equal(0, tooShort.OutputSi("pass"));
    }

    [Fact]
    public void Z3_Property_However_The_Length_Is_Split_Into_Two_Runs_The_Leg_And_The_Screening_Are_The_L_Bends_And_The_Run_Stresses_Go_As_The_Lengths()
    {
        Gen.Select(Gen.Double[20, 200], Gen.Double[0.15, 0.85], Gen.Double[3, 20], Gen.Double[-40, 60]).Sample((total, share, h, dt) =>
        {
            if (Math.Abs(dt) < 1)
                return true;

            var l1 = total * share;
            var l2 = total - l1;
            var mode = CalcInputValue.Choice("temp_mode", PipeExpansionCalculator.ChangeGiven);
            var z = Run(mode, Q("dT_in", dt, "K"), Q("L", l1, "m"), Q("L2", l2, "m"), Q("H", h, "m"));
            var l = Run(mode, Q("dT_in", dt, "K"), Q("L", total, "m"), Q("H", h, "m"));

            if (z.IsBlocked || l.IsBlocked)
                return false;

            static bool Close(double a, double b) => Math.Abs(a - b) <= 1e-9 * Math.Max(Math.Abs(a), Math.Abs(b)) + 1e-18;

            return Close(z.OutputSi("L_req"), l.OutputSi("L_req"))
                && Close(z.OutputSi("scr"), l.OutputSi("scr"))
                && Close(z.OutputSi("dL"), l.OutputSi("dL"))
                && Close(z.OutputSi("F_leg"), l.OutputSi("F_leg"))
                && Close(z.OutputSi("sigma_run") / z.OutputSi("sigma_run2"), l1 / l2)
                && Close(z.OutputSi("F_run"), 12 * 210e9 * (Math.PI / 64 * (Math.Pow(0.0889, 4) - Math.Pow(0.0825, 4))) * (12e-6 * Math.Abs(dt) * h) / (Math.Pow(l1, 3) + Math.Pow(l2, 3)));
        }, iter: 200);
    }

    [Fact]
    public void N2_L2_Is_Only_For_The_Offset_Leg_And_The_Anchor_Moment_Is_Stated_As_A_Simplification()
    {
        var loop = Run(Loop(), Q("H", 3, "m"), Q("L2", 20, "m"));
        Assert.StartsWith("L2: ", Assert.Single(loop.Warnings, w => w.RuleId == "l2-unused").Message, StringComparison.Ordinal);
        Assert.Equal(Run(Loop(), Q("H", 3, "m")).OutputSi("dL"), loop.OutputSi("dL"), 12);
        Assert.Null(loop.FindOutput("sigma_run2"));
        Assert.DoesNotContain(Run().Warnings, w => w.RuleId == "l2-unused");
        Assert.Contains(Pipe.Template.Assumptions, a => a.Statement.StartsWith("anchor_moment is", StringComparison.Ordinal) && a.Statement.Contains("not the moment on a frame anchor", StringComparison.Ordinal));
        Assert.Contains("not a frame anchor moment", Pipe.Template.OutputPorts!.Single(p => p.Symbol == "anchor_moment").Description, StringComparison.Ordinal);
    }

    [Fact]
    public void N4_Short_Leg_Warnings_Are_Prefixed_With_The_Input_They_Are_About()
    {
        var shortLeg = Assert.Single(Run(Q("H", 0.8, "m")).Warnings, w => w.RuleId == "short-leg");
        Assert.StartsWith("H: ", shortLeg.Message, StringComparison.Ordinal);

        var shortRun = Run(Q("L", 0.8, "m"));
        Assert.StartsWith("L: ", Assert.Single(shortRun.Warnings, w => w.RuleId == "short-leg").Message, StringComparison.Ordinal);

        var shortSecond = Run(Q("L", 30, "m"), Q("L2", 0.8, "m"));
        Assert.StartsWith("L2: ", Assert.Single(shortSecond.Warnings, w => w.RuleId == "short-leg").Message, StringComparison.Ordinal);
    }

    [Fact]
    public void N5_The_Bellows_Pressure_Is_The_Governing_Pressure_And_The_Ratings_Note_Is_On_The_Sheet()
    {
        var spec = Pipe.Template.FindInput("P_b")!;
        Assert.Contains("governing", spec.Description, StringComparison.Ordinal);
        Assert.Contains("compression and extension ratings can differ", spec.Description, StringComparison.Ordinal);
        Assert.Contains(Pipe.Template.Assumptions, a => a.Statement.Contains("compression and extension ratings can differ", StringComparison.Ordinal));
    }

    [Fact]
    public void L2_The_Signed_Movement_And_Its_Direction_Are_Reported()
    {
        var hot = Run();
        Assert.Equal(0.042, hot.OutputSi("dL_signed"), 12);
        Assert.StartsWith("growth", ((TextValue)hot.FindOutput("direction")!.Value).Value, StringComparison.Ordinal);

        var cold = Run(Q("T_install", 80, "degC"), Q("T_operate", 10, "degC"));
        Assert.Equal(-0.042, cold.OutputSi("dL_signed"), 12);
        Assert.Equal(0.042, cold.OutputSi("dL"), 12);
        Assert.StartsWith("contraction", ((TextValue)cold.FindOutput("direction")!.Value).Value, StringComparison.Ordinal);
        Assert.Contains("tension", ((TextValue)cold.FindOutput("direction")!.Value).Value, StringComparison.Ordinal);
    }

    [Fact]
    public void L3_A_Temperature_Change_Below_Minus_300_K_Is_Refused()
    {
        var r = Run(CalcInputValue.Choice("temp_mode", PipeExpansionCalculator.ChangeGiven), Q("dT_in", -301, "K"));
        Assert.True(r.IsBlocked);
        Assert.Contains(r.Blocks, b => b.Message.StartsWith("dT_in", StringComparison.Ordinal));
        Assert.False(Run(CalcInputValue.Choice("temp_mode", PipeExpansionCalculator.ChangeGiven), Q("dT_in", -300, "K")).IsBlocked);
    }

    [Fact]
    public void L4_A_Short_Required_Leg_Alone_Does_Not_Warn_But_A_Short_Available_Leg_Does()
    {
        // A tiny movement needs a leg of well under ten diameters, yet the 6 m available leg is long: no warning.
        var tiny = Run(Q("L", 1, "m"), Q("T_operate", 11, "degC"));
        Assert.True(tiny.OutputSi("L_req") < 10 * 0.0889);
        Assert.DoesNotContain(tiny.Warnings, w => w.RuleId == "short-leg");
        Assert.Contains(Run(Q("H", 0.8, "m")).Warnings, w => w.RuleId == "short-leg");
    }

    [Fact]
    public void L7_The_Elbow_Stress_Intensification_Limit_Is_Its_Own_Assumption()
    {
        Assert.Contains(Pipe.Template.Assumptions, a => a.Statement.Contains("stress-intensification factor", StringComparison.Ordinal) && a.Statement.Contains("elbow", StringComparison.Ordinal));
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
    public void Property_Growth_Is_Linear_And_The_Leg_Obeys_The_Cantilever_Scaling_In_E_Od_Sa_And_H()
    {
        Gen.Select(Gen.Select(Gen.Double[5, 300], Gen.Double[-40, 60], Gen.Double[5, 30], Gen.Double[0.5, 2.0], Gen.Bool), Gen.Select(Gen.Double[50, 300], Gen.Double[30, 300], Gen.Double[3, 30], Gen.Double[20, 300])).Sample((a, b) =>
        {
            var (len, dt, alpha, scale, loop) = a;
            var (e, od, h, sa) = b;

            if (Math.Abs(dt) < 1)
                return true;

            CalcRunResult At(double l, double height) => Run(
                CalcInputValue.Choice("layout", loop ? PipeExpansionCalculator.UpLoop : PipeExpansionCalculator.OffsetLeg), CalcInputValue.Choice("temp_mode", PipeExpansionCalculator.ChangeGiven),
                Q("L", l, "m"), Q("dT_in", dt, "K"), Q("alpha", alpha, "um/(m*K)"), Q("E", e, "GPa"), Q("OD", od, "mm"), Q("H", height, "m"), Q("Sa", sa, "MPa"));

            var one = At(len, h);
            var longer = At(len * scale, h);
            var higher = At(len, 2 * h);

            if (one.IsBlocked || longer.IsBlocked || higher.IsBlocked)
                return false;

            var delta = alpha * 1e-6 * len * Math.Abs(dt) * (loop ? 0.5 : 1);
            var expected = Math.Sqrt(3 * e * 1e9 * od / 1000 * delta / (sa * 1e6));
            var linear = Math.Abs(longer.OutputSi("dL") - scale * one.OutputSi("dL")) <= 1e-12 * Math.Abs(longer.OutputSi("dL")) + 1e-18;
            var cantilever = Math.Abs(one.OutputSi("L_req") - expected) <= 1e-9 * expected;
            var root = Math.Abs(longer.OutputSi("L_req") - Math.Sqrt(scale) * one.OutputSi("L_req")) <= 1e-9 * longer.OutputSi("L_req");
            var cubic = Math.Abs(one.OutputSi("F_leg") - 8 * higher.OutputSi("F_leg")) <= 1e-9 * one.OutputSi("F_leg");
            var moment = Math.Abs(one.OutputSi("M_leg") - one.OutputSi("F_leg") * h / 2) <= 1e-9 * one.OutputSi("M_leg");
            var stress = Math.Abs(one.OutputSi("u_sigma") - one.OutputSi("u_leg") * one.OutputSi("u_leg")) <= 1e-9 * one.OutputSi("u_sigma");
            return linear && cantilever && root && cubic && moment && stress;
        }, iter: 200);
    }
}
