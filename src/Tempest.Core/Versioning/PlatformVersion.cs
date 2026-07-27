namespace Tempest.Core.Versioning;

/// <summary>
/// An immutable snapshot of the running platform's own version, resolved
/// once from the executing assembly's build-time metadata.
/// </summary>
/// <remarks>
/// Every value here is derived from the same underlying build metadata — no
/// value is ever hand-typed as a duplicated constant anywhere in the
/// platform. See <see cref="PlatformVersionProvider"/> for how each value is
/// resolved, and its documented fallback behaviour when a piece of metadata
/// is unavailable.
/// </remarks>
public sealed class PlatformVersion
{
    /// <summary>
    /// Initialises a new instance of the <see cref="PlatformVersion"/> class.
    /// </summary>
    /// <param name="semanticVersion">The platform's semantic version.</param>
    /// <param name="assemblyVersion">The executing assembly's own <see cref="System.Version"/>.</param>
    /// <param name="informationalVersion">
    /// The executing assembly's informational version, if one is present.
    /// </param>
    public PlatformVersion(string semanticVersion, Version assemblyVersion, string? informationalVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(semanticVersion);
        ArgumentNullException.ThrowIfNull(assemblyVersion);

        SemanticVersion = semanticVersion;
        AssemblyVersion = assemblyVersion;
        InformationalVersion = informationalVersion;
    }

    /// <summary>
    /// Gets the platform's semantic version (for example, <c>"0.3.0"</c>) —
    /// the same value recorded in the repository's own <c>VERSION</c> file
    /// at the time the running assembly was built.
    /// </summary>
    /// <remarks>
    /// Resolved from <see cref="InformationalVersion"/> when one is present;
    /// otherwise derived from <see cref="AssemblyVersion"/> — see
    /// <see cref="PlatformVersionProvider"/> for the exact fallback rule.
    /// </remarks>
    public string SemanticVersion { get; }

    /// <summary>
    /// Gets the executing assembly's own <see cref="System.Version"/>
    /// (<c>Major.Minor.Build.Revision</c>), as recorded by the .NET runtime
    /// itself. Never <see langword="null"/> — an assembly with no version
    /// metadata at all still has a default <see cref="System.Version"/>
    /// (<c>0.0.0.0</c>).
    /// </summary>
    public Version AssemblyVersion { get; }

    /// <summary>
    /// Gets the executing assembly's raw informational version string, if
    /// one is present in its build metadata; otherwise <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Present for every assembly built with the repository's own
    /// <c>Directory.Build.props</c> (which sets <c>&lt;Version&gt;</c>, and
    /// the .NET SDK derives an informational version from it automatically).
    /// <see langword="null"/> only for an assembly built without that
    /// metadata at all — see "Missing Metadata Behaviour" in
    /// <see cref="PlatformVersionProvider"/>'s own remarks.
    /// </remarks>
    public string? InformationalVersion { get; }
}
