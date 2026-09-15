using Tempest.Core.Calculations;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Calculations;

/// <summary>The command ids <see cref="CalculationsWorkspaceRegistration"/> registers.</summary>
public static class CalculationsCommandIds
{
    public const string Create = "calculations.create";
    public const string Rename = "calculations.rename";
    public const string Edit = "calculations.edit";
    public const string Delete = "calculations.delete";
    public const string Move = "calculations.move";
    public const string Copy = "calculations.copy";
    public const string Duplicate = "calculations.duplicate";
    public const string Execute = "calculations.execute";
    public const string Recalculate = "calculations.recalculate";
    public const string Lock = "calculations.lock";
    public const string Unlock = "calculations.unlock";
    public const string RequestReview = "calculations.request-review";
    public const string Approve = "calculations.approve";
    public const string Archive = "calculations.archive";

    /// <summary>Marks the selected Calculation complete (`TD-181`, Product Owner decision 2026-09-15 §2) — a Calculation only, never a Calculation Set.</summary>
    public const string Complete = "calculations.complete";

    /// <summary>Sets, or clears, the selected Calculation's own due date (`WP 20.10B`, T2) — a Calculation only, never a Calculation Set.</summary>
    public const string SetDueDate = "calculations.set-due-date";
}

/// <summary>
/// The single composition-root entry point wiring the whole Engineering
/// Calculations discipline into a running Workspace — everything
/// <c>Program.cs</c> needs, kept out of <c>Program.cs</c> itself, mirroring
/// <see cref="Mechanical.MechanicalWorkspaceRegistration"/>/<see cref="Requirements.RequirementsWorkspaceRegistration"/>'s
/// own identical shape (`WP 9.2A`, the third real Engineering discipline
/// wired this way).
/// </summary>
/// <remarks>
/// Must run <em>after</em> the Runtime Host has started, exactly like
/// Mechanical/Requirements — every piece here needs
/// <see cref="EngineeringDomainContext"/>/<see cref="ICalculationEngine"/>/
/// <see cref="ICommandDispatcher"/>/<see cref="ICommandRegistry"/>, all
/// resolvable only once <c>ITempestHost.Services</c> is populated.
/// </remarks>
public static class CalculationsWorkspaceRegistration
{
    /// <summary>The Kinds a new calculation may be placed under by default (`WP 17.9.3`).</summary>
    public static readonly IReadOnlyList<string> CalculationContainerKinds =
    [
        "Project", "Assembly", "SubAssembly", "Part", "Component",
        CalculationObjectFactoryRegistry.CalculationSetKind, CalculationObjectFactoryRegistry.CalculationKind,
    ];

    /// <summary>The two Calculation Kinds this Work Package registers a View and a Property Facet Provider for, plus the synthetic <c>"CalculationTemplate"</c> Kind.</summary>
    public static readonly IReadOnlyList<string> SupportedKinds = ["Calculation", "CalculationSet", "CalculationTemplate"];

    /// <summary>The one real Kind <c>calculations.complete</c> (`WP 20.1B`, `TD-181`) applies to — a Calculation Set is a container, never itself a task.</summary>
    private static readonly IReadOnlyList<string> CalculationOnlyKind = [CalculationObjectFactoryRegistry.CalculationKind];

    /// <summary>Registers every Engineering Calculations Workspace extension point, including the five representative Calculation Templates (`WP 9.2A`).</summary>
    public static CalculationTemplateRegistry Register(
        IWorkspaceManager manager, EngineeringDomainContext domainContext, ICalculationEngine calculationEngine,
        ICommandDispatcher commandDispatcher, ICommandRegistry commandRegistry)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(calculationEngine);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        var templateRegistry = new CalculationTemplateRegistry(calculationEngine, domainContext);
        RegisterRepresentativeTemplates(templateRegistry);

        manager.RegisterExplorerArea(
            CalculationsWorkspaceExplorerModule.NavigationItemId,
            new CalculationsNodeProvider(CalculationsWorkspaceExplorerModule.NavigationItemId, domainContext, templateRegistry));

        foreach (var kind in SupportedKinds)
        {
            manager.RegisterView(kind, new CalculationsWorkspaceViewFactory(kind, domainContext, templateRegistry));
            manager.RegisterFacetProvider(kind, new CalculationsPropertyFacetProvider(kind, domainContext, templateRegistry));

            // WP 10.2A (ADR-0096): rename/delete only for the two real
            // EngineeringDomainContext-backed Kinds — the synthetic
            // "CalculationTemplate" Kind (this class's own remarks) is never
            // an EngineeringDomainContext.Repository object, so
            // RenameCalculationObjectCommandHandler/DeleteCalculationObjectCommandHandler
            // would always fail against it; honestly never registered,
            // rather than offering a menu item that can never succeed.
            if (kind != "CalculationTemplate")
            {
                manager.RegisterRenameFactory(kind, static (id, targetKind, name) => new RenameCalculationObjectCommand(id, targetKind, name));
                manager.RegisterDeleteFactory(kind, static (id, targetKind) => new DeleteCalculationObjectCommand(id, targetKind));

                // WP 10.3A (ADR-0097): real revise dispatch, the identical
                // "real Kinds only" exclusion as Rename/Delete above.
                manager.RegisterReviseFactory(kind, static (id, targetKind, content) => new ReviseCalculationCommand(id, targetKind, content));
            }
        }

        var factoryRegistry = new CalculationObjectFactoryRegistry(domainContext);
        var copyHandler = new CopyCalculationObjectCommandHandler(domainContext, factoryRegistry, commandDispatcher);
        var executeHandler = new ExecuteCalculationCommandHandler(templateRegistry);

        commandDispatcher.RegisterHandler<CreateCalculationObjectCommand>(new CreateCalculationObjectCommandHandler(factoryRegistry, domainContext, commandDispatcher));
        commandDispatcher.RegisterHandler<RenameCalculationObjectCommand>(new RenameCalculationObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<ReviseCalculationCommand>(new ReviseCalculationCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<DeleteCalculationObjectCommand>(new DeleteCalculationObjectCommandHandler(domainContext, commandDispatcher));
        commandDispatcher.RegisterHandler<UndeleteCalculationObjectCommand>(new UndeleteCalculationObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<MoveCalculationObjectCommand>(new MoveCalculationObjectCommandHandler(domainContext, commandDispatcher));
        commandDispatcher.RegisterHandler<CopyCalculationObjectCommand>(copyHandler);
        commandDispatcher.RegisterHandler<DuplicateCalculationObjectCommand>(new DuplicateCalculationObjectCommandHandler(domainContext, copyHandler));
        commandDispatcher.RegisterHandler<SetCalculationStatusCommand>(new SetCalculationStatusCommandHandler(domainContext, commandDispatcher));
        commandDispatcher.RegisterHandler<ExecuteCalculationCommand>(executeHandler);
        commandDispatcher.RegisterHandler<RecalculateCalculationCommand>(new RecalculateCalculationCommandHandler(executeHandler));
        commandDispatcher.RegisterHandler<CompleteCalculationCommand>(new CompleteCalculationCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<SetCalculationDueDateCommand>(new SetCalculationDueDateCommandHandler(domainContext));

        // TD-77 Stage 3 — descriptor binding. Every binding below is a
        // hand-written lambda closing over the same constructor the handler
        // registered above already expects: nothing here dispatches, and
        // nothing reaches a handler except through the registry's own
        // CommandHandlerTable path.
        //
        // Kind restrictions reuse CalculationObjectFactoryRegistry's own
        // SupportedKinds — the two real EngineeringDomainContext-backed
        // Kinds — never this class's own three-entry SupportedKinds, which
        // adds the synthetic "CalculationTemplate" Kind. That is the
        // identical exclusion the Rename/Delete/Revise factory registration
        // above already makes, for the identical reason: every command here
        // resolves its target through EngineeringDomainContext.Repository,
        // which a Calculation Template is not in.
        var boundKinds = CalculationObjectFactoryRegistry.SupportedKinds;

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: CalculationsCommandIds.Create, displayName: "Create Calculation", category: "Calculations",
            description: "Creates a new Calculation or Calculation Set.")
        {
            // `WP 17.9.3` (`TD-172`): the new calculation goes under the selected
            // set, calculation, part, assembly or project, else under the open project.
            // `WP 20.10B` (T2): `CreationPlacement.ParentFor`'s own preference
            // for the current selection is guarded here against a selection
            // left over from a DIFFERENT project or standalone Engineering —
            // see `ResolveCreateParent`'s own remarks. `dueOn` is collected
            // for both Kinds this descriptor can create — the one shared
            // parameter list every invocation of one command fills in —
            // and discarded server-side for a "CalculationSet" or a
            // standalone "Calculation" (`CalculationObjectFactoryRegistry.CreateAsync`'s
            // own remarks).
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (context, values) => new CreateCalculationObjectCommand(
                    WorkspaceCommandBindings.Canonical(boundKinds, values["kind"]), values["displayName"],
                    parentId: ResolveCreateParent(context, domainContext),
                    dueOn: ParseDueOn(values["dueOn"])),
                [
                    WorkspaceCommandBindings.Choice("kind", "Kind", boundKinds, CalculationObjectFactoryRegistry.CalculationKind),
                    WorkspaceCommandBindings.ObjectName("displayName", "Name"),
                    new CommandParameter(
                        "dueOn", "Due (yyyy-mm-dd)", DefaultValue: DefaultDueOn(), Validate: ValidateDueOn),
                ]),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: CalculationsCommandIds.Rename, displayName: "Rename Calculation", category: "Calculations",
            description: "Renames the selected Calculation Domain object.")
        {
            // Bound for the Palette and every other future Id-based
            // consumer. The Ribbon still routes "rename"/"edit" to the
            // Object Editor before it ever reads a binding (RibbonView's own
            // verb branch), so this changes nothing there.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new RenameCalculationObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId,
                    WorkspaceCommandBindings.Target(context).Kind,
                    values["newDisplayName"]),
                [WorkspaceCommandBindings.ObjectName("newDisplayName", "New name")],
                boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: CalculationsCommandIds.Edit, displayName: "Edit Calculation", category: "Calculations",
            description: "Records a new content revision of the selected Calculation's own method statement.")
        {
            // ChangeSummary is left at ReviseCalculationCommand's own
            // optional default, exactly as the Object Editor's own revise
            // path already leaves it — a binding that can proceed without a
            // value declares no parameter for it.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new ReviseCalculationCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId,
                    WorkspaceCommandBindings.Target(context).Kind,
                    values["newContent"]),
                [WorkspaceCommandBindings.Text("newContent", "New content")],
                boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: CalculationsCommandIds.Delete, displayName: "Delete Calculation", category: "Calculations",
            description: "Soft-deletes the selected Calculation Domain object.")
        {
            // The confirmation is what keeps a soft-delete out of an
            // unattended macro. Ribbon deletion is untouched: it never
            // reaches a binding, and still clears selection on success
            // through its own WorkspaceManager.DeleteObjectAsync path.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new DeleteCalculationObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId,
                    WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: boundKinds,
                confirmationMessage: WorkspaceCommandBindings.DeleteConfirmation("Calculation")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: CalculationsCommandIds.Move, displayName: "Move Calculation", category: "Calculations",
            description: "Reparents the selected Calculation Domain object.")
        {
            // WP 20.2A (S2-2, FCR-0073): the destination is chosen from the
            // object picker rather than typed. Blank means top level —
            // MoveCalculationObjectCommand's own NewParentId is nullable for
            // exactly that.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new MoveCalculationObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId,
                    WorkspaceCommandBindings.Target(context).Kind,
                    WorkspaceCommandBindings.ParseDestination(values["destinationId"])),
                [WorkspaceCommandBindings.Destination("destinationId", "Destination")],
                boundKinds,
                mutates: true),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: CalculationsCommandIds.Copy, displayName: "Copy Calculation", category: "Calculations",
            description: "Creates a copy of the selected object under a chosen target parent.")
        {
            // NewIdentifier/NewDisplayName stay at the command's own
            // optional defaults, exactly as Duplicate's own binding already
            // leaves them.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new CopyCalculationObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId,
                    WorkspaceCommandBindings.Target(context).Kind,
                    WorkspaceCommandBindings.ParseDestination(values["destinationId"])),
                [WorkspaceCommandBindings.Destination("destinationId", "Destination")],
                boundKinds,
                mutates: true),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: CalculationsCommandIds.Duplicate, displayName: "Duplicate Calculation", category: "Calculations",
            description: "Creates a copy of the selected object under its own current parent.")
        {
            // NewIdentifier is left at the command's own optional default,
            // exactly as the Ribbon's own duplicate flow already leaves it.
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new DuplicateCalculationObjectCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId,
                    WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: boundKinds,
                confirmationMessage: WorkspaceCommandBindings.DuplicateConfirmation("Calculation")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: CalculationsCommandIds.Execute, displayName: "Execute Calculation", category: "Calculations",
            description: "Executes a registered Calculation Template against the selected object, recording a new CalculationRecord.")
        {
            Binding = CommandBinding.Unavailable(
                WorkspaceCommandBindings.StructuredInputRequired(
                    "Executing a Calculation needs the chosen Template's own structured input document — a different set of typed fields per Template, supplied as JSON")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: CalculationsCommandIds.Recalculate, displayName: "Recalculate", category: "Calculations",
            description: "Re-executes a Calculation Template already executed against the selected object, with fresh input.")
        {
            Binding = CommandBinding.Unavailable(
                WorkspaceCommandBindings.StructuredInputRequired(
                    "Recalculating needs the Template's own structured input document again, with fresh values — a different set of typed fields per Template, supplied as JSON")),
        });

        // The five status transitions. Each needs only the selection, so
        // each is the one shape that can run unattended in a macro
        // (ADR-0098): no parameters to collect, and nothing to confirm.
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: CalculationsCommandIds.Lock, displayName: "Lock Calculation", category: "Calculations",
            description: "Locks the selected Calculation against further edits by transitioning its own status to Approved (SetCalculationStatusCommand).")
        {
            Binding = StatusBinding(LifecycleState.Approved, boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: CalculationsCommandIds.Unlock, displayName: "Unlock Calculation", category: "Calculations",
            description: "Unlocks the selected Calculation for further edits by transitioning its own status back to Draft (SetCalculationStatusCommand).")
        {
            Binding = StatusBinding(LifecycleState.Draft, boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: CalculationsCommandIds.RequestReview, displayName: "Request Review", category: "Calculations",
            description: "Transitions the selected Calculation's own status to InReview (SetCalculationStatusCommand).")
        {
            Binding = StatusBinding(LifecycleState.InReview, boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: CalculationsCommandIds.Approve, displayName: "Approve Calculation", category: "Calculations",
            description: "Transitions the selected Calculation's own status to Approved (SetCalculationStatusCommand).")
        {
            Binding = StatusBinding(LifecycleState.Approved, boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: CalculationsCommandIds.Archive, displayName: "Archive Calculation", category: "Calculations",
            description: "Transitions the selected Calculation's own status to Archived, a terminal state (SetCalculationStatusCommand).")
        {
            Binding = StatusBinding(LifecycleState.Archived, boundKinds),
        });

        // `WP 20.1B` (`TD-181`): a Calculation is a task from creation —
        // completing it is its own act, narrower than the five status
        // transitions above (a Calculation only, never a Calculation Set,
        // which is a container, not itself task-worthy). No parameter, no
        // confirmation — the same person may complete it, exactly as
        // `Tempest.Workspace.Tasks.TaskCommandIds.Complete` already is.
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: CalculationsCommandIds.Complete, displayName: "Complete Calculation", category: "Calculations",
            description: "Marks the selected Calculation complete.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new CompleteCalculationCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind),
                appliesToKinds: CalculationOnlyKind),
        });

        // `WP 20.10B` (T2): the generic editor's own Due row dispatches
        // this directly (`ObjectEditorView.OnSaveCalculationDueAsync`),
        // mirroring `SetBomLineCommand`'s own identical Ribbon/Palette-plus-editor
        // shape; blank is accepted here (clears the due date) — unlike
        // the create prompt's own `dueOn` parameter, which refuses it.
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: CalculationsCommandIds.SetDueDate, displayName: "Set Calculation Due Date", category: "Calculations",
            description: "Sets, or clears, the selected Calculation's own due date.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new SetCalculationDueDateCommand(
                    WorkspaceCommandBindings.Target(context).ObjectId, WorkspaceCommandBindings.Target(context).Kind,
                    ParseDueOnOrNull(values["dueOn"])),
                [new CommandParameter("dueOn", "Due (yyyy-mm-dd, blank to clear)", DefaultValue: string.Empty, Validate: ValidateOptionalDueOn)],
                appliesToKinds: CalculationOnlyKind,
                mutates: true),
        });

        return templateRegistry;
    }

    /// <summary>
    /// <see cref="CreationPlacement.ParentFor"/>'s own placement, trusted
    /// only when it genuinely sits under the currently open project
    /// (`WP 20.10B`, T2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The bug this closes.</b> <see cref="CreationPlacement"/> prefers
    /// the current selection over the open project whenever the selection's
    /// own Kind is one of <see cref="CalculationContainerKinds"/> (`TD-172`)
    /// — reasonable when that selection is something in the project the
    /// user is standing in, but <c>ISelectionService</c> is one single,
    /// global service that no navigation clears (<c>CreatedObjectOpensRightUpTests</c>
    /// already found this once, `WP 17.9.4`, and worked around it locally
    /// rather than fixing the service). A selection surviving from a
    /// <em>different</em> project, or from standalone Engineering, silently
    /// parented a new Calculation outside the project the Product Owner had
    /// actually navigated to — from there its own project ancestor either
    /// resolved to the wrong project or (standalone) to none at all, and
    /// the Tasks read model's Calculations bucket
    /// (<c>TasksReadModelService.ResolveProjectId</c>, `TD-181`) never
    /// listed it, exactly Product Owner finding T2.
    /// </para>
    /// <para>
    /// <b>The fix stays local to Calculations' own create command,
    /// deliberately.</b> <see cref="CreationPlacement"/> is shared by every
    /// discipline (Documents, Manufacturing, Mechanical, Evidence,
    /// Calculations); widening its own contract, or clearing selection on
    /// every project navigation, is a cross-cutting change this Work
    /// Package's own "files you own" does not cover. Guarded here instead:
    /// once an open project is known, <see cref="CreationPlacement.ParentFor"/>'s
    /// own placement is used only if <see cref="BusinessIdentifierScope.ResolveProjectId"/>
    /// — the identical, already-synchronous parent-chain walk this same
    /// method already runs for `TD-38`'s own business-identifier numbering
    /// scope (<see cref="CalculationObjectFactoryRegistry.CreateAsync"/>) —
    /// agrees it sits under that project; otherwise the open project itself
    /// is used, exactly as if nothing at all had been selected.
    /// </para>
    /// </remarks>
    private static Guid? ResolveCreateParent(CommandContext context, EngineeringDomainContext domainContext)
    {
        var placed = CreationPlacement.ParentFor(context, CalculationContainerKinds);

        if (context.ProjectId is { } openProjectId && placed is { } candidateParentId
            && BusinessIdentifierScope.ResolveProjectId(candidateParentId, domainContext.Repository) != openProjectId)
        {
            return openProjectId;
        }

        return placed;
    }

    /// <summary>Today, offered as the create prompt's own default (`WP 20.10B`, T2) — not injected: a create prompt's default value is a starting point for the person to edit, never a value this platform computes anything from (unlike the Tasks read model's own bucket placement, which reads <see cref="TimeProvider"/>).</summary>
    private const int DefaultDueOnOffsetDays = 14;

    /// <summary>Today + <see cref="DefaultDueOnOffsetDays"/> days, formatted the one way this platform's date parameters already are (<c>TaskWorkspaceRegistration.ParseDateOrNull</c>'s own identical round-trip format).</summary>
    private static string DefaultDueOn() => DateOnly.FromDateTime(DateTime.Today).AddDays(DefaultDueOnOffsetDays).ToString("O");

    /// <summary>
    /// A calculation's own create prompt refuses a blank Due date (`WP
    /// 20.10B`, T2) — unlike <c>TaskWorkspaceRegistration.ValidateOptionalDate</c>'s
    /// identical-looking manual-task field, which allows one. The
    /// descriptor collects this parameter once, for either Kind it can
    /// create; a "CalculationSet" — never itself a task — simply never
    /// reads it back (<see cref="CalculationObjectFactoryRegistry.CreateAsync"/>'s
    /// own remarks).
    /// </summary>
    private static string? ValidateDueOn(string value) =>
        DateOnly.TryParse(value, out _) ? null : "'Due' is required (yyyy-mm-dd).";

    /// <summary>Parses a <see cref="ValidateDueOn"/>-checked value. Never called on one that has not already passed it — the same invariant <c>WorkspaceCommandBindings.ParseDestination</c>'s own remarks state for its parameter.</summary>
    private static DateOnly? ParseDueOn(string value) => DateOnly.TryParse(value, out var date) ? date : null;

    /// <summary>
    /// <see cref="CalculationsCommandIds.SetDueDate"/>'s own field —
    /// unlike the create prompt's <see cref="ValidateDueOn"/>, blank is
    /// accepted (clears the due date), mirroring
    /// <c>TaskWorkspaceRegistration.ValidateOptionalDate</c>'s identical
    /// "optional" shape.
    /// </summary>
    private static string? ValidateOptionalDueOn(string value) =>
        string.IsNullOrWhiteSpace(value) || DateOnly.TryParse(value, out _) ? null : "'Due' must be a date (yyyy-mm-dd), or blank to clear it.";

    /// <summary>Parses a <see cref="ValidateOptionalDueOn"/>-checked value — blank means "clear".</summary>
    private static DateOnly? ParseDueOnOrNull(string value) =>
        !string.IsNullOrWhiteSpace(value) && DateOnly.TryParse(value, out var date) ? date : null;

    /// <summary>
    /// The one binding shape the five Calculation status transitions share
    /// — <c>SetCalculationStatusCommand</c>'s own constructor, closed over
    /// the fixed <see cref="LifecycleState"/> each descriptor transitions
    /// to. Declares no parameter and no confirmation, which is precisely
    /// what makes these five macro-eligible.
    /// </summary>
    private static CommandBinding StatusBinding(LifecycleState status, IReadOnlyList<string> appliesToKinds) =>
        new(CommandContextRequirement.SelectedObject,
            (context, _) => new SetCalculationStatusCommand(
                WorkspaceCommandBindings.Target(context).ObjectId,
                WorkspaceCommandBindings.Target(context).Kind,
                status),
            appliesToKinds: appliesToKinds);

    /// <summary>
    /// Registers this Work Package's own five representative Calculation
    /// Templates (Bolt, Beam, Bearing, Pressure, Material Selection) with
    /// <paramref name="templateRegistry"/> so they are Explorer/Palette
    /// discoverable, addressed by their own already-registered
    /// <see cref="ICalculationDefinition{TInput, TResult}.CalculationId"/> —
    /// the underlying definitions themselves are registered with
    /// <see cref="ICalculationEngine"/> separately, by
    /// <see cref="EngineeringCalculationsWorkspaceSampleModule"/>, mirroring
    /// <see cref="CalculationSampleModule"/>'s own established
    /// "a module owns registering its own definitions" precedent. A
    /// throwaway instance of each definition is constructed here purely to
    /// read its own already-fixed <see cref="CalculationMetadata"/> — every
    /// definition is a small, stateless class, so this costs nothing beyond
    /// the allocation itself, and avoids adding a "list every registered
    /// definition" method to the frozen <see cref="ICalculationEngine"/>
    /// contract.
    /// </summary>
    private static void RegisterRepresentativeTemplates(CalculationTemplateRegistry templateRegistry)
    {
        templateRegistry.Register<BoltShearCapacityInput, BoltShearCapacityResult>(
            BoltShearCapacityCalculationDefinition.Id, new BoltShearCapacityCalculationDefinition().Metadata);

        templateRegistry.Register<BeamBendingStressInput, BeamBendingStressResult>(
            BeamBendingStressCalculationDefinition.Id, new BeamBendingStressCalculationDefinition().Metadata);

        templateRegistry.Register<BearingLoadCapacityInput, BearingLoadCapacityResult>(
            BearingLoadCapacityCalculationDefinition.Id, new BearingLoadCapacityCalculationDefinition().Metadata);

        templateRegistry.Register<PressureVesselWallThicknessInput, PressureVesselWallThicknessResult>(
            PressureVesselWallThicknessCalculationDefinition.Id, new PressureVesselWallThicknessCalculationDefinition().Metadata);

        templateRegistry.Register<MaterialSelectionMarginInput, MaterialSelectionMarginResult>(
            MaterialSelectionMarginCalculationDefinition.Id, new MaterialSelectionMarginCalculationDefinition().Metadata);
    }
}
