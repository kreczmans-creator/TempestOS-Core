using Tempest.Core.Materials;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations.Modules;

/// <summary>How a generated form should present one input of a calculation module.</summary>
public enum CalculationInputKind
{
    /// <summary>A <see cref="Quantity{TDimension}"/>: a number with a unit of the named dimension.</summary>
    Quantity,

    /// <summary>A plain number: a factor, a ratio, a count.</summary>
    Number,

    /// <summary>Free text, such as a fastener grade.</summary>
    Text,

    /// <summary>One of a fixed set of choices, listed in the descriptor.</summary>
    Choice,

    /// <summary>Yes or no.</summary>
    Boolean,

    /// <summary>A list of rows, such as bolt positions or load blocks.</summary>
    List,

    /// <summary>A <see cref="ReferenceData.ReferencePin"/> to a released reference record.</summary>
    Reference,
}

/// <summary>One input of a calculation module, as a form should present it.</summary>
/// <param name="Name">The input record's own property name, exactly.</param>
/// <param name="Label">What to call it on screen.</param>
/// <param name="Kind">What sort of value it is.</param>
/// <param name="DimensionName">For a quantity, the dimension's type name (<c>nameof(Length)</c> and so on); otherwise <see langword="null"/>.</param>
/// <param name="DefaultUnitSymbol">For a quantity, the unit an engineer would usually state it in; otherwise <see langword="null"/>.</param>
/// <param name="Limits">The limits the calculation enforces, in words.</param>
/// <param name="Description">What the input means.</param>
/// <param name="Choices">For a choice, the member names; for a list, the row's field names; otherwise <see langword="null"/>.</param>
/// <param name="IsOptional">Whether the input may be left empty (a nullable pin, limit or stiffness).</param>
/// <param name="MaterialPropertyName">
/// For a quantity that comes from the released material record, the
/// <see cref="MaterialPropertyNames"/> member it is read from, so a form
/// fills it from the picked record rather than letting it be typed;
/// otherwise <see langword="null"/>.
/// </param>
public sealed record CalculationInputDescriptor(
    string Name,
    string Label,
    CalculationInputKind Kind,
    string? DimensionName,
    string? DefaultUnitSymbol,
    string Limits,
    string Description,
    IReadOnlyList<string>? Choices = null,
    bool IsOptional = false,
    string? MaterialPropertyName = null);

/// <summary>One calculation module, as a catalogue should list it and a form should build it.</summary>
/// <param name="Id">The calculation Id the engine knows it by.</param>
/// <param name="Title">Its title.</param>
/// <param name="Category">The catalogue category it belongs under.</param>
/// <param name="MethodReference">The standard or text the method comes from.</param>
/// <param name="SpecificationPath">The repository path of its specification, or <see langword="null"/> for a definition that predates the specifications.</param>
/// <param name="DefinitionType">The <see cref="ICalculationDefinition{TInput, TResult}"/> implementation, so a form can build its input record and execute it.</param>
/// <param name="Inputs">Its inputs, in the input record's own order.</param>
public sealed record CalculationModuleDescriptor(
    string Id,
    string Title,
    string Category,
    string MethodReference,
    string? SpecificationPath,
    Type DefinitionType,
    IReadOnlyList<CalculationInputDescriptor> Inputs)
{
    /// <summary>The definition's own metadata: name, description, category, assumptions and constraints.</summary>
    public CalculationMetadata Metadata =>
        (CalculationMetadata)DefinitionType.GetProperty(nameof(ICalculationDefinition<object, object>.Metadata))!.GetValue(Activator.CreateInstance(DefinitionType))!;

    /// <summary>The closed <see cref="ICalculationDefinition{TInput, TResult}"/> interface the definition implements.</summary>
    public Type DefinitionInterface => DefinitionType.GetInterfaces()
        .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICalculationDefinition<,>));

    /// <summary>The input record type.</summary>
    public Type InputType => DefinitionInterface.GetGenericArguments()[0];

    /// <summary>The result record type.</summary>
    public Type ResultType => DefinitionInterface.GetGenericArguments()[1];
}

/// <summary>
/// Every product calculation, described for a catalogue and a generated
/// form: id, title, category, method reference, and every input with its
/// label, unit, limits, meaning and, where it comes from the material
/// record, which property.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a registry rather than reflection.</b> A form built from an input
/// record's constructor alone can name a parameter and know its dimension;
/// it cannot say what the parameter means, what limits apply, which unit
/// an engineer would expect to type, or which material property it is.
/// Those are engineering facts and they live here, once, beside the
/// definitions they describe. `WP 21.7A` wrote the eleven modules'
/// descriptors so `WP 21.7B` (the dynamic calculator surface the Product
/// Owner wants) can generate its forms from data; `WP 21.7B` added the
/// five original definitions so that surface lists all sixteen.
/// </para>
/// <para>
/// <b>Kept honest by a test.</b> Every descriptor's input names are asserted
/// against the definition's own input record, every dimension against a
/// registered unit catalogue, and every material property against the
/// vocabulary and the input's dimension, so nothing here can drift.
/// </para>
/// </remarks>
public static class CalculationModuleDescriptors
{
    private const string Docs = "docs/engineering/calculations/";

    /// <summary>Every module descriptor, in registration order: the five original definitions, then the eleven `WP 21.7A` modules.</summary>
    public static IReadOnlyList<CalculationModuleDescriptor> All { get; } =
    [
        new(BoltShearCapacityCalculationDefinition.Id, "Bolt shear capacity", "Structural",
            "Allowable shear capacity of a bolted joint from the shear-plane area, the ultimate shear strength and a safety factor: the standard single- and double-shear hand check.",
            null, typeof(BoltShearCapacityCalculationDefinition),
            [
                Q("Diameter", "Bolt diameter", nameof(Length), "mm", "> 0", "The bolt's nominal shank diameter."),
                Q("UltimateShearStrength", "Ultimate shear strength", nameof(Pressure), "MPa", "> 0", "The bolt material's ultimate shear strength."),
                Number("ShearPlanes", "Shear planes", "1 or 2", "1 for single shear, 2 for double shear."),
                Number("SafetyFactor", "Safety factor", ">= 1", "The factor the allowable capacity is derived under."),
            ]),

        new(BeamBendingStressCalculationDefinition.Id, "Beam bending stress (cantilever, rectangular section)", "Structural",
            "Elastic bending stress at the fixed end of a rectangular cantilever under an end point load, sigma = 6M/(b h^2), against an allowable.",
            null, typeof(BeamBendingStressCalculationDefinition),
            [
                Q("AppliedLoad", "Applied load", nameof(Force), "N", "Any", "The end point load."),
                Q("CantileverLength", "Cantilever length", nameof(Length), "mm", "Any", "Fixed end to the load."),
                Q("SectionWidth", "Section width b", nameof(Length), "mm", "> 0", "The rectangular section's width."),
                Q("SectionHeight", "Section height h", nameof(Length), "mm", "> 0", "The rectangular section's height, in the plane of bending."),
                Q("AllowableBendingStress", "Allowable bending stress", nameof(Pressure), "MPa", "Any", "The allowable the computed stress is compared against.", materialProperty: MaterialPropertyNames.YieldStrength),
            ]),

        new(BearingLoadCapacityCalculationDefinition.Id, "Bearing load capacity at a hole", "Structural",
            "Allowable bearing load at a bolted or pinned hole from the projected bearing area (diameter times thickness), a bearing strength and a safety factor.",
            null, typeof(BearingLoadCapacityCalculationDefinition),
            [
                Q("HoleDiameter", "Hole diameter", nameof(Length), "mm", "> 0", "The hole diameter."),
                Q("PlateThickness", "Plate thickness", nameof(Length), "mm", "> 0", "The bearing plate thickness."),
                Q("BearingStrength", "Bearing strength", nameof(Pressure), "MPa", "Any", "The plate material's ultimate bearing strength."),
                Number("SafetyFactor", "Safety factor", ">= 1", "The factor the allowable capacity is derived under."),
            ]),

        new(PressureVesselWallThicknessCalculationDefinition.Id, "Thin-wall pressure vessel wall thickness", "Pressure Systems",
            "Minimum wall thickness of a thin-walled cylindrical vessel under internal pressure, t = P R / (S E - 0.6 P), scaled by a design safety factor.",
            null, typeof(PressureVesselWallThicknessCalculationDefinition),
            [
                Q("InternalPressure", "Internal pressure", nameof(Pressure), "MPa", "S E must exceed 0.6 P", "The design internal pressure."),
                Q("InnerRadius", "Inner radius", nameof(Length), "mm", "Any", "The shell's inner radius."),
                Q("AllowableStress", "Allowable stress", nameof(Pressure), "MPa", "S E must exceed 0.6 P", "The shell material's allowable stress.", materialProperty: MaterialPropertyNames.YieldStrength),
                Number("JointEfficiency", "Joint efficiency E", "0 to 1", "The weld joint efficiency."),
                Number("SafetyFactor", "Safety factor", ">= 1", "The design factor the minimum thickness is scaled by."),
            ]),

        new(MaterialSelectionMarginCalculationDefinition.Id, "Material selection margin", "Materials",
            "The ratio of a candidate material's allowable stress to an applied stress: a screening check, not a stress analysis.",
            null, typeof(MaterialSelectionMarginCalculationDefinition),
            [
                Text("MaterialId", "Material record id", "Must be named", "The candidate material's registered record id."),
                Q("MaterialAllowableStress", "Material allowable stress", nameof(Pressure), "MPa", "Any", "The candidate material's allowable stress.", materialProperty: MaterialPropertyNames.YieldStrength),
                Q("AppliedStress", "Applied stress", nameof(Pressure), "MPa", "> 0", "The stress the candidate would be subjected to."),
            ]),

        new(BeamDeflectionCalculationDefinition.Id, "Beam bending and deflection", "Structural",
            "Euler-Bernoulli beam theory; the closed forms tabulated in Roark's Formulas for Stress and Strain (Table 8.1) and Gere & Goodno, Mechanics of Materials (Appendix G).",
            Docs + "calc.beam-deflection.md", typeof(BeamDeflectionCalculationDefinition),
            [
                Reference("MaterialPin", "Material record", "The released material record the modulus comes from."),
                Choice("Support", "Support", "Simply supported or cantilever.", nameof(BeamSupport.SimplySupported), nameof(BeamSupport.Cantilever)),
                Choice("Loading", "Loading", "A point load, or a uniformly distributed load entered as its total.", nameof(BeamLoading.PointLoad), nameof(BeamLoading.UniformlyDistributed)),
                Q("Load", "Load W", nameof(Force), "kN", "> 0", "The point load, or the total of the distributed load."),
                Q("Span", "Span L", nameof(Length), "mm", "> 0; at least ten times the depth (twice the extreme fibre distance)", "The span, or the cantilever length."),
                Q("YoungsModulus", "Young's modulus E", nameof(Pressure), "GPa", "> 0; from the material record", "The material's modulus of elasticity.", materialProperty: MaterialPropertyNames.YoungsModulus),
                Q("SecondMomentOfArea", "Second moment of area I", nameof(SecondMomentOfArea), "mm^4", "> 0", "About the bending axis."),
                Q("ExtremeFibreDistance", "Extreme fibre distance c", nameof(Length), "mm", "> 0", "Neutral axis to the most stressed fibre."),
                Q("AllowableBendingStress", "Allowable bending stress", nameof(Pressure), "MPa", "> 0", "The stress the maximum bending stress is compared against.", materialProperty: MaterialPropertyNames.YieldStrength),
                Q("DeflectionLimit", "Deflection limit", nameof(Length), "mm", "> 0", "The deflection the maximum deflection is compared against, for example span/250."),
            ]),

        new(BoltedJointPreloadCalculationDefinition.Id, "Bolted joint preload and clamp force", "Fasteners",
            "The joint-diagram method: Shigley's Mechanical Engineering Design (tension joints, the external load); VDI 2230 in its fuller form.",
            Docs + "calc.bolted-joint-preload.md", typeof(BoltedJointPreloadCalculationDefinition),
            [
                Reference("FastenerPin", "Fastener record", "The fastener record the proof strength comes from, where one exists.", optional: true),
                Text("FastenerGrade", "Fastener grade", "Must be named", "The property class the proof strength belongs to, for example ISO 898-1 class 8.8."),
                Q("Preload", "Preload F_i", nameof(Force), "N", "> 0 and below the proof load", "The assembly preload in the bolt."),
                Q("ExternalLoad", "External load P", nameof(Force), "kN", ">= 0 (tensile)", "The external tensile load on the joint."),
                Q("BoltStiffness", "Bolt stiffness k_b", nameof(Stiffness), "kN/mm", "> 0", "The bolt's axial stiffness."),
                Q("MemberStiffness", "Member stiffness k_m", nameof(Stiffness), "kN/mm", "> 0", "The clamped members' axial stiffness."),
                Q("TensileStressArea", "Tensile stress area A_t", nameof(Area), "mm²", "> 0", "The thread's tensile stress area."),
                Q("ProofStrength", "Proof strength S_p", nameof(Pressure), "MPa", "> 0", "The fastener grade's proof strength (600 MPa for class 8.8, 830 MPa for class 10.9)."),
            ]),

        new(BoltGroupEccentricShearCalculationDefinition.Id, "Bolt group under eccentric in-plane load", "Fasteners",
            "The elastic (vector) method: Shigley's Mechanical Engineering Design (shear joints with eccentric loading); AISC Steel Construction Manual, Part 7.",
            Docs + "calc.bolt-group-eccentric-shear.md", typeof(BoltGroupEccentricShearCalculationDefinition),
            [
                Text("FastenerGrade", "Fastener grade", "Must be named", "The fastener the allowable shear belongs to, for example ISO 898-1 class 8.8 M16."),
                List("Bolts", "Bolt positions", "At least one; no two coincident", "One bolt per line: its x and y with units, for example '75 mm, 50 mm'.", "X", "Y"),
                Q("LoadX", "Load, x component", nameof(Force), "kN", "Not both components zero", "The in-plane load's x component."),
                Q("LoadY", "Load, y component", nameof(Force), "kN", "Not both components zero", "The in-plane load's y component."),
                Q("LoadPointX", "Load point, x", nameof(Length), "mm", "Any", "Where the load acts, x."),
                Q("LoadPointY", "Load point, y", nameof(Length), "mm", "Any", "Where the load acts, y."),
                Q("AllowableShearPerBolt", "Allowable shear per bolt", nameof(Force), "kN", "> 0", "The allowable shear force on one bolt."),
            ]),

        new(FilletWeldThroatStressCalculationDefinition.Id, "Fillet weld throat stress", "Welds",
            "EN 1993-1-8 clause 4.5.3.3, the simplified method, with the correlation factor of clause 4.5.3.2 and the limits of clauses 4.5.1 and 4.5.2.",
            Docs + "calc.fillet-weld-throat-stress.md", typeof(FilletWeldThroatStressCalculationDefinition),
            [
                Reference("MaterialPin", "Material record (weaker part)", "The released material record of the weaker part joined."),
                Q("ParallelForce", "Force along the weld", nameof(Force), "kN", "Not all three components zero", "The force component along the weld axis."),
                Q("TransverseForce", "Force across the weld", nameof(Force), "kN", "Not all three components zero", "The force component across the weld, in the plate plane."),
                Q("NormalForce", "Force normal to the plate", nameof(Force), "kN", "Not all three components zero", "The force component normal to the plate."),
                Q("EffectiveLength", "Effective length L", nameof(Length), "mm", ">= the larger of 30 mm and six throats", "The weld's total effective length."),
                Q("ThroatThickness", "Throat a", nameof(Length), "mm", ">= 3 mm", "The weld throat."),
                Q("UltimateStrength", "Ultimate strength f_u", nameof(Pressure), "MPa", "> 0; from the material record", "The weaker part's ultimate strength.", materialProperty: MaterialPropertyNames.UltimateTensileStrength),
                Number("CorrelationFactor", "Correlation factor β_w", "> 0", "0.8 for S235, 0.85 for S275, 0.9 for S355, 1.0 for S420 and S460."),
                Number("PartialFactor", "Partial factor γ_M2", ">= 1", "1.25 recommended."),
            ]),

        new(LiftingLugPinJointCalculationDefinition.Id, "Lifting lug and pin joint", "Lifting",
            "The classical pinned-lug hand check (net section, bearing, tear-out, pin shear and pin bending), as in the pinned-connection provisions of ASME BTH-1 chapter 3.",
            Docs + "calc.lifting-lug-pin-joint.md", typeof(LiftingLugPinJointCalculationDefinition),
            [
                Reference("LugMaterialPin", "Lug material record", "The released material record the lug allowables were derived from."),
                Reference("PinMaterialPin", "Pin material record", "The released material record the pin allowables were derived from."),
                Q("Load", "Load P", nameof(Force), "kN", "> 0", "The load through the pin."),
                Q("LugThickness", "Lug thickness t", nameof(Length), "mm", "> 0", "The lug plate thickness."),
                Q("LugWidth", "Lug width W", nameof(Length), "mm", "> hole diameter", "The lug width across the hole."),
                Q("HoleDiameter", "Hole diameter d_h", nameof(Length), "mm", "> 0", "The hole diameter."),
                Q("PinDiameter", "Pin diameter d_p", nameof(Length), "mm", "> 0, <= hole, >= 0.9 x hole", "The pin diameter."),
                Q("EdgeDistance", "Edge distance a", nameof(Length), "mm", "> 0", "Hole edge to lug end, in the load direction."),
                Q("CheekPlateThickness", "Cheek plate thickness t_s", nameof(Length), "mm", "> 0", "The thickness of each cheek plate the pin bears on."),
                Q("Clearance", "Clearance g", nameof(Length), "mm", ">= 0", "The gap between the lug face and each cheek plate."),
                Q("AllowableTensileStress", "Lug allowable tension", nameof(Pressure), "MPa", "> 0", "The lug allowable in tension."),
                Q("AllowableBearingStress", "Lug allowable bearing", nameof(Pressure), "MPa", "> 0", "The lug allowable in bearing."),
                Q("AllowableShearStress", "Lug allowable shear", nameof(Pressure), "MPa", "> 0", "The lug allowable in shear."),
                Q("PinAllowableBendingStress", "Pin allowable bending", nameof(Pressure), "MPa", "> 0", "The pin allowable in bending."),
                Q("PinAllowableShearStress", "Pin allowable shear", nameof(Pressure), "MPa", "> 0", "The pin allowable in shear."),
            ]),

        new(ColumnBucklingCalculationDefinition.Id, "Column buckling (Euler with Perry-Robertson)", "Structural",
            "Euler's critical stress with the Perry-Robertson correction in the form of BS 5950-1:2000 Annex C; the slenderness limit of clause 4.7.3.2.",
            Docs + "calc.column-buckling.md", typeof(ColumnBucklingCalculationDefinition),
            [
                Reference("MaterialPin", "Material record", "The released material record the modulus and yield strength come from."),
                Q("EffectiveLength", "Effective length L_E", nameof(Length), "mm", "> 0; slenderness <= 180", "The strut's effective length."),
                Q("Area", "Area A", nameof(Area), "mm²", "> 0", "The section area."),
                Q("SecondMomentOfArea", "Second moment of area I", nameof(SecondMomentOfArea), "mm^4", "> 0", "About the weaker axis."),
                Q("YoungsModulus", "Young's modulus E", nameof(Pressure), "GPa", "> 0; from the material record", "205 GPa for steel in BS 5950.", materialProperty: MaterialPropertyNames.YoungsModulus),
                Q("YieldStrength", "Yield strength p_y", nameof(Pressure), "MPa", "> 0; from the material record", "The design strength.", materialProperty: MaterialPropertyNames.YieldStrength),
                Number("RobertsonConstant", "Robertson constant a", "> 0", "2.0 for curve a, 3.5 for b, 5.5 for c, 8.0 for d."),
                Q("AppliedLoad", "Applied load P", nameof(Force), "kN", ">= 0", "The axial compression the resistance is compared against."),
            ]),

        new(ShaftCombinedStressCalculationDefinition.Id, "Shaft under combined torsion and bending", "Machine Elements",
            "Elastic surface stresses of a solid round shaft combined by the maximum-shear-stress and distortion-energy theories: Shigley's Mechanical Engineering Design (static shaft design and the static failure theories).",
            Docs + "calc.shaft-combined-stress.md", typeof(ShaftCombinedStressCalculationDefinition),
            [
                Reference("MaterialPin", "Material record", "The released material record the yield strength comes from."),
                Q("Diameter", "Diameter d", nameof(Length), "mm", "> 0", "The solid shaft diameter at the section."),
                Q("BendingMoment", "Bending moment M", nameof(Torque), "N.m", ">= 0; not both loads zero", "The bending moment at the section."),
                Q("Torque", "Torque T", nameof(Torque), "N.m", ">= 0; not both loads zero", "The torque at the section."),
                Q("YieldStrength", "Yield strength S_y", nameof(Pressure), "MPa", "> 0; from the material record", "The material's yield strength.", materialProperty: MaterialPropertyNames.YieldStrength),
                Number("BendingStressConcentrationFactor", "Bending stress concentration K_t", ">= 1", "1 for none."),
                Number("TorsionalStressConcentrationFactor", "Torsional stress concentration K_ts", ">= 1", "1 for none."),
                Number("RequiredSafetyFactor", "Required factor of safety", ">= 1", "The factor the lower of the two theories must reach."),
            ]),

        new(BearingRatingLifeCalculationDefinition.Id, "Rolling bearing rating life L10", "Machine Elements",
            "ISO 281 basic rating life with the reliability factor a1; the life modification factor a_ISO is not applied.",
            Docs + "calc.bearing-rating-life.md", typeof(BearingRatingLifeCalculationDefinition),
            [
                Reference("BearingPin", "Bearing record", "The bearing record the dynamic load rating comes from, where one exists.", optional: true),
                Text("BearingDesignation", "Bearing designation", "Must be named", "The bearing the rating belongs to, for example 6208."),
                Choice("BearingType", "Bearing type", "Ball (exponent 3) or roller (exponent 10/3).", nameof(RollingBearingType.Ball), nameof(RollingBearingType.Roller)),
                Q("BasicDynamicLoadRating", "Dynamic load rating C", nameof(Force), "kN", "> 0; from the bearing record", "The basic dynamic load rating."),
                Q("RadialLoad", "Radial load F_r", nameof(Force), "kN", ">= 0", "The radial load."),
                Q("AxialLoad", "Axial load F_a", nameof(Force), "kN", ">= 0", "The axial load."),
                Number("RadialFactor", "Radial factor X", ">= 0", "From the bearing catalogue."),
                Number("AxialFactor", "Axial factor Y", ">= 0", "From the bearing catalogue."),
                Q("Speed", "Speed n", nameof(RotationalSpeed), "r/min", "> 0", "The running speed."),
                Number("ReliabilityFactor", "Reliability factor a1", "0 < a1 <= 1", "1 at 90 %, 0.64 at 95 %, 0.55 at 96 %, 0.47 at 97 %, 0.37 at 98 %, 0.25 at 99 %."),
                Q("RequiredLife", "Required life", nameof(Duration), "h", ">= 0; zero for no criterion", "The life the modified life is compared against."),
            ]),

        new(ThickWalledCylinderCalculationDefinition.Id, "Thick-walled cylinder stresses (Lame)", "Pressure Systems",
            "Lame's equations for a thick-walled cylinder under internal and external pressure: Timoshenko, Strength of Materials Part II; Roark's Formulas for Stress and Strain.",
            Docs + "calc.thick-walled-cylinder.md", typeof(ThickWalledCylinderCalculationDefinition),
            [
                Reference("MaterialPin", "Material record", "The released material record the allowable stress was derived from."),
                Q("InnerRadius", "Inner radius a", nameof(Length), "mm", "> 0", "The bore radius."),
                Q("OuterRadius", "Outer radius b", nameof(Length), "mm", "> inner radius", "The outer radius."),
                Q("InternalPressure", "Internal pressure p_i", nameof(Pressure), "MPa", ">= 0; not both pressures zero", "The internal gauge pressure."),
                Q("ExternalPressure", "External pressure p_o", nameof(Pressure), "MPa", ">= 0; not both pressures zero", "The external gauge pressure."),
                Boolean("ClosedEnds", "Closed ends", "Whether the ends are closed, so the pressures load the wall axially."),
                Q("AllowableStress", "Allowable stress", nameof(Pressure), "MPa", "> 0", "The stress the maximum von Mises stress is compared against.", materialProperty: MaterialPropertyNames.YieldStrength),
            ]),

        new(ThermalExpansionStressCalculationDefinition.Id, "Thermal expansion and restrained thermal stress", "Thermal",
            "Linear thermal expansion and the restrained-bar compatibility problem: Hibbeler, Mechanics of Materials (axial load, thermal stress); Gere & Goodno (thermal effects).",
            Docs + "calc.thermal-expansion-stress.md", typeof(ThermalExpansionStressCalculationDefinition),
            [
                Reference("MaterialPin", "Material record", "The released material record the modulus and expansion coefficient come from."),
                Q("Length", "Length L", nameof(Length), "mm", "> 0", "The bar length."),
                Q("Area", "Area A", nameof(Area), "mm²", "> 0", "The bar section area."),
                Q("YoungsModulus", "Young's modulus E", nameof(Pressure), "GPa", "> 0; from the material record", "The material's modulus of elasticity.", materialProperty: MaterialPropertyNames.YoungsModulus),
                Q("ExpansionCoefficient", "Expansion coefficient α", nameof(ThermalExpansion), "1/K", "> 0; from the material record", "The coefficient of linear thermal expansion.", materialProperty: MaterialPropertyNames.ThermalExpansionCoefficient),
                Q("TemperatureChange", "Temperature change ΔT", nameof(TemperatureDelta), "K", "Any sign", "Positive for heating."),
                Q("Gap", "Gap g", nameof(Length), "mm", ">= 0", "The clearance that must close before the restraint engages."),
                Q("RestraintStiffness", "Restraint stiffness k_s", nameof(Stiffness), "kN/mm", "> 0 when given; empty for rigid", "The restraint's stiffness.", optional: true),
                Q("AllowableStress", "Allowable stress", nameof(Pressure), "MPa", "> 0", "The stress the stress magnitude is compared against.", materialProperty: MaterialPropertyNames.YieldStrength),
            ]),

        new(FatigueMinerCalculationDefinition.Id, "Fatigue under variable amplitude (S-N with Miner's rule)", "Fatigue",
            "A single-slope Basquin S-N curve with an optional endurance limit and the Palmgren-Miner linear damage sum; the form of the EN 1993-1-9 detail categories at two million cycles.",
            Docs + "calc.fatigue-miner.md", typeof(FatigueMinerCalculationDefinition),
            [
                Reference("MaterialPin", "Material record", "The released material record the reference fatigue strength comes from, where the curve is a material's.", optional: true),
                Text("CurveReference", "S-N curve reference", "Required when no material record is given", "Where the curve comes from, for example EN 1993-1-9 detail category 71."),
                Q("ReferenceStressRange", "Reference stress range", nameof(Pressure), "MPa", "> 0", "The stress range at the reference point of the curve."),
                Number("ReferenceCycles", "Reference cycles", "> 0", "The cycles at the reference point (2 000 000 for a Eurocode detail category)."),
                Number("Slope", "Inverse slope m", "> 0", "3 for welded steel details, 5 for some non-welded."),
                Q("EnduranceLimit", "Endurance limit", nameof(Pressure), "MPa", "> 0 when given; empty for none", "Ranges at or below it cause no damage.", optional: true),
                List("Blocks", "Loading spectrum", "At least one block; range > 0, cycles >= 0", "One block per line: its stress range with a unit and its cycles, for example '100 MPa, 100000'.", "StressRange", "Cycles"),
            ]),
    ];

    /// <summary>The descriptor for <paramref name="calculationId"/>, or <see langword="null"/> where no product calculation has that Id.</summary>
    public static CalculationModuleDescriptor? For(string calculationId) =>
        All.FirstOrDefault(d => string.Equals(d.Id, calculationId, StringComparison.Ordinal));

    private static CalculationInputDescriptor Q(string name, string label, string dimension, string unit, string limits, string description, bool optional = false, string? materialProperty = null) =>
        new(name, label, CalculationInputKind.Quantity, dimension, unit, limits, description, IsOptional: optional, MaterialPropertyName: materialProperty);

    private static CalculationInputDescriptor Number(string name, string label, string limits, string description) =>
        new(name, label, CalculationInputKind.Number, null, null, limits, description);

    private static CalculationInputDescriptor Text(string name, string label, string limits, string description) =>
        new(name, label, CalculationInputKind.Text, null, null, limits, description);

    private static CalculationInputDescriptor Boolean(string name, string label, string description) =>
        new(name, label, CalculationInputKind.Boolean, null, null, "Yes or no", description);

    private static CalculationInputDescriptor Choice(string name, string label, string description, params string[] choices) =>
        new(name, label, CalculationInputKind.Choice, null, null, "One of the choices", description, choices);

    private static CalculationInputDescriptor List(string name, string label, string limits, string description, params string[] fields) =>
        new(name, label, CalculationInputKind.List, null, null, limits, description, fields);

    private static CalculationInputDescriptor Reference(string name, string label, string description, bool optional = false) =>
        new(name, label, CalculationInputKind.Reference, null, null, optional ? "A released reference record, or empty" : "A released reference record", description, IsOptional: optional);
}
