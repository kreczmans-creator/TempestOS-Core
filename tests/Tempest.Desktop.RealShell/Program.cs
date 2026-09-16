using System.Globalization;
using Avalonia;

namespace Tempest.Desktop.RealShell;

/// <summary>
/// WP 21.5C (Linux variant) - the real-shell acceptance journey.
///
/// <para>
/// The runner starts the shipped application in-process, on the main
/// thread, through <see cref="Tempest.Desktop.Program.BuildAvaloniaApp"/>
/// and <c>StartWithClassicDesktopLifetime</c> - the identical start-up path
/// a user gets, with the real <c>App</c>, the real <c>WorkspaceHost</c>,
/// the real X11 platform and the real Skia renderer. A background thread
/// then drives that running application the way a person would: it reads
/// the live visual tree only to *find* controls and to *assert* what is on
/// screen, and sends every click, keystroke and character through the
/// operating system with <c>xdotool</c>.
/// </para>
/// </summary>
public static class Program
{
    private const string WindowTitleFragment = "TempestOS";

    /// <summary>Process entry point.</summary>
    public static int Main(string[] args)
    {
        var display = Argument(args, "--display") ?? Environment.GetEnvironmentVariable("DISPLAY") ?? string.Empty;
        var root = Argument(args, "--root");
        var outputDirectory = Argument(args, "--out");
        var mode = Argument(args, "--mode") ?? "journey";

        if (root is null || outputDirectory is null)
        {
            Console.Error.WriteLine("usage: Tempest.Desktop.RealShell --root <persistence root> --out <output dir> [--display :N] [--mode journey|verify|drive]");
            return 2;
        }

        if (!OperatingSystem.IsLinux())
        {
            Console.Error.WriteLine("This runner drives the application through real X11 input (xdotool) and is Linux-only. Nothing was run.");
            return 3;
        }

        // The application's own X11 backend is native code: it reads
        // DISPLAY out of the real process environment, which a managed
        // `Environment.SetEnvironmentVariable` on Unix does not touch. So
        // DISPLAY must already be exported by whoever starts this runner
        // (scripts/run-realshell-linux.sh does); `--display` only names the
        // same display for the input tools.
        if (display.Length == 0)
        {
            Console.Error.WriteLine("No X display: export DISPLAY=:N before starting this runner. Nothing was run.");
            return 3;
        }

        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outputDirectory);
        OsInput.UseDisplay(display);

        var journal = new Journal(outputDirectory, mode);

        var driver = new Thread(() => Drive(journal, mode, root))
        {
            IsBackground = true,
            Name = "real-shell journey",
        };
        driver.Start();

        // The real application, on the real main thread, exactly as shipped.
        // `VelopackApp.Build().Run()` - the one line `Tempest.Desktop.Program.Main`
        // runs before this - is deliberately not called: it exists only for an
        // installed run's own install/update hooks and would tell this process
        // "not installed" either way. That is the single, disclosed difference
        // between this start-up and a user's.
        Tempest.Desktop.Program.BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(["--persistence-root", root]);

        journal.Note("The application's own process loop returned; the window is closed.");
        journal.Write(root);

        Console.WriteLine(journal.Summary());
        return journal.Failed ? 1 : 0;
    }

    private static void Drive(Journal journal, string mode, string root)
    {
        try
        {
            if (!WaitForTheApplication(journal))
            {
                journal.Fail("startup", "The application never presented a window this runner could find.");
                Shutdown();
                return;
            }

            switch (mode)
            {
                case "drive":
                    Interactive.Run();
                    break;
                case "verify":
                    VerifyJourney.Run(journal);
                    VerifyJourney.VerifyStore(journal, root);
                    OsInput.CloseWindow();
                    break;
                default:
                    MainJourney.Run(journal);
                    break;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            journal.Fail("runner", $"The runner itself threw: {exception.GetType().Name}: {exception.Message}");
            journal.Note(exception.ToString());
        }
        finally
        {
            // The report is written here, on the driver thread, *before*
            // the process is asked to end: a failure during the
            // application's own shutdown must not cost the evidence for
            // everything that came before it. Main writes it again once
            // the loop returns, which simply overwrites it with the same
            // rows plus the closing note.
            try
            {
                journal.Write(root);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"The journal could not be written: {exception.Message}");
            }

            Shutdown();
        }
    }

    /// <summary>
    /// Waits for the real window - both on the X server (so input can reach
    /// it) and in the visual tree (so the shell has actually composed).
    /// </summary>
    private static bool WaitForTheApplication(Journal journal)
    {
        var deadline = DateTime.UtcNow.AddMinutes(3);

        while (DateTime.UtcNow < deadline && Ui.Lifetime?.MainWindow is null)
            Thread.Sleep(200);

        if (Ui.Lifetime?.MainWindow is null)
            return false;

        while (DateTime.UtcNow < deadline && !OsInput.TryFindWindow(WindowTitleFragment))
            Thread.Sleep(300);

        if (OsInput.WindowId.Length == 0)
            return false;

        OsInput.Focus();

        // The rail is the last thing composed on a cold start; when its own
        // Home entry is hit-testable the shell is genuinely up.
        var composed = Ui.WaitUntil(() => Ui.ByName("Home") is { HasArea: true }, 120_000);
        journal.Note($"Window {OsInput.WindowId} on display {OsInput.Display}; shell composed: {composed}.");
        Thread.Sleep(700);
        return composed;
    }

    private static void Shutdown()
    {
        try
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (Ui.Lifetime is { } lifetime)
                    lifetime.Shutdown();
            });
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Environment.Exit(1);
        }
    }

    internal static string? Argument(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal))
                return args[index + 1];
        }

        return null;
    }

    internal static string Now() => DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}
