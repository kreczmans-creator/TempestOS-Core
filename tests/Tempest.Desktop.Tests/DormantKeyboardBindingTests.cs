using Tempest.Desktop.Input;

namespace Tempest.Desktop.Tests;

/// <summary>
/// WP-H (`AT-23`, `WP-A2` trigger) — the keyboard input-binding path is
/// wired, registered, and bound to nothing, and that is what keeps the
/// obsolete Id-only invocation behind it dormant.
/// </summary>
/// <remarks>
/// <para>
/// <b>The decision this protects.</b> <c>ADR-0100</c> makes the keyboard one
/// <see cref="Tempest.Core.Input.IInputBindingProvider"/> among several, and
/// <c>AT-23</c> records that it ships with zero default bindings and no
/// remapping UI. <c>InputBindingRouter</c> routes whatever a provider
/// requests through <c>ICommandRegistry.InvokeAsync(id, cancellationToken)</c>
/// — the obsolete Id-only overload, which throws for every descriptor
/// without a <c>CreateDefault</c>, i.e. all 74 production discipline
/// commands. That line is allow-listed in
/// <c>Tempest.Core.Tests.Commands.IdOnlyInvocationGuardTests</c> as DORMANT
/// on exactly one premise: nothing in production ever calls
/// <see cref="KeyboardCommandBindingProvider.Bind"/>, so
/// <c>CommandRequested</c> never fires. This test is that premise, checked.
/// </para>
/// <para>
/// <b>The failure this catches.</b> Someone gives a discipline command a
/// keyboard shortcut — a one-line, entirely reasonable-looking change — and
/// the dormant path becomes live. The shortcut then throws
/// <c>CommandException</c> into a fire-and-forget <c>async void</c>, where
/// <c>InputBindingRouter</c> catches it and writes a log line. The key
/// appears to do nothing. That is `WP-A2`'s trigger, and this test is what
/// makes it fire at the change rather than at a bug report.
/// </para>
/// <para>
/// <b>Why a behavioural test would not catch it.</b> The provider works
/// correctly — <c>ProductivityExperienceTests</c> binds a gesture and proves
/// the routing end to end. What cannot be observed at runtime is that the
/// product ships with the dictionary empty: a test that binds something is
/// testing the mechanism, and a test that asserts "no bindings" on a
/// freshly-constructed provider proves only that the constructor adds none,
/// not that <c>MainWindow</c> does not. The claim is about production call
/// sites, so it is asserted against them.
/// </para>
/// </remarks>
public sealed class DormantKeyboardBindingTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "global.json")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not locate the repository root above '{AppContext.BaseDirectory}'.");
    }

    [Fact]
    public void NoProductionCode_BindsAGestureToACommandId()
    {
        var offenders = new List<string>();
        var source = Path.Combine(RepositoryRoot, "src");

        foreach (var file in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            // The provider's own declaration of Bind is not a call to it.
            if (file.EndsWith("KeyboardCommandBindingProvider.cs", StringComparison.Ordinal))
                continue;

            var relative = Path.GetRelativePath(RepositoryRoot, file).Replace(Path.DirectorySeparatorChar, '/');

            foreach (var line in File.ReadAllLines(file).Select(line => line.Trim()))
            {
                if (line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith('*'))
                    continue;

                // Any call to a binding provider's Bind — narrowed to the
                // gesture form, so Avalonia's own property binding
                // (ThemeReactiveBrush.Bind, used ~40 times in views) and a
                // socket Bind are not mistaken for one.
                if (line.Contains("BindingProvider.Bind(", StringComparison.Ordinal)
                    || line.Contains("Bind(new KeyGesture", StringComparison.Ordinal)
                    || line.Contains("Bind(gesture,", StringComparison.Ordinal))
                {
                    offenders.Add($"{relative}: {line}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Production code now binds a keyboard gesture to a command Id, which activates the dormant\n"
            + "Id-only invocation in InputBindingRouter — allow-listed as DORMANT on the premise that this\n"
            + "never happens. That premise is now false: WP-A2 (route the keyboard through\n"
            + "Evaluate/InvokeAsync(id, context, prompt, ct)) is required before the binding can ship.\n\n"
            + string.Join("\n", offenders));
    }

    /// <summary>
    /// The other half of the same fact: the provider the shell actually
    /// registers starts empty. Stated here so the source rule above reads as
    /// "and this is what it means", rather than as a grep with no subject.
    /// </summary>
    [Fact]
    public void AFreshProvider_CarriesNoBindings_AndTheShellAddsNone()
    {
        Assert.Empty(new KeyboardCommandBindingProvider().Bindings);

        // MainWindow constructs and registers the provider (so the extension
        // point is genuinely wired, not merely declared) and binds nothing to
        // it. Both halves matter: a provider nobody registers would make this
        // dormancy meaningless rather than deliberate.
        var mainWindow = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Tempest.Desktop", "MainWindow.cs"));

        Assert.Contains("InputBindingRegistry.Register(_keyboardBindingProvider)", mainWindow, StringComparison.Ordinal);
    }
}
