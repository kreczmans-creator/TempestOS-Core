using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Verification;

namespace Tempest.App.Workspace.Verification;

/// <summary>
/// The single composition-root entry point wiring the whole Verification
/// Management discipline into a running Workspace — everything
/// <c>Program.cs</c> needs, kept out of <c>Program.cs</c> itself, mirroring
/// <see cref="Mechanical.MechanicalWorkspaceRegistration"/>/
/// <see cref="Requirements.RequirementsWorkspaceRegistration"/>/
/// <see cref="Calculations.CalculationsWorkspaceRegistration"/>/
/// <see cref="Documents.DocumentsWorkspaceRegistration"/>'s own identical
/// shape (`WP 9.3A`, the fifth real Engineering discipline wired this
/// way).
/// </summary>
/// <remarks>
/// Must run <em>after</em> the Runtime Host has started, exactly like
/// every prior real discipline — every piece here needs
/// <see cref="EngineeringDomainContext"/>/<see cref="IVerificationService"/>/
/// <see cref="ICommandDispatcher"/>/<see cref="ICommandRegistry"/>, all
/// resolvable only once <c>ITempestHost.Services</c> is populated.
/// </remarks>
public static class VerificationWorkspaceRegistration
{
    /// <summary>The one Verification Kind this Work Package registers a View and a Property Facet Provider for.</summary>
    public static readonly IReadOnlyList<string> SupportedKinds = [VerificationActivityFactoryRegistry.SupportedKind];

    /// <summary>Registers every Verification Management Workspace extension point.</summary>
    public static void Register(
        IWorkspaceManager manager, EngineeringDomainContext domainContext, IVerificationService verificationService,
        ICommandDispatcher commandDispatcher, ICommandRegistry commandRegistry)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(verificationService);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        manager.RegisterExplorerArea(
            VerificationWorkspaceExplorerModule.NavigationItemId,
            new VerificationActivityNodeProvider(VerificationWorkspaceExplorerModule.NavigationItemId, domainContext));

        foreach (var kind in SupportedKinds)
        {
            manager.RegisterView(kind, new VerificationActivityWorkspaceViewFactory(kind, domainContext));
            manager.RegisterFacetProvider(kind, new VerificationActivityPropertyFacetProvider(kind, domainContext));

            // WP 10.2A (ADR-0096): real rename/delete dispatch, mirroring Mechanical's own identical wiring.
            manager.RegisterRenameFactory(kind, static (id, targetKind, name) => new RenameVerificationActivityCommand(id, targetKind, name));
            manager.RegisterDeleteFactory(kind, static (id, targetKind) => new DeleteVerificationActivityCommand(id, targetKind));

            // WP 10.3A (ADR-0097): real revise dispatch — reused directly by
            // Manufacturing's own "Inspection" Kind below (identical
            // rename/delete reuse pattern, `WP 10.2A`).
            manager.RegisterReviseFactory(kind, static (id, targetKind, content) => new ReviseVerificationActivityCommand(id, targetKind, content));
        }

        var factoryRegistry = new VerificationActivityFactoryRegistry(domainContext);
        var copyHandler = new CopyVerificationActivityCommandHandler(domainContext, factoryRegistry);

        commandDispatcher.RegisterHandler<CreateVerificationActivityCommand>(new CreateVerificationActivityCommandHandler(factoryRegistry));
        commandDispatcher.RegisterHandler<RenameVerificationActivityCommand>(new RenameVerificationActivityCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<ReviseVerificationActivityCommand>(new ReviseVerificationActivityCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<DeleteVerificationActivityCommand>(new DeleteVerificationActivityCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<MoveVerificationActivityCommand>(new MoveVerificationActivityCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<CopyVerificationActivityCommand>(copyHandler);
        commandDispatcher.RegisterHandler<DuplicateVerificationActivityCommand>(new DuplicateVerificationActivityCommandHandler(domainContext, copyHandler));
        commandDispatcher.RegisterHandler<SetVerificationActivityStatusCommand>(new SetVerificationActivityStatusCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<RecordVerificationResultCommand>(new RecordVerificationResultCommandHandler(verificationService));

        // TD-77 Stage 3 — descriptor binding. Every binding below is a
        // hand-written lambda closing over the same constructor the handler
        // registered above already expects; nothing here dispatches, and
        // nothing reaches a handler except through the registry's own
        // CommandHandlerTable path. Kind restrictions are this discipline's
        // own already-declared SupportedKinds, unchanged.
        var boundKinds = SupportedKinds;

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.create", displayName: "Create Verification Activity", category: "Verification",
            description: "Creates a new Verification Activity — a Verification Plan until a result is recorded.")
        {
            // Creating a Verification Activity genuinely means "verify the object I
            // have selected": SubjectId is the current selection's own Id, never a
            // fabricated one, which is why this Create — alone among the six —
            // requires a selected object. Method keeps the Ribbon's and the Object
            // Editor's own identical existing default; ParentId and InitialContent
            // stay at the command's own optional defaults.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new CreateVerificationActivityCommand(
                    values["displayName"], WorkspaceCommandBindings.Target(context).ObjectId, values["method"]),
                [
                    WorkspaceCommandBindings.ObjectName("displayName", "Name"),
                    // The default method value is written inline rather than
                    // held as a named constant: "Inspection" is a canonically
                    // owned vocabulary value (ManufacturingObjectFactoryRegistry's
                    // own Inspection Kind), and ADR-0105 allows exactly one
                    // named owner per value. The Ribbon's own prompt and the
                    // Object Editor's own record-result path already spell
                    // this default the same, literal way, for the same reason.
                    WorkspaceCommandBindings.Required("method", "Method", "Inspection"),
                ]),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.rename", displayName: "Rename Verification Activity", category: "Verification",
            description: "Renames the selected Verification Activity.")
        {
            // Bound for the Palette and every other future Id-based consumer.
            // The Ribbon still routes "rename"/"edit" to the Object Editor before
            // it ever reads a binding (RibbonView's own verb branch).
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new RenameVerificationActivityCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind, values["newDisplayName"]),
                [WorkspaceCommandBindings.ObjectName("newDisplayName", "New name")],
                boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.edit", displayName: "Edit Verification Activity", category: "Verification",
            description: "Records a new content revision of the selected Verification Activity.")
        {
            // ChangeSummary stays at the command's own optional default.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new ReviseVerificationActivityCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind, values["newContent"]),
                [WorkspaceCommandBindings.Text("newContent", "New content")],
                boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.delete", displayName: "Delete Verification Activity", category: "Verification",
            description: "Soft-deletes the selected Verification Activity.")
        {
            // The confirmation is what keeps a soft-delete out of an unattended
            // macro. Ribbon deletion is untouched: it never reaches a binding, and
            // still clears selection on success through its own
            // WorkspaceManager.DeleteObjectAsync path.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new DeleteVerificationActivityCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: boundKinds,
                confirmationMessage: WorkspaceCommandBindings.DeleteConfirmation("Verification Activity")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.move", displayName: "Move Verification Activity", category: "Verification",
            description: "Reparents the selected Verification Activity.")
        {
            Binding = CommandBinding.Unavailable(
                WorkspaceCommandBindings.ObjectPickerRequired("Moving a Verification Activity needs a destination parent chosen from the object tree")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.copy", displayName: "Copy Verification Activity", category: "Verification",
            description: "Creates a copy of the selected object under a chosen target parent.")
        {
            Binding = CommandBinding.Unavailable(
                WorkspaceCommandBindings.ObjectPickerRequired("Copying a Verification Activity needs a destination parent chosen from the object tree")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.duplicate", displayName: "Duplicate Verification Activity", category: "Verification",
            description: "Creates a copy of the selected object under its own current parent.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new DuplicateVerificationActivityCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: boundKinds,
                confirmationMessage: WorkspaceCommandBindings.DuplicateConfirmation("Verification Activity")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.record-result", displayName: "Record Verification Result", category: "Verification",
            description: "Records a real IVerificationRecord (Pass/Fail/Conditional, criteria, evidence) against the selected Verification Activity — this Work Package's own realisation of Execute/Record Result/Attach Evidence together (ADR-0089).")
        {
            // This discipline's own binding for RecordVerificationResultCommand,
            // scoped to its own "VerificationActivity" Kind — independent of, and
            // carrying different Kinds from, Manufacturing's own
            // "manufacturing.record-inspection-result" binding over the same
            // command type. A binding belongs to a descriptor, not to a command.
            // Every optional collection (criteria, evidence, linked documents and
            // calculation records, referenced materials) stays at the command's own
            // default, exactly as the Object Editor's own record-result path
            // already leaves them.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new RecordVerificationResultCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId,
                    WorkspaceCommandBindings.Target(context).Kind,
                    Enum.Parse<VerificationOutcome>(values["outcome"], ignoreCase: true),
                    values["method"]),
                [
                    WorkspaceCommandBindings.EnumChoice<VerificationOutcome>("outcome", "Outcome"),
                    WorkspaceCommandBindings.Required("method", "Method", "Inspection"),
                ],
                boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.request-review", displayName: "Request Review", category: "Verification",
            description: "Transitions the selected Verification Activity's own status to InReview (SetVerificationActivityStatusCommand).")
        {
            Binding = StatusBinding(LifecycleState.InReview, boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.approve", displayName: "Approve Verification Activity", category: "Verification",
            description: "Transitions the selected Verification Activity's own status to Approved (SetVerificationActivityStatusCommand).")
        {
            Binding = StatusBinding(LifecycleState.Approved, boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.archive", displayName: "Archive Verification Activity", category: "Verification",
            description: "Transitions the selected Verification Activity's own status to Archived, a terminal state (SetVerificationActivityStatusCommand).")
        {
            Binding = StatusBinding(LifecycleState.Archived, boundKinds),
        });
    }
    /// <summary>
    /// The one binding shape the three Verification status transitions
    /// share — <c>SetVerificationActivityStatusCommand</c>'s own
    /// constructor, closed over the fixed <see cref="LifecycleState"/> each
    /// descriptor transitions to. Declares no parameter and no
    /// confirmation, which is precisely what makes these three
    /// macro-eligible.
    /// </summary>
    private static CommandBinding StatusBinding(LifecycleState status, IReadOnlyList<string> appliesToKinds) =>
        new(CommandContextRequirement.SelectedObject,
            (context, _) => new SetVerificationActivityStatusCommand(
                WorkspaceCommandBindings.Target(context).ObjectId,
                WorkspaceCommandBindings.Target(context).Kind,
                status),
            appliesToKinds: appliesToKinds);

}
