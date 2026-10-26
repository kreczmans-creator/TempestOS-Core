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
/// <param name="SqliteHandlesAfterDispose">Handles still open on the database's own files once the host is disposed: none may be.</param>
/// <param name="HandlesBaseline">Open file handles of the process before the host was built.</param>
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
    int SqliteHandlesAfterDispose = 0);

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

    /// <summary>Open file handles of this process (the descriptors under /proc/self/fd on Linux, the handle count elsewhere).</summary>
    public static int OpenHandles()
    {
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
