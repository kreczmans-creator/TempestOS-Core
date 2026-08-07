using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Calculations;

/// <summary>
/// Creates a new Calculation Domain object. Plain <see cref="ICommand"/>,
/// not <see cref="IWorkspaceCommand"/> — there is no pre-existing target
/// object/view to refresh, only a new one to be navigated to once created,
/// mirroring <see cref="Mechanical.CreateMechanicalObjectCommand"/>'s own
/// identical reasoning exactly.
/// </summary>
public sealed class CreateCalculationObjectCommand : ICommand
{
    public CreateCalculationObjectCommand(
        string kind, string displayName, string? identifier = null, Guid? parentId = null, string? initialContent = null,
        IReadOnlyList<Guid>? memberCalculationIds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Kind = kind;
        DisplayName = displayName;
        Identifier = identifier;
        ParentId = parentId;
        InitialContent = initialContent ?? $"{displayName} — created via the Calculations module.";
        MemberCalculationIds = memberCalculationIds;
    }

    /// <summary>Gets the Kind to create — one of <see cref="CalculationObjectFactoryRegistry.SupportedKinds"/>.</summary>
    public string Kind { get; }

    /// <summary>Gets the new object's own display name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the new object's own business identifier, or <see langword="null"/> to leave it unset.</summary>
    public string? Identifier { get; }

    /// <summary>Gets the new object's own parent, or <see langword="null"/> for a top-level object.</summary>
    public Guid? ParentId { get; }

    /// <summary>Gets the new object's own initial revision content.</summary>
    public string InitialContent { get; }

    /// <summary>Gets the new Calculation Set's own frozen members — only meaningful for <c>"CalculationSet"</c>; ignored for <c>"Calculation"</c>.</summary>
    public IReadOnlyList<Guid>? MemberCalculationIds { get; }
}

/// <summary>Handles <see cref="CreateCalculationObjectCommand"/>.</summary>
public sealed class CreateCalculationObjectCommandHandler : ICommandHandler<CreateCalculationObjectCommand>
{
    private readonly CalculationObjectFactoryRegistry _registry;

    public CreateCalculationObjectCommandHandler(CalculationObjectFactoryRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _registry = registry;
    }

    public async Task<CommandResult> HandleAsync(CreateCalculationObjectCommand command, CancellationToken cancellationToken)
    {
        IEngineeringObject created;

        try
        {
            created = await _registry.CreateAsync(
                command.Kind, command.Identifier, command.DisplayName, command.InitialContent, command.ParentId,
                command.MemberCalculationIds, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return CommandResult.Success($"Created {command.Kind} '{created.Id}'.");
    }
}
