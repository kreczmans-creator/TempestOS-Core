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
/// manifest file (<see cref="ManifestFileName"/> by default, overridable via
/// <c>Runtime:Plugins:ManifestFileName</c>). Candidates are processed in
/// deterministic, ordinal order by folder name — closing the precision gap
/// ADR-0026 identified in ADR-0025's own "first manifest encountered wins"
/// language, since raw filesystem enumeration order is not guaranteed stable
/// across operating systems or file systems.
/// </para>
/// <para>
/// Every plugin-scoped failure (ADR-0025, categories 1-5; ADR-0107, categories
/// 12-14) is isolated: logged at that category's assigned severity via
/// <see cref="PluginFailureLogging"/>, recorded into the Plugin Registry if one
/// is available, and excluded from the returned list.
/// <see cref="DiscoverManifests()"/> throws only for a genuine defect in this
/// service's own orchestration (ADR-0025, category 11) — enumerating the
/// plugins root itself, not processing any one candidate.
/// </para>
/// <para>
/// After every candidate has been individually validated, the surviving set's
/// declared inter-plugin dependencies (ADR-0107) are resolved via
/// <see cref="ResolveDependencyGraph"/> — a pure, side-effect-free computation
/// that stays entirely inside this Phase 3.1 boundary, with no new Host
/// Lifecycle phase.
/// </para>
/// <para>
/// <b>Signature verification and trust tier assignment (ADR-0112, WP 13.2A).</b>
/// Once a candidate's fields parse and validate, its declared <c>Signature</c>
/// (if any) is verified — parsed via <see cref="PluginSignatureEnvelope.TryParse"/>,
/// resolved against the optional, constructor-supplied <see cref="IPluginTrustStore"/>,
/// and cryptographically checked via <see cref="PluginSignatureVerifier"/> —
/// entirely from bytes already on disk (<see cref="File.ReadAllBytes(string)"/>),
/// never via <see cref="System.Reflection.Assembly.LoadFrom(string)"/>: this
/// remains Discovery, side-effect-free, per ADR-0026's own phase boundary and
/// ADR-0112's "Why Discovery, not Loading" reasoning. A missing/blank
/// <c>Signature</c> assigns <see cref="PluginTrustTier.UnsignedLocal"/> only
/// if the constructor-supplied <c>allowUnsignedLoad</c> flag is
/// <see langword="true"/>; otherwise it isolates the candidate
/// (<see cref="PluginUnsignedLoadNotAllowedException"/>, ADR-0025 category
/// 16). A present <c>Signature</c> that fails to parse or fails cryptographic
/// verification isolates the candidate identically
/// (<see cref="PluginSignatureVerificationFailedException"/>, category 15) —
/// never downgraded to Unsigned-Local. Both new exception types derive from
/// <see cref="PluginException"/> and flow through the exact same
/// <see cref="ProcessCandidate"/> catch block, <see cref="PluginFailureLogging"/>
/// call sites, and registry-recording path every other category already uses
/// — no parallel isolation mechanism is introduced.
/// </para>
/// </remarks>
public sealed class PluginManifestDiscoveryService : IPluginManifestDiscoveryService
{
    /// <summary>
    /// The default manifest file name expected inside each plugin candidate
    /// folder, used unless overridden via the <c>manifestFileName</c>
    /// constructor parameter (<c>Runtime:Plugins:ManifestFileName</c>).
    /// </summary>
    internal const string ManifestFileName = "plugin.manifest.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _pluginsRootPath;
    private readonly IPlatformVersionProvider _platformVersionProvider;
    private readonly ILogger? _logger;
    private readonly string _manifestFileName;
    private readonly IReadOnlyCollection<string>? _disabledPluginIds;
    private readonly IPluginRegistryRecorder? _registryRecorder;
    private readonly IPluginTrustStore? _trustStore;
    private readonly bool _allowUnsignedLoad;

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
    /// <param name="manifestFileName">
    /// The manifest file name expected inside each plugin candidate folder.
    /// Defaults to <see cref="ManifestFileName"/>; overridable via
    /// <c>Runtime:Plugins:ManifestFileName</c>.
    /// </param>
    /// <param name="disabledPluginIds">
    /// The set of plugin identifiers to skip via
    /// <c>Runtime:Plugins:Disabled</c> configuration, or
    /// <see langword="null"/> if none are disabled.
    /// </param>
    /// <param name="registryRecorder">
    /// An optional Plugin Registry write side, used to record each
    /// candidate's outcome. May be <see langword="null"/> if no registry is
    /// available.
    /// </param>
    /// <param name="trustStore">
    /// An optional plugin trust store, used to resolve a signed candidate's
    /// publisher certificate (ADR-0112). May be <see langword="null"/> if no
    /// trust store is available — in which case any candidate declaring a
    /// <c>Signature</c> is treated as failing verification (there is nothing
    /// to verify it against), and only <paramref name="allowUnsignedLoad"/>
    /// governs whether an unsigned candidate is accepted at all.
    /// </param>
    /// <param name="allowUnsignedLoad">
    /// Whether a candidate manifest with no <c>Signature</c> field is
    /// accepted, at <see cref="PluginTrustTier.UnsignedLocal"/>. Defaults to
    /// <see langword="false"/> — ADR-0112's own safe default
    /// (<c>Plugins:AllowUnsignedLoad</c>) — meaning an unsigned candidate is
    /// isolated (category 16) unless a caller explicitly opts in.
    /// </param>
    public PluginManifestDiscoveryService(
        IPlatformVersionProvider platformVersionProvider,
        ILogger? logger = null,
        string manifestFileName = ManifestFileName,
        IReadOnlyCollection<string>? disabledPluginIds = null,
        IPluginRegistryRecorder? registryRecorder = null,
        IPluginTrustStore? trustStore = null,
        bool allowUnsignedLoad = false)
        : this(
            Path.Combine(AppContext.BaseDirectory, "Plugins"),
            platformVersionProvider,
            logger,
            manifestFileName,
            disabledPluginIds,
            registryRecorder,
            trustStore,
            allowUnsignedLoad)
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
    /// <param name="manifestFileName">
    /// The manifest file name expected inside each plugin candidate folder.
    /// Defaults to <see cref="ManifestFileName"/>; overridable via
    /// <c>Runtime:Plugins:ManifestFileName</c>.
    /// </param>
    /// <param name="disabledPluginIds">
    /// The set of plugin identifiers to skip via
    /// <c>Runtime:Plugins:Disabled</c> configuration, or
    /// <see langword="null"/> if none are disabled.
    /// </param>
    /// <param name="registryRecorder">
    /// An optional Plugin Registry write side, used to record each
    /// candidate's outcome. May be <see langword="null"/> if no registry is
    /// available.
    /// </param>
    /// <param name="trustStore">
    /// An optional plugin trust store, used to resolve a signed candidate's
    /// publisher certificate (ADR-0112). May be <see langword="null"/>.
    /// </param>
    /// <param name="allowUnsignedLoad">
    /// Whether a candidate manifest with no <c>Signature</c> field is
    /// accepted, at <see cref="PluginTrustTier.UnsignedLocal"/>. Defaults to
    /// <see langword="false"/>.
    /// </param>
    /// <remarks>
    /// Internal test seam — mirrors
    /// <see cref="Modules.ReflectionFrameworkDiscoveryService"/>'s own
    /// internal, assembly-set-accepting constructor, so discovery can be
    /// exercised deterministically against a controlled temporary directory
    /// in tests. Every existing caller of this constructor that does not
    /// pass <paramref name="trustStore"/>/<paramref name="allowUnsignedLoad"/>
    /// reproduces today's default — no trust store, unsigned load
    /// disallowed — which means (ADR-0112's own table, category 16) a
    /// pre-WP-13.2A test manifest carrying no <c>Signature</c> field is now
    /// isolated unless it is updated to pass <c>allowUnsignedLoad: true</c>
    /// explicitly. This is a deliberate, disclosed consequence of
    /// implementing ADR-0112 faithfully, not a defect of this constructor's
    /// own default values — see this Work Package's own final report.
    /// </remarks>
    internal PluginManifestDiscoveryService(
        string pluginsRootPath,
        IPlatformVersionProvider platformVersionProvider,
        ILogger? logger = null,
        string manifestFileName = ManifestFileName,
        IReadOnlyCollection<string>? disabledPluginIds = null,
        IPluginRegistryRecorder? registryRecorder = null,
        IPluginTrustStore? trustStore = null,
        bool allowUnsignedLoad = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginsRootPath);
        ArgumentNullException.ThrowIfNull(platformVersionProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestFileName);

        _pluginsRootPath = pluginsRootPath;
        _platformVersionProvider = platformVersionProvider;
        _logger = logger;
        _manifestFileName = manifestFileName;
        _disabledPluginIds = disabledPluginIds;
        _registryRecorder = registryRecorder;
        _trustStore = trustStore;
        _allowUnsignedLoad = allowUnsignedLoad;
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
    /// <returns>The eligible plugin manifests, in dependency-topological load order.</returns>
    /// <remarks>
    /// This overload is <see langword="internal"/>. It isolates the core
    /// discovery algorithm — manifest parsing, validation, version
    /// compatibility, duplicate detection, and dependency graph resolution —
    /// from plugins-root enumeration, mirroring
    /// <see cref="Modules.ReflectionFrameworkDiscoveryService"/>'s own
    /// <c>DiscoverModules(IEnumerable&lt;Type&gt;)</c> seam, so it can be
    /// exercised against candidates that cannot be constructed as real
    /// directories on disk (for example, proving that an exception outside
    /// ADR-0025's classification is not swallowed here).
    /// </remarks>
    internal IReadOnlyList<PluginManifest> DiscoverManifests(IEnumerable<string> candidateFolders)
    {
        var acceptedById = new Dictionary<string, PluginManifest>(StringComparer.Ordinal);
        var orderedIds = new List<string>();

        foreach (var folder in candidateFolders)
            ProcessCandidate(folder, acceptedById, orderedIds);

        var result = ResolveDependencyGraph(acceptedById, orderedIds);

        _logger?.Information($"Plugin discovery completed. {result.Count} plugin(s) eligible.");

        return result;
    }

    private void ProcessCandidate(string folder, Dictionary<string, PluginManifest> acceptedById, List<string> orderedIds)
    {
        var manifestPath = Path.Combine(folder, _manifestFileName);

        if (!File.Exists(manifestPath))
        {
            _logger?.Warning(
                $"Plugin candidate folder '{folder}' does not contain a manifest file " +
                $"('{_manifestFileName}'); skipping.");

            return;
        }

        try
        {
            var manifest = ParseAndValidate(folder, manifestPath);

            if (manifest is null)
            {
                // Disabled via Runtime:Plugins:Disabled — already logged and
                // recorded inside ParseAndValidate. Not a failure of any kind.
                return;
            }

            if (acceptedById.ContainsKey(manifest.Id))
                throw new DuplicatePluginIdException(manifest.Id);

            acceptedById.Add(manifest.Id, manifest);
            orderedIds.Add(manifest.Id);

            _logger?.Information(
                $"Plugin manifest accepted: '{manifest.Id}' ({manifest.Name} v{manifest.Version}) from '{folder}'.");
        }
        catch (PluginException ex)
        {
            PluginFailureLogging.LogIsolatedFailure(_logger, ex, folder);
            PluginFailureLogging.RecordIsolatedFailure(_registryRecorder, ex, folder);
        }
    }

    private PluginManifest? ParseAndValidate(string folder, string manifestPath)
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

        if (_disabledPluginIds is not null && _disabledPluginIds.Contains(dto.Id!, StringComparer.Ordinal))
        {
            _logger?.Information($"Plugin '{dto.Id}' is disabled via configuration; skipping.");
            _registryRecorder?.Record(new PluginRegistryEntry(
                dto.Id!, dto.Name, dto.Version, PluginRegistryState.Disabled, "Disabled via Runtime:Plugins:Disabled configuration."));

            return null;
        }

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
        string normalizedFolder;

        try
        {
            normalizedFolder = Path.GetFullPath(folder);
            assemblyPath = Path.GetFullPath(Path.Combine(normalizedFolder, dto.AssemblyFileName!));
        }
        catch (ArgumentException ex)
        {
            throw new InvalidPluginManifestException(
                $"Manifest file '{manifestPath}' has an invalid AssemblyFileName value: " +
                $"'{dto.AssemblyFileName}'.", ex);
        }

        // Security baseline (WP 5.0S): AssemblyFileName is manifest-declared,
        // untrusted input. Without this check, an absolute path or a "../" escape
        // would resolve outside the plugin's own candidate folder — Path.Combine
        // discards its first argument entirely when the second is rooted. The
        // manifest declares a file *within its own folder*; nothing outside that
        // folder is a valid target, regardless of what this plugin is otherwise
        // trusted to do once loaded (see Plugin Manifest Architecture.md).
        if (!IsWithinFolder(assemblyPath, normalizedFolder))
        {
            throw new InvalidPluginManifestException(
                $"Manifest file '{manifestPath}' declares an AssemblyFileName value that resolves " +
                $"outside its own candidate folder: '{dto.AssemblyFileName}'.");
        }

        var dependencies = ParseDependencies(dto.Dependencies, manifestPath);
        var requestedCapabilities = dto.RequestedCapabilities ?? [];

        var trustTier = AssignTrustTier(dto, assemblyPath);

        return new PluginManifest(
            dto.Id!,
            dto.Name!,
            dto.Version!,
            minimumPlatformVersion,
            dto.AssemblyFileName!,
            assemblyPath,
            dependencies,
            requestedCapabilities,
            dto.Publisher,
            dto.Signature,
            trustTier);
    }

    /// <summary>
    /// Verifies <paramref name="dto"/>'s own declared <c>Signature</c> (if
    /// any) and assigns the resulting <see cref="PluginTrustTier"/>, exactly
    /// per ADR-0112's own tier-assignment table.
    /// </summary>
    /// <param name="dto">The candidate's already field-validated raw manifest.</param>
    /// <param name="assemblyPath">The candidate's resolved, absolute assembly path.</param>
    /// <exception cref="PluginUnsignedLoadNotAllowedException">
    /// <paramref name="dto"/> declares no <c>Signature</c> and this service was
    /// not constructed with <c>allowUnsignedLoad: true</c> (category 16).
    /// </exception>
    /// <exception cref="PluginSignatureVerificationFailedException">
    /// <paramref name="dto"/> declares a <c>Signature</c> that fails to parse
    /// or fails cryptographic verification (category 15) — never downgraded
    /// to <see cref="PluginTrustTier.UnsignedLocal"/>.
    /// </exception>
    private PluginTrustTier AssignTrustTier(PluginManifestDto dto, string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(dto.Signature))
        {
            if (_allowUnsignedLoad)
                return PluginTrustTier.UnsignedLocal;

            throw new PluginUnsignedLoadNotAllowedException(dto.Id!);
        }

        // A Signature value that fails to parse as the expected JSON
        // envelope is treated identically to one that fails cryptographic
        // verification (ADR-0112) - both funnel into the same
        // failureReason-then-throw path below, never a separate, softer
        // category.
        var envelope = PluginSignatureEnvelope.TryParse(dto.Signature);
        string? failureReason;

        if (envelope is null)
        {
            failureReason = "Signature value could not be parsed as the expected JSON envelope.";
        }
        else
        {
            var matchedCertificate = _trustStore?.FindByThumbprint(envelope.PublisherCertificateThumbprint);

            if (matchedCertificate is null)
            {
                failureReason = _trustStore is null
                    ? "No plugin trust store is configured; the signature cannot be verified."
                    : $"No trusted publisher certificate matches thumbprint '{envelope.PublisherCertificateThumbprint}'.";
            }
            else
            {
                failureReason = VerifySignature(dto, assemblyPath, envelope, matchedCertificate);
            }
        }

        if (failureReason is not null)
            throw new PluginSignatureVerificationFailedException(dto.Id!, failureReason);

        return _trustStore!.IsFirstPartyThumbprint(envelope!.PublisherCertificateThumbprint)
            ? PluginTrustTier.FirstParty
            : PluginTrustTier.VerifiedSigned;
    }

    /// <summary>
    /// Recomputes the manifest+assembly payload (ADR-0112) from bytes
    /// already on disk — <see cref="File.ReadAllBytes(string)"/> only, never
    /// <see cref="System.Reflection.Assembly.LoadFrom(string)"/> — and
    /// cryptographically verifies <paramref name="envelope"/>'s own signature
    /// value against it.
    /// </summary>
    /// <returns>
    /// <see langword="null"/> if verification succeeds; otherwise a
    /// human-readable failure reason.
    /// </returns>
    private static string? VerifySignature(
        PluginManifestDto dto,
        string assemblyPath,
        PluginSignatureEnvelope envelope,
        System.Security.Cryptography.X509Certificates.X509Certificate2 matchedCertificate)
    {
        byte[] assemblyBytes;

        try
        {
            assemblyBytes = File.ReadAllBytes(assemblyPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"The declared assembly '{assemblyPath}' could not be read to verify its signature: {ex.Message}";
        }

        var dtoWithSignatureNulled = new PluginManifestDto
        {
            Id = dto.Id,
            Name = dto.Name,
            Version = dto.Version,
            MinimumPlatformVersion = dto.MinimumPlatformVersion,
            AssemblyFileName = dto.AssemblyFileName,
            Dependencies = dto.Dependencies,
            RequestedCapabilities = dto.RequestedCapabilities,
            Publisher = dto.Publisher,
            Signature = null,
        };

        var manifestHash = PluginSignatureVerifier.ComputeManifestHash(dtoWithSignatureNulled);
        var assemblyHash = PluginSignatureVerifier.ComputeAssemblyHash(assemblyBytes);
        var payload = PluginSignatureVerifier.BuildPayload(manifestHash, assemblyHash);

        return PluginSignatureVerifier.Verify(payload, envelope.Value, matchedCertificate)
            ? null
            : "The signature does not verify against the recomputed manifest and assembly hash.";
    }

    private static IReadOnlyList<PluginDependency> ParseDependencies(IReadOnlyList<PluginDependencyDto>? dtos, string manifestPath)
    {
        if (dtos is null || dtos.Count == 0)
            return [];

        var dependencies = new List<PluginDependency>(dtos.Count);

        foreach (var dependencyDto in dtos)
        {
            RequireField(dependencyDto.Id, "Dependencies[].Id", manifestPath);
            RequireField(dependencyDto.MinimumVersion, "Dependencies[].MinimumVersion", manifestPath);

            if (!Version.TryParse(dependencyDto.MinimumVersion, out var minimumVersion))
            {
                throw new InvalidPluginManifestException(
                    $"Manifest file '{manifestPath}' declares a dependency on '{dependencyDto.Id}' with an " +
                    $"unparseable MinimumVersion value: '{dependencyDto.MinimumVersion}'.");
            }

            Version? maximumVersion = null;

            if (!string.IsNullOrWhiteSpace(dependencyDto.MaximumVersion))
            {
                if (!Version.TryParse(dependencyDto.MaximumVersion, out maximumVersion))
                {
                    throw new InvalidPluginManifestException(
                        $"Manifest file '{manifestPath}' declares a dependency on '{dependencyDto.Id}' with an " +
                        $"unparseable MaximumVersion value: '{dependencyDto.MaximumVersion}'.");
                }

                if (maximumVersion < minimumVersion)
                {
                    throw new InvalidPluginManifestException(
                        $"Manifest file '{manifestPath}' declares a dependency on '{dependencyDto.Id}' whose " +
                        $"MaximumVersion ('{maximumVersion}') is less than its MinimumVersion ('{minimumVersion}').");
                }
            }

            dependencies.Add(new PluginDependency(dependencyDto.Id!, minimumVersion, maximumVersion));
        }

        return dependencies;
    }

    /// <summary>
    /// Resolves the dependency graph (ADR-0107) over the surviving,
    /// individually-valid candidate manifests in <paramref name="acceptedById"/>.
    /// </summary>
    /// <param name="acceptedById">
    /// Every candidate that passed individual validation, keyed by its own
    /// declared <see cref="PluginManifest.Id"/>. Mutated in place: any
    /// candidate excluded by dependency resolution is removed.
    /// </param>
    /// <param name="orderedIds">
    /// The deterministic (ordinal folder-name) order candidates were
    /// accepted in — the tie-break of last resort for both removal order and
    /// the final topological sort.
    /// </param>
    /// <returns>The surviving candidates, in dependency-topological load order.</returns>
    /// <remarks>
    /// A fixed-point reduction, not a bespoke cascade (ADR-0107): a candidate
    /// whose own dependency was itself excluded — for any reason, including a
    /// dependency of its own being unmet — is removed automatically in the
    /// next reduction pass, with no separate propagation logic required. A
    /// cycle is detected the same way a missing dependency is, after the
    /// fixed point stabilises: every participating plugin, and only those
    /// plugins, is isolated. Removing a cycle can newly break another
    /// candidate's previously-satisfied dependency on a removed cycle member,
    /// so the whole fixed-point-then-cycle-detection sequence repeats until a
    /// full pass of each finds nothing left to remove.
    /// </remarks>
    private List<PluginManifest> ResolveDependencyGraph(Dictionary<string, PluginManifest> acceptedById, List<string> orderedIds)
    {
        while (true)
        {
            var removedByFixedPoint = RemoveCandidatesWithUnmetDependencies(acceptedById, orderedIds);
            var removedByCycleDetection = FindAndRemoveCycles(acceptedById, orderedIds);

            if (!removedByFixedPoint && !removedByCycleDetection)
                break;
        }

        return TopologicalSort(acceptedById, orderedIds);
    }

    /// <summary>
    /// Repeatedly removes any remaining candidate whose declared dependency
    /// is missing from, or version-incompatible with, the current surviving
    /// set, until a full pass removes nothing.
    /// </summary>
    /// <returns><see langword="true"/> if any candidate was removed across all passes.</returns>
    private bool RemoveCandidatesWithUnmetDependencies(Dictionary<string, PluginManifest> acceptedById, List<string> orderedIds)
    {
        var removedAny = false;
        bool removedThisPass;

        do
        {
            removedThisPass = false;

            foreach (var id in orderedIds)
            {
                if (!acceptedById.TryGetValue(id, out var candidate))
                    continue;

                foreach (var dependency in candidate.Dependencies)
                {
                    if (!acceptedById.TryGetValue(dependency.Id, out var target))
                    {
                        Isolate(candidate, new MissingPluginDependencyException(candidate.Id, dependency.Id), acceptedById);
                        removedThisPass = true;
                        removedAny = true;
                        break;
                    }

                    Version.TryParse(target.Version, out var actualVersion);

                    var incompatible = actualVersion is null
                        || actualVersion < dependency.MinimumVersion
                        || (dependency.MaximumVersion is not null && actualVersion > dependency.MaximumVersion);

                    if (incompatible)
                    {
                        Isolate(
                            candidate,
                            new IncompatiblePluginDependencyVersionException(
                                candidate.Id, dependency.Id, dependency.MinimumVersion, dependency.MaximumVersion, target.Version),
                            acceptedById);
                        removedThisPass = true;
                        removedAny = true;
                        break;
                    }
                }
            }
        }
        while (removedThisPass);

        return removedAny;
    }

    /// <summary>
    /// Detects every candidate participating in a dependency cycle, via a
    /// three-colour depth-first search over the current surviving set, and
    /// removes each one found.
    /// </summary>
    /// <returns><see langword="true"/> if any candidate was removed.</returns>
    private bool FindAndRemoveCycles(Dictionary<string, PluginManifest> acceptedById, List<string> orderedIds)
    {
        var color = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new List<string>();
        var cyclePathsById = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var id in orderedIds)
        {
            if (!acceptedById.ContainsKey(id))
                continue;

            if (!color.ContainsKey(id))
                Visit(id, acceptedById, color, stack, cyclePathsById);
        }

        if (cyclePathsById.Count == 0)
            return false;

        foreach (var id in orderedIds)
        {
            if (!cyclePathsById.TryGetValue(id, out var cyclePath))
                continue;

            if (acceptedById.TryGetValue(id, out var candidate))
                Isolate(candidate, new CircularPluginDependencyException(id, cyclePath), acceptedById);
        }

        return true;
    }

    private static void Visit(
        string id,
        Dictionary<string, PluginManifest> acceptedById,
        Dictionary<string, int> color,
        List<string> stack,
        Dictionary<string, List<string>> cyclePathsById)
    {
        const int White = 0;
        const int Gray = 1;
        const int Black = 2;

        color[id] = Gray;
        stack.Add(id);

        if (acceptedById.TryGetValue(id, out var manifest))
        {
            foreach (var dependency in manifest.Dependencies)
            {
                if (!acceptedById.ContainsKey(dependency.Id))
                    continue;

                var dependencyColor = color.GetValueOrDefault(dependency.Id, White);

                if (dependencyColor == White)
                {
                    Visit(dependency.Id, acceptedById, color, stack, cyclePathsById);
                }
                else if (dependencyColor == Gray)
                {
                    var startIndex = stack.IndexOf(dependency.Id);
                    var cycle = stack.Skip(startIndex).ToList();
                    cycle.Add(dependency.Id);

                    foreach (var node in cycle.Take(cycle.Count - 1))
                    {
                        if (!cyclePathsById.ContainsKey(node))
                            cyclePathsById[node] = cycle;
                    }
                }
            }
        }

        stack.RemoveAt(stack.Count - 1);
        color[id] = Black;
    }

    /// <summary>
    /// Topologically sorts the surviving candidates for load order, via a
    /// Kahn's-algorithm pass — a candidate loads only after every plugin it
    /// depends on — breaking ties between candidates that share no ordering
    /// constraint using <paramref name="orderedIds"/>'s own ordinal
    /// folder-name order.
    /// </summary>
    private static List<PluginManifest> TopologicalSort(Dictionary<string, PluginManifest> acceptedById, List<string> orderedIds)
    {
        var survivors = orderedIds.Where(acceptedById.ContainsKey).ToList();

        // Distinct target count, not raw Dependencies.Count: a manifest
        // declaring the same dependency Id more than once (nothing rejects
        // this as invalid input - it is still one graph edge, just declared
        // redundantly) must still reach zero once that one target is
        // emitted. The decrement loop below fires once per removed target
        // (via .Any), never once per duplicate entry - counting duplicates
        // here would leave remainingDependencyCount permanently above zero
        // for a perfectly valid candidate, silently dropping it from the
        // result with no isolation, no log line, and no registry entry.
        var remainingDependencyCount = survivors.ToDictionary(
            id => id,
            id => acceptedById[id].Dependencies.Select(dependency => dependency.Id).Distinct(StringComparer.Ordinal).Count(),
            StringComparer.Ordinal);
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<PluginManifest>(survivors.Count);

        while (emitted.Count < survivors.Count)
        {
            var next = survivors.FirstOrDefault(id => !emitted.Contains(id) && remainingDependencyCount[id] == 0);

            if (next is null)
                break;

            emitted.Add(next);
            result.Add(acceptedById[next]);

            foreach (var id in survivors)
            {
                if (emitted.Contains(id))
                    continue;

                if (acceptedById[id].Dependencies.Any(dependency => dependency.Id == next))
                    remainingDependencyCount[id]--;
            }
        }

        return result;
    }

    private void Isolate(PluginManifest candidate, PluginException exception, Dictionary<string, PluginManifest> acceptedById)
    {
        PluginFailureLogging.LogIsolatedFailure(_logger, exception, candidate.Id);
        PluginFailureLogging.RecordIsolatedFailure(_registryRecorder, exception, candidate.Id);
        acceptedById.Remove(candidate.Id);
    }

    private static bool IsWithinFolder(string candidatePath, string normalizedFolder)
    {
        var folderWithSeparator = normalizedFolder.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedFolder
            : normalizedFolder + Path.DirectorySeparatorChar;

        return candidatePath.StartsWith(folderWithSeparator, StringComparison.OrdinalIgnoreCase);
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
