using Tempest.Desktop.Input;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// WP-H (`AT-23`) — the keyboard input-binding path is wired, registered,
/// and bound to nothing. That is now a product choice and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// <b>The decision this protects.</b> <c>ADR-0100</c> makes the keyboard one
/// <see cref="Tempest.Core.Input.IInputBindingProvider"/> among several, and
/// <c>AT-23</c> records that it ships with zero default bindings and no
/// remapping UI. This test is that record, checked against production
/// source: the extension point is genuinely wired, and genuinely bound to
/// nothing.
/// </para>
/// <para>
/// <b>What changed, and why this test did not.</b> When `WP-H` wrote this,
/// <c>InputBindingRouter</c> still routed through the obsolete Id-only
/// <c>InvokeAsync(id, cancellationToken)</c>, which throws for every
/// descriptor without a <c>CreateDefault</c> — all 74 production
/// discipline commands — into a fire-and-forget <c>async void</c> that
/// caught it and wrote a log line. Binding any real command would have
/// produced a key that silently did nothing, so this test was also the
/// tripwire on that defect, and the router was allow-listed in
/// <c>IdOnlyInvocationGuardTests</c> as DORMANT.
/// </para>
/// <para>
/// <b>`WP-A2` closed that.</b> The router now takes the canonical path —
/// <c>Evaluate(id, context)</c> then
/// <c>InvokeAsync(commandId, context, ParameterPrompt)</c> — and its
/// allow-list entry is gone, asserted by
/// <c>IdOnlyInvocationGuardTests.TheInputBindingRouter_IsNoLongerAllowListed_BecauseItWasMigrated</c>.
/// So this test no longer guards a defect. It still guards `AT-23`: the
/// product ships bound to nothing because that is the product decision,
/// and a default binding appearing without one is what this catches.
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
            "Production code now binds a keyboard gesture to a command Id. The routing itself is sound —\n"
            + "WP-A2 put InputBindingRouter on the canonical Evaluate/InvokeAsync(id, context, prompt, ct)\n"
            + "path — so this is not a defect report. It is AT-23: the keyboard ships with zero default\n"
            + "bindings and no remapping UI by product decision. Shipping one means amending AT-23 first.\n\n"
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
