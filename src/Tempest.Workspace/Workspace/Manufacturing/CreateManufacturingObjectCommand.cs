using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Manufacturing;

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
                    command.PartId ?? throw new ArgumentException("Select the Part (or Component, Assembly or Sub-Assembly) the operation is performed on in the Project Explorer first; a Manufacturing Operation is created against it.", nameof(command)),
                    command.Classification, command.ParentId, cancellationToken).ConfigureAwait(false),

                "WorkInstruction" => await _registry.CreateWorkInstructionAsync(
                    command.Identifier, command.DisplayName, command.InitialContent,
                    command.ManufacturingOperationId ?? throw new ArgumentException("Select the Manufacturing Operation the instruction belongs to first; a Work Instruction is created against it.", nameof(command)),
                    command.ParentId, cancellationToken).ConfigureAwait(false),

                "Inspection" => await _registry.CreateInspectionAsync(
                    command.DisplayName, command.InitialContent,
                    command.SubjectId ?? throw new ArgumentException("Select the object being inspected first; an Inspection is created against it.", nameof(command)),
                    command.Method ?? throw new ArgumentException("Method is required to create an Inspection.", nameof(command)),
                    command.ParentId, cancellationToken).ConfigureAwait(false),

                _ => throw new ArgumentException($"'{command.Kind}' is not a supported Manufacturing Kind — expected one of: {string.Join(", ", ManufacturingObjectFactoryRegistry.SupportedKinds)}.", nameof(command)),
            };
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (EngineeringDocumentNotFoundException)
        {
            // `WP 18.1B` §5: ParentId is now resolved from the selection
            // (CreationPlacement.ParentFor), not only PartId/ManufacturingOperationId/
            // SubjectId — a selection that named a real Kind but no longer
            // names a live object (deleted between selection and this
            // command reaching the domain) refuses cleanly here rather
            // than throwing out of Build, exactly as an ArgumentException
            // from a missing selection already does above.
            return CommandResult.Failure("The selected placement no longer exists; select it again and retry.");
        }

        return CommandResult.Success($"Created {command.Kind} '{(created as IHasBusinessIdentifier)?.DisplayName ?? created.Id.ToString()}'.", created.Id, command.Kind);
    }
}
