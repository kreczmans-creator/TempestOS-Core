using System.Globalization;
using System.Text;
using Avalonia.Threading;

namespace Tempest.Desktop.Diagnostics;

/// <summary>
/// The last-resort crash record (`WP-Z4` Stage 28) — a single append-only
/// text file, beside the executable, that captures any exception which
/// reaches an unhandled boundary.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> The platform's only log sink is
/// <see cref="Tempest.Core.Logging.ConsoleLogSink"/>, and
/// <c>Tempest.Desktop</c> ships as a <c>WinExe</c> — which has no console.
/// Launched from Explorer, or as the built <c>.exe</c>, every unhandled
/// exception went to a stderr nobody was reading, so the `WP-Z4` Stage 27
/// startup crash presented as "the window appears and vanishes" with no
/// evidence anywhere. That cost days of diagnosis. This class makes the
/// next one a one-line lookup.
/// </para>
/// <para>
/// <b>Deliberately tiny.</b> No logging framework, no configuration, no
/// dependency on the Runtime Host — a crash during host construction must
/// still be recorded, so this can take no dependency on anything that
/// might itself be the thing that failed. It is a static writer over
/// <see cref="File.AppendAllText(string, string)"/> and nothing else.
/// </para>
/// <para>
/// <b>It never changes exception semantics.</b> Every hook below records
/// and returns; none marks an exception handled, and none keeps a doomed
/// process alive. A crash that used to terminate the application still
/// terminates it — it now leaves a trace on the way out.
/// </para>
/// </remarks>
internal static class CrashLog
{
    /// <summary>The file name written beside the executable, inside <see cref="LogFolderName"/>.</summary>
    public const string FileName = "tempestos-crash.log";

    /// <summary>The folder, relative to <see cref="AppContext.BaseDirectory"/>, the record is written into.</summary>
    public const string LogFolderName = "logs";

    private static readonly object Gate = new();
    private static bool _installed;

    /// <summary>
    /// The full path of the crash record — beside the executable
    /// (<see cref="AppContext.BaseDirectory"/>), the same fixed-convention
    /// anchor <c>Tempest.Core</c> already uses for <c>Plugins</c>,
    /// <c>TrustedPublishers</c> and the licence file, rather than the
    /// working directory (which `PHYSICAL_REVIEW.md` §4 documents as
    /// varying between <c>dotnet run</c> and a double-click).
    /// </summary>
    public static string FilePath { get; } = BuildPath();

    /// <summary>
    /// Subscribes every unhandled-exception boundary a desktop process
    /// has. Safe to call more than once — only the first call subscribes.
    /// </summary>
    /// <remarks>
    /// Three boundaries, because they catch genuinely different things:
    /// <list type="bullet">
    /// <item><description><see cref="Dispatcher.UnhandledException"/> — an exception on the Avalonia UI thread, which is where an <c>async void</c> event handler's throw surfaces. This is the boundary the Stage 27 crash crossed, and the one the process previously had no hook on at all.</description></item>
    /// <item><description><see cref="AppDomain.UnhandledException"/> — anything fatal on any other thread.</description></item>
    /// <item><description><see cref="TaskScheduler.UnobservedTaskException"/> — a faulted fire-and-forget <see cref="Task"/> collected without ever being awaited.</description></item>
    /// </list>
    /// </remarks>
    public static void Install()
    {
        lock (Gate)
        {
            if (_installed)
                return;

            _installed = true;
        }

        try
        {
            // The UI thread. An `async void` handler (Window.Opened, a
            // Click handler) that throws arrives here and nowhere else.
            // Deliberately does NOT set e.Handled: the exception keeps
            // exactly the semantics it had before this class existed.
            Dispatcher.UIThread.UnhandledException += (_, e) => Record("Dispatcher.UnhandledException", e.Exception);
        }
        catch
        {
            // An Avalonia build without the event, or a dispatcher not yet
            // constructed. Recording is best-effort by definition.
        }

        try
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) => Record("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        }
        catch
        {
        }

        try
        {
            // Note: App.cs also subscribes this boundary to show the user a
            // real dialog and calls SetObserved there. Both handlers run;
            // this one only writes the record.
            TaskScheduler.UnobservedTaskException += (_, e) => Record("TaskScheduler.UnobservedTaskException", e.Exception);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Appends one record: UTC and local timestamp, the boundary it came
    /// through, the exception type, its message, and the full stack trace
    /// — plus the same for every inner exception.
    /// </summary>
    /// <remarks>
    /// Every failure mode of writing the record is swallowed. A crash
    /// handler that throws turns a diagnosable crash into an undiagnosable
    /// one, so this method's own contract is that it cannot fail: an
    /// unwritable folder, a locked file, a denied path or a null exception
    /// all end in a silent return.
    /// </remarks>
    public static void Record(string source, Exception? exception)
    {
        try
        {
            if (exception is null)
                return;

            var text = Format(source, exception);

            lock (Gate)
            {
                var directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.AppendAllText(FilePath, text, Encoding.UTF8);
            }

            // Also to stderr, so a terminal launch shows it immediately
            // without anyone needing to know this file exists.
            Console.Error.WriteLine(text);
        }
        catch
        {
            // Never let the recorder become the failure.
        }
    }

    /// <summary>Builds one record's text. Pure, so a test can assert the shape without touching the file system.</summary>
    internal static string Format(string source, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var builder = new StringBuilder();
        builder.Append("---- TempestOS crash ").Append('-', 48).AppendLine();
        builder.Append("Timestamp (UTC)  : ").AppendLine(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
        builder.Append("Timestamp (local): ").AppendLine(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture));
        builder.Append("Boundary         : ").AppendLine(source ?? "(unknown)");
        builder.Append("Process          : ").AppendLine(Environment.ProcessPath ?? "(unknown)");
        builder.Append("OS               : ").AppendLine(Environment.OSVersion.VersionString);

        var depth = 0;
        for (var current = exception; current is not null; current = current.InnerException, depth++)
        {
            var indent = depth == 0 ? string.Empty : new string(' ', depth * 2);
            builder.AppendLine();
            builder.Append(indent).Append(depth == 0 ? "Exception type   : " : "Inner exception  : ").AppendLine(current.GetType().FullName);
            builder.Append(indent).Append("Message          : ").AppendLine(current.Message);
            builder.Append(indent).Append("Stack trace      :").AppendLine();
            builder.AppendLine(current.StackTrace ?? $"{indent}(no stack trace)");

            // A pathological exception chain must not produce an unbounded
            // file, so the chain is walked to a fixed depth.
            if (depth >= 8)
            {
                builder.Append(indent).AppendLine("(inner exception chain truncated)");
                break;
            }
        }

        builder.AppendLine();
        return builder.ToString();
    }

    private static string BuildPath()
    {
        try
        {
            return Path.Combine(AppContext.BaseDirectory, LogFolderName, FileName);
        }
        catch
        {
            return FileName;
        }
    }
}
