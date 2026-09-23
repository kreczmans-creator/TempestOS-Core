using System.Diagnostics;
using System.Globalization;

namespace Tempest.Desktop.RealShell;

/// <summary>
/// Every input this runner gives the application goes through the operating
/// system: <c>xdotool</c> moves the real pointer, presses the real buttons
/// and sends real key events to the X server, and <c>import</c> reads the
/// real framebuffer back. Nothing here reaches into Avalonia - the
/// application receives genuine X11 events and cannot tell this runner from
/// a person at the keyboard.
/// </summary>
internal static class OsInput
{
    /// <summary>The X display this runner drives, e.g. <c>:102</c>.</summary>
    internal static string Display { get; private set; } = string.Empty;

    /// <summary>The X window id of the application's own main window, once found.</summary>
    internal static string WindowId { get; private set; } = string.Empty;

    internal static bool Available => OperatingSystem.IsLinux() && Display.Length > 0;

    internal static void UseDisplay(string display) => Display = display;

    /// <summary>
    /// Finds the application's own top-level X window by title. Returns
    /// <see langword="false"/> until the window actually exists on the
    /// server - the runner polls this rather than assuming.
    /// </summary>
    internal static bool TryFindWindow(string titleFragment)
    {
        var output = Run("xdotool", $"search --name \"{titleFragment}\"");
        var first = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(line => line.All(char.IsDigit));

        if (first is null)
            return false;

        WindowId = first;
        return true;
    }

    /// <summary>Gives the application's own window the X input focus (there is no window manager on a bare Xvfb display to do it).</summary>
    internal static void Focus()
    {
        if (WindowId.Length == 0)
            return;

        Run("xdotool", $"windowfocus {WindowId}");
    }

    internal static void MoveTo(int x, int y) =>
        Run("xdotool", string.Create(CultureInfo.InvariantCulture, $"mousemove {x} {y}"));

    internal static void Click(int x, int y, int button = 1)
    {
        Run("xdotool", string.Create(CultureInfo.InvariantCulture, $"mousemove {x} {y}"));
        Thread.Sleep(90);
        Run("xdotool", string.Create(CultureInfo.InvariantCulture, $"click {button}"));
    }

    /// <summary>Turns the real mouse wheel over a point, <paramref name="times"/> notches at once.</summary>
    internal static void Scroll(int x, int y, int button, int times)
    {
        Run("xdotool", string.Create(CultureInfo.InvariantCulture, $"mousemove {x} {y}"));
        Thread.Sleep(60);
        Run("xdotool", string.Create(CultureInfo.InvariantCulture, $"click --repeat {Math.Clamp(times, 1, 30)} --delay 25 {button}"));
    }

    internal static void DoubleClick(int x, int y)
    {
        Run("xdotool", string.Create(CultureInfo.InvariantCulture, $"mousemove {x} {y}"));
        Thread.Sleep(90);
        Run("xdotool", "click --repeat 2 --delay 80 1");
    }

    /// <summary>
    /// Types text as real key events. The window is deliberately *not*
    /// re-focused here: on X11 a fresh <c>windowfocus</c> makes the toolkit
    /// restore focus to whatever it last stored, which silently redirects
    /// the typing into the previous field. The window is focused once, when
    /// it is found, and again after an operating-system dialog takes focus
    /// away.
    /// </summary>
    internal static void Type(string text) =>
        Run("xdotool", $"type --delay 26 -- {Quote(text)}");

    /// <summary>Presses a key or chord in xdotool's own syntax, e.g. <c>ctrl+k</c>, <c>Return</c>, <c>ctrl+shift+m</c>.</summary>
    internal static void Key(string keys) =>
        Run("xdotool", $"key --delay 40 {keys}");

    /// <summary>
    /// Clears a focused text box the way a person does - select all, then
    /// type over it.
    /// </summary>
    internal static void ClearAndType(string text)
    {
        Run("xdotool", "key --delay 40 ctrl+a");
        Thread.Sleep(60);
        Run("xdotool", "key --delay 40 Delete");
        Thread.Sleep(60);
        Run("xdotool", $"type --delay 26 -- {Quote(text)}");
    }

    /// <summary>
    /// Closes the window the way a title-bar close button does: the X11
    /// <c>WM_DELETE_WINDOW</c> client message, which is exactly what
    /// <c>xdotool windowclose</c> sends and exactly what Avalonia turns
    /// into <c>Window.Closing</c>. Alt+F4 is a *window-manager* binding,
    /// and a bare Xvfb display runs no window manager to honour it - see
    /// the report for why this substitution is the honest one.
    /// </summary>
    internal static void CloseWindow()
    {
        if (WindowId.Length == 0)
            return;

        Run("xdotool", $"windowclose {WindowId}");
    }

    /// <summary>
    /// Drives an operating-system file dialog - a real GTK window this
    /// application does not own - the way a person does with the keyboard:
    /// Ctrl+L opens its own location bar, the path is typed, Return
    /// accepts. Real input into a real OS dialog, not a stubbed picker.
    /// </summary>
    internal static bool DriveFileDialog(string titleFragment, string path, int timeoutMs = 20_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var dialogId = string.Empty;

        while (DateTime.UtcNow < deadline)
        {
            var output = Run("xdotool", $"search --name \"{titleFragment}\"");
            dialogId = output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(line => line.All(char.IsDigit)) ?? string.Empty;

            if (dialogId.Length > 0)
                break;

            Thread.Sleep(300);
        }

        if (dialogId.Length == 0)
            return false;

        Run("xdotool", $"windowfocus {dialogId}");
        Thread.Sleep(400);
        Run("xdotool", $"key --window {dialogId} --delay 40 ctrl+l");
        Thread.Sleep(600);
        Run("xdotool", $"type --delay 24 -- {Quote(path)}");
        Thread.Sleep(500);
        Run("xdotool", "key --delay 40 Return");
        Thread.Sleep(1_500);

        // The dialog is gone when the application owns the input again.
        Focus();
        return true;
    }

    /// <summary>
    /// Drives an operating-system *save* dialog: its name field already has
    /// focus, so selecting all of it and typing a full path over it, then
    /// Return, is what a person does. Real input into a real OS dialog.
    /// </summary>
    internal static bool DriveSaveDialog(string titleFragment, string path, int timeoutMs = 20_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var dialogId = string.Empty;

        while (DateTime.UtcNow < deadline)
        {
            dialogId = Run("xdotool", $"search --name \"{titleFragment}\"")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(line => line.All(char.IsDigit)) ?? string.Empty;

            if (dialogId.Length > 0)
                break;

            Thread.Sleep(300);
        }

        if (dialogId.Length == 0)
            return false;

        Run("xdotool", $"windowfocus {dialogId}");
        Thread.Sleep(400);
        Run("xdotool", "key --delay 40 ctrl+a");
        Thread.Sleep(200);
        Run("xdotool", $"type --delay 24 -- {Quote(path)}");
        Thread.Sleep(500);
        Run("xdotool", "key --delay 40 Return");
        Thread.Sleep(2_000);
        Focus();
        return true;
    }

    /// <summary>True while a window whose title carries this fragment is on the X server.</summary>
    internal static bool WindowExists(string titleFragment) =>
        Run("xdotool", $"search --name \"{titleFragment}\"")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(line => line.All(char.IsDigit));

    /// <summary>Reads the whole root window back off the X server as a PNG.</summary>
    internal static bool Screenshot(string path)
    {
        if (!Available)
            return false;

        Run("import", $"-window root {Quote(path)}");
        return File.Exists(path);
    }

    private static string Quote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    /// <summary>
    /// Runs one external tool with the runner's own DISPLAY. Deliberately
    /// forgiving: a missing tool or a non-zero exit is reported as empty
    /// output rather than thrown, so a step records "Unknown" instead of
    /// tearing the whole journey down.
    /// </summary>
    private static string Run(string fileName, string arguments)
    {
        if (!OperatingSystem.IsLinux())
            return string.Empty;

        try
        {
            var start = new ProcessStartInfo("/bin/sh")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add($"{fileName} {arguments}");
            start.Environment["DISPLAY"] = Display;

            using var process = Process.Start(start);
            if (process is null)
                return string.Empty;

            var output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit(20_000);
            return output;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return string.Empty;
        }
    }
}
