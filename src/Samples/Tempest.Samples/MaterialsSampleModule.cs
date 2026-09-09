using Tempest.Core.Commands;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
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
    internal static IReadOnlyDictionary<string, ReferenceQuantityValue> BuildSampleProperties(double yieldStrengthMPa, double referenceLengthMm) =>
        new Dictionary<string, ReferenceQuantityValue>
        {
            [MaterialPropertyNames.YieldStrength] = new ReferenceQuantityValue(
                new Quantity<Pressure>(yieldStrengthMPa, PressureUnits.Megapascal),
                ReferenceValueOrigin.Unknown,
                conditions: "Demonstration only — no real applicable conditions"),
            ["ReferenceLength"] = new ReferenceQuantityValue(
                new Quantity<Length>(referenceLengthMm, LengthUnits.Millimetre),
                ReferenceValueOrigin.Unknown),
        };

    /// <summary>
    /// The provenance every record this module registers carries: openly
    /// fictional, and therefore never verifiable, so it can never be
    /// released.
    /// </summary>
    internal static ReferenceProvenance SampleProvenance { get; } = new(
        SourceOrganisation: "TempestOS sample module",
        SourceDocument: "Fictional test fixture — not a real material standard",
        Notes: "Invented for WP 7.1C's own living-reference module; never a real, published value.");

    /// <summary>Builds the fictional demonstration material this module registers and revises.</summary>
    internal static MaterialDefinition BuildSampleDefinition(double yieldStrengthMPa, double referenceLengthMm) => new()
    {
        Name = "Fictional Test Alloy",
        Family = MaterialFamily.Other,
        SourceClassification = "TestFixture",
        Properties = BuildSampleProperties(yieldStrengthMPa, referenceLengthMm),
    };

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Registers a fictional material with two dimensioned properties, then
    /// revises it once — proving register/revise/find all work end to end
    /// against the real catalogue — then registers
    /// <see cref="RegisterSampleMaterialCommand"/> and
    /// <see cref="ReviseSampleMaterialCommand"/>'s handlers and descriptors.
    /// </para>
    /// <para>
    /// <b>Idempotent restart (`WP 10.1B`, `TD-37`):</b> <see cref="IMaterialCatalog"/>
    /// is built directly on <see cref="Tempest.Core.Persistence.IPersistenceStore"/>
    /// (`ADR-0055`), which is durable and, by default, shared across every
    /// process launched from the same working directory (`ADR-0041`) — so a
    /// second real launch of the console or desktop application, from the
    /// same directory as a first, successful one, would otherwise find
    /// <see cref="SampleMaterialId"/> already registered and fail loudly.
    /// This module now checks first, and treats an already-registered
    /// sample material as this module's own prior work, not a collision —
    /// re-reading it rather than re-registering it, and still (re-)registering
    /// its own commands, which are in-memory only and never survive a
    /// restart on their own.
    /// </para>
    /// </remarks>
    public override async Task InitialiseAsync(CancellationToken cancellationToken)
    {
        var existing = await _materialCatalog.FindAsync(SampleMaterialId, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            // Already durably registered by an earlier launch against this
            // same persistence store (TD-37) - reuse it rather than
            // re-attempting RegisterAsync, which would throw
            // DuplicateMaterialException.
            RegisteredMaterialId = existing.Id;
        }
        else
        {
            var material = await _materialCatalog.RegisterAsync(
                SampleMaterialId,
                BuildSampleDefinition(yieldStrengthMPa: 100.0, referenceLengthMm: 10.0),
                SampleProvenance,
                cancellationToken)
                .ConfigureAwait(false);
            RegisteredMaterialId = material.Id;

            await _materialCatalog.ReviseAsync(
                SampleMaterialId,
                BuildSampleDefinition(yieldStrengthMPa: 105.0, referenceLengthMm: 10.0),
                SampleProvenance,
                "Sample revision — fictional updated test value.",
                cancellationToken)
                .ConfigureAwait(false);
        }

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
