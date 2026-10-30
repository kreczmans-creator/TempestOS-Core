using Tempest.Calc.BuildingServices;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.BuildingServices;

/// <summary>Worked examples for the building-services templates ported from the legacy calculation modules (ADR-0167).</summary>
public static class BuildingServicesPortWorkedExamples
{
    private static CalcInputSet Sourced(string drawing, string[] criteria, params CalcInputValue[] values) =>
        new([.. values.Select(v => v with
        {
            Provenance = criteria.Contains(v.Symbol) ? CalcProvenance.Specification("SPEC-BS-04 design conditions") : CalcProvenance.Drawing(drawing, "A"),
        })]);

    private static CalcReferenceSource Legacy(int n) =>
        new("TempestOS calc.plane-wall-heat-transfer (v0.21.0) hand calculation, after Çengel & Ghajar", null, $"docs/engineering/calculations/calc.plane-wall-heat-transfer.md, Example {n}");

    private static readonly string[] Conditions = ["T_1", "h_1", "h_2", "T_2"];

    private static CalcInputValue[] Window(CalcInputValue layers) =>
    [
        CalcInputValue.Quantity("T_1", 20, "degC"), CalcInputValue.Quantity("h_1", 10, "W/(m^2*K)"), layers,
        CalcInputValue.Quantity("h_2", 40, "W/(m^2*K)"), CalcInputValue.Quantity("T_2", -10, "degC"), CalcInputValue.Quantity("A", 1.2, "m^2"),
    ];

    public static IReadOnlyList<WorkedExample> All { get; } =
    [
        new(PlaneWallCalculator.Id, "single-pane-window", "Single-pane window, 20 °C inside, −10 °C outside", Legacy(1),
            Sourced("DRG-GL-801", Conditions, Window(PlaneWallCalculator.Layers(("glass", 8, 0.78)))),
            [new CalcExpectedValue("R_tot", 0.11271, "K/W", 5e-5), new CalcExpectedValue("Phi", 0.26616, "kW", 5e-5), new CalcExpectedValue("U_wall", 7.3934, "W/(m^2*K)", 5e-5), new CalcExpectedValue("T_s0", -2.1801, "degC", 5e-5)],
            "An 8 mm pane, 0.8 × 1.5 m. The glass itself is under 8 % of the resistance; the two air films carry the rest. The inner surface sits at −2.2 °C, so the room side of the glass will frost."),
        new(PlaneWallCalculator.Id, "double-pane-window", "Double-pane window with a 10 mm air gap", Legacy(2),
            Sourced("DRG-GL-802", Conditions, Window(PlaneWallCalculator.Layers(("glass", 4, 0.78), ("air gap", 10, 0.026), ("glass", 4, 0.78)))),
            [new CalcExpectedValue("R_2", 0.32051, "K/W", 5e-5), new CalcExpectedValue("Phi", 0.069248, "kW", 5e-5), new CalcExpectedValue("U_wall", 1.9236, "W/(m^2*K)", 5e-5), new CalcExpectedValue("T_s0", 14.229, "degC", 5e-5)],
            "The same window with two 4 mm panes and still air between. The 10 mm gap is three-quarters of the wall's resistance; the heat loss falls to a quarter and the inner surface warms to 14.2 °C."),
    ];
}
