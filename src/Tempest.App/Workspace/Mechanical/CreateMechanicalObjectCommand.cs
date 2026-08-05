using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Mechanical;

/// <summary>
/// Creates a new Mechanical Product Structure object. Plain <see cref="ICommand"/>,
/// not <see cref="IWorkspaceCommand"/> — there is no pre-existing target
/// object/view to refresh (the one <see cref="IWorkspaceCommand"/> exists
/// to identify), only a new one to be navigated to once created.
/// </summary>
public sealed class CreateMechanicalObjectCommand : ICommand
{
    public CreateMechanicalObjectCommand(
        string kind, string displayName, string? identifier = null, Guid? parentId = null, string? initialContent = null,
        IReadOnlyList<ConfigurationMember>? memberRevisions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Kind = kind;
        DisplayName = displayName;
        Identifier = identifier;
        ParentId = parentId;
        InitialContent = initialContent ?? $"{displayName} — created via the Mechanical Product Structure module.";
        MemberRevisions = memberRevisions;
    }

    /// <summary>Gets the Kind to create — one of <see cref="MechanicalObjectFactoryRegistry.SupportedKinds"/>.</summary>
    public string Kind { get; }

    /// <summary>Gets the new object's own display name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the new object's own business identifier, or <see langword="null"/> to leave it unset.</summary>
    public string? Identifier { get; }

    /// <summary>Gets the new object's own parent, or <see langword="null"/> for a top-level object.</summary>
    public Guid? ParentId { get; }

    /// <summary>Gets the new object's own initial revision content.</summary>
    public string InitialContent { get; }

    /// <summary>Gets the new object's own baselined members — only meaningful for <c>"Configuration"</c>/<c>"Baseline"</c>/<c>"Release"</c> (`WP 9.0B`).</summary>
    public IReadOnlyList<ConfigurationMember>? MemberRevisions { get; }
}

/// <summary>Handles <see cref="CreateMechanicalObjectCommand"/>.</summary>
public sealed class CreateMechanicalObjectCommandHandler : ICommandHandler<CreateMechanicalObjectCommand>
{
    private readonly MechanicalObjectFactoryRegistry _registry;

    public CreateMechanicalObjectCommandHandler(MechanicalObjectFactoryRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _registry = registry;
    }

    public async Task<CommandResult> HandleAsync(CreateMechanicalObjectCommand command, CancellationToken cancellationToken)
    {
        IEngineeringObject created;

        try
        {
            created = await _registry.CreateAsync(
                command.Kind, command.Identifier, command.DisplayName, command.InitialContent, command.ParentId,
                command.MemberRevisions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return CommandResult.Success($"Created {command.Kind} '{created.Id}'.");
    }
}
