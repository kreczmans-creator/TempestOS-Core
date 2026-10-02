using Tempest.Core.Calculations.Modules;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// Test vectors for <c>calc.plane-wall-heat-transfer</c> — the worked
/// examples and edge cases of
/// <c>docs/engineering/calculations/calc.plane-wall-heat-transfer.md</c>, as
/// data. Every expected figure was derived by hand; resistances in K/W, heat
/// flow in W, flux in W/m², U in W/(m²·K), temperatures in °C.
/// </summary>
public static class PlaneWallHeatTransferVectors
{
    /// <summary>One vector.</summary>
    public sealed record Vector(
        string Name,
        Quantity<Temperature> Hot,
        Quantity<HeatTransferCoefficient>? HotFilm,
        IReadOnlyList<PlaneWallLayer> Layers,
        Quantity<HeatTransferCoefficient>? ColdFilm,
        Quantity<Temperature> Cold,
        Quantity<Area> Area,
        IReadOnlyList<double>? ExpectedResistances = null,
        double? ExpectedTotalResistance = null,
        double? ExpectedU = null,
        double? ExpectedHeatFlow = null,
        double? ExpectedFlux = null,
        IReadOnlyList<double>? ExpectedSurfacesCelsius = null,
        string? ExpectedReasonFragment = null)
    {
        /// <summary>Relative tolerance the expected figures are stated to (five significant figures: half a unit in the fifth).</summary>
        public double RelativeTolerance => 5e-5;

        /// <summary>The vector as the definition's input.</summary>
        public PlaneWallHeatTransferInput ToInput() => new(Hot, HotFilm, Layers, ColdFilm, Cold, Area);

        /// <inheritdoc />
        public override string ToString() => Name;
    }

    private static Quantity<Temperature> C(double c) => new(c, TemperatureUnits.DegreeCelsius);
    private static Quantity<HeatTransferCoefficient> H(double h) => new(h, HeatTransferCoefficientUnits.WattPerSquareMetreKelvin);
    private static Quantity<Area> M2(double a) => new(a, AreaUnits.SquareMetre);
    private static PlaneWallLayer Layer(string name, double mm, double k) =>
        new(name, new Quantity<Length>(mm, LengthUnits.Millimetre), new Quantity<ThermalConductivity>(k, ThermalConductivityUnits.WattPerMetreKelvin));

    /// <summary>The worked examples.</summary>
    public static TheoryData<Vector> WorkedExamples =>
    [
        // Cengel & Ghajar, Heat and Mass Transfer, ch. 3: the single-pane window.
        new("Example 1: single-pane window (Cengel)", C(20), H(10), [Layer("Glass", 8, 0.78)], H(40), C(-10), M2(1.2),
            [0.083333, 0.0085470, 0.020833], 0.112714, 7.39336, 266.161, 221.801, [-2.1801, -4.4550]),

        // The same chapter: the double-pane window with a 10 mm stagnant air gap.
        new("Example 2: double-pane window (Cengel)", C(20), H(10),
            [Layer("Outer glass", 4, 0.78), Layer("Air gap", 10, 0.026), Layer("Inner glass", 4, 0.78)], H(40), C(-10), M2(1.2),
            [0.083333, 0.0042735, 0.32051, 0.0042735, 0.020833], 0.433226, 1.92355, 69.2478, 57.7065, [14.229, 13.933, -8.2614, -8.5573]),

        // Surface temperatures given directly: pure conduction, 3 m² wall,
        // 200 mm brick (0.72) and 50 mm insulation (0.04), 25 °C inside, 5 °C outside.
        // R = 0.2/(0.72x3) + 0.05/(0.04x3) = 0.092593 + 0.41667 = 0.50926 K/W.
        new("Example 3: surface temperatures, no films", C(25), null,
            [Layer("Brick", 200, 0.72), Layer("Insulation", 50, 0.04)], null, C(5), M2(3),
            [0.092593, 0.41667], 0.50926, 0.65455, 39.273, 13.091, [25, 21.364, 5]),

        new("Example 1 in imperial units", new(68, TemperatureUnits.DegreeFahrenheit), new(10 / 5.678263341113487, HeatTransferCoefficientUnits.BtuPerHourSquareFootDegreeFahrenheit),
            [new("Glass", new(8 / 25.4, LengthUnits.Inch), new(0.78 / 1.7307346664744324, ThermalConductivityUnits.BtuPerHourFootDegreeFahrenheit))],
            new(40 / 5.678263341113487, HeatTransferCoefficientUnits.BtuPerHourSquareFootDegreeFahrenheit), new(14, TemperatureUnits.DegreeFahrenheit),
            new(1.2 / 0.09290304, AreaUnits.SquareFoot),
            [0.083333, 0.0085470, 0.020833], 0.112714, 7.39336, 266.161, 221.801, [-2.1801, -4.4550]),
    ];

    /// <summary>Inputs rejected as invalid.</summary>
    public static TheoryData<Vector> InvalidInputs =>
    [
        new("no layers", C(20), H(10), [], H(40), C(-10), M2(1.2), ExpectedReasonFragment: "layer"),
        new("unnamed layer", C(20), H(10), [Layer(" ", 8, 0.78)], H(40), C(-10), M2(1.2), ExpectedReasonFragment: "named"),
        new("zero thickness", C(20), H(10), [Layer("Glass", 0, 0.78)], H(40), C(-10), M2(1.2), ExpectedReasonFragment: "thickness"),
        new("negative conductivity", C(20), H(10), [Layer("Glass", 8, -0.78)], H(40), C(-10), M2(1.2), ExpectedReasonFragment: "conductivity"),
        new("zero hot film", C(20), H(0), [Layer("Glass", 8, 0.78)], H(40), C(-10), M2(1.2), ExpectedReasonFragment: "Hot-side film"),
        new("negative cold film", C(20), H(10), [Layer("Glass", 8, 0.78)], H(-40), C(-10), M2(1.2), ExpectedReasonFragment: "Cold-side film"),
        new("zero area", C(20), H(10), [Layer("Glass", 8, 0.78)], H(40), C(-10), M2(0), ExpectedReasonFragment: "Area"),
    ];
}
