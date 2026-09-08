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

        // TD-77 Stage 3 — descriptor binding. Every binding below is a
        // hand-written lambda closing over the same constructor the handler
        // registered above already expects; nothing here dispatches, and
        // nothing reaches a handler except through the registry's own
        // CommandHandlerTable path. Unlike the other five disciplines, the
        // three Requirements Kinds each have their own dedicated commands,
        // so a binding's own Kind scope is the one Kind its command acts on
        // — never this class's own three-entry SupportedKinds.

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.create", displayName: "Create Requirement", category: "Requirements",
            description: "Creates a new Requirement with a business identifier and statement.")
        {
            // Both prompts the Ribbon's own Create flow already collects, with the
            // identical "an identifier is required"/"a statement is required"
            // rules. Category stays at the command's own optional default.
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (_, values) => new CreateRequirementCommand(values["identifier"], values["statement"]),
                [
                    WorkspaceCommandBindings.Required("identifier", "Identifier (e.g. REQ-001)"),
                    WorkspaceCommandBindings.Required("statement", "Statement"),
                ]),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.revise", displayName: "Revise Requirement", category: "Requirements",
            description: "Records a new revision of the selected Requirement's own statement.")
        {
            // A Requirement's own mutable field is its Statement — this discipline
            // has no Rename/Content concept (this class's own remarks). Bound for
            // the Palette and every other future Id-based consumer; the Ribbon
            // routes "edit" to the Object Editor before it reads a binding.
            // ChangeSummary stays at the command's own optional default.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new ReviseRequirementCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, values["newStatement"]),
                [WorkspaceCommandBindings.Required("newStatement", "New statement")],
                RequirementKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.set-status", displayName: "Set Requirement Status", category: "Requirements",
            description: "Sets the selected Requirement's own current lifecycle status.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new SetRequirementStatusCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId,
                    Enum.Parse<RequirementStatus>(values["status"], ignoreCase: true)),
                [WorkspaceCommandBindings.EnumChoice<RequirementStatus>("status", "New status")],
                RequirementKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.set-owner", displayName: "Set Requirement Owner", category: "Requirements",
            description: "Sets the selected Requirement's own current owner.")
        {
            // Owner is nullable on the command and unvalidated in the Ribbon's own
            // prompt; left blank it reaches the constructor as the null that
            // already means "no owner".
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new SetRequirementOwnerCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.OrNull(values["owner"])),
                [WorkspaceCommandBindings.Text("owner", "Owner")],
                RequirementKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.set-priority", displayName: "Set Requirement Priority", category: "Requirements",
            description: "Sets the selected Requirement's own current priority.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new SetRequirementPriorityCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId,
                    Enum.Parse<RequirementPriority>(values["priority"], ignoreCase: true)),
                [WorkspaceCommandBindings.EnumChoice<RequirementPriority>("priority", "Priority")],
                RequirementKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.delete", displayName: "Delete Requirement", category: "Requirements",
            description: "Soft-deletes the selected Requirement.")
        {
            // The confirmation is what keeps a delete out of an unattended macro.
            // Ribbon deletion is untouched: it never reaches a binding, and still
            // clears selection on success through its own
            // WorkspaceManager.DeleteObjectAsync path.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new DeleteRequirementCommand(WorkspaceCommandBindings.Target(context).ObjectId),
                appliesToKinds: RequirementKinds,
                confirmationMessage: WorkspaceCommandBindings.DeleteConfirmation("Requirement")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.move", displayName: "Move Requirement", category: "Requirements",
            description: "Moves the selected Requirement into a different group, or ungroups it.")
        {
            Binding = CommandBinding.Unavailable(
                WorkspaceCommandBindings.ObjectPickerRequired("Moving a Requirement needs a destination Requirement Group chosen from the object tree")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.duplicate", displayName: "Duplicate Requirement", category: "Requirements",
            description: "Creates a copy of the selected Requirement's own Statement/Category/Priority/Group under a new identifier.")
        {
            // Alone among the six disciplines' Duplicate commands, this one takes a
            // required new identifier rather than defaulting it — the Ribbon's own
            // dedicated handler collects it for the same reason.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new DuplicateRequirementCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, values["newIdentifier"]),
                [WorkspaceCommandBindings.Required("newIdentifier", "New identifier for the duplicate")],
                RequirementKinds,
                WorkspaceCommandBindings.DuplicateConfirmation("Requirement")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.link", displayName: "Link Requirement", category: "Requirements",
            description: "Records a typed relationship from the selected Requirement to another document — allocation, dependency, derivation, reference, or satisfaction.")
        {
            Binding = CommandBinding.Unavailable(
                WorkspaceCommandBindings.ObjectPickerRequired("Linking a Requirement needs a target object chosen from the object tree")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.create-group", displayName: "Create Requirement Group", category: "Requirements",
            description: "Creates a new Requirement Group, optionally nested under an existing parent group.")
        {
            // ParentGroupId stays at the command's own optional default: nesting a
            // new group under an existing one would need a group chosen from the
            // tree, which is the same missing capability "requirements.move-group"
            // declares.
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (_, values) => new CreateRequirementGroupCommand(values["name"]),
                [WorkspaceCommandBindings.Required("name", "Name for the new group")]),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.move-group", displayName: "Move Requirement Group", category: "Requirements",
            description: "Reparents the selected Requirement Group, or makes it a root group.")
        {
            Binding = CommandBinding.Unavailable(
                WorkspaceCommandBindings.ObjectPickerRequired("Moving a Requirement Group needs a destination parent Group chosen from the object tree")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.delete-group", displayName: "Delete Requirement Group", category: "Requirements",
            description: "Soft-deletes the selected Requirement Group (rejected if it still has live grouped requirements or sub-groups).")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new DeleteRequirementGroupCommand(WorkspaceCommandBindings.Target(context).ObjectId),
                appliesToKinds: [RequirementsService.RequirementGroupDocumentKind],
                confirmationMessage: WorkspaceCommandBindings.DeleteConfirmation("Requirement Group")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.create-collection", displayName: "Create Requirement Collection", category: "Requirements",
            description: "Creates a new, empty Requirement Collection (a Requirement Set).")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (_, values) => new CreateRequirementCollectionCommand(values["name"]),
                [WorkspaceCommandBindings.Required("name", "Name for the new collection")]),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.delete-collection", displayName: "Delete Requirement Collection", category: "Requirements",
            description: "Soft-deletes the selected Requirement Collection — never affects any member requirement.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new DeleteRequirementCollectionCommand(WorkspaceCommandBindings.Target(context).ObjectId),
                appliesToKinds: [RequirementsService.RequirementCollectionDocumentKind],
                confirmationMessage: WorkspaceCommandBindings.DeleteConfirmation("Requirement Collection")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.add-to-collection", displayName: "Add Requirement to Collection", category: "Requirements",
            description: "Adds the selected Requirement to an existing Requirement Collection.")
        {
            Binding = CommandBinding.Unavailable(
                WorkspaceCommandBindings.ObjectPickerRequired("Adding a Requirement to a Collection needs the target Collection chosen from the object tree")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.bulk-set-status", displayName: "Bulk Set Requirement Status", category: "Requirements",
            description: "Sets the same status on every requirement in a set.")
        {
            // The whole ordered selection, not just the primary — which is exactly
            // what MultipleAllowed declares, and without it Evaluate would refuse a
            // multi-selection rather than let this command act on one.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject | CommandContextRequirement.MultipleAllowed,
                (context, values) => new BulkSetRequirementStatusCommand(
                    WorkspaceCommandBindings.SelectedIds(context),
                    Enum.Parse<RequirementStatus>(values["status"], ignoreCase: true)),
                [WorkspaceCommandBindings.EnumChoice<RequirementStatus>("status", "New status")],
                RequirementKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.bulk-set-owner", displayName: "Bulk Set Requirement Owner", category: "Requirements",
            description: "Sets the same owner on every requirement in a set.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject | CommandContextRequirement.MultipleAllowed,
                (context, values) => new BulkSetRequirementOwnerCommand(
                    WorkspaceCommandBindings.SelectedIds(context), WorkspaceCommandBindings.OrNull(values["owner"])),
                [WorkspaceCommandBindings.Text("owner", "Owner")],
                RequirementKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "requirements.bulk-set-priority", displayName: "Bulk Set Requirement Priority", category: "Requirements",
            description: "Sets the same priority on every requirement in a set.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject | CommandContextRequirement.MultipleAllowed,
                (context, values) => new BulkSetRequirementPriorityCommand(
                    WorkspaceCommandBindings.SelectedIds(context),
                    Enum.Parse<RequirementPriority>(values["priority"], ignoreCase: true)),
                [WorkspaceCommandBindings.EnumChoice<RequirementPriority>("priority", "Priority")],
                RequirementKinds),
        });
    }
    /// <summary>
    /// The single Kind every Requirement-scoped command acts on. Held here
    /// rather than inline so the scope is stated once and can be asserted
    /// directly; <c>RequirementGroup</c>/<c>RequirementCollection</c> have
    /// their own dedicated commands and their own separate scopes.
    /// </summary>
    internal static readonly IReadOnlyList<string> RequirementKinds =
        [RequirementsService.RequirementDocumentKind];

}
