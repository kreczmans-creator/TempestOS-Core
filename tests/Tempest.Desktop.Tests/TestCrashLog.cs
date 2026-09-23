using System.Runtime.CompilerServices;
using Tempest.Desktop.Diagnostics;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Installs <see cref="CrashLog"/> in the test host exactly as
/// <c>App.cs</c> installs it in the real application, so an exception
/// that escapes a background thread, an <c>async void</c> handler on the
/// dispatcher, or a faulted task nobody observed is written to
/// <c>logs/tempestos-crash.log</c> beside the test assembly before the
/// process dies — a test-host crash then names itself rather than
/// leaving "the host process exited unexpectedly" as the only evidence.
/// </summary>
internal static class TestCrashLog
{
    [ModuleInitializer]
    internal static void Install() => CrashLog.Install();
}
