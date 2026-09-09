using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Identity;
using Tempest.Core.Versioning;
using Tempest.Workspace.Mechanical;

namespace Tempest.Workspace.Evidence;

/// <summary>
/// The single composition-root entry point wiring the Evidence discipline
/// into a running Workspace — mirrors
/// <see cref="MechanicalWorkspaceRegistration.Register"/>/
/// <see cref="Verification.VerificationWorkspaceRegistration.Register"/>'s
/// own identical shape (`ADR-0148`).
/// </summary>
/// <remarks>
/// <para>
/// <b>No view is registered here</b> — `WP 18.0A`'s own explicit scope is
/// substrate only; the Evidence workspace view is `WP 18.2A`. The Object
/// Editor already opens any Kind generically from its own
/// <see cref="IPropertyFacetProvider"/>, which this registration <em>does</em>
/// supply, so a created record still opens right up (Product Owner guard,
/// `WP 17.9.4`) even with no <see cref="IWorkspaceViewFactory"/> registered
/// for it.
/// </para>
/// <para>
/// <b>Rename and delete reuse the existing generic commands</b>
/// (<see cref="RenameMechanicalObjectCommand"/>/<see cref="DeleteMechanicalObjectCommand"/>)
/// rather than declaring Evidence-specific ones: both handlers already act
/// on any <c>IRenamable</c>/<c>IDeletable</c> object regardless of
/// <c>Kind</c>, and are already registered by
/// <see cref="MechanicalWorkspaceRegistration.Register"/>, which runs
/// earlier in the same composition sequence
/// (<c>EngineeringWorkspaceComposer.RegisterEngineeringDisciplines</c>).
/// </para>
/// </remarks>
public static class EvidenceWorkspaceRegistration
{
    /// <summary>The Project Explorer area this registration populates.</summary>
    public const string ExplorerAreaId = "evidence";

    /// <summary>The Kinds a new piece of evidence lands under when nothing container-shaped is selected — just the project (`ADR-0148`; not ERP/PLM: evidence's only real container is the project it belongs to).</summary>
    public static readonly IReadOnlyList<string> ContainerKinds = [MechanicalObjectFactoryRegistry.Project];

    private static readonly IReadOnlyList<string> BoundKinds = [Core.Evidence.Evidence.CanonicalKind];

    /// <summary>Registers every Evidence Workspace extension point this Work Package owns.</summary>
    /// <param name="issueSheetRenderer">
    /// Renders the issue sheet on Issue (`WP 18.2B`, §2) — <see langword="null"/>
    /// for a composition root with none (the console harness, which does
    /// not reference <c>Tempest.Desktop</c>'s own SkiaSharp renderer);
    /// Issue still succeeds, only the sheet is skipped
    /// (<see cref="IssueEvidenceCommandHandler"/>'s own remarks).
    /// </param>
    /// <param name="principalDirectory">Resolves an identity id to a display name for the issue sheet's own author/checker lines. Registered platform-wide (`TempestHost`), so every composition root has one.</param>
    /// <param name="platformVersionProvider">Names the running build on the issue sheet's own footer. Registered platform-wide, so every composition root has one.</param>
    public static void Register(
        IWorkspaceManager manager, EngineeringDomainContext domainContext, IEvidenceService evidenceService,
        ICommandDispatcher commandDispatcher, ICommandRegistry commandRegistry,
        IIssueSheetRenderer? issueSheetRenderer, IPrincipalDirectory principalDirectory, IPlatformVersionProvider platformVersionProvider)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(evidenceService);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);
        ArgumentNullException.ThrowIfNull(principalDirectory);
        ArgumentNullException.ThrowIfNull(platformVersionProvider);

        manager.RegisterExplorerArea(ExplorerAreaId, new EvidenceNodeProvider(ExplorerAreaId, domainContext));
        manager.RegisterFacetProvider(Core.Evidence.Evidence.CanonicalKind, new EvidencePropertyFacetProvider(Core.Evidence.Evidence.CanonicalKind, domainContext));

        // `WP 18.2A`: a plain data view, never itself rendered — see
        // `EvidenceObjectView`'s own remarks. Without this,
        // `IWorkspaceNavigation.OpenAsync("Evidence")` throws
        // `WorkspaceViewFactoryNotFoundException` and a created record
        // could never actually open right up (Product Owner guard,
        // `WP 17.9.4`); with it, `WorkspaceViewCoordinator.BuildDocumentContent`
        // falls through to the Object Editor's own declaration-per-Kind
        // rendering for this Kind, exactly as it already does for every
        // Mechanical Kind.
        manager.RegisterView(Core.Evidence.Evidence.CanonicalKind, new EvidenceObjectViewFactory(domainContext));

        // WP 10.2A (ADR-0096): reuse, not reinvent — see this class's own
        // remarks. Revise has its own real meaning for Evidence (reopen an
        // Issued record as Draft, `ReviseEvidenceCommand`) so, unlike
        // rename/delete, it is not registered as a revise factory here —
        // the Object Editor's generic Content-revision affordance would
        // otherwise let a caller bump the revision number without moving
        // the status, which is not what this Kind's own Revise means.
        manager.RegisterRenameFactory(Core.Evidence.Evidence.CanonicalKind, static (id, targetKind, name) => new RenameMechanicalObjectCommand(id, targetKind, name));
        manager.RegisterDeleteFactory(Core.Evidence.Evidence.CanonicalKind, static (id, targetKind) => new DeleteMechanicalObjectCommand(id, targetKind));

        commandDispatcher.RegisterHandler<CreateEvidenceCommand>(new CreateEvidenceCommandHandler(evidenceService));
        commandDispatcher.RegisterHandler<CreateEvidenceFromFilesCommand>(new CreateEvidenceFromFilesCommandHandler(evidenceService));
        commandDispatcher.RegisterHandler<CiteEvidenceCommand>(new CiteEvidenceCommandHandler(evidenceService));
        commandDispatcher.RegisterHandler<RemoveEvidenceCitationCommand>(new RemoveEvidenceCitationCommandHandler(evidenceService));
        commandDispatcher.RegisterHandler<DeclareEvidenceFigureCommand>(new DeclareEvidenceFigureCommandHandler(evidenceService));
        commandDispatcher.RegisterHandler<RecordEvidenceCheckCommand>(new RecordEvidenceCheckCommandHandler(evidenceService));
        commandDispatcher.RegisterHandler<IssueEvidenceCommand>(new IssueEvidenceCommandHandler(
            evidenceService, domainContext, issueSheetRenderer, principalDirectory, platformVersionProvider));
        commandDispatcher.RegisterHandler<ReviseEvidenceCommand>(new ReviseEvidenceCommandHandler(evidenceService));
        commandDispatcher.RegisterHandler<SetEvidenceSubjectCommand>(new SetEvidenceSubjectCommandHandler(evidenceService));

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "evidence.create", displayName: "Create Evidence", category: "Evidence",
            description: "Creates a new piece of evidence — a record of engineering work done elsewhere.")
        {
            // Parent is where the user is standing (`WP 17.9.3`): the
            // selected container (a Project) if one is, else the open
            // project, else standalone. A non-container selection still
            // lands under the open project — CreationPlacement's own rule.
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (context, values) => new CreateEvidenceCommand(
                    values["title"],
                    Enum.Parse<EvidenceClassification>(values["classification"], ignoreCase: true),
                    CreationPlacement.ParentFor(context, ContainerKinds)),
                [
                    WorkspaceCommandBindings.ObjectName("title", "Title"),
                    WorkspaceCommandBindings.EnumChoice<EvidenceClassification>("classification", "Classification", nameof(EvidenceClassification.Calculation)),
                ]),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "evidence.cite", displayName: "Cite Reference", category: "Evidence",
            description: "Cites a released reference record from one of the five governed libraries, pinned to the revision held. An unreleased record is refused, with the record named.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new CiteEvidenceCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind,
                    values["library"], values["recordId"]),
                [
                    WorkspaceCommandBindings.Required("library", "Library"),
                    WorkspaceCommandBindings.Required("recordId", "Record"),
                ],
                BoundKinds),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "evidence.declare-figure", displayName: "Declare Figure", category: "Evidence",
            description: "Declares a named, typed figure — an input or a result — against the selected evidence.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new DeclareEvidenceFigureCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind,
                    values["name"], Enum.Parse<DeclaredFigureRole>(values["role"], ignoreCase: true), values["quantity"]),
                [
                    WorkspaceCommandBindings.Required("name", "Name"),
                    WorkspaceCommandBindings.EnumChoice<DeclaredFigureRole>("role", "Role", nameof(DeclaredFigureRole.Result)),
                    WorkspaceCommandBindings.Required("quantity", "Quantity (e.g. \"142 MPa\")"),
                ],
                BoundKinds),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "evidence.check", displayName: "Record Check", category: "Evidence",
            description: "Records a check against the selected evidence — the client's own review, entered by hand, unless the independent-check rule is on.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new RecordEvidenceCheckCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind,
                    values["checkerName"], values["checkerOrganisation"], values["statement"],
                    Enum.Parse<CheckOutcome>(values["outcome"], ignoreCase: true)),
                [
                    WorkspaceCommandBindings.Required("checkerName", "Checker name"),
                    WorkspaceCommandBindings.Required("checkerOrganisation", "Checker organisation"),
                    WorkspaceCommandBindings.Required("statement", "Statement"),
                    WorkspaceCommandBindings.EnumChoice<CheckOutcome>("outcome", "Outcome", nameof(CheckOutcome.Accepted)),
                ],
                BoundKinds),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "evidence.issue", displayName: "Issue", category: "Evidence",
            description: "Issues the selected, checked evidence to the client.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new IssueEvidenceCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind,
                    values["issueReference"], values["revision"], values["client"]),
                [
                    WorkspaceCommandBindings.Required("issueReference", "Issue reference"),
                    WorkspaceCommandBindings.Required("revision", "Revision"),
                    WorkspaceCommandBindings.Required("client", "Client"),
                ],
                BoundKinds),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "evidence.revise", displayName: "Revise", category: "Evidence",
            description: "Reopens the selected, issued evidence as a new Draft revision; the issued revision stays readable.")
        {
            // Confirmed, not unattended: this is a genuine status move
            // (Issued -> Draft) on a new revision, not a read-only or
            // validate-only act, so it does not join ADR-0098's
            // macro-safe set (`Tempest.Core.Tests.Workspace.CommandDescriptorBindingTests`'s
            // own "no parameterless-and-unconfirmed command outside
            // MacroSafe" guard).
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new ReviseEvidenceCommand(WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: BoundKinds,
                confirmationMessage: "Revise the selected, issued evidence? A new Draft revision begins; the issued revision stays readable."),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "evidence.set-subject", displayName: "Change Subject", category: "Evidence",
            description: "Tags the selected evidence to a Part, Assembly, Requirement or Deliverable, or clears the tag. Refused once the evidence is Issued.")
        {
            // No `Binding.Fields` — the Subject picker collects a `Guid?`
            // no string-valued palette field can carry, so this descriptor
            // exists for discoverability and the confirmation/refusal
            // plumbing; the Object Editor's own "Change subject" action
            // (`WP 18.2B`) constructs and dispatches the command directly,
            // exactly as Cite/Declare-figure already do for their own
            // picker-collected values.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new SetEvidenceSubjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind,
                    string.IsNullOrWhiteSpace(values["subjectId"]) ? null : Guid.Parse(values["subjectId"])),
                [WorkspaceCommandBindings.Text("subjectId", "Subject id (blank clears it)")],
                BoundKinds),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "evidence.rename", displayName: "Rename Evidence", category: "Evidence",
            description: "Renames the selected evidence.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new RenameMechanicalObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind, values["newDisplayName"]),
                [WorkspaceCommandBindings.ObjectName("newDisplayName", "New name")],
                BoundKinds),
        });

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "evidence.delete", displayName: "Delete Evidence", category: "Evidence",
            description: "Soft-deletes the selected evidence (rejected if it still has live children).")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new DeleteMechanicalObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: BoundKinds,
                confirmationMessage: WorkspaceCommandBindings.DeleteConfirmation("Evidence")),
        });
    }
}
