using System.Text.Json;
using Tempest.Core.Logging;
using Tempest.Core.Versioning;

namespace Tempest.Core.Plugins;

/// <summary>
/// Discovers <see cref="PluginManifest"/>s by scanning a plugins directory for
/// per-plugin manifest files.
/// </summary>
/// <remarks>
/// <para>
/// Host-owned, Phase 3.1 (ADR-0026). Each immediate subdirectory of the
/// plugins root is one candidate; a candidate is expected to contain a
/// <c>plugin.manifest.json</c> file. Candidates are processed in
/// deterministic, ordinal order by folder name — closing the precision gap
/// ADR-0026 identified in ADR-0025's own "first manifest encountered wins"
/// language, since raw filesystem enumeration order is not guaranteed stable
/// across operating systems or file systems.
/// </para>
/// <para>
/// Every plugin-scoped failure (ADR-0025, categories 1-5) is isolated: logged
/// at that category's assigned severity via <see cref="PluginFailureLogging"/>,
/// and excluded from the returned list. <see cref="DiscoverManifests()"/>
/// throws only for a genuine defect in this service's own orchestration
/// (ADR-0025, category 11) — enumerating the plugins root itself, not
/// processing any one candidate.
/// </para>
/// </remarks>
public sealed class PluginManifestDiscoveryService : IPluginManifestDiscoveryService
{
    /// <summary>
    /// The manifest file name expected inside each plugin candidate folder.
    /// </summary>
    internal const string ManifestFileName = "plugin.manifest.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _pluginsRootPath;
    private readonly IPlatformVersionProvider _platformVersionProvider;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="PluginManifestDiscoveryService"/>
    /// class that scans the conventional plugins directory
    /// (<c>Plugins</c>, relative to the application's base directory).
    /// </summary>
    /// <param name="platformVersionProvider">
    /// The running platform's own version, used to evaluate each manifest's
    /// declared <c>MinimumPlatformVersion</c> (ADR-0025, category 4).
    /// </param>
    /// <param name="logger">
    /// An optional logger used to record discovery progress and isolated
    /// failures. May be <see langword="null"/> if logging is not required.
    /// </param>
    public PluginManifestDiscoveryService(IPlatformVersionProvider platformVersionProvider, ILogger? logger = null)
        : this(Path.Combine(AppContext.BaseDirectory, "Plugins"), platformVersionProvider, logger)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="PluginManifestDiscoveryService"/>
    /// class that scans a specific plugins root directory.
    /// </summary>
    /// <param name="pluginsRootPath">The directory containing plugin candidate folders.</param>
    /// <param name="platformVersionProvider">
    /// The running platform's own version, used to evaluate each manifest's
    /// declared <c>MinimumPlatformVersion</c> (ADR-0025, category 4).
    /// </param>
    /// <param name="logger">
    /// An optional logger used to record discovery progress and isolated
    /// failures. May be <see langword="null"/> if logging is not required.
    /// </param>
    /// <remarks>
    /// Internal test seam — mirrors
    /// <see cref="Modules.ReflectionFrameworkDiscoveryService"/>'s own
    /// internal, assembly-set-accepting constructor, so discovery can be
    /// exercised deterministically against a controlled temporary directory
    /// in tests.
    /// </remarks>
    internal PluginManifestDiscoveryService(string pluginsRootPath, IPlatformVersionProvider platformVersionProvider, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginsRootPath);
        ArgumentNullException.ThrowIfNull(platformVersionProvider);

        _pluginsRootPath = pluginsRootPath;
        _platformVersionProvider = platformVersionProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<PluginManifest> DiscoverManifests()
    {
        _logger?.Information("Plugin discovery started.");

        if (!Directory.Exists(_pluginsRootPath))
        {
            _logger?.Information(
                $"No plugins directory found at '{_pluginsRootPath}'. Plugin discovery found 0 plugin(s).");

            return [];
        }

        var candidateFolders = Directory.GetDirectories(_pluginsRootPath)
            .OrderBy(folder => Path.GetFileName(folder), StringComparer.Ordinal)
            .ToList();

        if (candidateFolders.Count == 0)
        {
            _logger?.Information(
                $"Plugins directory '{_pluginsRootPath}' contains no candidate folders. " +
                "Plugin discovery found 0 plugin(s).");

            return [];
        }

        return DiscoverManifests(candidateFolders);
    }

    /// <summary>
    /// Discovers manifests from an explicit, pre-enumerated list of candidate folders.
    /// </summary>
    /// <param name="candidateFolders">
    /// The candidate folders to evaluate, in the order they should be processed.
    /// </param>
    /// <returns>The eligible plugin manifests, in the given order.</returns>
    /// <remarks>
    /// This overload is <see langword="internal"/>. It isolates the core
    /// discovery algorithm — manifest parsing, validation, version
    /// compatibility, and duplicate detection — from plugins-root enumeration,
    /// mirroring <see cref="Modules.ReflectionFrameworkDiscoveryService"/>'s own
    /// <c>DiscoverModules(IEnumerable&lt;Type&gt;)</c> seam, so it can be
    /// exercised against candidates that cannot be constructed as real
    /// directories on disk (for example, proving that an exception outside
    /// ADR-0025's classification is not swallowed here).
    /// </remarks>
    internal IReadOnlyList<PluginManifest> DiscoverManifests(IEnumerable<string> candidateFolders)
    {
        var acceptedById = new Dictionary<string, PluginManifest>(StringComparer.Ordinal);
        var ordered = new List<PluginManifest>();

        foreach (var folder in candidateFolders)
            ProcessCandidate(folder, acceptedById, ordered);

        _logger?.Information($"Plugin discovery completed. {ordered.Count} plugin(s) eligible.");

        return ordered;
    }

    private void ProcessCandidate(string folder, Dictionary<string, PluginManifest> acceptedById, List<PluginManifest> ordered)
    {
        var manifestPath = Path.Combine(folder, ManifestFileName);

        if (!File.Exists(manifestPath))
        {
            _logger?.Warning(
                $"Plugin candidate folder '{folder}' does not contain a manifest file " +
                $"('{ManifestFileName}'); skipping.");

            return;
        }

        try
        {
            var manifest = ParseAndValidate(folder, manifestPath);

            if (acceptedById.ContainsKey(manifest.Id))
                throw new DuplicatePluginIdException(manifest.Id);

            acceptedById.Add(manifest.Id, manifest);
            ordered.Add(manifest);

            _logger?.Information(
                $"Plugin manifest accepted: '{manifest.Id}' ({manifest.Name} v{manifest.Version}) from '{folder}'.");
        }
        catch (PluginException ex)
        {
            PluginFailureLogging.LogIsolatedFailure(_logger, ex, folder);
        }
    }

    private PluginManifest ParseAndValidate(string folder, string manifestPath)
    {
        string json;

        try
        {
            json = File.ReadAllText(manifestPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidPluginManifestException($"Manifest file '{manifestPath}' could not be read.", ex);
        }

        PluginManifestDto? dto;

        try
        {
            dto = JsonSerializer.Deserialize<PluginManifestDto>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidPluginManifestException($"Manifest file '{manifestPath}' is not valid JSON.", ex);
        }

        if (dto is null)
            throw new InvalidPluginManifestException($"Manifest file '{manifestPath}' deserialised to nothing.");

        RequireField(dto.Id, nameof(dto.Id), manifestPath);
        RequireField(dto.Name, nameof(dto.Name), manifestPath);
        RequireField(dto.Version, nameof(dto.Version), manifestPath);
        RequireField(dto.MinimumPlatformVersion, nameof(dto.MinimumPlatformVersion), manifestPath);
        RequireField(dto.AssemblyFileName, nameof(dto.AssemblyFileName), manifestPath);

        if (!Version.TryParse(dto.MinimumPlatformVersion, out var minimumPlatformVersion))
        {
            throw new InvalidPluginManifestException(
                $"Manifest file '{manifestPath}' has an unparseable MinimumPlatformVersion value: " +
                $"'{dto.MinimumPlatformVersion}'.");
        }

        var runningPlatformVersion = _platformVersionProvider.Version.AssemblyVersion;

        if (minimumPlatformVersion > runningPlatformVersion)
            throw new IncompatiblePluginVersionException(dto.Id!, minimumPlatformVersion, runningPlatformVersion);

        string assemblyPath;

        try
        {
            assemblyPath = Path.GetFullPath(Path.Combine(folder, dto.AssemblyFileName!));
        }
        catch (ArgumentException ex)
        {
            throw new InvalidPluginManifestException(
                $"Manifest file '{manifestPath}' has an invalid AssemblyFileName value: " +
                $"'{dto.AssemblyFileName}'.", ex);
        }

        return new PluginManifest(dto.Id!, dto.Name!, dto.Version!, minimumPlatformVersion, dto.AssemblyFileName!, assemblyPath);
    }

    private static void RequireField(string? value, string fieldName, string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidPluginManifestException(
                $"Manifest file '{manifestPath}' has a null, empty, or whitespace '{fieldName}' field.");
        }
    }
}
