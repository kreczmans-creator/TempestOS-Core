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

            // A reference input names its library; nothing else does.
            Assert.Equal(input.Kind == CalculationInputKind.Reference, input.Library is not null);

            // A sourced input names a reference input of this same module and
            // a property that input's library can read, of the right kind —
            // for a material, a well-known property of the input's own dimension.
            if (input.IsSourced)
            {
                var source = descriptor.Inputs.SingleOrDefault(i => i.Name == input.SourceInputName);
                Assert.True(source is { Kind: CalculationInputKind.Reference }, $"{id}.{input.Name} is read from '{input.SourceInputName}', which is not a reference input of the module.");
                Assert.NotEqual(CalculationInputKind.Reference, input.Kind);
                var property = input.SourcePropertyName;
                Assert.False(string.IsNullOrWhiteSpace(property), $"{id}.{input.Name} names no property to read.");

                switch (source!.Library)
                {
                    case ReferenceLibrary.Materials:
                        Assert.Equal(CalculationInputKind.Quantity, input.Kind);
                        Assert.True(MaterialPropertyNames.IsWellKnown(property!), $"{id}.{input.Name}: {property} is not a well-known material property.");
                        Assert.Equal(MaterialPropertyNames.ExpectedDimensionOf(property!), input.DimensionName);
                        break;
                    case ReferenceLibrary.Fasteners:
                        Assert.Contains(property, input.Kind == CalculationInputKind.Quantity ? FastenerPropertyReader.QuantityProperties : FastenerPropertyReader.TextProperties);
                        break;
                    case ReferenceLibrary.Bearings:
                        Assert.Contains(property, input.Kind == CalculationInputKind.Quantity ? BearingPropertyReader.QuantityProperties : BearingPropertyReader.TextProperties);
                        break;
                }
            }
            else
            {
                Assert.Null(input.SourcePropertyName);
            }

            Assert.False(string.IsNullOrWhiteSpace(input.Label));
            Assert.False(string.IsNullOrWhiteSpace(input.Limits));
            Assert.False(string.IsNullOrWhiteSpace(input.Description));
        }
    }

    [Fact]
    public void EachReferenceInputHasItsOwnLibrary_TheLugTwoMaterials_TheFastenerAndBearingModulesTheirOwn()
    {
        var lug = CalculationModuleDescriptors.For(LiftingLugPinJointCalculationDefinition.Id)!;
        Assert.Equal(["LugMaterialPin", "PinMaterialPin"], lug.References.Select(r => r.Name).ToList());
        Assert.All(lug.References, r => Assert.Equal(ReferenceLibrary.Materials, r.Library));
        Assert.All(lug.References, r => Assert.False(r.IsOptional));

        foreach (var id in new[] { BoltedJointPreloadCalculationDefinition.Id, BoltGroupEccentricShearCalculationDefinition.Id })
        {
            var fastener = Assert.Single(CalculationModuleDescriptors.For(id)!.References);
            Assert.Equal("FastenerPin", fastener.Name);
            Assert.Equal(ReferenceLibrary.Fasteners, fastener.Library);
            Assert.True(fastener.IsOptional);
        }

        var bearing = Assert.Single(CalculationModuleDescriptors.For(BearingRatingLifeCalculationDefinition.Id)!.References);
        Assert.Equal(ReferenceLibrary.Bearings, bearing.Library);
        Assert.True(bearing.IsOptional);

        // The bolted joint reads three inputs from its fastener; the bearing
        // life reads three from its bearing, one of them a choice.
        var joint = CalculationModuleDescriptors.For(BoltedJointPreloadCalculationDefinition.Id)!;
        Assert.Equal(["FastenerGrade", "TensileStressArea", "ProofStrength"], joint.Inputs.Where(i => i.IsSourced).Select(i => i.Name).ToList());
        var life = CalculationModuleDescriptors.For(BearingRatingLifeCalculationDefinition.Id)!;
        Assert.Equal(["BearingDesignation", "BearingType", "BasicDynamicLoadRating"], life.Inputs.Where(i => i.IsSourced).Select(i => i.Name).ToList());
        Assert.Equal(CalculationInputKind.Choice, life.Inputs.Single(i => i.Name == "BearingType").Kind);

        // The five original definitions read nothing from a record.
        foreach (var original in CalculationModuleDescriptors.All.Where(d => d.SpecificationPath is null))
            Assert.DoesNotContain(original.Inputs, i => i.IsSourced || i.Kind == CalculationInputKind.Reference);
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
