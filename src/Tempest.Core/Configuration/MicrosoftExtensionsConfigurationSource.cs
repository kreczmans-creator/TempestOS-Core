using Microsoft.Extensions.Configuration;

namespace Tempest.Core.Configuration;

/// <summary>
/// An <see cref="IConfigurationSource"/> that builds a
/// <c>Microsoft.Extensions.Configuration</c> tree from the operator's own
/// environment and flattens it to the <c>Section:Key</c> strings this
/// platform's own <see cref="IConfigurationProvider"/> already uses
/// (`WP 17.2A`, ADR-0146).
/// </summary>
/// <remarks>
/// <para>
/// <b>Sources, in increasing precedence</b> (a later source overrides an
/// earlier one for any key both define — the same
/// <see cref="Tempest.Core.Configuration.ConfigurationBuilder"/> convention
/// this single, merged source is internally consistent with):
/// </para>
/// <list type="number">
/// <item><description>an <c>appsettings.json</c> file next to the running
/// executable (<see cref="AppContext.BaseDirectory"/>);</description></item>
/// <item><description>an <c>appsettings.json</c> file in the current
/// working directory;</description></item>
/// <item><description>environment variables prefixed
/// <see cref="EnvironmentVariablePrefix"/>, with <c>__</c> (double
/// underscore) as the section separator — the Microsoft.Extensions.Configuration
/// convention, so <c>TEMPEST_Runtime__Logging__MinimumLevel=Debug</c> sets
/// <c>Runtime:Logging:MinimumLevel</c>;</description></item>
/// <item><description>the command line, in
/// <c>Microsoft.Extensions.Configuration.CommandLine</c>'s own
/// <c>--Section:Key=value</c>/<c>/Section:Key value</c> shape.</description></item>
/// </list>
/// <para>
/// Both files are optional — a fresh install with no <c>appsettings.json</c>
/// anywhere is not an error, it simply contributes nothing from that
/// source. Every key this class yields is already flattened by
/// <see cref="ConfigurationExtensions.AsEnumerable(Microsoft.Extensions.Configuration.IConfiguration, bool)"/>,
/// using the same <c>:</c> section separator this platform's own keys
/// (<c>Runtime:Logging:MinimumLevel</c>) already use — no further parsing
/// is needed, and this single source can never itself produce a duplicate
/// key, because <c>Microsoft.Extensions.Configuration</c> has already
/// resolved every source's own precedence before <see cref="Load"/> ever
/// enumerates the result.
/// </para>
/// <para>
/// <b>Registered by <see cref="Runtime.TempestHostBuilder"/> before any
/// source added via <see cref="Runtime.ITempestHostBuilder.AddConfigurationSource"/>.</b>
/// Because <see cref="Tempest.Core.Configuration.ConfigurationBuilder"/>
/// lets a later-added source override an earlier one, this ordering means
/// an explicit in-memory override — the Desktop's own persistence-root
/// override, a test's isolated persistence root — always wins over
/// whatever this class read from a file, an environment variable, or the
/// command line.
/// </para>
/// </remarks>
public sealed class MicrosoftExtensionsConfigurationSource : IConfigurationSource
{
    /// <summary>The prefix an environment variable must carry to be read as configuration.</summary>
    public const string EnvironmentVariablePrefix = "TEMPEST_";

    /// <summary>The file name searched for next to the executable and in the current directory.</summary>
    public const string AppSettingsFileName = "appsettings.json";

    private readonly IReadOnlyList<string> _commandLineArgs;
    private readonly string _executableDirectory;
    private readonly string _currentDirectory;

    /// <summary>
    /// Initialises a new instance of the <see cref="MicrosoftExtensionsConfigurationSource"/>
    /// class for the real running process: <see cref="AppContext.BaseDirectory"/>
    /// and <see cref="Directory.GetCurrentDirectory"/> are searched for
    /// <see cref="AppSettingsFileName"/>.
    /// </summary>
    /// <param name="commandLineArgs">
    /// The process's own command-line arguments (<c>Program.Main(string[] args)</c>),
    /// or <see langword="null"/> to contribute none.
    /// </param>
    public MicrosoftExtensionsConfigurationSource(IReadOnlyList<string>? commandLineArgs = null)
        : this(commandLineArgs, AppContext.BaseDirectory, Directory.GetCurrentDirectory())
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="MicrosoftExtensionsConfigurationSource"/>
    /// class for a specific executable and current directory.
    /// </summary>
    /// <remarks>
    /// Internal test seam: lets a test point both directories at a
    /// controlled temporary directory, so precedence between the file
    /// source and the sources above it can be proven deterministically,
    /// without the test actually changing the process's own current
    /// directory or the location it happens to be built into.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="executableDirectory"/> or <paramref name="currentDirectory"/> is <see langword="null"/>.
    /// </exception>
    internal MicrosoftExtensionsConfigurationSource(
        IReadOnlyList<string>? commandLineArgs,
        string executableDirectory,
        string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(executableDirectory);
        ArgumentNullException.ThrowIfNull(currentDirectory);

        _commandLineArgs = commandLineArgs ?? [];
        _executableDirectory = executableDirectory;
        _currentDirectory = currentDirectory;
    }

    /// <inheritdoc />
    public IEnumerable<KeyValuePair<string, string>> Load()
    {
        var builder = new Microsoft.Extensions.Configuration.ConfigurationBuilder();

        builder.AddJsonFile(Path.Combine(_executableDirectory, AppSettingsFileName), optional: true, reloadOnChange: false);
        builder.AddJsonFile(Path.Combine(_currentDirectory, AppSettingsFileName), optional: true, reloadOnChange: false);
        builder.AddEnvironmentVariables(prefix: EnvironmentVariablePrefix);
        builder.AddCommandLine(_commandLineArgs.ToArray());

        var root = builder.Build();

        foreach (var entry in root.AsEnumerable())
        {
            if (entry.Value is not null)
                yield return new KeyValuePair<string, string>(entry.Key, entry.Value);
        }
    }
}
