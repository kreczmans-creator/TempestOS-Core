using Tempest.Core.Modules;

namespace Tempest.Core.Plugins;

/// <summary>
/// Describes a plugin found by an <see cref="IPluginManifestDiscoveryService"/>,
/// before its assembly is ever loaded.
/// </summary>
/// <remarks>
/// An immutable snapshot, mirroring <see cref="Modules.ModuleDescriptor"/>
/// exactly, but describing something not yet loaded into the process rather
/// than something already reflectable — see <c>Plugin Manifest
/// Architecture.md</c>'s "The Manifest describes. The Runtime decides."
/// <see cref="AssemblyPath"/> is not itself a manifest field: it is the
/// fully-resolved, absolute form of <see cref="AssemblyFileName"/>, computed
/// once at discovery time (relative to the manifest's own folder) exactly as
/// <see cref="Modules.ModuleDescriptor.ModuleType"/> captures something derived
/// at discovery time rather than declared directly in <see cref="IModule"/>.
/// <see cref="TrustTier"/> is the same kind of discovery-computed value, one
/// step further: ADR-0112's own signature verification and tier-assignment
/// table (no <c>Signature</c> field plus <c>Plugins:AllowUnsignedLoad</c>;
/// a verifying <c>Signature</c> matched against the trust store) decides it —
/// a manifest never declares its own trust tier directly, exactly as it
/// never declares its own resolved <see cref="AssemblyPath"/>.
/// </remarks>
public sealed class PluginManifest
{
    /// <summary>
    /// Initialises a new instance of the <see cref="PluginManifest"/> class.
    /// </summary>
    /// <param name="id">The plugin's unique identifier.</param>
    /// <param name="name">The plugin's human-readable name.</param>
    /// <param name="version">The plugin's own version string.</param>
    /// <param name="minimumPlatformVersion">The minimum platform version the plugin requires.</param>
    /// <param name="assemblyFileName">The declared, manifest-relative assembly file name.</param>
    /// <param name="assemblyPath">The resolved, absolute path to the plugin's assembly.</param>
    /// <param name="dependencies">The plugin's declared inter-plugin dependencies.</param>
    /// <param name="requestedCapabilities">The plugin's declared, opaque requested capability identifiers.</param>
    /// <param name="publisher">The plugin's declared publisher, if any.</param>
    /// <param name="signature">The plugin's declared signature, if any.</param>
    /// <param name="trustTier">
    /// The plugin's trust tier, as computed by signature verification and
    /// tier assignment (ADR-0112) at Plugin Discovery/Loading time — never a
    /// manifest-declared value; see <see cref="TrustTier"/>.
    /// </param>
    public PluginManifest(
        string id,
        string name,
        string version,
        Version minimumPlatformVersion,
        string assemblyFileName,
        string assemblyPath,
        IReadOnlyList<PluginDependency> dependencies,
        IReadOnlyList<string> requestedCapabilities,
        string? publisher,
        string? signature,
        PluginTrustTier trustTier)
    {
        Id = id;
        Name = name;
        Version = version;
        MinimumPlatformVersion = minimumPlatformVersion;
        AssemblyFileName = assemblyFileName;
        AssemblyPath = assemblyPath;
        Dependencies = dependencies;
        RequestedCapabilities = requestedCapabilities;
        Publisher = publisher;
        Signature = signature;
        TrustTier = trustTier;
    }

    /// <summary>
    /// Gets the plugin's unique identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the plugin's human-readable name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the plugin's own version string.
    /// </summary>
    /// <remarks>
    /// A plain string, matching <see cref="IModule.Version"/>'s existing,
    /// unvalidated-format convention exactly — not cross-checked here; a
    /// mismatch against the loaded module's own <see cref="IModule.Version"/>
    /// is a matter for a future consumer, not this value object.
    /// </remarks>
    public string Version { get; }

    /// <summary>
    /// Gets the minimum platform version this plugin declares it requires.
    /// </summary>
    public Version MinimumPlatformVersion { get; }

    /// <summary>
    /// Gets the declared assembly file name, relative to the manifest's own folder.
    /// </summary>
    public string AssemblyFileName { get; }

    /// <summary>
    /// Gets the fully-resolved, absolute path to the plugin's assembly,
    /// computed at discovery time from the manifest's own folder and
    /// <see cref="AssemblyFileName"/>.
    /// </summary>
    public string AssemblyPath { get; }

    /// <summary>
    /// Gets the plugin's declared inter-plugin dependencies.
    /// </summary>
    /// <remarks>
    /// Never <see langword="null"/> — an empty list if none were declared.
    /// ADR-0107. Resolved into dependency-topological load order by Plugin
    /// Discovery's own graph resolution step; see
    /// <see cref="PluginManifestDiscoveryService"/>.
    /// </remarks>
    public IReadOnlyList<PluginDependency> Dependencies { get; }

    /// <summary>
    /// Gets the plugin's declared, opaque requested capability identifiers.
    /// </summary>
    /// <remarks>
    /// Never <see langword="null"/> — an empty list if none were declared.
    /// This document (<c>Plugin Platform Architecture.md</c>) defines only
    /// that this field exists, is a list of opaque strings, and is read, not
    /// interpreted, at Plugin Discovery time. Its semantics — what a
    /// capability identifier means, and whether a plugin is entitled to what
    /// it requests — are owned entirely by
    /// <c>docs/security/Plugin Trust &amp; Isolation Architecture.md</c> and
    /// are explicitly out of this Work Package's own scope; nothing in this
    /// Work Package validates, enforces, or otherwise acts on this field's
    /// content.
    /// </remarks>
    public IReadOnlyList<string> RequestedCapabilities { get; }

    /// <summary>
    /// Gets the plugin's declared publisher, if any.
    /// </summary>
    /// <remarks>
    /// Free text, unverified. Its semantics — verification, display, trust
    /// weighting — are owned entirely by
    /// <c>docs/security/Plugin Trust &amp; Isolation Architecture.md</c> and
    /// are explicitly out of this Work Package's own scope; this value is
    /// stored and forwarded only.
    /// </remarks>
    public string? Publisher { get; }

    /// <summary>
    /// Gets the plugin's declared signature, if any.
    /// </summary>
    /// <remarks>
    /// An opaque, encoded blob — algorithm and encoding undecided by this
    /// Work Package. Its semantics — algorithm, verification, failure
    /// handling — are owned entirely by
    /// <c>docs/security/Plugin Trust &amp; Isolation Architecture.md</c> and
    /// are explicitly out of this Work Package's own scope; this value is
    /// stored and forwarded only, never cryptographically evaluated here.
    /// </remarks>
    public string? Signature { get; }

    /// <summary>
    /// Gets the plugin's trust tier, as computed by signature verification
    /// and tier assignment (ADR-0112) at Plugin Discovery/Loading time.
    /// </summary>
    /// <remarks>
    /// <b>Discovery-computed, never manifest-declared</b> — exactly like
    /// <see cref="AssemblyPath"/>. Neither <see cref="PluginManifestDto"/>
    /// nor the raw JSON manifest file carries a <c>TrustTier</c> field; a
    /// plugin author cannot simply write <c>"TrustTier": "FirstParty"</c>
    /// into a manifest and be believed. This value is derived entirely from
    /// <see cref="Signature"/> (or its absence) and, where present, from
    /// which entry in the local trust store its embedded
    /// <c>PublisherCertificateThumbprint</c> matches — see ADR-0112,
    /// "Trust store and tier assignment". A <see cref="PluginManifest"/>
    /// instance is only ever constructed once a tier has been successfully
    /// assigned; a plugin that fails verification or is rejected for lacking
    /// a signature never reaches this constructor at all — see
    /// <see cref="PluginTrustTier"/>'s own remarks for why no fourth,
    /// "rejected" tier value exists here.
    /// </remarks>
    public PluginTrustTier TrustTier { get; }
}
