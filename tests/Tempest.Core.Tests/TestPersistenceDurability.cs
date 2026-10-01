using System.Runtime.CompilerServices;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;

namespace Tempest.Core.Tests;

/// <summary>
/// Runs every store a Core test builds through the host's own configuration
/// at <c>PRAGMA synchronous = NORMAL</c> (`ADR-0144` amendment, 2026-10-01),
/// the same module initialiser the Desktop suite has. The Core restart
/// tests seed shipped reference data on fresh roots and took 70-94 s each
/// at <c>FULL</c> on a Windows CI runner, where every commit waits on an
/// fsync (`v0.23.0` CI board G-10). It reaches only stores whose
/// configuration includes the environment-variable source; a test that
/// builds its configuration explicitly (the <c>FULL</c>/<c>NORMAL</c>
/// cases in <c>SqlitePersistenceStoreTests</c>) is unaffected and keeps
/// asserting <c>FULL</c> as the default.
/// </summary>
internal static class TestPersistenceDurability
{
    [ModuleInitializer]
    internal static void Relax() =>
        Environment.SetEnvironmentVariable(
            MicrosoftExtensionsConfigurationSource.EnvironmentVariablePrefix + SqlitePersistenceStore.SynchronousConfigurationKey.Replace(":", "__", StringComparison.Ordinal),
            SqlitePersistenceStore.RelaxedSynchronousValue);
}
