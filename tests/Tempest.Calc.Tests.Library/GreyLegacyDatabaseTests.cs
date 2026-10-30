using System.IO.Compression;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Calculations;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;

namespace Tempest.Calc.Tests.Library;

/// <summary>
/// Grey team (ADR-0167 §2): real databases written by the released v0.25.3
/// and v0.26.0-rc1 code — one or more stored runs of every one of the 19
/// legacy product calculations, 46 records, with the product's record index
/// — open under the current code and migrate to archive worksheets with
/// nothing lost: every record becomes an archive, every archive renders, the
/// records themselves stay readable, and a second migration adds nothing.
/// This is the test the legacy surface's retirement waits on.
/// </summary>
/// <remarks>
/// Fixtures: <c>Fixtures/legacy-*.db.gz</c>, produced by each release's own
/// legacy module tests (worked examples and vectors) executed against a SQLite
/// store, plus the five original definitions. Regenerating them is described
/// in <c>docs/reviews/grey-team.md</c>.
/// </remarks>
public sealed class GreyLegacyDatabaseTests
{
    public const int LegacyCalculations = 19;
    public const int Records = 46;

    private static string Unpack(string fixture)
    {
        var root = Path.Combine(Path.GetTempPath(), "tempest-grey-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var target = File.Create(Path.Combine(root, "tempest.db"));
        gzip.CopyTo(target);
        return root;
    }

    private static SqlitePersistenceStore Open(string root) =>
        new(new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource([new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, root)]))
            .Build());

    private static CalculationEngine Engine(SqlitePersistenceStore store)
    {
        var principals = new CurrentPrincipalAccessor();
        return new CalculationEngine(new EngineeringDocumentStore(store, principals), principals, recordIndex: store);
    }

    [Theory]
    [InlineData("legacy-v0.25.3.db.gz")]
    [InlineData("legacy-v0.26.0-rc1.db.gz")]
    public async Task Every_Record_Of_A_Released_Database_Migrates_To_A_Readable_Archive(string fixture)
    {
        var root = Unpack(fixture);

        try
        {
            {
                await using var store = Open(root);
                var engine = Engine(store);
                var records = await engine.ListRecordsAsync();
                Assert.Equal(Records, records.Count);
                Assert.Equal(LegacyCalculations, records.Select(r => r.CalculationId).Distinct(StringComparer.Ordinal).Count());

                var calcStore = new CalcInstanceStore(store);
                var report = await new LegacyCalculationMigration(engine, calcStore).MigrateAsync();
                Assert.Empty(report.Failed);
                Assert.Equal(Records, report.Migrated);

                var archives = (await calcStore.ListInstancesAsync()).Where(i => i.IsLegacyArchive).ToList();
                Assert.Equal(records.Select(r => r.Id).Order(), archives.Select(a => a.Id).Order());
                Assert.Equal(LegacyCalculations, archives.Select(a => a.TemplateId).Distinct(StringComparer.Ordinal).Count());

                foreach (var archive in archives)
                {
                    Assert.Equal(CalcInstanceStatus.Draft, archive.Status);
                    Assert.NotEmpty(archive.Inputs);
                    Assert.All(archive.Inputs, i => Assert.Equal(CalcProvenanceKind.Manual, i.Provenance?.Kind));
                    Assert.True(archive.Outputs.Count > 0 || archive.Checks.Count > 0, $"{archive.Name} carries no figure.");

                    var text = CalcSheetTextRenderer.Render(LegacyArchiveSheet.Build(archive, "—", "grey", "engine", null, archive.CreatedAt));
                    Assert.Contains("Archive of a legacy calculation record", text, StringComparison.Ordinal);
                    Assert.DoesNotContain("NaN", text, StringComparison.Ordinal);
                    Assert.NotNull(await engine.FindRecordDocumentAsync(archive.Id));
                }
            }

            // Reopened: the archives persisted, the legacy records are untouched, and a second migration adds nothing.
            {
                await using var store = Open(root);
                var engine = Engine(store);
                Assert.Equal(Records, (await engine.ListRecordsAsync()).Count);
                var again = await new LegacyCalculationMigration(engine, new CalcInstanceStore(store)).MigrateAsync();
                Assert.Equal((0, Records), (again.Migrated, again.AlreadyMigrated));
                Assert.Empty(again.Failed);
            }
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // A Windows file lock released late; the temp folder is cleaned by the OS.
            }
        }
    }
}
