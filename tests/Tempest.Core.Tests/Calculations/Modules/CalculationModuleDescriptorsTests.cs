using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// The form descriptors cannot drift from the definitions they describe:
/// every module has one, every input is named exactly as the input record
/// names it, and every quantity names a real dimension.
/// </summary>
public class CalculationModuleDescriptorsTests
{
    private static readonly IReadOnlyList<Type> ModuleTypes =
    [
        typeof(BeamDeflectionCalculationDefinition),
        typeof(BoltedJointPreloadCalculationDefinition),
        typeof(BoltGroupEccentricShearCalculationDefinition),
        typeof(FilletWeldThroatStressCalculationDefinition),
        typeof(LiftingLugPinJointCalculationDefinition),
        typeof(ColumnBucklingCalculationDefinition),
        typeof(ShaftCombinedStressCalculationDefinition),
        typeof(BearingRatingLifeCalculationDefinition),
        typeof(ThickWalledCylinderCalculationDefinition),
        typeof(ThermalExpansionStressCalculationDefinition),
        typeof(FatigueMinerCalculationDefinition),
    ];

    /// <summary>Every module type with its descriptor.</summary>
    public static TheoryData<Type> EveryModule() => [.. ModuleTypes];

    [Fact]
    public void EveryModuleHasADescriptor_AndEveryDescriptorIsAModule()
    {
        var moduleIds = ModuleTypes.Select(t => (string)t.GetField("Id")!.GetValue(null)!).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var describedIds = CalculationModuleDescriptors.All.Select(d => d.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();

        Assert.Equal(moduleIds, describedIds);
        Assert.All(moduleIds, id => Assert.Contains(id, ProductCalculationCatalogue.CalculationIds));
        Assert.All(moduleIds, id => Assert.NotNull(CalculationModuleDescriptors.For(id)));
        Assert.Null(CalculationModuleDescriptors.For("calc.no-such-module"));
    }

    [Theory]
    [MemberData(nameof(EveryModule))]
    public void TheDescriptorNamesEveryInputOfTheInputRecord_InOrder(Type moduleType)
    {
        var id = (string)moduleType.GetField("Id")!.GetValue(null)!;
        var descriptor = CalculationModuleDescriptors.For(id)!;

        var definitionInterface = moduleType.GetInterfaces().Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICalculationDefinition<,>));
        var inputType = definitionInterface.GetGenericArguments()[0];
        var parameters = inputType.GetConstructors().Single().GetParameters();

        Assert.Equal(parameters.Select(p => p.Name!).ToList(), descriptor.Inputs.Select(i => i.Name).ToList());

        foreach (var (parameter, input) in parameters.Zip(descriptor.Inputs))
        {
            var parameterType = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;

            switch (input.Kind)
            {
                case CalculationInputKind.Quantity:
                    Assert.True(parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() == typeof(Quantity<>), $"{id}.{input.Name} is described as a quantity but is a {parameter.ParameterType.Name}.");
                    Assert.Equal(parameterType.GetGenericArguments()[0].Name, input.DimensionName);
                    Assert.False(string.IsNullOrWhiteSpace(input.DefaultUnitSymbol));
                    break;
                case CalculationInputKind.Number:
                    Assert.Equal(typeof(double), parameterType);
                    break;
                case CalculationInputKind.Text:
                    Assert.Equal(typeof(string), parameterType);
                    break;
                case CalculationInputKind.Choice:
                    Assert.True(parameterType.IsEnum, $"{id}.{input.Name} is described as a choice but is a {parameter.ParameterType.Name}.");
                    Assert.Equal(Enum.GetNames(parameterType), input.Choices);
                    break;
                case CalculationInputKind.Boolean:
                    Assert.Equal(typeof(bool), parameterType);
                    break;
                case CalculationInputKind.List:
                    Assert.True(typeof(System.Collections.IEnumerable).IsAssignableFrom(parameterType), $"{id}.{input.Name} is described as a list but is a {parameter.ParameterType.Name}.");
                    break;
                case CalculationInputKind.Reference:
                    Assert.Equal(typeof(Tempest.Core.ReferenceData.ReferencePin), parameterType);
                    break;
            }

            // Optional in the descriptor exactly when the record allows null.
            var nullable = Nullable.GetUnderlyingType(parameter.ParameterType) is not null
                || (!parameter.ParameterType.IsValueType && new System.Reflection.NullabilityInfoContext().Create(parameter).WriteState == System.Reflection.NullabilityState.Nullable);
            Assert.Equal(nullable, input.IsOptional);

            Assert.False(string.IsNullOrWhiteSpace(input.Label));
            Assert.False(string.IsNullOrWhiteSpace(input.Limits));
            Assert.False(string.IsNullOrWhiteSpace(input.Description));
        }

        Assert.False(string.IsNullOrWhiteSpace(descriptor.MethodReference));
        Assert.StartsWith("docs/engineering/calculations/calc.", descriptor.SpecificationPath, StringComparison.Ordinal);
        Assert.Equal(id + ".md", descriptor.SpecificationPath["docs/engineering/calculations/".Length..]);
    }

    [Theory]
    [MemberData(nameof(EveryModule))]
    public void TheDescriptorsUnitSymbols_AreRealUnitsOfTheirDimension(Type moduleType)
    {
        var id = (string)moduleType.GetField("Id")!.GetValue(null)!;
        var descriptor = CalculationModuleDescriptors.For(id)!;

        foreach (var input in descriptor.Inputs.Where(i => i.Kind == CalculationInputKind.Quantity))
        {
            var symbols = input.DimensionName switch
            {
                nameof(Length) => LengthUnits.All.Select(u => u.Symbol),
                nameof(Force) => ForceUnits.All.Select(u => u.Symbol),
                nameof(Pressure) => PressureUnits.All.Select(u => u.Symbol),
                nameof(Area) => AreaUnits.All.Select(u => u.Symbol),
                nameof(SecondMomentOfArea) => SecondMomentOfAreaUnits.All.Select(u => u.Symbol),
                nameof(Torque) => TorqueUnits.All.Select(u => u.Symbol),
                nameof(Stiffness) => StiffnessUnits.All.Select(u => u.Symbol),
                nameof(RotationalSpeed) => RotationalSpeedUnits.All.Select(u => u.Symbol),
                nameof(Duration) => DurationUnits.All.Select(u => u.Symbol),
                nameof(ThermalExpansion) => ThermalExpansionUnits.All.Select(u => u.Symbol),
                nameof(TemperatureDelta) => TemperatureDeltaUnits.All.Select(u => u.Symbol),
                _ => throw new Xunit.Sdk.XunitException($"{id}.{input.Name}: no unit catalogue known for dimension {input.DimensionName}."),
            };

            Assert.Contains(input.DefaultUnitSymbol, symbols);
        }
    }
}
