using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The after-test clean-up of open windows (<see cref="LeftoverWindows"/>, CI fix 2026-10-29). Before it, a test that built a
/// <see cref="MainWindow"/> and never closed it kept its whole <see cref="WorkspaceHost"/> alive for the rest of the run, because the
/// application's theme event holds every window until it closes. A Desktop shard's process grew by about 25 MB a test and timed out.
/// </summary>
[Collection(WorkspacePersistenceCollection.Name)]
public sealed class LeftoverWindowsTests
{
    private static MethodInfo ThisTest([CallerMemberName] string name = "") =>
        typeof(LeftoverWindowsTests).GetMethod(name) ?? throw new InvalidOperationException(name);

    private static void CollectEverything()
    {
        for (var pass = 0; pass < 3; pass++)
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
    }

    /// <summary>Builds a host and a main window, then disposes the host and leaves the window open, as most host tests did.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(WeakReference Host, WeakReference Window)> LeaveAWindowOpenAsync()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        await host.StartAsync();
        var window = new MainWindow(host);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        await host.ShutdownAsync();
        await host.DisposeAsync();
        return (new WeakReference(host), new WeakReference(window));
    }

    [AvaloniaFact]
    public async Task A_Window_Left_Open_Holds_Its_Host_Until_The_Clean_Up_Closes_It()
    {
        var (host, window) = await LeaveAWindowOpenAsync();

        // The leak itself: the window is still listed, and through it the disposed host survives a full collection.
        Assert.Contains(LeftoverWindows.Open(), w => ReferenceEquals(w, window.Target));
        CollectEverything();
        Assert.True(host.IsAlive, "The disposed host was collected while its window was still open; the leak this guards against is gone.");

        new CloseLeftoverWindowsAttribute().After(ThisTest());

        Assert.Empty(LeftoverWindows.Open());

        // A toast the window showed holds it for its few seconds on screen; after that nothing does.
        await DesktopTestHelpers.WaitUntilAsync(() => { CollectEverything(); return !host.IsAlive && !window.IsAlive; }, baseSeconds: 20);
    }

    [AvaloniaFact]
    public void A_Test_Outside_The_Host_Collection_Leaves_The_Windows_Alone()
    {
        Assert.True(LeftoverWindows.IsHostCollectionTest(typeof(LeftoverWindowsTests)));
        Assert.False(LeftoverWindows.IsHostCollectionTest(typeof(UnscaledTestDeadlineTests)));

        var window = new Window();
        try
        {
            // Tests of other collections share the one UI thread, so their clean-up must not close a window another test is using.
            new CloseLeftoverWindowsAttribute().After(typeof(UnscaledTestDeadlineTests).GetMethods().First(m => m.GetCustomAttribute<FactAttribute>() is not null));
            Assert.Contains(LeftoverWindows.Open(), w => ReferenceEquals(w, window));
        }
        finally
        {
            Assert.True(LeftoverWindows.CloseAll() >= 1);
        }

        Assert.DoesNotContain(LeftoverWindows.Open(), w => ReferenceEquals(w, window));
    }

    [Fact]
    public void The_Clean_Up_Runs_After_Every_Test_In_The_Assembly_And_Can_Find_The_Windows()
    {
        Assert.NotNull(typeof(LeftoverWindowsTests).Assembly.GetCustomAttribute<CloseLeftoverWindowsAttribute>());

        // The open windows are read from the application's theme event; an Avalonia upgrade that moves it must fail here, not leak again.
        Assert.True(LeftoverWindows.CanFindWindows);
    }
}
