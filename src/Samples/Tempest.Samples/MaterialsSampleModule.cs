using Tempest.Core.Commands;
using Tempest.Core.Materials;
using Tempest.Core.Modules;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that demonstrates the
/// Materials Framework: it registers a material with two dimensioned
/// properties (each carrying provenance) during its own initialisation,
/// revises it once, and registers two commands (register/revise)
/// demonstrating both paths for manual invocation.
/// </summary>
/// <remarks>
/// <para>
/// The living reference module `WP 7.1C` validates the Materials
/// Framework against — mirrors <see cref="EngineeringDataSampleModule"/>'s
/// own role for the Engineering Data Model. Carries
/// <see cref="ModuleMetadataAttribute"/> so Discovery can read its
/// identity without instantiating it (ADR-0027).
/// </para>
/// <para>
/// <b>Every value below is fictional, invented purely for this
/// demonstration — never a real material standard's own published
/// value.</b> Each property's own <see cref="MaterialPropertyProvenance.SourceReference"/>
/// says so explicitly, per this Work Package's own controlling
/// instruction ("do not invent values").
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.materials", "Materials Sample", "1.0.0")]
public sealed class MaterialsSampleModule : ModuleLifecycleBase
{
    /// <summary>The <c>materialId</c> this module registers its own sample material under.</summary>
    public const string SampleMaterialId = "sample.fictional-test-alloy";

    /// <summary>
    /// The <see cref="Commands.CommandDescriptor.Id"/> this module registers
    /// for <see cref="RegisterSampleMaterialCommand"/>.
    /// </summary>
    public const string RegisterSampleMaterialCommandId = "sample.materials-register";

    /// <summary>
    /// The <see cref="Commands.CommandDescriptor.Id"/> this module registers
    /// for <see cref="ReviseSampleMaterialCommand"/>.
    /// </summary>
    public const string ReviseSampleMaterialCommandId = "sample.materials-revise";

    private readonly IMaterialCatalog _materialCatalog;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICommandRegistry _commandRegistry;

    /// <summary>
    /// Initialises a new instance of the <see cref="MaterialsSampleModule"/> class.
    /// </summary>
    /// <param name="materialCatalog">The Materials Framework service this module registers and revises materials through, resolved via ordinary constructor injection.</param>
    /// <param name="commandDispatcher">The Command Framework's dispatch-side surface this module registers its handlers through.</param>
    /// <param name="commandRegistry">The Command Framework's discovery-side surface this module registers its descriptors through.</param>
    public MaterialsSampleModule(
        IMaterialCatalog materialCatalog,
        ICommandDispatcher commandDispatcher,
        ICommandRegistry commandRegistry)
        : base("tempest.samples.materials", "Materials Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(materialCatalog);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _materialCatalog = materialCatalog;
        _commandDispatcher = commandDispatcher;
        _commandRegistry = commandRegistry;
    }

    /// <summary>Gets the <c>materialId</c> registered during <see cref="InitialiseAsync"/>, once initialisation has completed.</summary>
    public string? RegisteredMaterialId { get; private set; }

    /// <summary>Gets a value indicating whether <see cref="InitialiseAsync"/> has registered this module's commands.</summary>
    public bool HasRegistered { get; private set; }

    /// <summary>Builds the two fictional, dimensioned demonstration properties this module registers and revises with.</summary>
    internal static IReadOnlyDictionary<string, MaterialProperty> BuildSampleProperties(double yieldStrengthMPa, double referenceLengthMm) =>
        new Dictionary<string, MaterialProperty>
        {
            ["YieldStrength"] = new MaterialProperty(
                new Quantity<Pressure>(yieldStrengthMPa, PressureUnits.Megapascal),
                new MaterialPropertyProvenance(
                    SourceReference: "Fictional test fixture — not a real material standard",
                    SourceRevision: null,
                    ValidationStatus: MaterialPropertyValidationStatus.Unvalidated,
                    ConfidenceLevel: MaterialPropertyConfidenceLevel.Unknown,
                    ApplicableConditions: "Demonstration only — no real applicable conditions",
                    Notes: "Invented for WP 7.1C's own living-reference module; never a real, published value.")),
            ["ReferenceLength"] = new MaterialProperty(
                new Quantity<Length>(referenceLengthMm, LengthUnits.Millimetre),
                new MaterialPropertyProvenance(
                    SourceReference: "Fictional test fixture — not a real material standard",
                    SourceRevision: null,
                    ValidationStatus: MaterialPropertyValidationStatus.Unvalidated,
                    ConfidenceLevel: MaterialPropertyConfidenceLevel.Unknown,
                    ApplicableConditions: null,
                    Notes: "Invented for WP 7.1C's own living-reference module; never a real, published value.")),
        };

    /// <inheritdoc />
    /// <remarks>
    /// Registers a fictional material with two dimensioned properties, then
    /// revises it once — proving register/revise/find all work end to end
    /// against the real catalogue — then registers
    /// <see cref="RegisterSampleMaterialCommand"/> and
    /// <see cref="ReviseSampleMaterialCommand"/>'s handlers and descriptors.
    /// </remarks>
    public override async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        var material = await _materialCatalog.RegisterAsync(
            SampleMaterialId,
            "Fictional Test Alloy",
            BuildSampleProperties(yieldStrengthMPa: 100.0, referenceLengthMm: 10.0),
            category: "TestFixture",
            cancellationToken)
            .ConfigureAwait(false);
        RegisteredMaterialId = material.MaterialId;

        await _materialCatalog.ReviseAsync(
            SampleMaterialId,
            BuildSampleProperties(yieldStrengthMPa: 105.0, referenceLengthMm: 10.0),
            "Sample revision — fictional updated test value.",
            cancellationToken)
            .ConfigureAwait(false);

        _commandDispatcher.RegisterHandler<RegisterSampleMaterialCommand>(
            new RegisterSampleMaterialCommandHandler(_materialCatalog));
        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: RegisterSampleMaterialCommandId,
            displayName: "Register Sample Material",
            category: "Sample",
            description: "Registers a new fictional sample material.",
            createDefault: () => new RegisterSampleMaterialCommand()));

        _commandDispatcher.RegisterHandler<ReviseSampleMaterialCommand>(
            new ReviseSampleMaterialCommandHandler(_materialCatalog, this));
        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: ReviseSampleMaterialCommandId,
            displayName: "Revise Sample Material",
            category: "Sample",
            description: "Revises this module's own sample material.",
            createDefault: () => new ReviseSampleMaterialCommand()));

        HasRegistered = true;
    }
}
