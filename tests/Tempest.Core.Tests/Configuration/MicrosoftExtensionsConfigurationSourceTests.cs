using Tempest.Core.Configuration;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Configuration;

/// <summary>
/// Proves <see cref="MicrosoftExtensionsConfigurationSource"/>'s own
/// precedence — file, then environment variable, then command line, each
/// overriding the one before it for a key all three define — and that it
/// flattens to the platform's own <c>Section:Key</c> shape (`WP 17.2A`,
/// ADR-0146).
/// </summary>
public class MicrosoftExtensionsConfigurationSourceTests
{
    private const string EnvironmentVariableName = "TEMPEST_Runtime__Precedence__Key";

    [Fact]
    public void Load_FileValue_IsReadWhenNothingElseOverridesIt()
    {
        using var temp = new TempDirectory();
        WriteAppSettings(temp.Path, """{ "Runtime": { "Precedence": { "Key": "from-file" } } }""");

        var source = new MicrosoftExtensionsConfigurationSource(null, temp.Path, temp.Path);

        var value = Single(source, "Runtime:Precedence:Key");

        Assert.Equal("from-file", value);
    }

    [Fact]
    public void Load_EnvironmentVariable_OverridesTheFile()
    {
        using var temp = new TempDirectory();
        WriteAppSettings(temp.Path, """{ "Runtime": { "Precedence": { "Key": "from-file" } } }""");

        using var scopedEnvironmentVariable = new ScopedEnvironmentVariable(EnvironmentVariableName, "from-environment");

        var source = new MicrosoftExtensionsConfigurationSource(null, temp.Path, temp.Path);

        var value = Single(source, "Runtime:Precedence:Key");

        Assert.Equal("from-environment", value);
    }

    [Fact]
    public void Load_CommandLine_OverridesBothTheFileAndTheEnvironmentVariable()
    {
        using var temp = new TempDirectory();
        WriteAppSettings(temp.Path, """{ "Runtime": { "Precedence": { "Key": "from-file" } } }""");

        using var scopedEnvironmentVariable = new ScopedEnvironmentVariable(EnvironmentVariableName, "from-environment");

        var source = new MicrosoftExtensionsConfigurationSource(
            ["--Runtime:Precedence:Key=from-command-line"], temp.Path, temp.Path);

        var value = Single(source, "Runtime:Precedence:Key");

        Assert.Equal("from-command-line", value);
    }

    [Fact]
    public void Load_ExecutableDirectoryFile_IsOverriddenByCurrentDirectoryFile()
    {
        using var executableDirectory = new TempDirectory();
        using var currentDirectory = new TempDirectory();

        WriteAppSettings(executableDirectory.Path, """{ "Runtime": { "Precedence": { "Key": "from-executable-directory" } } }""");
        WriteAppSettings(currentDirectory.Path, """{ "Runtime": { "Precedence": { "Key": "from-current-directory" } } }""");

        var source = new MicrosoftExtensionsConfigurationSource(null, executableDirectory.Path, currentDirectory.Path);

        var value = Single(source, "Runtime:Precedence:Key");

        Assert.Equal("from-current-directory", value);
    }

    [Fact]
    public void Load_NoFileEnvironmentVariableOrCommandLine_YieldsNoEntryForTheKey()
    {
        using var temp = new TempDirectory();

        var source = new MicrosoftExtensionsConfigurationSource(null, temp.Path, temp.Path);

        Assert.DoesNotContain(source.Load(), entry => entry.Key == "Runtime:Precedence:Key");
    }

    private static void WriteAppSettings(string directory, string json) =>
        File.WriteAllText(Path.Combine(directory, MicrosoftExtensionsConfigurationSource.AppSettingsFileName), json);

    private static string Single(MicrosoftExtensionsConfigurationSource source, string key) =>
        source.Load().Single(entry => entry.Key == key).Value;

    /// <summary>Sets an environment variable for the life of this instance, restoring whatever it was before.</summary>
    private sealed class ScopedEnvironmentVariable : IDisposable
    {
        private readonly string _name;
        private readonly string? _originalValue;

        public ScopedEnvironmentVariable(string name, string value)
        {
            _name = name;
            _originalValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _originalValue);
    }
}
