using System.Text.RegularExpressions;
using Tempest.Core.Tests.Templates;

namespace Tempest.Core.Tests.Commands;

/// <summary>
/// WP-A1 (`TD-105`, `TD-106`) — the obsolete Id-only
/// <c>ICommandRegistry.InvokeAsync(string, CancellationToken)</c> overload
/// may not gain a new production caller unnoticed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a source test and not a runtime one.</b> Both overloads are
/// legitimate API. The compiler cannot tell a deliberate legacy caller from
/// an accidental one, and no runtime assertion can either — the Id-only path
/// simply throws <see cref="CommandException"/> for any descriptor without a
/// <c>CreateDefault</c>, which is every one of the seventy-four production
/// discipline commands. That is exactly how the Cockpit spent `TD-77` Stages
/// 2 through 5 quietly broken: three surfaces were on the obsolete path, the
/// contract tests all passed, and nothing in the suite asked whether the
/// surfaces agreed with each other.
/// </para>
/// <para>
/// The canonical surface path is
/// <c>Evaluate(id, context)</c> then
/// <c>InvokeAsync(id, context, prompt, cancellationToken)</c>. Anything else
/// has to be named here, with its reason, or this test fails.
/// </para>
/// </remarks>
public class IdOnlyInvocationGuardTests
{
    /// <summary>
    /// Every production call site of the Id-only overload that is allowed to
    /// exist, and why. A file appears here only because someone decided it
    /// should — which is the entire point.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> SanctionedCallers =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["src/Tempest.Core/Api/ApiRequestHandler.cs"] =
                "SANCTIONED EXCEPTION. The REST transport has no request-parameter binding at all "
                + "(`AT-10`): an inbound request's body and query string are never threaded into the "
                + "invocation, so a mapped route dispatches its command's own parameterless "
                + "CreateDefault instance by design. There is no selection behind an HTTP call and "
                + "therefore no CommandContext to build — the Id-only overload is the correct one "
                + "here, not a leftover. Revisit only if `AT-10` is ever closed.",

            ["src/Tempest.Core/Input/InputBindingRouter.cs"] =
                "DORMANT, trigger-gated (`WP-A2`). KeyboardCommandBindingProvider ships with zero "
                + "default bindings and no remapping UI (`AT-23`), and no production code calls "
                + "Bind(gesture, commandId) — so CommandRequested never fires and this line never "
                + "runs today. It becomes a real defect the moment a gesture is bound to a "
                + "discipline command, which is the trigger for `WP-A2`.",
        };

    /// <summary>
    /// Matches a call to the Id-only overload: an Id argument optionally
    /// followed by a cancellation token, and nothing else. The context-aware
    /// overload always passes a context as its second argument, so it cannot
    /// match.
    /// </summary>
    private static readonly Regex IdOnlyCall = new(
        @"\.InvokeAsync\(\s*[^,()]+\s*(?:,\s*(?:cancellationToken|ct|CancellationToken\.None)\s*)?\)",
        RegexOptions.Compiled);

    private static IEnumerable<(string RelativePath, string Source)> ProductionSources()
    {
        var root = Path.Combine(RepositoryPaths.RepositoryRoot, "src");

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var relative = Path.GetRelativePath(RepositoryPaths.RepositoryRoot, file)
                .Replace(Path.DirectorySeparatorChar, '/');

            yield return (relative, File.ReadAllText(file));
        }
    }

    /// <summary>Executable lines only — a doc comment naming the overload is not a call.</summary>
    private static IEnumerable<string> CodeLines(string source) =>
        source.Split('\n')
            .Select(line => line.Trim())
            .Where(line => !line.StartsWith("//", StringComparison.Ordinal)
                           && !line.StartsWith("///", StringComparison.Ordinal)
                           && !line.StartsWith('*'));

    [Fact]
    public void NoProductionCallSite_UsesTheIdOnlyOverload_OutsideTheAllowList()
    {
        var offenders = new List<string>();

        foreach (var (relativePath, source) in ProductionSources())
        {
            // The registry itself declares and implements both overloads.
            if (relativePath is "src/Tempest.Core/Commands/ICommandRegistry.cs"
                or "src/Tempest.Core/Commands/CommandRegistry.cs")
            {
                continue;
            }

            foreach (var line in CodeLines(source))
            {
                if (!IdOnlyCall.IsMatch(line))
                    continue;

                if (!SanctionedCallers.ContainsKey(relativePath))
                    offenders.Add($"{relativePath}: {line}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "A production call site uses the obsolete Id-only ICommandRegistry.InvokeAsync overload.\n"
            + "The canonical surface path is Evaluate(id, context) then InvokeAsync(id, context, prompt, ct).\n"
            + "If the call is genuinely correct, add its file to SanctionedCallers with a reason.\n\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void EverySanctionedCaller_StillExists_AndStillContainsSuchACall()
    {
        var stale = new List<string>();

        foreach (var (path, reason) in SanctionedCallers)
        {
            var absolute = Path.Combine(RepositoryPaths.RepositoryRoot, path.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(absolute))
            {
                stale.Add($"{path} is allow-listed but no longer exists.");
                continue;
            }

            if (!CodeLines(File.ReadAllText(absolute)).Any(IdOnlyCall.IsMatch))
                stale.Add($"{path} is allow-listed but no longer calls the Id-only overload — remove the entry.");

            Assert.False(string.IsNullOrWhiteSpace(reason), $"{path} is allow-listed without a reason.");
        }

        // An allow-list that outlives what it excuses is how an exception
        // quietly becomes the rule.
        Assert.Empty(stale);
    }

    [Fact]
    public void TheCockpit_IsNoLongerAllowListed_BecauseItWasMigrated()
    {
        // WP-A1's actual deliverable, asserted as a fact about the repository
        // rather than as a count: the one LIVE caller is gone, and it is gone
        // by migration rather than by being excused.
        Assert.DoesNotContain("src/Tempest.App/Workspace/EngineeringCockpit.cs", SanctionedCallers.Keys);

        var cockpit = File.ReadAllText(Path.Combine(
            RepositoryPaths.RepositoryRoot, "src", "Tempest.App", "Workspace", "EngineeringCockpit.cs"));

        Assert.DoesNotContain(CodeLines(cockpit), IdOnlyCall.IsMatch);
        Assert.Contains("InvokeAsync(commands[index - 1].Id, context, prompt, cancellationToken)", cockpit, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMacroManagerDialogsRunPath_IsNoLongerAllowListed_BecauseItWasMigrated()
    {
        // The fourth live caller — found by this very guard rather than by
        // the audit that preceded it, which is the argument for the guard.
        // MainWindow's Macro Manager "Run" path did not throw (a macro
        // descriptor does carry a CreateDefault), so it failed quietly
        // instead: every step of the macro ran against no context at all and
        // reported "needs a selected object", however the workspace was
        // selected. The palette's own macro path had already been given the
        // captured context in `TD-77` Stage 5; this is the same fix, on the
        // other of the two surfaces that can start a macro.
        Assert.DoesNotContain("src/Tempest.Desktop/MainWindow.cs", SanctionedCallers.Keys);

        var mainWindow = File.ReadAllText(Path.Combine(
            RepositoryPaths.RepositoryRoot, "src", "Tempest.Desktop", "MainWindow.cs"));

        Assert.DoesNotContain(CodeLines(mainWindow), IdOnlyCall.IsMatch);
    }

    /// <summary>
    /// WP-H — the REST entry above is sanctioned on a premise, and this is
    /// the premise: no shipped code maps an HTTP route onto a command.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The decision this protects.</b> <c>AT-10</c> records that the REST
    /// transport threads nothing from a request into the invocation — no
    /// body, no query string — so a mapped route dispatches its command's
    /// parameterless <c>CreateDefault</c> instance. That is correct for the
    /// two sample routes that exist, both in <c>Tempest.Samples</c>, which
    /// the shipped Desktop does not reference.
    /// </para>
    /// <para>
    /// <b>The failure this catches.</b> A real discipline command is mapped
    /// to a route. It has no <c>CreateDefault</c> — none of the 74 do — so
    /// the endpoint answers every request with a <c>CommandException</c>,
    /// and the fix is not in the mapping but in the transport. Mapping one
    /// is the trigger for closing <c>AT-10</c>, and this is where that gets
    /// said.
    /// </para>
    /// <para>
    /// <b>Why a behavioural test would not catch it.</b> The transport is
    /// live — it is discovered and started by
    /// <c>HostedServiceDiscoveryService</c> scanning the current AppDomain —
    /// and every existing endpoint test passes, because the sample commands
    /// it maps do carry <c>CreateDefault</c>. A newly mapped discipline
    /// command breaks only that route, only at runtime, and only for a
    /// caller the test suite does not have.
    /// </para>
    /// </remarks>
    [Fact]
    public void NoShippedAssembly_MapsAnHttpRouteOntoACommand()
    {
        var callers = new List<string>();

        foreach (var (relativePath, source) in ProductionSources())
        {
            // The registry declares and implements MapCommand; the sample
            // harness is the sanctioned demonstration of it and ships with
            // nothing (SampleSeparationTests proves the Desktop excludes it).
            if (relativePath.StartsWith("src/Samples/", StringComparison.Ordinal)
                || relativePath is "src/Tempest.Core/Api/IApiEndpointRegistry.cs"
                or "src/Tempest.Core/Api/ApiEndpointRegistry.cs")
            {
                continue;
            }

            foreach (var line in CodeLines(source))
            {
                if (line.Contains(".MapCommand(", StringComparison.Ordinal))
                    callers.Add($"{relativePath}: {line}");
            }
        }

        Assert.True(
            callers.Count == 0,
            "Production code now maps an HTTP route onto a command. The REST transport has no request-parameter\n"
            + "binding (AT-10), so the route will dispatch the command's parameterless CreateDefault instance —\n"
            + "which no production discipline command has. Close AT-10 before mapping one.\n\n"
            + string.Join("\n", callers));
    }
}
