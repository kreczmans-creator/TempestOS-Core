using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Tempest.Desktop.Tests.Soak;

/// <summary>What one pass of one scenario cost, and what it left behind.</summary>
/// <param name="Seconds">Wall time of the scenario itself (the host start-up and shut-down are separate).</param>
/// <param name="HostStartSeconds">Wall time to start the host (the real composition: store, seeding, catalogue).</param>
/// <param name="HeapBeforeBytes">Managed heap after a forced collection, with the host running and nothing yet done.</param>
/// <param name="HeapAfterBytes">Managed heap after a forced collection, with the host still running (what the application holds for this data).</param>
/// <param name="HeapAfterDisposeBytes">Managed heap after a forced collection once the host is disposed (what is left that nothing owns).</param>
/// <param name="PeakManagedBytes">The largest managed heap seen while the scenario ran (sampled, not collected).</param>
/// <param name="PeakWorkingSetBytes">The largest working set seen while the scenario ran (sampled).</param>
/// <param name="ProcessPeakWorkingSetBytes">The process's own peak working set so far (all passes before it included).</param>
/// <param name="DbBytes">tempest.db at the end of the scenario.</param>
/// <param name="WalBytes">tempest.db-wal at the end of the scenario.</param>
/// <param name="DbBytesAfterShutdown">tempest.db once the host is shut down (the write-ahead log folded in).</param>
/// <param name="HandlesBefore">Open file handles of the process with the host running, before the scenario.</param>
/// <param name="HandlesAfter">The same, after it.</param>
/// <param name="SqliteHandlesBefore">Handles open on the database's own files (the pooled connections), before the scenario.</param>
/// <param name="SqliteHandlesAfter">The same, after it.</param>
/// <param name="HandlesAfterDispose">Open file handles of the process once the host is disposed; the second pass's is compared with the first's (the first also holds what loading the application's assemblies opens, once).</param>
/// <param name="DatabaseBytesBefore">tempest.db and its write-ahead log before the scenario: the seeded database every pass starts from.</param>
/// <param name="SqliteHandlesAfterDispose">Handles still open on the database's own files once the host is disposed: none may be.</param>
/// <param name="HandlesBaseline">Open file handles of the process before the host was built.</param>
/// <param name="InHost">The settled heap at the halfway point of the scenario's items and after the last, within this one host; <see langword="null"/> where the scenario has too few items to say.</param>
/// <param name="ItemsTotal">The items the scenario did (0 when it did not report them).</param>
/// <param name="Gen2Collections">Generation 2 collections the scenario caused.</param>
/// <param name="AllocatedBytes">Bytes the scenario allocated in total.</param>
internal sealed record SoakPass(
    double Seconds,
    double HostStartSeconds,
    long HeapBeforeBytes,
    long HeapAfterBytes,
    long HeapAfterDisposeBytes,
    long PeakManagedBytes,
    long PeakWorkingSetBytes,
    long ProcessPeakWorkingSetBytes,
    long DbBytes,
    long WalBytes,
    long DbBytesAfterShutdown,
    int HandlesBefore,
    int HandlesAfter,
    int SqliteHandlesBefore,
    int SqliteHandlesAfter,
    int HandlesAfterDispose,
    int HandlesBaseline,
    int Gen2Collections,
    long AllocatedBytes,
    int SqliteHandlesAfterDispose = 0,
    long DatabaseBytesBefore = 0,
    SoakInHostSample? InHost = null,
    int ItemsTotal = 0);

/// <summary>The probes a soak reads: managed heap after a full collection, working set, open handles and the database's size.</summary>
internal static class SoakProbe
{
    /// <summary>The managed heap after collecting everything that can be collected (several rounds, so finalisers and what they free are settled too).</summary>
    public static long SettledHeap()
    {
        for (var i = 0; i < 3; i++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }

        return GC.GetTotalMemory(forceFullCollection: true);
    }

    /// <summary>
    /// Open file handles of this process: the descriptors under /proc/self/fd on Linux, the File handles of its handle table on Windows
    /// (<see cref="WindowsFileHandles"/>), the handle count elsewhere.
    /// </summary>
    public static int OpenHandles()
    {
        if (OperatingSystem.IsWindows() && WindowsFileHandles.Count() is { } files)
            return files;

        if (OperatingSystem.IsLinux() && Directory.Exists("/proc/self/fd"))
        {
            try
            {
                // One of the entries is the descriptor the enumeration itself holds; it is there before and after alike.
                return Directory.GetFileSystemEntries("/proc/self/fd").Length;
            }
            catch (IOException)
            {
            }
        }

        using var process = Process.GetCurrentProcess();
        return process.HandleCount;
    }

    /// <summary>
    /// Handles open on the files under <paramref name="root"/> (tempest.db, its write-ahead log and shared-memory file, the lock file): each pooled
    /// SQLite connection holds some of them, so this is the connection count's footprint. -1 where the platform cannot say.
    /// </summary>
    public static int HandlesUnder(string root)
    {
        if (!OperatingSystem.IsLinux() || !Directory.Exists("/proc/self/fd"))
            return -1;

        var count = 0;

        try
        {
            foreach (var entry in Directory.GetFileSystemEntries("/proc/self/fd"))
            {
                string? target;

                try
                {
                    target = new FileInfo(entry).LinkTarget;
                }
                catch (IOException)
                {
                    continue;
                }

                if (target is not null && target.StartsWith(root, StringComparison.Ordinal))
                    count++;
            }
        }
        catch (IOException)
        {
            return -1;
        }

        return count;
    }

    /// <summary>
    /// What the database holds, counted in its own pages by a second, short-lived connection (the write-ahead log is part of what it sees): the
    /// pages in use and the pages on its free list (deleted rows' pages, reused before the file grows again), in bytes. The file's size cannot say this:
    /// SQLite never shortens its file when rows are deleted, and the write-ahead log is folded into it at moments the soak does not choose.
    /// </summary>
    public static (long UsedBytes, long FreeBytes) LogicalDatabase(string path)
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = path, Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
        connection.Open();

        long Pragma(string name)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA " + name + ";";
            return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }

        var pageSize = Pragma("page_size");
        var pages = Pragma("page_count");
        var free = Pragma("freelist_count");
        return ((pages - free) * pageSize, free * pageSize);
    }

    public static long FileBytes(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;

    /// <summary>A one-line description of the machine, for the report.</summary>
    public static string MachineDescription()
    {
        var info = GC.GetGCMemoryInfo();
        var cpu = ReadCpuModel();
        return $"{RuntimeInformation.OSDescription}; {RuntimeInformation.OSArchitecture}; {Environment.ProcessorCount} logical processors{(cpu is null ? string.Empty : $" ({cpu})")}; "
            + $"{info.TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024):0.0} GiB available to the runtime; {RuntimeInformation.FrameworkDescription}; "
            + $"Server GC {(System.Runtime.GCSettings.IsServerGC ? "on" : "off")}";
    }

    private static string? ReadCpuModel()
    {
        try
        {
            if (File.Exists("/proc/cpuinfo"))
                return File.ReadLines("/proc/cpuinfo").FirstOrDefault(l => l.StartsWith("model name", StringComparison.Ordinal))?.Split(':', 2)[1].Trim();
        }
        catch (IOException)
        {
        }

        return null;
    }
}

/// <summary>
/// Samples the working set and the managed heap on a background thread while a scenario runs, so the peak is the highest value seen and not
/// the value at the end. The sampling interval is short against a scenario's length and long against its cost.
/// </summary>
internal sealed class SoakSampler : IDisposable
{
    private readonly Thread _thread;
    private volatile bool _stop;
    private long _peakWorkingSet;
    private long _peakManaged;

    public SoakSampler(TimeSpan? interval = null)
    {
        var wait = interval ?? TimeSpan.FromMilliseconds(100);
        _peakWorkingSet = Environment.WorkingSet;
        _peakManaged = GC.GetTotalMemory(forceFullCollection: false);
        _thread = new Thread(() =>
        {
            while (!_stop)
            {
                Observe();
                Thread.Sleep(wait);
            }
        })
        { IsBackground = true, Name = "soak-sampler" };
        _thread.Start();
    }

    public long PeakWorkingSetBytes => Interlocked.Read(ref _peakWorkingSet);

    public long PeakManagedBytes => Interlocked.Read(ref _peakManaged);

    private void Observe()
    {
        var workingSet = Environment.WorkingSet;
        var managed = GC.GetTotalMemory(forceFullCollection: false);

        long seen;

        while (workingSet > (seen = Interlocked.Read(ref _peakWorkingSet)))
            Interlocked.CompareExchange(ref _peakWorkingSet, workingSet, seen);

        while (managed > (seen = Interlocked.Read(ref _peakManaged)))
            Interlocked.CompareExchange(ref _peakManaged, managed, seen);
    }

    /// <summary>Takes one last sample and stops the thread.</summary>
    public void Stop()
    {
        Observe();
        _stop = true;
        _thread.Join(TimeSpan.FromSeconds(5));
    }

    public void Dispose() => Stop();
}

/// <summary>
/// The open file handles of this process on Windows: the entries of its handle table whose object type is File, which covers files, pipes,
/// sockets and devices, as /proc/self/fd does on Linux. <see cref="Process.HandleCount"/> counts every kernel object instead, among them the
/// handles each thread holds (the thread itself and the events the runtime gives it), so it moves with the thread pool's size: on the Debug
/// Windows leg a pass that ended with a pool thread or two more than the pass before read as a leak of 4 to 6 handles with no file left
/// open (run 40475352930, three soak scenarios). Returns <see langword="null"/> where the table cannot be read, and the caller falls back to
/// the handle count.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
internal static class WindowsFileHandles
{
    /// <summary><c>ProcessHandleInformation</c>: a snapshot of the process's own handle table (Windows 8 and later).</summary>
    private const int ProcessHandleInformation = 51;

    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);

    /// <summary><c>PROCESS_HANDLE_SNAPSHOT_INFORMATION</c> on x64: NumberOfHandles and Reserved, then the entries.</summary>
    private const int HeaderBytes = 16;

    /// <summary><c>PROCESS_HANDLE_TABLE_ENTRY_INFO</c> on x64: HandleValue, HandleCount, PointerCount, GrantedAccess, ObjectTypeIndex, HandleAttributes, Reserved.</summary>
    private const int EntryBytes = 40;

    private const int ObjectTypeIndexOffset = 28;

    private static readonly IntPtr CurrentProcess = new(-1);

    private static int? s_fileTypeIndex;

    /// <summary>The number of File handles this process holds, or <see langword="null"/> where that cannot be read.</summary>
    public static int? Count()
    {
        if (!Environment.Is64BitProcess)
            return null;

        try
        {
            s_fileTypeIndex ??= FileTypeIndex();

            if (s_fileTypeIndex is not { } fileType || Snapshot() is not { } table)
                return null;

            return table.Count(entry => entry.Type == fileType);
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or MarshalDirectiveException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The object type index of File in this boot: the type of a handle this method opens on a file itself.</summary>
    private static int? FileTypeIndex()
    {
        using var stream = new FileStream(typeof(WindowsFileHandles).Assembly.Location, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var handle = (long)stream.SafeFileHandle.DangerousGetHandle();
        return Snapshot()?.Where(entry => entry.Handle == handle).Select(entry => (int?)entry.Type).FirstOrDefault();
    }

    private static List<(long Handle, int Type)>? Snapshot()
    {
        var size = 64 * 1024;

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var buffer = new byte[size];
            var status = NtQueryInformationProcess(CurrentProcess, ProcessHandleInformation, buffer, buffer.Length, out var needed);

            if (status == StatusInfoLengthMismatch)
            {
                size = Math.Max(size * 2, needed + 4096);
                continue;
            }

            if (status != 0)
                return null;

            var count = BitConverter.ToInt64(buffer, 0);

            if (count < 0 || HeaderBytes + count * EntryBytes > buffer.Length)
                return null;

            var entries = new List<(long Handle, int Type)>((int)count);

            for (var i = 0; i < count; i++)
            {
                var offset = HeaderBytes + (i * EntryBytes);
                entries.Add((BitConverter.ToInt64(buffer, offset), BitConverter.ToInt32(buffer, offset + ObjectTypeIndexOffset)));
            }

            return entries;
        }

        return null;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass, byte[] processInformation, int processInformationLength, out int returnLength);
}
