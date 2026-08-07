using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Samples;

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

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.create", displayName: "Create Document", category: "Documents",
            description: "Creates a new Document, Drawing, or CAD Model."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.rename", displayName: "Rename Document", category: "Documents",
            description: "Renames the selected Document Domain object."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.edit", displayName: "Edit Document", category: "Documents",
            description: "Records a new content revision of the selected Document."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.delete", displayName: "Delete Document", category: "Documents",
            description: "Soft-deletes the selected Document Domain object."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.move", displayName: "Move Document", category: "Documents",
            description: "Reparents the selected Document Domain object."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.copy", displayName: "Copy Document", category: "Documents",
            description: "Creates a copy of the selected object under a chosen target parent."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.duplicate", displayName: "Duplicate Document", category: "Documents",
            description: "Creates a copy of the selected object under its own current parent."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.attach", displayName: "Attach File", category: "Documents",
            description: "Attaches a new file reference to the selected Document (IHasAttachments.AttachAsync)."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.request-review", displayName: "Request Review", category: "Documents",
            description: "Transitions the selected Document's own status to InReview (SetDocumentStatusCommand)."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.approve", displayName: "Approve Document", category: "Documents",
            description: "Transitions the selected Document's own status to Approved (SetDocumentStatusCommand)."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "documents.release", displayName: "Release Document", category: "Documents",
            description: "Transitions the selected Document's own status to Released (SetDocumentStatusCommand)."));
    }
}
