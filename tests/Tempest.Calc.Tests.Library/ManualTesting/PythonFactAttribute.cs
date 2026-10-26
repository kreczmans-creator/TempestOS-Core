using System.Diagnostics;

namespace Tempest.Calc.Tests.Library.ManualTesting;

/// <summary>
/// A test that needs Python 3.10 or later (the guided test script generator is a Python script). Where there is none, xUnit reports the test
/// <b>skipped</b>, with a reason that says so, instead of passing it silently: a skipped line in the run output is how a machine without Python is seen.
/// CI images carry Python (<c>scripts/check-licences.py</c> needs it).
/// </summary>
/// <remarks>As <c>WindowsFactAttribute</c>: xUnit 2 has no run-time skip, so the decision is made when the attribute is constructed.</remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PythonFactAttribute : FactAttribute
{
    private static readonly Lazy<string?> Found = new(Find);

    /// <summary>Initialises a new instance of the <see cref="PythonFactAttribute"/> class, skipping the test when Python is not available.</summary>
    public PythonFactAttribute()
    {
        if (Found.Value is null)
            Skip = "skipped: Python not available (needs python3 3.10 or later); the shipped guided test script was not re-generated and compared.";
    }

    /// <summary>The command that runs Python 3.10 or later here, or <see langword="null"/>.</summary>
    public static string? Command => Found.Value;

    private static string? Find() => new[] { "python3", "python" }.FirstOrDefault(Runs);

    private static bool Runs(string python)
    {
        try
        {
            var start = new ProcessStartInfo(python, "-I -c \"import sys; sys.exit(0 if sys.version_info >= (3, 10) else 1)\"") { RedirectStandardOutput = true, RedirectStandardError = true };
            using var probe = Process.Start(start);
            return probe is not null && probe.WaitForExit(20_000) && probe.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
