using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.Json;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>
/// Where an installation's optional calculator features (`ADR-0173`) are switched on: a source only the machine's administrator can write. The
/// application itself installs per user (Velopack, under the user's own profile), so nothing the user can write, the install folder included, is
/// ever a source. An implementation reports the features that are on and, when it ignored a source it found but did not trust, says why.
/// </summary>
public interface ICalcFeatureSource
{
    /// <summary>The features switched on, with this source named in <see cref="CalcFeatureFlags.SourceOf"/>; every feature off when there is no trusted source.</summary>
    CalcFeatureFlags Read(out string? problem);
}

/// <summary>The sources the product reads.</summary>
public static class CalcFeatureSources
{
    /// <summary>The Windows policy key (under <c>HKEY_LOCAL_MACHINE</c>), an administrator's: each feature is a DWORD value named for it, <c>1</c> to switch it on.</summary>
    public const string WindowsPolicyKey = @"SOFTWARE\Policies\TempestOS\Features";

    /// <summary>The file the other platforms read, owned by root and writable by no one else.</summary>
    public const string UnixFeatureFile = "/etc/tempestos/features.json";

    /// <summary>The source for this machine: the HKLM policy key on Windows, the root-owned file elsewhere.</summary>
    public static ICalcFeatureSource ForThisMachine() =>
        OperatingSystem.IsWindows()
            ? new WindowsPolicyFeatureSource(WindowsPolicyFeatureSource.ReadMachineRegistry)
            : new UnixFeatureFileSource(UnixFeatureFile, new StatFileProbe());

    /// <summary>No feature is on and no source is read: what a harness that wants everything off composes.</summary>
    public static ICalcFeatureSource None { get; } = new NoFeatureSource();

    private sealed class NoFeatureSource : ICalcFeatureSource
    {
        public CalcFeatureFlags Read(out string? problem)
        {
            problem = null;
            return CalcFeatureFlags.None;
        }
    }
}

/// <summary>What a registry read found for one value.</summary>
public readonly record struct PolicyValue(bool Exists, bool IsDword, long Number);

/// <summary>
/// Reads the features from the machine policy key. Only a DWORD value of exactly <c>1</c> switches a feature on; a missing value, <c>0</c>, a string
/// ("1", "true") or any other kind leaves it off. The default reader opens <c>HKEY_LOCAL_MACHINE</c> only, read-only, and never the current user's hive.
/// </summary>
public sealed class WindowsPolicyFeatureSource : ICalcFeatureSource
{
    private readonly Func<string, string, PolicyValue> _read;

    /// <param name="read">Reads the value named by (key path under HKLM, value name); injectable so a test can prove which values count.</param>
    public WindowsPolicyFeatureSource(Func<string, string, PolicyValue> read)
    {
        _read = read ?? throw new ArgumentNullException(nameof(read));
    }

    public CalcFeatureFlags Read(out string? problem)
    {
        problem = null;
        var on = new List<string>();

        try
        {
            foreach (var feature in CalcFeatures.All)
            {
                var name = char.ToUpperInvariant(feature[0]) + feature[1..];
                var value = _read(CalcFeatureSources.WindowsPolicyKey, name);

                if (value is { Exists: true, IsDword: true, Number: 1 })
                    on.Add(feature);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or PlatformNotSupportedException)
        {
            problem = $"The machine policy key HKLM\\{CalcFeatureSources.WindowsPolicyKey} could not be read ({ex.GetType().Name}); every feature stays off.";
            return CalcFeatureFlags.None;
        }

        return new CalcFeatureFlags(on, $"machine policy HKLM\\{CalcFeatureSources.WindowsPolicyKey}");
    }

    /// <summary>Reads one value from <c>HKEY_LOCAL_MACHINE</c> (64-bit view), read-only.</summary>
    [SupportedOSPlatform("windows")]
    internal static PolicyValue ReadMachineRegistry(string keyPath, string valueName)
    {
        using var hklm = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64);
        using var key = hklm.OpenSubKey(keyPath, writable: false);

        if (key is null || key.GetValue(valueName, null, Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames) is not { } value)
            return new PolicyValue(false, false, 0);

        return key.GetValueKind(valueName) == Microsoft.Win32.RegistryValueKind.DWord
            ? new PolicyValue(true, true, Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) & 0xFFFFFFFFL)
            : new PolicyValue(true, false, 0);
    }
}

/// <summary>The owner and mode of a file or directory, whether it is a symbolic link, and whether it is a regular file (not a FIFO, socket or device).</summary>
public readonly record struct UnixFileFacts(uint OwnerUid, UnixFileMode Mode, bool IsSymbolicLink, bool IsRegularFile = true);

/// <summary>Looks a path up for the trust check; injectable so a test can say who owns it.</summary>
public interface IUnixFileProbe
{
    /// <summary>The facts about <paramref name="path"/>, or <see langword="null"/> when it does not exist or cannot be examined.</summary>
    UnixFileFacts? Probe(string path);
}

/// <summary>
/// Reads the features from a root-owned JSON file (<c>{ "Lifting": true }</c>). The file is trusted only when it, and the directory it is in, are
/// owned by root, are not symbolic links and are writable by no one else (no group or other write bit): a file a user can write is no administrator's
/// decision, so it is ignored and the reason is reported.
/// </summary>
public sealed class UnixFeatureFileSource : ICalcFeatureSource
{
    /// <summary>The largest file read (the file is a few lines).</summary>
    public const int MaxFileBytes = 64 * 1024;

    private const UnixFileMode Writable = UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;
    private readonly string _path;
    private readonly IUnixFileProbe _probe;

    public UnixFeatureFileSource(string path, IUnixFileProbe probe)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
    }

    public CalcFeatureFlags Read(out string? problem)
    {
        problem = null;

        try
        {
            if (_probe.Probe(_path) is not { } file)
                return CalcFeatureFlags.None;

            var directory = Path.GetDirectoryName(_path);

            if (string.IsNullOrEmpty(directory) || _probe.Probe(directory) is not { } folder)
            {
                problem = $"{_path} was ignored: its directory could not be examined; every feature stays off.";
                return CalcFeatureFlags.None;
            }

            // A FIFO or device at the path would block or stream forever when read (S8 verification I-2): only a regular file is read.
            var reason = (file.IsRegularFile ? null : "file is not a regular file") ?? Untrusted(file, "file") ?? Untrusted(folder, "directory");

            if (reason is not null)
            {
                problem = $"{_path} was ignored: the {reason}; every feature stays off.";
                return CalcFeatureFlags.None;
            }

            var info = new FileInfo(_path);

            if (info.Length > MaxFileBytes)
            {
                problem = $"{_path} is larger than {MaxFileBytes} bytes and was not read for feature flags.";
                return CalcFeatureFlags.None;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(_path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, MaxDepth = 8 });

            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return CalcFeatureFlags.None;

            var on = new List<string>();

            foreach (var feature in CalcFeatures.All)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (!string.Equals(property.Name, char.ToUpperInvariant(feature[0]) + feature[1..], StringComparison.Ordinal))
                        continue;

                    var value = property.Value;

                    if (value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.String && string.Equals(value.GetString()?.Trim(), "true", StringComparison.OrdinalIgnoreCase)))
                        on.Add(feature);
                }
            }

            return new CalcFeatureFlags(on, $"root-owned file {_path}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            problem = $"{_path} could not be read for feature flags ({ex.GetType().Name}); every feature stays off.";
            return CalcFeatureFlags.None;
        }
    }

    private static string? Untrusted(UnixFileFacts facts, string what)
    {
        if (facts.IsSymbolicLink)
            return $"{what} is a symbolic link";

        if (facts.OwnerUid != 0)
            return $"{what} is not owned by root (owner {facts.OwnerUid})";

        if ((facts.Mode & Writable) != 0)
            return $"{what} is writable by someone other than root";

        return null;
    }
}

/// <summary>The default probe: the mode from the runtime and the owner and link state from <c>/usr/bin/stat</c> (no shell, an absolute path).</summary>
[UnsupportedOSPlatform("windows")]
internal sealed class StatFileProbe : IUnixFileProbe
{
    public UnixFileFacts? Probe(string path)
    {
        try
        {
            var info = new FileInfo(path);
            var isDirectory = Directory.Exists(path);

            if (!isDirectory && !info.Exists)
                return null;

            FileSystemInfo entry = isDirectory ? new DirectoryInfo(path) : info;
            var mode = File.GetUnixFileMode(path);
            var stat = OwnerAndType(path);
            return stat is null ? null : new UnixFileFacts(stat.Value.Uid, mode, entry.LinkTarget is not null, isDirectory || stat.Value.IsRegular);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return null;
        }
    }

    private const uint FileTypeMask = 0xF000; // S_IFMT
    private const uint RegularFile = 0x8000; // S_IFREG

    private static (uint Uid, bool IsRegular)? OwnerAndType(string path)
    {
        // GNU stat first (raw mode in hex), then the BSD/macOS form (mode in octal).
        foreach (var (arguments, modeBase) in new[] { (new[] { "-c", "%u %f", path }, 16), (new[] { "-f", "%u %p", path }, 8) })
        {
            var start = new ProcessStartInfo("/usr/bin/stat") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };

            foreach (var argument in arguments)
                start.ArgumentList.Add(argument);

            using var process = Process.Start(start);

            if (process is null)
                continue;

            var output = process.StandardOutput.ReadToEnd().Trim();

            if (!process.WaitForExit(5000))
            {
                process.Kill();
                continue;
            }

            var parts = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (process.ExitCode != 0 || parts.Length != 2 || !uint.TryParse(parts[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var uid))
                continue;

            try
            {
                var rawMode = Convert.ToUInt32(parts[1], modeBase);
                return (uid, (rawMode & FileTypeMask) == RegularFile);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
            {
                continue;
            }
        }

        return null;
    }
}
