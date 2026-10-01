using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.Calculations.Modules.Diagrams;

namespace Tempest.Core.Tests.Calculations.Modules;

/// <summary>
/// The calculator reference diagrams (`PO-2`, `ADR-0158`): every
/// calculation has exactly one diagram — a calculation is not complete
/// without it (Engineering Principle 33) — every shape is bound to an
/// input its calculation really has, every choice the form offers selects
/// a variant, and a label reads the form as typed — <c>L = 2000 mm</c>,
/// or <c>L = ?</c> where the value cannot be read.
/// </summary>
public class CalculationDiagramsTests
{
    public static TheoryData<string> EveryDiagram() => [.. CalculationDiagrams.All.Select(d => d.CalculationId)];

    [Fact]
    public void EveryCalculation_HasExactlyOneReferenceDiagram()
    {
        var drawn = CalculationDiagrams.All.Select(d => d.CalculationId).ToList();
        var duplicated = drawn.GroupBy(id => id, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(duplicated.Count == 0, $"More than one reference diagram for: {string.Join(", ", duplicated)}.");

        var missing = CalculationModuleDescriptors.All.Select(d => d.Id).Except(drawn, StringComparer.Ordinal).ToList();
        Assert.True(
            missing.Count == 0,
            $"A calculation is not complete without its reference diagram (Engineering Principle 33, ADR-0158). "
            + $"Add one to CalculationDiagrams.All for: {string.Join(", ", missing)}.");

        var orphaned = drawn.Except(CalculationModuleDescriptors.All.Select(d => d.Id), StringComparer.Ordinal).ToList();
        Assert.True(orphaned.Count == 0, $"Diagrams for no registered calculation: {string.Join(", ", orphaned)}.");
        Assert.All(CalculationModuleDescriptors.All, d => Assert.NotNull(CalculationDiagrams.For(d.Id)));
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

            // Every label anchor sits on the sheet, and every label — with a representative four-digit value and unit —
            // fits the room the sheet leaves it around that anchor, so no label is cut off at the sheet's edge.
            foreach (var element in variant.Elements)
            {
                var anchor = element.LabelAnchor;
                Assert.InRange(anchor.X, 0, CalculationDiagramSpec.Width);
                Assert.InRange(anchor.Y, 0, CalculationDiagramSpec.Height);

                if (element.Symbol is not { } symbol)
                    continue;

                var text = element.InputName is null ? symbol : $"{symbol} = {RepresentativeValue(inputs[element.InputName])}";
                var width = text.Length * CalculationDiagramSpec.LabelFontSize * CalculationDiagramSpec.LabelCharacterWidth;
                var leftAligned = element is DiagramDimension { IsVertical: true, LabelAt: null };
                var room = leftAligned ? CalculationDiagramSpec.Width - anchor.X : 2 * Math.Min(anchor.X, CalculationDiagramSpec.Width - anchor.X);
                Assert.True(width <= room, $"{calculationId}/{element.Id}: '{text}' needs {width:0} units at x={anchor.X}; the sheet leaves {room:0}.");
            }
        }

        // An empty form draws the variant drawn otherwise, or one keyed only on empty optional inputs.
        var empty = CalculationDiagramReader.SelectVariant(spec, module, _ => null);
        Assert.True(
            ReferenceEquals(empty, spec.Variants[^1]) || (empty.When.Count > 0 && empty.When.Values.All(v => v is "" or CalculationDiagramReader.BooleanFalse)),
            $"{calculationId}: an empty form drew '{empty.Caption}'.");
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

    [Theory]
    [InlineData(" 2 ", "Double shear")]
    [InlineData("+2", "Double shear")]
    [InlineData("02", "Double shear")]
    [InlineData("1", "Single shear")]
    [InlineData("+1", "Single shear")]
    [InlineData("3", "The bolt through lapped plates")]
    [InlineData("0", "The bolt through lapped plates")]
    [InlineData("-1", "The bolt through lapped plates")]
    [InlineData("2.0", "The bolt through lapped plates")]
    [InlineData("", "The bolt through lapped plates")]
    public void ANumberCondition_ReadsTheNumberAsTheFormDoes(string typed, string caption)
    {
        // The form reads "+2" and "02" as 2 (a whole number) and calculates double shear; the diagram must agree.
        // Anything but 1 or 2 is drawn with a neutral caption, never as "one shear plane".
        var spec = CalculationDiagrams.For(BoltShearCapacityCalculationDefinition.Id)!;
        var module = CalculationModuleDescriptors.For(BoltShearCapacityCalculationDefinition.Id)!;

        var variant = CalculationDiagramReader.SelectVariant(spec, module, name => name == "ShearPlanes" ? Field(name, typed) : null);

        Assert.StartsWith(caption, variant.Caption, StringComparison.Ordinal);
        Assert.Equal(caption == "Double shear" ? 3 : 2, variant.Elements.Count(e => e is DiagramPlate p && p.Id.StartsWith("plate", StringComparison.Ordinal)));
        if (caption == "Double shear")
            Assert.Equal(2, variant.Elements.Count(e => e is DiagramPointLoad { Symbol: "F/2" }));
    }

    [Fact]
    public void AWholeNumberInput_ShowsOnlyAWholeNumber_AsTheFormReadsIt()
    {
        var module = CalculationModuleDescriptors.For(BoltShearCapacityCalculationDefinition.Id)!;
        Assert.Equal("+2", CalculationDiagramReader.ValueText(module, "ShearPlanes", Field("ShearPlanes", "+2")));
        Assert.Equal(CalculationDiagramReader.Unknown, CalculationDiagramReader.ValueText(module, "ShearPlanes", Field("ShearPlanes", "2.0")));
        Assert.Equal("1.5", CalculationDiagramReader.ValueText(module, "SafetyFactor", Field("SafetyFactor", "1.5")));
    }

    [Fact]
    public void AnOptionalInputLeftEmpty_DrawsItsOwnBoundaryCondition()
    {
        // No restraint stiffness: a rigid restraint, no spring.
        var rigid = Read(ThermalExpansionStressCalculationDefinition.Id, new() { ["RestraintStiffness"] = Field("RestraintStiffness", "") });
        Assert.Contains("rigid", rigid.Variant.Caption, StringComparison.Ordinal);
        Assert.DoesNotContain(rigid.Shapes, s => s.Element is DiagramSpring);
        Assert.Equal("k_s = not given", rigid.LabelFor("RestraintStiffness"));

        var elastic = Read(ThermalExpansionStressCalculationDefinition.Id, new() { ["RestraintStiffness"] = Field("RestraintStiffness", "5", "kN/mm") });
        Assert.Contains("elastic", elastic.Variant.Caption, StringComparison.Ordinal);
        Assert.Contains(elastic.Shapes, s => s.Element is DiagramSpring { InputName: "RestraintStiffness" });

        // No film coefficient on a side: no film drawn there; the temperature is the wall face's own.
        var bare = Read(PlaneWallHeatTransferCalculationDefinition.Id, new()
        {
            ["HotSideFilmCoefficient"] = Field("HotSideFilmCoefficient", ""),
            ["ColdSideFilmCoefficient"] = Field("ColdSideFilmCoefficient", "25", "W/(m².K)"),
        });
        Assert.Contains("T₁ at the hot face", bare.Variant.Caption, StringComparison.Ordinal);
        Assert.DoesNotContain(bare.Shapes, s => s.Element is DiagramPointLoad { InputName: "HotSideFilmCoefficient" });
        Assert.Contains(bare.Shapes, s => s.Element is DiagramPointLoad { InputName: "ColdSideFilmCoefficient" });

        var both = Read(PlaneWallHeatTransferCalculationDefinition.Id, new()
        {
            ["HotSideFilmCoefficient"] = Field("HotSideFilmCoefficient", "10", "W/(m².K)"),
            ["ColdSideFilmCoefficient"] = Field("ColdSideFilmCoefficient", "25", "W/(m².K)"),
        });
        Assert.Equal(2, both.Shapes.Count(s => s.Element is DiagramPointLoad));
        var neither = Read(PlaneWallHeatTransferCalculationDefinition.Id, new());
        Assert.Contains("no films", neither.Variant.Caption, StringComparison.Ordinal);
        Assert.DoesNotContain(neither.Shapes, s => s.Element is DiagramPointLoad);
    }

    [Fact]
    public void TheSummary_IsWordedForAScreenReader_WithNoBareQuestionMarkOrUnderscore()
    {
        var reading = Read(BeamDeflectionCalculationDefinition.Id, new() { ["Span"] = Field("Span", "2000", "mm") });
        Assert.Contains("L = 2000 mm", reading.Summary, StringComparison.Ordinal);
        Assert.Contains("σ allow = not readable yet", reading.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("?", reading.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("_", reading.Summary, StringComparison.Ordinal);
        Assert.Equal("σ_allow = ?", reading.LabelFor("AllowableBendingStress"));
    }

    [Fact]
    public void SolidParts_AreDrawnSolid_NeverAsHoles()
    {
        // A bolt, a pin, a rolling element or a point is solid ink; a hole stays open.
        Assert.All(
            Read(BoltGroupEccentricShearCalculationDefinition.Id, new()).Shapes.Where(s => s.Element is DiagramCircle { InputName: "Bolts" }),
            s => Assert.True(((DiagramCircle)s.Element).Solid));
        var lug = Read(LiftingLugPinJointCalculationDefinition.Id, new()).Shapes.Select(s => s.Element).OfType<DiagramCircle>().ToList();
        Assert.True(lug.Single(c => c.Id == "pin").Solid);
        Assert.False(lug.Single(c => c.Id == "hole").Solid);
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
        Assert.Equal("T₁ = 20 degC", reading.LabelFor("HotSideTemperature"));
        Assert.Equal("h₁ = not given", reading.LabelFor("HotSideFilmCoefficient"));
        Assert.Equal("T₂ = ?", reading.LabelFor("ColdSideTemperature"));
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
    [Fact]
    public void TheChartLikeDiagrams_AreDrawn_OnAxesAndLines_BoundToTheirInputs()
    {
        string[] charts = [BearingRatingLifeCalculationDefinition.Id, FatigueMinerCalculationDefinition.Id, MaterialSelectionMarginCalculationDefinition.Id];
        Assert.All(charts, id => Assert.NotNull(CalculationDiagrams.For(id)));

        // The S-N diagram: axes, the line bound to its slope, the reference point, the cut-off, the blocks drawn representatively.
        var fatigue = CalculationDiagrams.For(FatigueMinerCalculationDefinition.Id)!;
        var module = CalculationModuleDescriptors.For(FatigueMinerCalculationDefinition.Id)!;
        var elements = fatigue.Variants.Single().Elements;
        Assert.Single(elements.OfType<DiagramAxes>());
        Assert.Contains(elements, e => e is DiagramPolyline { InputName: "Slope" });
        Assert.Contains(elements, e => e is DiagramPolyline { InputName: "EnduranceLimit", Dashed: true });
        Assert.True(elements.Count(e => e.InputName == "Blocks") >= 3);
        Assert.Contains("representatively", fatigue.Variants.Single().Caption, StringComparison.Ordinal);
        Assert.All(new[] { "CurveReference", "ReferenceStressRange", "ReferenceCycles", "Slope", "EnduranceLimit", "Blocks" }, name => Assert.Contains(name, fatigue.BoundInputNames));

        var typed = new Dictionary<string, CalculationFormField>
        {
            ["ReferenceStressRange"] = Field("ReferenceStressRange", "71", "MPa"),
            ["ReferenceCycles"] = Field("ReferenceCycles", "2000000"),
            ["Slope"] = Field("Slope", "3"),
            ["Blocks"] = new("Blocks", Rows: ["100 MPa, 100000", "60 MPa, 2000000"]),
        };
        var reading = CalculationDiagramReader.Read(fatigue, module, name => typed.GetValueOrDefault(name));
        Assert.Equal("Δσ_C = 71 MPa", reading.LabelFor("ReferenceStressRange"));
        Assert.Equal("N_C = 2000000", reading.LabelFor("ReferenceCycles"));
        Assert.Equal("m = 3", reading.LabelFor("Slope"));
        Assert.Equal("Δσ_L = not given", reading.LabelFor("EnduranceLimit"));
        Assert.Equal("blocks = 2 rows", reading.LabelFor("Blocks"));
        Assert.Equal("curve = ?", reading.LabelFor("CurveReference"));

        // The bearing: the rolling element follows the bearing type; loads and speed are labelled as typed.
        var bearing = CalculationDiagrams.For(BearingRatingLifeCalculationDefinition.Id)!;
        var bearingModule = CalculationModuleDescriptors.For(BearingRatingLifeCalculationDefinition.Id)!;
        var ball = CalculationDiagramReader.SelectVariant(bearing, bearingModule, name => name == "BearingType" ? Field(name, choice: nameof(RollingBearingType.Ball)) : null);
        var roller = CalculationDiagramReader.SelectVariant(bearing, bearingModule, name => name == "BearingType" ? Field(name, choice: nameof(RollingBearingType.Roller)) : null);
        Assert.Contains(ball.Elements, e => e is DiagramCircle { InputName: "BearingType" });
        Assert.Contains(roller.Elements, e => e is DiagramPlate { InputName: "BearingType" });
        Assert.DoesNotContain(roller.Elements, e => e is DiagramCircle { InputName: "BearingType" });
        var loads = CalculationDiagramReader.Read(bearing, bearingModule, name => name switch
        {
            "RadialLoad" => Field(name, "2", "kN"),
            "Speed" => Field(name, "1500", "r/min"),
            _ => null,
        });
        Assert.Equal("F_r = 2 kN", loads.LabelFor("RadialLoad"));
        Assert.Equal("F_a = ?", loads.LabelFor("AxialLoad"));
        Assert.Equal("n = 1500 r/min", loads.LabelFor("Speed"));
        Assert.Contains(loads.Shapes, s => s.Element is DiagramPointLoad { InputName: "AxialLoad", Direction: DiagramDirection.Right });

        // The margin: one stress on the piece and on its bar, the allowable on its own bar.
        var margin = CalculationDiagrams.For(MaterialSelectionMarginCalculationDefinition.Id)!;
        var marginModule = CalculationModuleDescriptors.For(MaterialSelectionMarginCalculationDefinition.Id)!;
        var stresses = CalculationDiagramReader.Read(margin, marginModule, name => name switch
        {
            "MaterialId" => Field(name, "S355"),
            "AppliedStress" => Field(name, "120", "MPa"),
            _ => null,
        });
        Assert.Equal("material = S355", stresses.LabelFor("MaterialId"));
        Assert.Equal("σ = 120 MPa", stresses.LabelFor("AppliedStress"));
        Assert.Equal("σ_allow = ?", stresses.LabelFor("MaterialAllowableStress"));
        Assert.Contains(stresses.Shapes, s => s.Element is DiagramPlate { InputName: "MaterialAllowableStress" });
        Assert.Contains(stresses.Shapes, s => s.Element is DiagramPlate { InputName: "AppliedStress" });
    }

    [Fact]
    public void APolyline_IsLabelledAboveItsMiddle_AndAxesAreNeverBound()
    {
        var line = new DiagramPolyline("line", [new(10, 50), new(110, 20), new(210, 80)]);
        Assert.Equal(new DiagramPoint(110, 8), line.LabelAnchor);
        Assert.Equal(new DiagramPoint(5, 6), (line with { LabelAt = new(5, 6) }).LabelAnchor);
        Assert.Equal(new DiagramPoint(0, 0), new DiagramPolyline("empty", []).LabelAnchor);

        var axes = new DiagramAxes("axes", new(40, 200), 380, 20);
        Assert.Null(axes.InputName);
        Assert.Null(axes.Symbol);
        Assert.Equal(new DiagramPoint(40, 200), axes.LabelAnchor);
    }

    /// <summary>A value as long as a label typically shows for <paramref name="input"/>: four digits and a unit, the longest choice, "yes", a row count.</summary>
    private static string RepresentativeValue(CalculationInputDescriptor input) => input.Kind switch
    {
        CalculationInputKind.Quantity => "1250 mm",
        CalculationInputKind.Number => "1250",
        CalculationInputKind.Choice => input.Choices!.Select(CalculationModuleForm.Humanise).MaxBy(c => c.Length)!,
        CalculationInputKind.Boolean => "yes",
        CalculationInputKind.List => "12 rows",
        _ => "S355J2",
    };

    private static CalculationFormField Field(string name, string? text = null, string? unit = null, string? choice = null) =>
        new(name, text, unit, choice, false, null, null);
}
