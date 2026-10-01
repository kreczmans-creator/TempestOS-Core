using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.Calculations.Modules.Diagrams;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// The calculator reference diagrams (`PO-2`, work packages A and D1): every
/// calculation has a diagram or is honestly listed as having none yet, every
/// shape is bound to an input its calculation really has, every choice the
/// form offers selects a variant, and a label reads the form as typed —
/// <c>L = 2000 mm</c>, or <c>L = ?</c> where the value cannot be read.
/// </summary>
public class CalculationDiagramsTests
{
    /// <summary>
    /// The calculations with no diagram on the day this list was written.
    /// <see cref="CalculationDiagrams.NoDiagramYet"/> may only shrink from
    /// this: adding a calculation here is a regression, removing one is a
    /// diagram delivered (remove it here too).
    /// </summary>
    private static readonly string[] NoDiagramCeiling =
    [
        MaterialSelectionMarginCalculationDefinition.Id,
        BearingRatingLifeCalculationDefinition.Id,
        FatigueMinerCalculationDefinition.Id,
    ];

    public static TheoryData<string> EveryDiagram() => [.. CalculationDiagrams.All.Select(d => d.CalculationId)];

    [Fact]
    public void EveryCalculation_HasADiagram_OrIsOnTheNoDiagramYetList_NeverBoth()
    {
        var drawn = CalculationDiagrams.All.Select(d => d.CalculationId).ToList();
        var pending = CalculationDiagrams.NoDiagramYet;

        Assert.Equal(drawn.Count, drawn.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(pending.Count, pending.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(drawn.Intersect(pending, StringComparer.Ordinal));

        var everyId = CalculationModuleDescriptors.All.Select(d => d.Id).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(everyId, drawn.Concat(pending).Order(StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void TheNoDiagramYetList_OnlyShrinks()
    {
        var added = CalculationDiagrams.NoDiagramYet.Except(NoDiagramCeiling, StringComparer.Ordinal).ToList();
        Assert.True(added.Count == 0, $"Calculations newly listed as having no diagram (draw one instead): {string.Join(", ", added)}.");
    }

    [Fact]
    public void TheSimpleDiagrams_AreDrawn()
    {
        string[] simple =
        [
            BeamBendingStressCalculationDefinition.Id, BeamDeflectionCalculationDefinition.Id, ColumnBucklingCalculationDefinition.Id,
            BoltShearCapacityCalculationDefinition.Id, BearingLoadCapacityCalculationDefinition.Id, ThermalExpansionStressCalculationDefinition.Id,
            ShaftCombinedStressCalculationDefinition.Id, PressureVesselWallThicknessCalculationDefinition.Id,
            PlaneWallHeatTransferCalculationDefinition.Id, ThermalResistanceChainCalculationDefinition.Id, ToleranceStackCalculationDefinition.Id,
            BoltGroupEccentricShearCalculationDefinition.Id, FilletWeldThroatStressCalculationDefinition.Id, LiftingLugPinJointCalculationDefinition.Id,
            ThickWalledCylinderCalculationDefinition.Id, BoltedJointPreloadCalculationDefinition.Id,
        ];

        Assert.All(simple, id => Assert.NotNull(CalculationDiagrams.For(id)));
        Assert.Null(CalculationDiagrams.For(FatigueMinerCalculationDefinition.Id));
    }

    [Theory]
    [MemberData(nameof(EveryDiagram))]
    public void EveryBoundInput_ExistsOnTheDescriptor_AndEveryChoiceConditionNamesAMember(string calculationId)
    {
        var spec = CalculationDiagrams.For(calculationId)!;
        var module = CalculationModuleDescriptors.For(calculationId)!;
        var inputs = module.Inputs.ToDictionary(i => i.Name, StringComparer.Ordinal);

        Assert.NotEmpty(spec.Variants);
        Assert.All(spec.BoundInputNames, name => Assert.True(inputs.ContainsKey(name), $"{calculationId}: no input '{name}'."));

        foreach (var variant in spec.Variants)
        {
            Assert.False(string.IsNullOrWhiteSpace(variant.Caption));
            Assert.Equal(variant.Elements.Count, variant.Elements.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count());

            foreach (var (name, value) in variant.When)
            {
                if (inputs[name].Kind == CalculationInputKind.Choice)
                    Assert.Contains(value, inputs[name].Choices!);
            }

            // Every shape and label sits on the sheet.
            foreach (var element in variant.Elements)
            {
                var anchor = element.LabelAnchor;
                Assert.InRange(anchor.X, 0, CalculationDiagramSpec.Width);
                Assert.InRange(anchor.Y, 0, CalculationDiagramSpec.Height);
            }
        }

        // An empty form still draws a variant.
        Assert.Contains(CalculationDiagramReader.SelectVariant(spec, module, _ => null), spec.Variants);
    }

    [Theory]
    [MemberData(nameof(EveryDiagram))]
    public void EveryCombinationOfChoices_SelectsAVariantThatNamesIt(string calculationId)
    {
        var spec = CalculationDiagrams.For(calculationId)!;
        var module = CalculationModuleDescriptors.For(calculationId)!;
        var conditioned = spec.Variants.SelectMany(v => v.When.Keys).Distinct(StringComparer.Ordinal)
            .Select(n => module.Inputs.Single(i => i.Name == n))
            .Where(i => i.Kind == CalculationInputKind.Choice)
            .ToList();

        IEnumerable<Dictionary<string, string>> combinations = [new Dictionary<string, string>(StringComparer.Ordinal)];
        foreach (var input in conditioned)
            combinations = combinations.SelectMany(c => input.Choices!.Select(choice => new Dictionary<string, string>(c, StringComparer.Ordinal) { [input.Name] = choice }));

        foreach (var combination in combinations)
        {
            var variant = CalculationDiagramReader.SelectVariant(spec, module, name => combination.TryGetValue(name, out var choice) ? Field(name, choice: choice) : null);
            Assert.All(conditioned, input => Assert.Equal(combination[input.Name], variant.When[input.Name]));
        }
    }

    [Fact]
    public void ALabel_ReadsTheValueAsTyped_WithItsUnit_OrAQuestionMark()
    {
        var spec = CalculationDiagrams.For(BeamDeflectionCalculationDefinition.Id)!;
        var module = CalculationModuleDescriptors.For(BeamDeflectionCalculationDefinition.Id)!;

        var typed = new Dictionary<string, CalculationFormField>
        {
            ["Span"] = Field("Span", "2000", "mm"),
            ["Load"] = Field("Load", "ten", "kN"),
            ["Support"] = Field("Support", choice: nameof(BeamSupport.Cantilever)),
            ["Loading"] = Field("Loading", choice: nameof(BeamLoading.UniformlyDistributed)),
        };

        var reading = CalculationDiagramReader.Read(spec, module, name => typed.GetValueOrDefault(name));

        Assert.Equal("L = 2000 mm", reading.LabelFor("Span"));
        Assert.Equal("W (total) = ?", reading.LabelFor("Load"));
        Assert.Equal("I = ?", reading.LabelFor("SecondMomentOfArea"));
        Assert.Equal("Cantilever, uniformly distributed load", reading.Variant.Caption);
        Assert.Contains(reading.Shapes, s => s.Element is DiagramDistributedLoad);
        Assert.Contains(reading.Shapes, s => s.Element is DiagramSupport { Kind: DiagramSupportKind.Fixed });
        Assert.StartsWith(module.Title, reading.Summary, StringComparison.Ordinal);
        Assert.Contains(CalculationDiagramReader.NotToScale, reading.Summary, StringComparison.Ordinal);
        Assert.Contains("L = 2000 mm", reading.Summary, StringComparison.Ordinal);

        // A unit the dimension does not have is no more readable than a typo.
        typed["Span"] = Field("Span", "2000", "kg");
        Assert.Equal("L = ?", CalculationDiagramReader.Read(spec, module, name => typed.GetValueOrDefault(name)).LabelFor("Span"));
    }

    [Fact]
    public void ANumberCondition_SelectsTheDoubleShearVariant()
    {
        var spec = CalculationDiagrams.For(BoltShearCapacityCalculationDefinition.Id)!;
        var module = CalculationModuleDescriptors.For(BoltShearCapacityCalculationDefinition.Id)!;

        var twin = CalculationDiagramReader.SelectVariant(spec, module, name => name == "ShearPlanes" ? Field(name, " 2 ") : null);
        var single = CalculationDiagramReader.SelectVariant(spec, module, name => name == "ShearPlanes" ? Field(name, "1") : null);

        Assert.StartsWith("Double shear", twin.Caption, StringComparison.Ordinal);
        Assert.StartsWith("Single shear", single.Caption, StringComparison.Ordinal);
        Assert.Equal(3, twin.Elements.Count(e => e is DiagramPlate p && p.Id.StartsWith("plate", StringComparison.Ordinal)));
    }

    [Fact]
    public void AListInput_IsLabelledWithHowManyRowsTheFormLists_AndTheRepresentativeShapesHighlightWithIt()
    {
        var spec = CalculationDiagrams.For(PlaneWallHeatTransferCalculationDefinition.Id)!;
        var module = CalculationModuleDescriptors.For(PlaneWallHeatTransferCalculationDefinition.Id)!;

        var typed = new Dictionary<string, CalculationFormField>
        {
            ["Layers"] = new("Layers", Rows: ["Glass, 8 mm, 0.78 W/(m.K)", "", "Air, 10 mm, 0.026 W/(m.K)"]),
            ["HotSideTemperature"] = Field("HotSideTemperature", "20", "degC"),
        };
        var reading = CalculationDiagramReader.Read(spec, module, name => typed.GetValueOrDefault(name));

        Assert.Equal("Layers = 2 rows", reading.LabelFor("Layers"));
        Assert.Equal("T_1 = 20 degC", reading.LabelFor("HotSideTemperature"));
        Assert.Equal("h_1 = not given", reading.LabelFor("HotSideFilmCoefficient"));
        Assert.Equal("T_2 = ?", reading.LabelFor("ColdSideTemperature"));
        Assert.True(reading.Shapes.Count(s => s.Element.InputName == "Layers") >= 3);

        Assert.Equal("1 row", CalculationDiagramReader.ValueText(module, "Layers", new("Layers", Rows: ["Glass, 8 mm, 0.78 W/(m.K)"])));
        Assert.Equal(CalculationDiagramReader.Unknown, CalculationDiagramReader.ValueText(module, "Layers", new("Layers", Rows: [" "])));

        var stack = CalculationDiagramReader.Read(
            CalculationDiagrams.For(ToleranceStackCalculationDefinition.Id)!,
            CalculationModuleDescriptors.For(ToleranceStackCalculationDefinition.Id)!,
            name => name == "MinimumResult" ? Field(name, "0.1", "mm") : null);
        Assert.Equal("min gap = 0.1 mm", stack.LabelFor("MinimumResult"));
        Assert.Equal("Contributors = ?", stack.LabelFor("Contributors"));

        var chain = CalculationDiagramReader.Read(
            CalculationDiagrams.For(ThermalResistanceChainCalculationDefinition.Id)!,
            CalculationModuleDescriptors.For(ThermalResistanceChainCalculationDefinition.Id)!,
            name => name == "PowerDissipation" ? Field(name, "40", "W") : null);
        Assert.Equal("P = 40 W", chain.LabelFor("PowerDissipation"));
        Assert.Equal("T_a = ?", chain.LabelFor("AmbientTemperature"));
    }

    [Fact]
    public void TheBoltGroup_DrawsTheListedBoltsRepresentatively_AndTheLoadAtItsPoint()
    {
        var reading = Read(BoltGroupEccentricShearCalculationDefinition.Id, new()
        {
            ["Bolts"] = new("Bolts", Rows: ["0 mm, 0 mm", "75 mm, 0 mm", "0 mm, 75 mm", "75 mm, 75 mm"]),
            ["LoadY"] = Field("LoadY", "-20", "kN"),
            ["LoadPointX"] = Field("LoadPointX", "250", "mm"),
        });

        Assert.Equal("Bolts = 4 rows", reading.LabelFor("Bolts"));
        Assert.Equal("F_y = -20 kN", reading.LabelFor("LoadY"));
        Assert.Equal("F_x = ?", reading.LabelFor("LoadX"));
        Assert.Equal("x_P = 250 mm", reading.LabelFor("LoadPointX"));
        Assert.Equal("y_P = ?", reading.LabelFor("LoadPointY"));
        Assert.Equal("V_allow = ?", reading.LabelFor("AllowableShearPerBolt"));
        Assert.Contains("six drawn", reading.Variant.Caption, StringComparison.Ordinal);
        Assert.Equal(6, reading.Shapes.Count(s => s.Element is DiagramCircle { InputName: "Bolts" }));
    }

    [Fact]
    public void TheFilletWeld_LabelsItsThroatLengthAndThreeForceComponents()
    {
        var reading = Read(FilletWeldThroatStressCalculationDefinition.Id, new()
        {
            ["ThroatThickness"] = Field("ThroatThickness", "5", "mm"),
            ["EffectiveLength"] = Field("EffectiveLength", "200", "mm"),
            ["ParallelForce"] = Field("ParallelForce", "150", "kN"),
            ["CorrelationFactor"] = Field("CorrelationFactor", "0.9"),
        });

        Assert.Equal("a = 5 mm", reading.LabelFor("ThroatThickness"));
        Assert.Equal("L = 200 mm", reading.LabelFor("EffectiveLength"));
        Assert.Equal("F_∥ = 150 kN", reading.LabelFor("ParallelForce"));
        Assert.Equal("F_⊥ = ?", reading.LabelFor("TransverseForce"));
        Assert.Equal("F_n = ?", reading.LabelFor("NormalForce"));
        Assert.Equal("β_w = 0.9", reading.LabelFor("CorrelationFactor"));
        Assert.Equal("γ_M2 = ?", reading.LabelFor("PartialFactor"));
        Assert.Equal("f_u = ?", reading.LabelFor("UltimateStrength"));
    }

    [Fact]
    public void TheLiftingLug_LabelsEveryDimensionOfLugAndPin_AndEveryAllowable()
    {
        var reading = Read(LiftingLugPinJointCalculationDefinition.Id, new()
        {
            ["Load"] = Field("Load", "50", "kN"),
            ["HoleDiameter"] = Field("HoleDiameter", "32", "mm"),
            ["PinDiameter"] = Field("PinDiameter", "30", "mm"),
            ["EdgeDistance"] = Field("EdgeDistance", "40", "mm"),
            ["Clearance"] = Field("Clearance", "2", "mm"),
        });

        Assert.Equal("P = 50 kN", reading.LabelFor("Load"));
        Assert.Equal("d_h = 32 mm", reading.LabelFor("HoleDiameter"));
        Assert.Equal("d_p = 30 mm", reading.LabelFor("PinDiameter"));
        Assert.Equal("a = 40 mm", reading.LabelFor("EdgeDistance"));
        Assert.Equal("g = 2 mm", reading.LabelFor("Clearance"));
        Assert.Equal("t = ?", reading.LabelFor("LugThickness"));
        Assert.Equal("W = ?", reading.LabelFor("LugWidth"));
        Assert.Equal("t_s = ?", reading.LabelFor("CheekPlateThickness"));
        Assert.Equal("S_t = ?", reading.LabelFor("AllowableTensileStress"));
        Assert.Equal("S_br = ?", reading.LabelFor("AllowableBearingStress"));
        Assert.Equal("S_v = ?", reading.LabelFor("AllowableShearStress"));
        Assert.Equal("S_b,pin = ?", reading.LabelFor("PinAllowableBendingStress"));
        Assert.Equal("S_v,pin = ?", reading.LabelFor("PinAllowableShearStress"));
    }

    [Fact]
    public void TheThickWalledCylinder_DrawsEndCapsOnlyWhenClosedEndsIsTicked()
    {
        var spec = CalculationDiagrams.For(ThickWalledCylinderCalculationDefinition.Id)!;
        var module = CalculationModuleDescriptors.For(ThickWalledCylinderCalculationDefinition.Id)!;

        var closed = Read(ThickWalledCylinderCalculationDefinition.Id, new()
        {
            ["ClosedEnds"] = new("ClosedEnds", Flag: true),
            ["InnerRadius"] = Field("InnerRadius", "50", "mm"),
            ["InternalPressure"] = Field("InternalPressure", "100", "MPa"),
        });
        Assert.Contains("closed ends", closed.Variant.Caption, StringComparison.Ordinal);
        Assert.Equal("closed ends = yes", closed.LabelFor("ClosedEnds"));
        Assert.Equal("a = 50 mm", closed.LabelFor("InnerRadius"));
        Assert.Equal("b = ?", closed.LabelFor("OuterRadius"));
        Assert.Equal("p_i = 100 MPa", closed.LabelFor("InternalPressure"));
        Assert.Equal("p_o = ?", closed.LabelFor("ExternalPressure"));
        Assert.Equal("S = ?", closed.LabelFor("AllowableStress"));
        Assert.Equal(2, closed.Shapes.Count(s => s.Element is DiagramPlate { InputName: "ClosedEnds" }));

        var open = CalculationDiagramReader.SelectVariant(spec, module, name => name == "ClosedEnds" ? new CalculationFormField(name, Flag: false) : null);
        Assert.Contains("open ends", open.Caption, StringComparison.Ordinal);
        Assert.DoesNotContain(open.Elements, e => e is DiagramPlate { InputName: "ClosedEnds" });
        Assert.Same(open, CalculationDiagramReader.SelectVariant(spec, module, _ => null));
    }

    [Fact]
    public void TheBoltedJoint_DrawsBoltAndMembersAsTwoSpringsInParallel_AndLabelsThePreloadAndTheLoad()
    {
        var reading = Read(BoltedJointPreloadCalculationDefinition.Id, new()
        {
            ["Preload"] = Field("Preload", "20000", "N"),
            ["BoltStiffness"] = Field("BoltStiffness", "500", "kN/mm"),
            ["ProofStrength"] = Field("ProofStrength", "600", "MPa"),
        });

        Assert.Equal("F_i = 20000 N", reading.LabelFor("Preload"));
        Assert.Equal("P = ?", reading.LabelFor("ExternalLoad"));
        Assert.Equal("k_b = 500 kN/mm", reading.LabelFor("BoltStiffness"));
        Assert.Equal("k_m = ?", reading.LabelFor("MemberStiffness"));
        Assert.Equal("A_t = ?", reading.LabelFor("TensileStressArea"));
        Assert.Equal("S_p = 600 MPa", reading.LabelFor("ProofStrength"));
        Assert.Contains(reading.Shapes, s => s.Element is DiagramSpring { InputName: "BoltStiffness" });
        Assert.Contains(reading.Shapes, s => s.Element is DiagramSpring { InputName: "MemberStiffness" });
    }

    [Fact]
    public void AnOptionalQuantityLeftEmpty_IsSaidToBeNotGiven()
    {
        var module = CalculationModuleDescriptors.For(ThermalExpansionStressCalculationDefinition.Id)!;
        Assert.Equal("not given", CalculationDiagramReader.ValueText(module, "RestraintStiffness", null));
        Assert.Equal(CalculationDiagramReader.Unknown, CalculationDiagramReader.ValueText(module, "Length", null));
    }

    private static CalculationDiagramReading Read(string calculationId, Dictionary<string, CalculationFormField> typed) =>
        CalculationDiagramReader.Read(CalculationDiagrams.For(calculationId)!, CalculationModuleDescriptors.For(calculationId)!, name => typed.GetValueOrDefault(name));

    private static CalculationFormField Field(string name, string? text = null, string? unit = null, string? choice = null) =>
        new(name, text, unit, choice, false, null, null);
}
