namespace Tempest.Desktop;

/// <summary>
/// The commit of the running build, read from the same place the title bar reads it (`WP 17.9.4`, <c>MainWindow.DescribeBuild</c>: the platform's
/// semantic version and the build metadata after the <c>+</c>). The guided test (Addendum C, C-5) records it in a run, so a test result and the window
/// it was run in name the same build.
/// </summary>
internal static class BuildDescription
{
    /// <summary>The short commit of the build (the seven characters the title bar shows), or an empty string when the build carries none.</summary>
    public static string Commit(Tempest.Core.DependencyInjection.ITempestServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var version = (services.GetService(typeof(Tempest.Core.Versioning.IPlatformVersionProvider)) as Tempest.Core.Versioning.IPlatformVersionProvider)?.Version.SemanticVersion;
        var plus = version?.IndexOf('+', StringComparison.Ordinal) ?? -1;

        if (version is null || plus < 0)
            return string.Empty;

        var metadata = version[(plus + 1)..];
        return metadata.Length > 7 ? metadata[..7] : metadata;
    }
}
