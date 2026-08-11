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
/// </remarks>
[CollectionDefinition("Tempest.Desktop WorkspaceHost persistence", DisableParallelization = true)]
public sealed class WorkspacePersistenceCollection
{
    /// <summary>
    /// Returns a fresh, uniquely-named directory under the OS temporary
    /// folder for use as a <see cref="WorkspaceHost"/>'s own isolated
    /// <see cref="Tempest.Core.Persistence.IPersistenceStore"/> root path —
    /// call once per independent test (or once per test that deliberately
    /// shares one store across two sequential hosts), never reused across
    /// two unrelated tests.
    /// </summary>
    public static string NewIsolatedPersistenceRootPath() =>
        Path.Combine(Path.GetTempPath(), $"TempestOS.Desktop.Tests.{Guid.NewGuid():N}");
}
