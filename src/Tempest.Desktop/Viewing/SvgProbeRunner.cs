using System.Diagnostics;
using System.Globalization;
using System.Text;
using SkiaSharp;

namespace Tempest.Desktop.Viewing;

/// <summary>
/// The process entry the bounded SVG probes run in (DB-08 review): <c>Tempest.Desktop.dll --svg-probe &lt;file.svg&gt; [--page-source] [--raster &lt;edge&gt;]</c>
/// reads one hostile document through <see cref="SvgPictureBuilder"/> (after <see cref="SvgMarkupSanitiser"/> with <c>--page-source</c>, as the
/// viewer does) and draws the picture into a bitmap of the given edge, then exits: 0 when both succeeded, <see cref="BuildFailed"/> when the
/// build threw (a refusal, which some probes expect), <see cref="DrawFailed"/> when the draw threw. Nothing of Avalonia is started.
/// </summary>
/// <remarks>
/// A probe runs in a process of its own, never in a test host, because the classes it exercises end in a native abort or an out-of-memory
/// when a cap is missing — outcomes a test can assert only from outside, as an exit code and a wall time. The test class
/// <c>SvgBoundedProbeTests</c> (opt-in, <c>TEMPEST_BOUNDED_PROBES=1</c>) writes each probe file, starts this entry under a heap cap and, on
/// Linux, a <c>ulimit -v</c>, and asserts the exit code, the wall time and the peak working set this entry prints on its last line.
/// </remarks>
public static class SvgProbeRunner
{
    public const string Switch = "--svg-probe";
    public const int BuildFailed = 2;
    public const int DrawFailed = 3;
    public const int BadArguments = 64;

    /// <summary>The viewer's own raster ceiling (<c>SvgDocumentPageSource.MaxRasterEdge</c>, restated here so this entry stays free of that class's platform attributes).</summary>
    public const int MaxRasterEdge = 8000;

    /// <summary>The last line of the output: the process's peak working set in MB, for the caller's memory bound.</summary>
    public const string PeakPrefix = "peak-working-set-mb=";

    /// <summary>Runs the probe named by <paramref name="args"/> (the switch first) and returns the exit code.</summary>
    public static int Run(string[] args, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);

        if (args.Length < 2 || args[0] != Switch)
        {
            output.WriteLine($"usage: {Switch} <file.svg> [--page-source] [--raster <edge>]");
            return BadArguments;
        }

        var path = args[1];
        var pageSource = false;
        var raster = 0;

        for (var i = 2; i < args.Length; i++)
        {
            if (args[i] == "--page-source")
                pageSource = true;
            else if (args[i] == "--raster" && i + 1 < args.Length && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var edge) && edge > 0)
                raster = Math.Min(edge, MaxRasterEdge);
        }

        var watch = Stopwatch.StartNew();
        SvgPictureBuilder.SvgPicture picture;

        try
        {
            var markup = File.ReadAllText(path, Encoding.UTF8);
            picture = SvgPictureBuilder.Build(pageSource ? SvgMarkupSanitiser.Sanitise(markup) : markup);
            output.WriteLine($"build ok {watch.Elapsed.TotalSeconds:F3} s; size {picture.Size.Width:F0} x {picture.Size.Height:F0}; elements {picture.Statistics.Elements}; not shown: {string.Join(", ", picture.NotShown)}");
        }
        catch (Exception ex)
        {
            output.WriteLine($"build threw {ex.GetType().Name} after {watch.Elapsed.TotalSeconds:F3} s: {ex.Message}");
            WritePeak(output);
            return BuildFailed;
        }

        using (picture)
        {
            if (raster > 0)
            {
                try
                {
                    // The viewer at its deepest zoom: the longest edge of the page is drawn at the raster edge asked for, scaling up as well as down.
                    var longest = Math.Max(picture.Size.Width, picture.Size.Height);
                    var scale = longest > 0 ? raster / longest : 1f;
                    var width = Math.Max(1, (int)Math.Round(picture.Size.Width * scale));
                    var height = Math.Max(1, (int)Math.Round(picture.Size.Height * scale));
                    watch.Restart();

                    using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                    using (var canvas = new SKCanvas(bitmap))
                    {
                        canvas.Clear(SKColors.Transparent);
                        canvas.Scale(scale);
                        canvas.DrawPicture(picture.Picture);
                    }

                    output.WriteLine($"draw ok {watch.Elapsed.TotalSeconds:F3} s at {width} x {height}");
                }
                catch (Exception ex)
                {
                    output.WriteLine($"draw threw {ex.GetType().Name} after {watch.Elapsed.TotalSeconds:F3} s: {ex.Message}");
                    WritePeak(output);
                    return DrawFailed;
                }
            }
        }

        WritePeak(output);
        return 0;
    }

    /// <summary>Writes the peak working set: <c>VmHWM</c> from <c>/proc/self/status</c> on Linux, the process's own figure elsewhere.</summary>
    private static void WritePeak(TextWriter output)
    {
        long megabytes = -1;

        try
        {
            if (OperatingSystem.IsLinux() && File.Exists("/proc/self/status"))
            {
                foreach (var line in File.ReadLines("/proc/self/status"))
                {
                    if (line.StartsWith("VmHWM:", StringComparison.Ordinal))
                    {
                        var digits = new string(line.Where(char.IsAsciiDigit).ToArray());
                        megabytes = long.Parse(digits, CultureInfo.InvariantCulture) / 1024;
                        break;
                    }
                }
            }

            if (megabytes < 0)
                megabytes = Process.GetCurrentProcess().PeakWorkingSet64 / (1024 * 1024);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or InvalidOperationException)
        {
            // A platform that does not say leaves the figure at -1, which the caller treats as unknown.
        }

        output.WriteLine($"{PeakPrefix}{megabytes.ToString(CultureInfo.InvariantCulture)}");
    }
}
