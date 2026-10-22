using Tempest.Core.Persistence;
using Tempest.Desktop.Startup;

namespace Tempest.Desktop.Tests.Startup;

/// <summary>
/// <see cref="PersistenceRootResolver"/>'s own claims (`WP 21.5A`, `WP
/// RC.0A` scope item 2): an explicit <c>--persistence-root</c> argument or
/// an already-configured <c>Persistence:RootPath</c> always wins outright;
/// a not-installed run is untouched; an installed run with nothing recorded
/// yet asks (via <see cref="PersistenceRootResolution.ShowFirstRunDialog"/>)
/// exactly once, and never again once <see cref="PersistenceRootResolver.RecordFirstRunChoice"/>
/// has run. Exercised entirely through <see cref="FakeInstalledAppLocator"/>
/// — the substitutable seam the brief asks for — so none of this needs a
/// real Velopack install, or even the real <c>Velopack</c> package, in
/// scope.
/// </summary>
public sealed class PersistenceRootResolverTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "tempestos-tests-firstrun-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void CliArgument_wins_outright_even_when_installed_and_configured()
    {
        var resolver = new PersistenceRootResolver(new FakeInstalledAppLocator(isInstalled: true, _tempDirectory));

        var resolution = resolver.Resolve(
            ["--persistence-root", @"D:\Somewhere\Else"],
            new Dictionary<string, string> { [SqlitePersistenceStore.RootPathConfigurationKey] = @"D:\Configured" });

        Assert.Equal(@"D:\Somewhere\Else", resolution.RootPathOverride);
        Assert.False(resolution.ShowFirstRunDialog);
    }

    [Fact]
    public void CliArgument_equals_form_is_also_recognised()
    {
        var resolver = new PersistenceRootResolver(new FakeInstalledAppLocator(isInstalled: false, installedDataDirectory: null));

        var resolution = resolver.Resolve(["--persistence-root=D:\\Data"], new Dictionary<string, string>());

        Assert.Equal(@"D:\Data", resolution.RootPathOverride);
        Assert.False(resolution.ShowFirstRunDialog);
    }

    [Fact]
    public void Configured_RootPath_wins_over_the_installed_default_and_shows_no_dialog()
    {
        var resolver = new PersistenceRootResolver(new FakeInstalledAppLocator(isInstalled: true, _tempDirectory));

        var resolution = resolver.Resolve(
            [],
            new Dictionary<string, string> { [SqlitePersistenceStore.RootPathConfigurationKey] = @"D:\Configured" });

        // No override is passed — the platform's own ordinary configuration
        // precedence already has it — but no dialog either.
        Assert.Null(resolution.RootPathOverride);
        Assert.False(resolution.ShowFirstRunDialog);
    }

    [Fact]
    public void NotInstalled_leaves_todays_default_untouched()
    {
        var resolver = new PersistenceRootResolver(new FakeInstalledAppLocator(isInstalled: false, installedDataDirectory: null));

        var resolution = resolver.Resolve([], new Dictionary<string, string>());

        Assert.Null(resolution.RootPathOverride);
        Assert.False(resolution.ShowFirstRunDialog);
        Assert.Equal(SqlitePersistenceStore.DefaultRootPath, resolution.SuggestedDefaultRoot);
    }

    [Fact]
    public void Installed_with_no_recorded_choice_asks_once_with_the_installed_default_suggested()
    {
        var resolver = new PersistenceRootResolver(new FakeInstalledAppLocator(isInstalled: true, _tempDirectory));

        var resolution = resolver.Resolve([], new Dictionary<string, string>());

        Assert.Null(resolution.RootPathOverride);
        Assert.True(resolution.ShowFirstRunDialog);
        Assert.Equal(Path.Combine(_tempDirectory, "persistence-data"), resolution.SuggestedDefaultRoot);
    }

    [Fact]
    public void Installed_with_a_recorded_choice_never_asks_again()
    {
        var locator = new FakeInstalledAppLocator(isInstalled: true, _tempDirectory);
        var resolver = new PersistenceRootResolver(locator);

        resolver.RecordFirstRunChoice(@"E:\Operator\Chosen\Folder");

        var resolution = resolver.Resolve([], new Dictionary<string, string>());

        Assert.Equal(@"E:\Operator\Chosen\Folder", resolution.RootPathOverride);
        Assert.False(resolution.ShowFirstRunDialog);
    }

    [Fact]
    public void RecordFirstRunChoice_throws_when_not_installed()
    {
        var resolver = new PersistenceRootResolver(new FakeInstalledAppLocator(isInstalled: false, installedDataDirectory: null));

        Assert.Throws<InvalidOperationException>(() => resolver.RecordFirstRunChoice(@"D:\Anywhere"));
    }

    /// <summary>v0.25.2 independent test P1: a choice recorded inside Velopack's install root is carried out, and the old default data moves with it.</summary>
    [Fact]
    public void MigrateLegacyChoice_CarriesTheChoiceOut_AndMovesTheOldDefaultData()
    {
        var legacy = Path.Combine(_tempDirectory, "TempestOS");
        var current = Path.Combine(_tempDirectory, "TempestOS-Data");
        var legacyLocator = new FakeInstalledAppLocator(isInstalled: true, legacy);
        new PersistenceRootResolver(legacyLocator).RecordFirstRunChoice(Path.Combine(legacy, "persistence-data"));
        Directory.CreateDirectory(Path.Combine(legacy, "persistence-data", "secrets"));
        File.WriteAllText(Path.Combine(legacy, "persistence-data", "tempest.db"), "data");

        var resolver = new PersistenceRootResolver(new FakeInstalledAppLocator(isInstalled: true, current, legacy));
        var carried = resolver.MigrateLegacyChoice();

        var moved = Path.Combine(current, "persistence-data");
        Assert.Equal(moved, carried);
        Assert.True(File.Exists(Path.Combine(moved, "tempest.db")));
        Assert.True(Directory.Exists(Path.Combine(moved, "secrets")));
        Assert.False(Directory.Exists(Path.Combine(legacy, "persistence-data")));
        var resolution = resolver.Resolve([], new Dictionary<string, string>());
        Assert.False(resolution.ShowFirstRunDialog);
        Assert.Equal(moved, resolution.RootPathOverride);

        // A second launch finds the new choice and carries nothing.
        Assert.Null(resolver.MigrateLegacyChoice());
    }

    [Fact]
    public void MigrateLegacyChoice_KeepsAChosenFolderElsewhere_WhereItIs()
    {
        var legacy = Path.Combine(_tempDirectory, "TempestOS");
        var current = Path.Combine(_tempDirectory, "TempestOS-Data");
        new PersistenceRootResolver(new FakeInstalledAppLocator(isInstalled: true, legacy)).RecordFirstRunChoice(@"C:\TempestOS-rc25-data");

        var resolver = new PersistenceRootResolver(new FakeInstalledAppLocator(isInstalled: true, current, legacy));

        Assert.Equal(@"C:\TempestOS-rc25-data", resolver.MigrateLegacyChoice());
        Assert.Equal(@"C:\TempestOS-rc25-data", resolver.Resolve([], new Dictionary<string, string>()).RootPathOverride);
    }

    [Fact]
    public void MigrateLegacyChoice_WithNothingRecorded_DoesNothing_AndTheDialogAsks()
    {
        var resolver = new PersistenceRootResolver(new FakeInstalledAppLocator(isInstalled: true, Path.Combine(_tempDirectory, "new"), Path.Combine(_tempDirectory, "old")));

        Assert.Null(resolver.MigrateLegacyChoice());
        Assert.True(resolver.Resolve([], new Dictionary<string, string>()).ShowFirstRunDialog);
    }

    private sealed class FakeInstalledAppLocator(bool isInstalled, string? installedDataDirectory, string? legacyDataDirectory = null) : IInstalledAppLocator
    {
        public bool IsInstalled => isInstalled;
        public string? InstalledDataDirectory => installedDataDirectory;
        public string? LegacyDataDirectory => legacyDataDirectory;
    }
}
