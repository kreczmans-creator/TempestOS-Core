using Tempest.Workspace;
using Tempest.Workspace.Composition;
using Tempest.Workspace.Mechanical;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Requirements;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Commands;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Verification;
using Tempest.Samples;

namespace Tempest.Core.Tests.Workspace;

// Proves EngineeringCockpit (Tempest.Workspace) - the Workspace's own
// default landing screen (ADR-0069), WP 8.1C - against real, production
// collaborators: the real ICommandRegistry (resolved through a real,
// running TempestHost) for the Cockpit's own Command Palette integration
// (ADR-0070), and NavigationService (WP 8.1A/8.1B) for every real,
// non-placeholder status indicator. EngineeringCockpit is internal, reached
// here via Tempest.Workspace's own InternalsVisibleTo grant (WP 8.1A).
public class EngineeringCockpitTests
{
    private static async Task<(IWorkspace Workspace, WorkspaceManager Manager, ITempestHost Host)> StartAsync(string rootPath, params Type[] moduleTypes)
    {
        var host = new TempestHostBuilder(moduleTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();
        var manager = new WorkspaceManager(host);

        IWorkspace workspace;
        workspace = await manager.StartAsync();

        return (workspace, manager, host);
    }

    // ----------------------------------------------------------------
    // Placeholder content
    // ----------------------------------------------------------------

    [Fact]
    public async Task ProjectName_NoMechanicalProjectExists_ReportsHonestEmptyState()
    {
        // WP 9.0A: ProjectName is now a real read of the Engineering Domain's
        // own live "Project" objects, not fixed placeholder text â€” with no
        // modules loaded (Type.EmptyTypes), none exists, so the honest empty
        // state is reported, mirroring FavouriteProjects_IsHonestlyEmpty's
        // own identical precedent.
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        Assert.Equal("No Mechanical Project yet", cockpit.ProjectName);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task Health_IsUnknown_NoSignalSourceExistsYet()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        Assert.Equal(EngineeringHealthStatus.Unknown, cockpit.Health);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task AttentionItems_HasFixedRepresentativeEntries()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        Assert.NotEmpty(cockpit.AttentionItems);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task OpenActions_HasFixedRepresentativeEntries()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        Assert.NotEmpty(cockpit.OpenActions);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task KpiCards_AreAllMarkedPlaceholder()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        Assert.NotEmpty(cockpit.KpiCards);
        Assert.All(cockpit.KpiCards, kpi => Assert.True(kpi.IsPlaceholder));

        await manager.ShutdownAsync();
    }

    // ----------------------------------------------------------------
    // Real Workspace service consumption (no placeholder)
    // ----------------------------------------------------------------

    [Fact]
    public async Task RecentActivity_DelegatesDirectly_ToNavigationServiceRecentItems()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        var objectId = Guid.NewGuid();

        await workspace.Navigation.OpenAsync(objectId, "Requirement");

        Assert.Single(cockpit.RecentActivity);
        Assert.Equal(objectId, cockpit.RecentActivity[0].ObjectId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task AreaCount_ReflectsTheRealNavigationProvider()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, typeof(NavigationSampleModule), typeof(SecondaryNavigationSampleModule));
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        Assert.Equal(2, cockpit.AreaCount);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task OpenDocumentCount_ReflectsRealOpenViews()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        await workspace.Navigation.OpenAsync(Guid.NewGuid(), "Requirement");
        await workspace.Navigation.OpenAsync(Guid.NewGuid(), "Requirement");

        Assert.Equal(2, cockpit.OpenDocumentCount);

        await manager.ShutdownAsync();
    }

    // ----------------------------------------------------------------
    // AvailableCommands / InvokeCommandAsync - Command Palette integration
    // (ADR-0070), against the real ICommandRegistry
    // ----------------------------------------------------------------

    [Fact]
    public async Task AvailableCommands_NoModulesLoaded_IsEmpty()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        Assert.Empty(cockpit.AvailableCommands(CommandContext.Empty));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task AvailableCommands_RealCommandModuleLoaded_ListsItsDescriptors()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, typeof(CommandSampleModule));
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        var available = cockpit.AvailableCommands(CommandContext.Empty);

        Assert.Contains(available, d => d.Id == CommandSampleModule.IncrementCounterCommandId);
        Assert.Contains(available, d => d.Id == CommandSampleModule.NavigateHomeCommandId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task AvailableCommands_ExcludesADescriptorWhoseCanExecuteReturnsFalse()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path, Type.EmptyTypes);
        var commandRegistry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "test.never-available",
            displayName: "Never Available",
            canExecute: () => false,
            createDefault: () => new RecordedCommandA()));
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "test.always-available",
            displayName: "Always Available",
            canExecute: () => true,
            createDefault: () => new RecordedCommandA()));

        // Neither a binding nor a default instance: nothing can construct
        // this command, so no surface may offer it (WP-A1, F-13). Before the
        // Cockpit moved onto Evaluate it listed exactly this descriptor and
        // then threw CommandException when it was chosen.
        commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: "test.not-invocable-by-id",
            displayName: "Not Invocable By Id",
            canExecute: () => true));
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        var listed = cockpit.AvailableCommands(CommandContext.Empty);

        // CanExecute is still honoured â€” Evaluate keeps it as the final gate.
        Assert.DoesNotContain(listed, d => d.Id == "test.never-available");
        Assert.DoesNotContain(listed, d => d.Id == "test.not-invocable-by-id");
        Assert.Contains(listed, d => d.Id == "test.always-available");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task InvokeCommandAsync_ValidIndex_DispatchesTheRealCommand()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, typeof(CommandSampleModule));
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        var index = cockpit.AvailableCommands(CommandContext.Empty)
            .Select((descriptor, i) => (descriptor, i))
            .Single(x => x.descriptor.Id == CommandSampleModule.IncrementCounterCommandId).i + 1;

        var invocation = await cockpit.InvokeCommandAsync(index, CommandContext.Empty);

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.True(invocation.Result!.Succeeded);
        Assert.Equal("Counter is now 1.", invocation.Result.Message);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task InvokeCommandAsync_IndexTooLow_ThrowsArgumentOutOfRangeException()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cockpit.InvokeCommandAsync(0, CommandContext.Empty));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task InvokeCommandAsync_IndexTooHigh_ThrowsArgumentOutOfRangeException()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, typeof(CommandSampleModule));
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => cockpit.InvokeCommandAsync(cockpit.AvailableCommands(CommandContext.Empty).Count + 1, CommandContext.Empty));

        await manager.ShutdownAsync();
    }

    // ----------------------------------------------------------------
    // WP-A1 (`TD-105`) - the Cockpit's own command surface is the same
    // Evaluate/InvokeAsync(context) contract every other surface uses,
    // with the context supplied by its caller (WorkspaceShell).
    // ----------------------------------------------------------------

    /// <summary>
    /// A command whose binding needs a selected object must not be listed
    /// when nothing is selected â€” the F-13 defect. Before WP-A1 the Cockpit
    /// listed every registered descriptor and then invoked it through the
    /// Id-only overload, so a command it had just reported as available threw
    /// <see cref="CommandException"/> the moment it was chosen.
    /// </summary>
    [Fact]
    public async Task AvailableCommands_HonoursTheBindingsOwnContextRequirement()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path, Type.EmptyTypes);
        var commandRegistry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
        commandRegistry.RegisterDescriptor(new CommandDescriptor("test.needs-selection", "Needs A Selection")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new RecordedCommandA(context.Primary!.ObjectId.ToString())),
        });
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        Assert.DoesNotContain(cockpit.AvailableCommands(CommandContext.Empty), d => d.Id == "test.needs-selection");
        Assert.Contains(
            cockpit.AvailableCommands(CommandContext.For(Guid.NewGuid(), "Requirement")),
            d => d.Id == "test.needs-selection");

        await manager.ShutdownAsync();
    }

    /// <summary>
    /// A command that declares itself unavailable is reported honestly â€” it
    /// is left out of the listing, and <c>Evaluate</c> still carries its own
    /// declared reason for a surface that wants to show it disabled
    /// (<c>ADR-0070</c>). The Cockpit is a listing surface, so it lists what
    /// can actually be run.
    /// </summary>
    [Fact]
    public async Task AvailableCommands_NeverListsACommandEvaluateReportsUnavailable()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path, typeof(CommandSampleModule));
        var commandRegistry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
        commandRegistry.RegisterDescriptor(new CommandDescriptor("test.no-picker", "Needs A Picker")
        {
            Binding = CommandBinding.Unavailable("this platform has no object picker yet."),
        });
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        var context = CommandContext.For(Guid.NewGuid(), "Requirement");

        var listed = cockpit.AvailableCommands(context);

        Assert.DoesNotContain(listed, d => d.Id == "test.no-picker");
        Assert.Equal(
            "this platform has no object picker yet.",
            commandRegistry.Evaluate("test.no-picker", context).Reason);

        // The invariant itself, over the whole registry rather than over the
        // one descriptor this test planted: the two answers cannot disagree.
        Assert.All(listed, d => Assert.True(commandRegistry.Evaluate(d.Id, context).IsAvailable));
        Assert.All(
            commandRegistry.Items.Where(d => !commandRegistry.Evaluate(d.Id, context).IsAvailable),
            d => Assert.DoesNotContain(listed, listedDescriptor => listedDescriptor.Id == d.Id));

        await manager.ShutdownAsync();
    }

    /// <summary>
    /// The whole point of WP-A1: a real context-aware command reaches its
    /// handler with the caller-supplied selection, through the Cockpit. The
    /// context here is built by the same
    /// <see cref="WorkspaceCommandContext"/> adapter WorkspaceShell uses, so
    /// this exercises the production translation, not a hand-built context.
    /// </summary>
    [Fact]
    public async Task InvokeCommandAsync_BuildsTheCommandFromTheCallerSuppliedSelection()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path, Type.EmptyTypes);
        var commandRegistry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
        var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
        var handler = new RecordingCommandHandler<RecordedCommandA>();
        commandDispatcher.RegisterHandler(handler);
        commandRegistry.RegisterDescriptor(new CommandDescriptor("test.acts-on-selection", "Acts On The Selection")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new RecordedCommandA(context.Primary!.ObjectId.ToString())),
        });
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        // Exactly what WorkspaceShell does: read the selection, translate it
        // through the one adapter, hand the result to the Cockpit.
        var selected = new WorkspaceSelection(Guid.NewGuid(), "Requirement");
        var context = WorkspaceCommandContext.From(selected, [selected]);
        var index = cockpit.AvailableCommands(context)
            .Select((descriptor, i) => (descriptor, i))
            .Single(x => x.descriptor.Id == "test.acts-on-selection").i + 1;

        var invocation = await cockpit.InvokeCommandAsync(index, context);

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.True(invocation.Result!.Succeeded);
        Assert.Equal(selected.ObjectId.ToString(), Assert.Single(handler.Received).Payload);

        await manager.ShutdownAsync();
    }

    // ----------------------------------------------------------------
    // Continue Where I Left Off / Recent Activity navigation (WP 8.1C)
    // ----------------------------------------------------------------

    [Fact]
    public async Task ContinueWhereILeftOff_NothingOpenedYet_IsNull()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        Assert.Null(cockpit.ContinueWhereILeftOff);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ContinueWhereILeftOff_ReflectsTheMostRecentlyOpenedObject()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await workspace.Navigation.OpenAsync(first, "Requirement");
        await workspace.Navigation.OpenAsync(second, "Requirement");

        Assert.Equal(second, cockpit.ContinueWhereILeftOff!.ObjectId);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ContinueAsync_NothingOpenedYet_ThrowsInvalidOperationException()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => cockpit.ContinueAsync());

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ContinueAsync_ReopensOrFocusesTheMostRecentObject()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        var objectId = Guid.NewGuid();
        var original = await workspace.Navigation.OpenAsync(objectId, "Requirement");

        var continued = await cockpit.ContinueAsync();

        Assert.Same(original, continued);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task OpenRecentAsync_ValidIndex_ReopensOrFocusesThatObject()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        var first = await workspace.Navigation.OpenAsync(Guid.NewGuid(), "Requirement");
        await workspace.Navigation.OpenAsync(Guid.NewGuid(), "Requirement");

        var reopened = await cockpit.OpenRecentAsync(2);

        Assert.Same(first, reopened);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task OpenRecentAsync_IndexOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cockpit.OpenRecentAsync(1));

        await manager.ShutdownAsync();
    }

    // ----------------------------------------------------------------
    // Project Health Dashboard / Risk / Digital Thread / Milestones
    // (WP 8.1C) - all disclosed placeholder content
    // ----------------------------------------------------------------

    [Fact]
    public async Task HealthDashboardStatuses_AreAllUnknown_NoRealSignalSourceExistsYet()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        Assert.Equal(EngineeringHealthStatus.Unknown, cockpit.RequirementsStatus);
        Assert.Equal(EngineeringHealthStatus.Unknown, cockpit.VerificationStatus);
        Assert.Equal(EngineeringHealthStatus.Unknown, cockpit.CalculationStatus);
        Assert.Equal(EngineeringHealthStatus.Unknown, cockpit.DocumentationStatus);
        Assert.Equal(EngineeringHealthStatus.Unknown, cockpit.ReviewStatus);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task RecentProjects_NoMechanicalProjectExists_IsHonestlyEmpty()
    {
        // WP 9.0A: RecentProjects is now a real read of the Engineering
        // Domain's own live "Project" objects, not fixed placeholder
        // content â€” with no modules loaded, none exists.
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        Assert.Empty(cockpit.RecentProjects);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task FavouriteProjects_IsHonestlyEmpty_FavouritingNotImplemented()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        Assert.Empty(cockpit.FavouriteProjects);

        await manager.ShutdownAsync();
    }

    // ----------------------------------------------------------------
    // QuickActions (WP 8.1C) - computed from real state, never fixed text
    // ----------------------------------------------------------------

    [Fact]
    public async Task QuickActions_NothingOpenNoAreasNoCommands_IsEmpty()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        Assert.Empty(cockpit.QuickActions);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task QuickActions_AreaRegistered_IncludesABrowseHint()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, typeof(NavigationSampleModule));
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        Assert.Contains(cockpit.QuickActions, hint => hint.Contains("Browse an Area", StringComparison.Ordinal));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task QuickActions_SomethingOpened_IncludesAContinueHint()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        manager.RegisterView("Requirement", new TestWorkspaceViewFactory("Requirement"));
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        await workspace.Navigation.OpenAsync(Guid.NewGuid(), "Requirement");

        Assert.Contains(cockpit.QuickActions, hint => hint.StartsWith("Continue:", StringComparison.Ordinal));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task QuickActions_CommandsAvailable_IncludesARunHint()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, typeof(CommandSampleModule));
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        Assert.Contains(cockpit.QuickActions, hint => hint.Contains("Run a Global Command", StringComparison.Ordinal));

        await manager.ShutdownAsync();
    }

    // ----------------------------------------------------------------
    // WP 9.1A: Requirements KPIs - real reads via IRequirementsService/
    // IRequirementValidationService, replacing the prior Requirement
    // placeholder cards.
    // ----------------------------------------------------------------

    [Fact]
    public async Task KpiCards_NoLiveRequirement_RequirementsEntryIsStillPlaceholder()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        var requirementsCard = Assert.Single(cockpit.KpiCards, c => c.Label == "Requirements");
        Assert.True(requirementsCard.IsPlaceholder);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task KpiCards_WithALiveRequirement_RequirementsEntryIsReal()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path, Type.EmptyTypes);
        var requirementsService = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));
        await requirementsService.CreateAsync("REQ-1", "The system shall do X.");
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        var requirementsCard = Assert.Single(cockpit.KpiCards, c => c.Label == "Requirements");
        Assert.False(requirementsCard.IsPlaceholder);
        Assert.Equal("1 total", requirementsCard.Value);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task RequirementsKpiCards_NoLiveRequirement_ReportsZeroesHonestly()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        var cards = cockpit.RequirementsKpiCards.ToDictionary(c => c.Label, c => c.Value);

        Assert.Equal("0", cards["Total Requirements"]);
        Assert.Equal("0", cards["Draft"]);
        Assert.Equal("0", cards["Outstanding Actions"]);
        Assert.Equal(EngineeringHealthStatus.Unknown.ToString(), cards["Requirement Health"]);
        Assert.All(cockpit.RequirementsKpiCards, c => Assert.False(c.IsPlaceholder));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task RequirementsKpiCards_CountsByStatus()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path, Type.EmptyTypes);
        var requirementsService = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));
        await requirementsService.CreateAsync("REQ-1", "Draft one.");
        await requirementsService.CreateAsync("REQ-2", "Draft two.");
        var third = await requirementsService.CreateAsync("REQ-3", "Reviewed one.");
        await requirementsService.SetStatusAsync(third.Id, RequirementStatus.Reviewed);
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        var cards = cockpit.RequirementsKpiCards.ToDictionary(c => c.Label, c => c.Value);

        Assert.Equal("3", cards["Total Requirements"]);
        Assert.Equal("2", cards["Draft"]);
        Assert.Equal("1", cards["Review"]);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task RequirementsKpiCards_DeletedRequirement_IsExcludedFromEveryCount()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path, Type.EmptyTypes);
        var requirementsService = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));
        var requirement = await requirementsService.CreateAsync("REQ-1", "The system shall do X.");
        await requirementsService.DeleteAsync(requirement.Id);
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        var cards = cockpit.RequirementsKpiCards.ToDictionary(c => c.Label, c => c.Value);

        Assert.Equal("0", cards["Total Requirements"]);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task RequirementsKpiCards_VerificationAndAllocationCoverage_ReflectRealDigitalThreadReads()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path, Type.EmptyTypes);
        var requirementsService = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));
        var verificationService = (IVerificationService)host.Services!.GetService(typeof(IVerificationService));
        var verified = await requirementsService.CreateAsync("REQ-1", "Verified and allocated.");
        await requirementsService.CreateAsync("REQ-2", "Neither verified nor allocated.");
        var target = await requirementsService.CreateAsync("REQ-TARGET", "Allocation target.");
        await requirementsService.LinkAsync(verified.Id, target.Id, RequirementRelationshipKinds.AllocatedTo);
        await verificationService.RecordAsync(verified.Id, VerificationOutcome.Pass, "Inspection", new VerificationContext());
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        var cards = cockpit.RequirementsKpiCards.ToDictionary(c => c.Label, c => c.Value);

        Assert.Equal("33% (1/3)", cards["Verification Coverage"]);
        Assert.Equal("33% (1/3)", cards["Allocation Coverage"]);

        // `WP 10.5C` â€” `CockpitKpiCard.PercentValue` is the identical
        // numerator/denominator `FormatCoverage`'s own display text
        // already computed, never a second, independent calculation that
        // could drift from the text a real progress bar renders beside.
        var percentByLabel = cockpit.RequirementsKpiCards.ToDictionary(c => c.Label, c => c.PercentValue);
        Assert.Equal(33, percentByLabel["Verification Coverage"]);
        Assert.Equal(33, percentByLabel["Allocation Coverage"]);

        await manager.ShutdownAsync();
    }

    /// <summary>
    /// `WP 10.5C` â€” the zero-denominator case (`FormatCoverage`'s own
    /// honest dash) has a matching honest <see langword="null"/>
    /// <see cref="CockpitKpiCard.PercentValue"/>, never a fabricated
    /// `0%` progress bar for "no requirements yet."
    /// </summary>
    [Fact]
    public async Task RequirementsKpiCards_NoLiveRequirement_PercentValueIsNullNotZero()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, _) = await StartAsync(temp.Path, Type.EmptyTypes);
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        var percentByLabel = cockpit.RequirementsKpiCards.ToDictionary(c => c.Label, c => c.PercentValue);

        Assert.Null(percentByLabel["Verification Coverage"]);
        Assert.Null(percentByLabel["Allocation Coverage"]);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task RequirementsStatus_OrphanRequirement_IsAttention()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path, Type.EmptyTypes);
        var requirementsService = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));
        await requirementsService.CreateAsync("REQ-1", "An orphan requirement with no relationships.");
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        Assert.Equal(EngineeringHealthStatus.Attention, cockpit.RequirementsStatus);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task OutstandingRequirementActions_CountsFindingsAcrossEveryLiveRequirement()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path, Type.EmptyTypes);
        var requirementsService = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));
        await requirementsService.CreateAsync("REQ-1", "An orphan requirement.");
        await requirementsService.CreateAsync("REQ-2", "Another orphan requirement.");
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        Assert.Equal(2, cockpit.OutstandingRequirementActions);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task AttentionItems_WithLiveRequirements_ReportsRequirementsManagementIsLive()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path, Type.EmptyTypes);
        var requirementsService = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));
        await requirementsService.CreateAsync("REQ-1", "The system shall do X.");
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        Assert.Contains(cockpit.AttentionItems, item => item.Title == "Requirements Management is live");

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task OpenActions_OutstandingRequirementActions_IncludesATriageEntryFirst()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path, Type.EmptyTypes);
        var requirementsService = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));
        await requirementsService.CreateAsync("REQ-1", "An orphan requirement.");
        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        Assert.Contains("Triage", cockpit.OpenActions[0].Title);

        await manager.ShutdownAsync();
    }

    // ----------------------------------------------------------------
    // `WP 18.1B` Â§4 â€” Recently changed
    // ----------------------------------------------------------------

    private static async Task<Part> CreatePartAsync(EngineeringDomainContext context, string identifier, string name) =>
        (Part)await new EngineeringObjectFactory<Part>(
            "Part", context, (doc, rev) => new Part(doc, rev, context, identifier, name, EngineeringObjectMetadata.Empty))
            .CreateAsync($"{name} â€” for test purposes.");

    /// <summary>Signs in a local session's own broad permission set (`Audit.AuditQuery.QueryPermission` among them) â€” mirrors <c>EvidenceTestHost.SignIn</c>'s own identical precedent.</summary>
    private static void SignIn(ITempestHost host)
    {
        var accessor = (CurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor));
        accessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity("recently-changed-test", "recently-changed-test"), ApplicationPermissions.LocalSession));
    }

    [Fact]
    public async Task RecentlyChanged_AfterCreatingAnObject_ListsItNewestFirstWithItsRealTitle()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path, Type.EmptyTypes);
        SignIn(host);
        var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

        var older = await CreatePartAsync(domainContext, "BRK-000", "Older Part");
        var newer = await CreatePartAsync(domainContext, "BRK-001", "Bracket Mounting Plate");

        var cockpit = ((Tempest.Workspace.Workspace)workspace).Cockpit;
        await cockpit.PrimeAsync();
        var recent = cockpit.RecentlyChanged;

        Assert.True(recent.Count >= 2);
        Assert.Equal(newer.Id, recent[0].ObjectId);
        Assert.Equal("Bracket Mounting Plate", recent[0].Title);
        Assert.Equal("Part", recent[0].Kind);
        Assert.Equal("Created", recent[0].ChangeType);
        Assert.Contains(recent, c => c.ObjectId == older.Id);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task RecentlyChanged_SurvivesRestart_ReadFromDurableAuditRowsNotASessionList()
    {
        using var temp = new TempDirectory();
        var (workspace, manager, host) = await StartAsync(temp.Path, typeof(MechanicalWorkspaceExplorerModule));
        SignIn(host);
        EngineeringWorkspaceComposer.RegisterEngineeringDisciplines(manager, host);
        var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
        var created = await CreatePartAsync(domainContext, "BRK-002", "Surviving Part");

        await manager.ShutdownAsync();

        // A brand-new WorkspaceManager/Host over the same root: nothing in
        // this new process has ever "seen" the earlier change happen â€” the
        // durable audit trail is the only place it could come from. The
        // discipline is registered again (what rebuilds each Kind's own
        // rehydrator) and rehydration is run explicitly (what
        // Repository.FindAsync needs to resolve the Part's own live title
        // rather than falling back to its bare id) â€” the identical two
        // steps `Tempest.Harness`/`WorkspaceHost` always run in that order.
        var (restartedWorkspace, restartedManager, restartedHost) = await StartAsync(temp.Path, typeof(MechanicalWorkspaceExplorerModule));
        SignIn(restartedHost);
        EngineeringWorkspaceComposer.RegisterEngineeringDisciplines(restartedManager, restartedHost);
        await EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(restartedHost);
        var cockpit = ((Tempest.Workspace.Workspace)restartedWorkspace).Cockpit;
        await cockpit.PrimeAsync();
        Assert.Contains(cockpit.RecentlyChanged, c => c.ObjectId == created.Id && c.Title == "Surviving Part");

        await restartedManager.ShutdownAsync();
    }

}
