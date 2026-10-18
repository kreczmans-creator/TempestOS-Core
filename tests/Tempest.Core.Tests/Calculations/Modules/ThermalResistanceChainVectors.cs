using Tempest.Core.Calculations.Modules;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// Test vectors for <c>calc.thermal-resistance-chain</c> — the worked
/// examples and edge cases of
/// <c>docs/engineering/calculations/calc.thermal-resistance-chain.md</c>, as
/// data. Every expected figure was derived by hand; temperatures in °C,
/// rises and margins in K, resistances in K/W.
/// </summary>
public static class ThermalResistanceChainVectors
{
    /// <summary>One vector.</summary>
    public sealed record Vector(
        string Name,
        Quantity<Power> Power,
        Quantity<Temperature> Ambient,
        IReadOnlyList<ThermalResistanceStage> Stages,
        Quantity<Temperature> Maximum,
        bool? ExpectedMeetsCriteria = null,
        double? ExpectedTotalResistance = null,
        IReadOnlyList<double>? ExpectedRises = null,
        IReadOnlyList<double>? ExpectedNodesCelsius = null,
        double? ExpectedMargin = null,
        double? ExpectedPermittedResistance = null,
        double? ExpectedUtilisation = null,
        string? ExpectedReasonFragment = null)
    {
        /// <summary>Relative tolerance the expected figures are stated to (five significant figures: half a unit in the fifth).</summary>
        public double RelativeTolerance => 5e-5;

        /// <summary>The vector as the definition's input.</summary>
        public ThermalResistanceChainInput ToInput() => new(Power, Ambient, Stages, Maximum);

        /// <inheritdoc />
        public override string ToString() => Name;
    }

    private static Quantity<Power> W(double w) => new(w, PowerUnits.Watt);
    private static Quantity<Temperature> C(double c) => new(c, TemperatureUnits.DegreeCelsius);
    private static ThermalResistanceStage S(string name, double kPerW) => new(name, new Quantity<ThermalResistance>(kPerW, ThermalResistanceUnits.KelvinPerWatt));

    private static readonly ThermalResistanceStage[] ThreeStage =
    [
        S("Junction to case", 0.5),
        S("Case to heat sink", 0.2),
        S("Heat sink to ambient", 1.8),
    ];

    /// <summary>The worked examples.</summary>
    public static TheoryData<Vector> WorkedExamples =>
    [
        new("Example 1: three-stage chain, 25 W", W(25), C(40), ThreeStage, C(125),
            true, 2.5, [12.5, 5, 45], [102.5, 90, 85], 22.5, 3.4, 0.735294),

        new("Example 2: heat sink selection, 60 W transistor case (Cengel)", W(60), C(30), [S("Case to ambient (heat sink)", 0.9)], C(90),
            true, 0.9, [54], [84], 6, 1.0, 0.9),

        new("Example 3: 40 W exceeds the junction limit", W(40), C(40), ThreeStage, C(125),
            false, 2.5, [20, 8, 72], [140, 120, 112], -15, 2.125, 1.17647),

        new("Example 4: 34 W lands exactly on the limit", W(34), C(40), ThreeStage, C(125),
            true, 2.5, [17, 6.8, 61.2], [125, 108, 101.2], 0, 2.5, 1.0),

        new("Example 1 in kW, kelvin, degC/W and K/mW", new(0.025, PowerUnits.Kilowatt), new(313.15, TemperatureUnits.Kelvin),
            [
                new("Junction to case", new(0.5, ThermalResistanceUnits.DegreeCelsiusPerWatt)),
                new("Case to heat sink", new(0.0002, ThermalResistanceUnits.KelvinPerMilliwatt)),
                new("Heat sink to ambient", new(1.8, ThermalResistanceUnits.KelvinPerWatt)),
            ],
            new(257, TemperatureUnits.DegreeFahrenheit),
            true, 2.5, [12.5, 5, 45], [102.5, 90, 85], 22.5, 3.4, 0.735294),
    ];

    /// <summary>Inputs rejected as invalid.</summary>
    public static TheoryData<Vector> InvalidInputs =>
    [
        new("negative power", W(-1), C(40), ThreeStage, C(125), ExpectedReasonFragment: "Power"),
        new("no stages", W(25), C(40), [], C(125), ExpectedReasonFragment: "stage"),
        new("unnamed stage", W(25), C(40), [S("", 1)], C(125), ExpectedReasonFragment: "named"),
        new("negative resistance", W(25), C(40), [S("Sink", -0.5), S("Case", 1)], C(125), ExpectedReasonFragment: "zero or positive"),
        new("zero total resistance", W(25), C(40), [S("Sink", 0)], C(125), ExpectedReasonFragment: "total thermal resistance"),
        new("maximum not above ambient", W(25), C(40), ThreeStage, C(40), ExpectedReasonFragment: "above the ambient"),
    ];
}
