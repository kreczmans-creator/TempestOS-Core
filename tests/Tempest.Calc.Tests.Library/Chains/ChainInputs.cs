using Tempest.Calc.Electrical;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// The inputs of every step of the six built-in chains for the chain tests and the pack worked examples. The values are the pack templates'
/// own example inputs (<see cref="CalcPackStep.Example"/>); here the figures a step chooses cite that step's (invented) specification or
/// drawing, and an input left at the sheet's default is recorded as an assumption.
/// </summary>
internal static class ChainInputs
{
    /// <summary>The provenance of a template default the example does not override.</summary>
    public static CalcProvenance DefaultAssumption { get; } = CalcProvenance.Assumed("Sheet default value; no project figure given.");

    private static CalcProvenance Spec(string reference) => CalcProvenance.Specification(reference);

    private static CalcProvenance Drg(string number) => CalcProvenance.Drawing(number, "B");

    /// <summary>The template's example inputs per step key, each step's chosen figures citing <paramref name="basis"/> for that key.</summary>
    public static Dictionary<string, IReadOnlyList<CalcInputValue>> For(LibraryTestHost host, CalcPackTemplate template, IReadOnlyDictionary<string, CalcProvenance> basis) =>
        template.Steps.ToDictionary(s => s.Key, s => host.Service.ExampleInputs(template, s, basis[s.Key], DefaultAssumption)
            ?? throw new InvalidOperationException($"Step '{s.Key}' has no example inputs."), StringComparer.Ordinal);

    public static Dictionary<string, IReadOnlyList<CalcInputValue>> DriveTrain(LibraryTestHost host) => For(host, CalcPackTemplates.DriveTrain, new Dictionary<string, CalcProvenance>
    {
        ["e1"] = Spec("SPEC-CNV-07 §3 (conveyor duty)"), ["m4"] = Drg("DRG-CNV-410"), ["m5"] = Spec("SPEC-CNV-07 §6 (bearing schedule)"), ["m2"] = Drg("DRG-CNV-411"), ["m3"] = Drg("DRG-CNV-412"),
    });

    /// <summary>
    /// The motor circuit's inputs: the example inputs, and E4's two tables given as pins to the records it is passed (null: the tables as plain
    /// synthetic inputs, never table values from a regulation).
    /// </summary>
    public static Dictionary<string, IReadOnlyList<CalcInputValue>> MotorCircuit(LibraryTestHost host, CalcProvenance? ampacityPin = null, CalcProvenance? ratingsPin = null)
    {
        var cable = Spec("SPEC-LINE-12 §5 (cabling)");
        var inputs = For(host, CalcPackTemplates.MotorCircuit, new Dictionary<string, CalcProvenance>
        {
            ["e1"] = Spec("SPEC-LINE-12 §2 (axis duty cycle)"), ["e6"] = Spec("SPEC-LINE-12 §4 (panel schedule)"), ["e4"] = cable, ["e3"] = cable,
            ["e5"] = Spec("SPEC-LINE-12 §6 (earthing and protection)"), ["e2"] = Drg("DRG-LINE-120"),
        });
        inputs["e4"] =
        [
            .. inputs["e4"],
            ConductorSizingCalculator.SyntheticAmpacity() with { Provenance = ampacityPin ?? Spec("SYNTHETIC test capacities (not a regulation table)") },
            ConductorSizingCalculator.TypicalRatings() with { Provenance = ratingsPin ?? Spec("SYNTHETIC test device ratings (not a manufacturer table)") },
        ];
        return inputs;
    }

    /// <summary>
    /// The hydraulic power unit's inputs: the example inputs, and E4's two tables given as pins to the records it is passed (null: the tables as plain
    /// synthetic inputs, labelled SYNTHETIC, never table values from a regulation or a manufacturer). The shipped example omits both tables.
    /// </summary>
    public static Dictionary<string, IReadOnlyList<CalcInputValue>> HydraulicPowerUnit(LibraryTestHost host, CalcProvenance? ampacityPin = null, CalcProvenance? ratingsPin = null)
    {
        var duty = Spec("SPEC-HPU-05 §2 (press duty)");
        var cable = Spec("SPEC-HPU-05 §6 (power supply)");
        var inputs = For(host, CalcPackTemplates.HydraulicPowerUnit, new Dictionary<string, CalcProvenance>
        {
            ["f1"] = duty, ["f2"] = Spec("SPEC-HPU-05 §3 (accumulator)"), ["b1"] = Drg("DRG-HPU-510"), ["e1"] = Spec("SPEC-HPU-05 §5 (pump drive)"), ["e4"] = cable,
        });
        inputs["e4"] =
        [
            .. inputs["e4"],
            ConductorSizingCalculator.SyntheticAmpacity() with { Provenance = ampacityPin ?? Spec("SYNTHETIC test capacities (not a regulation table)") },
            ConductorSizingCalculator.TypicalRatings() with { Provenance = ratingsPin ?? Spec("SYNTHETIC test device ratings (not a manufacturer table)") },
        ];
        return inputs;
    }

    public static Dictionary<string, IReadOnlyList<CalcInputValue>> WetHeating(LibraryTestHost host)
    {
        var spec = Spec("SPEC-HTG-01 §3");
        return For(host, CalcPackTemplates.WetHeating, new Dictionary<string, CalcProvenance> { ["b4a"] = spec, ["b4b"] = spec, ["b6"] = spec, ["b1"] = spec, ["e1"] = spec });
    }

    public static Dictionary<string, IReadOnlyList<CalcInputValue>> Cooling(LibraryTestHost host) => For(host, CalcPackTemplates.Cooling, new Dictionary<string, CalcProvenance>
    {
        ["e6"] = Spec("SPEC-MCC-03 §4 (panel schedule)"), ["e2"] = Drg("DRG-MCC-210"), ["b5"] = Spec("SPEC-MCC-03 §7 (cooling water)"), ["b1"] = Drg("DRG-MCC-215"),
    });

    /// <summary>The characteristic vertical actions on the bracket, kN (downward negative, as S5 reads a signed effect); they match the S5 example inputs.</summary>
    public const double DeadLoad = 12;

    public const double ImposedLoad = 8;

    /// <summary>The ULS horizontal design action at the column top, kN (a design value: S2 combines it at factor 1); it matches the S2 example inputs.</summary>
    public const double HorizontalLoad = 5;

    public static Dictionary<string, IReadOnlyList<CalcInputValue>> SteelSupport(LibraryTestHost host) => For(host, CalcPackTemplates.SteelSupport, new Dictionary<string, CalcProvenance>
    {
        ["s5"] = Spec("SPEC-STR-09 §2 (actions)"), ["s2"] = Drg("DRG-STR-310"), ["s3"] = Spec("SPEC-STR-09 §3 (members)"), ["m3"] = Drg("DRG-STR-312"),
        ["s4a"] = Drg("DRG-STR-312"), ["s4b"] = Drg("DRG-STR-315"), ["s6"] = Drg("DRG-STR-315"),
    });

    public static Dictionary<string, IReadOnlyList<CalcInputValue>> EnvelopeMoisture(LibraryTestHost host) => For(host, CalcPackTemplates.EnvelopeMoisture, new Dictionary<string, CalcProvenance>
    {
        ["b4"] = Spec("SPEC-ENV-02 §3 (wall build-up)"), ["b2"] = Spec("SPEC-ENV-02 §2 (room conditions)"), ["b7"] = Drg("DRG-ENV-210"),
    });

    public static Dictionary<string, IReadOnlyList<CalcInputValue>> ColdPipe(LibraryTestHost host) => For(host, CalcPackTemplates.ColdPipe, new Dictionary<string, CalcProvenance>
    {
        ["b1"] = Spec("SPEC-CHW-04 §2 (chilled water duty)"), ["b2"] = Spec("SPEC-CHW-04 §3 (plant room conditions)"), ["b9"] = Drg("DRG-CHW-410"),
    });

    public static Dictionary<string, IReadOnlyList<CalcInputValue>> PipeworkSupports(LibraryTestHost host) => For(host, CalcPackTemplates.PipeworkSupports, new Dictionary<string, CalcProvenance>
    {
        ["b8"] = Spec("SPEC-PIP-11 §4 (thermal movement)"), ["s2"] = Drg("DRG-PIP-610"), ["s6"] = Drg("DRG-PIP-611"),
    });

    public static Dictionary<string, IReadOnlyList<CalcInputValue>> ElectronicsEnclosure(LibraryTestHost host) => For(host, CalcPackTemplates.ElectronicsEnclosure, new Dictionary<string, CalcProvenance>
    {
        ["e7"] = Spec("SPEC-ELX-02 §3 (supply rail)"), ["e6"] = Spec("SPEC-ELX-02 §4 (board loads)"), ["e2"] = Drg("DRG-ELX-210"), ["m8"] = Spec("SPEC-ELX-02 §6 (lid latch; Es and eps_perm for 55 C)"), ["s9"] = Spec("SPEC-ELX-02 §6 (lid; E for 55 C)"),
    });
}
