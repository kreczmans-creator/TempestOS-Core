using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Calculations;

/// <summary>
/// The product's engineering calculation catalogue — Bolt, Beam,
/// Bearing, Pressure and Material Selection — five real, if simplified,
/// hand-calculation formulas, registered into the Workspace by
/// <c>Tempest.App.Workspace.Calculations.CalculationsWorkspaceRegistration</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>These are product content, and they live in the domain (`TD-75`
/// phase 1).</b> They depend on nothing but
/// <see cref="ICalculationDefinition{TInput, TResult}"/> and
/// <c>Tempest.Core.UnitsAndQuantities</c> — a calculation catalogue is
/// engineering knowledge, not workspace wiring, so it belongs beside the
/// framework that executes it rather than beside the registration that
/// surfaces it. That also keeps it reachable from the sample harness,
/// which demonstrates these calculations and references only
/// <c>Tempest.Core</c>. They were declared in
/// <c>Tempest.Samples</c> until 2026-08-30 — so the product's entire
/// calculation catalogue shipped inside the sample harness, and removing
/// that harness removed the calculations. The 2026-08-30 Product Gap
/// Reconciliation audit measured the coupling and found it was never a
/// packaging problem: it was product content filed in the wrong assembly.
/// The trivial arithmetic stand-in <c>DoubleLengthCalculationDefinition</c>
/// stays in <c>Tempest.Samples</c>, because that one genuinely is a
/// demonstration.
/// </para>
/// <para>
/// Each definition is
/// a small, stateless, registrable <see cref="ICalculationDefinition{TInput, TResult}"/>,
/// pure in <see cref="ICalculationDefinition{TInput, TResult}.Calculate"/>,
/// declaring its own assumptions/constraints once as
/// <see cref="CalculationMetadata"/>.
/// </summary>
/// <remarks>
/// <b>Safety Factor, disclosed:</b> no dedicated Safety Factor contract
/// exists anywhere in the Engineering Domain or Calculation Framework —
/// every definition below represents its own safety factor (or the
/// derived margin it produces) as a plainly-named
/// <see cref="CalculationIntermediateResult"/>, the framework's own open,
/// generic evidentiary shape, exactly as <see cref="CalculationIntermediateResult.Value"/>'s
/// own XML documentation anticipates ("not constrained to" a
/// <see cref="Quantity{TDimension}"/>). This is a data convention, not a
/// new type.
/// </remarks>
public sealed class BoltShearCapacityCalculationDefinition : ICalculationDefinition<BoltShearCapacityInput, BoltShearCapacityResult>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "calc.bolt-shear-capacity";

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Bolt Shear Capacity",
        Description: "Allowable shear capacity of a bolted joint, from bolt diameter, ultimate shear strength, shear plane count, and a safety factor.",
        Category: "Structural",
        Assumptions:
        [
            new CalculationAssumption("Load is distributed evenly across every shear plane.", "Standard single/double-shear joint assumption."),
            new CalculationAssumption("The bolt is loaded in pure shear, with no bending or prying action.", "Simplified hand-calculation scope."),
        ],
        Constraints:
        [
            new CalculationConstraint("Bolt diameter must be positive."),
            new CalculationConstraint("Safety factor must be at least 1.0."),
        ]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException"><paramref name="input"/>'s own diameter is not positive, or its safety factor is below 1.0.</exception>
    public BoltShearCapacityResult Calculate(BoltShearCapacityInput input, CalculationContext context)
    {
        var diameterMm = input.Diameter.ConvertTo(LengthUnits.Millimetre).Value;
        var isPositiveDiameter = diameterMm > 0;
        context.RecordConstraintCheck("Bolt diameter must be positive.", isPositiveDiameter, $"Diameter was {diameterMm:0.###} mm.");
        if (!isPositiveDiameter)
            throw new CalculationInputInvalidException($"Bolt diameter must be positive; received {diameterMm} mm.");

        var isValidSafetyFactor = input.SafetyFactor >= 1.0;
        context.RecordConstraintCheck("Safety factor must be at least 1.0.", isValidSafetyFactor, $"Safety factor was {input.SafetyFactor:0.###}.");
        if (!isValidSafetyFactor)
            throw new CalculationInputInvalidException($"Safety factor must be at least 1.0; received {input.SafetyFactor}.");

        var shearPlaneAreaMm2 = Math.PI / 4.0 * diameterMm * diameterMm;
        var totalShearAreaMm2 = shearPlaneAreaMm2 * input.ShearPlanes;
        var ultimateShearStrengthMPa = input.UltimateShearStrength.ConvertTo(PressureUnits.Megapascal).Value;

        // 1 MPa == 1 N/mm^2, so mm^2 x MPa yields N directly.
        var ultimateCapacityN = totalShearAreaMm2 * ultimateShearStrengthMPa;
        var allowableCapacityN = ultimateCapacityN / input.SafetyFactor;

        context.RecordIntermediate("Single Shear Plane Area", new Quantity<Area>(shearPlaneAreaMm2, AreaUnits.SquareMillimetre));
        context.RecordIntermediate("Total Shear Area", new Quantity<Area>(totalShearAreaMm2, AreaUnits.SquareMillimetre));
        context.RecordIntermediate("Ultimate Shear Capacity", new Quantity<Force>(ultimateCapacityN, ForceUnits.Newton));
        context.RecordIntermediate("Safety Factor", input.SafetyFactor);

        return new BoltShearCapacityResult(new Quantity<Force>(allowableCapacityN, ForceUnits.Newton));
    }
}

/// <param name="Diameter">The bolt's own nominal shank diameter.</param>
/// <param name="UltimateShearStrength">The bolt material's own ultimate shear strength.</param>
/// <param name="ShearPlanes">The number of shear planes carrying the joint's own load (1 for single shear, 2 for double shear).</param>
/// <param name="SafetyFactor">The safety factor the allowable capacity is derived under (must be at least 1.0).</param>
public sealed record BoltShearCapacityInput(Quantity<Length> Diameter, Quantity<Pressure> UltimateShearStrength, int ShearPlanes, double SafetyFactor);

/// <param name="AllowableShearCapacity">The joint's own allowable shear capacity, after applying <see cref="BoltShearCapacityInput.SafetyFactor"/>.</param>
public sealed record BoltShearCapacityResult(Quantity<Force> AllowableShearCapacity);

/// <summary>
/// Bending stress in a rectangular-section cantilever beam under a single
/// end point load, and the resulting margin against an allowable stress —
/// <c>σ = 6M / (b·h²)</c>, a standard elastic bending-stress hand
/// calculation. See <see cref="BoltShearCapacityCalculationDefinition"/>'s
/// own remarks for this file's shared Safety Factor disclosure.
/// </summary>
public sealed class BeamBendingStressCalculationDefinition : ICalculationDefinition<BeamBendingStressInput, BeamBendingStressResult>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "calc.beam-bending-stress";

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Beam Bending Stress",
        Description: "Elastic bending stress at the fixed end of a rectangular-section cantilever beam under a single end point load, compared against an allowable stress.",
        Category: "Structural",
        Assumptions:
        [
            new CalculationAssumption("The beam behaves as a linear-elastic, prismatic cantilever.", "Standard elastic bending theory scope."),
            new CalculationAssumption("The section is solid and rectangular, with no stress concentrations considered.", "Simplified hand-calculation scope."),
        ],
        Constraints:
        [
            new CalculationConstraint("Section width and height must both be positive."),
            new CalculationConstraint("Computed bending stress should not exceed the allowable stress (advisory — a Conditional result is still returned)."),
        ]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException"><paramref name="input"/>'s own section width or height is not positive.</exception>
    public BeamBendingStressResult Calculate(BeamBendingStressInput input, CalculationContext context)
    {
        var widthMm = input.SectionWidth.ConvertTo(LengthUnits.Millimetre).Value;
        var heightMm = input.SectionHeight.ConvertTo(LengthUnits.Millimetre).Value;
        var isPositiveSection = widthMm > 0 && heightMm > 0;
        context.RecordConstraintCheck("Section width and height must both be positive.", isPositiveSection, $"Width {widthMm:0.###} mm, height {heightMm:0.###} mm.");
        if (!isPositiveSection)
            throw new CalculationInputInvalidException($"Section width and height must both be positive; received {widthMm} mm x {heightMm} mm.");

        var loadN = input.AppliedLoad.ConvertTo(ForceUnits.Newton).Value;
        var lengthMm = input.CantileverLength.ConvertTo(LengthUnits.Millimetre).Value;
        var momentNmm = loadN * lengthMm;

        var bendingStressMPa = 6.0 * momentNmm / (widthMm * heightMm * heightMm);
        var allowableStressMPa = input.AllowableBendingStress.ConvertTo(PressureUnits.Megapascal).Value;
        var isWithinAllowable = bendingStressMPa <= allowableStressMPa;
        var marginOfSafety = allowableStressMPa / bendingStressMPa - 1.0;

        context.RecordIntermediate("Bending Moment", $"{momentNmm:0.##} N·mm");
        context.RecordIntermediate("Safety Factor (Margin of Safety)", marginOfSafety);
        context.RecordConstraintCheck(
            "Computed bending stress should not exceed the allowable stress (advisory — a Conditional result is still returned).",
            isWithinAllowable,
            $"Computed {bendingStressMPa:0.##} MPa vs allowable {allowableStressMPa:0.##} MPa.");

        return new BeamBendingStressResult(new Quantity<Pressure>(bendingStressMPa, PressureUnits.Megapascal), marginOfSafety);
    }
}

/// <param name="AppliedLoad">The single end point load applied to the free end of the cantilever.</param>
/// <param name="CantileverLength">The distance from the fixed end to the applied load.</param>
/// <param name="SectionWidth">The rectangular section's own width.</param>
/// <param name="SectionHeight">The rectangular section's own height (in the plane of bending).</param>
/// <param name="AllowableBendingStress">The material/design allowable bending stress the computed stress is checked against.</param>
public sealed record BeamBendingStressInput(
    Quantity<Force> AppliedLoad, Quantity<Length> CantileverLength, Quantity<Length> SectionWidth, Quantity<Length> SectionHeight, Quantity<Pressure> AllowableBendingStress);

/// <param name="BendingStress">The computed elastic bending stress at the fixed end.</param>
/// <param name="MarginOfSafety">The margin against <see cref="BeamBendingStressInput.AllowableBendingStress"/> — negative when the section is overstressed.</param>
public sealed record BeamBendingStressResult(Quantity<Pressure> BendingStress, double MarginOfSafety);

/// <summary>
/// Allowable bearing load at a bolted/pinned hole, from projected bearing
/// area (hole diameter × plate thickness), material bearing strength, and
/// a safety factor. See <see cref="BoltShearCapacityCalculationDefinition"/>'s
/// own remarks for this file's shared Safety Factor disclosure.
/// </summary>
public sealed class BearingLoadCapacityCalculationDefinition : ICalculationDefinition<BearingLoadCapacityInput, BearingLoadCapacityResult>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "calc.bearing-load-capacity";

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Bearing Load Capacity",
        Description: "Allowable bearing load at a bolted or pinned hole, from projected bearing area, material bearing strength, and a safety factor.",
        Category: "Structural",
        Assumptions:
        [
            new CalculationAssumption("Bearing stress is uniform across the projected area (hole diameter x plate thickness).", "Standard simplified bearing-check assumption."),
        ],
        Constraints:
        [
            new CalculationConstraint("Hole diameter and plate thickness must both be positive."),
            new CalculationConstraint("Safety factor must be at least 1.0."),
        ]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException"><paramref name="input"/>'s own hole diameter/plate thickness is not positive, or its safety factor is below 1.0.</exception>
    public BearingLoadCapacityResult Calculate(BearingLoadCapacityInput input, CalculationContext context)
    {
        var diameterMm = input.HoleDiameter.ConvertTo(LengthUnits.Millimetre).Value;
        var thicknessMm = input.PlateThickness.ConvertTo(LengthUnits.Millimetre).Value;
        var isPositive = diameterMm > 0 && thicknessMm > 0;
        context.RecordConstraintCheck("Hole diameter and plate thickness must both be positive.", isPositive, $"Diameter {diameterMm:0.###} mm, thickness {thicknessMm:0.###} mm.");
        if (!isPositive)
            throw new CalculationInputInvalidException($"Hole diameter and plate thickness must both be positive; received {diameterMm} mm x {thicknessMm} mm.");

        var isValidSafetyFactor = input.SafetyFactor >= 1.0;
        context.RecordConstraintCheck("Safety factor must be at least 1.0.", isValidSafetyFactor, $"Safety factor was {input.SafetyFactor:0.###}.");
        if (!isValidSafetyFactor)
            throw new CalculationInputInvalidException($"Safety factor must be at least 1.0; received {input.SafetyFactor}.");

        var bearingAreaMm2 = diameterMm * thicknessMm;
        var bearingStrengthMPa = input.BearingStrength.ConvertTo(PressureUnits.Megapascal).Value;
        var ultimateCapacityN = bearingAreaMm2 * bearingStrengthMPa;
        var allowableCapacityN = ultimateCapacityN / input.SafetyFactor;

        context.RecordIntermediate("Projected Bearing Area", new Quantity<Area>(bearingAreaMm2, AreaUnits.SquareMillimetre));
        context.RecordIntermediate("Ultimate Bearing Capacity", new Quantity<Force>(ultimateCapacityN, ForceUnits.Newton));
        context.RecordIntermediate("Safety Factor", input.SafetyFactor);

        return new BearingLoadCapacityResult(new Quantity<Force>(allowableCapacityN, ForceUnits.Newton));
    }
}

/// <param name="HoleDiameter">The bolted/pinned hole's own diameter.</param>
/// <param name="PlateThickness">The bearing plate's own thickness.</param>
/// <param name="BearingStrength">The plate material's own ultimate bearing strength.</param>
/// <param name="SafetyFactor">The safety factor the allowable capacity is derived under (must be at least 1.0).</param>
public sealed record BearingLoadCapacityInput(Quantity<Length> HoleDiameter, Quantity<Length> PlateThickness, Quantity<Pressure> BearingStrength, double SafetyFactor);

/// <param name="AllowableBearingCapacity">The joint's own allowable bearing capacity, after applying <see cref="BearingLoadCapacityInput.SafetyFactor"/>.</param>
public sealed record BearingLoadCapacityResult(Quantity<Force> AllowableBearingCapacity);

/// <summary>
/// Minimum required wall thickness of a thin-walled cylindrical pressure
/// vessel under internal pressure — the standard ASME-form thin-wall
/// formula <c>t = P·R / (S·E − 0.6·P)</c>, then scaled up by a design
/// safety factor. See <see cref="BoltShearCapacityCalculationDefinition"/>'s
/// own remarks for this file's shared Safety Factor disclosure.
/// </summary>
public sealed class PressureVesselWallThicknessCalculationDefinition
    : ICalculationDefinition<PressureVesselWallThicknessInput, PressureVesselWallThicknessResult>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "calc.pressure-vessel-wall-thickness";

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Pressure Vessel Wall Thickness",
        Description: "Minimum required wall thickness of a thin-walled cylindrical pressure vessel under internal pressure, scaled by a design safety factor.",
        Category: "Pressure Systems",
        Assumptions:
        [
            new CalculationAssumption("The vessel is thin-walled — wall thickness is small relative to inner radius.", "Standard thin-wall pressure vessel scope; a thick-wall (Lamé) formula is not used."),
            new CalculationAssumption("The weld joint efficiency uniformly reduces the allowable stress across the whole shell.", "Standard ASME-form simplification."),
        ],
        Constraints:
        [
            new CalculationConstraint("Allowable stress x joint efficiency must exceed 0.6 x internal pressure (thin-wall formula validity)."),
        ]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException">The thin-wall formula's own denominator is not positive for <paramref name="input"/>.</exception>
    public PressureVesselWallThicknessResult Calculate(PressureVesselWallThicknessInput input, CalculationContext context)
    {
        var pressureMPa = input.InternalPressure.ConvertTo(PressureUnits.Megapascal).Value;
        var radiusMm = input.InnerRadius.ConvertTo(LengthUnits.Millimetre).Value;
        var allowableStressMPa = input.AllowableStress.ConvertTo(PressureUnits.Megapascal).Value;

        var denominator = allowableStressMPa * input.JointEfficiency - 0.6 * pressureMPa;
        var isValid = denominator > 0;
        context.RecordConstraintCheck(
            "Allowable stress x joint efficiency must exceed 0.6 x internal pressure (thin-wall formula validity).",
            isValid, $"S·E = {allowableStressMPa * input.JointEfficiency:0.###} MPa, 0.6·P = {0.6 * pressureMPa:0.###} MPa.");
        if (!isValid)
            throw new CalculationInputInvalidException("The thin-wall pressure vessel formula's own denominator (S·E - 0.6·P) is not positive for the given input.");

        var minimumThicknessMm = pressureMPa * radiusMm / denominator;
        var designThicknessMm = minimumThicknessMm * input.SafetyFactor;

        context.RecordIntermediate("Minimum Required Thickness", new Quantity<Length>(minimumThicknessMm, LengthUnits.Millimetre));
        context.RecordIntermediate("Safety Factor", input.SafetyFactor);

        return new PressureVesselWallThicknessResult(new Quantity<Length>(designThicknessMm, LengthUnits.Millimetre));
    }
}

/// <param name="InternalPressure">The vessel's own design internal pressure.</param>
/// <param name="InnerRadius">The vessel shell's own inner radius.</param>
/// <param name="AllowableStress">The shell material's own allowable stress.</param>
/// <param name="JointEfficiency">The weld joint efficiency factor (0 to 1).</param>
/// <param name="SafetyFactor">The design safety factor the minimum thickness is scaled by.</param>
public sealed record PressureVesselWallThicknessInput(
    Quantity<Pressure> InternalPressure, Quantity<Length> InnerRadius, Quantity<Pressure> AllowableStress, double JointEfficiency, double SafetyFactor);

/// <param name="DesignThickness">The vessel's own design wall thickness, after applying <see cref="PressureVesselWallThicknessInput.SafetyFactor"/>.</param>
public sealed record PressureVesselWallThicknessResult(Quantity<Length> DesignThickness);

/// <summary>
/// A material selection margin check — the ratio of a registered
/// material's own allowable stress to an applied stress. References the
/// material by Id via <see cref="CalculationContext.ReferenceMaterial"/>
/// only (never a live catalogue lookup — <see cref="ICalculationDefinition{TInput, TResult}.Calculate"/>
/// must stay pure/I/O-free, mirroring <see cref="DoubleLengthCalculationDefinition"/>'s
/// own identical, already-established pattern: the caller resolves the
/// material's own allowable stress from <see cref="Tempest.Core.Materials.IMaterialCatalog"/>
/// beforehand and passes it as plain input).
/// </summary>
public sealed class MaterialSelectionMarginCalculationDefinition : ICalculationDefinition<MaterialSelectionMarginInput, MaterialSelectionMarginResult>
{
    /// <summary>The Id this calculation is registered under.</summary>
    public const string Id = "calc.material-selection-margin";

    /// <inheritdoc />
    public string CalculationId => Id;

    /// <inheritdoc />
    public CalculationMetadata Metadata { get; } = new(
        Name: "Material Selection Margin",
        Description: "The margin between a candidate material's own allowable stress and an applied stress — a material selection screening check.",
        Category: "Materials",
        Assumptions:
        [
            new CalculationAssumption("The applied stress already reflects every load case and load factor relevant to the candidate material.", "Screening-level check, not a full stress analysis."),
        ],
        Constraints:
        [
            new CalculationConstraint("Applied stress must be positive."),
        ]);

    /// <inheritdoc />
    /// <exception cref="CalculationInputInvalidException"><paramref name="input"/>'s own applied stress is not positive.</exception>
    public MaterialSelectionMarginResult Calculate(MaterialSelectionMarginInput input, CalculationContext context)
    {
        var appliedMPa = input.AppliedStress.ConvertTo(PressureUnits.Megapascal).Value;
        var isPositive = appliedMPa > 0;
        context.RecordConstraintCheck("Applied stress must be positive.", isPositive, $"Applied stress was {appliedMPa:0.###} MPa.");
        if (!isPositive)
            throw new CalculationInputInvalidException($"Applied stress must be positive; received {appliedMPa} MPa.");

        var allowableMPa = input.MaterialAllowableStress.ConvertTo(PressureUnits.Megapascal).Value;
        var marginRatio = allowableMPa / appliedMPa;

        context.ReferenceMaterial(input.MaterialId);
        context.RecordIntermediate("Safety Factor (Margin Ratio)", marginRatio);
        context.RecordConstraintCheck(
            "Applied stress should not exceed the material's own allowable stress (advisory — a Conditional result is still returned).",
            marginRatio >= 1.0, $"Allowable {allowableMPa:0.##} MPa vs applied {appliedMPa:0.##} MPa.");

        return new MaterialSelectionMarginResult(marginRatio);
    }
}

/// <param name="MaterialId">The candidate material's own registered Id (<see cref="Tempest.Core.Materials.IMaterialCatalog"/>).</param>
/// <param name="MaterialAllowableStress">The candidate material's own allowable stress, resolved by the caller before execution.</param>
/// <param name="AppliedStress">The stress the candidate material would be subjected to.</param>
public sealed record MaterialSelectionMarginInput(string MaterialId, Quantity<Pressure> MaterialAllowableStress, Quantity<Pressure> AppliedStress);

/// <param name="MarginRatio">The ratio of allowable to applied stress — at least 1.0 for an acceptable margin.</param>
public sealed record MaterialSelectionMarginResult(double MarginRatio);
