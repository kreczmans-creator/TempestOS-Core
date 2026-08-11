using Tempest.App.Workspace;
using Tempest.App.Workspace.Samples;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Samples;

namespace Tempest.Core.Tests.Workspace;

// Proves WorkspaceShell (Tempest.App.Workspace) end to end: TempestOS's
// Internal Engineering Harness (ADR-0101) launches directly into a
// functioning, five-region Workspace shell, over a real, unmodified
// TempestHost - the same real-collaborator, StringWriter/StringReader
// testing discipline this project established for its console shell at
// WP 5.0D, applied to the Workspace for the first time.
[Collection("Console output capture")]
public class WorkspaceShellTests
{
    private static WorkspaceManager BuildManager(string rootPath, params Type[] moduleTypes)
    {
        var host = new TempestHostBuilder(moduleTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();
        return new WorkspaceManager(host);
    }

    // Mirrors Program.cs's own registration of the Project Explorer's living
    // reference content (WP 8.1B) against a real WorkspaceManager.
    private static WorkspaceManager BuildManagerWithSampleExplorer(string rootPath)
    {
        var manager = BuildManager(rootPath, typeof(WorkspaceExplorerSampleModule));
        manager.RegisterExplorerArea(WorkspaceExplorerSampleModule.NavigationItemId, new SampleProjectExplorerNodeProvider(WorkspaceExplorerSampleModule.NavigationItemId));
        manager.RegisterView(SampleExplorerContent.ComponentKind, new SampleWorkspaceViewFactory(SampleExplorerContent.ComponentKind));
        return manager;
    }

    // ----------------------------------------------------------------
    // Construction
    // ----------------------------------------------------------------

    [Fact]
    public void Constructor_NullManager_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new WorkspaceShell(null!, new StringWriter(), new StringReader("")));

    [Fact]
    public void Constructor_NullOutput_ThrowsArgumentNullException()
    {
        using var temp = new TempDirectory();
        Assert.Throws<ArgumentNullException>(() => new WorkspaceShell(BuildManager(temp.Path, Type.EmptyTypes), null!, new StringReader("")));
    }

    [Fact]
    public void Constructor_NullInput_ThrowsArgumentNullException()
    {
        using var temp = new TempDirectory();
        Assert.Throws<ArgumentNullException>(() => new WorkspaceShell(BuildManager(temp.Path, Type.EmptyTypes), new StringWriter(), null!));
    }

    // ----------------------------------------------------------------
    // StartAsync / rendering
    // ----------------------------------------------------------------

    [Fact]
    public async Task StartAsync_RendersApplicationTitle()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, Type.EmptyTypes), writer, new StringReader(""));

        await wshell.StartAsync();

        Assert.Contains("TempestOS", writer.ToString());
        Assert.Contains("Engineering Workspace", writer.ToString());

        await wshell.StopAsync();
    }

    [Fact]
    public async Task StartAsync_RendersAreas_FromTheRealNavigationProvider()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, typeof(NavigationSampleModule), typeof(SecondaryNavigationSampleModule)), writer, new StringReader(""));

        await wshell.StartAsync();

        var output = writer.ToString();
        Assert.Contains("Areas", output);
        Assert.Contains("1 - Home", output);
        Assert.Contains("2 - Settings", output);
        Assert.Contains("0 - Exit", output);

        await wshell.StopAsync();
    }

    [Fact]
    public async Task StartAsync_WithNoModules_RendersOnlyExit()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, Type.EmptyTypes), writer, new StringReader(""));

        await wshell.StartAsync();

        var output = writer.ToString();
        Assert.Contains("0 - Exit", output);
        Assert.DoesNotContain("1 -", output);

        await wshell.StopAsync();
    }

    [Fact]
    public async Task RunAsync_SwitchArea_RendersEmptyProjectExplorer_NoEngineeringFunctionality()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        var manager = BuildManager(temp.Path, typeof(NavigationSampleModule));
        await using var wshell = new WorkspaceShell(manager, writer, new StringReader("1\n0\n"));

        await wshell.RunAsync();

        var output = writer.ToString();
        Assert.Contains("Project Explorer", output);
        Assert.Contains("no engineering module registered yet", output);
    }

    [Fact]
    public async Task RunAsync_SwitchArea_RendersEmptyDocumentArea()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        var manager = BuildManager(temp.Path, typeof(NavigationSampleModule));
        await using var wshell = new WorkspaceShell(manager, writer, new StringReader("1\n0\n"));

        await wshell.RunAsync();

        var output = writer.ToString();
        Assert.Contains("Documents", output);
        Assert.Contains("(no documents open)", output);
    }

    [Fact]
    public async Task RunAsync_SwitchArea_RendersEmptyProperties()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        var manager = BuildManager(temp.Path, typeof(NavigationSampleModule));
        await using var wshell = new WorkspaceShell(manager, writer, new StringReader("1\n0\n"));

        await wshell.RunAsync();

        var output = writer.ToString();
        Assert.Contains("Properties", output);
        Assert.Contains("(nothing selected)", output);
    }

    [Fact]
    public async Task StartAsync_RendersStatusBar_InitiallyReady()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, Type.EmptyTypes), writer, new StringReader(""));

        await wshell.StartAsync();

        Assert.Contains("Status: Ready.", writer.ToString());

        await wshell.StopAsync();
    }

    // ----------------------------------------------------------------
    // Input handling
    // ----------------------------------------------------------------

    [Fact]
    public async Task HandleInputAsync_Zero_RequestsExit()
    {
        using var temp = new TempDirectory();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, Type.EmptyTypes), new StringWriter(), new StringReader(""));
        await wshell.StartAsync();

        var shouldContinue = await wshell.HandleInputAsync("0");

        Assert.False(shouldContinue);

        await wshell.StopAsync();
    }

    [Fact]
    public async Task HandleInputAsync_Null_RequestsExit()
    {
        using var temp = new TempDirectory();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, Type.EmptyTypes), new StringWriter(), new StringReader(""));
        await wshell.StartAsync();

        var shouldContinue = await wshell.HandleInputAsync(null);

        Assert.False(shouldContinue);

        await wshell.StopAsync();
    }

    [Fact]
    public async Task HandleInputAsync_ValidAreaSelection_SwitchesArea_UpdatesStatusBar()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, typeof(NavigationSampleModule)), writer, new StringReader(""));
        await wshell.StartAsync();

        var shouldContinue = await wshell.HandleInputAsync("1");

        Assert.True(shouldContinue);

        await wshell.StopAsync();
    }

    [Fact]
    public async Task HandleInputAsync_OutOfRangeSelection_ReportsInvalid_StillContinues()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, Type.EmptyTypes), writer, new StringReader(""));
        await wshell.StartAsync();

        var shouldContinue = await wshell.HandleInputAsync("99");

        Assert.True(shouldContinue);
        Assert.Contains("Invalid selection.", writer.ToString());

        await wshell.StopAsync();
    }

    // ----------------------------------------------------------------
    // Full run loop
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_ImmediateExit_StartsAndStopsCleanly()
    {
        using var temp = new TempDirectory();
        var manager = BuildManager(temp.Path, Type.EmptyTypes);
        await using var wshell = new WorkspaceShell(manager, new StringWriter(), new StringReader("0\n"));

        var exception = await Record.ExceptionAsync(() => wshell.RunAsync());

        Assert.Null(exception);
        Assert.Null(manager.Current);
    }

    [Fact]
    public async Task RunAsync_SwitchAreaThenExit_RendersTwice_EndsCleanly()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        var manager = BuildManager(temp.Path, typeof(NavigationSampleModule));
        await using var wshell = new WorkspaceShell(manager, writer, new StringReader("1\n0\n"));

        await wshell.RunAsync();

        Assert.Contains("Viewing: Home", writer.ToString());
        Assert.Null(manager.Current);
    }

    // ----------------------------------------------------------------
    // Project Explorer navigation (WP 8.1B): open / up / filter / back /
    // forward / menu / close, against the real living reference content
    // (Tempest.App.Workspace.Samples), the same content Program.cs itself
    // registers.
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_OpenCategory_DrillsIntoIt_ShowsBreadcrumb()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        var manager = BuildManagerWithSampleExplorer(temp.Path);
        await using var wshell = new WorkspaceShell(manager, writer, new StringReader("1\nopen 1\n0\n"));

        await wshell.RunAsync();

        Assert.Contains("Path: Sample Objects › Assemblies", writer.ToString());
    }

    [Fact]
    public async Task RunAsync_DrillDownToLeafObject_OpensADocument()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        var manager = BuildManagerWithSampleExplorer(temp.Path);
        await using var wshell = new WorkspaceShell(manager, writer, new StringReader("1\nopen 1\nopen 1\nopen 1\n0\n"));

        await wshell.RunAsync();

        var output = writer.ToString();
        Assert.Contains("Opened: Longeron", output);
        Assert.Contains("* Longeron", output);
    }

    [Fact]
    public async Task HandleInputAsync_Up_AtRoot_ReturnsRootNodes_NoError()
    {
        using var temp = new TempDirectory();
        await using var wshell = new WorkspaceShell(BuildManagerWithSampleExplorer(temp.Path), new StringWriter(), new StringReader(""));
        await wshell.StartAsync();
        await wshell.HandleInputAsync("1");

        var exception = await Record.ExceptionAsync(() => wshell.HandleInputAsync("up"));

        Assert.Null(exception);

        await wshell.StopAsync();
    }

    [Fact]
    public async Task HandleInputAsync_Up_AfterOpen_ReturnsToParentLevel()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManagerWithSampleExplorer(temp.Path), writer, new StringReader(""));
        await wshell.StartAsync();
        await wshell.HandleInputAsync("1");
        await wshell.HandleInputAsync("open 1");

        await wshell.HandleInputAsync("up");
        await wshell.HandleInputAsync("menu 1");

        Assert.Contains("Actions for 'Assemblies':", writer.ToString());

        await wshell.StopAsync();
    }

    [Fact]
    public async Task HandleInputAsync_Filter_NarrowsExplorerListing()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManagerWithSampleExplorer(temp.Path), writer, new StringReader(""));
        await wshell.StartAsync();
        await wshell.HandleInputAsync("1");

        await wshell.HandleInputAsync("filter Bracket");
        writer.GetStringBuilder().Clear();
        await wshell.HandleInputAsync("menu 1");

        Assert.Contains("Actions for 'Bracket':", writer.ToString());

        await wshell.StopAsync();
    }

    [Fact]
    public async Task HandleInputAsync_FilterWithNoArgument_ClearsTheFilter_RestoresRootListing()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManagerWithSampleExplorer(temp.Path), writer, new StringReader(""));
        await wshell.StartAsync();
        await wshell.HandleInputAsync("1");
        await wshell.HandleInputAsync("filter Bracket");

        await wshell.HandleInputAsync("filter");
        await wshell.HandleInputAsync("menu 1");

        Assert.Contains("Actions for 'Assemblies':", writer.ToString());

        await wshell.StopAsync();
    }

    [Fact]
    public async Task HandleInputAsync_Back_NoHistoryYet_ReportsNothingToGoBackTo()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, typeof(NavigationSampleModule)), writer, new StringReader(""));
        await wshell.StartAsync();
        await wshell.HandleInputAsync("1");

        await wshell.HandleInputAsync("back");

        Assert.Contains("Nothing to go back to.", writer.ToString());

        await wshell.StopAsync();
    }

    [Fact]
    public async Task HandleInputAsync_Back_AfterTwoAreaSwitches_ReturnsToTheFirst()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        var manager = BuildManager(temp.Path, typeof(NavigationSampleModule), typeof(SecondaryNavigationSampleModule));
        await using var wshell = new WorkspaceShell(manager, writer, new StringReader(""));
        await wshell.StartAsync();
        await wshell.HandleInputAsync("1");
        await wshell.HandleInputAsync("2");

        await wshell.HandleInputAsync("back");

        var navigationService = (NavigationService)manager.Current!.Navigation;
        Assert.Equal(NavigationSampleModule.NavigationItemId, navigationService.CurrentAreaId);

        await wshell.StopAsync();
    }

    [Fact]
    public async Task HandleInputAsync_Forward_AtNewestEntry_ReportsNothingToGoForwardTo()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, typeof(NavigationSampleModule)), writer, new StringReader(""));
        await wshell.StartAsync();
        await wshell.HandleInputAsync("1");

        await wshell.HandleInputAsync("forward");

        Assert.Contains("Nothing to go forward to.", writer.ToString());

        await wshell.StopAsync();
    }

    [Fact]
    public async Task HandleInputAsync_Menu_NonObjectNode_ListsEnterAction()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManagerWithSampleExplorer(temp.Path), writer, new StringReader(""));
        await wshell.StartAsync();
        await wshell.HandleInputAsync("1");

        await wshell.HandleInputAsync("menu 1");

        Assert.Contains("open <N> - enter", writer.ToString());

        await wshell.StopAsync();
    }

    [Fact]
    public async Task HandleInputAsync_Menu_UnopenedObject_ListsOpenAction()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManagerWithSampleExplorer(temp.Path), writer, new StringReader(""));
        await wshell.StartAsync();
        await wshell.HandleInputAsync("1");
        await wshell.HandleInputAsync("open 1");
        await wshell.HandleInputAsync("open 1");

        await wshell.HandleInputAsync("menu 1");

        Assert.Contains("open <N> - open", writer.ToString());

        await wshell.StopAsync();
    }

    [Fact]
    public async Task HandleInputAsync_Menu_AlreadyOpenObject_ListsFocusAndCloseActions()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManagerWithSampleExplorer(temp.Path), writer, new StringReader(""));
        await wshell.StartAsync();
        await wshell.HandleInputAsync("1");
        await wshell.HandleInputAsync("open 1");
        await wshell.HandleInputAsync("open 1");
        await wshell.HandleInputAsync("open 1");

        await wshell.HandleInputAsync("menu 1");

        var output = writer.ToString();
        Assert.Contains("open <N> - focus (already open)", output);
        Assert.Contains("close <N> - close", output);

        await wshell.StopAsync();
    }

    [Fact]
    public async Task HandleInputAsync_Close_ClosesTheOpenDocument()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        var manager = BuildManagerWithSampleExplorer(temp.Path);
        await using var wshell = new WorkspaceShell(manager, writer, new StringReader(""));
        await wshell.StartAsync();
        await wshell.HandleInputAsync("1");
        await wshell.HandleInputAsync("open 1");
        await wshell.HandleInputAsync("open 1");
        await wshell.HandleInputAsync("open 1");
        Assert.Single(manager.Current!.OpenViews);

        await wshell.HandleInputAsync("close 1");

        Assert.Empty(manager.Current!.OpenViews);

        await wshell.StopAsync();
    }

    [Fact]
    public async Task HandleInputAsync_Close_OutOfRange_ReportsInvalid()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManagerWithSampleExplorer(temp.Path), writer, new StringReader(""));
        await wshell.StartAsync();

        await wshell.HandleInputAsync("close 1");

        Assert.Contains("Invalid selection.", writer.ToString());

        await wshell.StopAsync();
    }

    [Fact]
    public async Task StartAsync_RendersRecentSection_InitiallyNone()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, Type.EmptyTypes), writer, new StringReader(""));

        await wshell.StartAsync();

        var output = writer.ToString();
        Assert.Contains("Recent", output);
        Assert.Contains("(none)", output);

        await wshell.StopAsync();
    }

    [Fact]
    public async Task RunAsync_OpenLeafObject_AddsToRecentSection()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        var manager = BuildManagerWithSampleExplorer(temp.Path);
        await using var wshell = new WorkspaceShell(manager, writer, new StringReader("1\nopen 1\nopen 1\nopen 1\n0\n"));

        await wshell.RunAsync();

        Assert.Contains($"Longeron ({SampleExplorerContent.ComponentKind})", writer.ToString());
    }

    [Fact]
    public async Task RunAsync_OpenLeafObject_SelectsIt_MarksItInExplorer()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        var manager = BuildManagerWithSampleExplorer(temp.Path);
        await using var wshell = new WorkspaceShell(manager, writer, new StringReader("1\nopen 1\nopen 1\nopen 1\n0\n"));

        await wshell.RunAsync();

        Assert.Contains("*1 - Longeron", writer.ToString());
    }

    // ----------------------------------------------------------------
    // Engineering Cockpit (WP 8.1C): default landing screen (ADR-0069),
    // Command Palette integration (ADR-0070).
    // ----------------------------------------------------------------

    [Fact]
    public async Task StartAsync_RendersEngineeringCockpit_ByDefault()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, Type.EmptyTypes), writer, new StringReader(""));

        await wshell.StartAsync();

        var output = writer.ToString();
        Assert.Contains("Engineering Cockpit", output);
        Assert.Contains("Continue Where I Left Off", output);
        Assert.Contains("Recent Projects", output);
        Assert.Contains("Favourite Projects", output);
        Assert.Contains("What Needs Attention", output);
        Assert.Contains("Open Decisions", output);
        Assert.Contains("Blocked Items", output);
        Assert.Contains("Overdue Actions", output);
        Assert.Contains("Project Health Dashboard", output);
        Assert.Contains("Engineering Health Summary (KPI Cards)", output);
        Assert.Contains("Risk Summary", output);
        Assert.Contains("Digital Thread Summary", output);
        Assert.Contains("Upcoming Milestones", output);
        Assert.Contains("Recent Engineering Activity", output);
        Assert.Contains("Workspace Status", output);
        Assert.Contains("Open Actions", output);
        Assert.Contains("Quick Actions", output);
        Assert.Contains("Navigation Shortcuts (Areas)", output);
        Assert.Contains("Global Commands (Command Palette)", output);

        await wshell.StopAsync();
    }

    [Fact]
    public async Task StartAsync_Cockpit_HealthIndicators_UseClosedStatusVocabulary()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, Type.EmptyTypes), writer, new StringReader(""));

        await wshell.StartAsync();

        var output = writer.ToString();
        Assert.Contains("[UNKNOWN]", output);
        Assert.DoesNotContain("[HEALTHY]", output);
        Assert.DoesNotContain("[BLOCKED]", output);
        Assert.DoesNotContain("[ATTENTION]", output);

        await wshell.StopAsync();
    }

    [Fact]
    public async Task StartAsync_Cockpit_RendersPlaceholderKpiCards()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, Type.EmptyTypes), writer, new StringReader(""));

        await wshell.StartAsync();

        var output = writer.ToString();
        Assert.Contains("Requirements: — (placeholder)", output);
        Assert.Contains("Verification: — (placeholder)", output);
        Assert.Contains("Calculations: — (placeholder)", output);

        await wshell.StopAsync();
    }

    [Fact]
    public async Task StartAsync_Cockpit_NoCommandModulesLoaded_RendersNoneAvailable()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, Type.EmptyTypes), writer, new StringReader(""));

        await wshell.StartAsync();

        Assert.Contains("(none available)", writer.ToString());

        await wshell.StopAsync();
    }

    [Fact]
    public async Task RunAsync_SwitchAreaFromCockpit_LeavesCockpit_ShowsAreaLayout()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        var manager = BuildManager(temp.Path, typeof(NavigationSampleModule));
        await using var wshell = new WorkspaceShell(manager, writer, new StringReader("1\n0\n"));

        await wshell.RunAsync();

        var output = writer.ToString();
        Assert.Contains("Project Explorer", output);
        Assert.Contains("Viewing: Home", output);
    }

    [Fact]
    public async Task RunAsync_CockpitCommand_ReturnsToCockpitFromArea()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        var manager = BuildManager(temp.Path, typeof(NavigationSampleModule));
        await using var wshell = new WorkspaceShell(manager, writer, new StringReader("1\ncockpit\n0\n"));

        await wshell.RunAsync();

        var occurrences = writer.ToString().Split("Engineering Cockpit").Length - 1;
        Assert.Equal(2, occurrences);
    }

    [Fact]
    public async Task HandleInputAsync_Cockpit_InvalidVerb_ReportsInvalid()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, Type.EmptyTypes), writer, new StringReader(""));
        await wshell.StartAsync();

        await wshell.HandleInputAsync("nonsense");

        Assert.Contains("Invalid selection.", writer.ToString());

        await wshell.StopAsync();
    }

    [Fact]
    public async Task HandleInputAsync_Cockpit_Run_NoCommandsAvailable_ReportsInvalid()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, Type.EmptyTypes), writer, new StringReader(""));
        await wshell.StartAsync();

        await wshell.HandleInputAsync("run 1");

        Assert.Contains("Invalid selection.", writer.ToString());

        await wshell.StopAsync();
    }

    [Fact]
    public async Task RunAsync_Run_InvokesTheRealCommand_UpdatesStatusBar()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        var manager = BuildManager(temp.Path, typeof(Tempest.Samples.CommandSampleModule));
        await using var wshell = new WorkspaceShell(manager, writer, new StringReader("run 1\n0\n"));

        await wshell.RunAsync();

        Assert.Contains("Increment Sample Counter: Counter is now 1.", writer.ToString());
    }

    [Fact]
    public async Task HandleInputAsync_Cockpit_Continue_NothingYet_ReportsNothingToContinue()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, Type.EmptyTypes), writer, new StringReader(""));
        await wshell.StartAsync();

        await wshell.HandleInputAsync("continue");

        Assert.Contains("Nothing to continue yet.", writer.ToString());

        await wshell.StopAsync();
    }

    [Fact]
    public async Task RunAsync_Continue_ReopensTheMostRecentSampleObject()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        var manager = BuildManagerWithSampleExplorer(temp.Path);
        await using var wshell = new WorkspaceShell(manager, writer, new StringReader("1\nopen 1\nopen 1\nopen 1\ncockpit\ncontinue\n0\n"));

        await wshell.RunAsync();

        Assert.Contains("Continued: Longeron", writer.ToString());
    }

    [Fact]
    public async Task HandleInputAsync_Cockpit_Recent_NoActivityYet_ReportsInvalid()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, Type.EmptyTypes), writer, new StringReader(""));
        await wshell.StartAsync();

        await wshell.HandleInputAsync("recent 1");

        Assert.Contains("Invalid selection.", writer.ToString());

        await wshell.StopAsync();
    }

    [Fact]
    public async Task RunAsync_Recent_ReopensTheSelectedActivityEntry()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        var manager = BuildManagerWithSampleExplorer(temp.Path);
        await using var wshell = new WorkspaceShell(manager, writer, new StringReader("1\nopen 1\nopen 1\nopen 1\ncockpit\nrecent 1\n0\n"));

        await wshell.RunAsync();

        Assert.Contains("Opened: Longeron", writer.ToString());
    }
}
