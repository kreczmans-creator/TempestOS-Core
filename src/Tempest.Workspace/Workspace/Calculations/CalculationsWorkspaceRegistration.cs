using Tempest.Core.Calculations;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Calculations;

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
    /// <summary>The two Calculation Kinds this Work Package registers a View and a Property Facet Provider for, plus the synthetic <c>"CalculationTemplate"</c> Kind.</summary>
    public static readonly IReadOnlyList<string> SupportedKinds = ["Calculation", "CalculationSet", "CalculationTemplate"];

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
        var copyHandler = new CopyCalculationObjectCommandHandler(domainContext, factoryRegistry);
        var executeHandler = new ExecuteCalculationCommandHandler(templateRegistry);

        commandDispatcher.RegisterHandler<CreateCalculationObjectCommand>(new CreateCalculationObjectCommandHandler(factoryRegistry));
        commandDispatcher.RegisterHandler<RenameCalculationObjectCommand>(new RenameCalculationObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<ReviseCalculationCommand>(new ReviseCalculationCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<DeleteCalculationObjectCommand>(new DeleteCalculationObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<MoveCalculationObjectCommand>(new MoveCalculationObjectCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<CopyCalculationObjectCommand>(copyHandler);
        commandDispatcher.RegisterHandler<DuplicateCalculationObjectCommand>(new DuplicateCalculationObjectCommandHandler(domainContext, copyHandler));
        commandDispatcher.RegisterHandler<SetCalculationStatusCommand>(new SetCalculationStatusCommandHandler(domainContext));
        commandDispatcher.RegisterHandler<ExecuteCalculationCommand>(executeHandler);
        commandDispatcher.RegisterHandler<RecalculateCalculationCommand>(new RecalculateCalculationCommandHandler(executeHandler));

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
            id: "calculations.create", displayName: "Create Calculation", category: "Calculations",
            description: "Creates a new Calculation or Calculation Set.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (_, values) => new CreateCalculationObjectCommand(
                    WorkspaceCommandBindings.Canonical(boundKinds, values["kind"]), values["displayName"]),
                [
                    WorkspaceCommandBindings.Choice("kind", "Kind", boundKinds, CalculationObjectFactoryRegistry.CalculationKind),
                    WorkspaceCommandBindings.ObjectName("displayName", "Name"),
                ]),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.rename", displayName: "Rename Calculation", category: "Calculations",
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
            id: "calculations.edit", displayName: "Edit Calculation", category: "Calculations",
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
            id: "calculations.delete", displayName: "Delete Calculation", category: "Calculations",
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
            id: "calculations.move", displayName: "Move Calculation", category: "Calculations",
            description: "Reparents the selected Calculation Domain object.")
        {
            Binding = CommandBinding.Unavailable(
                WorkspaceCommandBindings.ObjectPickerRequired("Moving a Calculation needs a destination parent chosen from the object tree")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.copy", displayName: "Copy Calculation", category: "Calculations",
            description: "Creates a copy of the selected object under a chosen target parent.")
        {
            Binding = CommandBinding.Unavailable(
                WorkspaceCommandBindings.ObjectPickerRequired("Copying a Calculation needs a destination parent chosen from the object tree")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.duplicate", displayName: "Duplicate Calculation", category: "Calculations",
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
            id: "calculations.execute", displayName: "Execute Calculation", category: "Calculations",
            description: "Executes a registered Calculation Template against the selected object, recording a new CalculationRecord.")
        {
            Binding = CommandBinding.Unavailable(
                WorkspaceCommandBindings.StructuredInputRequired(
                    "Executing a Calculation needs the chosen Template's own structured input document — a different set of typed fields per Template, supplied as JSON")),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.recalculate", displayName: "Recalculate", category: "Calculations",
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
            id: "calculations.lock", displayName: "Lock Calculation", category: "Calculations",
            description: "Locks the selected Calculation against further edits by transitioning its own status to Approved (SetCalculationStatusCommand).")
        {
            Binding = StatusBinding(LifecycleState.Approved, boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.unlock", displayName: "Unlock Calculation", category: "Calculations",
            description: "Unlocks the selected Calculation for further edits by transitioning its own status back to Draft (SetCalculationStatusCommand).")
        {
            Binding = StatusBinding(LifecycleState.Draft, boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.request-review", displayName: "Request Review", category: "Calculations",
            description: "Transitions the selected Calculation's own status to InReview (SetCalculationStatusCommand).")
        {
            Binding = StatusBinding(LifecycleState.InReview, boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.approve", displayName: "Approve Calculation", category: "Calculations",
            description: "Transitions the selected Calculation's own status to Approved (SetCalculationStatusCommand).")
        {
            Binding = StatusBinding(LifecycleState.Approved, boundKinds),
        });
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.archive", displayName: "Archive Calculation", category: "Calculations",
            description: "Transitions the selected Calculation's own status to Archived, a terminal state (SetCalculationStatusCommand).")
        {
            Binding = StatusBinding(LifecycleState.Archived, boundKinds),
        });

        return templateRegistry;
    }

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
