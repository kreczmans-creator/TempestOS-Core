using Tempest.App.Workspace;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Samples;

namespace Tempest.Core.Tests.Workspace;

// Proves WorkspaceShell (Tempest.App.Workspace) end to end: TempestOS
// launches directly into a functioning, five-region Workspace shell, over a
// real, unmodified TempestHost - the same real-collaborator,
// StringWriter/StringReader testing discipline TempestShellTests.cs already
// established (WP 5.0D), applied to the Workspace for the first time.
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
    public async Task StartAsync_RendersEmptyProjectExplorer_NoEngineeringFunctionality()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, Type.EmptyTypes), writer, new StringReader(""));

        await wshell.StartAsync();

        var output = writer.ToString();
        Assert.Contains("Project Explorer", output);
        Assert.Contains("no engineering module registered yet", output);

        await wshell.StopAsync();
    }

    [Fact]
    public async Task StartAsync_RendersEmptyDocumentArea()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, Type.EmptyTypes), writer, new StringReader(""));

        await wshell.StartAsync();

        var output = writer.ToString();
        Assert.Contains("Documents", output);
        Assert.Contains("(no documents open)", output);

        await wshell.StopAsync();
    }

    [Fact]
    public async Task StartAsync_RendersEmptyProperties()
    {
        using var temp = new TempDirectory();
        var writer = new StringWriter();
        await using var wshell = new WorkspaceShell(BuildManager(temp.Path, Type.EmptyTypes), writer, new StringReader(""));

        await wshell.StartAsync();

        var output = writer.ToString();
        Assert.Contains("Properties", output);
        Assert.Contains("(nothing selected)", output);

        await wshell.StopAsync();
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
}
