using Tempest.App.Workspace.Documents;
using Tempest.App.Workspace.Verification;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Samples;

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

        // Disclosed cross-Work-Package reuse — see this class's own remarks.
        manager.RegisterView("WorkInstruction", new DocumentsWorkspaceViewFactory("WorkInstruction", domainContext));
        manager.RegisterFacetProvider("WorkInstruction", new DocumentsPropertyFacetProvider("WorkInstruction", domainContext));
        manager.RegisterView("Inspection", new VerificationActivityWorkspaceViewFactory("Inspection", domainContext));
        manager.RegisterFacetProvider("Inspection", new VerificationActivityPropertyFacetProvider("Inspection", domainContext));

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

        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "manufacturing.create", displayName: "Create Manufacturing Object", category: "Manufacturing",
            description: "Creates a new Manufacturing Operation (incl. Routing/Supplier Operation via Classification), Work Instruction, or Inspection."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "manufacturing.rename", displayName: "Rename Manufacturing Object", category: "Manufacturing",
            description: "Renames the selected Manufacturing object."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "manufacturing.edit", displayName: "Edit Manufacturing Object", category: "Manufacturing",
            description: "Records a new content revision of the selected Manufacturing object."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "manufacturing.delete", displayName: "Delete Manufacturing Object", category: "Manufacturing",
            description: "Soft-deletes the selected Manufacturing object."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "manufacturing.move", displayName: "Move Manufacturing Object", category: "Manufacturing",
            description: "Reparents the selected Manufacturing object — for an Operation, this adds/removes it from a Routing's own sequence."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "manufacturing.copy", displayName: "Copy Manufacturing Object", category: "Manufacturing",
            description: "Creates a copy of the selected object under a chosen target parent."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "manufacturing.duplicate", displayName: "Duplicate Manufacturing Object", category: "Manufacturing",
            description: "Creates a copy of the selected object under its own current parent."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "manufacturing.release", displayName: "Release", category: "Manufacturing",
            description: "Transitions the selected Manufacturing object's own status to Released (SetManufacturingObjectStatusCommand)."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "manufacturing.archive", displayName: "Archive", category: "Manufacturing",
            description: "Transitions the selected Manufacturing object's own status to Archived, a terminal state (SetManufacturingObjectStatusCommand)."));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "manufacturing.record-inspection-result", displayName: "Record Inspection Result", category: "Manufacturing",
            description: "Records a real IVerificationRecord (Pass/Fail/Conditional) against the selected Inspection — dispatches Verification.RecordVerificationResultCommand directly, disclosed cross-Work-Package reuse."));
    }
}
