using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>One conducting layer of a plane wall, typed as one row: "Glass, 8 mm, 0.78 W/(m.K)".</summary>
/// <param name="Name">What the layer is.</param>
/// <param name="Thickness">The layer's thickness in the direction of heat flow.</param>
/// <param name="Conductivity">The layer material's thermal conductivity.</param>
public sealed record PlaneWallLayer(string Name, Quantity<Length> Thickness, Quantity<ThermalConductivity> Conductivity);

/// <summary>The inputs to one steady one-dimensional heat transfer through a plane wall.</summary>
/// <param name="HotSideTemperature">The hot side's fluid temperature — or, with no film coefficient, its surface temperature.</param>
/// <param name="HotSideFilmCoefficient">The hot side's convection film coefficient h₁, or <see langword="null"/> where the temperature given is the wall surface's own.</param>
/// <param name="Layers">The conducting layers in series, hot side first.</param>
/// <param name="ColdSideFilmCoefficient">The cold side's convection film coefficient h₂, or <see langword="null"/> where the temperature given is the wall surface's own.</param>
/// <param name="ColdSideTemperature">The cold side's fluid temperature — or, with no film coefficient, its surface temperature.</param>
/// <param name="Area">The wall area normal to the heat flow.</param>
public sealed record PlaneWallHeatTransferInput(
    Quantity<Temperature> HotSideTemperature,
    Quantity<HeatTransferCoefficient>? HotSideFilmCoefficient,
    IReadOnlyList<PlaneWallLayer> Layers,
    Quantity<HeatTransferCoefficient>? ColdSideFilmCoefficient,
    Quantity<Temperature> ColdSideTemperature,
    Quantity<Area> Area);

/// <summary>The result of one plane-wall heat transfer calculation. There is no criterion, so the run is simply computed.</summary>
/// <param name="ResistanceNames">Each resistance in the thermal circuit, hot side first: the films and the layers.</param>
/// <param name="Resistances">Each resistance's value, in the same order.</param>
/// <param name="TotalThermalResistance">The sum of the circuit.</param>
/// <param name="OverallHeatTransferCoefficient">U = 1 / (R_total A).</param>
/// <param name="HeatFlow">The heat flow from the hot side to the cold side; negative when the "hot" side is the colder.</param>
/// <param name="HeatFlux">The heat flow per unit area.</param>
/// <param name="TemperatureDrops">The temperature drop across each resistance, in the same order.</param>
/// <param name="SurfaceTemperatures">The temperature at each surface and interface, hot side first: the hot surface, between each pair of layers, the cold surface.</param>
public sealed record PlaneWallHeatTransferResult(
    IReadOnlyList<string> ResistanceNames,
    IReadOnlyList<Quantity<ThermalResistance>> Resistances,
    Quantity<ThermalResistance> TotalThermalResistance,
    Quantity<HeatTransferCoefficient> OverallHeatTransferCoefficient,
    Quantity<Power> HeatFlow,
    Quantity<HeatFlux> HeatFlux,
    IReadOnlyList<Quantity<TemperatureDelta>> TemperatureDrops,
    IReadOnlyList<Quantity<Temperature>> SurfaceTemperatures);

/// <summary>
/// Steady one-dimensional heat transfer through a composite plane wall by
/// the thermal-circuit method: convection films and conducting layers as
/// resistances in series. Specified in
/// <c>docs/engineering/calculations/calc.plane-wall-heat-transfer.md</c>.
/// </summary>
/// <remarks>
/// The conduction half of the thermal work the v0.16.0 suite deferred: its
/// heat-sink calculator took every resistance as stated, and named
/// <c>R = t / (k A)</c> from a layer's thickness and conductivity as the
/// next equation to add. This is that equation, with the convection film
/// <c>R = 1 / (h A)</c> beside it.
/// </remarks>
public sealed class PlaneWallHeatTransferCalculationDefinition : ICalculationDefinition<PlaneWallHeatTransferInput, PlaneWallHeatTransferResult>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "calc.plane-wall-heat-transfer";

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Plane Wall Heat Transfer (Conduction Layers and Convection Films)",
        Description:
            "Steady heat flow through a composite plane wall between two fluids: each layer's conduction resistance t/(kA), each "
            + "film's convection resistance 1/(hA), the overall U-value, the heat flow and flux, and every surface and interface temperature.",
        Category: "Thermal",
        Assumptions:
        [
            new CalculationAssumption("Steady, one-dimensional conduction through plane layers of constant conductivity, with no internal heat generation.", "The thermal-circuit idealisation: the layers' areas are equal and their edges lose nothing."),
            new CalculationAssumption("Layers are in perfect thermal contact, and each film coefficient is constant over the surface.", "A contact resistance, where it matters, can be entered as a thin layer of equivalent thickness and conductivity."),
        ],
        Constraints:
        [
            new CalculationConstraint("At least one layer, each named, each thickness and conductivity positive; a film coefficient, where given, positive; the area positive."),
        ]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException">Any constraint above is not met.</exception>
    public PlaneWallHeatTransferResult Calculate(PlaneWallHeatTransferInput input, CalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        var layers = input.Layers ?? [];
        var areaM2 = input.Area.BaseValue;
        var hotK = input.HotSideTemperature.BaseValue;
        var coldK = input.ColdSideTemperature.BaseValue;
        var h1 = input.HotSideFilmCoefficient?.BaseValue;
        var h2 = input.ColdSideFilmCoefficient?.BaseValue;

        ModuleGuards.Require(context, "At least one layer is required.", layers.Count >= 1, $"{layers.Count} layer(s)");
        foreach (var layer in layers)
        {
            ModuleGuards.Require(context, "Every layer must be named.", !string.IsNullOrWhiteSpace(layer.Name), "an unnamed layer");
            ModuleGuards.Require(context, "Every layer thickness must be positive.", layer.Thickness.BaseValue > 0, $"{layer.Name}: {EngineeringNumber.Format(layer.Thickness.BaseValue * 1000.0)} mm");
            ModuleGuards.Require(context, "Every layer conductivity must be positive.", layer.Conductivity.BaseValue > 0, $"{layer.Name}: {EngineeringNumber.Format(layer.Conductivity.BaseValue)} W/(m.K)");
        }

        ModuleGuards.Require(context, "Hot-side film coefficient must be positive when given.", h1 is null || h1 > 0, input, nameof(input.HotSideFilmCoefficient));
        ModuleGuards.Require(context, "Cold-side film coefficient must be positive when given.", h2 is null || h2 > 0, input, nameof(input.ColdSideFilmCoefficient));
        ModuleGuards.Require(context, "Area must be positive.", areaM2 > 0, input, nameof(input.Area));

        var names = new List<string>();
        var values = new List<double>();

        if (h1 is { } hot)
        {
            names.Add("Hot-side convection film");
            values.Add(1.0 / (hot * areaM2));
        }

        foreach (var layer in layers)
        {
            names.Add(layer.Name);
            values.Add(layer.Thickness.BaseValue / (layer.Conductivity.BaseValue * areaM2));
        }

        if (h2 is { } cold)
        {
            names.Add("Cold-side convection film");
            values.Add(1.0 / (cold * areaM2));
        }

        var totalKpW = values.Sum();
        var heatW = (hotK - coldK) / totalKpW;
        var resistances = values.Select(r => new Quantity<ThermalResistance>(r, ThermalResistanceUnits.KelvinPerWatt)).ToList();
        var drops = values.Select(r => new Quantity<TemperatureDelta>(heatW * r, TemperatureDeltaUnits.Kelvin)).ToList();

        // Surface and interface temperatures: walk the circuit from the hot
        // fluid. A film's cold side is a surface; a layer's is an interface
        // (or the cold surface after the last layer). The cold fluid itself
        // is not a surface and is not listed.
        var surfaces = new List<Quantity<Temperature>>();
        var nodeK = hotK;
        if (h1 is null)
            surfaces.Add(Celsius(nodeK));

        for (var i = 0; i < values.Count; i++)
        {
            nodeK -= heatW * values[i];
            var isColdFilm = h2 is not null && i == values.Count - 1;
            if (!isColdFilm)
                surfaces.Add(Celsius(nodeK));
        }

        var total = new Quantity<ThermalResistance>(totalKpW, ThermalResistanceUnits.KelvinPerWatt);
        var u = new Quantity<HeatTransferCoefficient>(1.0 / (totalKpW * areaM2), HeatTransferCoefficientUnits.WattPerSquareMetreKelvin);
        var heat = new Quantity<Power>(heatW, PowerUnits.Watt);
        var flux = new Quantity<HeatFlux>(heatW / areaM2, HeatFluxUnits.WattPerSquareMetre);

        for (var i = 0; i < names.Count; i++)
            context.RecordIntermediate($"{names[i]} resistance", resistances[i]);

        context.RecordIntermediate("Total thermal resistance", total);
        context.RecordIntermediate("Overall heat transfer coefficient", u);
        context.RecordIntermediate("Heat flow", heat);
        context.RecordIntermediate("Surface temperatures", surfaces);

        return new PlaneWallHeatTransferResult(names, resistances, total, u, heat, flux, drops, surfaces);
    }

    private static Quantity<Temperature> Celsius(double kelvin) =>
        new Quantity<Temperature>(kelvin, TemperatureUnits.Kelvin).ConvertTo(TemperatureUnits.DegreeCelsius);
}
