using Tempest.Core.Calculations;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Modules;
using Tempest.Core.Requirements;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module demonstrating `WP 9.2A`'s
/// Engineering Calculations Workspace — real, representative engineering
/// calculations for the Engineering Workspace's own Calculations area,
/// Engineering Cockpit KPIs, and Digital Thread to present, per this Work
/// Package's own explicit "meaningful engineering data rather than
/// placeholders" requirement.
/// </summary>
/// <remarks>
/// <para>
/// Registers all five representative Calculation Templates
/// (Bolt/Beam/Bearing/Pressure/Material Selection) with
/// <see cref="ICalculationEngine"/>, mirroring
/// <see cref="CalculationSampleModule"/>'s own established "a module owns
/// registering its own definitions" precedent, then builds five real
/// <see cref="Calculation"/> Domain objects plus one
/// <see cref="CalculationSet"/> ("Wing Attach Bolt Calculations",
/// grouping the bolt shear and bearing checks). Every Calculation is
/// executed at least once through the real engine, each execution linked
/// back to its own Domain object via <c>"calculatedBy"</c> (never a new
/// relationship kind — already mapped to
/// <see cref="RelationshipCategory.Calculation"/> by
/// <c>RelationshipKindCategoryMap</c>). One calculation chain
/// (<c>"basedOnCalculation"</c>: the bearing check based on the bolt shear
/// check) and two Digital Thread cross-discipline links (a Mechanical
/// Assembly/Part <c>"calculatedBy"</c> a Calculation, a Requirement
/// <c>"calculatedBy"</c> a Calculation) round out the traceability this
/// Work Package's own scope names. A mix of lifecycle statuses (Draft,
/// InReview, Approved) and two deliberately real, honest edge cases:
/// the Material Selection check's own applied stress exceeds its
/// allowable stress (a genuine <see cref="CalculationValidationOutcome.Conditional"/>
/// outcome — the Engineering Cockpit's own "Failed" KPI), and the
/// Pressure Vessel check is revised (<see cref="IHasRevisions.ReviseAsync"/>)
/// after being executed (the Cockpit's own "Out-of-date" KPI).
/// </para>
/// <para>
/// <b>A disclosed, deliberate second cross-sample-module dependency:</b>
/// constructor-injects both <see cref="MechanicalProductStructureSampleModule"/>
/// and <see cref="RequirementsWorkspaceSampleModule"/> directly, mirroring
/// <see cref="RequirementsWorkspaceSampleModule"/>'s own already-established
/// precedent for the first such dependency. Safe for the identical reason:
/// <see cref="ModuleServiceCollectionExtensions.AddDiscoveredModules"/>
/// registers every discovered module type as a DI singleton, and
/// <c>ModuleLifecycleManager</c> initialises modules in ordinal Id order —
/// <c>tempest.samples.mechanicalproductstructure</c>, then
/// <c>tempest.samples.requirementsworkspace</c>, then this module's own
/// <c>tempest.samples.workspacecalculations</c> sort in exactly that order,
/// so both dependencies' own Ids are already populated by the time this
/// module's own <see cref="InitialiseAsync"/> runs.
/// </para>
/// <para>
/// Builds its own <see cref="EngineeringObjectFactory{T}"/> instances
/// directly, in its own composition root — never through
/// <c>Tempest.App.Workspace.Calculations.CalculationObjectFactoryRegistry</c>,
/// which lives in <c>Tempest.App</c> (never referenced by this project),
/// mirroring <see cref="MechanicalProductStructureSampleModule"/>'s own
/// identical, disclosed precedent.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.workspacecalculations", "Calculations Workspace Sample", "1.0.0")]
public sealed class EngineeringCalculationsWorkspaceSampleModule : ModuleLifecycleBase
{
    /// <summary>The identity id this module establishes as current during its own initialisation.</summary>
    public const string SampleIdentityId = "sample.calculationsworkspace-user";

    /// <summary>The relationship kind linking a Calculation Domain object to its own executed <see cref="CalculationRecord{TResult}"/> document — must match <c>Tempest.App.Workspace.Calculations.CalculationTemplateRegistry.CalculatedByRelationshipKind</c> exactly; also reused, in the opposite direction, for cross-discipline Digital Thread links (a Requirement/Part "is calculatedBy" a Calculation).</summary>
    public const string CalculatedByRelationshipKind = "calculatedBy";

    /// <summary>The relationship kind linking one Calculation to another it depends on.</summary>
    public const string BasedOnCalculationRelationshipKind = "basedOnCalculation";

    private readonly IIdentityService _identityService;
    private readonly EngineeringDomainContext _context;
    private readonly ICalculationEngine _calculationEngine;
    private readonly IRequirementsService _requirementsService;
    private readonly MechanicalProductStructureSampleModule _mechanicalSampleModule;
    private readonly RequirementsWorkspaceSampleModule _requirementsSampleModule;

    /// <summary>Initialises a new instance of the <see cref="EngineeringCalculationsWorkspaceSampleModule"/> class.</summary>
    public EngineeringCalculationsWorkspaceSampleModule(
        IIdentityService identityService,
        EngineeringDomainContext context,
        ICalculationEngine calculationEngine,
        IRequirementsService requirementsService,
        MechanicalProductStructureSampleModule mechanicalSampleModule,
        RequirementsWorkspaceSampleModule requirementsSampleModule)
        : base("tempest.samples.workspacecalculations", "Calculations Workspace Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(identityService);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(calculationEngine);
        ArgumentNullException.ThrowIfNull(requirementsService);
        ArgumentNullException.ThrowIfNull(mechanicalSampleModule);
        ArgumentNullException.ThrowIfNull(requirementsSampleModule);

        _identityService = identityService;
        _context = context;
        _calculationEngine = calculationEngine;
        _requirementsService = requirementsService;
        _mechanicalSampleModule = mechanicalSampleModule;
        _requirementsSampleModule = requirementsSampleModule;
    }

    public Guid? BoltCalculationSetId { get; private set; }
    public Guid? BoltShearCalculationId { get; private set; }
    public Guid? BearingCalculationId { get; private set; }
    public Guid? BeamCalculationId { get; private set; }
    public Guid? PressureVesselCalculationId { get; private set; }
    public Guid? MaterialSelectionCalculationId { get; private set; }
    public bool HasRegistered { get; private set; }

    /// <inheritdoc />
    public override async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _identityService.EstablishCurrentPrincipal(SampleIdentityId);

        _calculationEngine.RegisterDefinition(new BoltShearCapacityCalculationDefinition());
        _calculationEngine.RegisterDefinition(new BeamBendingStressCalculationDefinition());
        _calculationEngine.RegisterDefinition(new BearingLoadCapacityCalculationDefinition());
        _calculationEngine.RegisterDefinition(new PressureVesselWallThicknessCalculationDefinition());
        _calculationEngine.RegisterDefinition(new MaterialSelectionMarginCalculationDefinition());

        // ---- Wing Attach Bolt Shear Check -> InReview -> Approved ----
        var boltShear = await CreateCalculationAsync(
            "Wing Attach Bolt Shear Check", "CALC-STR-001", "AN4 bolt, double shear, wing-to-spar attach fitting.", cancellationToken).ConfigureAwait(false);
        BoltShearCalculationId = boltShear.Id;

        var boltShearRecord = await _calculationEngine.ExecuteAsync<BoltShearCapacityInput, BoltShearCapacityResult>(
            BoltShearCapacityCalculationDefinition.Id,
            new BoltShearCapacityInput(new Quantity<Length>(6.35, LengthUnits.Millimetre), new Quantity<Pressure>(310, PressureUnits.Megapascal), ShearPlanes: 2, SafetyFactor: 1.5),
            cancellationToken).ConfigureAwait(false);
        await boltShear.LinkAsync(boltShearRecord.Id, CalculatedByRelationshipKind, cancellationToken).ConfigureAwait(false);
        await boltShear.TransitionAsync(LifecycleState.InReview, cancellationToken).ConfigureAwait(false);
        await boltShear.TransitionAsync(LifecycleState.Approved, cancellationToken).ConfigureAwait(false);

        // ---- Wing Attach Bearing Check -> basedOnCalculation the bolt shear check above ----
        var bearing = await CreateCalculationAsync(
            "Wing Attach Bearing Check", "CALC-STR-002", "Bearing check at the wing spar web plate bolt hole.", cancellationToken).ConfigureAwait(false);
        BearingCalculationId = bearing.Id;

        var bearingRecord = await _calculationEngine.ExecuteAsync<BearingLoadCapacityInput, BearingLoadCapacityResult>(
            BearingLoadCapacityCalculationDefinition.Id,
            new BearingLoadCapacityInput(new Quantity<Length>(6.35, LengthUnits.Millimetre), new Quantity<Length>(3.2, LengthUnits.Millimetre), new Quantity<Pressure>(690, PressureUnits.Megapascal), SafetyFactor: 1.5),
            cancellationToken).ConfigureAwait(false);
        await bearing.LinkAsync(bearingRecord.Id, CalculatedByRelationshipKind, cancellationToken).ConfigureAwait(false);
        await bearing.LinkAsync(boltShear.Id, BasedOnCalculationRelationshipKind, cancellationToken).ConfigureAwait(false);

        var boltSet = await CreateCalculationSetAsync("Wing Attach Bolt Calculations", "CALC-SET-001", [boltShear.Id, bearing.Id], cancellationToken).ConfigureAwait(false);
        BoltCalculationSetId = boltSet.Id;

        // ---- Wing Spar Bending Check -> left at InReview (the Cockpit's own "awaiting review" signal) ----
        var beam = await CreateCalculationAsync(
            "Wing Spar Bending Check", "CALC-STR-003", "Cantilever bending check at the wing spar root.", cancellationToken).ConfigureAwait(false);
        BeamCalculationId = beam.Id;

        // F=9 kN, L=1200 mm, 40x100 mm section -> ~162 MPa computed vs 310 MPa allowable (a real, comfortable margin - Valid, not Conditional; the deliberate Conditional/Failed demonstration is the Material Selection check below).
        var beamRecord = await _calculationEngine.ExecuteAsync<BeamBendingStressInput, BeamBendingStressResult>(
            BeamBendingStressCalculationDefinition.Id,
            new BeamBendingStressInput(
                new Quantity<Force>(9_000, ForceUnits.Newton), new Quantity<Length>(1200, LengthUnits.Millimetre),
                new Quantity<Length>(40, LengthUnits.Millimetre), new Quantity<Length>(100, LengthUnits.Millimetre),
                new Quantity<Pressure>(310, PressureUnits.Megapascal)),
            cancellationToken).ConfigureAwait(false);
        await beam.LinkAsync(beamRecord.Id, CalculatedByRelationshipKind, cancellationToken).ConfigureAwait(false);
        await beam.TransitionAsync(LifecycleState.InReview, cancellationToken).ConfigureAwait(false);

        // ---- Fuselage Pressure Vessel Wall Thickness -> revised after execution (the Cockpit's own "Out-of-date" signal) ----
        var pressureVessel = await CreateCalculationAsync(
            "Fuselage Pressure Vessel Wall Thickness", "CALC-PRS-001", "Cabin pressure vessel minimum wall thickness.", cancellationToken).ConfigureAwait(false);
        PressureVesselCalculationId = pressureVessel.Id;

        var pressureRecord = await _calculationEngine.ExecuteAsync<PressureVesselWallThicknessInput, PressureVesselWallThicknessResult>(
            PressureVesselWallThicknessCalculationDefinition.Id,
            new PressureVesselWallThicknessInput(
                new Quantity<Pressure>(0.62, PressureUnits.Megapascal), new Quantity<Length>(1000, LengthUnits.Millimetre),
                new Quantity<Pressure>(276, PressureUnits.Megapascal), JointEfficiency: 0.85, SafetyFactor: 1.5),
            cancellationToken).ConfigureAwait(false);
        await pressureVessel.LinkAsync(pressureRecord.Id, CalculatedByRelationshipKind, cancellationToken).ConfigureAwait(false);
        await pressureVessel.ReviseAsync(
            "Cabin altitude assumption updated after this calculation was last executed — now out of date; a fresh execution is required.",
            "Updated cabin altitude assumption.", cancellationToken).ConfigureAwait(false);

        // ---- Spar Cap Material Selection Margin -> a genuine Conditional/"Failed" outcome (applied stress exceeds allowable) ----
        var materialSelection = await CreateCalculationAsync(
            "Spar Cap Material Selection Margin", "CALC-MAT-001", "Screening check for a spar cap candidate material.", cancellationToken).ConfigureAwait(false);
        MaterialSelectionCalculationId = materialSelection.Id;

        var materialRecord = await _calculationEngine.ExecuteAsync<MaterialSelectionMarginInput, MaterialSelectionMarginResult>(
            MaterialSelectionMarginCalculationDefinition.Id,
            new MaterialSelectionMarginInput(
                MaterialsSampleModule.SampleMaterialId, new Quantity<Pressure>(250, PressureUnits.Megapascal), new Quantity<Pressure>(300, PressureUnits.Megapascal)),
            cancellationToken).ConfigureAwait(false);
        await materialSelection.LinkAsync(materialRecord.Id, CalculatedByRelationshipKind, cancellationToken).ConfigureAwait(false);

        // ---- Digital Thread: real cross-discipline links to the Mechanical/Requirements sample data ----
        if (_mechanicalSampleModule.WingAssemblyId is { } wingAssemblyId)
            await LinkSubjectToCalculationAsync(wingAssemblyId, boltShear.Id, cancellationToken).ConfigureAwait(false);

        if (_mechanicalSampleModule.SparWebPlateId is { } sparWebPlateId)
            await LinkSubjectToCalculationAsync(sparWebPlateId, bearing.Id, cancellationToken).ConfigureAwait(false);

        if (_requirementsSampleModule.AllSampleRequirementIds.Count > 0)
        {
            await _requirementsService.LinkAsync(
                _requirementsSampleModule.AllSampleRequirementIds[0], beam.Id, CalculatedByRelationshipKind, cancellationToken)
                .ConfigureAwait(false);
        }

        HasRegistered = true;
    }

    private async Task<Calculation> CreateCalculationAsync(string displayName, string identifier, string content, CancellationToken cancellationToken)
    {
        var created = await new EngineeringObjectFactory<Calculation>(
            "Calculation", _context, (doc, rev) => new Calculation(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty))
            .CreateAsync(content, cancellationToken).ConfigureAwait(false);

        return (Calculation)created;
    }

    private async Task<CalculationSet> CreateCalculationSetAsync(string displayName, string identifier, IReadOnlyList<Guid> memberCalculationIds, CancellationToken cancellationToken)
    {
        var created = await new EngineeringObjectFactory<CalculationSet>(
            "CalculationSet", _context, (doc, rev) => new CalculationSet(doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty, memberCalculationIds))
            .CreateAsync($"{displayName} — created via the Calculations Workspace sample module.", cancellationToken).ConfigureAwait(false);

        return (CalculationSet)created;
    }

    /// <summary>Links a Mechanical Domain object (an Assembly/Part, already an <see cref="IHasRelationships"/>-composing <see cref="EngineeringObjectBase"/>) to a Calculation via <see cref="CalculatedByRelationshipKind"/> — "this object's own value is calculatedBy this Calculation."</summary>
    private async Task LinkSubjectToCalculationAsync(Guid subjectId, Guid calculationId, CancellationToken cancellationToken)
    {
        var subject = await _context.Repository.FindAsync(subjectId, cancellationToken).ConfigureAwait(false);

        if (subject is IHasRelationships hasRelationships)
            await hasRelationships.LinkAsync(calculationId, CalculatedByRelationshipKind, cancellationToken).ConfigureAwait(false);
    }
}
