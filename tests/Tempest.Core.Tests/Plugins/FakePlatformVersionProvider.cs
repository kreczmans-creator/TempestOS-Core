using Tempest.Core.Versioning;

namespace Tempest.Core.Tests.Plugins;

/// <summary>
/// A fixed, test-controlled <see cref="IPlatformVersionProvider"/>, so
/// <c>MinimumPlatformVersion</c> compatibility tests can pin the "running
/// platform version" to a specific value rather than depending on this
/// solution's own, incidental build version.
/// </summary>
internal sealed class FakePlatformVersionProvider : IPlatformVersionProvider
{
    public FakePlatformVersionProvider(Version version)
    {
        Version = new PlatformVersion(version.ToString(3), version, version.ToString(3));
    }

    public PlatformVersion Version { get; }
}
