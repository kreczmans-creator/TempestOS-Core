using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Manufacturing;

/// <summary>
/// Builds a new Manufacturing Domain object — <c>"ManufacturingOperation"</c>,
/// <c>"WorkInstruction"</c>, or <c>"Inspection"</c> — a small,
/// Workspace-layer composition helper wrapping
/// <see cref="EngineeringObjectFactory{T}"/> instances, mirroring
/// <see cref="Documents.DocumentObjectFactoryRegistry"/>/
/// <see cref="Verification.VerificationActivityFactoryRegistry"/>'s own
/// identical shape (`WP 9.5A`) exactly. Never a Domain-layer registry
/// contract — this type lives entirely in <c>Tempest.App</c>.
/// </summary>
/// <remarks>
/// All three Kinds are `WP 8.2C`-compiled, confirmed by direct
/// repository-wide search to have been instantiated by no sample module
/// or test anywhere before this Work Package. <c>"Test"</c> (also a real,
/// compiled `VerificationActivity` subtype) is deliberately never
/// constructed here — this Work Package's own scope names "Inspection
/// Operations," never "Test Operations" — disclosed in
/// `WP9.5A Technical Debt Assessment.md`.
/// </remarks>
/// <remarks>
/// **`WP 12.1B` (`ADR-0105`).** <see cref="ManufacturingOperationKind"/>/
/// <see cref="WorkInstructionKind"/>/<see cref="InspectionKind"/> — this
/// class's own three base Kinds — are now declared as named constants
/// alongside its own already-disciplined `Classification` sub-values
/// (`ADR-0091` already required those be declared once; this Work
/// Package closes the one remaining gap, the base Kinds themselves,
/// previously only named inside <see cref="SupportedKinds"/>). Named
/// with a `Kind` suffix, not bare `ManufacturingOperation`, since the
/// Domain type of that identical name is already this class's own most
/// frequently used type reference. No value, no behaviour, and no
/// public signature changed — only where each literal is declared.
/// </remarks>
public sealed class ManufacturingObjectFactoryRegistry
{
    /// <summary>The <see cref="IEngineeringObject.Kind"/> for a Manufacturing Operation.</summary>
    public const string ManufacturingOperationKind = "ManufacturingOperation";

    /// <summary>The <see cref="IEngineeringObject.Kind"/> for a Work Instruction.</summary>
    public const string WorkInstructionKind = "WorkInstruction";

    /// <summary>The <see cref="IEngineeringObject.Kind"/> for an Inspection.</summary>
    public const string InspectionKind = "Inspection";

    /// <summary>The Kinds this registry can construct.</summary>
    public static readonly IReadOnlyList<string> SupportedKinds = [ManufacturingOperationKind, WorkInstructionKind, InspectionKind];

    /// <summary>
    /// The <see cref="EngineeringObjectMetadata.Classification"/> values
    /// this Work Package assigns to a plain <c>"ManufacturingOperation"</c>
    /// to realise Routings/Operations/Supplier Operations without a
    /// dedicated Domain Kind for any of them (`ADR-0091`, mirroring
    /// `ADR-0088`'s own identical Classification-taxonomy precedent).
    /// </summary>
    public const string Routing = "Routing";
    public const string Operation = "Operation";
    public const string SupplierOperation = "Supplier Operation";

    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="ManufacturingObjectFactoryRegistry"/> class.</summary>
    public ManufacturingObjectFactoryRegistry(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    /// <summary>Creates a new <see cref="ManufacturingOperationKind"/>, moving it under <paramref name="parentId"/> if one is given.</summary>
    /// <param name="partId">The Mechanical Part/Assembly this Operation manufactures — <see cref="IManufacturingOperation.PartId"/>.</param>
    /// <param name="classification">One of <see cref="Routing"/>/<see cref="Operation"/>/<see cref="SupplierOperation"/>, or any other free-text classification.</param>
    /// <exception cref="ArgumentException"><paramref name="displayName"/> is null/empty/whitespace.</exception>
    public async Task<IEngineeringObject> CreateOperationAsync(
        string? identifier, string displayName, string initialContent, Guid partId, string? classification, Guid? parentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(initialContent);

        var metadata = classification is null ? EngineeringObjectMetadata.Empty : new EngineeringObjectMetadata(Classification: classification);

        var factory = new EngineeringObjectFactory<ManufacturingOperation>(
            ManufacturingOperationKind, _context, (doc, rev) => new ManufacturingOperation(doc, rev, _context, identifier, displayName, metadata, partId));

        var created = await factory.CreateAsync(initialContent, cancellationToken).ConfigureAwait(false);

        if (parentId is { } pid && created is IHasParent hasParent)
            await hasParent.MoveAsync(pid, cancellationToken).ConfigureAwait(false);

        return created;
    }

    /// <summary>Creates a new <see cref="WorkInstructionKind"/> against <paramref name="manufacturingOperationId"/>, moving it under <paramref name="parentId"/> if one is given.</summary>
    /// <exception cref="ArgumentException"><paramref name="displayName"/> is null/empty/whitespace.</exception>
    public async Task<IEngineeringObject> CreateWorkInstructionAsync(
        string? identifier, string displayName, string initialContent, Guid manufacturingOperationId, Guid? parentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(initialContent);

        var factory = new EngineeringObjectFactory<WorkInstruction>(
            WorkInstructionKind, _context, (doc, rev) => new WorkInstruction(
                doc, rev, _context, identifier, displayName, EngineeringObjectMetadata.Empty, manufacturingOperationId));

        var created = await factory.CreateAsync(initialContent, cancellationToken).ConfigureAwait(false);

        if (parentId is { } pid && created is IHasParent hasParent)
            await hasParent.MoveAsync(pid, cancellationToken).ConfigureAwait(false);

        return created;
    }

    /// <summary>Creates a new <see cref="InspectionKind"/> verifying <paramref name="subjectId"/>, moving it under <paramref name="parentId"/> if one is given.</summary>
    /// <exception cref="ArgumentException"><paramref name="displayName"/>/<paramref name="method"/> is null/empty/whitespace.</exception>
    public async Task<IEngineeringObject> CreateInspectionAsync(
        string displayName, string initialContent, Guid subjectId, string method, Guid? parentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentNullException.ThrowIfNull(initialContent);

        var factory = new EngineeringObjectFactory<Inspection>(
            InspectionKind, _context, (doc, rev) => new Inspection(doc, rev, _context, displayName, EngineeringObjectMetadata.Empty, subjectId, method));

        var created = await factory.CreateAsync(initialContent, cancellationToken).ConfigureAwait(false);

        if (parentId is { } pid && created is IHasParent hasParent)
            await hasParent.MoveAsync(pid, cancellationToken).ConfigureAwait(false);

        return created;
    }

    /// <summary>Registers how each of this discipline's own three Kinds comes back after a restart (`TD-85`) — see <c>MechanicalObjectFactoryRegistry.RegisterRehydrators</c> for the rationale.</summary>
    public static void RegisterRehydrators(IEngineeringObjectRehydratorRegistry registry, EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(context);

        registry.Register<ManufacturingOperation>(ManufacturingOperationKind, context);
        registry.Register<WorkInstruction>(WorkInstructionKind, context);
        registry.Register<Inspection>(InspectionKind, context);
    }
}
