using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Verification;
using Tempest.Samples;

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

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.create", displayName: "Create Verification Activity", category: "Verification",
            description: "Creates a new Verification Activity — a Verification Plan until a result is recorded."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.rename", displayName: "Rename Verification Activity", category: "Verification",
            description: "Renames the selected Verification Activity."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.edit", displayName: "Edit Verification Activity", category: "Verification",
            description: "Records a new content revision of the selected Verification Activity."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.delete", displayName: "Delete Verification Activity", category: "Verification",
            description: "Soft-deletes the selected Verification Activity."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.move", displayName: "Move Verification Activity", category: "Verification",
            description: "Reparents the selected Verification Activity."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.copy", displayName: "Copy Verification Activity", category: "Verification",
            description: "Creates a copy of the selected object under a chosen target parent."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.duplicate", displayName: "Duplicate Verification Activity", category: "Verification",
            description: "Creates a copy of the selected object under its own current parent."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.record-result", displayName: "Record Verification Result", category: "Verification",
            description: "Records a real IVerificationRecord (Pass/Fail/Conditional, criteria, evidence) against the selected Verification Activity — this Work Package's own realisation of Execute/Record Result/Attach Evidence together (ADR-0089)."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.request-review", displayName: "Request Review", category: "Verification",
            description: "Transitions the selected Verification Activity's own status to InReview (SetVerificationActivityStatusCommand)."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.approve", displayName: "Approve Verification Activity", category: "Verification",
            description: "Transitions the selected Verification Activity's own status to Approved (SetVerificationActivityStatusCommand)."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "verification.archive", displayName: "Archive Verification Activity", category: "Verification",
            description: "Transitions the selected Verification Activity's own status to Archived, a terminal state (SetVerificationActivityStatusCommand)."));
    }
}
