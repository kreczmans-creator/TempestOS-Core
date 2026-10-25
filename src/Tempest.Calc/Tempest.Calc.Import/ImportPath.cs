using System.Globalization;
using System.Text;

namespace Tempest.Calc.Import;

/// <summary>
/// Path canonicalisation for a file an engineer points an importer at. A path is refused (never repaired) when it contains
/// a control or format character, a <c>..</c> segment, a network share or device prefix (<c>\\server\share</c>,
/// <c>\\.\</c>, <c>\\?\</c>, <c>/dev/</c>), an alternate data stream or drive-relative form, a Windows reserved device
/// name, or a segment ending in a dot or space; and, when a root is given, when the result is not inside that root.
/// A file that is a link is not followed.
/// </summary>
public static class ImportPath
{
    private const int MaxPathChars = 1024;
    private const int MaxSegmentChars = 255;

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private static readonly string[] DeviceRoots = ["/dev/", "/proc/", "/sys/"];

    /// <summary>Checks <paramref name="path"/> and returns its full form; no file access.</summary>
    public static string Canonicalise(string path, string? requiredRoot = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw Unsafe("No file path was given.");

        if (path.Length > MaxPathChars)
            throw Unsafe($"The file path is longer than {MaxPathChars} characters.");

        foreach (var c in path)
        {
            if (char.IsControl(c) || CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
                throw Unsafe("The file path holds a control or invisible character.");
        }

        var norm = path.Replace('\\', '/');

        if (norm.StartsWith("//", StringComparison.Ordinal))
            throw Unsafe("Network shares and device paths (\\\\server\\share, \\\\.\\, \\\\?\\) are not read.");

        foreach (var root in DeviceRoots)
        {
            if (norm.StartsWith(root, StringComparison.Ordinal))
                throw Unsafe("Device and system paths are not read.");
        }

        var rest = norm;

        if (norm.Length >= 2 && norm[1] == ':' && char.IsAsciiLetter(norm[0]))
        {
            if (norm.Length == 2 || norm[2] != '/')
                throw Unsafe("A drive-relative path (C:file) is not read; give the full path.");

            rest = norm[2..];
        }

        if (rest.Contains(':', StringComparison.Ordinal))
            throw Unsafe("A path with a colon (an alternate data stream or a device) is not read.");

        foreach (var segment in rest.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
                throw Unsafe("A path that climbs with '..' is not read.");

            if (segment == ".")
                continue;

            if (segment.Length > MaxSegmentChars)
                throw Unsafe("A path segment is too long.");

            if (segment[^1] is '.' or ' ')
                throw Unsafe("A path segment may not end in a dot or a space.");

            var stem = segment.Split('.', 2)[0].TrimEnd(' ');

            if (ReservedDeviceNames.Contains(stem))
                throw Unsafe($"'{segment}' is a reserved device name.");
        }

        string full;

        try
        {
            if (requiredRoot is not null)
            {
                var rootFull = Path.GetFullPath(requiredRoot);
                full = Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(path, rootFull);
                var prefix = rootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

                if (!full.StartsWith(prefix, comparison))
                    throw Unsafe("The path is outside the folder imports are read from.");
            }
            else
            {
                full = Path.GetFullPath(path);
            }
        }
        catch (Exception e) when (e is ArgumentException or PathTooLongException or NotSupportedException or System.Security.SecurityException)
        {
            throw Unsafe("The file path is not valid.");
        }

        return full;
    }

    /// <summary>As <see cref="Canonicalise"/>, then requires an existing regular file that is not a link.</summary>
    public static string CanonicaliseExistingFile(string path, string? requiredRoot = null)
    {
        var full = Canonicalise(path, requiredRoot);
        var info = new FileInfo(full);

        if (!info.Exists)
            throw new ImportRefusedException(ImportRefusalCode.NotFound, "The file does not exist.");

        if (info.LinkTarget is not null || info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw Unsafe("A file that is a link is not followed.");

        return full;
    }

    /// <summary>Requires one of the extensions (with the dot, any case).</summary>
    public static void RequireExtension(string path, params string[] allowed)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(allowed);
        var extension = Path.GetExtension(path);

        if (!allowed.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new ImportRefusedException(ImportRefusalCode.Unsupported, $"A '{extension}' file is not read here; expected {string.Join(", ", allowed)}.");
    }

    /// <summary>
    /// The leaf of a file name made safe to show and to store in provenance: no directory, no control or invisible
    /// characters, at most 128 characters, never empty. Never throws.
    /// </summary>
    public static string SafeLeafName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "file";

        var leaf = fileName.Replace('\\', '/');
        leaf = leaf[(leaf.LastIndexOf('/') + 1)..];
        var sb = new StringBuilder(leaf.Length);

        foreach (var c in leaf)
        {
            if (char.IsControl(c) || CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
                continue;

            sb.Append(c);
        }

        var clean = sb.ToString().Trim();

        if (clean.Length > 128)
            clean = clean[..128];

        return clean.Length == 0 ? "file" : clean;
    }

    private static ImportRefusedException Unsafe(string message) => new(ImportRefusalCode.UnsafePath, message);
}
