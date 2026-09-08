using Tempest.App.Workspace.Documents;
using Tempest.App.Workspace.Verification;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

using Tempest.Core.Verification;
namespace Tempest.App.Workspace.Manufacturing;

/// <summary>
/// The single composition-root entry point wiring the whole Manufacturing
/// discipline into a running Workspace — everything <c>Program.cs</c>
/// needs, kept out of <c>Program.cs</c> itself, mirroring every prior
/// real-discipline Work Package's own identical shape (`WP 9.5A`, the
/// sixth real Engineering discipline wired this way).
/// </summary>
/// <remarks>
/// <para>
/// <b>A disclosed, deliberate first for this project — genuine
/// cross-Work-Package read-side reuse.</b> <c>"WorkInstruction"</c>
/// (a real <see cref="Documents.IDocument"/> subtype, `WP 8.2C`) is
/// registered here with <see cref="Documents.DocumentsPropertyFacetProvider"/>/
/// <see cref="Documents.DocumentsWorkspaceView"/>/
/// <see cref="Documents.DocumentsWorkspaceViewFactory"/> **directly**,
/// constructed with <c>kind: "WorkInstruction"</c> — both types are
/// already generic over their own <see cref="IPropertyFacetProvider.Kind"/>/
/// <see cref="IWorkspaceViewFactory.Kind"/> parameter (confirmed by direct
/// read of `WP 9.4A`'s own source), so zero new facet/view code is written
/// for it. <c>"Inspection"</c> (a real
/// <see cref="Tempest.Core.EngineeringDomain.IVerificationActivity"/>
/// subtype, `WP 8.2C`) is registered the identical way, reusing
/// <see cref="Verification.VerificationActivityPropertyFacetProvider"/>/
/// <see cref="Verification.VerificationActivityWorkspaceView"/>/
/// <see cref="Verification.VerificationActivityWorkspaceViewFactory"/>
/// directly, constructed with <c>kind: "Inspection"</c>. Recording an
/// Inspection's own result reuses
/// <see cref="Verification.RecordVerificationResultCommand"/>/
/// <see cref="Verification.RecordVerificationResultCommandHandler"/>
/// directly — already Kind-agnostic (dispatches through
/// <see cref="Tempest.Core.Verification.IVerificationService.RecordAsync"/>
/// by Id alone, never checking the target's own Kind string).
/// </para>
/// <para>
/// <b>Commands remain this Work Package's own</b>, never reused from
/// Documents/Verification, mirroring every prior Work Package's own
/// established pattern of a fresh, thin command set per discipline — a
/// deliberate asymmetry (read-side reuse, write-side fresh commands),
/// disclosed in `WP9.5A Implementation Report.md`: reused commands would
/// show a `"Documents"`/`"Verification"` Command Palette category for a
/// Manufacturing object, and `Documents.DocumentObjectFactoryRegistry`/
/// `Verification.VerificationActivityFactoryRegistry`'s own Create
/// machinery cannot construct a <c>"WorkInstruction"</c>/<c>"Inspection"</c>
/// at all (both require Manufacturing-specific fields —
/// <see cref="Tempest.Core.EngineeringDomain.IWorkInstruction.ManufacturingOperationId"/>/
/// <see cref="Tempest.Core.EngineeringDomain.IVerificationActivity.SubjectId"/>
/// — their own factories never accept).
/// </para>
/// <para>
/// Must run <em>after</em> the Runtime Host has started, exactly like
/// every prior real discipline.
/// </para>
/// </remarks>
public static class ManufacturingWorkspaceRegistration
{
    /// <summary>The three Manufacturing Kinds this Work Package registers a View and a Property Facet Provider for.</summary>
    public static readonly IReadOnlyList<string> SupportedKinds = ManufacturingObjectFactoryRegistry.SupportedKinds;

    /// <summary>Registers every Manufacturing Workspace extension point.</summary>
    public static void Register(
        IWorkspaceManager manager, EngineeringDomainContext domainContext, ICommandDispatcher commandDispatcher, ICommandRegistry commandRegistry)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        manager.RegisterExplorerArea(
            ManufacturingWorkspaceExplorerModule.NavigationItemId,
            new ManufacturingNodeProvider(ManufacturingWorkspaceExplorerModule.NavigationItemId, domainContext));

        manager.RegisterView("ManufacturingOperation", new ManufacturingWorkspaceViewFactory("ManufacturingOperation", domainContext));
        manager.RegisterFacetProvider("ManufacturingOperation", new ManufacturingOperationPropertyFacetProvider("ManufacturingOperation", domainContext));
        // WP 10.2A (ADR-0096): real rename/delete dispatch.
        manager.RegisterRenameFactory("ManufacturingOperation", static (id, targetKind, name) => new RenameManufacturingObjectCommand(id, targetKind, name));
        manager.RegisterDeleteFactory("ManufacturingOperation", static (id, targetKind) => new DeleteManufacturingObjectCommand(id, targetKind));
        // WP 10.3A (ADR-0097): real revise dispatch.
        manager.RegisterReviseFactory("ManufacturingOperation", static (id, targetKind, content) => new ReviseManufacturingObjectCommand(id, targetKind, content));

        // Disclosed cross-Work-Package reuse — see this class's own remarks.
        manager.RegisterView("WorkInstruction", new DocumentsWorkspaceViewFactory("WorkInstruction", domainContext));
        manager.RegisterFacetProvider("WorkInstruction", new DocumentsPropertyFacetProvider("WorkInstruction", domainContext));
        // WP 10.2A (ADR-0096): reuses Documents' own already-registered
        // Rename/DeleteDocumentObjectCommand handler for the identical
        // reason the View/Facet Provider above already do — neither
        // handler inspects TargetKind beyond passing it through.
        manager.RegisterRenameFactory("WorkInstruction", static (id, targetKind, name) => new RenameDocumentObjectCommand(id, targetKind, name));
        manager.RegisterDeleteFactory("WorkInstruction", static (id, targetKind) => new DeleteDocumentObjectCommand(id, targetKind));
        // WP 10.3A (ADR-0097): reuses Documents' own already-registered ReviseDocumentCommand handler, identical reuse rationale.
        manager.RegisterReviseFactory("WorkInstruction", static (id, targetKind, content) => new ReviseDocumentCommand(id, targetKind, content));

        manager.RegisterView("Inspection", new VerificationActivityWorkspaceViewFactory("Inspection", domainContext));
        manager.RegisterFacetProvider("Inspection", new VerificationActivityPropertyFacetProvider("Inspection", domainContext));
        manager.RegisterRenameFactory("Inspection", static (id, targetKind, name) => new RenameVerificationActivityCommand(id, targetKind, name));
        manager.RegisterDeleteFactory("Inspection", static (id, targetKind) => new DeleteVerificationActivityCommand(id, targetKind));
        // WP 10.3A (ADR-0097): reuses Verification's own already-registered ReviseVerificationActivityCommand handler, identical reuse rationale.
        manager.RegisterReviseFactory("Inspection", static (id, targetKind, content) => new ReviseVerificationActivityCommand(id, targetKind, content));

        var factoryRegistry = new ManufacturingObjectFactoryRegistry(domainContext);
        var copyHandler = new CopyManufacturingObjectCommandHandler(domainContext, factoryRegistry);

        commandDispatcher.RegisterHandler<CreateManufacturingObjectCommand>(new CreateManufacturingObjectCommandHandler(factoryRegistry));
        commandDispatcher.RegisterHandler<RenameManufacturingObjectCommand>(new RenameManufacturingObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<ReviseManufacturingObjectCommand>(new ReviseManufacturingObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<DeleteManufacturingObjectCommand>(new DeleteManufacturingObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<MoveManufacturingObjectCommand>(new MoveManufacturingObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<CopyManufacturingObjectCommand>(copyHandler);
        commandDispatcher.RegisterHandler<DuplicateManufacturingObjectCommand>(new DuplicateManufacturingObjectCommandHandler(domainContext, copyHandler));
        commandDispatcher.RegisterHandler<SetManufacturingObjectStatusCommand>(new SetManufacturingObjectStatusCommandHandler(domainContext));

        // TD-77 Stage 3 — descriptor binding. Every binding below is a
        // hand-written lambda closing over the same constructor the handler
        // registered above already expects; nothing here dispatches, and
        // nothing reaches a handler except through the registry's own
        // CommandHandlerTable path. Kind restrictions are this discipline's
        // own already-declared SupportedKinds, unchanged.
        var boundKinds = SupportedKinds;

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "manufacturing.create", displayName: "Create Manufacturing Object", category: "Manufacturing",
            description: "Creates a new Manufacturing Operation (incl. Routing/Supplier Operation via Classification), Work Instruction, or Inspection.")
        {
            // Kind is offered as this discipline's own already-declared
            // SupportedKinds constant, defaulted to the Ribbon's own existing
            // "ManufacturingOperation" default. A WorkInstruction/Inspection also
            // needs an owning operation / a subject object, which no collected
            // value can carry; asked for one, the factory reports its own precise
            // reason through the normal handler path rather than failing silently.
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (_, values) => new CreateManufacturingObjectCommand(
                    WorkspaceCommandBindings.Canonical(boundKinds, values["kind"]), values["displayName"]),
                [
                    WorkspaceCommandBindings.Choice("kind", "Kind", boundKinds, ManufacturingObjectFactoryRegistry.ManufacturingOperationKind),
                    WorkspaceCommandBindings.ObjectName("displayName", "Name"),
                ]),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "manufacturing.rename", displayName: "Rename Manufacturing Object", category: "Manufacturing",
            description: "Renames the selected Manufacturing object.")
        {
            // Bound for the Palette and every other future Id-based consumer.
            // The Ribbon still routes "rename"/"edit" to the Object Editor before
            // it ever reads a binding (RibbonView's own verb branch).
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new RenameManufacturingObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind, values["newDisplayName"]),
                [WorkspaceCommandBindings.ObjectName("newDisplayName", "New name")],
                boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "manufacturing.edit", displayName: "Edit Manufacturing Object", category: "Manufacturing",
            description: "Records a new content revision of the selected Manufacturing object.")
        {
            // ChangeSummary stays at the command's own optional default.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new ReviseManufacturingObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind, values["newContent"]),
                [WorkspaceCommandBindings.Text("newContent", "New content")],
                boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "manufacturing.delete", displayName: "Delete Manufacturing Object", category: "Manufacturing",
            description: "Soft-deletes the selected Manufacturing object.")
        {
            // The confirmation is what keeps a soft-delete out of an unattended
            // macro. Ribbon deletion is untouched: it never reaches a binding, and
            // still clears selection on success through its own
            // WorkspaceManager.DeleteObjectAsync path.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new DeleteManufacturingObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: boundKinds,
                confirmationMessage: WorkspaceCommandBindings.DeleteConfirmation("Manufacturing object")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "manufacturing.move", displayName: "Move Manufacturing Object", category: "Manufacturing",
            description: "Reparents the selected Manufacturing object — for an Operation, this adds/removes it from a Routing's own sequence.")
        {
            Binding = CommandBinding.Unavailable(
                WorkspaceCommandBindings.ObjectPickerRequired("Moving a Manufacturing object needs a destination parent chosen from the object tree")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "manufacturing.copy", displayName: "Copy Manufacturing Object", category: "Manufacturing",
            description: "Creates a copy of the selected object under a chosen target parent.")
        {
            Binding = CommandBinding.Unavailable(
                WorkspaceCommandBindings.ObjectPickerRequired("Copying a Manufacturing object needs a destination parent chosen from the object tree")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "manufacturing.duplicate", displayName: "Duplicate Manufacturing Object", category: "Manufacturing",
            description: "Creates a copy of the selected object under its own current parent.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new DuplicateManufacturingObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: boundKinds,
                confirmationMessage: WorkspaceCommandBindings.DuplicateConfirmation("Manufacturing object")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "manufacturing.release", displayName: "Release", category: "Manufacturing",
            description: "Transitions the selected Manufacturing object's own status to Released (SetManufacturingObjectStatusCommand).")
        {
            Binding = StatusBinding(LifecycleState.Released, boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "manufacturing.archive", displayName: "Archive", category: "Manufacturing",
            description: "Transitions the selected Manufacturing object's own status to Archived, a terminal state (SetManufacturingObjectStatusCommand).")
        {
            Binding = StatusBinding(LifecycleState.Archived, boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "manufacturing.record-inspection-result", displayName: "Record Inspection Result", category: "Manufacturing",
            description: "Records a real IVerificationRecord (Pass/Fail/Conditional) against the selected Inspection — dispatches Verification.RecordVerificationResultCommand directly, disclosed cross-Work-Package reuse.")
        {
            // The disclosed cross-discipline reuse this class's own remarks
            // already document, now expressed in a binding: this descriptor builds
            // Verification's own RecordVerificationResultCommand directly, scoped
            // to the "Inspection" Kind. The binding belongs to this descriptor, not
            // to the command type — "verification.record-result" carries its own,
            // independent binding scoped to its own Kind. Every optional collection
            // stays at the command's own default, exactly as the Ribbon's and the
            // Object Editor's own record-result paths already leave them.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new RecordVerificationResultCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId,
                    WorkspaceCommandBindings.Target(context).Kind,
                    Enum.Parse<VerificationOutcome>(values["outcome"], ignoreCase: true),
                    values["method"]),
                [
                    WorkspaceCommandBindings.EnumChoice<VerificationOutcome>("outcome", "Outcome"),
                    // The default method value is written inline rather than
                    // held as a named constant: "Inspection" is a canonically
                    // owned vocabulary value (ManufacturingObjectFactoryRegistry's
                    // own Inspection Kind), and ADR-0105 allows exactly one
                    // named owner per value. The Ribbon's own prompt and the
                    // Object Editor's own record-result path already spell
                    // this default the same, literal way, for the same reason.
                    WorkspaceCommandBindings.Required("method", "Method", "Inspection"),
                ],
                [ManufacturingObjectFactoryRegistry.InspectionKind]),
        });
    }
    /// <summary>
    /// The one binding shape the two Manufacturing status transitions share
    /// — <c>SetManufacturingObjectStatusCommand</c>'s own constructor,
    /// closed over the fixed <see cref="LifecycleState"/> each descriptor
    /// transitions to. Declares no parameter and no confirmation, which is
    /// precisely what makes these two macro-eligible.
    /// </summary>
    private static CommandBinding StatusBinding(LifecycleState status, IReadOnlyList<string> appliesToKinds) =>
        new(CommandContextRequirement.SelectedObject,
            (context, _) => new SetManufacturingObjectStatusCommand(
                WorkspaceCommandBindings.Target(context).ObjectId,
                WorkspaceCommandBindings.Target(context).Kind,
                status),
            appliesToKinds: appliesToKinds);

}
