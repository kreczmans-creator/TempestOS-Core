using System.Runtime.InteropServices;

namespace Tempest.Calc.Import;

/// <summary>
/// The kind of a file system entry found without opening it, so that a named pipe or a device can be refused before an open that would
/// block on it. Uses <c>statx</c> (Linux) or <c>lstat</c> (macOS); a platform or libc that does not offer them answers "unknown", and the
/// caller's other defences (the time limit and the seekable-stream check after the open) still apply. Windows has no such entries here.
/// </summary>
internal static class FileKind
{
    private const int AtFdCwd = -100;
    private const int AtSymlinkNoFollow = 0x100;
    private const uint StatxType = 0x1;
    private const int FormatMask = 0xF000;
    private const int RegularFile = 0x8000;

    /// <summary>Whether the path names a regular file (<see langword="true"/>), something else (<see langword="false"/>), or the kind cannot be told (<see langword="null"/>).</summary>
    public static bool? IsRegularFile(string path)
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                var buffer = new byte[256];

                if (statx(AtFdCwd, path, AtSymlinkNoFollow, StatxType, buffer) != 0)
                    return null;

                // stx_mode is a 16-bit field at offset 28 of struct statx on every architecture.
                var mode = BitConverter.ToUInt16(buffer, 28);
                return (mode & FormatMask) == RegularFile;
            }

            if (OperatingSystem.IsMacOS())
            {
                var buffer = new byte[512];

                if (lstat(path, buffer) != 0)
                    return null;

                // st_mode is a 16-bit field at offset 4 of struct stat on macOS (x86-64 and arm64).
                var mode = BitConverter.ToUInt16(buffer, 4);
                return (mode & FormatMask) == RegularFile;
            }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or MarshalDirectiveException or BadImageFormatException)
        {
            return null;
        }

        return null;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("libc", EntryPoint = "statx", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern int statx(int dirfd, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags, uint mask, byte[] buffer);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("libc", EntryPoint = "lstat", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern int lstat([MarshalAs(UnmanagedType.LPUTF8Str)] string path, byte[] buffer);
}
