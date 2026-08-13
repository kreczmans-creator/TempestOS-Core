using System.Reflection;
using Tempest.Core.Logging;

namespace Tempest.Core.Plugins;

/// <summary>
/// The concrete <see cref="IPluginAssemblyLoader"/> implementation, loading
/// each plugin's declared assembly via <see cref="Assembly.LoadFrom(string)"/>.
/// </summary>
/// <remarks>
/// <para>
/// Requires no cooperation from Module Discovery: once an assembly is loaded
/// here, <see cref="Modules.ReflectionFrameworkDiscoveryService"/>'s existing,
/// unchanged <see cref="AppDomain.CurrentDomain"/> default already sees it —
/// see <c>Plugin Manifest Architecture.md</c>'s Responsibilities Matrix.
/// </para>
/// <para>
/// Every plugin-scoped failure (ADR-0025, categories 5-6) is isolated: logged
/// via <see cref="PluginFailureLogging"/>, and excluded from the returned
/// list. Only a genuine defect in this loader's own orchestration — not
/// attributable to any specific plugin — propagates.
/// </para>
/// </remarks>
public sealed class PluginAssemblyLoader : IPluginAssemblyLoader
{
    private readonly ILogger? _logger;
    private readonly IPluginRegistryRecorder? _registryRecorder;

    /// <summary>
    /// Initialises a new instance of the <see cref="PluginAssemblyLoader"/> class.
    /// </summary>
    /// <param name="logger">
    /// An optional logger used to record loading progress and isolated
    /// failures. May be <see langword="null"/> if logging is not required.
    /// </param>
    /// <param name="registryRecorder">
    /// An optional Plugin Registry write side, used to record each
    /// candidate's outcome. May be <see langword="null"/> if no registry is
    /// available.
    /// </param>
    public PluginAssemblyLoader(ILogger? logger = null, IPluginRegistryRecorder? registryRecorder = null)
    {
        _logger = logger;
        _registryRecorder = registryRecorder;
    }

    /// <inheritdoc />
    public IReadOnlyList<Assembly> LoadPlugins(IReadOnlyList<PluginManifest> manifests)
    {
        ArgumentNullException.ThrowIfNull(manifests);

        _logger?.Information("Plugin loading started.");

        var loaded = new List<Assembly>();

        foreach (var manifest in manifests)
        {
            try
            {
                loaded.Add(LoadOne(manifest));

                _logger?.Information($"Plugin assembly loaded: '{manifest.Id}' from '{manifest.AssemblyPath}'.");
                _registryRecorder?.Record(new PluginRegistryEntry(manifest.Id, manifest.Name, manifest.Version, PluginRegistryState.Loaded, null));
            }
            catch (PluginException ex)
            {
                PluginFailureLogging.LogIsolatedFailure(_logger, ex, manifest.Id);
                PluginFailureLogging.RecordIsolatedFailure(_registryRecorder, ex, manifest.Id);
            }
        }

        _logger?.Information($"Plugin loading completed. {loaded.Count} plugin assembly(ies) loaded.");

        return loaded;
    }

    private static Assembly LoadOne(PluginManifest manifest)
    {
        if (!File.Exists(manifest.AssemblyPath))
            throw new PluginAssemblyNotFoundException(manifest.Id, manifest.AssemblyPath);

        try
        {
            return Assembly.LoadFrom(manifest.AssemblyPath);
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or IOException)
        {
            throw new PluginAssemblyLoadException(manifest.Id, manifest.AssemblyPath, ex);
        }
    }
}
