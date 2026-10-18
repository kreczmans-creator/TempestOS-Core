using System.Runtime.CompilerServices;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Runs every store a Desktop test builds at <c>PRAGMA synchronous = NORMAL</c>
/// (`ADR-0144` amendment, 2026-10-01). Each test host starts on a fresh root
/// and seeds and releases every shipped reference record, a few thousand
/// commits; at <c>FULL</c> each one waits on an fsync, which on a Windows CI
/// runner made every host start about 14 seconds slower and pushed the
/// Desktop shards past their job timeout. Set through the same
/// environment-variable configuration source as <see cref="TestProjectFolders"/>,
/// so every host reads it exactly as a real launch reads its configuration.
/// </summary>
internal static class TestPersistenceDurability
{
    [ModuleInitializer]
    internal static void Relax() =>
        Environment.SetEnvironmentVariable(
            MicrosoftExtensionsConfigurationSource.EnvironmentVariablePrefix + SqlitePersistenceStore.SynchronousConfigurationKey.Replace(":", "__", StringComparison.Ordinal),
            SqlitePersistenceStore.RelaxedSynchronousValue);
}
