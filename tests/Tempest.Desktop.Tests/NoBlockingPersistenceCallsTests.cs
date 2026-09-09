using System.Text.RegularExpressions;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The `WP 18.1A` invariant, asserted structurally rather than
/// behaviourally: not "everything returns <see cref="Task"/>", but "no
/// blocking UI-thread access to persistence or workspace operations" —
/// <c>src/Tempest.Desktop</c> and, since `WP 18.1A-R1`, <c>src/Tempest.Workspace</c>
/// carry no <c>GetAwaiter().GetResult()</c>, <c>.Result</c>, or
/// <c>.Wait()</c> on a <see cref="Task"/>, outside a small, named,
/// disclosed allow-list per source root.
/// </summary>
/// <remarks>
/// <para>
/// A source-text scan, on the same precedent
/// <c>Tempest.Core.Tests.Architecture.DependencyDirectionTests</c> set for
/// a build-graph invariant no behavioural test can observe: there
/// is no failing runtime behaviour to assert against a blocking call that
/// happens to complete instantly today, only the shape of the source
/// itself, which a future edit could reintroduce silently.
/// </para>
/// <para>
/// <b>Desktop's own two disclosed exceptions, named exactly rather than by whole file.</b>
/// </para>
/// <list type="bullet">
/// <item><description><c>App.cs</c> and <c>Composition/DesktopSessionState.cs</c>
/// run entirely before Avalonia's own dispatcher loop starts pumping —
/// <c>App.OnFrameworkInitializationCompleted</c> blocks synchronously on
/// <c>WorkspaceHost.StartAsync</c>/<c>ShutdownAsync</c> before
/// <c>desktop.MainWindow</c> is even assigned, and
/// <c>DesktopSessionState</c>'s own five loads run inside
/// <c>MainWindow</c>'s constructor, called from that same pre-loop path —
/// so neither can contend with a UI-thread caller because there is no
/// pumped UI thread yet for either to block. Excepted by file.</description></item>
/// <item><description><c>Editors/ObjectEditorView.cs</c>'s own <c>TryCreate</c>
/// keeps exactly one blocking existence check, disclosed on the method's
/// own remarks: its signature is fixed by <c>DocumentAreaView</c>'s
/// synchronous <c>Func&lt;IWorkspaceView, Control&gt;</c> content-builder
/// contract (not owned by this Work Package), and converting it cascades
/// into every <c>ShowTab</c> caller across the shell —
/// <c>QuickAccessToolbarFactory.cs</c> and <c>MainWindow.cs</c> among
/// them. Excepted by file and by the exact call it names, so any other
/// blocking call this file ever grows is still caught.</description></item>
/// </list>
/// <para>
/// <b>`WP 18.1A-R1` (`TD-108`, `TD-118`): the Workspace read surface.</b>
/// The audit that opened this Work Package found the original `WP 18.1A`
/// guard scanned only <c>src/Tempest.Desktop</c> while
/// <c>EngineeringCockpit</c> and four of its six per-discipline
/// <c>*CockpitReadModel</c> collaborators still blocked the UI thread via
/// <c>Dispatcher.UIThread.Post</c> → <c>CockpitView.Refresh</c> → a
/// synchronous <c>GetAwaiter().GetResult()</c> read. Every one of those —
/// <c>EngineeringCockpit.cs</c> and all six <c>*CockpitReadModel.cs</c>
/// files — is now converted: each exposes an async <c>LoadAsync</c>/
/// <c>PrimeAsync</c> that <c>CockpitView.RefreshAsync</c> awaits once per
/// render, and no property performs I/O of its own any more. What
/// remains, named exactly below, is the synchronous <c>IWorkspaceViewFactory.Create</c>
/// factory-contract bridge (an interface this Work Package does not own)
/// and three small, pre-existing, self-disclosed sites unrelated to the
/// Cockpit read surface.
/// </para>
/// </remarks>
public sealed class NoBlockingPersistenceCallsTests
{
    private static readonly string DesktopSourceRoot = Path.Combine(DesktopTestHelpers.RepositoryRoot, "src", "Tempest.Desktop");

    /// <summary>Files allowed to block because they run before the dispatcher loop starts — see class remarks.</summary>
    private static readonly string[] AllowedStartupFiles =
    [
        "App.cs",
        Path.Combine("Composition", "DesktopSessionState.cs"),
    ];

    private static readonly string ObjectEditorViewRelativePath = Path.Combine("Editors", "ObjectEditorView.cs");

    /// <summary>The one disclosed exception's own exact call text — see class remarks.</summary>
    private const string DisclosedObjectEditorViewCall = "domainContext.Repository.FindAsync(objectId).GetAwaiter().GetResult();";

    private static IEnumerable<string> DesktopSourceFiles() =>
        Directory.EnumerateFiles(DesktopSourceRoot, "*.cs", SearchOption.AllDirectories);

    private static string DesktopRelativePath(string file) => Path.GetRelativePath(DesktopSourceRoot, file);

    [Fact]
    public void NoGetAwaiterGetResult_OutsideTheTwoDisclosedExceptions()
    {
        var violations = new List<string>();

        foreach (var file in DesktopSourceFiles())
        {
            var relative = DesktopRelativePath(file);

            if (AllowedStartupFiles.Contains(relative, StringComparer.OrdinalIgnoreCase))
                continue;

            foreach (var (lineNumber, line) in BlockingGetResultLines(file))
            {
                if (string.Equals(relative, ObjectEditorViewRelativePath, StringComparison.OrdinalIgnoreCase)
                    && line.Contains(DisclosedObjectEditorViewCall, StringComparison.Ordinal))
                    continue;

                violations.Add($"{relative}:{lineNumber}: {line.Trim()}");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Blocking 'GetAwaiter().GetResult()' found outside the two disclosed exceptions (see this test's own class remarks):" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void NoTaskResultOrWait_Anywhere()
    {
        var violations = new List<string>();

        foreach (var file in DesktopSourceFiles())
        {
            var relative = DesktopRelativePath(file);

            foreach (var (lineNumber, line) in BlockingResultOrWaitLines(file))
                violations.Add($"{relative}:{lineNumber}: {line.Trim()}");
        }

        Assert.True(
            violations.Count == 0,
            "Blocking '.Result'/'.Wait()' on a Task found:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// Pins the exception list itself down to exactly the files/calls this
    /// test's own remarks name — so a change to either exception is a
    /// deliberate edit to this test, never a silent widening.
    /// </summary>
    [Fact]
    public void TheDisclosedExceptions_AreExactlyTwoStartupFiles_AndOneNamedCallInObjectEditorView()
    {
        Assert.Equal(2, AllowedStartupFiles.Length);
        Assert.Contains("App.cs", AllowedStartupFiles);
        Assert.Contains(Path.Combine("Composition", "DesktopSessionState.cs"), AllowedStartupFiles);

        var objectEditorViewPath = Path.Combine(DesktopSourceRoot, "Editors", "ObjectEditorView.cs");
        Assert.True(File.Exists(objectEditorViewPath), $"Expected to find '{objectEditorViewPath}'.");

        var occurrences = File.ReadAllLines(objectEditorViewPath)
            .Count(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)
                && line.Contains("GetAwaiter().GetResult()", StringComparison.Ordinal));

        Assert.Equal(1, occurrences);
    }

    // ================================================================
    // `src/Tempest.Workspace` — `WP 18.1A-R1` (`TD-108`, `TD-118`).
    // ================================================================

    private static readonly string WorkspaceSourceRoot = Path.Combine(DesktopTestHelpers.RepositoryRoot, "src", "Tempest.Workspace");

    /// <summary>
    /// Every remaining disclosed blocking-call site in
    /// <c>src/Tempest.Workspace</c>, named by its file's own path relative
    /// to that root, with the exact count of occurrences that file still
    /// carries and the one-line reason it is excused. A file not listed
    /// here carries none.
    /// </summary>
    /// <remarks>
    /// Twenty-five sites across seven files closed this Work Package
    /// (<c>EngineeringCockpit.cs</c> and all six <c>*CockpitReadModel.cs</c>
    /// collaborators) — see the class remarks. These eleven, across nine
    /// files, remain:
    /// </remarks>
    private static readonly Dictionary<string, (int Count, string Reason)> AllowedWorkspaceBlockingCallSites = new(StringComparer.OrdinalIgnoreCase)
    {
        // The six `IWorkspaceViewFactory.Create` implementations bridging
        // that frozen, synchronous `WP8.0B` factory contract (not owned by
        // this Work Package — see `RequirementsWorkspaceViewFactory.cs`'s
        // own disclosed remarks, which the other five mirror exactly) to a
        // single keyed lookup on the platform's one store. Requirements
        // carries three call sites (one per Requirements Kind its own
        // `Create` switches over); every other factory carries one. Every
        // path below is relative to src/Tempest.Workspace, one level above
        // the Workspace/ subdirectory every one of these files lives in.
        [Path.Combine("Workspace", "Requirements", "RequirementsWorkspaceViewFactory.cs")] =
            (3, "IWorkspaceViewFactory.Create is a synchronous WP8.0B contract this Work Package does not own; disclosed on the method's own remarks."),
        [Path.Combine("Workspace", "Calculations", "CalculationsWorkspaceViewFactory.cs")] =
            (1, "Same IWorkspaceViewFactory.Create sync/async boundary as RequirementsWorkspaceViewFactory.cs."),
        [Path.Combine("Workspace", "Documents", "DocumentsWorkspaceViewFactory.cs")] =
            (1, "Same IWorkspaceViewFactory.Create sync/async boundary as RequirementsWorkspaceViewFactory.cs."),
        [Path.Combine("Workspace", "Manufacturing", "ManufacturingWorkspaceViewFactory.cs")] =
            (1, "Same IWorkspaceViewFactory.Create sync/async boundary as RequirementsWorkspaceViewFactory.cs."),
        [Path.Combine("Workspace", "Mechanical", "MechanicalWorkspaceViewFactory.cs")] =
            (1, "Same IWorkspaceViewFactory.Create sync/async boundary as RequirementsWorkspaceViewFactory.cs."),
        [Path.Combine("Workspace", "Verification", "VerificationActivityWorkspaceViewFactory.cs")] =
            (1, "Same IWorkspaceViewFactory.Create sync/async boundary as RequirementsWorkspaceViewFactory.cs."),

        // Owned by a parallel Work Package (`WP 18.2B`) — out of this
        // Work Package's scope ("do not touch ... any Evidence view").
        [Path.Combine("Workspace", "Evidence", "EvidenceObjectView.cs")] =
            (1, "Owned by WP 18.2B (Evidence workspace), running in parallel; out of this Work Package's scope."),

        // Pre-existing, self-disclosed, unrelated to the Cockpit read
        // surface `TD-108`/`TD-118` named.
        [Path.Combine("Workspace", "Macros", "MacroWorkspaceRegistration.cs")] =
            (1, "Runs once during Workspace startup composition, before any UI-thread caller exists to contend with — the same startup shape App.cs/DesktopSessionState.cs are excepted for above."),
        [Path.Combine("Workspace", "WorkspaceManager.cs")] =
            (1, "ThrowIfHostRunFaulted's own disclosed non-blocking rethrow: only called once _hostRunTask.IsCompleted is already true, so GetResult() returns immediately rather than blocking."),
    };

    private static IEnumerable<string> WorkspaceSourceFiles() =>
        Directory.EnumerateFiles(WorkspaceSourceRoot, "*.cs", SearchOption.AllDirectories);

    private static string WorkspaceRelativePath(string file) => Path.GetRelativePath(WorkspaceSourceRoot, file);

    [Fact]
    public void NoGetAwaiterGetResult_InWorkspace_OutsideTheDisclosedAllowList()
    {
        var violations = new List<string>();
        var seenCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in WorkspaceSourceFiles())
        {
            var relative = WorkspaceRelativePath(file);
            var lines = BlockingGetResultLines(file).ToList();

            if (lines.Count == 0)
                continue;

            if (!AllowedWorkspaceBlockingCallSites.TryGetValue(relative, out var allowed))
            {
                foreach (var (lineNumber, line) in lines)
                    violations.Add($"{relative}:{lineNumber}: {line.Trim()}");
                continue;
            }

            seenCounts[relative] = lines.Count;

            if (lines.Count > allowed.Count)
            {
                foreach (var (lineNumber, line) in lines.Skip(allowed.Count))
                    violations.Add($"{relative}:{lineNumber}: {line.Trim()} (exceeds the {allowed.Count} allow-listed for this file)");
            }
        }

        // A file allow-listed for N sites that now carries fewer than N is
        // stale — the count "can only go down" per this Work Package's own
        // brief, but only as a deliberate edit to the list below, never as
        // a silent drift nobody notices.
        foreach (var (relative, allowed) in AllowedWorkspaceBlockingCallSites)
        {
            var actual = seenCounts.GetValueOrDefault(relative, 0);
            if (actual < allowed.Count)
            {
                violations.Add(
                    $"{relative} is allow-listed for {allowed.Count} site(s) but now carries only {actual} — "
                    + "lower AllowedWorkspaceBlockingCallSites' own count rather than leaving it stale.");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Blocking 'GetAwaiter().GetResult()' found in src/Tempest.Workspace outside the disclosed allow-list (see this test's own class remarks):" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void NoTaskResultOrWait_InWorkspace_Anywhere()
    {
        var violations = new List<string>();

        foreach (var file in WorkspaceSourceFiles())
        {
            var relative = WorkspaceRelativePath(file);

            foreach (var (lineNumber, line) in BlockingResultOrWaitLines(file))
                violations.Add($"{relative}:{lineNumber}: {line.Trim()}");
        }

        Assert.True(
            violations.Count == 0,
            "Blocking '.Result'/'.Wait()' on a Task found in src/Tempest.Workspace:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    /// <summary>
    /// Pins the Workspace allow-list itself: exactly eleven disclosed
    /// sites across exactly nine files — down from the thirty-six this
    /// Work Package found across the whole of <c>src/Tempest.Workspace</c>
    /// once the twenty-five sites across the seven Cockpit-owned files
    /// (<c>EngineeringCockpit.cs</c> and all six <c>*CockpitReadModel.cs</c>
    /// collaborators) were fixed. A future fix that closes one of these
    /// eleven must shrink this test deliberately; nothing here can
    /// silently widen.
    /// </summary>
    [Fact]
    public void TheWorkspaceAllowList_PinsExactlyElevenSites_AcrossNineFiles()
    {
        Assert.Equal(9, AllowedWorkspaceBlockingCallSites.Count);
        Assert.Equal(11, AllowedWorkspaceBlockingCallSites.Values.Sum(v => v.Count));

        foreach (var (relative, allowed) in AllowedWorkspaceBlockingCallSites)
        {
            var path = Path.Combine(WorkspaceSourceRoot, relative);
            Assert.True(File.Exists(path), $"Expected to find '{path}'.");
            Assert.False(string.IsNullOrWhiteSpace(allowed.Reason), $"{relative} is allow-listed without a reason.");
        }
    }

    /// <summary>
    /// Every Cockpit-owned file this Work Package fixed — pinned so a
    /// regression (a future edit reintroducing a blocking call in one of
    /// these) is caught here even though the general scan above would
    /// also catch it: this test names the claim, that one only enforces
    /// it.
    /// </summary>
    [Fact]
    public void TheCockpitReadSurface_CarriesNoBlockingCallsOfItsOwn_AnyMore()
    {
        string[] fixedFiles =
        [
            Path.Combine("Workspace", "EngineeringCockpit.cs"),
            Path.Combine("Workspace", "Mechanical", "MechanicalCockpitReadModel.cs"),
            Path.Combine("Workspace", "Requirements", "RequirementsCockpitReadModel.cs"),
            Path.Combine("Workspace", "Calculations", "CalculationsCockpitReadModel.cs"),
            Path.Combine("Workspace", "Documents", "DocumentsCockpitReadModel.cs"),
            Path.Combine("Workspace", "Verification", "VerificationCockpitReadModel.cs"),
            Path.Combine("Workspace", "Manufacturing", "ManufacturingCockpitReadModel.cs"),
        ];

        foreach (var relative in fixedFiles)
        {
            var path = Path.Combine(WorkspaceSourceRoot, relative);
            Assert.True(File.Exists(path), $"Expected to find '{path}'.");
            Assert.False(AllowedWorkspaceBlockingCallSites.ContainsKey(relative), $"{relative} should no longer need an allow-list entry.");
            Assert.Empty(BlockingGetResultLines(path));
        }
    }

    // ------------------------------------------------------------
    // Shared scanning helpers.
    // ------------------------------------------------------------

    /// <summary>Every non-comment line in <paramref name="file"/> containing a blocking <c>GetAwaiter().GetResult()</c> call, 1-based.</summary>
    private static IEnumerable<(int LineNumber, string Line)> BlockingGetResultLines(string file)
    {
        var lines = File.ReadAllLines(file);
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();

            // Doc comments and ordinary comments name the pattern in prose
            // (this file's own remarks are one example) without being the
            // blocking call itself.
            if (trimmed.StartsWith("///", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal))
                continue;

            if (lines[i].Contains("GetAwaiter().GetResult()", StringComparison.Ordinal))
                yield return (i + 1, lines[i]);
        }
    }

    /// <summary>
    /// Every line in <paramref name="file"/> matching an async-named call
    /// immediately followed by <c>.Result</c>/<c>.Wait()</c> — narrower
    /// than every bare <c>.Result</c>, which would also flag a plain
    /// property on an already-awaited value (e.g. <c>CommandInvocation.Result</c>)
    /// as a false positive.
    /// </summary>
    private static IEnumerable<(int LineNumber, string Line)> BlockingResultOrWaitLines(string file)
    {
        var resultPattern = new Regex(@"Async\([^()]*\)\.Result\b", RegexOptions.Compiled);
        var waitPattern = new Regex(@"Async\([^()]*\)\.Wait\(\)", RegexOptions.Compiled);

        var lines = File.ReadAllLines(file);
        for (var i = 0; i < lines.Length; i++)
        {
            if (resultPattern.IsMatch(lines[i]) || waitPattern.IsMatch(lines[i]))
                yield return (i + 1, lines[i]);
        }
    }
}
