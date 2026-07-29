using Tempest.App.Shell;
using Tempest.Core.Navigation;
using Tempest.Core.Runtime;
using Tempest.Samples;

namespace Tempest.Core.Tests.Shell;

// Proves WP 5.0D end-to-end: TempestShell (Tempest.App.Shell) is the
// composition root ADR-0033 designs - constructs and runs a real,
// unmodified TempestHost, resolves the real INavigationProvider/IEventBus
// through the real ITempestHost.Services (ADR-0034), and renders
// Navigation/Content regions using its own private page mapping (ADR-0035).
// Every collaborator here is the real production type; only the Shell's own
// TextWriter/TextReader are test-supplied (a StringWriter/StringReader is a
// real implementation of both contracts, observing output exactly as a
// console would - not a mock standing in for one).
public class TempestShellTests
{
    private static ITempestHost BuildHost(params Type[] moduleTypes) =>
        new TempestHostBuilder(moduleTypes).Build();

    // ----------------------------------------------------------------
    // Shell construction
    // ----------------------------------------------------------------

    [Fact]
    public void Constructor_NullHost_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new TempestShell(null!, new StringWriter(), new StringReader("")));

    [Fact]
    public void Constructor_NullOutput_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new TempestShell(BuildHost(), null!, new StringReader("")));

    [Fact]
    public void Constructor_NullInput_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new TempestShell(BuildHost(), new StringWriter(), null!));

    [Fact]
    public void Constructor_RealHostInCreatedState_DoesNotThrow()
    {
        var exception = Record.Exception(() => new TempestShell(BuildHost(), new StringWriter(), new StringReader("")));

        Assert.Null(exception);
    }

    // ----------------------------------------------------------------
    // Host startup / platform service resolution
    // ----------------------------------------------------------------

    [Fact]
    public async Task StartAsync_StartsTheRealHost_ReachesRunning()
    {
        var host = BuildHost(typeof(NavigationSampleModule));
        var writer = new StringWriter();
        await using var shell = new TempestShell(host, writer, new StringReader(""));

        await shell.StartAsync();

        Assert.Equal(HostState.Running, host.State);

        await shell.StopAsync();
    }

    [Fact]
    public async Task StartAsync_ResolvesRealPlatformServices_ThroughHostServices()
    {
        var host = BuildHost(typeof(NavigationSampleModule));
        var writer = new StringWriter();
        await using var shell = new TempestShell(host, writer, new StringReader(""));

        await shell.StartAsync();

        // Proven indirectly: Services is only non-null once Dependency
        // Injection Built has completed, and StartAsync resolves through it
        // successfully (no exception) - the same real container a module
        // would resolve through.
        Assert.NotNull(host.Services);

        await shell.StopAsync();
    }

    // ----------------------------------------------------------------
    // Navigation rendering
    // ----------------------------------------------------------------

    [Fact]
    public async Task StartAsync_RendersApplicationTitle()
    {
        var host = BuildHost(Type.EmptyTypes);
        var writer = new StringWriter();
        await using var shell = new TempestShell(host, writer, new StringReader(""));

        await shell.StartAsync();

        Assert.Contains("TempestOS", writer.ToString());

        await shell.StopAsync();
    }

    [Fact]
    public async Task StartAsync_RendersNavigationRegion_FromTheRealNavigationProvider()
    {
        var host = BuildHost(typeof(NavigationSampleModule), typeof(SecondaryNavigationSampleModule));
        var writer = new StringWriter();
        await using var shell = new TempestShell(host, writer, new StringReader(""));

        await shell.StartAsync();

        var output = writer.ToString();
        Assert.Contains("Navigation", output);
        Assert.Contains("1 - Home", output);
        Assert.Contains("2 - Settings", output);
        Assert.Contains("0 - Exit", output);

        await shell.StopAsync();
    }

    [Fact]
    public async Task StartAsync_WithNoNavigationItemsRegistered_RendersOnlyExit()
    {
        var host = BuildHost(Type.EmptyTypes);
        var writer = new StringWriter();
        await using var shell = new TempestShell(host, writer, new StringReader(""));

        await shell.StartAsync();

        var output = writer.ToString();
        Assert.Contains("0 - Exit", output);
        Assert.DoesNotContain("1 -", output);

        await shell.StopAsync();
    }

    [Fact]
    public async Task StartAsync_RendersReservedStatusBar()
    {
        var host = BuildHost(Type.EmptyTypes);
        var writer = new StringWriter();
        await using var shell = new TempestShell(host, writer, new StringReader(""));

        await shell.StartAsync();

        Assert.Contains("Status: (reserved for future use)", writer.ToString());

        await shell.StopAsync();
    }

    // ----------------------------------------------------------------
    // Multiple pages / Content rendering / Placeholder rendering
    // ----------------------------------------------------------------

    [Fact]
    public async Task Navigate_ToHome_RendersTheRealHomePlaceholderPage()
    {
        var host = BuildHost(typeof(NavigationSampleModule));
        var writer = new StringWriter();
        await using var shell = new TempestShell(host, writer, new StringReader(""));
        await shell.StartAsync();

        var navigationProvider = (INavigationProvider)host.Services!.GetService(typeof(INavigationProvider));
        await navigationProvider.Navigate(NavigationSampleModule.NavigationItemId);

        var output = writer.ToString();
        Assert.Contains("Content", output);
        Assert.Contains("Home", output);
        Assert.Contains("minimum viable placeholder", output);

        await shell.StopAsync();
    }

    [Fact]
    public async Task Navigate_ToSettings_RendersTheRealSettingsPlaceholderPage()
    {
        var host = BuildHost(typeof(SecondaryNavigationSampleModule));
        var writer = new StringWriter();
        await using var shell = new TempestShell(host, writer, new StringReader(""));
        await shell.StartAsync();

        var navigationProvider = (INavigationProvider)host.Services!.GetService(typeof(INavigationProvider));
        await navigationProvider.Navigate(SecondaryNavigationSampleModule.NavigationItemId);

        Assert.Contains("Settings", writer.ToString());

        await shell.StopAsync();
    }

    [Fact]
    public async Task Navigate_BetweenTwoDifferentPages_RendersEachDistinctly()
    {
        var host = BuildHost(typeof(NavigationSampleModule), typeof(SecondaryNavigationSampleModule));
        var writer = new StringWriter();
        await using var shell = new TempestShell(host, writer, new StringReader(""));
        await shell.StartAsync();

        var navigationProvider = (INavigationProvider)host.Services!.GetService(typeof(INavigationProvider));

        await navigationProvider.Navigate(NavigationSampleModule.NavigationItemId);
        var afterHome = writer.ToString();

        await navigationProvider.Navigate(SecondaryNavigationSampleModule.NavigationItemId);
        var afterSettings = writer.ToString();

        Assert.Contains("This is the Home page", afterHome);
        Assert.Contains("This is the Settings page", afterSettings);
        Assert.DoesNotContain("This is the Settings page", afterHome);

        await shell.StopAsync();
    }

    // ----------------------------------------------------------------
    // Unknown page behaviour
    // ----------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_ItemWithNoRegisteredPage_RendersTheUnknownPagePlaceholder()
    {
        var host = BuildHost(Type.EmptyTypes);
        var writer = new StringWriter();
        await using var shell = new TempestShell(host, writer, new StringReader(""));
        await shell.StartAsync();

        // A real NavigationItem/NavigationRequestedEvent for an Id the Shell
        // has no built-in page for - exactly what a plugin-contributed item
        // would look like to the Shell today (ADR-0035's own disclosed gap).
        var unregisteredItem = new NavigationItem("some.plugin.page", "Mystery Page");
        await shell.HandleAsync(new NavigationRequestedEvent(unregisteredItem), CancellationToken.None);

        var output = writer.ToString();
        Assert.Contains("Not Found", output);
        Assert.Contains("No view is registered", output);

        await shell.StopAsync();
    }

    // ----------------------------------------------------------------
    // Navigation selection (input handling)
    // ----------------------------------------------------------------

    [Fact]
    public async Task HandleInputAsync_ZeroSelectsExit_ReturnsFalse()
    {
        var host = BuildHost(Type.EmptyTypes);
        var writer = new StringWriter();
        await using var shell = new TempestShell(host, writer, new StringReader(""));
        await shell.StartAsync();

        var shouldContinue = await shell.HandleInputAsync("0");

        Assert.False(shouldContinue);

        await shell.StopAsync();
    }

    [Fact]
    public async Task HandleInputAsync_NullInput_ReturnsFalse()
    {
        var host = BuildHost(Type.EmptyTypes);
        var writer = new StringWriter();
        await using var shell = new TempestShell(host, writer, new StringReader(""));
        await shell.StartAsync();

        var shouldContinue = await shell.HandleInputAsync(null);

        Assert.False(shouldContinue);

        await shell.StopAsync();
    }

    [Fact]
    public async Task HandleInputAsync_ValidSelection_NavigatesAndReturnsTrue()
    {
        var host = BuildHost(typeof(NavigationSampleModule));
        var writer = new StringWriter();
        await using var shell = new TempestShell(host, writer, new StringReader(""));
        await shell.StartAsync();

        var shouldContinue = await shell.HandleInputAsync("1");

        Assert.True(shouldContinue);
        Assert.Contains("This is the Home page", writer.ToString());

        await shell.StopAsync();
    }

    [Theory]
    [InlineData("99")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("  ")]
    public async Task HandleInputAsync_InvalidSelection_ReportsInvalid_AndReturnsTrue(string input)
    {
        var host = BuildHost(typeof(NavigationSampleModule));
        var writer = new StringWriter();
        await using var shell = new TempestShell(host, writer, new StringReader(""));
        await shell.StartAsync();

        var shouldContinue = await shell.HandleInputAsync(input);

        Assert.True(shouldContinue);
        Assert.Contains("Invalid selection.", writer.ToString());

        await shell.StopAsync();
    }

    // ----------------------------------------------------------------
    // Event handling (real IEventBus, real NavigationRequestedEvent)
    // ----------------------------------------------------------------

    [Fact]
    public async Task Navigate_PublishesThroughTheRealEventBus_AndShellRendersInResponse()
    {
        var host = BuildHost(typeof(NavigationSampleModule));
        var writer = new StringWriter();
        await using var shell = new TempestShell(host, writer, new StringReader(""));
        await shell.StartAsync();

        var beforeLength = writer.ToString().Length;

        var navigationProvider = (INavigationProvider)host.Services!.GetService(typeof(INavigationProvider));
        await navigationProvider.Navigate(NavigationSampleModule.NavigationItemId);

        Assert.True(writer.ToString().Length > beforeLength);

        await shell.StopAsync();
    }

    // ----------------------------------------------------------------
    // Full interactive loop (real StringReader driving RunAsync end to end)
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_FullInteractiveSession_NavigatesThenExits()
    {
        var host = BuildHost(typeof(NavigationSampleModule), typeof(SecondaryNavigationSampleModule));
        var writer = new StringWriter();
        var reader = new StringReader("1\n2\n0\n");
        await using var shell = new TempestShell(host, writer, reader);

        await shell.RunAsync();

        Assert.Equal(HostState.Stopped, host.State);

        var output = writer.ToString();
        Assert.Contains("This is the Home page", output);
        Assert.Contains("This is the Settings page", output);
    }

    [Fact]
    public async Task RunAsync_ImmediateExit_StopsCleanlyWithoutNavigating()
    {
        var host = BuildHost(Type.EmptyTypes);
        var writer = new StringWriter();
        var reader = new StringReader("0\n");
        await using var shell = new TempestShell(host, writer, reader);

        await shell.RunAsync();

        Assert.Equal(HostState.Stopped, host.State);
    }

    // ----------------------------------------------------------------
    // Graceful shutdown
    // ----------------------------------------------------------------

    [Fact]
    public async Task StopAsync_AfterStart_HostReachesStopped_WithoutThrowing()
    {
        var host = BuildHost(typeof(NavigationSampleModule));
        await using var shell = new TempestShell(host, new StringWriter(), new StringReader(""));
        await shell.StartAsync();

        var exception = await Record.ExceptionAsync(() => shell.StopAsync());

        Assert.Null(exception);
        Assert.Equal(HostState.Stopped, host.State);
    }

    [Fact]
    public async Task DisposeAsync_AfterStop_DisposesTheUnderlyingHost()
    {
        var host = BuildHost(typeof(NavigationSampleModule));
        var shell = new TempestShell(host, new StringWriter(), new StringReader(""));
        await shell.StartAsync();
        await shell.StopAsync();

        await shell.DisposeAsync();

        Assert.Equal(HostState.Disposed, host.State);
    }

    // ----------------------------------------------------------------
    // Repeated startup/shutdown across fresh instances
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_RepeatedAcrossFreshShellAndHostInstances_ReachesStoppedEveryTime()
    {
        for (var i = 0; i < 3; i++)
        {
            var host = BuildHost(typeof(NavigationSampleModule));
            var writer = new StringWriter();
            var reader = new StringReader("1\n0\n");
            await using var shell = new TempestShell(host, writer, reader);

            await shell.RunAsync();

            Assert.Equal(HostState.Stopped, host.State);
            Assert.Contains("This is the Home page", writer.ToString());
        }
    }

    // ----------------------------------------------------------------
    // Shell composition (real TempestHostBuilder -> TempestShell, end to end)
    // ----------------------------------------------------------------

    [Fact]
    public async Task Shell_ComposedFromRealHostBuilder_RunsAndPresentsNavigationEndToEnd()
    {
        ITempestHost host = new TempestHostBuilder(
            [typeof(NavigationSampleModule), typeof(SecondaryNavigationSampleModule)]).Build();

        var writer = new StringWriter();
        var reader = new StringReader("0\n");
        await using var shell = new TempestShell(host, writer, reader);

        await shell.RunAsync();

        var output = writer.ToString();
        Assert.Contains("TempestOS", output);
        Assert.Contains("1 - Home", output);
        Assert.Contains("2 - Settings", output);
        Assert.Equal(HostState.Stopped, host.State);
    }

    // ----------------------------------------------------------------
    // Integration with sample modules: duplicate-ID module is isolated,
    // does not affect the Shell's own presentation
    // ----------------------------------------------------------------

    [Fact]
    public async Task Shell_WithDuplicateNavigationModulePresent_StillPresentsTheSuccessfullyRegisteredItems()
    {
        var host = BuildHost(
            typeof(NavigationSampleModule),
            typeof(SecondaryNavigationSampleModule),
            typeof(DuplicateNavigationSampleModule));

        var writer = new StringWriter();
        await using var shell = new TempestShell(host, writer, new StringReader(""));

        await shell.StartAsync();

        var output = writer.ToString();
        Assert.Contains("1 - Home", output);
        Assert.Contains("2 - Settings", output);

        // The duplicate module's own failure is isolated (ADR-0013) - the
        // Host is still Running, not Faulted.
        Assert.Equal(HostState.Running, host.State);

        await shell.StopAsync();
    }
}
