using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Manufacturing;

/// <summary>
/// Creates a new Manufacturing Domain object — <c>"ManufacturingOperation"</c>,
/// <c>"WorkInstruction"</c>, or <c>"Inspection"</c>. Plain <see cref="ICommand"/>,
/// not <see cref="IWorkspaceCommand"/> — there is no pre-existing target
/// object/view to refresh, only a new one to be navigated to once created,
/// mirroring <see cref="Documents.CreateDocumentObjectCommand"/>'s own
/// identical reasoning exactly.
/// </summary>
public sealed class CreateManufacturingObjectCommand : ICommand
{
    public CreateManufacturingObjectCommand(
        string kind, string displayName, string? identifier = null, Guid? parentId = null, string? initialContent = null,
        Guid? partId = null, string? classification = null, Guid? manufacturingOperationId = null,
        Guid? subjectId = null, string? method = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Kind = kind;
        DisplayName = displayName;
        Identifier = identifier;
        ParentId = parentId;
        InitialContent = initialContent ?? $"{displayName} — created via the Manufacturing module.";
        PartId = partId;
        Classification = classification;
        ManufacturingOperationId = manufacturingOperationId;
        SubjectId = subjectId;
        Method = method;
    }

    /// <summary>Gets the Kind to create — one of <see cref="ManufacturingObjectFactoryRegistry.SupportedKinds"/>.</summary>
    public string Kind { get; }

    /// <summary>Gets the new object's own display name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the new object's own business identifier — meaningful for <c>"ManufacturingOperation"</c>/<c>"WorkInstruction"</c> only; <c>"Inspection"</c> carries none (mirrors <c>VerificationActivity</c>'s own identical shape).</summary>
    public string? Identifier { get; }

    /// <summary>Gets the new object's own parent, or <see langword="null"/> for a top-level object.</summary>
    public Guid? ParentId { get; }

    /// <summary>Gets the new object's own initial revision content.</summary>
    public string InitialContent { get; }

    /// <summary>Gets the Mechanical Part/Assembly this Operation manufactures — required for <c>"ManufacturingOperation"</c>, ignored otherwise.</summary>
    public Guid? PartId { get; }

    /// <summary>Gets the new object's own classification — meaningful for <c>"ManufacturingOperation"</c> (<see cref="ManufacturingObjectFactoryRegistry.Routing"/>/<see cref="ManufacturingObjectFactoryRegistry.Operation"/>/<see cref="ManufacturingObjectFactoryRegistry.SupplierOperation"/>, `ADR-0091`); ignored otherwise.</summary>
    public string? Classification { get; }

    /// <summary>Gets the Manufacturing Operation this Work Instruction documents — required for <c>"WorkInstruction"</c>, ignored otherwise.</summary>
    public Guid? ManufacturingOperationId { get; }

    /// <summary>Gets the engineering object this Inspection verifies — required for <c>"Inspection"</c>, ignored otherwise.</summary>
    public Guid? SubjectId { get; }

    /// <summary>Gets the inspection method — required for <c>"Inspection"</c>, ignored otherwise.</summary>
    public string? Method { get; }
}

/// <summary>Handles <see cref="CreateManufacturingObjectCommand"/>.</summary>
public sealed class CreateManufacturingObjectCommandHandler : ICommandHandler<CreateManufacturingObjectCommand>
{
    private readonly ManufacturingObjectFactoryRegistry _registry;

    public CreateManufacturingObjectCommandHandler(ManufacturingObjectFactoryRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _registry = registry;
    }

    public async Task<CommandResult> HandleAsync(CreateManufacturingObjectCommand command, CancellationToken cancellationToken)
    {
        IEngineeringObject created;

        try
        {
            created = command.Kind switch
            {
                "ManufacturingOperation" => await _registry.CreateOperationAsync(
                    command.Identifier, command.DisplayName, command.InitialContent,
                    command.PartId ?? throw new ArgumentException("PartId is required to create a ManufacturingOperation.", nameof(command)),
                    command.Classification, command.ParentId, cancellationToken).ConfigureAwait(false),

                "WorkInstruction" => await _registry.CreateWorkInstructionAsync(
                    command.Identifier, command.DisplayName, command.InitialContent,
                    command.ManufacturingOperationId ?? throw new ArgumentException("ManufacturingOperationId is required to create a WorkInstruction.", nameof(command)),
                    command.ParentId, cancellationToken).ConfigureAwait(false),

                "Inspection" => await _registry.CreateInspectionAsync(
                    command.DisplayName, command.InitialContent,
                    command.SubjectId ?? throw new ArgumentException("SubjectId is required to create an Inspection.", nameof(command)),
                    command.Method ?? throw new ArgumentException("Method is required to create an Inspection.", nameof(command)),
                    command.ParentId, cancellationToken).ConfigureAwait(false),

                _ => throw new ArgumentException($"'{command.Kind}' is not a supported Manufacturing Kind — expected one of: {string.Join(", ", ManufacturingObjectFactoryRegistry.SupportedKinds)}.", nameof(command)),
            };
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return CommandResult.Success($"Created {command.Kind} '{created.Id}'.");
    }
}
