using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>Worked examples for the electrical templates ported from the legacy calculation modules (ADR-0167, D-40).</summary>
public static class ElectricalPortWorkedExamples
{
    private static CalcInputSet Sourced(string datasheet, string[] criteria, params CalcInputValue[] values) =>
        new([.. values.Select(v => v with
        {
            Provenance = criteria.Contains(v.Symbol) ? CalcProvenance.Specification("SPEC-EL-05 thermal limits") : CalcProvenance.Specification(datasheet, "A"),
        })]);

    private static CalcReferenceSource Legacy(int n) =>
        new("TempestOS calc.thermal-resistance-chain (v0.21.0) hand calculation", null, $"docs/engineering/calculations/calc.thermal-resistance-chain.md, Example {n}");

    private static readonly string[] Limits = ["T_a", "T_max"];

    private static CalcInputValue[] ThreeStage(double power) =>
    [
        CalcInputValue.Quantity("P", power, "W"), CalcInputValue.Quantity("T_a", 40, "degC"),
        ThermalChainCalculator.Stages(("junction to case", 0.5), ("case to sink", 0.2), ("sink to ambient", 1.8)), CalcInputValue.Quantity("T_max", 125, "degC"),
    ];

    public static IReadOnlyList<WorkedExample> All { get; } =
    [
        new(ThermalChainCalculator.Id, "three-stage-25W", "Three-stage chain, 25 W, 40 °C ambient", Legacy(1),
            Sourced("DS-PWR-901 device and heat sink datasheets", Limits, ThreeStage(25)),
            [new CalcExpectedValue("T_1", 102.5, "degC", 5e-5), new CalcExpectedValue("dT_m", 22.5, "K", 5e-5), new CalcExpectedValue("u", 0.73529, "", 5e-5), new CalcExpectedValue("R_max", 3.4, "K/W", 5e-5)],
            "Junction to case, interface pad and heat sink in series, 2.5 K/W in all. The junction runs at 102.5 °C against 125 °C; the sink-to-ambient stage takes 45 of the 62.5 K rise, so a better sink is where any further margin would come from."),
        new(ThermalChainCalculator.Id, "three-stage-40W-fails", "The same chain at 40 W — over the limit", Legacy(3),
            Sourced("DS-PWR-902 device and heat sink datasheets", Limits, ThreeStage(40)),
            [new CalcExpectedValue("T_1", 140, "degC", 5e-5), new CalcExpectedValue("dT_m", -15, "K", 5e-5), new CalcExpectedValue("u", 1.1765, "", 5e-5), new CalcExpectedValue("R_max", 2.125, "K/W", 5e-5)],
            "At 40 W the junction reaches 140 °C, 15 K over. The budget is 2.125 K/W; with 0.7 K/W already in the device and pad, the sink must be 1.4 K/W or better."),
    ];
}
