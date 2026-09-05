namespace Tempest.Core.Tests.Projects;

/// <summary>
/// The single, per-process parent directory every "project" test fixture's
/// isolated persistence root lives beneath — <c>GovernanceFixture</c>,
/// <c>TaskFixture</c>, <c>MilestoneFixture</c> (this namespace) and
/// <c>RegisterFixture</c> (<c>Tempest.Core.Tests.Shell.ProjectAreaRegisterTests</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Closes the Core-side leak <c>TD-120</c> (Technical Debt Register.md)
/// left open:</b> that row's own fix (`WP 15.2A`) covered only the Desktop
/// suite's <see cref="Docking.WorkspaceLayoutHost"/>-adjacent
/// <c>WorkspaceHost</c> roots. These four Core fixtures create an isolated
/// <c>Path.GetTempPath()/tempest-project-*-{guid}</c> root apiece — 65
/// call sites across the four files, one per fixture creation, none of it
/// ever deleted — and were never in scope for that Work Package.
/// </para>
/// <para>
/// Every root any of the four fixtures ever creates is nested under this
/// one, process-wide parent, purely so a crashed run's leftovers (a test
/// that throws before its own cleanup, `Dispose` itself failing) are
/// findable and removable as one directory instead of scattered loose
/// under the OS temp directory. The actual deletion is per-test, not
/// per-process: each of the four owning test classes implements
/// <see cref="IDisposable"/> and deletes exactly the root(s) its own
/// instance created (<c>ProjectGovernanceTests</c>, <c>ProjectTaskTests</c>,
/// <c>ProjectMilestoneTests</c>, <c>ProjectAreaRegisterTests</c>) — see each
/// class's own <c>Dispose</c>.
/// </para>
/// <para>
/// A shared <c>[CollectionDefinition]</c>/<see cref="Xunit.ICollectionFixture{TFixture}"/>
/// (the Desktop suite's own <c>WorkspacePersistenceCollection</c>
/// precedent, `TD-120`) was deliberately not used here: that would put all
/// four classes into one xUnit collection, which xUnit never runs two
/// classes from in parallel — serialising four classes that today run
/// concurrently with each other (each is its own default collection).
/// Per-test <see cref="IDisposable"/> disposal gets the same outcome, zero
/// directories left behind, without that cost.
/// </para>
/// </remarks>
internal static class ProjectFixtureRoot
{
    private static readonly string RootPath = Initialise();

    private static string Initialise()
    {
        var path = Path.Combine(Path.GetTempPath(), "tempestos-core-tests-" + Guid.NewGuid().ToString("N"));

        // Best-effort, not the primary mechanism: every individual test
        // already deletes its own subdirectory (each owning class's own
        // `Dispose`), so by the time the process exits this parent is
        // normally already empty. This only catches what per-test cleanup
        // could not — a test process killed rather than exited, or a
        // `Dispose` itself throwing — and never masks a real test failure
        // with a cleanup exception.
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
                // Best-effort only — see above.
            }
        };

        return path;
    }

    /// <summary>
    /// A fresh, isolated root for one fixture instance, nested under the
    /// shared per-process parent — mirrors
    /// <c>WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath</c>'s
    /// own shape.
    /// </summary>
    /// <param name="prefix">
    /// Which fixture this root belongs to (e.g. <c>"governance"</c>) —
    /// cosmetic only, so a leftover directory names its own origin.
    /// </param>
    public static string NewIsolatedRoot(string prefix) =>
        Path.Combine(RootPath, prefix + "-" + Guid.NewGuid().ToString("N"));
}
