using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Samples;

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
        }

        var factoryRegistry = new MechanicalObjectFactoryRegistry(domainContext);
        var copyHandler = new CopyMechanicalObjectCommandHandler(domainContext, factoryRegistry);

        commandDispatcher.RegisterHandler<CreateMechanicalObjectCommand>(new CreateMechanicalObjectCommandHandler(factoryRegistry));
        commandDispatcher.RegisterHandler<RenameMechanicalObjectCommand>(new RenameMechanicalObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<DeleteMechanicalObjectCommand>(new DeleteMechanicalObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<MoveMechanicalObjectCommand>(new MoveMechanicalObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<CopyMechanicalObjectCommand>(copyHandler);
        commandDispatcher.RegisterHandler<DuplicateMechanicalObjectCommand>(new DuplicateMechanicalObjectCommandHandler(domainContext, copyHandler));
        commandDispatcher.RegisterHandler<SetBomLineCommand>(new SetBomLineCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<CompareBaselinesCommand>(new CompareBaselinesCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<ValidateConfigurationCommand>(new ValidateConfigurationCommandHandler(domainContext, referenceIntegrityChecker));

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "mechanical.create", displayName: "Create Mechanical Object", category: "Mechanical",
            description: "Creates a new Project, Assembly, Sub-Assembly, Part, or Component."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "mechanical.rename", displayName: "Rename Mechanical Object", category: "Mechanical",
            description: "Renames the selected Mechanical Product Structure object."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "mechanical.delete", displayName: "Delete Mechanical Object", category: "Mechanical",
            description: "Soft-deletes the selected Mechanical Product Structure object (rejected if it still has live children)."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "mechanical.move", displayName: "Move Mechanical Object", category: "Mechanical",
            description: "Reparents the selected Mechanical Product Structure object."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "mechanical.copy", displayName: "Copy Mechanical Object", category: "Mechanical",
            description: "Creates a copy of the selected object under a chosen target parent."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "mechanical.duplicate", displayName: "Duplicate Mechanical Object", category: "Mechanical",
            description: "Creates a copy of the selected object under its own current parent."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "mechanical.set-bom-line", displayName: "Set BOM Line", category: "Mechanical",
            description: "Sets the selected object's own Quantity, Unit of Measure, Find Number, Item Number, and Reference Designator."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "mechanical.compare-baselines", displayName: "Compare Baselines", category: "Mechanical",
            description: "Compares two Configuration/Baseline/Release objects' own member revisions — added, removed, revision-changed."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "mechanical.validate-configuration", displayName: "Validate Configuration", category: "Mechanical",
            description: "Checks a Baseline/Release's own member consistency (every member exists, at the referenced revision)."));
    }
}
