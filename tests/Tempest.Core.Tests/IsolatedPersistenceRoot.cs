using Tempest.Core.Configuration;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;

namespace Tempest.Core.Tests;

/// <summary>
/// Gives every <see cref="ITempestHostBuilder"/> in this assembly its own
/// persistence root, so that no test in this suite ever writes into
/// <see cref="SqlitePersistenceStore.DefaultRootPath"/> — the real,
/// working-directory-relative folder the shipped application keeps a
/// user's data in.
/// </summary>
/// <remarks>
/// <para>
/// `WP 17.0A` established this rule for <c>PersistenceStoreTests</c>;
/// `WP 17.1A` extends it to every Host-building test, for two reasons.
/// The first is the original one: a suite run from the wrong working
/// directory was writing test data into, and could delete, a real store.
/// The second is new, and is why it could no longer be left alone —
/// <c>SqlitePersistenceStore</c> holds its root's <c>tempest.lock</c>
/// exclusively for its lifetime (`ADR-0144`), so two Hosts sharing the
/// default root are no longer merely untidy: whichever starts second is
/// refused, correctly, and the test fails for a reason that has nothing
/// to do with what it was testing.
/// </para>
/// <para>
/// Declared in this assembly's own root namespace deliberately: every
/// test namespace here is nested inside it, so the extension method
/// resolves without a <c>using</c>, and a Host built in a new test file
/// gets the isolation by writing one call rather than by remembering an
/// import.
/// </para>
/// </remarks>
internal static class IsolatedPersistenceRoot
{
    /// <summary>
    /// The single parent directory every isolated root this test run
    /// creates lives under — one per process, not one per test, so a
    /// machine running the suite repeatedly accumulates one directory per
    /// run rather than one per Host.
    /// </summary>
    internal static readonly string RunRootPath =
        Path.Combine(Path.GetTempPath(), $"TempestOS.Core.Tests.Run.{Guid.NewGuid():N}");

    static IsolatedPersistenceRoot()
    {
        // Best-effort, at the one point every Host in this run has
        // certainly released its store: xUnit v2 has no assembly-wide
        // teardown, and a collection fixture would have to be attached to
        // every collection that builds a Host to serve the same purpose.
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                if (Directory.Exists(RunRootPath))
                    Directory.Delete(RunRootPath, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A run that cannot clean up after itself must not fail
                // over disk hygiene.
            }
        };
    }

    /// <summary>
    /// A fresh, uniquely-named persistence root path under
    /// <see cref="RunRootPath"/>. The directory itself is created by the
    /// store, not here.
    /// </summary>
    internal static string NewPath() => Path.Combine(RunRootPath, Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Points <paramref name="builder"/>'s Host at a fresh persistence
    /// root of its own. Returns the builder, so it chains ahead of
    /// <c>Build()</c>.
    /// </summary>
    internal static ITempestHostBuilder WithIsolatedPersistenceRoot(this ITempestHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddConfigurationSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, NewPath()),
        ]));
    }
}
