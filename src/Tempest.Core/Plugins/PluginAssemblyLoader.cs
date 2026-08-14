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
/// <para>
/// <b>Trust enforcement (ADR-0110/ADR-0111, WP 13.2A).</b> After a plugin's
/// assembly successfully loads, and still within this same try/catch phase
/// boundary — no new phase — two static checks run before the plugin is ever
/// recorded <see cref="PluginRegistryState.Loaded"/>: every
/// <see cref="PluginManifest.RequestedCapabilities"/> entry is checked
/// against <see cref="PluginManifest.TrustTier"/>'s own ceiling, and every
/// discovered <see cref="Modules.IModule"/> implementer's public constructors
/// are reflected over for conformance against the fixed always-allowed
/// baseline plus the plugin's own eligible <c>plugin.services.resolve:*</c>
/// grants. Either failure isolates the whole plugin
/// (<see cref="PluginTrustDeniedException"/>, category 17) — the assembly
/// remains loaded in the process (ADR-0015: that step cannot be undone), but
/// the plugin is recorded <see cref="PluginRegistryState.TrustDenied"/>, not
/// <see cref="PluginRegistryState.Loaded"/>, and is excluded from this
/// method's own returned list. A plugin that passes both checks has its
/// component principal (<see cref="Identity.PlatformPrincipal"/>, reusing
/// <see cref="Identity.IPrincipal"/>/<see cref="Identity.IIdentity"/>
/// directly — no new principal type, per ADR-0111) constructed once here and
/// recorded, per discovered module <see cref="Type"/>, into the optional
/// <see cref="IPluginComponentPrincipalRecorder"/> — the mechanism
/// <c>TempestHost</c>'s own <see cref="Modules.ModuleLifecycleManager"/>
/// wiring later uses to push the right ambient component principal around
/// each of that module's own lifecycle calls, without Module Discovery,
/// Registration, or Lifecycle ever needing to know a plugin exists.
/// </para>
/// </remarks>
public sealed class PluginAssemblyLoader : IPluginAssemblyLoader
{
    /// <summary>
    /// The fixed, always-allowed constructor-parameter baseline every module
    /// (first-party or plugin) has always been able to depend on, exactly
    /// per ADR-0111 — never gated by any <c>plugin.services.resolve:*</c>
    /// declaration.
    /// </summary>
    private static readonly HashSet<Type> AlwaysAllowedConstructorBaseline =
    [
        typeof(Logging.ILogger),
        typeof(Configuration.IConfigurationProvider),
        typeof(Diagnostics.IDiagnosticsProvider),
    ];

    /// <summary>
    /// The fixed, two-key capability ceiling <see cref="PluginTrustTier.UnsignedLocal"/>
    /// is clamped to, regardless of what its manifest requests (ADR-0111).
    /// </summary>
    private static readonly HashSet<string> UnsignedLocalCapabilityCeiling = new(StringComparer.Ordinal)
    {
        PluginCapability.Navigation,
        PluginCapability.Commands,
    };

    /// <summary>
    /// Concrete, Host-authority collaborator types no <c>plugin.services.resolve:*</c>
    /// grant may ever name, for any trust tier including <see cref="PluginTrustTier.FirstParty"/>
    /// (WP 13.2B security review finding). Both hold a write surface —
    /// <see cref="Identity.CurrentComponentAccessor.BeginScope"/> and
    /// <see cref="Identity.CurrentPrincipalAccessor.SetCurrent"/> — that a plugin
    /// resolving the exact same Host-owned singleton instance the container hands
    /// every other collaborator could use to forge an arbitrary component principal
    /// (including a self-granted <see cref="PluginTrustPermission.FirstParty"/> marker)
    /// or hijack the ambient, process-wide user principal outright. Neither type's
    /// own capability-gated surface (<see cref="Identity.ICurrentComponentAccessor"/>,
    /// the read-only interface) is affected by this denylist — only the concrete,
    /// mutable types are ever named here, and only via constructor injection or an
    /// explicit <c>plugin.services.resolve:*</c> grant; this is not a general
    /// capability restriction. See <c>PluginAssemblyLoaderEnforceTrustTests.cs</c>'s
    /// own coverage of this exact escalation path.
    /// </summary>
    private static readonly HashSet<Type> NeverEligibleServiceResolveTypes =
    [
        typeof(Identity.CurrentComponentAccessor),
        typeof(Identity.CurrentPrincipalAccessor),
    ];

    private const string EventPublishPrefix = "plugin.events.publish:";
    private const string ServiceResolvePrefix = "plugin.services.resolve:";

    private readonly ILogger? _logger;
    private readonly IPluginRegistryRecorder? _registryRecorder;
    private readonly IPluginComponentPrincipalRecorder? _componentPrincipalRecorder;

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
    /// <param name="componentPrincipalRecorder">
    /// An optional recorder a trust-checked plugin's own component principal
    /// is written into, keyed by each of its discovered
    /// <see cref="Modules.IModule"/> types (ADR-0111). May be
    /// <see langword="null"/> if no such recorder is available — in which
    /// case trust checks still run and can still deny a plugin, but no
    /// principal is ever recorded for one that passes.
    /// </param>
    public PluginAssemblyLoader(
        ILogger? logger = null,
        IPluginRegistryRecorder? registryRecorder = null,
        IPluginComponentPrincipalRecorder? componentPrincipalRecorder = null)
    {
        _logger = logger;
        _registryRecorder = registryRecorder;
        _componentPrincipalRecorder = componentPrincipalRecorder;
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
                var assembly = LoadOne(manifest);

                EnforceTrust(manifest, assembly);

                loaded.Add(assembly);

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

    /// <summary>
    /// Performs the two static trust checks (ADR-0111) against
    /// <paramref name="manifest"/>'s own already-loaded <paramref name="assembly"/>,
    /// and, if both pass, constructs and records this plugin's own component
    /// principal for each of its discovered <see cref="Modules.IModule"/> types.
    /// </summary>
    /// <exception cref="PluginTrustDeniedException">
    /// <paramref name="manifest"/> requests a capability outside its own
    /// <see cref="PluginManifest.TrustTier"/>'s ceiling, or names a key that
    /// is not a recognised capability shape at all, or a discovered
    /// <see cref="Modules.IModule"/> type has no compliant public
    /// constructor (category 17).
    /// </exception>
    private void EnforceTrust(PluginManifest manifest, Assembly assembly)
    {
        var ineligibleCapability = FindIneligibleCapability(manifest);

        if (ineligibleCapability is not null)
        {
            throw new PluginTrustDeniedException(
                manifest.Id,
                $"Requested capability '{ineligibleCapability}' is not eligible for trust tier " +
                $"'{manifest.TrustTier}'.");
        }

        var moduleTypes = GetLoadableTypes(assembly).Where(IsModuleType).ToList();

        var nonCompliantType = moduleTypes.FirstOrDefault(type => !HasCompliantConstructor(type, manifest));

        if (nonCompliantType is not null)
        {
            throw new PluginTrustDeniedException(
                manifest.Id,
                $"Module type '{nonCompliantType.FullName}' has no public constructor whose parameters are " +
                "all within the fixed always-allowed baseline or an eligible, granted " +
                "'plugin.services.resolve:*' capability.");
        }

        if (moduleTypes.Count == 0)
            return;

        var grantedPermissions = manifest.RequestedCapabilities
            .Select(key => new Identity.Permission(key))
            .Append(new Identity.Permission(PluginTrustPermission.ForTier(manifest.TrustTier)))
            .ToList();

        Identity.IPrincipal principal = new Identity.PlatformPrincipal(
            new Identity.PlatformIdentity(manifest.Id, manifest.Name),
            grantedPermissions);

        foreach (var moduleType in moduleTypes)
            _componentPrincipalRecorder?.Record(moduleType, principal);
    }

    /// <summary>
    /// Finds the first requested capability key that is either not a
    /// recognised capability shape at all, or is outside
    /// <see cref="PluginManifest.TrustTier"/>'s own ceiling — <c>null</c> if
    /// every requested key is eligible.
    /// </summary>
    private static string? FindIneligibleCapability(PluginManifest manifest)
    {
        foreach (var key in manifest.RequestedCapabilities)
        {
            if (!IsRecognisedCapabilityShape(key))
                return key;

            if (manifest.TrustTier == PluginTrustTier.UnsignedLocal && !UnsignedLocalCapabilityCeiling.Contains(key))
                return key;

            if (key.StartsWith(ServiceResolvePrefix, StringComparison.Ordinal)
                && IsNeverEligibleServiceResolveTarget(key[ServiceResolvePrefix.Length..]))
                return key;
        }

        return null;
    }

    /// <summary>
    /// Reports whether <paramref name="fullServiceTypeName"/> names a type in
    /// <see cref="NeverEligibleServiceResolveTypes"/> — ineligible for every
    /// trust tier, with no exception for <see cref="PluginTrustTier.FirstParty"/>.
    /// </summary>
    private static bool IsNeverEligibleServiceResolveTarget(string fullServiceTypeName) =>
        NeverEligibleServiceResolveTypes.Any(type =>
            string.Equals(type.FullName, fullServiceTypeName, StringComparison.Ordinal));

    /// <summary>
    /// Reports whether <paramref name="key"/> matches one of the five
    /// well-known capability key shapes (ADR-0111) — a key that matches
    /// none of them is not a genuine grant of anything, and is treated as
    /// ineligible for every trust tier, not only <see cref="PluginTrustTier.UnsignedLocal"/>.
    /// </summary>
    private static bool IsRecognisedCapabilityShape(string key) =>
        key == PluginCapability.Navigation
        || key == PluginCapability.Commands
        || key == PluginCapability.DiRegister
        || key.StartsWith(EventPublishPrefix, StringComparison.Ordinal)
        || key.StartsWith(ServiceResolvePrefix, StringComparison.Ordinal);

    /// <summary>
    /// Mirrors <see cref="Modules.ReflectionFrameworkDiscoveryService"/>'s
    /// own <c>IsValidModuleType</c> filter exactly, run here independently
    /// and before handoff to Module Discovery — see ADR-0111's own disclosed
    /// duplication risk.
    /// </summary>
    private static bool IsModuleType(Type type) =>
        typeof(Modules.IModule).IsAssignableFrom(type)
        && !type.IsInterface
        && !type.IsAbstract
        && !type.IsGenericTypeDefinition;

    /// <summary>
    /// Reports whether at least one of <paramref name="moduleType"/>'s own
    /// public instance constructors is compliant — every parameter type is
    /// either in the fixed always-allowed baseline, or is named by an
    /// eligible, already-granted <c>plugin.services.resolve:*</c> entry in
    /// <paramref name="manifest"/>'s own <see cref="PluginManifest.RequestedCapabilities"/>.
    /// </summary>
    private static bool HasCompliantConstructor(Type moduleType, PluginManifest manifest)
    {
        var grantedServiceTypeNames = new HashSet<string>(
            manifest.RequestedCapabilities
                .Where(key => key.StartsWith(ServiceResolvePrefix, StringComparison.Ordinal))
                .Select(key => key[ServiceResolvePrefix.Length..]),
            StringComparer.Ordinal);

        foreach (var constructor in moduleType.GetConstructors())
        {
            var isCompliant = constructor.GetParameters().All(parameter =>
                !NeverEligibleServiceResolveTypes.Contains(parameter.ParameterType)
                && (AlwaysAllowedConstructorBaseline.Contains(parameter.ParameterType)
                    || (parameter.ParameterType.FullName is not null
                        && grantedServiceTypeNames.Contains(parameter.ParameterType.FullName))));

            if (isCompliant)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Mirrors <see cref="Modules.ReflectionFrameworkDiscoveryService"/>'s
    /// own <see cref="ReflectionTypeLoadException"/>-tolerant type-loading
    /// pattern exactly — <see cref="Assembly.GetTypes"/> is not assumed to
    /// never throw.
    /// </summary>
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null).Select(type => type!);
        }
    }
}
