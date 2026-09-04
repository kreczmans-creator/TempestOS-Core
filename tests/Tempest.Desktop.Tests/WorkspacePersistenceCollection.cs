namespace Tempest.Desktop.Tests;

/// <summary>
/// Serialises every test that builds a real <see cref="WorkspaceHost"/> —
/// each test isolates its own persisted state via
/// <see cref="NewIsolatedPersistenceRootPath"/> (`WP 10.1B`, below), but
/// this collection's own headless Avalonia dispatcher/UI-thread machinery
/// is still process-wide, so running these tests in parallel (xUnit's own
/// default) remains unsafe for reasons unrelated to persisted state —
/// serialised here exactly like this project's own <c>[Collection("Console
/// output capture")]</c> convention serialises a different class of
/// process-wide shared state.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>TD-37</c> fix (`WP 10.1B`):</b> every <see cref="WorkspaceHost"/>
/// constructed by this test assembly must pass a path from
/// <see cref="NewIsolatedPersistenceRootPath"/> to its own constructor,
/// rather than defaulting to <c>PersistenceStore.DefaultRootPath</c> (the
/// real application's own working-directory-relative, cross-launch-durable
/// store). Root-caused via direct file-system evidence, `WP 10.1B`: this
/// test assembly's own build output directory's <c>persistence-data/</c>
/// folder — reused, unmodified, run after run, exactly like the real
/// desktop application's own launch directory — already contained
/// `SAMPLE-MAT-001`/`SAMPLE-REQ-001` (etc.) from an earlier test run
/// before the affected `Tempest.Samples` module ever ran a second time;
/// `EngineeringDocumentStore`/`MaterialCatalog`/`RequirementsService` are
/// all built directly on <see cref="Tempest.Core.Persistence.IPersistenceStore"/>
/// (`ADR-0053`/`ADR-0055`/`ADR-0058`), the same durable substrate `TD-36`
/// already named for Settings alone — sample modules seeding fixed,
/// literal business identifiers therefore collided with data their own
/// earlier run had already durably written, not with each other and not
/// with any double-invocation.
/// </para>
/// <para>
/// <b>One fresh path per independent test, not one shared path for the
/// whole assembly run:</b> a single assembly-wide path would still let
/// test <em>N</em> collide with test <em>N-1</em>'s own already-seeded
/// data within the same run (each test's own <see cref="WorkspaceHost"/>
/// starts a brand new, empty in-memory <c>IEngineeringObjectRepository</c>
/// regardless of what is already durably on disk — `WP10.1B Root Cause
/// Analysis.md` §3 — so a "successfully skipped, already seeded" sample
/// module would leave that test's own Cockpit reads honestly empty rather
/// than genuinely populated). Calling <see cref="NewIsolatedPersistenceRootPath"/>
/// fresh, once per independent test, gives every ordinary (single-host)
/// test a genuinely empty store, exactly like `Tempest.Core.Tests` has
/// given every <see cref="Tempest.Core.Runtime.ITempestHostBuilder"/>
/// construction since `WP 7.3A`. The two tests that deliberately construct
/// two sequential <see cref="WorkspaceHost"/> instances to prove
/// cross-restart session persistence (`ADR-0064`, `TD-35`) instead call
/// this method once and pass the same resulting path to both — the one
/// case where two hosts are supposed to observe each other's persisted
/// state within a single test.
/// </para>
/// <para>
/// <b><c>TD-120</c> fix (`WP 15.2A`):</b> every isolated root this method
/// returns now lives under one shared, per-test-run parent directory
/// (<see cref="RunRootPath"/>), generated once when this class is first
/// loaded — not scattered directly under the OS temp folder, one entry
/// per test, forever. <see cref="PersistenceRootCleanupFixture"/>, wired
/// to this collection via <see cref="ICollectionFixture{TFixture}"/>,
/// deletes that entire parent directory once, in its own
/// <see cref="IDisposable.Dispose"/>, when every test in this collection
/// has finished — the same point xUnit already guarantees no test in this
/// collection is still using its own subdirectory. A machine running the
/// suite many times over (exactly what release verification does) no
/// longer accumulates one directory per test per run; a run that never
/// reaches its own clean shutdown (a crash, a disk-exhaustion kill, a
/// forceful interrupt) leaves its directory behind — deliberately: that
/// is also the one situation a real diagnosis might need it. This
/// resolves the design question `TD-120`'s own register entry left open
/// ("depends on whether a failed test's persistence root should survive
/// for diagnosis") without special-casing individual test outcomes, which
/// xUnit v2 collection fixtures cannot observe: an ordinary assertion
/// failure inside a test already carries its own failure message and
/// stack trace, so it does not need its directory kept; the only
/// situation this design leaves a directory behind is the one situation
/// severe enough that this fixture's own <c>Dispose()</c> never got to
/// run at all.
/// </para>
/// </remarks>
[CollectionDefinition("Tempest.Desktop WorkspaceHost persistence", DisableParallelization = true)]
public sealed class WorkspacePersistenceCollection : ICollectionFixture<PersistenceRootCleanupFixture>
{
    /// <summary>
    /// The single parent directory every isolated persistence root this
    /// test run creates lives under — one per process, not one per test.
    /// </summary>
    internal static readonly string RunRootPath =
        Path.Combine(Path.GetTempPath(), $"TempestOS.Desktop.Tests.Run.{Guid.NewGuid():N}");

    /// <summary>
    /// Returns a fresh, uniquely-named directory under this run's own
    /// shared <see cref="RunRootPath"/> for use as a <see cref="WorkspaceHost"/>'s
    /// own isolated <see cref="Tempest.Core.Persistence.IPersistenceStore"/>
    /// root path — call once per independent test (or once per test that
    /// deliberately shares one store across two sequential hosts), never
    /// reused across two unrelated tests.
    /// </summary>
    public static string NewIsolatedPersistenceRootPath() =>
        Path.Combine(RunRootPath, $"{Guid.NewGuid():N}");
}

/// <summary>
/// Deletes <see cref="WorkspacePersistenceCollection.RunRootPath"/> once,
/// when every test in the "Tempest.Desktop WorkspaceHost persistence"
/// collection has finished — see that class's own remarks for why a
/// collection fixture, not a per-test cleanup, is the right lifetime for
/// this (`TD-120`, `WP 15.2A`).
/// </summary>
public sealed class PersistenceRootCleanupFixture : IDisposable
{
    public void Dispose() =>
        TestTempDirectoryCleanup.TryDeleteDirectoryRecursively(WorkspacePersistenceCollection.RunRootPath);
}

/// <summary>
/// The actual recursive-delete mechanism <see cref="PersistenceRootCleanupFixture"/>
/// relies on, factored out so it can be exercised directly against a
/// throwaway directory rather than against the real, live
/// <see cref="WorkspacePersistenceCollection.RunRootPath"/> — deleting the
/// real one mid-test-run to prove the mechanism works would defeat the
/// isolation this whole file exists to provide (`TD-120`, `WP 15.2A`).
/// </summary>
public static class TestTempDirectoryCleanup
{
    /// <summary>
    /// Deletes <paramref name="path"/> and everything under it if it
    /// exists; a no-op, not a throw, if it does not (a run that created no
    /// isolated roots at all — every test skipped or filtered out — must
    /// not fail its own cleanup for having nothing to clean up). Best
    /// effort: an individual file or subdirectory the OS still has locked
    /// (observed nowhere in this suite, but not provably impossible) is
    /// swallowed rather than allowed to fail test-run teardown over disk
    /// hygiene, which is exactly the class of problem this method exists
    /// to reduce, not add to.
    /// </summary>
    public static void TryDeleteDirectoryRecursively(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort — see the method's own remarks.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort — see the method's own remarks.
        }
    }
}
