using System.Text.Json;
using Tempest.Core.Persistence;

namespace Tempest.Desktop.Startup;

/// <summary>
/// What <see cref="PersistenceRootResolver.Resolve"/> decided: whether to
/// pass an explicit persistence-root override into
/// <see cref="WorkspaceHost"/>, and whether the first-run "where should
/// your data live" dialog should be shown before starting it.
/// </summary>
/// <param name="RootPathOverride">
/// The value to pass as <see cref="WorkspaceHost"/>'s own
/// <c>persistenceRootPathOverride</c> constructor argument, or
/// <see langword="null"/> to pass none and let the platform's own ordinary
/// configuration precedence (appsettings.json, environment, command line)
/// decide, exactly as it already does today.
/// </param>
/// <param name="ShowFirstRunDialog">
/// <see langword="true"/> only for an installed run with no explicit
/// override anywhere and no recorded first-run choice yet.
/// </param>
/// <param name="SuggestedDefaultRoot">
/// The path the first-run dialog should show as its own default — the
/// installed default when <paramref name="ShowFirstRunDialog"/> is
/// <see langword="true"/>; otherwise whatever root this resolution already
/// settled on, for a caller that wants to display or log it regardless.
/// </param>
public sealed record PersistenceRootResolution(string? RootPathOverride, bool ShowFirstRunDialog, string SuggestedDefaultRoot);

/// <summary>
/// Decides where an installed <c>Tempest.Desktop</c> run's persistence root
/// lives (`WP 21.5A`, `WP RC.0A` scope item 2) — a pure, substitutable seam
/// over <see cref="IInstalledAppLocator"/>, deliberately kept free of
/// Avalonia and of the real <c>Velopack</c> package so it can be exercised
/// directly by a unit test, per this Work Package's own brief ("a seam you
/// can substitute").
/// </summary>
/// <remarks>
/// <para><b>Precedence, highest to lowest</b> — the same ordering
/// `PHYSICAL_REVIEW.md` §4 already documents for every other configuration
/// key, with this class's own new <see cref="CliArgumentName"/> folded in
/// at the command-line tier it belongs to:</para>
/// <list type="number">
/// <item><description>an explicit <c>--persistence-root &lt;path&gt;</c>
/// command-line argument;</description></item>
/// <item><description>an explicit <see cref="SqlitePersistenceStore.RootPathConfigurationKey"/>
/// from the platform's own merged configuration (appsettings.json, a
/// <c>TEMPEST_</c> environment variable, or the generic
/// <c>--Persistence:RootPath=...</c> command-line form);</description></item>
/// <item><description>a not-installed run (<c>dotnet run</c>, a plain
/// <c>bin/</c> executable, the plain release zip): no override — today's
/// working-directory-relative default, completely
/// unchanged;</description></item>
/// <item><description>an installed run with a recorded first-run choice
/// (<see cref="FirstRunMarkerFileName"/> under
/// <see cref="IInstalledAppLocator.InstalledDataDirectory"/>): that
/// recorded path;</description></item>
/// <item><description>an installed run with nothing recorded yet: no
/// override yet — <see cref="PersistenceRootResolution.ShowFirstRunDialog"/>
/// is <see langword="true"/>, and the caller is expected to show the dialog,
/// then call <see cref="RecordFirstRunChoice"/> with whatever the operator
/// confirmed.</description></item>
/// </list>
/// </remarks>
public sealed class PersistenceRootResolver
{
    /// <summary>This class's own friendly command-line alias for <see cref="SqlitePersistenceStore.RootPathConfigurationKey"/>.</summary>
    public const string CliArgumentName = "--persistence-root";

    /// <summary>The file name the operator's first-run choice is recorded under, inside <see cref="IInstalledAppLocator.InstalledDataDirectory"/>.</summary>
    public const string FirstRunMarkerFileName = "first-run.json";

    /// <summary>The sub-folder name of the installed default persistence root, inside <see cref="IInstalledAppLocator.InstalledDataDirectory"/>.</summary>
    public const string InstalledPersistenceFolderName = "persistence-data";

    private readonly IInstalledAppLocator _locator;

    /// <summary>Initialises a new instance of the <see cref="PersistenceRootResolver"/> class.</summary>
    public PersistenceRootResolver(IInstalledAppLocator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        _locator = locator;
    }

    /// <summary>
    /// Resolves the persistence root for this launch.
    /// </summary>
    /// <param name="commandLineArgs">The process's own command-line arguments, unparsed.</param>
    /// <param name="generalConfiguration">
    /// The platform's own merged, non-override configuration (appsettings.json
    /// next to the executable, appsettings.json in the current directory,
    /// <c>TEMPEST_</c> environment variables, and the generic
    /// <c>--Section:Key=value</c> command-line form) — the same precedence
    /// <see cref="Tempest.Core.Configuration.MicrosoftExtensionsConfigurationSource"/>
    /// already merges for the real Host; passed in here, rather than loaded
    /// by this class, so a test can supply a fixed dictionary instead of a
    /// real environment.
    /// </param>
    public PersistenceRootResolution Resolve(
        IReadOnlyList<string> commandLineArgs,
        IReadOnlyDictionary<string, string> generalConfiguration)
    {
        ArgumentNullException.ThrowIfNull(commandLineArgs);
        ArgumentNullException.ThrowIfNull(generalConfiguration);

        if (TryReadCliArgument(commandLineArgs, out var explicitPath) && explicitPath is not null)
            return new PersistenceRootResolution(explicitPath, ShowFirstRunDialog: false, explicitPath);

        if (generalConfiguration.TryGetValue(SqlitePersistenceStore.RootPathConfigurationKey, out var configuredPath)
            && !string.IsNullOrWhiteSpace(configuredPath))
        {
            return new PersistenceRootResolution(null, ShowFirstRunDialog: false, configuredPath);
        }

        if (!_locator.IsInstalled || _locator.InstalledDataDirectory is not { } dataDirectory)
        {
            return new PersistenceRootResolution(null, ShowFirstRunDialog: false, SqlitePersistenceStore.DefaultRootPath);
        }

        var installedDefaultRoot = Path.Combine(dataDirectory, InstalledPersistenceFolderName);
        var markerPath = Path.Combine(dataDirectory, FirstRunMarkerFileName);

        if (TryReadFirstRunMarker(markerPath, out var recordedRoot) && recordedRoot is not null)
            return new PersistenceRootResolution(recordedRoot, ShowFirstRunDialog: false, recordedRoot);

        return new PersistenceRootResolution(null, ShowFirstRunDialog: true, installedDefaultRoot);
    }

    /// <summary>
    /// Records the operator's first-run choice — the default shown, or a
    /// folder chosen via "Change…" — so <see cref="Resolve"/> never shows
    /// the dialog again for this install.
    /// </summary>
    /// <exception cref="InvalidOperationException">The application is not installed (<see cref="IInstalledAppLocator.InstalledDataDirectory"/> is <see langword="null"/>).</exception>
    public void RecordFirstRunChoice(string chosenRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chosenRoot);

        if (_locator.InstalledDataDirectory is not { } dataDirectory)
        {
            throw new InvalidOperationException(
                "Cannot record a first-run persistence-root choice: this run is not an installed application.");
        }

        Directory.CreateDirectory(dataDirectory);

        var markerPath = Path.Combine(dataDirectory, FirstRunMarkerFileName);
        File.WriteAllText(markerPath, JsonSerializer.Serialize(new FirstRunMarker(chosenRoot)));
    }

    private static bool TryReadCliArgument(IReadOnlyList<string> args, out string? value)
    {
        const string equalsPrefix = CliArgumentName + "=";

        for (var i = 0; i < args.Count; i++)
        {
            if (args[i].StartsWith(equalsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                value = args[i][equalsPrefix.Length..];
                return !string.IsNullOrWhiteSpace(value);
            }

            if (string.Equals(args[i], CliArgumentName, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
            {
                value = args[i + 1];
                return !string.IsNullOrWhiteSpace(value);
            }
        }

        value = null;
        return false;
    }

    private static bool TryReadFirstRunMarker(string markerPath, out string? recordedRoot)
    {
        recordedRoot = null;

        if (!File.Exists(markerPath))
            return false;

        try
        {
            var marker = JsonSerializer.Deserialize<FirstRunMarker>(File.ReadAllText(markerPath));
            if (marker is null || string.IsNullOrWhiteSpace(marker.PersistenceRoot))
                return false;

            recordedRoot = marker.PersistenceRoot;
            return true;
        }
        catch (JsonException)
        {
            // A corrupt or foreign-shaped marker file is treated exactly
            // like no marker at all — the dialog asks again, the operator's
            // next choice overwrites it with a well-formed one.
            return false;
        }
    }

    private sealed record FirstRunMarker(string PersistenceRoot);
}
