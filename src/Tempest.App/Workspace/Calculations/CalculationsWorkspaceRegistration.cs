using Tempest.Core.Calculations;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Samples;

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

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.create", displayName: "Create Calculation", category: "Calculations",
            description: "Creates a new Calculation or Calculation Set."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.rename", displayName: "Rename Calculation", category: "Calculations",
            description: "Renames the selected Calculation Domain object."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.edit", displayName: "Edit Calculation", category: "Calculations",
            description: "Records a new content revision of the selected Calculation's own method statement."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.delete", displayName: "Delete Calculation", category: "Calculations",
            description: "Soft-deletes the selected Calculation Domain object."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.move", displayName: "Move Calculation", category: "Calculations",
            description: "Reparents the selected Calculation Domain object."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.copy", displayName: "Copy Calculation", category: "Calculations",
            description: "Creates a copy of the selected object under a chosen target parent."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.duplicate", displayName: "Duplicate Calculation", category: "Calculations",
            description: "Creates a copy of the selected object under its own current parent."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.execute", displayName: "Execute Calculation", category: "Calculations",
            description: "Executes a registered Calculation Template against the selected object, recording a new CalculationRecord."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.recalculate", displayName: "Recalculate", category: "Calculations",
            description: "Re-executes a Calculation Template already executed against the selected object, with fresh input."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.lock", displayName: "Lock Calculation", category: "Calculations",
            description: "Locks the selected Calculation against further edits by transitioning its own status to Approved (SetCalculationStatusCommand)."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.unlock", displayName: "Unlock Calculation", category: "Calculations",
            description: "Unlocks the selected Calculation for further edits by transitioning its own status back to Draft (SetCalculationStatusCommand)."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.request-review", displayName: "Request Review", category: "Calculations",
            description: "Transitions the selected Calculation's own status to InReview (SetCalculationStatusCommand)."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.approve", displayName: "Approve Calculation", category: "Calculations",
            description: "Transitions the selected Calculation's own status to Approved (SetCalculationStatusCommand)."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "calculations.archive", displayName: "Archive Calculation", category: "Calculations",
            description: "Transitions the selected Calculation's own status to Archived, a terminal state (SetCalculationStatusCommand)."));

        return templateRegistry;
    }

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
