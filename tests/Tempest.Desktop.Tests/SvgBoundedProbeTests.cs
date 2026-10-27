using System.Diagnostics;
using System.Globalization;
using System.Text;
using Tempest.Desktop.Viewing;
using Xunit.Abstractions;

namespace Tempest.Desktop.Tests;

/// <summary>
/// DB-08 review: the full-size hostile-SVG probes (the review's <c>svg-probes/README.md</c>, 40 files of up to 30 MB), each run in a
/// process of its own — <c>Tempest.Desktop.dll --svg-probe</c>, see <see cref="SvgProbeRunner"/> — under a 512 MB managed heap cap and, on
/// Linux, a 2 GiB <c>ulimit -v</c>, with the exit code, the wall time and the peak working set asserted against the bound the review set
/// for each once the fixes were in. Opt-in: every test returns at once unless <c>TEMPEST_BOUNDED_PROBES=1</c>, because these files took
/// seconds to minutes, or killed the process, before the fixes, and a test host is no place to find out which still does. CI runs them in
/// their own Linux job (<c>svg-bounded-probes</c> in <c>ci.yml</c>); a developer runs
/// <c>TEMPEST_BOUNDED_PROBES=1 dotnet test tests/Tempest.Desktop.Tests --filter Category=BoundedProbe</c>. On a small machine
/// <c>TEMPEST_BOUNDED_PROBES_ULIMIT_KB</c> and <c>TEMPEST_BOUNDED_PROBES_HEAP_HEX</c> lower the child's address-space and heap limits.
/// </summary>
[Trait("Category", "BoundedProbe")]
public sealed class SvgBoundedProbeTests(ITestOutputHelper output)
{
    /// <summary>Whether the probes run at all.</summary>
    public static bool Enabled => Environment.GetEnvironmentVariable("TEMPEST_BOUNDED_PROBES") == "1";

    /// <summary>The longest any one probe process may live before it is killed and counted as a hang (exit 124), as the review's harness did.</summary>
    public static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(120 * DesktopTestHelpers.TimeoutFactor);

    /// <summary>Added to every probe's wall-time bound: a .NET process starting and loading SkiaSharp.</summary>
    private const double StartupAllowanceSeconds = 2.0;

    private const string Ns = "xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\"";
    private const string NestedSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><image href=\"file:///etc/hostname\"/></svg>";

    /// <summary>One probe: how its file is made, how it is run, and the bound it must stay within.</summary>
    /// <param name="Exits">The exit codes the review allows: 0 (read and drawn) or <see cref="SvgProbeRunner.BuildFailed"/> (refused in a sentence).</param>
    /// <param name="MaxSeconds">The review's wall-time bound for the read (and the draw), before the start-up allowance and the timeout factor.</param>
    /// <param name="MaxPeakMb">The review's peak-working-set bound, or 0 for none.</param>
    /// <param name="Raster">The edge of the bitmap the picture is drawn into after the read, or 0 to read only.</param>
    /// <param name="PageSource">Whether the file goes through the sanitiser first, as an attachment does.</param>
    public sealed record Probe(string Name, Func<string> Markup, int[] Exits, double MaxSeconds, int MaxPeakMb = 0, int Raster = 0, bool PageSource = false);

    private static readonly int[] Ok = [0];
    private static readonly int[] Refused = [SvgProbeRunner.BuildFailed];
    private static readonly int[] OkOrRefused = [0, SvgProbeRunner.BuildFailed];

    private static string Svg(string body, string size = "width=\"100\" height=\"100\"") => $"<svg {Ns} {size}>{body}</svg>";

    private static string Repeat(string text, int count) => string.Concat(Enumerable.Repeat(text, count));

    private static string Gradient(int stops, int rects, bool css = false)
    {
        var sb = new StringBuilder($"<svg {Ns} width=\"100\" height=\"100\"><defs><linearGradient id=\"g\">");

        for (var i = 0; i < stops; i++)
            sb.Append(CultureInfo.InvariantCulture, $"<stop offset=\"{(double)i / stops:F6}\" stop-color=\"#{i & 0xFFFFFF:x6}\"/>");

        sb.Append("</linearGradient></defs>");

        if (css)
            sb.Append("<style>rect{fill:url(#g);stroke:url(#g)}</style>");

        for (var i = 0; i < rects; i++)
            sb.Append("<rect width=\"10\" height=\"10\"").Append(css ? string.Empty : " fill=\"url(#g)\" stroke=\"url(#g)\"").Append("/>");

        return sb.Append("</svg>").ToString();
    }

    /// <summary>A JPEG with a frame header of <paramref name="width"/> by <paramref name="height"/> and no image data: the decoder learns the size, then fails late.</summary>
    private static byte[] JpegHeaderOnly(int width, int height)
    {
        var b = new List<byte> { 0xff, 0xd8, 0xff, 0xdb, 0x00, 0x43, 0x00 };
        b.AddRange(Enumerable.Repeat((byte)1, 64));
        b.AddRange([0xff, 0xc0, 0x00, 0x0b, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 0x01, 0x01, 0x11, 0x00]);
        b.AddRange([0xff, 0xc4, 0x00, 0x1f, 0x00, 0x00, 0x01, 0x05, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01]);
        b.AddRange(new byte[8]);
        b.AddRange(Enumerable.Range(0, 12).Select(i => (byte)i));
        b.AddRange([0xff, 0xc4, 0x00, 0x14, 0x10, 0x01, 0x00, 0x00]);
        b.AddRange(new byte[13]);
        b.AddRange([0x01, 0xff, 0xda, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3f, 0x00, 0xff, 0xd9]);
        return [.. b];
    }

    private static string DataUri(byte[] data, string mime = "image/png") => $"data:{mime};base64,{Convert.ToBase64String(data)}";

    private static string Image(byte[] data, string mime = "image/png") => $"<image width=\"100\" height=\"100\" href=\"{DataUri(data, mime)}\"/>";

    private static string UseFanOut()
    {
        var sb = new StringBuilder("<g id=\"a\"><rect width=\"1\" height=\"1\"/>" + Repeat("<use href=\"#b\"/>", 10) + "</g>");

        foreach (var (id, next) in new[] { ("b", "c"), ("c", "d"), ("d", "e"), ("e", "f"), ("f", "g") })
            sb.Append(CultureInfo.InvariantCulture, $"<g id=\"{id}\">").Append(Repeat($"<use href=\"#{next}\"/>", 10)).Append("</g>");

        sb.Append("<g id=\"g\"><rect width=\"1\" height=\"1\"/></g>").Append(Repeat("<use href=\"#a\"/>", 10));
        return Svg(sb.ToString());
    }

    /// <summary>Every probe of the review's set, with the bound its README states (the post-fix column). The 7 to 30 MB ones are the README's <c>--large</c> set.</summary>
    public static readonly IReadOnlyList<Probe> Probes =
    [
        // --- regular expressions ---
        new("rgb-spaces-20k", () => Svg("<rect width=\"10\" height=\"10\" fill=\"rgb(1" + new string(' ', 20_000) + "x)\"/>"), Ok, 0.3),
        new("rgb-spaces-40k", () => Svg("<rect width=\"10\" height=\"10\" fill=\"rgb(1" + new string(' ', 40_000) + "x)\"/>"), Ok, 0.3),
        new("rgb-spaces-1M", () => Svg("<rect width=\"10\" height=\"10\" fill=\"rgb(1" + new string(' ', 1_000_000) + "x)\"/>"), OkOrRefused, 5),
        new("css-comment-50k-openers", () => Svg("<style>" + Repeat("/*a", 50_000) + "</style>"), Ok, 0.3),
        new("css-comment-100k-openers", () => Svg("<style>" + Repeat("/*a", 100_000) + "</style>"), Ok, 0.3),
        new("css-comment-1M-openers", () => Svg("<style>" + Repeat("/*a", 1_000_000) + "</style>"), OkOrRefused, 1),
        new("script-50k-openers", () => Svg(Repeat("<script>", 50_000), "width=\"10\" height=\"10\""), Refused, 1, PageSource: true),
        new("script-1M-openers", () => Svg(Repeat("<script>", 1_000_000), "width=\"10\" height=\"10\""), Refused, 2, PageSource: true),

        // --- nesting ---
        new("nest-20k-g", () => Svg(Repeat("<g>", 20_000) + "<rect width=\"5\" height=\"5\"/>" + Repeat("</g>", 20_000)), Refused, 0.3),
        new("nest-100k-g", () => Svg(Repeat("<g>", 100_000) + "<rect width=\"5\" height=\"5\"/>" + Repeat("</g>", 100_000)), Refused, 0.5),
        new("nest-300-g-past-cap", () => Svg(Repeat("<g>", 300) + "<rect width=\"100\" height=\"100\" fill=\"#f00\"/>" + Repeat("</g>", 300)), Refused, 0.5),

        // --- gradients ---
        new("degenerate-gradients", () => Svg(
            "<defs><linearGradient id=\"a\" x1=\"5\" y1=\"5\" x2=\"5\" y2=\"5\" gradientUnits=\"userSpaceOnUse\"><stop offset=\"0\" stop-color=\"#f00\"/><stop offset=\"1\" stop-color=\"#00f\"/></linearGradient>" +
            "<radialGradient id=\"b\" r=\"0\"><stop offset=\"0\"/><stop offset=\"1\"/></radialGradient>" +
            "<radialGradient id=\"c\" r=\"1e38\" fx=\"1e38\" fy=\"-1e38\" gradientTransform=\"scale(0)\"><stop offset=\"NaN\"/><stop offset=\"-1e38%\"/><stop offset=\"1e38\"/></radialGradient>" +
            "<linearGradient id=\"d\" href=\"#d\"/><linearGradient id=\"e\" href=\"#f\"/><linearGradient id=\"f\" href=\"#e\"/><linearGradient id=\"g\" href=\"http://x/#a\"/></defs>" +
            "<rect width=\"50\" height=\"50\" fill=\"url(#a)\" stroke=\"url(#b)\"/><rect x=\"50\" width=\"50\" height=\"50\" fill=\"url(#c)\"/>" +
            "<rect y=\"50\" width=\"50\" height=\"50\" fill=\"url(#d)\" stroke=\"url(#e)\"/><rect x=\"50\" y=\"50\" width=\"50\" height=\"50\" fill=\"url(#f)\" stroke=\"url(#g)\"/>" +
            "<rect width=\"0\" height=\"0\" fill=\"url(#a)\"/><line x1=\"0\" y1=\"0\" x2=\"10\" y2=\"0\" stroke=\"url(#a)\"/>"), Ok, 0.5),
        new("gradient-100k-stops-x-200-rects", () => Gradient(100_000, 200), Ok, 2, MaxPeakMb: 200),
        new("gradient-100k-stops-x-2000-rects", () => Gradient(100_000, 2_000), Ok, 2, MaxPeakMb: 200),
        new("gradient-1k-stops-x-50k-rects-css", () => Gradient(1_000, 50_000, css: true), Ok, 2, MaxPeakMb: 200),

        // --- dashes ---
        new("dash-0.001-on-long-path", () => Svg("<path d=\"" + Repeat("M0 0 L1000 1000 L0 1000 L1000 0 ", 250) + "\" stroke=\"#000\" stroke-dasharray=\"0.001\"/>", "width=\"1000\" height=\"1000\""), Ok, 1, Raster: 1000),
        new("dash-1M-values-x-500-lines", () => Svg("<g stroke=\"#000\" stroke-dasharray=\"" + Repeat("1 2 ", 500_000) + "\">" + Repeat("<line x1=\"0\" y1=\"0\" x2=\"10\" y2=\"10\"/>", 500) + "</g>"), Ok, 1),

        // --- path data and points ---
        new("path-4M-segments-stroke", () => Svg("<path d=\"M0 0 " + Repeat("l1 1 ", 4_000_000) + "\" stroke=\"#000\" fill=\"none\"/>", "width=\"1000\" height=\"1000\""), OkOrRefused, 3, MaxPeakMb: 400),
        new("path-4M-segments-fill-evenodd", () => Svg("<path fill-rule=\"evenodd\" d=\"M0 0 " + Repeat("l7 3 l-5 2 ", 2_000_000) + "Z\"/>", "width=\"1000\" height=\"1000\""), OkOrRefused, 5, Raster: 1000),
        new("polygon-5M-numbers", () => Svg("<polygon points=\"" + Repeat("1.5,2.5 ", 2_500_000) + "\" stroke=\"#000\"/>", "width=\"1000\" height=\"1000\""), OkOrRefused, 5, MaxPeakMb: 300),
        new("path-cubic-1M-huge", () => Svg("<path d=\"M0 0 " + Repeat("C1e30 1e30 -1e30 -1e30 5 5 ", 1_000_000) + "\" stroke=\"#000\"/>", "width=\"1000\" height=\"1000\""), OkOrRefused, 5, MaxPeakMb: 300),
        new("path-arcs-500k", () => Svg("<path d=\"M0 0 " + Repeat("a1e9 1e9 0 1 1 1 1 ", 500_000) + "\" stroke=\"#000\"/>", "width=\"1000\" height=\"1000\""), Ok, 3),

        // --- text ---
        new("text-5MB-run", () => Svg("<text x=\"0\" y=\"50\" font-size=\"12\">" + Repeat("Beam L = 6 m ", 400_000) + "</text>", "width=\"1000\" height=\"1000\""), Ok, 3),
        new("text-20k-tspans-nested", () => Svg("<text>" + Repeat("<tspan dx=\"1\" dy=\"1\">a", 20_000) + Repeat("</tspan>", 20_000) + "</text>", "width=\"1000\" height=\"1000\""), Refused, 1),
        new("text-20k-elements-huge-font", () => Svg(Repeat("<text font-size=\"1e6\" x=\"0\" y=\"500\">W</text>", 20_000), "width=\"1000\" height=\"1000\""), Ok, 2),
        new("text-20k-elements-10-missing-families", () => Svg(Repeat("<text font-family=\"Nope1,Nope2,Nope3,Nope4,Nope5,Nope6,Nope7,Nope8,Nope9,Nope10\" x=\"0\" y=\"50\">W</text>", 20_000)), Ok, 2),
        new("font-family-path", () => Svg("<text font-family=\"/etc/passwd, file:///etc/passwd, ../../x\">a</text>"), Ok, 1),

        // --- style sheets ---
        new("css-20k-rules-x-20k-elements", () => Svg("<style>" + Repeat("rect{fill:#123456}", 20_000) + "</style>" + Repeat("<rect width=\"1\" height=\"1\"/>", 20_000)), Ok, 2),
        new("css-200k-rules-x-100k-elements", () => Svg("<style>" + Repeat("rect{fill:#123456}", 200_000) + "</style>" + Repeat("<rect width=\"1\" height=\"1\"/>", 100_000)), OkOrRefused, 3),
        new("css-1M-selectors-x-5k-elements", () => Svg("<style>" + Repeat("a,", 1_000_000) + "b{fill:red}</style>" + Repeat("<rect width=\"1\" height=\"1\"/>", 5_000)), OkOrRefused, 2),

        // --- images ---
        new("png-8000x8000-single", () => Svg(Image(SvgHostileInputTests.OneBitPng(8000, 8000))), Ok, 1, MaxPeakMb: 600),
        new("png-8001-refused", () => Svg(Image(SvgHostileInputTests.OneBitPng(8001, 10))), Ok, 1, MaxPeakMb: 150),
        new("png-8000x8000-x16-via-use", () => Svg("<defs><image id=\"i\" width=\"100\" height=\"100\" href=\"" + DataUri(SvgHostileInputTests.OneBitPng(8000, 8000)) + "\"/></defs>" + Repeat("<use href=\"#i\"/>", 16)), Ok, 1, MaxPeakMb: 600),
        new("png-8000x8000-x16-elements", () => Svg(Repeat(Image(SvgHostileInputTests.OneBitPng(8000, 8000)), 16)), Ok, 1, MaxPeakMb: 600),
        new("png-4096x4096-x4-within-the-edge", () => Svg(Repeat(Image(SvgHostileInputTests.OneBitPng(4096, 4096)), 4)), Ok, 3, MaxPeakMb: 600),
        new("jpeg-header-8000x8000-x8", () => Svg(Repeat(Image(JpegHeaderOnly(8000, 8000), "image/jpeg"), 8)), Ok, 3, MaxPeakMb: 600),
        new("base64-malformed-16MB", () => Svg("<image width=\"100\" height=\"100\" href=\"data:image/png;base64," + new string('A', 16 * 1024 * 1024) + "!\"/>"), OkOrRefused, 1),
        new("random-bytes-12MB", () => Svg(Image(RandomBytes(12 * 1024 * 1024))), OkOrRefused, 1),
        new("svg-as-image", () => Svg(Image(Encoding.UTF8.GetBytes(NestedSvg), "image/svg+xml")), Ok, 1),

        // --- opacity layers (drawn at a raster edge; see the remarks on the layer caps in the test below) ---
        new("opacity-nested-20@8000", () => Svg(Repeat("<g opacity=\"0.9\">", 20) + "<rect width=\"1000\" height=\"1000\"/><rect width=\"1\" height=\"1\"/>" + Repeat("</g>", 20), "width=\"1000\" height=\"1000\""), Ok, 5, MaxPeakMb: 1400, Raster: 8000),
        new("opacity-nested-250@2000", () => Svg(Repeat("<g opacity=\"0.9\">", 250) + "<rect width=\"1000\" height=\"1000\"/><rect width=\"1\" height=\"1\"/>" + Repeat("</g>", 250), "width=\"1000\" height=\"1000\""), Ok, 5, MaxPeakMb: 1024, Raster: 2000),
        new("opacity-siblings-20k@4000", () => Svg(Repeat("<g opacity=\"0.9\"><rect width=\"1000\" height=\"1000\"/><rect width=\"1\" height=\"1\"/></g>", 20_000), "width=\"1000\" height=\"1000\""), Ok, 5, Raster: 1000),

        // --- DTD, entities and references (through the sanitiser, as an attachment goes) ---
        new("xxe-and-billion-laughs", () => "<?xml version=\"1.0\"?><!DOCTYPE svg [<!ENTITY xxe SYSTEM \"file:///etc/hostname\"><!ENTITY a \"aaaaaaaaaa\"><!ENTITY b \"&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;\">" +
            "<!ENTITY c \"&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;\"><!ENTITY d \"&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;\">]>" +
            Svg("<text>&xxe;&d;</text><rect width=\"10\" height=\"10\"/>", "width=\"10\" height=\"10\""), Refused, 1, PageSource: true),
        new("xinclude", () => Svg("<xi:include xmlns:xi=\"http://www.w3.org/2001/XInclude\" href=\"file:///etc/hostname\" parse=\"text\"/><rect width=\"10\" height=\"10\"/>", "width=\"10\" height=\"10\""), Ok, 1, PageSource: true),
        new("doctype-unclosed-bracket", () => "<!DOCTYPE svg [" + Repeat("<!ENTITY a \"b\">", 100_000) + Svg(string.Empty, "width=\"10\" height=\"10\""), Refused, 1, PageSource: true),
        new("doctype-1M-no-close", () => "<!DOCTYPE svg " + new string('a', 3_000_000) + Svg(string.Empty, "width=\"10\" height=\"10\""), Refused, 1, PageSource: true),
        new("href-external-everywhere", () => Svg("<image href=\"http://127.0.0.1:9/a.png\"/><image xlink:href = 'file:///etc/passwd'/><use href=\"other.svg#x\"/>" +
            "<a href=\"javascript:alert(1)\"><rect width=\"5\" height=\"5\"/></a><image href=\"&#104;ttp://x/a.png\"/><image href=\" data:image/png;base64,AAAA\"/>" +
            "<image href=\"file:///etc/hostname\" width=\"10\" height=\"10\"/><use xlink:href=\"file:///etc/hostname#a\"/>", "width=\"10\" height=\"10\""), Ok, 1, PageSource: true),
        new("href-strace-target", () => Svg("<image href=\"file:///etc/hostname\" width=\"10\" height=\"10\"/><image xlink:href=\"http://127.0.0.1:9/a.png\" width=\"10\" height=\"10\"/>" +
            "<use href=\"file:///etc/hostname#a\"/><text font-family=\"/etc/hostname\">a</text>" + Image(Encoding.UTF8.GetBytes(NestedSvg), "image/svg+xml"), "width=\"10\" height=\"10\""), Ok, 1, PageSource: true),

        // --- values ---
        new("nan-inf-huge-values", () => Svg("<g transform=\"matrix(1e38 1e38 1e38 1e38 1e38 1e38) scale(1e30) rotate(NaN) translate(Infinity)\">" +
            "<rect x=\"NaN\" y=\"-Infinity\" width=\"1e38\" height=\"1e38\" rx=\"-1\" stroke-width=\"1e38\" stroke=\"#000\"/>" +
            "<circle cx=\"1e30\" cy=\"1e30\" r=\"1e30\"/><ellipse rx=\"1e-45\" ry=\"1e38\"/><line x1=\"-1e38\" x2=\"1e38\" y1=\"1\" y2=\"1\" stroke=\"#000\" stroke-miterlimit=\"1e38\" stroke-dasharray=\"1e38 1e-38\"/>" +
            "<g font-size=\"1e30em\"><g font-size=\"1e30em\"><text font-size=\"1e30\" x=\"1e30\" y=\"-1e30\" dx=\"NaN\" dy=\"Infinity\">W</text></g></g>" +
            "<rect width=\"1e38%\" height=\"-1e38%\" fill=\"rgba(1e38,-1e38,NaN,1e38)\" opacity=\"1e38\" fill-opacity=\"-1e38\"/>" +
            "<svg x=\"1e38\" y=\"1e38\" width=\"1e38\" height=\"1e38\" viewBox=\"0 0 1e-45 1e38\"><rect width=\"1\" height=\"1\"/></svg>" +
            "<use href=\"#u\" x=\"1e38\" y=\"1e38\" width=\"-1\" height=\"0\"/><symbol id=\"u\" viewBox=\"NaN NaN NaN NaN\"><rect width=\"1\" height=\"1\"/></symbol></g>",
            "width=\"1e30\" height=\"1e30\" viewBox=\"-1e38 -1e38 1e38 1e38\""), Ok, 0.5),

        // --- element count, attributes and use fan-out ---
        new("attrs-1.5M-one-element", () => $"<svg {Ns} width=\"10\" height=\"10\"><rect width=\"5\" height=\"5\"" + string.Concat(Enumerable.Range(0, 1_500_000).Select(i => $" a{i}=\"\"")) + "/></svg>", OkOrRefused, 3, MaxPeakMb: 600),
        new("rects-300k-30MB", () => Svg(Repeat("<rect x=\"1.5\" y=\"2.5\" width=\"30.25\" height=\"20.75\" fill=\"#123456\" stroke=\"#654321\" stroke-width=\"1.5\"/>", 300_000), "width=\"1000\" height=\"1000\""), OkOrRefused, 3, MaxPeakMb: 600),
        new("use-200k-of-symbol", () => Svg("<symbol id=\"s\" viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\"/></symbol>" + Repeat("<use href=\"#s\" width=\"10\" height=\"10\"/>", 200_000)), Ok, 3),
        new("use-fanout-10M", UseFanOut, Ok, 2),
    ];

    public static TheoryData<string> Names => [.. Probes.Select(p => p.Name)];

    private static byte[] RandomBytes(int count)
    {
        var bytes = new byte[count];
        new Random(12345).NextBytes(bytes);
        return bytes;
    }

    [Fact]
    public void Every_Probe_Of_The_Reviews_Set_Is_Here_Once()
    {
        Assert.Equal(Probes.Count, Probes.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.True(Probes.Count >= 50, $"{Probes.Count} probes");
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Probe_Stays_Within_Its_Bound(string name)
    {
        if (!Enabled)
        {
            output.WriteLine("Skipped: set TEMPEST_BOUNDED_PROBES=1 to run the bounded probes, one process each.");
            return;
        }

        var probe = Probes.Single(p => p.Name == name);
        var folder = Path.Combine(Path.GetTempPath(), "tempest-svg-probes");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name + ".svg");

        try
        {
            File.WriteAllText(path, probe.Markup(), new UTF8Encoding(false));
            output.WriteLine($"{name}: {new FileInfo(path).Length / 1024.0:F0} KB{(probe.PageSource ? ", through the sanitiser" : string.Empty)}{(probe.Raster > 0 ? $", drawn at {probe.Raster}" : string.Empty)}");

            var (exit, seconds, peakMb, log) = RunProbe(path, probe);
            output.WriteLine(log);
            output.WriteLine($"{name}: exit {exit}, {seconds:F2} s, peak {peakMb} MB");

            var maxSeconds = (probe.MaxSeconds + StartupAllowanceSeconds) * DesktopTestHelpers.TimeoutFactor;
            Assert.True(exit != 124, $"{name}: the process did not finish within {ProcessTimeout.TotalSeconds:F0} s and was killed.");
            Assert.True(probe.Exits.Contains(exit), $"{name}: exit code {exit} (allowed: {string.Join(", ", probe.Exits)}; 134 is a native abort).");
            Assert.True(seconds <= maxSeconds, $"{name}: took {seconds:F2} s (limit {maxSeconds:F1} s).");

            if (probe.MaxPeakMb > 0 && peakMb >= 0)
                Assert.True(peakMb <= probe.MaxPeakMb, $"{name}: peak working set {peakMb} MB (limit {probe.MaxPeakMb} MB).");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Runs the probe entry in a process of its own under the review's limits and returns its exit code, wall time, peak working set and output.</summary>
    private static (int Exit, double Seconds, long PeakMb, string Log) RunProbe(string path, Probe probe)
    {
        var dll = Path.Combine(AppContext.BaseDirectory, "Tempest.Desktop.dll");
        Assert.True(File.Exists(dll), $"{dll} is missing: the probe entry ships with the Desktop build.");

        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");

        if (string.IsNullOrEmpty(dotnet) || !File.Exists(dotnet))
            dotnet = Environment.ProcessPath is { } host && Path.GetFileNameWithoutExtension(host).Equals("dotnet", StringComparison.OrdinalIgnoreCase) ? host : "dotnet";

        var arguments = new List<string> { dll, SvgProbeRunner.Switch, path };

        if (probe.PageSource)
            arguments.Add("--page-source");

        if (probe.Raster > 0)
        {
            arguments.Add("--raster");
            arguments.Add(probe.Raster.ToString(CultureInfo.InvariantCulture));
        }

        var start = new ProcessStartInfo { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };

        if (OperatingSystem.IsLinux())
        {
            // A 2 GiB address-space limit, so a class that still allocates without bound dies here (exit 134 or 137), never in CI's test host.
            start.FileName = "/bin/sh";
            start.ArgumentList.Add("-c");
            start.ArgumentList.Add($"ulimit -v {Environment.GetEnvironmentVariable("TEMPEST_BOUNDED_PROBES_ULIMIT_KB") ?? "2097152"}; exec \"$0\" \"$@\"");
            start.ArgumentList.Add(dotnet);
        }
        else
        {
            start.FileName = dotnet;
        }

        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        // The review's harness: a 512 MB managed heap, workstation GC. The GC's region reservation is held to twice the heap, since by
        // default it reserves far more address space than any ulimit -v allows and the runtime would not start at all.
        var heapHex = Environment.GetEnvironmentVariable("TEMPEST_BOUNDED_PROBES_HEAP_HEX") ?? "0x20000000";
        var heap = Convert.ToInt64(heapHex, 16);
        start.Environment["DOTNET_GCHeapHardLimit"] = $"0x{heap:X}";
        start.Environment["DOTNET_GCRegionRange"] = $"0x{heap * 2:X}";
        start.Environment["DOTNET_gcServer"] = "0";
        start.Environment["DOTNET_TieredPGO"] = "0";

        var watch = Stopwatch.StartNew();
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        var exit = 124;

        if (process.WaitForExit((int)ProcessTimeout.TotalMilliseconds))
        {
            process.WaitForExit();
            exit = process.ExitCode;
        }
        else
        {
            process.Kill(entireProcessTree: true);
        }

        watch.Stop();
        var log = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
        var peakLine = log.Split('\n').Select(l => l.Trim()).LastOrDefault(l => l.StartsWith(SvgProbeRunner.PeakPrefix, StringComparison.Ordinal));
        var peakMb = peakLine is not null && long.TryParse(peakLine[SvgProbeRunner.PeakPrefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mb) ? mb : -1;
        return (exit, watch.Elapsed.TotalSeconds, peakMb, log.Trim());
    }
}
