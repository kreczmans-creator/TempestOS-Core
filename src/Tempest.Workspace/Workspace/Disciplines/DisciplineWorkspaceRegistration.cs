using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Disciplines;

/// <summary>
/// Wires every <see cref="DisciplineCatalogue"/> discipline into a running
/// Workspace (`ADR-0163`): its Explorer area, a view and Properties
/// provider per Kind, the shared command handlers once, and a full
/// command set (create, rename, edit, delete, move, request review,
/// approve, release, set attribute) under the discipline's own category.
/// </summary>
/// <remarks>
/// The handlers act on any <see cref="DisciplineObject"/>, so they are
/// registered once; each discipline registers its own descriptors (its
/// own ids and category), so the command bar shows Structural commands
/// under Structural and so on. Must run after the Runtime Host has started.
/// </remarks>
public static class DisciplineWorkspaceRegistration
{
    /// <summary>Registers every discipline in <see cref="DisciplineCatalogue.All"/>.</summary>
    public static void Register(
        IWorkspaceManager manager, EngineeringDomainContext domainContext, ICommandDispatcher commandDispatcher, ICommandRegistry commandRegistry)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        commandDispatcher.RegisterHandler<CreateDisciplineObjectCommand>(new CreateDisciplineObjectCommandHandler(domainContext, commandDispatcher));
        commandDispatcher.RegisterHandler<RenameDisciplineObjectCommand>(new RenameDisciplineObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<ReviseDisciplineObjectCommand>(new ReviseDisciplineObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<DeleteDisciplineObjectCommand>(new DeleteDisciplineObjectCommandHandler(domainContext, commandDispatcher));
        commandDispatcher.RegisterHandler<UndeleteDisciplineObjectCommand>(new UndeleteDisciplineObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<MoveDisciplineObjectCommand>(new MoveDisciplineObjectCommandHandler(domainContext, commandDispatcher));
        commandDispatcher.RegisterHandler<SetDisciplineObjectStatusCommand>(new SetDisciplineObjectStatusCommandHandler(domainContext, commandDispatcher));
        commandDispatcher.RegisterHandler<SetDisciplineAttributeCommand>(new SetDisciplineAttributeCommandHandler(domainContext));

        foreach (var definition in DisciplineCatalogue.All)
            RegisterDiscipline(definition, manager, domainContext, commandRegistry);
    }

    /// <summary>Registers how every discipline Kind comes back after a restart.</summary>
    public static void RegisterRehydrators(IEngineeringObjectRehydratorRegistry registry, EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var kind in DisciplineCatalogue.AllKinds)
            registry.Register<DisciplineObject>(kind, context);
    }

    private static void RegisterDiscipline(
        DisciplineDefinition definition, IWorkspaceManager manager, EngineeringDomainContext domainContext, ICommandRegistry commandRegistry)
    {
        manager.RegisterExplorerArea(definition.NavigationItemId, new DisciplineNodeProvider(definition, domainContext));

        foreach (var kind in definition.Kinds)
        {
            manager.RegisterView(kind.Kind, new DisciplineWorkspaceViewFactory(kind.Kind, domainContext));
            manager.RegisterFacetProvider(kind.Kind, new DisciplinePropertyFacetProvider(kind, domainContext));
            manager.RegisterRenameFactory(kind.Kind, static (id, targetKind, name) => new RenameDisciplineObjectCommand(id, targetKind, name));
            manager.RegisterDeleteFactory(kind.Kind, static (id, targetKind) => new DeleteDisciplineObjectCommand(id, targetKind));
            manager.RegisterReviseFactory(kind.Kind, static (id, targetKind, content) => new ReviseDisciplineObjectCommand(id, targetKind, content));
        }

        var category = definition.Category;
        var noun = $"{category} object";
        var kinds = definition.KindNames;
        var attributes = definition.Kinds.SelectMany(k => k.Attributes).Distinct(StringComparer.Ordinal).ToList();

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: definition.CommandId("create"), displayName: $"Create {noun}", category: category,
            description: $"Creates a new {string.Join(", ", definition.Kinds.Select(k => k.Label.ToLowerInvariant()))} — under the selected {category} object, or the open project.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (context, values) => new CreateDisciplineObjectCommand(
                    WorkspaceCommandBindings.Canonical(kinds, values["kind"]), values["displayName"],
                    CreationPlacement.ParentFor(context, kinds)),
                [
                    WorkspaceCommandBindings.Choice("kind", "Kind", kinds, kinds[0]),
                    WorkspaceCommandBindings.ObjectName("displayName", "Name"),
                ],
                mutates: true),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: definition.CommandId("rename"), displayName: $"Rename {noun}", category: category,
            description: $"Renames the selected {noun}.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new RenameDisciplineObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind, values["newDisplayName"]),
                [WorkspaceCommandBindings.ObjectName("newDisplayName", "New name")],
                kinds,
                mutates: true),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: definition.CommandId("edit"), displayName: $"Edit {noun}", category: category,
            description: $"Records a new content revision of the selected {noun}.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new ReviseDisciplineObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind, values["newContent"]),
                [WorkspaceCommandBindings.Text("newContent", "New content")],
                kinds,
                mutates: true),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: definition.CommandId("delete"), displayName: $"Delete {noun}", category: category,
            description: $"Soft-deletes the selected {noun}.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new DeleteDisciplineObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: kinds,
                confirmationMessage: WorkspaceCommandBindings.DeleteConfirmation(noun),
                mutates: true),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: definition.CommandId("move"), displayName: $"Move {noun}", category: category,
            description: $"Reparents the selected {noun}.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new MoveDisciplineObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId,
                    WorkspaceCommandBindings.Target(context).Kind,
                    WorkspaceCommandBindings.ParseDestination(values["destinationId"])),
                [WorkspaceCommandBindings.Destination("destinationId", "Destination")],
                kinds,
                mutates: true),
        });
        // The platform lifecycle (`LifecycleTransitionTable`): Draft →
        // In review → Approved → Released, one command per step.
        foreach (var (verb, displayName, status) in new[]
                 {
                     ("request-review", "Request review", LifecycleState.InReview),
                     ("approve", "Approve", LifecycleState.Approved),
                     ("release", "Release", LifecycleState.Released),
                 })
        {
            commandRegistry.RegisterDescriptor(new CommandDescriptor(
                id: definition.CommandId(verb), displayName: displayName, category: category,
                description: $"Transitions the selected {noun}'s status to {status}.")
            {
                Binding = StatusBinding(status, kinds),
            });
        }
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: definition.CommandId("set-attribute"), displayName: "Set attribute", category: category,
            description: $"Records one attribute of the selected {noun} (blank clears it). Values are recorded as typed; nothing is checked against a design code.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new SetDisciplineAttributeCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId,
                    WorkspaceCommandBindings.Target(context).Kind,
                    WorkspaceCommandBindings.Canonical(attributes, values["attribute"]),
                    WorkspaceCommandBindings.OrNull(values["value"])),
                [
                    WorkspaceCommandBindings.Choice("attribute", "Attribute", attributes, attributes[0]),
                    WorkspaceCommandBindings.Text("value", "Value"),
                ],
                kinds,
                mutates: true),
        });
    }

    private static CommandBinding StatusBinding(LifecycleState status, IReadOnlyList<string> appliesToKinds) =>
        new(CommandContextRequirement.SelectedObject,
            (context, _) => new SetDisciplineObjectStatusCommand(
                WorkspaceCommandBindings.Target(context).ObjectId,
                WorkspaceCommandBindings.Target(context).Kind,
                status),
            appliesToKinds: appliesToKinds,
            mutates: true);
}
