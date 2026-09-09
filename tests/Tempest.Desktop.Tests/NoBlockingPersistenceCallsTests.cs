using System.Text.RegularExpressions;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The `WP 18.1A` invariant, asserted structurally rather than
/// behaviourally: not "everything returns <see cref="Task"/>", but "no
/// blocking UI-thread access to persistence or workspace operations" —
/// <c>src/Tempest.Desktop</c> carries no <c>GetAwaiter().GetResult()</c>,
/// <c>.Result</c>, or <c>.Wait()</c> on a <see cref="Task"/>, outside two
/// named, disclosed exceptions.
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
/// <b>Two disclosed exceptions, named exactly rather than by whole file.</b>
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

    private static string RelativePath(string file) => Path.GetRelativePath(DesktopSourceRoot, file);

    [Fact]
    public void NoGetAwaiterGetResult_OutsideTheTwoDisclosedExceptions()
    {
        var violations = new List<string>();

        foreach (var file in DesktopSourceFiles())
        {
            var relative = RelativePath(file);

            if (AllowedStartupFiles.Contains(relative, StringComparer.OrdinalIgnoreCase))
                continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.TrimStart();

                // Doc comments and ordinary comments name the pattern in
                // prose (this file's own remarks are one example) without
                // being the blocking call itself.
                if (trimmed.StartsWith("///", StringComparison.Ordinal) || trimmed.StartsWith("//", StringComparison.Ordinal))
                    continue;

                if (!line.Contains("GetAwaiter().GetResult()", StringComparison.Ordinal))
                    continue;

                if (string.Equals(relative, ObjectEditorViewRelativePath, StringComparison.OrdinalIgnoreCase)
                    && line.Contains(DisclosedObjectEditorViewCall, StringComparison.Ordinal))
                    continue;

                violations.Add($"{relative}:{i + 1}: {line.Trim()}");
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
        // Matched narrowly — an async-named call immediately followed by
        // `.Result`/`.Wait()` — rather than every bare `.Result`, which
        // would also flag `CommandInvocation.Result` (a plain property on
        // an already-awaited value, not `Task<T>.Result`; see
        // `MainWindow.cs`'s own two legitimate reads of it) as a false
        // positive. No startup exception: unlike `GetAwaiter().GetResult()`,
        // this pattern has never had one in this codebase.
        var resultPattern = new Regex(@"Async\([^()]*\)\.Result\b", RegexOptions.Compiled);
        var waitPattern = new Regex(@"Async\([^()]*\)\.Wait\(\)", RegexOptions.Compiled);

        var violations = new List<string>();

        foreach (var file in DesktopSourceFiles())
        {
            var relative = RelativePath(file);
            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                if (resultPattern.IsMatch(lines[i]) || waitPattern.IsMatch(lines[i]))
                    violations.Add($"{relative}:{i + 1}: {lines[i].Trim()}");
            }
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
}
