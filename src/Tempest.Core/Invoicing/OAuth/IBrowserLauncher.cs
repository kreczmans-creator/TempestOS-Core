using System.Diagnostics;

namespace Tempest.Core.Invoicing.OAuth;

/// <summary>
/// Opens a URL in whatever the operating system considers "the browser" —
/// the seam <see cref="OAuthAuthoriser"/> depends on rather than
/// <see cref="Process.Start(ProcessStartInfo)"/> directly, so a test drives
/// the authorisation-code round trip with a fake that calls the loopback
/// itself instead of ever popping a real browser window (`WP 19.1A` part 2).
/// </summary>
public interface IBrowserLauncher
{
    /// <summary>Opens <paramref name="url"/> in the system browser.</summary>
    void Open(Uri url);
}

/// <summary>
/// The real <see cref="IBrowserLauncher"/>: hands the URL to the operating
/// system shell exactly as if the user had typed it into a browser's own
/// address bar, so whichever browser is the user's own default opens it —
/// this platform's first use of <see cref="Process.Start(ProcessStartInfo)"/>
/// (no earlier precedent in live code).
/// </summary>
public sealed class SystemBrowserLauncher : IBrowserLauncher
{
    /// <inheritdoc />
    public void Open(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);

        Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true });
    }
}
