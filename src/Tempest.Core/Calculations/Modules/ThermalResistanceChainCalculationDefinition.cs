using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>One thermal resistance in the chain, typed as one row: "Junction to case, 0.5 K/W".</summary>
/// <param name="Name">What the resistance is between, in the words a datasheet uses.</param>
/// <param name="Resistance">The thermal resistance.</param>
public sealed record ThermalResistanceStage(string Name, Quantity<ThermalResistance> Resistance);

/// <summary>The inputs to one steady-state junction (or source) temperature check.</summary>
/// <param name="PowerDissipation">The steady power the component dissipates: the heat flow through every stage.</param>
/// <param name="AmbientTemperature">The ambient the last stage rejects heat to.</param>
/// <param name="Stages">The resistances in series, hottest first: junction to case, case to heat sink, heat sink to ambient.</param>
/// <param name="MaximumSourceTemperature">The highest temperature the hottest node (the junction, or the case) is permitted.</param>
public sealed record ThermalResistanceChainInput(
    Quantity<Power> PowerDissipation,
    Quantity<Temperature> AmbientTemperature,
    IReadOnlyList<ThermalResistanceStage> Stages,
    Quantity<Temperature> MaximumSourceTemperature);

/// <summary>The result of one thermal resistance chain. This method has no refusal, so every figure is always present.</summary>
/// <param name="Outcome">Whether the source temperature is within its permitted maximum.</param>
/// <param name="TotalThermalResistance">The sum of the stages.</param>
/// <param name="StageTemperatureRises">The temperature drop across each stage, hottest first.</param>
/// <param name="NodeTemperatures">The temperature at the hot side of each stage, hottest first: the first is the source temperature.</param>
/// <param name="SourceTemperature">The temperature of the hottest node: the junction.</param>
/// <param name="TotalTemperatureRise">The whole rise from ambient to the source.</param>
/// <param name="ThermalMargin">How far the source sits below its permitted maximum; negative when over.</param>
/// <param name="MaximumPermittedTotalResistance">The largest total resistance that keeps the source at its maximum: (T_max − T_a) / P. The heat sink budget.</param>
/// <param name="Utilisation">The rise above ambient over the permitted rise above ambient.</param>
/// <param name="CriterionMet">Whether the utilisation is at or below one.</param>
public sealed record ThermalResistanceChainResult(
    EngineeringCheckOutcome Outcome,
    Quantity<ThermalResistance> TotalThermalResistance,
    IReadOnlyList<Quantity<TemperatureDelta>> StageTemperatureRises,
    IReadOnlyList<Quantity<Temperature>> NodeTemperatures,
    Quantity<Temperature> SourceTemperature,
    Quantity<TemperatureDelta> TotalTemperatureRise,
    Quantity<TemperatureDelta> ThermalMargin,
    Quantity<ThermalResistance>? MaximumPermittedTotalResistance,
    double Utilisation,
    bool CriterionMet);

/// <summary>
/// A steady-state thermal resistance chain from a heat source (a
/// semiconductor junction) to ambient: the heat sink sizing calculation.
/// Specified in <c>docs/engineering/calculations/calc.thermal-resistance-chain.md</c>.
/// </summary>
/// <remarks>
/// Recovered from the v0.16.0 release-candidate suite's Heat Sink / Thermal
/// Calculator, which fixed the chain at three stages (junction to case, case
/// to sink, sink to ambient). Generalised here to any number of stages in
/// series, and given the heat-sink budget — the largest total resistance the
/// limit permits — that sizing a sink starts from.
/// <para>
/// <b>Rises are temperature differences, not temperatures.</b> Each stage's
/// drop is a <see cref="TemperatureDelta"/> in kelvin; only the node
/// temperatures are absolute and carry Celsius's offset.
/// </para>
/// </remarks>
public sealed class ThermalResistanceChainCalculationDefinition : ICalculationDefinition<ThermalResistanceChainInput, ThermalResistanceChainResult>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "calc.thermal-resistance-chain";

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Heat Sink Thermal Resistance Chain",
        Description:
            "Steady-state source (junction) temperature from a series chain of thermal resistances to ambient — junction to case, "
            + "case to heat sink, heat sink to ambient — with each stage's temperature drop, the margin against the permitted "
            + "maximum, and the total resistance the limit allows.",
        Category: "Thermal",
        Assumptions:
        [
            new CalculationAssumption("Steady state: the whole dissipated power flows through the one chain, and each resistance is constant.", "No thermal capacitance, so nothing about warm-up or a transient overload; no parallel path through the board."),
            new CalculationAssumption("The stated ambient is the temperature the last stage actually rejects to.", "Air preheated by neighbouring parts can be well above room temperature."),
        ],
        Constraints:
        [
            new CalculationConstraint("Power must not be negative; at least one stage, each named, each resistance zero or positive, the total positive; the maximum source temperature must be above ambient."),
            new CalculationConstraint("The source temperature must not exceed the permitted maximum."),
        ]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException">Any constraint above is not met.</exception>
    public ThermalResistanceChainResult Calculate(ThermalResistanceChainInput input, CalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(context);

        var stages = input.Stages ?? [];
        var powerW = input.PowerDissipation.BaseValue;
        var ambientK = input.AmbientTemperature.BaseValue;
        var maximumK = input.MaximumSourceTemperature.BaseValue;

        ModuleGuards.Require(context, "Power dissipation must not be negative.", powerW >= 0, input, nameof(input.PowerDissipation));
        ModuleGuards.Require(context, "At least one thermal resistance stage is required.", stages.Count >= 1, $"{stages.Count} stage(s)");
        foreach (var stage in stages)
        {
            ModuleGuards.Require(context, "Every stage must be named.", !string.IsNullOrWhiteSpace(stage.Name), "an unnamed stage");
            ModuleGuards.Require(context, "Every thermal resistance must be zero or positive.", stage.Resistance.BaseValue >= 0, $"{stage.Name}: {EngineeringNumber.Format(stage.Resistance.BaseValue)} K/W");
        }

        var totalKpW = stages.Sum(s => s.Resistance.BaseValue);
        ModuleGuards.Require(context, "The total thermal resistance must be positive.", totalKpW > 0, $"{EngineeringNumber.Format(totalKpW)} K/W");
        ModuleGuards.Require(
            context,
            "The maximum source temperature must be above the ambient temperature.",
            maximumK > ambientK,
            $"maximum {ModuleGuards.Describe(input, nameof(input.MaximumSourceTemperature))} against ambient {ModuleGuards.Describe(input, nameof(input.AmbientTemperature))}");

        // Built from the source down: each node is the one above it less
        // that stage's drop, so the last node's cold side is the ambient.
        var totalRiseK = powerW * totalKpW;
        var sourceK = ambientK + totalRiseK;
        var rises = new Quantity<TemperatureDelta>[stages.Count];
        var nodes = new Quantity<Temperature>[stages.Count];
        var nodeK = sourceK;

        for (var i = 0; i < stages.Count; i++)
        {
            var riseK = powerW * stages[i].Resistance.BaseValue;
            nodes[i] = Celsius(nodeK);
            rises[i] = new Quantity<TemperatureDelta>(riseK, TemperatureDeltaUnits.Kelvin);
            context.RecordIntermediate($"{stages[i].Name} rise", rises[i]);
            nodeK -= riseK;
        }

        var permittedRiseK = maximumK - ambientK;
        var marginK = maximumK - sourceK;
        var utilisation = totalRiseK / permittedRiseK;
        var met = ModuleGuards.IsMet(utilisation);
        Quantity<ThermalResistance>? permitted = powerW > 0 ? new Quantity<ThermalResistance>(permittedRiseK / powerW, ThermalResistanceUnits.KelvinPerWatt) : null;

        var total = new Quantity<ThermalResistance>(totalKpW, ThermalResistanceUnits.KelvinPerWatt);
        var source = Celsius(sourceK);

        context.RecordIntermediate("Total thermal resistance", total);
        context.RecordIntermediate("Node temperatures", nodes);
        context.RecordIntermediate("Source temperature", source);
        context.RecordIntermediate("Maximum permitted total resistance", permitted?.ToString() ?? "unbounded — no power dissipated");
        context.RecordIntermediate("Utilisation", utilisation);

        context.RecordConstraintCheck(
            "The source temperature must not exceed the permitted maximum.",
            met,
            $"Source {EngineeringNumber.Format(sourceK - 273.15)} degC against maximum {EngineeringNumber.Format(maximumK - 273.15)} degC; margin {EngineeringNumber.Format(marginK)} K.");

        return new ThermalResistanceChainResult(
            ModuleGuards.OutcomeOf(met),
            total,
            rises,
            nodes,
            source,
            new Quantity<TemperatureDelta>(totalRiseK, TemperatureDeltaUnits.Kelvin),
            new Quantity<TemperatureDelta>(marginK, TemperatureDeltaUnits.Kelvin),
            permitted,
            utilisation,
            met);
    }

    private static Quantity<Temperature> Celsius(double kelvin) =>
        new Quantity<Temperature>(kelvin, TemperatureUnits.Kelvin).ConvertTo(TemperatureUnits.DegreeCelsius);
}
