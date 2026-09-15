using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.Materials;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// The form descriptors cannot drift from the definitions they describe:
/// every product calculation has one, every input is named exactly as the
/// input record names it, every quantity names a registered dimension and
/// a real unit of it, and every material-sourced input names a well-known
/// property of the same dimension.
/// </summary>
public class CalculationModuleDescriptorsTests
{
    /// <summary>Every product definition, original and `WP 21.7A` module alike.</summary>
    public static TheoryData<Type> EveryDefinition() =>
        [.. CalculationModuleDescriptors.All.Select(d => d.DefinitionType)];

    [Fact]
    public void EveryProductCalculationHasADescriptor_AndEveryDescriptorIsAProductCalculation()
    {
        var describedIds = CalculationModuleDescriptors.All.Select(d => d.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();
        var catalogueIds = ProductCalculationCatalogue.CalculationIds.OrderBy(id => id, StringComparer.Ordinal).ToList();

        Assert.Equal(catalogueIds, describedIds);
        Assert.Equal(16, describedIds.Count);
        Assert.All(describedIds, id => Assert.NotNull(CalculationModuleDescriptors.For(id)));
        Assert.Null(CalculationModuleDescriptors.For("calc.no-such-module"));
    }

    [Theory]
    [MemberData(nameof(EveryDefinition))]
    public void TheDescriptorIdAndTitleMatchTheDefinition(Type definitionType)
    {
        var id = (string)definitionType.GetField("Id")!.GetValue(null)!;
        var descriptor = CalculationModuleDescriptors.For(id)!;

        Assert.Same(definitionType, descriptor.DefinitionType);
        Assert.Equal(id, descriptor.Id);
        Assert.False(string.IsNullOrWhiteSpace(descriptor.Title));
        Assert.False(string.IsNullOrWhiteSpace(descriptor.MethodReference));
        Assert.Equal(descriptor.Metadata.Category, descriptor.Category);
        Assert.Equal(descriptor.DefinitionInterface.GetGenericArguments()[0], descriptor.InputType);
        Assert.Equal(descriptor.DefinitionInterface.GetGenericArguments()[1], descriptor.ResultType);

        if (descriptor.SpecificationPath is { } path)
        {
            Assert.StartsWith("docs/engineering/calculations/calc.", path, StringComparison.Ordinal);
            Assert.Equal(id + ".md", path["docs/engineering/calculations/".Length..]);
        }
    }

    [Theory]
    [MemberData(nameof(EveryDefinition))]
    public void TheDescriptorNamesEveryInputOfTheInputRecord_InOrder(Type definitionType)
    {
        var id = (string)definitionType.GetField("Id")!.GetValue(null)!;
        var descriptor = CalculationModuleDescriptors.For(id)!;
        var parameters = descriptor.InputType.GetConstructors().Single().GetParameters();

        Assert.Equal(parameters.Select(p => p.Name!).ToList(), descriptor.Inputs.Select(i => i.Name).ToList());

        foreach (var (parameter, input) in parameters.Zip(descriptor.Inputs))
        {
            var parameterType = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;

            switch (input.Kind)
            {
                case CalculationInputKind.Quantity:
                    Assert.True(parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() == typeof(Quantity<>), $"{id}.{input.Name} is described as a quantity but is a {parameter.ParameterType.Name}.");
                    Assert.Equal(parameterType.GetGenericArguments()[0].Name, input.DimensionName);
                    Assert.True(CalculationInputUnits.IsKnown(input.DimensionName!), $"{id}.{input.Name}: no unit catalogue is registered for {input.DimensionName}.");
                    Assert.Contains(input.DefaultUnitSymbol, CalculationInputUnits.SymbolsOf(input.DimensionName!));
                    break;
                case CalculationInputKind.Number:
                    Assert.True(parameterType == typeof(double) || parameterType == typeof(int), $"{id}.{input.Name} is described as a number but is a {parameter.ParameterType.Name}.");
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
                    Assert.True(parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>), $"{id}.{input.Name} is described as a list but is a {parameter.ParameterType.Name}.");
                    Assert.Equal(parameterType.GetGenericArguments()[0].GetConstructors().Single().GetParameters().Select(p => p.Name!).ToList(), input.Choices);
                    break;
                case CalculationInputKind.Reference:
                    Assert.Equal(typeof(Tempest.Core.ReferenceData.ReferencePin), parameterType);
                    break;
            }

            // Optional in the descriptor exactly when the record allows null.
            var nullable = Nullable.GetUnderlyingType(parameter.ParameterType) is not null
                || (!parameter.ParameterType.IsValueType && new System.Reflection.NullabilityInfoContext().Create(parameter).WriteState == System.Reflection.NullabilityState.Nullable);
            Assert.Equal(nullable, input.IsOptional);

            // A material-sourced input names a well-known property of its own dimension.
            if (input.MaterialPropertyName is { } property)
            {
                Assert.Equal(CalculationInputKind.Quantity, input.Kind);
                Assert.True(MaterialPropertyNames.IsWellKnown(property), $"{id}.{input.Name}: {property} is not a well-known material property.");
                Assert.Equal(MaterialPropertyNames.ExpectedDimensionOf(property), input.DimensionName);
            }

            Assert.False(string.IsNullOrWhiteSpace(input.Label));
            Assert.False(string.IsNullOrWhiteSpace(input.Limits));
            Assert.False(string.IsNullOrWhiteSpace(input.Description));
        }
    }

    [Fact]
    public void TheUnitRegistry_ParsesAndConvertsEveryRegisteredDimension()
    {
        foreach (var dimension in CalculationInputUnits.DimensionNames)
        {
            var symbols = CalculationInputUnits.SymbolsOf(dimension);
            Assert.NotEmpty(symbols);

            var parsed = CalculationInputUnits.TryParse(dimension, "2.5", symbols[0], out var problem);
            Assert.Null(problem);
            Assert.NotNull(parsed);
            Assert.Equal(2.5, CalculationInputUnits.ValueIn(dimension, parsed, symbols[0]), 1e-12);
        }

        Assert.Null(CalculationInputUnits.TryParse(nameof(Length), "abc", "mm", out var notANumber));
        Assert.Contains("not a number", notANumber);
        Assert.Null(CalculationInputUnits.TryParse(nameof(Length), "5", "kg", out var wrongUnit));
        Assert.Contains("not a unit of Length", wrongUnit);
        Assert.Null(CalculationInputUnits.TryParseWithUnit(nameof(Length), "75", out var noUnit));
        Assert.Contains("needs a unit", noUnit);
        Assert.Throws<ArgumentException>(() => CalculationInputUnits.SymbolsOf("Voltage"));

        var inches = CalculationInputUnits.TryParseWithUnit(nameof(Length), "2 in", out _);
        Assert.Equal(50.8, CalculationInputUnits.ValueIn(nameof(Length), inches!, "mm"), 1e-9);
    }
}
