using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Documents;

/// <summary>
/// The single composition-root entry point wiring the whole Engineering
/// Documents discipline into a running Workspace — everything
/// <c>Program.cs</c> needs, kept out of <c>Program.cs</c> itself, mirroring
/// <see cref="Mechanical.MechanicalWorkspaceRegistration"/>/
/// <see cref="Requirements.RequirementsWorkspaceRegistration"/>/
/// <see cref="Calculations.CalculationsWorkspaceRegistration"/>'s own
/// identical shape (`WP 9.4A`, the fourth real Engineering discipline wired
/// this way).
/// </summary>
/// <remarks>
/// Must run <em>after</em> the Runtime Host has started, exactly like
/// Mechanical/Requirements/Calculations — every piece here needs
/// <see cref="EngineeringDomainContext"/>/<see cref="ICommandDispatcher"/>/
/// <see cref="ICommandRegistry"/>, all resolvable only once
/// <c>ITempestHost.Services</c> is populated.
/// </remarks>
public static class DocumentsWorkspaceRegistration
{
    /// <summary>The three Document Kinds this Work Package registers a View and a Property Facet Provider for.</summary>
    public static readonly IReadOnlyList<string> SupportedKinds = DocumentObjectFactoryRegistry.SupportedKinds;

    /// <summary>Registers every Engineering Documents Workspace extension point.</summary>
    public static void Register(
        IWorkspaceManager manager, EngineeringDomainContext domainContext, ICommandDispatcher commandDispatcher, ICommandRegistry commandRegistry)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        manager.RegisterExplorerArea(
            DocumentsWorkspaceExplorerModule.NavigationItemId,
            new DocumentsNodeProvider(DocumentsWorkspaceExplorerModule.NavigationItemId, domainContext));

        foreach (var kind in SupportedKinds)
        {
            manager.RegisterView(kind, new DocumentsWorkspaceViewFactory(kind, domainContext));
            manager.RegisterFacetProvider(kind, new DocumentsPropertyFacetProvider(kind, domainContext));

            // WP 10.2A (ADR-0096): real rename/delete dispatch, mirroring Mechanical's own identical wiring.
            manager.RegisterRenameFactory(kind, static (id, targetKind, name) => new RenameDocumentObjectCommand(id, targetKind, name));
            manager.RegisterDeleteFactory(kind, static (id, targetKind) => new DeleteDocumentObjectCommand(id, targetKind));

            // WP 10.3A (ADR-0097): real revise dispatch — reused directly by
            // Manufacturing's own "WorkInstruction" Kind below (identical
            // rename/delete reuse pattern, `WP 10.2A`).
            manager.RegisterReviseFactory(kind, static (id, targetKind, content) => new ReviseDocumentCommand(id, targetKind, content));
        }

        var factoryRegistry = new DocumentObjectFactoryRegistry(domainContext);
        var copyHandler = new CopyDocumentObjectCommandHandler(domainContext, factoryRegistry);

        commandDispatcher.RegisterHandler<CreateDocumentObjectCommand>(new CreateDocumentObjectCommandHandler(factoryRegistry));
        commandDispatcher.RegisterHandler<RenameDocumentObjectCommand>(new RenameDocumentObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<ReviseDocumentCommand>(new ReviseDocumentCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<DeleteDocumentObjectCommand>(new DeleteDocumentObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<MoveDocumentObjectCommand>(new MoveDocumentObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<CopyDocumentObjectCommand>(copyHandler);
        commandDispatcher.RegisterHandler<DuplicateDocumentObjectCommand>(new DuplicateDocumentObjectCommandHandler(domainContext, copyHandler));
        commandDispatcher.RegisterHandler<SetDocumentStatusCommand>(new SetDocumentStatusCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<AttachDocumentCommand>(new AttachDocumentCommandHandler(domainContext));

        // TD-77 Stage 3 — descriptor binding. Every binding below is a
        // hand-written lambda closing over the same constructor the handler
        // registered above already expects; nothing here dispatches, and
        // nothing reaches a handler except through the registry's own
        // CommandHandlerTable path. Kind restrictions are this discipline's
        // own already-declared SupportedKinds, unchanged.
        var boundKinds = SupportedKinds;

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.create", displayName: "Create Document", category: "Documents",
            description: "Creates a new Document, Drawing, or CAD Model.")
        {
            // Classification/DrawingNumber/ModelFormat stay at
            // CreateDocumentObjectCommand's own optional defaults, exactly
            // as the Ribbon's own Create flow already leaves them.
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (_, values) => new CreateDocumentObjectCommand(
                    WorkspaceCommandBindings.Canonical(boundKinds, values["kind"]), values["displayName"]),
                [
                    WorkspaceCommandBindings.Choice("kind", "Kind", boundKinds, DocumentObjectFactoryRegistry.Document),
                    WorkspaceCommandBindings.ObjectName("displayName", "Name"),
                ]),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.rename", displayName: "Rename Document", category: "Documents",
            description: "Renames the selected Document Domain object.")
        {
            // Bound for the Palette and every other future Id-based
            // consumer. The Ribbon still routes "rename"/"edit" to the
            // Object Editor before it ever reads a binding.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new RenameDocumentObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId,
                    WorkspaceCommandBindings.Target(context).Kind,
                    values["newDisplayName"]),
                [WorkspaceCommandBindings.ObjectName("newDisplayName", "New name")],
                boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.edit", displayName: "Edit Document", category: "Documents",
            description: "Records a new content revision of the selected Document.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new ReviseDocumentCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId,
                    WorkspaceCommandBindings.Target(context).Kind,
                    values["newContent"]),
                [WorkspaceCommandBindings.Text("newContent", "New content")],
                boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.delete", displayName: "Delete Document", category: "Documents",
            description: "Soft-deletes the selected Document Domain object.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new DeleteDocumentObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId,
                    WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: boundKinds,
                confirmationMessage: WorkspaceCommandBindings.DeleteConfirmation("Document")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.move", displayName: "Move Document", category: "Documents",
            description: "Reparents the selected Document Domain object.")
        {
            Binding = CommandBinding.Unavailable(
                WorkspaceCommandBindings.ObjectPickerRequired("Moving a Document needs a destination parent chosen from the object tree")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.copy", displayName: "Copy Document", category: "Documents",
            description: "Creates a copy of the selected object under a chosen target parent.")
        {
            Binding = CommandBinding.Unavailable(
                WorkspaceCommandBindings.ObjectPickerRequired("Copying a Document needs a destination parent chosen from the object tree")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.duplicate", displayName: "Duplicate Document", category: "Documents",
            description: "Creates a copy of the selected object under its own current parent.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new DuplicateDocumentObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId,
                    WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: boundKinds,
                confirmationMessage: WorkspaceCommandBindings.DuplicateConfirmation("Document")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.attach", displayName: "Attach File", category: "Documents",
            description: "Attaches a new file reference to the selected Document (IHasAttachments.AttachAsync).")
        {
            // Not a weaker parameter: AttachDocumentCommand's own two
            // constructors take either a file's real bytes or its already-
            // measured size, and neither is expressible as collected text.
            Binding = CommandBinding.Unavailable(
                WorkspaceCommandBindings.StructuredInputRequired(
                    "Attaching a file needs the file itself — chosen with a file picker, then read for its own content type, size and bytes")),
        });

        // The three status transitions. Each needs only the selection, so
        // each is the one shape that can run unattended in a macro
        // (ADR-0098): no parameters to collect, and nothing to confirm.
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.request-review", displayName: "Request Review", category: "Documents",
            description: "Transitions the selected Document's own status to InReview (SetDocumentStatusCommand).")
        {
            Binding = StatusBinding(LifecycleState.InReview, boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.approve", displayName: "Approve Document", category: "Documents",
            description: "Transitions the selected Document's own status to Approved (SetDocumentStatusCommand).")
        {
            Binding = StatusBinding(LifecycleState.Approved, boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.release", displayName: "Release Document", category: "Documents",
            description: "Transitions the selected Document's own status to Released (SetDocumentStatusCommand).")
        {
            Binding = StatusBinding(LifecycleState.Released, boundKinds),
        });
    }

    /// <summary>
    /// The one binding shape the three Document status transitions share —
    /// <c>SetDocumentStatusCommand</c>'s own constructor, closed over the
    /// fixed <see cref="LifecycleState"/> each descriptor transitions to.
    /// Declares no parameter and no confirmation, which is precisely what
    /// makes these three macro-eligible.
    /// </summary>
    private static CommandBinding StatusBinding(LifecycleState status, IReadOnlyList<string> appliesToKinds) =>
        new(CommandContextRequirement.SelectedObject,
            (context, _) => new SetDocumentStatusCommand(
                WorkspaceCommandBindings.Target(context).ObjectId,
                WorkspaceCommandBindings.Target(context).Kind,
                status),
            appliesToKinds: appliesToKinds);
}
