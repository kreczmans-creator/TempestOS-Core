namespace Tempest.Core.Tests;

/// <summary>
/// A test of something only Windows has — DPAPI behind
/// <c>WindowsDpapiSecretStore</c>, or Windows PowerShell 5
/// (<c>powershell.exe</c>), which the shipped scripts target. It runs on
/// Windows, as every CI leg is; anywhere else xUnit reports it
/// <b>skipped</b> with that reason rather than failed, so a run on another
/// platform shows only real failures.
/// </summary>
/// <remarks>
/// As <c>XeroLiveFactAttribute</c>: xUnit 2 has no run-time skip, so the
/// decision is made when the attribute is constructed.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class WindowsFactAttribute : FactAttribute
{
    /// <summary>Initialises a new instance of the <see cref="WindowsFactAttribute"/> class, skipping the test off Windows.</summary>
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Windows only: needs DPAPI or Windows PowerShell 5, which CI's Windows runners have.";
    }
}
