using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Mechanical;

/// <summary>
/// The single composition-root entry point wiring the whole Mechanical
/// Product Structure discipline into a running Workspace — everything
/// `Program.cs` needs, kept out of `Program.cs` itself the same way
/// `WorkspaceManager` already keeps generic Workspace wiring out of it.
/// </summary>
/// <remarks>
/// <para>
/// Must run <em>after</em> the Runtime Host has started (unlike the
/// Sample area registrations `Program.cs` makes before starting): every
/// piece here needs <see cref="EngineeringDomainContext"/>/<see cref="ICommandDispatcher"/>/
/// <see cref="ICommandRegistry"/>, all three only resolvable once
/// <c>ITempestHost.Services</c> is populated. This is a disclosed,
/// genuine first: no prior Workspace registration ever needed a running
/// Host, since none read the Engineering Domain before. Still a
/// composition-root registration, not a Host-discovered module one
/// (`ADR-0071`) — only <em>when</em> within `Program.cs` it runs is new.
/// </para>
/// <para>
/// <c>createDefault</c> is deliberately omitted from every descriptor
/// below: none of these nine commands has a meaningful parameterless
/// default in a shell with no pre-selected object context, so none can be
/// invoked by bare Id through <see cref="ICommandRegistry.InvokeAsync"/>
/// today — they are still registered and listed (this Work Package's own
/// "Command Palette integration" requirement: discoverable, described,
/// categorised), and dispatched with real data through
/// <see cref="ICommandDispatcher.DispatchAsync{TCommand}"/> by a caller
/// that already has it (a future context-menu action).
/// </para>
/// </remarks>
public static class MechanicalWorkspaceRegistration
{
    /// <summary>Registers every Mechanical Product Structure Workspace extension point.</summary>
    public static void Register(
        IWorkspaceManager manager, EngineeringDomainContext domainContext, ICommandDispatcher commandDispatcher,
        ICommandRegistry commandRegistry, IReferenceIntegrityChecker referenceIntegrityChecker)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);
        ArgumentNullException.ThrowIfNull(referenceIntegrityChecker);

        manager.RegisterExplorerArea(
            MechanicalWorkspaceExplorerModule.NavigationItemId,
            new MechanicalProductStructureNodeProvider(MechanicalWorkspaceExplorerModule.NavigationItemId, domainContext));

        foreach (var kind in MechanicalObjectFactoryRegistry.SupportedKinds)
        {
            manager.RegisterView(kind, new MechanicalWorkspaceViewFactory(kind, domainContext));
            manager.RegisterFacetProvider(kind, new MechanicalPropertyFacetProvider(kind, domainContext));

            // WP 10.2A (ADR-0096): the real, generic rename/delete dispatch
            // path IWorkspaceCommand/RenameMechanicalObjectCommand's own
            // remarks already anticipated ("a future context-menu action") -
            // the Project Explorer's own inline rename and context menu.
            manager.RegisterRenameFactory(kind, static (id, targetKind, name) => new RenameMechanicalObjectCommand(id, targetKind, name));
            manager.RegisterDeleteFactory(kind, static (id, targetKind) => new DeleteMechanicalObjectCommand(id, targetKind));

            // WP 10.3A (ADR-0097): real revise dispatch — the Object Editor
            // Framework's own Content field. ReviseMechanicalObjectCommand
            // is this Work Package's own new command, the one discipline of
            // six that had none before it (this class's own remarks).
            manager.RegisterReviseFactory(kind, static (id, targetKind, content) => new ReviseMechanicalObjectCommand(id, targetKind, content));
        }

        var factoryRegistry = new MechanicalObjectFactoryRegistry(domainContext);
        var copyHandler = new CopyMechanicalObjectCommandHandler(domainContext, factoryRegistry);

        commandDispatcher.RegisterHandler<CreateMechanicalObjectCommand>(new CreateMechanicalObjectCommandHandler(factoryRegistry));
        commandDispatcher.RegisterHandler<RenameMechanicalObjectCommand>(new RenameMechanicalObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<ReviseMechanicalObjectCommand>(new ReviseMechanicalObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<DeleteMechanicalObjectCommand>(new DeleteMechanicalObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<MoveMechanicalObjectCommand>(new MoveMechanicalObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<CopyMechanicalObjectCommand>(copyHandler);
        commandDispatcher.RegisterHandler<DuplicateMechanicalObjectCommand>(new DuplicateMechanicalObjectCommandHandler(domainContext, copyHandler));
        commandDispatcher.RegisterHandler<SetBomLineCommand>(new SetBomLineCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<CompareBaselinesCommand>(new CompareBaselinesCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<ValidateConfigurationCommand>(new ValidateConfigurationCommandHandler(domainContext, referenceIntegrityChecker));

        // TD-77 Stage 3 — descriptor binding. Every binding below is a
        // hand-written lambda closing over the same constructor the handler
        // registered above already expects; nothing here dispatches, and
        // nothing reaches a handler except through the registry's own
        // CommandHandlerTable path. This is also what this class's own
        // remarks anticipated: those nine commands are no longer reachable
        // only through ICommandDispatcher.DispatchAsync.
        var boundKinds = MechanicalObjectFactoryRegistry.SupportedKinds;

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "mechanical.create", displayName: "Create Mechanical Object", category: "Mechanical",
            description: "Creates a new Project, Assembly, Sub-Assembly, Part, or Component.")
        {
            // Kind is offered as this discipline's own already-declared
            // SupportedKinds constant, defaulted to the Ribbon's own existing
            // "Part" default. A SubAssembly additionally requires a parent
            // Assembly Id, which no collected value can carry; asked for one, the
            // factory reports its own precise reason through the normal handler
            // path rather than failing silently.
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (_, values) => new CreateMechanicalObjectCommand(
                    WorkspaceCommandBindings.Canonical(boundKinds, values["kind"]), values["displayName"]),
                [
                    WorkspaceCommandBindings.Choice("kind", "Kind", boundKinds, MechanicalObjectFactoryRegistry.Part),
                    WorkspaceCommandBindings.ObjectName("displayName", "Name"),
                ]),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "mechanical.rename", displayName: "Rename Mechanical Object", category: "Mechanical",
            description: "Renames the selected Mechanical Product Structure object.")
        {
            // Bound for the Palette and every other future Id-based consumer.
            // The Ribbon still routes "rename"/"edit" to the Object Editor before
            // it ever reads a binding (RibbonView's own verb branch).
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new RenameMechanicalObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind, values["newDisplayName"]),
                [WorkspaceCommandBindings.ObjectName("newDisplayName", "New name")],
                boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "mechanical.edit", displayName: "Edit Mechanical Object", category: "Mechanical",
            description: "Records a new content revision of the selected Mechanical Product Structure object.")
        {
            // ChangeSummary stays at the command's own optional default.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new ReviseMechanicalObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind, values["newContent"]),
                [WorkspaceCommandBindings.Text("newContent", "New content")],
                boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "mechanical.delete", displayName: "Delete Mechanical Object", category: "Mechanical",
            description: "Soft-deletes the selected Mechanical Product Structure object (rejected if it still has live children).")
        {
            // The confirmation is what keeps a soft-delete out of an unattended
            // macro. Ribbon deletion is untouched: it never reaches a binding, and
            // still clears selection on success through its own
            // WorkspaceManager.DeleteObjectAsync path.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new DeleteMechanicalObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: boundKinds,
                confirmationMessage: WorkspaceCommandBindings.DeleteConfirmation("Mechanical object")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "mechanical.move", displayName: "Move Mechanical Object", category: "Mechanical",
            description: "Reparents the selected Mechanical Product Structure object.")
        {
            Binding = CommandBinding.Unavailable(
                WorkspaceCommandBindings.ObjectPickerRequired("Moving a Mechanical object needs a destination parent chosen from the object tree")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "mechanical.copy", displayName: "Copy Mechanical Object", category: "Mechanical",
            description: "Creates a copy of the selected object under a chosen target parent.")
        {
            Binding = CommandBinding.Unavailable(
                WorkspaceCommandBindings.ObjectPickerRequired("Copying a Mechanical object needs a destination parent chosen from the object tree")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "mechanical.duplicate", displayName: "Duplicate Mechanical Object", category: "Mechanical",
            description: "Creates a copy of the selected object under its own current parent.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new DuplicateMechanicalObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: boundKinds,
                confirmationMessage: WorkspaceCommandBindings.DuplicateConfirmation("Mechanical object")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "mechanical.set-bom-line", displayName: "Set BOM Line", category: "Mechanical",
            description: "Sets the selected object's own Quantity, Unit of Measure, Find Number, Item Number, and Reference Designator.")
        {
            // Quantity is a decimal, so it is validated as one before Build runs
            // rather than parsed inside it — a throw out of a build lambda is a
            // defect, not an outcome (CommandBinding's own remarks). The remaining
            // four are the command's own optional strings and stay optional in
            // meaning: left blank, each reaches the constructor as the null it
            // already documents as "leave it unset".
            //
            // Scoped to the four Kinds whose own contracts declare IHasBomLine
            // (IAssembly — which ISubAssembly extends — IPart and IComponent). A
            // Project/Configuration/Baseline/Release carries no BOM line of its
            // own, and SetBomLineCommandHandler already says so.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new SetBomLineCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId,
                    WorkspaceCommandBindings.Target(context).Kind,
                    WorkspaceCommandBindings.ParseDecimal(values["quantity"])!.Value,
                    WorkspaceCommandBindings.OrNull(values["unitOfMeasure"]),
                    WorkspaceCommandBindings.OrNull(values["findNumber"]),
                    WorkspaceCommandBindings.OrNull(values["itemNumber"]),
                    WorkspaceCommandBindings.OrNull(values["referenceDesignator"])),
                [
                    WorkspaceCommandBindings.Decimal("quantity", "Quantity"),
                    WorkspaceCommandBindings.Text("unitOfMeasure", "Unit of measure"),
                    WorkspaceCommandBindings.Text("findNumber", "Find number"),
                    WorkspaceCommandBindings.Text("itemNumber", "Item number"),
                    WorkspaceCommandBindings.Text("referenceDesignator", "Reference designator"),
                ],
                BomLineKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "mechanical.compare-baselines", displayName: "Compare Baselines", category: "Mechanical",
            description: "Compares two Configuration/Baseline/Release objects' own member revisions — added, removed, revision-changed.")
        {
            // Two objects, and a context carries one selection whose first entry is
            // the primary — the second Baseline/Release has nowhere to come from.
            Binding = CommandBinding.Unavailable(
                WorkspaceCommandBindings.ObjectPickerRequired("Comparing baselines needs a second Baseline or Release chosen from the object tree")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "mechanical.validate-configuration", displayName: "Validate Configuration", category: "Mechanical",
            description: "Checks a Baseline/Release's own member consistency (every member exists, at the referenced revision).")
        {
            // Needs only the selection: no parameter, no confirmation, and no
            // mutation. The one non-status command in this platform that can run
            // unattended in a macro (ADR-0098). Scoped to the two Kinds that
            // satisfy IBaseline — ValidateConfigurationCommand's own remarks
            // already record that a plain, working Configuration does not.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new ValidateConfigurationCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: [MechanicalObjectFactoryRegistry.Baseline, MechanicalObjectFactoryRegistry.Release]),
        });
    }
    /// <summary>
    /// The Mechanical Kinds that carry a Bill of Materials line — the four
    /// whose own contracts declare <c>IHasBomLine</c>
    /// (<c>IAssembly</c>, which <c>ISubAssembly</c> extends, <c>IPart</c>
    /// and <c>IComponent</c>). Held here rather than inline so
    /// <c>"mechanical.set-bom-line"</c>'s own scope is stated once and can
    /// be asserted directly.
    /// </summary>
    internal static readonly IReadOnlyList<string> BomLineKinds =
        [MechanicalObjectFactoryRegistry.Assembly, MechanicalObjectFactoryRegistry.SubAssembly, MechanicalObjectFactoryRegistry.Part, MechanicalObjectFactoryRegistry.Component];

}
