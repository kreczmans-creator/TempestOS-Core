using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Xunit.Sdk;

[assembly: Tempest.Desktop.Tests.CloseLeftoverWindows]

namespace Tempest.Desktop.Tests;

/// <summary>
/// Closes the windows a test left open, so a finished test cannot keep its <see cref="WorkspaceHost"/> alive.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why (CI fix, 2026-10-29).</b> Every Avalonia <see cref="TopLevel"/> subscribes to
/// <see cref="Application.ActualThemeVariantChanged"/> when it is built and lets go only when it is closed. Most host tests build a
/// <see cref="MainWindow"/> or a bare <see cref="Window"/> and never close it, so the application kept every one, and through each
/// window its whole host: the engineering libraries, the records, the views. A heap dump traced the path, and a Desktop shard's
/// test process grew by about 25 MB for each such test. On a Windows runner the heap kept growing until the run slowed two to four
/// times over and the 25-minute step limit cut it off (run 40937494747, Release desktop-3).
/// </para>
/// <para>
/// <b>What it does.</b> The windows still held are exactly the ones the application's theme event still lists. Each is closed
/// through its platform window, which runs Avalonia's own close clean-up but not the window's <c>Closing</c> gate: that gate saves
/// to the host, which the test has already disposed.
/// </para>
/// <para>
/// <b>When.</b> Only after a test of the serialised host collection, on the UI thread. That collection runs alone, so every window
/// still open then belongs to a test that has finished. Tests of other collections may run side by side on the one UI thread, so
/// their windows are left alone until a host test ends.
/// </para>
/// </remarks>
public static class LeftoverWindows
{
    /// <summary>The application's theme event, whose handlers are the top levels it still holds.</summary>
    private static readonly FieldInfo? ThemeChangedField =
        typeof(Application).GetField(nameof(Application.ActualThemeVariantChanged), BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>Whether this Avalonia version still keeps its top levels where <see cref="Open"/> reads them.</summary>
    public static bool CanFindWindows => ThemeChangedField is not null;

    /// <summary>The top levels the application still holds: every window built and not yet closed.</summary>
    public static IReadOnlyList<TopLevel> Open()
    {
        if (Application.Current is not { } application || ThemeChangedField?.GetValue(application) is not Delegate handlers)
            return [];

        return [.. handlers.GetInvocationList().Select(h => h.Target).OfType<TopLevel>().Distinct()];
    }

    /// <summary>Closes every top level the application still holds. Must run on the UI thread. Returns how many it closed.</summary>
    public static int CloseAll()
    {
        Dispatcher.UIThread.VerifyAccess();
        var closed = 0;

        foreach (var topLevel in Open())
        {
            // The platform window's own disposal raises its Closed callback, which detaches the top level from the application
            // (the theme event included) without raising the window's Closing event.
            topLevel.PlatformImpl?.Dispose();
            closed++;
        }

        if (closed > 0)
            Dispatcher.UIThread.RunJobs();

        return closed;
    }

    /// <summary>Whether a test class belongs to the serialised host collection.</summary>
    internal static bool IsHostCollectionTest(Type? testClass) =>
        testClass?.GetCustomAttributesData().Any(a =>
            a.AttributeType == typeof(CollectionAttribute)
            && a.ConstructorArguments.Count == 1
            && Equals(a.ConstructorArguments[0].Value, WorkspacePersistenceCollection.Name)) == true;
}

/// <summary>After each host-collection test, closes the windows it left open (<see cref="LeftoverWindows"/>).</summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = false)]
public sealed class CloseLeftoverWindowsAttribute : BeforeAfterTestAttribute
{
    /// <inheritdoc />
    public override void After(MethodInfo methodUnderTest)
    {
        ArgumentNullException.ThrowIfNull(methodUnderTest);

        // No application yet means no windows; checked first, so a plain test never touches the dispatcher before Avalonia sets it up.
        if (!LeftoverWindows.IsHostCollectionTest(methodUnderTest.ReflectedType) || Application.Current is null || !Dispatcher.UIThread.CheckAccess())
            return;

        LeftoverWindows.CloseAll();
    }
}
