using Tempest.Core.Commands;
using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

/// <summary>
/// The single composition-root entry point wiring the whole Requirements
/// Management discipline into a running Workspace — everything
/// `Program.cs` needs, kept out of `Program.cs` itself, mirroring
/// <c>MechanicalWorkspaceRegistration</c>'s own identical shape
/// (`WP 9.1A`, the second real Engineering discipline wired this way).
/// </summary>
/// <remarks>
/// Must run <em>after</em> the Runtime Host has started, exactly like
/// <c>MechanicalWorkspaceRegistration</c> — every piece here needs
/// <see cref="IRequirementsService"/>/<see cref="ICommandDispatcher"/>/
/// <see cref="ICommandRegistry"/>, all resolvable only once
/// <c>ITempestHost.Services</c> is populated.
/// </remarks>
public static class RequirementsWorkspaceRegistration
{
    /// <summary>The three Requirements Kinds this Work Package registers a View and a Property Facet Provider for.</summary>
    public static readonly IReadOnlyList<string> SupportedKinds =
    [
        RequirementsService.RequirementDocumentKind,
        RequirementsService.RequirementCollectionDocumentKind,
        RequirementsService.RequirementGroupDocumentKind,
    ];

    /// <summary>Registers every Requirements Management Workspace extension point.</summary>
    public static void Register(
        IWorkspaceManager manager, IRequirementsService requirementsService, ICommandDispatcher commandDispatcher, ICommandRegistry commandRegistry)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(requirementsService);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        manager.RegisterExplorerArea(
            RequirementsWorkspaceExplorerModule.NavigationItemId,
            new RequirementsNodeProvider(RequirementsWorkspaceExplorerModule.NavigationItemId, requirementsService));

        foreach (var kind in SupportedKinds)
        {
            manager.RegisterView(kind, new RequirementsWorkspaceViewFactory(kind, requirementsService));
            manager.RegisterFacetProvider(kind, new RequirementsPropertyFacetProvider(kind, requirementsService));
        }

        // WP 10.2A (ADR-0096): real delete dispatch, one factory per Kind
        // since each has its own dedicated command
        // (Delete{Requirement,RequirementGroup,RequirementCollection}Command)
        // — never a Rename factory for any of the three: no
        // Rename*Command exists for this discipline (a Requirement's own
        // mutable field is its Statement, mutated via
        // ReviseRequirementCommand, not a DisplayName/RenameAsync concept
        // the other five disciplines share) — honestly not registered,
        // rather than offering a menu item with nothing to dispatch to.
        manager.RegisterDeleteFactory(RequirementsService.RequirementDocumentKind, static (id, _) => new DeleteRequirementCommand(id));
        manager.RegisterDeleteFactory(RequirementsService.RequirementGroupDocumentKind, static (id, _) => new DeleteRequirementGroupCommand(id));
        manager.RegisterDeleteFactory(RequirementsService.RequirementCollectionDocumentKind, static (id, _) => new DeleteRequirementCollectionCommand(id));

        // WP 10.3A (ADR-0097): real revise dispatch — the Object Editor
        // Framework's own Content field, realised here as the Requirement's
        // own Statement (ReviseRequirementCommand, unchanged since `WP
        // 7.3A`). Requirement only — RequirementGroup/RequirementCollection
        // are structural containers with no Statement/Content concept of
        // their own, honestly not registered, the identical asymmetry this
        // class's own remarks already disclose for Rename (which exists for
        // neither Kind), just the other way around for Requirement itself.
        manager.RegisterReviseFactory(RequirementsService.RequirementDocumentKind, static (id, _, content) => new ReviseRequirementCommand(id, content));

        commandDispatcher.RegisterHandler<CreateRequirementCommand>(new CreateRequirementCommandHandler(requirementsService));
        commandDispatcher.RegisterHandler<ReviseRequirementCommand>(new ReviseRequirementCommandHandler(requirementsService));
        commandDispatcher.RegisterHandler<SetRequirementStatusCommand>(new SetRequirementStatusCommandHandler(requirementsService));
        commandDispatcher.RegisterHandler<SetRequirementOwnerCommand>(new SetRequirementOwnerCommandHandler(requirementsService));
        commandDispatcher.RegisterHandler<SetRequirementPriorityCommand>(new SetRequirementPriorityCommandHandler(requirementsService));
        commandDispatcher.RegisterHandler<DeleteRequirementCommand>(new DeleteRequirementCommandHandler(requirementsService));
        commandDispatcher.RegisterHandler<MoveRequirementCommand>(new MoveRequirementCommandHandler(requirementsService));
        commandDispatcher.RegisterHandler<DuplicateRequirementCommand>(new DuplicateRequirementCommandHandler(requirementsService));
        commandDispatcher.RegisterHandler<LinkRequirementCommand>(new LinkRequirementCommandHandler(requirementsService));
        commandDispatcher.RegisterHandler<CreateRequirementGroupCommand>(new CreateRequirementGroupCommandHandler(requirementsService));
        commandDispatcher.RegisterHandler<MoveRequirementGroupCommand>(new MoveRequirementGroupCommandHandler(requirementsService));
        commandDispatcher.RegisterHandler<DeleteRequirementGroupCommand>(new DeleteRequirementGroupCommandHandler(requirementsService));
        commandDispatcher.RegisterHandler<CreateRequirementCollectionCommand>(new CreateRequirementCollectionCommandHandler(requirementsService));
        commandDispatcher.RegisterHandler<DeleteRequirementCollectionCommand>(new DeleteRequirementCollectionCommandHandler(requirementsService));
        commandDispatcher.RegisterHandler<AddRequirementToCollectionCommand>(new AddRequirementToCollectionCommandHandler(requirementsService));
        commandDispatcher.RegisterHandler<BulkSetRequirementStatusCommand>(new BulkSetRequirementStatusCommandHandler(requirementsService));
        commandDispatcher.RegisterHandler<BulkSetRequirementOwnerCommand>(new BulkSetRequirementOwnerCommandHandler(requirementsService));
        commandDispatcher.RegisterHandler<BulkSetRequirementPriorityCommand>(new BulkSetRequirementPriorityCommandHandler(requirementsService));

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.create", displayName: "Create Requirement", category: "Requirements",
            description: "Creates a new Requirement with a business identifier and statement."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.revise", displayName: "Revise Requirement", category: "Requirements",
            description: "Records a new revision of the selected Requirement's own statement."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.set-status", displayName: "Set Requirement Status", category: "Requirements",
            description: "Sets the selected Requirement's own current lifecycle status."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.set-owner", displayName: "Set Requirement Owner", category: "Requirements",
            description: "Sets the selected Requirement's own current owner."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.set-priority", displayName: "Set Requirement Priority", category: "Requirements",
            description: "Sets the selected Requirement's own current priority."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.delete", displayName: "Delete Requirement", category: "Requirements",
            description: "Soft-deletes the selected Requirement."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.move", displayName: "Move Requirement", category: "Requirements",
            description: "Moves the selected Requirement into a different group, or ungroups it."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.duplicate", displayName: "Duplicate Requirement", category: "Requirements",
            description: "Creates a copy of the selected Requirement's own Statement/Category/Priority/Group under a new identifier."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.link", displayName: "Link Requirement", category: "Requirements",
            description: "Records a typed relationship from the selected Requirement to another document — allocation, dependency, derivation, reference, or satisfaction."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.create-group", displayName: "Create Requirement Group", category: "Requirements",
            description: "Creates a new Requirement Group, optionally nested under an existing parent group."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.move-group", displayName: "Move Requirement Group", category: "Requirements",
            description: "Reparents the selected Requirement Group, or makes it a root group."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.delete-group", displayName: "Delete Requirement Group", category: "Requirements",
            description: "Soft-deletes the selected Requirement Group (rejected if it still has live grouped requirements or sub-groups)."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.create-collection", displayName: "Create Requirement Collection", category: "Requirements",
            description: "Creates a new, empty Requirement Collection (a Requirement Set)."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.delete-collection", displayName: "Delete Requirement Collection", category: "Requirements",
            description: "Soft-deletes the selected Requirement Collection — never affects any member requirement."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.add-to-collection", displayName: "Add Requirement to Collection", category: "Requirements",
            description: "Adds the selected Requirement to an existing Requirement Collection."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.bulk-set-status", displayName: "Bulk Set Requirement Status", category: "Requirements",
            description: "Sets the same status on every requirement in a set."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.bulk-set-owner", displayName: "Bulk Set Requirement Owner", category: "Requirements",
            description: "Sets the same owner on every requirement in a set."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.bulk-set-priority", displayName: "Bulk Set Requirement Priority", category: "Requirements",
            description: "Sets the same priority on every requirement in a set."));
    }
}
