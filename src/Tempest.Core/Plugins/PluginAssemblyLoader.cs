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
    private readonly IPluginDeniedTypeRecorder? _deniedTypeRecorder;

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
    /// <param name="deniedTypeRecorder">
    /// An optional recorder every discovered <see cref="Modules.IModule"/>
    /// or <see cref="BackgroundServices.IHostedService"/> type belonging to
    /// a trust-denied plugin is written into (WP 13.9.4) — the execution
    /// boundary <c>TempestHost</c> filters Module Registration and Hosted
    /// Service Registration against, so a denied plugin's own module or
    /// hosted service can never reach
    /// <c>InitialiseAsync</c>/<c>StartAsync</c>. May be <see langword="null"/>
    /// if no such recorder is available — in which case trust checks still
    /// run and can still deny a plugin, but nothing downstream is filtered.
    /// </param>
    public PluginAssemblyLoader(
        ILogger? logger = null,
        IPluginRegistryRecorder? registryRecorder = null,
        IPluginComponentPrincipalRecorder? componentPrincipalRecorder = null,
        IPluginDeniedTypeRecorder? deniedTypeRecorder = null)
    {
        _logger = logger;
        _registryRecorder = registryRecorder;
        _componentPrincipalRecorder = componentPrincipalRecorder;
        _deniedTypeRecorder = deniedTypeRecorder;
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
    /// constructor (category 17). <c>WP 13.11B</c>: also thrown, from here,
    /// when a discovered <see cref="Modules.IModule"/> or
    /// <see cref="BackgroundServices.IHostedService"/> type declares a
    /// constructor parameter whose own type cannot be resolved —
    /// <see cref="DiscoverModuleTypes"/> detects that during its own scan
    /// but no longer throws it there, returning it through its
    /// <c>unresolvableConstructorDenial</c> out parameter so this method can
    /// record the denial before raising it, exactly as it does for the other
    /// two reasons.
    /// </exception>
    /// <remarks>
    /// <b>Corrected, <c>WP 13.9.4</c> trust-denial execution boundary
    /// remediation.</b> <see cref="DiscoverModuleTypes"/>'s own fixed-point
    /// transitive scan now always runs first, unconditionally, before either
    /// static check — not only ahead of the constructor-conformance check as
    /// before. This is required, not cosmetic: a plugin denied purely for an
    /// ineligible requested capability used to throw before
    /// <see cref="DiscoverModuleTypes"/> ever ran, so nothing anywhere ever
    /// learned which <see cref="Modules.IModule"/> types (or which
    /// transitively-loaded assemblies) belonged to it — leaving no data a
    /// downstream execution-boundary filter could key off for that denial
    /// reason. Every discovered <see cref="Modules.IModule"/> AND
    /// <see cref="BackgroundServices.IHostedService"/> type is now recorded
    /// via <see cref="IPluginDeniedTypeRecorder"/> on <i>either</i> denial
    /// path, before the corresponding exception is thrown — not only the one
    /// type that happened to trigger it, since the whole plugin is isolated
    /// and every type reachable from its own scan must never reach Module
    /// Registration or Hosted Service Registration (<c>TempestHost</c>'s own
    /// filters, keyed against this recorder's <see cref="IPluginDeniedTypeRegistry"/>
    /// read side). Hosted service types are recorded only on denial — unlike
    /// module types, they are never constructor-checked and never receive a
    /// component principal for a passing plugin, matching
    /// <see cref="BackgroundServices.IHostedServiceManager"/>'s own existing,
    /// unrelated lack of a component-scope hook (a separate, pre-existing
    /// gap, not introduced or widened here).
    /// </remarks>
    private void EnforceTrust(PluginManifest manifest, Assembly assembly)
    {
        var moduleTypes = DiscoverModuleTypes(
            manifest.Id, assembly, out var hostedServiceTypes, out var unresolvableConstructorDenial);

        // Corrected, WP 13.11B (TD-51, reopened by WP 13.11A). This denial is
        // detected inside DiscoverModuleTypes' own scan, not here, so before
        // this block existed it was thrown from there directly - strictly
        // before either RecordDenied call site below could ever run, leaving
        // deniedTypeRegistry empty for this one denial reason alone. Raised
        // to a first-class denial path here, at the same seam as the other
        // two, so it records exactly what they record before throwing
        // exactly as they throw. Placed FIRST, ahead of the capability
        // check, deliberately: it preserves the message precedence the
        // in-scan throw already had (this denial reason has always won when
        // a plugin triggers more than one), and it keeps the offending type
        // away from HasCompliantConstructor below - whose own
        // GetConstructors()/GetParameters() calls are unguarded and would
        // rethrow the identical, uncaught CLR type-load failure.
        if (unresolvableConstructorDenial is not null)
        {
            RecordDenied(moduleTypes, hostedServiceTypes);

            throw unresolvableConstructorDenial;
        }

        var ineligibleCapability = FindIneligibleCapability(manifest);

        if (ineligibleCapability is not null)
        {
            RecordDenied(moduleTypes, hostedServiceTypes);

            throw new PluginTrustDeniedException(
                manifest.Id,
                $"Requested capability '{ineligibleCapability}' is not eligible for trust tier " +
                $"'{manifest.TrustTier}'.");
        }

        var nonCompliantType = moduleTypes.Concat(hostedServiceTypes).FirstOrDefault(type => !HasCompliantConstructor(type, manifest));

        if (nonCompliantType is not null)
        {
            RecordDenied(moduleTypes, hostedServiceTypes);

            throw new PluginTrustDeniedException(
                manifest.Id,
                $"Module or hosted service type '{nonCompliantType.FullName}' has no public constructor whose parameters are " +
                "all within the fixed always-allowed baseline or an eligible, granted " +
                "'plugin.services.resolve:*' capability.");
        }

        if (moduleTypes.Count == 0 && hostedServiceTypes.Count == 0)
            return;

        var grantedPermissions = manifest.RequestedCapabilities
            .Select(key => new Identity.Permission(key))
            .Append(new Identity.Permission(PluginTrustPermission.ForTier(manifest.TrustTier)))
            .ToList();

        Identity.IPrincipal principal = new Identity.PlatformPrincipal(
            new Identity.PlatformIdentity(manifest.Id, manifest.Name),
            grantedPermissions);

        foreach (var type in moduleTypes.Concat(hostedServiceTypes))
            _componentPrincipalRecorder?.Record(type, principal);
    }

    /// <summary>
    /// Records every one of <paramref name="moduleTypes"/> and
    /// <paramref name="hostedServiceTypes"/> as belonging to a plugin just
    /// denied trust (WP 13.9.4) — the whole plugin is isolated, so every
    /// type its own transitive scan found must never reach Module
    /// Registration or Hosted Service Registration, not only the specific
    /// type that triggered the denial.
    /// </summary>
    private void RecordDenied(IReadOnlyList<Type> moduleTypes, IReadOnlyList<Type> hostedServiceTypes)
    {
        foreach (var moduleType in moduleTypes)
            _deniedTypeRecorder?.Record(moduleType);

        foreach (var hostedServiceType in hostedServiceTypes)
            _deniedTypeRecorder?.Record(hostedServiceType);
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
        || key == PluginCapability.IdentityEstablish
        || key.StartsWith(EventPublishPrefix, StringComparison.Ordinal)
        || key.StartsWith(ServiceResolvePrefix, StringComparison.Ordinal);

    /// <summary>
    /// Discovers every <see cref="Modules.IModule"/> implementer reachable
    /// from <paramref name="assembly"/>'s own type scan, following every
    /// assembly that enters the <see cref="AppDomain"/> as a direct or
    /// transitive side effect of that scan — <c>WP 13.9.1</c> security
    /// remediation, closing the gap <c>WP 13.9.0</c>'s Security/Trust review
    /// found and ADR-0111's own corrected scope now states; <c>WP 13.9.3</c>
    /// closes a second, narrower gap in that same remediation, below.
    /// </summary>
    /// <remarks>
    /// <para>
    /// .NET only loads a referenced assembly lazily, the moment one of its
    /// types is actually resolved — <see cref="Assembly.GetTypes"/> triggers
    /// exactly this via base-type-chain resolution. Before <c>WP 13.9.1</c>'s
    /// fix, a plugin could package a second, wholly undeclared assembly in
    /// its own candidate folder, have its primary assembly's own type
    /// inherit from a type in that second assembly, and the second
    /// assembly's own <see cref="Modules.IModule"/> implementers would still
    /// reach Module Discovery (deliberately plugin-unaware of trust, per
    /// ADR-0110) — with zero trust checking of any kind, indistinguishable
    /// from first-party code.
    /// </para>
    /// <para>
    /// This performs the identical fixed-point, breadth-first traversal
    /// idiom <see cref="PluginManifestDiscoveryService"/>'s own
    /// dependency-graph resolution already uses: diff
    /// <see cref="AppDomain.CurrentDomain"/>'s own assembly set immediately
    /// before and after each scan step, enqueueing only assemblies newly
    /// present as a direct consequence of that specific step, until nothing
    /// new appears. An assembly already resident in the
    /// <see cref="AppDomain"/> for an unrelated reason before this plugin's
    /// own scan began is correctly out of scope — the before/after diff
    /// excludes it by construction. This deliberately widens only the
    /// existing capability-scoped enforcement mechanism's own coverage; it
    /// introduces no new isolation mechanism — no alternate assembly-loading
    /// context, no process separation — per ADR-0110's own unchanged
    /// boundary.
    /// </para>
    /// <para>
    /// <b>Corrected, <c>WP 13.9.3</c> security remediation.</b> <c>Assembly.GetTypes()</c>
    /// is not the only reflection call in this plugin's own trust evaluation
    /// capable of lazily loading a referenced assembly — resolving a
    /// constructor parameter's own <see cref="System.Reflection.ParameterInfo.ParameterType"/>,
    /// which <see cref="HasCompliantConstructor"/> does for every discovered
    /// module type in a later, separate pass, is an equally unavoidable CLR
    /// load trigger, and it happens strictly after this method has already
    /// returned — invisible to the diff above. A plugin module could smuggle
    /// a second, undeclared assembly by referencing one of its types only
    /// from a constructor parameter (not a base type): a non-compliant
    /// constructor still force-loads the referenced assembly while the
    /// module itself is denied (leaving that assembly's own, separately
    /// discoverable module types un-vetted); worse, if the same module also
    /// declared an alternate, compliant constructor, the module was accepted
    /// outright — <see cref="System.Reflection.Type.GetConstructors()"/>
    /// resolves every returned constructor's own full parameter signature
    /// regardless of declaration order or which one ultimately proves
    /// compliant, so this was never merely a lazy, incidentally-avoidable
    /// effect. Closed by forcing every discovered module type's every public
    /// constructor's every parameter's <c>ParameterType</c> to resolve here,
    /// unconditionally, inside this same per-step diff window — so any
    /// assembly this pulls in is captured exactly like any other
    /// transitively-loaded one, and a later loop iteration discovers and
    /// trust-checks its own module types too. <see cref="HasCompliantConstructor"/>
    /// itself is unchanged; only the point at which its own unavoidable
    /// reflection side effects are allowed to fire moved earlier, into this
    /// method's own already-diffed scan window. No new isolation mechanism —
    /// still the same fixed-point, capability-scoped enforcement this type
    /// has always performed, now correctly capturing both of its own two
    /// reflection touchpoints instead of one.
    /// </para>
    /// <para>
    /// <b>Corrected, <c>WP 13.9.4</c> trust-denial execution boundary
    /// remediation.</b> This scan also now collects every discovered
    /// <see cref="BackgroundServices.IHostedService"/> implementer, alongside
    /// <see cref="Modules.IModule"/> ones, in the same single pass — closing
    /// a second, sibling defect <c>WP 13.9.4</c>'s own Adversarial Review
    /// found: Module Registration and Hosted Service Registration are two
    /// wholly independent discovery pipelines (<c>ReflectionFrameworkDiscoveryService</c>
    /// / <c>HostedServiceDiscoveryService</c>), and a denied plugin's
    /// already-loaded assembly could still contribute an
    /// <see cref="BackgroundServices.IHostedService"/> implementer that
    /// reached <c>StartAsync</c> with zero trust checking, even one sharing
    /// the identical <see cref="Type"/> a denied <see cref="Modules.IModule"/>
    /// implementer was already correctly excluded through the other
    /// pipeline. Hosted service types are collected here purely for
    /// denial-recording purposes (<see cref="EnforceTrust"/>'s own
    /// <c>RecordDenied</c> call) — they are never constructor-checked and
    /// never granted a component principal for a passing plugin, exactly
    /// matching this type's own existing, unchanged behaviour for hosted
    /// services today.
    /// </para>
    /// <para>
    /// <b>Corrected, <c>WP 13.11B</c> trust-denial recording remediation
    /// (<c>TD-51</c>, reopened by <c>WP 13.11A</c>).</b> The
    /// unresolvable-constructor-parameter-type failure below no longer
    /// throws from inside this scan. It is reported to
    /// <see cref="EnforceTrust"/> through
    /// <paramref name="unresolvableConstructorDenial"/> instead, so that
    /// denial reason records exactly what the other two record
    /// (<see cref="RecordDenied"/>) before throwing, and so this scan still
    /// runs to its own fixed point rather than abandoning assemblies it has
    /// already pulled into the <see cref="AppDomain"/>. Throwing from here
    /// bypassed both: nothing was ever recorded for this denial reason, so
    /// <c>TempestHost</c>'s own <c>WP 13.9.6</c> Module Discovery filter had
    /// nothing to exclude and
    /// <see cref="Modules.ReflectionFrameworkDiscoveryService"/>'s own
    /// <c>CreateDescriptor</c> then rethrew the identical CLR type-load
    /// failure uncaught, faulting the whole Host — a platform-wide outage
    /// from a plugin-scoped failure, which ADR-0025 exists to forbid. See
    /// the <c>catch</c> block's own comment for the complete account,
    /// including why recording alone, without completing the scan, would
    /// have traded that crash for a silent trust bypass.
    /// </para>
    /// </remarks>
    private static List<Type> DiscoverModuleTypes(
        string pluginId,
        Assembly assembly,
        out List<Type> hostedServiceTypes,
        out PluginTrustDeniedException? unresolvableConstructorDenial)
    {
        unresolvableConstructorDenial = null;

        var scannedAssemblies = new HashSet<Assembly> { assembly };
        var toScan = new Queue<Assembly>();
        toScan.Enqueue(assembly);
        var allModuleTypes = new List<Type>();
        var allHostedServiceTypes = new List<Type>();

        while (toScan.Count > 0)
        {
            var current = toScan.Dequeue();
            var before = new HashSet<Assembly>(AppDomain.CurrentDomain.GetAssemblies());

            var currentLoadableTypes = GetLoadableTypes(current).ToList();

            var currentModuleTypes = currentLoadableTypes.Where(IsModuleType).ToList();
            allModuleTypes.AddRange(currentModuleTypes);

            var currentHostedServiceTypes = currentLoadableTypes.Where(IsHostedServiceType).ToList();
            allHostedServiceTypes.AddRange(currentHostedServiceTypes);

            // WP 13.9.3: force every discovered type's every public
            // constructor's every parameter's ParameterType to resolve now,
            // inside this scan step - unconditionally, not only the ones
            // HasCompliantConstructor's own short-circuiting .All()/return
            // would happen to touch - so any assembly this pulls in is
            // captured by this step's own before/after diff, not invisible
            // to it. HasCompliantConstructor itself remains unchanged; its
            // own reflection side effects simply have nowhere new left to
            // hide by the time it runs.
            //
            // Corrected, WP 13.10B: this loop originally iterated
            // currentModuleTypes only. Once EnforceTrust's own constructor-
            // conformance check was extended to also cover hostedServiceTypes
            // (TD-51), that left an IHostedService-only type's constructor
            // parameters never pre-resolved here - HasCompliantConstructor's
            // own later call to GetParameters() became the FIRST resolution
            // attempt for such a type, with no exception handling of any
            // kind, and no try/catch anywhere else in this method's own
            // reach protects it either. An unresolvable parameter type on a
            // hosted-service-only plugin's constructor then threw an
            // uncaught exception all the way out of LoadPlugins and
            // TempestHost.RunAsync itself - a genuine Host-wide crash, not
            // merely a denied plugin, and a strictly worse regression than
            // the gap TD-51 itself closed. Found live, by WP 13.10B's own
            // independent Adversarial Security review, immediately after
            // this same method's IModule-only version of this exact fix
            // landed. Iterating both currentModuleTypes and
            // currentHostedServiceTypes here closes it for both discovery
            // axes uniformly, and also completes WP 13.9.3's own original
            // transitive-assembly-discovery intent for hosted services -
            // forcing a hosted service's own constructor-parameter types to
            // resolve here is exactly what lets this step's own before/after
            // diff, below, discover a secondary assembly reachable only via
            // a hosted-service's own constructor parameter, not only a
            // module's.
            foreach (var moduleType in currentModuleTypes.Concat(currentHostedServiceTypes))
            {
                foreach (var constructor in moduleType.GetConstructors())
                {
                    // WP 13.10B (TD-cheap-hardening): forcing every
                    // parameter's ParameterType to resolve is a genuine CLR
                    // type-load, capable of throwing TypeLoadException/
                    // FileNotFoundException/FileLoadException/
                    // BadImageFormatException for a malformed or missing
                    // dependency belonging to this one plugin's own
                    // assembly - exactly the failure modes LoadOne already
                    // isolates for Assembly.LoadFrom itself, below.
                    // Corrected: the try must wrap constructor.GetParameters()
                    // itself, not only the inner _ = parameter.ParameterType
                    // access - RuntimeConstructorInfo.GetParameters() eagerly
                    // resolves every parameter's own Signature the moment
                    // it's called, so a genuinely unresolvable parameter type
                    // throws from GetParameters() itself, in this loop's own
                    // header, never reaching a per-parameter try/catch placed
                    // only around the body (confirmed live: an earlier
                    // version of this fix placed the try/catch one level too
                    // deep and never actually caught anything for its own
                    // documented scenario). Left unguarded, that exception is
                    // not a PluginException, so LoadPlugins's own
                    // catch (PluginException) would not catch it here - it
                    // would propagate out of LoadPlugins entirely, aborting
                    // every other plugin's own loading in the same call, not
                    // just this one's (violating ADR-0025's isolation
                    // discipline). Converted here into a
                    // PluginTrustDeniedException instead, so this one plugin
                    // is isolated and denied exactly like any other
                    // trust-check failure.
                    try
                    {
                        foreach (var parameter in constructor.GetParameters())
                        {
                            _ = parameter.ParameterType;
                        }
                    }
                    catch (Exception ex) when (ex is TypeLoadException or FileNotFoundException or FileLoadException or BadImageFormatException)
                    {
                        // Corrected, WP 13.11B (TD-51, reopened by WP 13.11A).
                        // This block used to throw here, immediately. Two
                        // separate defects followed from that, both closed by
                        // recording the denial and letting the scan run to its
                        // own fixed point instead:
                        //
                        // (1) The throw escaped DiscoverModuleTypes before
                        //     EnforceTrust's own two RecordDenied call sites
                        //     could run, so NOTHING was recorded in
                        //     deniedTypeRegistry for this one denial reason.
                        //     TempestHost's own WP 13.9.6 Module Discovery
                        //     filter (isTypeExcluded: deniedTypeRegistry.IsDenied)
                        //     therefore never excluded the offending type, and
                        //     ReflectionFrameworkDiscoveryService.CreateDescriptor's
                        //     own type.GetConstructor(Type.EmptyTypes) call -
                        //     itself a genuine CLR type-load - rethrew the
                        //     identical failure uncaught, out through
                        //     TempestHost.RunAsync, faulting the whole Host.
                        //     Reachable by a single, otherwise-inert IModule
                        //     type, any trust tier, zero requested
                        //     capabilities; empirically reproduced by
                        //     WP 13.11A's own Security/Adversarial review.
                        //
                        // (2) Aborting mid-scan also skipped the before/after
                        //     assembly diff below, so any assembly this
                        //     plugin had ALREADY pulled into the AppDomain -
                        //     enqueued but not yet dequeued, or loaded earlier
                        //     in this very step - was never scanned and its
                        //     own module types never recorded. Merely adding
                        //     a RecordDenied call to the old throw would have
                        //     left those types resident, undiscovered by this
                        //     scan, yet fully visible to Module Discovery's
                        //     own deliberately plugin-unaware AppDomain scan
                        //     (ADR-0110) - and a well-formed one among them
                        //     (attributed, parameterless ctor) would have been
                        //     registered and lifecycle-run with a null, and
                        //     therefore First-Party-treated (PluginTrustPermission.IsFirstParty),
                        //     ambient component principal. That would have
                        //     traded a Host crash for a silent trust bypass -
                        //     strictly worse, and not fail-closed.
                        //
                        // ??= keeps the FIRST offending type, so the thrown
                        // message is byte-identical to the one the immediate
                        // throw produced. break leaves this type's remaining
                        // constructors uninspected - the plugin is already
                        // denied, and every later constructor of an
                        // already-condemned type is irrelevant - while the
                        // enclosing loops carry on, so the diff below still
                        // runs and the fixed point still closes. EnforceTrust
                        // records and throws, above.
                        unresolvableConstructorDenial ??= new PluginTrustDeniedException(
                            pluginId,
                            $"Module or hosted service type '{moduleType.FullName}' declares a constructor " +
                            $"parameter whose type could not be resolved ('{ex.GetType().Name}': {ex.Message}).");

                        break;
                    }
                }
            }

            foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!before.Contains(loaded) && scannedAssemblies.Add(loaded))
                    toScan.Enqueue(loaded);
            }
        }

        hostedServiceTypes = allHostedServiceTypes;
        return allModuleTypes;
    }

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
    /// Mirrors <see cref="BackgroundServices.HostedServiceDiscoveryService"/>'s
    /// own <c>IsValidHostedServiceType</c> filter exactly (WP 13.9.4), run
    /// here independently and before handoff to Hosted Service Discovery —
    /// the same duplication-risk shape ADR-0111 already discloses for
    /// <see cref="IsModuleType"/>.
    /// </summary>
    private static bool IsHostedServiceType(Type type) =>
        typeof(BackgroundServices.IHostedService).IsAssignableFrom(type)
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
