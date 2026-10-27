using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using SkiaSharp;

namespace Tempest.Desktop.Viewing;

/// <summary>
/// The app's own SVG rasteriser (DB-08): reads an SVG document with <see cref="XmlReader"/> and records it as a SkiaSharp
/// <see cref="SKPicture"/> in the document's user units. It replaces <c>Svg.Skia</c>, whose transitive <c>Svg.Custom</c> is
/// MS-PL, a licence outside the MIT, BSD and Apache set the owner allows; SkiaSharp itself is MIT and already shipped for the
/// PDF viewer.
/// </summary>
/// <remarks>
/// <para>
/// <b>What it draws.</b> The static subset an engineering attachment uses: <c>svg</c> (root and nested), <c>g</c>, <c>a</c>,
/// <c>switch</c>, <c>rect</c> (with <c>rx</c>/<c>ry</c>), <c>circle</c>, <c>ellipse</c>, <c>line</c>, <c>polyline</c>,
/// <c>polygon</c>, <c>path</c> (every path command, through <see cref="SKPath.ParseSvgPathData"/>), <c>text</c> with
/// <c>tspan</c>, <c>image</c> with an embedded <c>data:</c> URI, <c>use</c> of any element by id (a <c>symbol</c> is drawn
/// as a group), and <c>linearGradient</c>/<c>radialGradient</c> fills and strokes (object-bounding-box or user-space units,
/// <c>gradientTransform</c>, spread method, stops inherited through <c>href</c>). Styling comes from presentation attributes,
/// a <c>&lt;style&gt;</c> sheet of plain type, class and id selectors, and the <c>style</c> attribute, in that order of
/// precedence; it covers fill and stroke (colours by hex, <c>rgb()</c>, <c>rgba()</c>, the CSS names, <c>currentColor</c>,
/// <c>none</c> and <c>url(#id)</c> with a fallback), stroke width, caps, joins, miter limit, dashes, opacity, fill and
/// stroke opacity, fill rule, visibility, display, font family, size, weight and style, and text anchor. Lengths take
/// <c>px</c>, <c>pt</c>, <c>pc</c>, <c>mm</c>, <c>cm</c>, <c>in</c>, <c>em</c>, <c>ex</c> and <c>%</c>. Transforms take
/// every SVG transform function; the root <c>viewBox</c> and <c>preserveAspectRatio</c> fit the content to the stated size.
/// </para>
/// <para>
/// <b>What it leaves out</b>, drawing the element without the effect or not at all: clip paths, masks, filters, patterns,
/// markers, animation, CSS beyond the simple selectors above, and <c>foreignObject</c>. An <c>image</c> that is not a
/// <c>data:</c> URI is never fetched (the sanitiser has already blanked it; this reader has no network or file path at all)
/// and a DOCTYPE is refused by the XML reader (<see cref="DtdProcessing.Prohibit"/>) should one get past the sanitiser.
/// </para>
/// <para>
/// <b>Caps.</b> At most <see cref="MaxElements"/> elements are drawn, <c>use</c> recursion stops at <see cref="MaxUseDepth"/>,
/// nesting at <see cref="MaxNesting"/>, an embedded image is decoded only when its data is under <see cref="MaxImageBytes"/>
/// and its edge under <see cref="MaxImageEdge"/> pixels, and the document itself under <see cref="MaxDocumentCharacters"/>
/// characters.
/// </para>
/// <para>
/// <b>Equivalence.</b> The Desktop test <c>SvgRenderComparisonTests</c> holds every fixture to the picture <c>Svg.Skia</c>
/// produced before the switch, within an anti-aliasing tolerance.
/// </para>
/// </remarks>
public static class SvgPictureBuilder
{
    public const int MaxElements = 200_000;
    public const int MaxUseDepth = 16;
    public const int MaxNesting = 256;
    public const int MaxImageBytes = 16 * 1024 * 1024;
    public const int MaxImageEdge = 4096;

    /// <summary>The longest document read (F11): 16M characters, under the 25 MB attachment cap so that it binds, since the tree takes about twenty times the file.</summary>
    public const long MaxDocumentCharacters = 16L * 1024 * 1024;

    /// <summary>The most stops a gradient contributes (F2): no drawn gradient has more, and a stop list is parsed into every shader that uses it.</summary>
    public const int MaxGradientStops = 64;

    /// <summary>The most values a <c>stroke-dasharray</c> may hold (F3); a longer list is treated as no dash, since the list is copied into every stroke that uses it.</summary>
    public const int MaxDashes = 64;

    /// <summary>The most pixels one document decodes from its embedded images, all of them together (F4): 64 megapixels is 256 MB of bitmap, which no drawing needs and a crafted one would multiply.</summary>
    public const long MaxDecodedPixels = 64_000_000;

    /// <summary>The most elements and attributes a document may hold, counted before it is loaded (F11): the DOM takes about twenty times the file's bytes, so this holds the load to a few hundred MB.</summary>
    public const long MaxNodes = 2_000_000;

    /// <summary>The largest font size in user units (F6): text is measured at its size, and a size of a million made measuring 20 000 runs take 48 s.</summary>
    public const float MaxFontSize = 16_384f;

    /// <summary>The most family names of a <c>font-family</c> list that are looked up (F6); each is a query to the platform's font configuration.</summary>
    public const int MaxFontFamilies = 8;

    /// <summary>The deepest nesting of group-opacity layers (F7): every open layer is a bitmap the size of the page at the raster scale, live at draw.</summary>
    public const int MaxOpacityLayerDepth = 8;

    /// <summary>The most group-opacity layers in one document (F7); each is a page-sized clear and composite at draw.</summary>
    public const int MaxOpacityLayers = 1_000;

    /// <summary>The longest <c>d</c> or <c>points</c> attribute a shape may have (F8); a longer shape is left out, since its path alone would be tens of MB.</summary>
    public const int MaxShapeDataCharacters = 2_000_000;

    /// <summary>The most path verbs one document draws, all its shapes together (F8): Skia's edge list at draw grows with them, and four million segments of an even-odd fill exhausted it.</summary>
    public const long MaxPathVerbs = 4_000_000;

    /// <summary>The most style-sheet rules of distinct selectors a document keeps (F9); a rule of a further selector is ignored (one that repeats a kept selector merges into it).</summary>
    public const int MaxStyleRules = 10_000;

    /// <summary>
    /// The caps a document is held to. The defaults are the constants above and are what the viewer uses; a test passes smaller
    /// ones so each cap is exercised on a small document, in the normal suite, instead of on the multi-megabyte file that found it.
    /// </summary>
    public sealed record Limits
    {
        public static readonly Limits Default = new();

        public int Elements { get; init; } = MaxElements;
        public int Nesting { get; init; } = MaxNesting;
        public long Nodes { get; init; } = MaxNodes;
        public long DocumentCharacters { get; init; } = MaxDocumentCharacters;
        public int GradientStops { get; init; } = MaxGradientStops;
        public int Dashes { get; init; } = MaxDashes;
        public int ImageEdge { get; init; } = MaxImageEdge;
        public int ImageBytes { get; init; } = MaxImageBytes;
        public long DecodedPixels { get; init; } = MaxDecodedPixels;
        public float FontSize { get; init; } = MaxFontSize;
        public int FontFamilies { get; init; } = MaxFontFamilies;
        public int OpacityLayerDepth { get; init; } = MaxOpacityLayerDepth;
        public int OpacityLayers { get; init; } = MaxOpacityLayers;
        public int ShapeDataCharacters { get; init; } = MaxShapeDataCharacters;
        public long PathVerbs { get; init; } = MaxPathVerbs;
        public int StyleRules { get; init; } = MaxStyleRules;
    }

    /// <summary>What the reader counted while recording a document: the figures the caps are tested on, and what was left out.</summary>
    /// <param name="Elements">Elements drawn (or passed over by a cap), <c>use</c> targets included.</param>
    /// <param name="GradientStopParses">Stop lists parsed; every further use of the same gradient takes the parsed list (F2).</param>
    /// <param name="DecodedPixels">Pixels decoded from embedded images, each distinct image once (F4).</param>
    /// <param name="ImagesSkipped">Embedded images not decoded because the document's pixel budget was spent (F4).</param>
    /// <param name="TypefaceLookups">Distinct (family list, weight, slant) combinations resolved to a typeface; every further run with the same takes the cached face (F6).</param>
    /// <param name="FamilyLookups">Family names asked of the platform's font manager, at most <see cref="Limits.FontFamilies"/> per typeface lookup (F6).</param>
    /// <param name="OpacityLayers">Group-opacity layers recorded (F7).</param>
    /// <param name="OpacityFolds">Elements whose group opacity was folded into their fill and stroke opacity instead, because a layer cap was reached (F7).</param>
    /// <param name="PathVerbs">Path verbs drawn, every shape together (F8).</param>
    /// <param name="ShapesSkipped">Shapes left out because their data was over <see cref="Limits.ShapeDataCharacters"/> or they would take the document past <see cref="Limits.PathVerbs"/> (F8).</param>
    /// <param name="StyleRules">Style-sheet rules kept, one per distinct selector (F9).</param>
    /// <param name="StyleRulesIgnored">Style-sheet rules of a further selector past <see cref="Limits.StyleRules"/>, ignored (F9).</param>
    public sealed record Statistics(int Elements, int GradientStopParses, long DecodedPixels, int ImagesSkipped, int TypefaceLookups, int FamilyLookups, int OpacityLayers, int OpacityFolds, long PathVerbs, int ShapesSkipped, int StyleRules, int StyleRulesIgnored);

    private const float PixelsPerInch = 96f;
    private const float DefaultFontSize = 16f;
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";
    private static readonly XNamespace Xlink = "http://www.w3.org/1999/xlink";

    /// <summary>The recorded picture and its natural size in user units (the stated width and height, else the viewBox, else the drawn content's bounds).</summary>
    /// <param name="Statistics">What the reader counted on the way, for the tests of its caps.</param>
    /// <param name="NotShown">
    /// The kinds of feature the document uses that this reader does not draw, by element name and in alphabetical order (<c>clipPath</c>,
    /// <c>filter</c>, <c>foreignObject</c>, <c>marker</c>, <c>mask</c>, <c>pattern</c>, <c>textPath</c>), so the viewer can say that
    /// some parts of the drawing are not shown rather than show it silently wrong (I2). Empty for a document drawn whole.
    /// </param>
    public sealed record SvgPicture(SKPicture Picture, SKSize Size, Statistics Statistics, IReadOnlyList<string> NotShown) : IDisposable
    {
        public void Dispose() => Picture.Dispose();
    }

    /// <summary>Every regular expression here gives up after this long, so no attribute value can hold the reader (F1); the caller reports the document as unreadable.</summary>
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary>Reads <paramref name="markup"/> and records it; a document that is not SVG throws (an <see cref="XmlException"/> for broken markup).</summary>
    /// <exception cref="XmlException">The markup is not well-formed XML.</exception>
    /// <exception cref="InvalidOperationException">The root element is not <c>svg</c>.</exception>
    public static SvgPicture Build(string markup) => Build(markup, CancellationToken.None);

    /// <summary>
    /// Reads <paramref name="markup"/> and records it, stopping at <paramref name="cancellationToken"/>: the token is looked at before every
    /// element, every gradient stop and every text run, so a caller with a deadline (the attachment viewer gives a document 15 seconds)
    /// gets an <see cref="OperationCanceledException"/> rather than a frozen window whatever the document contains (F10).
    /// </summary>
    /// <exception cref="XmlException">The markup is not well-formed XML.</exception>
    /// <exception cref="InvalidOperationException">The root element is not <c>svg</c>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled before the picture was recorded.</exception>
    public static SvgPicture Build(string markup, CancellationToken cancellationToken) => Build(markup, cancellationToken, Limits.Default);

    /// <summary>As <see cref="Build(string, CancellationToken)"/>, holding the document to <paramref name="limits"/> instead of the defaults.</summary>
    public static SvgPicture Build(string markup, CancellationToken cancellationToken, Limits limits)
    {
        ArgumentNullException.ThrowIfNull(markup);
        ArgumentNullException.ThrowIfNull(limits);
        cancellationToken.ThrowIfCancellationRequested();

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = false,
            MaxCharactersInDocument = limits.DocumentCharacters,
            MaxCharactersFromEntities = 0,
        };

        // F5/F11: a linear pass over the markup before the tree is built. XDocument.Load is quadratic in nesting depth (100 000
        // levels, 683 KB: 110 s) and the tree takes about twenty times the file's bytes, so the depth and the node count are
        // refused here, where the raw reader measures them in milliseconds, rather than after the load that they make expensive.
        using (var probe = XmlReader.Create(new StringReader(markup), settings))
        {
            long nodes = 0;

            while (probe.Read())
            {
                if (probe.Depth > limits.Nesting)
                    throw new InvalidOperationException($"the file nests elements deeper than {limits.Nesting} levels.");

                nodes += 1 + probe.AttributeCount;

                if (nodes > limits.Nodes)
                    throw new InvalidOperationException($"the file has more than {limits.Nodes:N0} elements and attributes.");

                if ((nodes & 4095) == 0)
                    cancellationToken.ThrowIfCancellationRequested();
            }
        }

        XDocument document;
        using (var reader = XmlReader.Create(new StringReader(markup), settings))
            document = XDocument.Load(reader, LoadOptions.None);

        var root = document.Root;

        if (root is null || !Is(root, "svg"))
            throw new InvalidOperationException("the file has no readable SVG content.");

        cancellationToken.ThrowIfCancellationRequested();
        var context = new Context(root, cancellationToken, limits);
        var declared = DeclaredSize(root, context);
        context.SetViewport(declared);

        // Recorded once in user units; the viewer scales the picture itself.
        using var recorder = new SKPictureRecorder();
        var bounds = new BoundsTracker();
        var cull = declared is { } d ? new SKRect(0, 0, d.Width, d.Height) : new SKRect(-1e6f, -1e6f, 1e6f, 1e6f);
        var canvas = recorder.BeginRecording(cull);
        var renderer = new Renderer(context, bounds);

        try
        {
            renderer.DrawRoot(canvas, root, declared);
        }
        finally
        {
            renderer.ReleaseImages();
        }

        var picture = recorder.EndRecording();
        var statistics = renderer.Statistics;
        var notShown = renderer.NotShown;

        if (declared is { } size)
            return new SvgPicture(picture, size, statistics, notShown);

        // No stated size and no viewBox: the picture is the drawn content, shifted to the origin.
        var content = bounds.Rect;

        if (content.Width <= 0 || content.Height <= 0)
            return new SvgPicture(picture, SKSize.Empty, statistics, notShown);

        using var shifted = new SKPictureRecorder();
        var shiftedCanvas = shifted.BeginRecording(new SKRect(0, 0, content.Width, content.Height));
        shiftedCanvas.Translate(-content.Left, -content.Top);
        shiftedCanvas.DrawPicture(picture);
        picture.Dispose();
        return new SvgPicture(shifted.EndRecording(), new SKSize(content.Width, content.Height), statistics, notShown);
    }

    /// <summary>The root's own size: absolute width and height, else the viewBox's, else <see langword="null"/>.</summary>
    private static SKSize? DeclaredSize(XElement root, Context context)
    {
        var viewBox = ViewBox(root);
        var width = AbsoluteLength(root.Attribute("width")?.Value, context, viewBox?.Width);
        var height = AbsoluteLength(root.Attribute("height")?.Value, context, viewBox?.Height);

        if (width is { } w && height is { } h)
            return w > 0 && h > 0 ? new SKSize(w, h) : SKSize.Empty;

        if (viewBox is { } box)
        {
            // One stated edge with a viewBox: the other follows the viewBox's aspect.
            if (width is { } onlyWidth)
                return new SKSize(onlyWidth, box.Height <= 0 ? 0 : onlyWidth * box.Height / box.Width);

            if (height is { } onlyHeight)
                return new SKSize(box.Width <= 0 ? 0 : onlyHeight * box.Width / box.Height, onlyHeight);

            return new SKSize(box.Width, box.Height);
        }

        return null;
    }

    /// <summary>A width or height attribute as pixels; a percentage counts as unstated (it refers to a container the viewer does not have), falling back to <paramref name="fallback"/>.</summary>
    private static float? AbsoluteLength(string? text, Context context, float? fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Trim();

        if (text.EndsWith('%'))
            return fallback;

        return Length(text, context, 0, DefaultFontSize);
    }

    private static SKRect? ViewBox(XElement element)
    {
        var numbers = Numbers(element.Attribute("viewBox")?.Value);
        return numbers.Count == 4 && numbers[2] > 0 && numbers[3] > 0 ? SKRect.Create(numbers[0], numbers[1], numbers[2], numbers[3]) : null;
    }

    private static bool Is(XElement element, string localName) =>
        string.Equals(element.Name.LocalName, localName, StringComparison.Ordinal) && (element.Name.Namespace == Svg || element.Name.Namespace == XNamespace.None);

    // ------------------------------------------------------------------
    // Document context: style sheet rules and elements by id
    // ------------------------------------------------------------------

    private sealed class Context
    {
        public readonly Dictionary<string, XElement> ById = new(StringComparer.Ordinal);
        public readonly CancellationToken Token;
        public readonly Limits Limits;
        private float _viewportWidth;

        // F9: every rule is filed under its most specific part (id, else class, else tag, else universal), so the rules an element
        // can match are the union of four short lists rather than every rule of the sheet (rules × elements: 20 000 × 20 000 never finished).
        private readonly Dictionary<string, List<StyleRule>> _rulesById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<StyleRule>> _rulesByClass = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<StyleRule>> _rulesByTag = new(StringComparer.Ordinal);
        private readonly List<StyleRule> _universalRules = [];

        /// <summary>Rules kept from every style sheet: one per distinct selector (a rule that repeats an earlier selector merges into it and is not counted).</summary>
        public int RuleCount { get; private set; }

        /// <summary>Rules of a new selector past <see cref="Limits.StyleRules"/>, ignored.</summary>
        public int RulesIgnored { get; private set; }

        /// <summary>The position of the next declaration in the sheet, for the cascade.</summary>
        private int _order;

        /// <summary>Files one parsed rule, or counts it as ignored past the cap.</summary>
        /// <summary>The one rule kept per distinct (tag, class, id) selector, so a sheet of ten thousand <c>rect{…}</c> rules is one candidate, not ten thousand.</summary>
        private readonly Dictionary<(string? Tag, string? Class, string? Id), StyleRule> _rulesBySelector = [];

        public void AddRule(string? tag, string? cls, string? id, IReadOnlyDictionary<string, string> declarations)
        {
            var order = _order++;

            // Rules with an identical selector match exactly the same elements, so the cascade among them is, per property, the latest
            // declaration: they are merged into one rule that remembers each property's value and the order it was declared at. Rules of
            // different selectors stay apart, and keep their order for the cascade among equal specificities (the re-review's F9 blocker:
            // 10 000 rules under one tag key were still filtered and sorted for every element).
            if (_rulesBySelector.TryGetValue((tag, cls, id), out var existing))
            {
                foreach (var (name, value) in declarations)
                    existing.Declarations[name] = (value, order);

                return;
            }

            if (RuleCount >= Limits.StyleRules)
            {
                RulesIgnored++;
                return;
            }

            RuleCount++;
            var specificity = (id is null ? 0 : 100) + (cls is null ? 0 : 10) + (tag is null ? 0 : 1);
            var rule = new StyleRule(tag, cls, id, specificity, declarations.ToDictionary(d => d.Key, d => (d.Value, order), StringComparer.OrdinalIgnoreCase));
            _rulesBySelector[(tag, cls, id)] = rule;
            var list = id is not null ? ListFor(_rulesById, id) : cls is not null ? ListFor(_rulesByClass, cls) : tag is not null ? ListFor(_rulesByTag, tag) : _universalRules;
            list.Add(rule);
        }

        private static List<StyleRule> ListFor(Dictionary<string, List<StyleRule>> index, string key)
        {
            if (!index.TryGetValue(key, out var list))
                index[key] = list = [];

            return list;
        }

        /// <summary>The rules that apply to an element of <paramref name="tag"/> with <paramref name="classes"/> and <paramref name="id"/>: one per distinct selector, any order (the caller cascades by specificity and declaration order).</summary>
        public IEnumerable<StyleRule> RulesFor(string tag, string[] classes, string? id)
        {
            if (RuleCount == 0)
                return [];

            var candidates = new List<StyleRule>(_universalRules);

            if (_rulesByTag.TryGetValue(tag, out var byTag))
                candidates.AddRange(byTag);

            foreach (var cls in classes)
            {
                if (_rulesByClass.TryGetValue(cls, out var byClass))
                    candidates.AddRange(byClass);
            }

            if (id is not null && _rulesById.TryGetValue(id, out var byId))
                candidates.AddRange(byId);

            return candidates
                .Where(r => (r.Tag is null || string.Equals(r.Tag, tag, StringComparison.Ordinal))
                    && (r.Class is null || classes.Contains(r.Class, StringComparer.Ordinal))
                    && (r.Id is null || string.Equals(r.Id, id, StringComparison.Ordinal)));
        }
        private float _viewportHeight;

        /// <summary>Sets the viewport a percentage refers to: the root's own size (the viewer has no outer container).</summary>
        public void SetViewport(SKSize? size)
        {
            _viewportWidth = size?.Width ?? 0;
            _viewportHeight = size?.Height ?? 0;
        }

        /// <summary>The viewport edge a percentage on <paramref name="axis"/> refers to: 0 the width, 1 the height, else the normalised diagonal.</summary>
        public float Viewport(int axis) => axis switch
        {
            0 => _viewportWidth,
            1 => _viewportHeight,
            _ => (float)(Math.Sqrt((_viewportWidth * _viewportWidth) + (_viewportHeight * _viewportHeight)) / Math.Sqrt(2)),
        };

        public Context(XElement root, CancellationToken token, Limits limits)
        {
            Token = token;
            Limits = limits;
            var seen = 0;

            foreach (var element in root.DescendantsAndSelf())
            {
                if ((++seen & 1023) == 0)
                    token.ThrowIfCancellationRequested();

                if (element.Attribute("id")?.Value is { Length: > 0 } id)
                    ById.TryAdd(id, element);

                if (Is(element, "style"))
                    ParseStyleSheet(element.Value, this);
            }
        }
    }

    /// <summary>One selector's rule: each property with its value and the order of the declaration that set it (the latest, over every rule of this selector).</summary>
    private sealed record StyleRule(string? Tag, string? Class, string? Id, int Specificity, Dictionary<string, (string Value, int Order)> Declarations);

    private static readonly Regex CssComment = new(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.NonBacktracking, MatchTimeout);

    /// <summary>A plain style sheet: rules of <c>tag</c>, <c>.class</c>, <c>tag.class</c> and <c>#id</c> selectors (any other selector is ignored), filed in <paramref name="context"/>.</summary>
    private static void ParseStyleSheet(string css, Context context)
    {
        css = CssComment.Replace(css, string.Empty);

        foreach (var block in css.Split('}', StringSplitOptions.RemoveEmptyEntries))
        {
            var open = block.IndexOf('{', StringComparison.Ordinal);

            if (open < 0)
                continue;

            var declarations = ParseDeclarations(block[(open + 1)..]);

            if (declarations.Count == 0)
                continue;

            foreach (var selector in block[..open].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (selector.Length == 0 || selector.Any(char.IsWhiteSpace) || selector.Contains('>', StringComparison.Ordinal) || selector.Contains(':', StringComparison.Ordinal) || selector.Contains('[', StringComparison.Ordinal))
                    continue;

                string? tag = null, cls = null, id = null;
                var hash = selector.IndexOf('#', StringComparison.Ordinal);
                var dot = selector.IndexOf('.', StringComparison.Ordinal);

                if (hash >= 0)
                {
                    id = selector[(hash + 1)..];
                    tag = hash > 0 ? selector[..hash] : null;
                }
                else if (dot >= 0)
                {
                    cls = selector[(dot + 1)..];
                    tag = dot > 0 ? selector[..dot] : null;
                }
                else
                {
                    tag = selector;
                }

                if (tag == "*")
                    tag = null;

                context.AddRule(tag, cls, id, declarations);
            }
        }
    }

    private static Dictionary<string, string> ParseDeclarations(string text)
    {
        var declarations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var declaration in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = declaration.IndexOf(':', StringComparison.Ordinal);

            if (colon <= 0)
                continue;

            var name = declaration[..colon].Trim();
            var value = declaration[(colon + 1)..].Trim();
            var important = value.IndexOf("!important", StringComparison.OrdinalIgnoreCase);

            if (important >= 0)
                value = value[..important].Trim();

            if (name.Length > 0 && value.Length > 0)
                declarations[name] = value;
        }

        return declarations;
    }

    // ------------------------------------------------------------------
    // Resolved style
    // ------------------------------------------------------------------

    /// <summary>What a fill or stroke paints with: a colour, a gradient by id (with the fallback colour), or nothing.</summary>
    private readonly record struct Paint(SKColor? Colour, string? GradientId)
    {
        public static readonly Paint None = new(null, null);
        public bool IsNone => Colour is null && GradientId is null;
    }

    /// <summary>The inherited presentation properties, as a value copied and amended down the tree.</summary>
    private sealed record Style
    {
        public Paint Fill { get; init; } = new(SKColors.Black, null);
        public Paint Stroke { get; init; } = Paint.None;
        public float StrokeWidth { get; init; } = 1f;
        public SKStrokeCap Cap { get; init; } = SKStrokeCap.Butt;
        public SKStrokeJoin Join { get; init; } = SKStrokeJoin.Miter;
        public float MiterLimit { get; init; } = 4f;
        public float[]? Dashes { get; init; }
        public float DashOffset { get; init; }
        public float FillOpacity { get; init; } = 1f;
        public float StrokeOpacity { get; init; } = 1f;
        public bool EvenOdd { get; init; }
        public SKColor CurrentColor { get; init; } = SKColors.Black;
        public string FontFamily { get; init; } = "sans-serif";
        public float FontSize { get; init; } = DefaultFontSize;
        public bool Bold { get; init; }
        public bool Italic { get; init; }
        public int Anchor { get; init; }
        public bool Visible { get; init; } = true;

        // Not inherited: read per element.
        public float Opacity { get; init; } = 1f;
        public bool Display { get; init; } = true;
    }

    private static readonly string[] StyleProperties =
    [
        "fill", "stroke", "stroke-width", "stroke-linecap", "stroke-linejoin", "stroke-miterlimit", "stroke-dasharray", "stroke-dashoffset",
        "fill-opacity", "stroke-opacity", "fill-rule", "color", "font-family", "font-size", "font-weight", "font-style", "text-anchor",
        "visibility", "opacity", "display", "stop-color", "stop-opacity",
        // Read only to report that the effect is not drawn (I2).
        "clip-path", "mask", "filter", "marker-start", "marker-mid", "marker-end", "marker",
    ];

    /// <summary>Every styling property of <paramref name="element"/>: presentation attributes, then matching sheet rules, then the <c>style</c> attribute.</summary>
    private static Dictionary<string, string> Properties(XElement element, Context context)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in StyleProperties)
        {
            if (element.Attribute(name)?.Value is { } value)
                properties[name] = value;
        }

        if (context.RuleCount > 0)
        {
            var tag = element.Name.LocalName;
            var classes = (element.Attribute("class")?.Value ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var id = element.Attribute("id")?.Value;

            // The cascade: least specific first, and among equals the earliest declaration first, so the last applied wins.
            var declarations = new List<(int Specificity, int Order, string Name, string Value)>();

            foreach (var rule in context.RulesFor(tag, classes, id))
            {
                foreach (var (name, (value, order)) in rule.Declarations)
                    declarations.Add((rule.Specificity, order, name, value));
            }

            declarations.Sort((a, b) => a.Specificity != b.Specificity ? a.Specificity.CompareTo(b.Specificity) : a.Order.CompareTo(b.Order));

            foreach (var (_, _, name, value) in declarations)
                properties[name] = value;
        }

        if (element.Attribute("style")?.Value is { } inline)
        {
            foreach (var (name, value) in ParseDeclarations(inline))
                properties[name] = value;
        }

        return properties;
    }

    private static Style Resolve(Style parent, XElement element, Context context) => Resolve(parent, element, context, out _);

    /// <summary>As <see cref="Resolve(Style, XElement, Context)"/>, also giving the element's resolved properties for the caller to look at.</summary>
    private static Style Resolve(Style parent, XElement element, Context context, out Dictionary<string, string> properties)
    {
        var p = properties = Properties(element, context);
        var style = parent with { Opacity = 1f, Display = true };

        if (p.TryGetValue("color", out var color) && ParseColour(color, style.CurrentColor) is { } current)
            style = style with { CurrentColor = current };

        if (p.TryGetValue("font-size", out var fontSize) && Length(fontSize, context, 0, parent.FontSize) is { } size && size > 0)
            style = style with { FontSize = Math.Min(size, context.Limits.FontSize) };

        if (p.TryGetValue("fill", out var fill))
            style = style with { Fill = ParsePaint(fill, style.CurrentColor, style.Fill) };

        if (p.TryGetValue("stroke", out var stroke))
            style = style with { Stroke = ParsePaint(stroke, style.CurrentColor, style.Stroke) };

        if (p.TryGetValue("stroke-width", out var strokeWidth) && Length(strokeWidth, context, 0, style.FontSize) is { } width)
            style = style with { StrokeWidth = Math.Max(0, width) };

        if (p.TryGetValue("stroke-linecap", out var cap))
            style = style with { Cap = cap.Trim().ToLowerInvariant() switch { "round" => SKStrokeCap.Round, "square" => SKStrokeCap.Square, _ => SKStrokeCap.Butt } };

        if (p.TryGetValue("stroke-linejoin", out var join))
            style = style with { Join = join.Trim().ToLowerInvariant() switch { "round" => SKStrokeJoin.Round, "bevel" => SKStrokeJoin.Bevel, _ => SKStrokeJoin.Miter } };

        if (p.TryGetValue("stroke-miterlimit", out var miter) && Number(miter) is { } limit && limit >= 1)
            style = style with { MiterLimit = limit };

        if (p.TryGetValue("stroke-dasharray", out var dash))
            style = style with { Dashes = ParseDashes(dash, context, style.FontSize) };

        if (p.TryGetValue("stroke-dashoffset", out var dashOffset) && Length(dashOffset, context, 0, style.FontSize) is { } offset)
            style = style with { DashOffset = offset };

        if (p.TryGetValue("fill-opacity", out var fillOpacity) && Opacity(fillOpacity) is { } fo)
            style = style with { FillOpacity = fo };

        if (p.TryGetValue("stroke-opacity", out var strokeOpacity) && Opacity(strokeOpacity) is { } so)
            style = style with { StrokeOpacity = so };

        if (p.TryGetValue("fill-rule", out var rule))
            style = style with { EvenOdd = string.Equals(rule.Trim(), "evenodd", StringComparison.OrdinalIgnoreCase) };

        if (p.TryGetValue("font-family", out var family))
            style = style with { FontFamily = family.Trim() };

        if (p.TryGetValue("font-weight", out var weight))
            style = style with { Bold = weight.Trim().ToLowerInvariant() is "bold" or "bolder" || (int.TryParse(weight.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var w) && w >= 600) };

        if (p.TryGetValue("font-style", out var fontStyle))
            style = style with { Italic = fontStyle.Trim().ToLowerInvariant() is "italic" or "oblique" };

        if (p.TryGetValue("text-anchor", out var anchor))
            style = style with { Anchor = anchor.Trim().ToLowerInvariant() switch { "middle" => 1, "end" => 2, _ => 0 } };

        if (p.TryGetValue("visibility", out var visibility))
            style = style with { Visible = visibility.Trim().ToLowerInvariant() is not ("hidden" or "collapse") };

        if (p.TryGetValue("opacity", out var opacity) && Opacity(opacity) is { } o)
            style = style with { Opacity = o };

        if (p.TryGetValue("display", out var display))
            style = style with { Display = !string.Equals(display.Trim(), "none", StringComparison.OrdinalIgnoreCase) };

        return style;
    }

    private static float[]? ParseDashes(string text, Context context, float fontSize)
    {
        if (string.Equals(text.Trim(), "none", StringComparison.OrdinalIgnoreCase))
            return null;

        // A percentage in a dash list refers to the viewport's normalised diagonal (SVG 1.1 §7.10), not its width.
        var values = text.Split([',', ' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(v => Length(v, context, 2, fontSize))
            .ToList();

        // F3: a list past the cap is no dash at all; the array is copied into a path effect for every stroke that uses it.
        if (values.Count == 0 || values.Count > context.Limits.Dashes || values.Any(v => v is null or < 0) || values.All(v => v == 0))
            return null;

        var dashes = values.Select(v => v!.Value).ToList();

        // An odd list repeats itself, per the specification; Skia wants an even count.
        if (dashes.Count % 2 == 1)
            dashes.AddRange([.. dashes]);

        return [.. dashes];
    }

    private static float? Opacity(string text) => Number(text) is { } n ? Math.Clamp(n, 0f, 1f) : null;

    // ------------------------------------------------------------------
    // Rendering
    // ------------------------------------------------------------------

    /// <summary>The union of everything drawn, in the root's user space, for a document that states no size.</summary>
    private sealed class BoundsTracker
    {
        private bool _any;
        public SKRect Rect { get; private set; }

        public void Add(SKRect rect)
        {
            // I1: every edge finite, not only the two that happened to be checked; a sizeless document is never reported infinite.
            if (!float.IsFinite(rect.Left) || !float.IsFinite(rect.Top) || !float.IsFinite(rect.Right) || !float.IsFinite(rect.Bottom) || rect.Width < 0 || rect.Height < 0)
                return;

            Rect = _any ? SKRect.Union(Rect, rect) : rect;
            _any = true;
        }
    }

    private sealed class Renderer(Context context, BoundsTracker bounds)
    {
        private int _elements;
        private int _gradientStopParses;
        private long _decodedPixels;
        private int _imagesSkipped;
        private int _typefaceLookups;
        private int _familyLookups;
        private int _opacityLayers;
        private int _opacityLayerDepth;
        private int _opacityFolds;
        private long _pathVerbs;
        private int _shapesSkipped;
        private readonly Stack<XElement> _useStack = new();
        private readonly SortedSet<string> _notShown = new(StringComparer.Ordinal);

        /// <summary>The kinds of feature used but not drawn, in alphabetical order (I2).</summary>
        public IReadOnlyList<string> NotShown => [.. _notShown];

        /// <summary>The typeface each (family list, bold, italic) resolved to (F6): one font-configuration query per combination, not per text run.</summary>
        private readonly Dictionary<(string Family, bool Bold, bool Italic), SKTypeface> _typefaces = [];

        /// <summary>Every embedded image decoded so far, by its <c>href</c> (F4): a <c>use</c> of one image sixteen times decodes it once, and the picture shares the one copy.</summary>
        private readonly Dictionary<string, SKImage?> _images = new(StringComparer.Ordinal);

        /// <summary>The parsed stops of every gradient used so far, by the id it was asked for (F2): a document with two thousand shapes on one gradient parses its stops once.</summary>
        private readonly Dictionary<string, List<(float Offset, SKColor Colour)>> _stops = new(StringComparer.Ordinal);

        public Statistics Statistics => new(_elements, _gradientStopParses, _decodedPixels, _imagesSkipped, _typefaceLookups, _familyLookups, _opacityLayers, _opacityFolds, _pathVerbs, _shapesSkipped, context.RuleCount, context.RulesIgnored);

        /// <summary>Whether a shape of <paramref name="verbs"/> more verbs fits the document's budget (F8), counting it when it does.</summary>
        private bool TakeVerbs(long verbs)
        {
            if (_pathVerbs + verbs > context.Limits.PathVerbs)
            {
                _shapesSkipped++;
                return false;
            }

            _pathVerbs += verbs;
            return true;
        }

        /// <summary>Releases the decoded images once the picture is recorded; the picture holds its own reference to each one it draws.</summary>
        public void ReleaseImages()
        {
            foreach (var image in _images.Values)
                image?.Dispose();

            _images.Clear();
        }

        public void DrawRoot(SKCanvas canvas, XElement root, SKSize? declared)
        {
            var style = Resolve(new Style(), root, context);

            if (declared is { } size && ViewBox(root) is { } viewBox)
            {
                var fit = Fit(viewBox, SKRect.Create(0, 0, size.Width, size.Height), root.Attribute("preserveAspectRatio")?.Value);
                canvas.Concat(ref fit);
            }

            DrawChildren(canvas, root, style, 0);
        }

        private void DrawChildren(SKCanvas canvas, XElement parent, Style style, int depth)
        {
            foreach (var child in parent.Elements())
                Draw(canvas, child, style, depth + 1);
        }

        private void Draw(SKCanvas canvas, XElement element, Style parentStyle, int depth)
        {
            context.Token.ThrowIfCancellationRequested();

            if (++_elements > context.Limits.Elements || depth > context.Limits.Nesting)
                return;

            if (element.Name.Namespace != Svg && element.Name.Namespace != XNamespace.None)
                return;

            var name = element.Name.LocalName;

            // Definitions are drawn only where they are used; styling and metadata draw nothing.
            if (name is "defs" or "symbol" or "linearGradient" or "radialGradient" or "clipPath" or "mask" or "marker" or "pattern" or "filter"
                or "style" or "title" or "desc" or "metadata" or "script")
                return;

            if (name is "foreignObject")
            {
                _notShown.Add(name);
                return;
            }

            var style = Resolve(parentStyle, element, context, out var properties);

            if (!style.Display)
                return;

            // I2: an effect this reader leaves off is reported, so the viewer can say the drawing is not shown whole.
            if (Refers(properties, "clip-path"))
                _notShown.Add("clipPath");

            if (Refers(properties, "mask"))
                _notShown.Add("mask");

            if (Refers(properties, "filter"))
                _notShown.Add("filter");

            if (Refers(properties, "marker-start") || Refers(properties, "marker-mid") || Refers(properties, "marker-end") || Refers(properties, "marker"))
                _notShown.Add("marker");

            var save = canvas.Save();
            var layered = false;

            try
            {
                if (ParseTransform(element.Attribute("transform")?.Value) is { } transform)
                    canvas.Concat(ref transform);

                if (style.Opacity < 1f)
                {
                    if (_opacityLayerDepth >= context.Limits.OpacityLayerDepth || _opacityLayers >= context.Limits.OpacityLayers)
                    {
                        // F7: past the caps the group's opacity is folded into its fill and stroke opacity, which its children inherit.
                        // Overlapping children then blend with each other where a layer would have blended them as one — a fidelity
                        // compromise only for a document with more layers than any drawing has, never a bitmap per layer at draw.
                        style = style with { FillOpacity = style.FillOpacity * style.Opacity, StrokeOpacity = style.StrokeOpacity * style.Opacity, Opacity = 1f };
                        _opacityFolds++;
                    }
                    else
                    {
                        using var layer = new SKPaint { Color = SKColors.Black.WithAlpha((byte)Math.Round(style.Opacity * 255)) };
                        canvas.SaveLayer(layer);
                        _opacityLayers++;
                        _opacityLayerDepth++;
                        layered = true;
                    }
                }

                switch (name)
                {
                    case "g":
                    case "a":
                        DrawChildren(canvas, element, style, depth);
                        break;

                    case "switch":
                        // No conditional attribute is evaluated: the first drawable child is the one drawn.
                        if (element.Elements().FirstOrDefault(e => Is(e, "g") || Is(e, "rect") || Is(e, "path") || Is(e, "text") || Is(e, "circle") || Is(e, "ellipse") || Is(e, "line") || Is(e, "polyline") || Is(e, "polygon") || Is(e, "image") || Is(e, "use") || Is(e, "svg")) is { } first)
                            Draw(canvas, first, style, depth + 1);
                        break;

                    case "svg":
                        DrawNestedSvg(canvas, element, style, depth);
                        break;

                    case "use":
                        DrawUse(canvas, element, style, depth);
                        break;

                    case "rect":
                    case "circle":
                    case "ellipse":
                    case "line":
                    case "polyline":
                    case "polygon":
                    case "path":
                        if (style.Visible && Shape(element, name, style) is { } path)
                        {
                            using (path)
                                DrawPath(canvas, path, style);
                        }
                        break;

                    case "text":
                        if (style.Visible)
                            DrawText(canvas, element, style);
                        break;

                    case "image":
                        if (style.Visible)
                            DrawImage(canvas, element, style);
                        break;

                    default:
                        break;
                }
            }
            finally
            {
                if (layered)
                    _opacityLayerDepth--;

                canvas.RestoreToCount(save);
            }
        }

        /// <summary>Whether <paramref name="properties"/> sets <paramref name="name"/> to a <c>url(…)</c> reference (an effect, rather than <c>none</c> or nothing).</summary>
        private static bool Refers(Dictionary<string, string> properties, string name) =>
            properties.TryGetValue(name, out var value) && value.TrimStart().StartsWith("url(", StringComparison.OrdinalIgnoreCase);

        private void DrawNestedSvg(SKCanvas canvas, XElement element, Style style, int depth)
        {
            var x = Length(element.Attribute("x")?.Value, context, 0, style.FontSize) ?? 0;
            var y = Length(element.Attribute("y")?.Value, context, 1, style.FontSize) ?? 0;
            canvas.Translate(x, y);

            var width = Length(element.Attribute("width")?.Value, context, 0, style.FontSize);
            var height = Length(element.Attribute("height")?.Value, context, 1, style.FontSize);

            if (width is { } w && height is { } h && w > 0 && h > 0)
            {
                // A nested svg shows only what lies within its viewport (overflow is hidden unless said otherwise), which is what
                // makes preserveAspectRatio="… slice" mean anything.
                if (!Overflows(element))
                    canvas.ClipRect(SKRect.Create(0, 0, w, h));

                if (ViewBox(element) is { } viewBox)
                {
                    var fit = Fit(viewBox, SKRect.Create(0, 0, w, h), element.Attribute("preserveAspectRatio")?.Value);
                    canvas.Concat(ref fit);
                }
            }

            DrawChildren(canvas, element, style, depth);
        }

        /// <summary>Whether a nested <c>svg</c> or <c>symbol</c> states <c>overflow: visible</c> (or <c>auto</c>), showing content outside its viewport.</summary>
        private static bool Overflows(XElement element)
        {
            var overflow = element.Attribute("overflow")?.Value;

            if (overflow is null && element.Attribute("style")?.Value is { } inline && ParseDeclarations(inline).TryGetValue("overflow", out var declared))
                overflow = declared;

            return overflow?.Trim().ToLowerInvariant() is "visible" or "auto";
        }

        private void DrawUse(SKCanvas canvas, XElement use, Style style, int depth)
        {
            var reference = Href(use);

            if (reference is null || !reference.StartsWith('#') || !context.ById.TryGetValue(reference[1..], out var target))
                return;

            if (_useStack.Count >= MaxUseDepth || _useStack.Contains(target) || target.AncestorsAndSelf().Contains(use))
                return;

            var x = Length(use.Attribute("x")?.Value, context, 0, style.FontSize) ?? 0;
            var y = Length(use.Attribute("y")?.Value, context, 1, style.FontSize) ?? 0;
            canvas.Translate(x, y);

            _useStack.Push(target);

            try
            {
                if (Is(target, "symbol"))
                {
                    var symbolStyle = Resolve(style, target, context);
                    var width = Length(use.Attribute("width")?.Value, context, 0, style.FontSize);
                    var height = Length(use.Attribute("height")?.Value, context, 1, style.FontSize);

                    if (width is { } w && height is { } h && w > 0 && h > 0)
                    {
                        // A symbol used at a size is a viewport like a nested svg: clipped to it, its viewBox fitted into it.
                        if (!Overflows(target))
                            canvas.ClipRect(SKRect.Create(0, 0, w, h));

                        if (ViewBox(target) is { } viewBox)
                        {
                            var fit = Fit(viewBox, SKRect.Create(0, 0, w, h), target.Attribute("preserveAspectRatio")?.Value);
                            canvas.Concat(ref fit);
                        }
                    }

                    DrawChildren(canvas, target, symbolStyle, depth);
                }
                else
                {
                    Draw(canvas, target, style, depth + 1);
                }
            }
            finally
            {
                _useStack.Pop();
            }
        }

        private SKPath? Shape(XElement element, string name, Style style)
        {
            float L(string attribute, int axis, float fallback = 0) => Length(element.Attribute(attribute)?.Value, context, axis, style.FontSize) ?? fallback;

            var path = new SKPath { FillType = style.EvenOdd ? SKPathFillType.EvenOdd : SKPathFillType.Winding };

            switch (name)
            {
                case "rect":
                {
                    var w = L("width", 0);
                    var h = L("height", 1);

                    if (w <= 0 || h <= 0)
                        break;

                    var rect = SKRect.Create(L("x", 0), L("y", 1), w, h);
                    var hasRx = element.Attribute("rx") is not null;
                    var hasRy = element.Attribute("ry") is not null;
                    var rx = hasRx ? L("rx", 0) : hasRy ? L("ry", 1) : 0;
                    var ry = hasRy ? L("ry", 1) : rx;
                    rx = Math.Min(Math.Max(0, rx), w / 2);
                    ry = Math.Min(Math.Max(0, ry), h / 2);

                    if (rx > 0 && ry > 0)
                        path.AddRoundRect(rect, rx, ry);
                    else
                        path.AddRect(rect);

                    return path;
                }

                case "circle":
                {
                    var r = L("r", 2);

                    if (r <= 0)
                        break;

                    path.AddCircle(L("cx", 0), L("cy", 1), r);
                    return path;
                }

                case "ellipse":
                {
                    var rx = L("rx", 0);
                    var ry = L("ry", 1);

                    if (rx <= 0 || ry <= 0)
                        break;

                    var cx = L("cx", 0);
                    var cy = L("cy", 1);
                    path.AddOval(new SKRect(cx - rx, cy - ry, cx + rx, cy + ry));
                    return path;
                }

                case "line":
                    path.MoveTo(L("x1", 0), L("y1", 1));
                    path.LineTo(L("x2", 0), L("y2", 1));
                    return path;

                case "polyline":
                case "polygon":
                {
                    var text = element.Attribute("points")?.Value;

                    if (text is { Length: var length } && length > context.Limits.ShapeDataCharacters)
                    {
                        _shapesSkipped++;
                        break;
                    }

                    var numbers = Numbers(text);

                    if (numbers.Count < 4 || !TakeVerbs(numbers.Count / 2))
                        break;

                    var points = new SKPoint[numbers.Count / 2];

                    for (var i = 0; i < points.Length; i++)
                        points[i] = new SKPoint(numbers[2 * i], numbers[(2 * i) + 1]);

                    path.AddPoly(points, name == "polygon");
                    return path;
                }

                case "path":
                {
                    var data = element.Attribute("d")?.Value;

                    if (string.IsNullOrWhiteSpace(data))
                        break;

                    if (data.Length > context.Limits.ShapeDataCharacters)
                    {
                        _shapesSkipped++;
                        break;
                    }

                    var parsed = SKPath.ParseSvgPathData(data);

                    if (parsed is null)
                        break;

                    if (!TakeVerbs(parsed.VerbCount))
                    {
                        parsed.Dispose();
                        break;
                    }

                    parsed.FillType = path.FillType;
                    path.Dispose();
                    return parsed;
                }

                default:
                    break;
            }

            path.Dispose();
            return null;
        }

        private void DrawPath(SKCanvas canvas, SKPath path, Style style)
        {
            var shapeBounds = path.TightBounds;
            bounds.Add(canvas.TotalMatrix.MapRect(path.Bounds));

            if (!style.Fill.IsNone)
            {
                using var fill = MakePaint(style.Fill, style.FillOpacity, shapeBounds);

                if (fill is not null)
                {
                    fill.Style = SKPaintStyle.Fill;
                    canvas.DrawPath(path, fill);
                }
            }

            if (!style.Stroke.IsNone && style.StrokeWidth > 0)
            {
                using var stroke = MakePaint(style.Stroke, style.StrokeOpacity, shapeBounds);

                if (stroke is not null)
                {
                    stroke.Style = SKPaintStyle.Stroke;
                    stroke.StrokeWidth = style.StrokeWidth;
                    stroke.StrokeCap = style.Cap;
                    stroke.StrokeJoin = style.Join;
                    stroke.StrokeMiter = style.MiterLimit;

                    if (style.Dashes is { } dashes)
                        stroke.PathEffect = SKPathEffect.CreateDash(dashes, style.DashOffset);

                    canvas.DrawPath(path, stroke);
                    bounds.Add(canvas.TotalMatrix.MapRect(SKRect.Inflate(path.Bounds, style.StrokeWidth, style.StrokeWidth)));
                }
            }
        }

        /// <summary>A paint for <paramref name="paint"/>, or <see langword="null"/> when it paints nothing (a gradient that cannot be found and has no fallback).</summary>
        private SKPaint? MakePaint(Paint paint, float opacity, SKRect shapeBounds)
        {
            var alpha = (byte)Math.Round(Math.Clamp(opacity, 0f, 1f) * 255);

            if (paint.GradientId is { } id)
            {
                if (Gradient(id, shapeBounds, alpha) is { } shader)
                    return new SKPaint { IsAntialias = true, Shader = shader };

                if (paint.Colour is null)
                    return null;
            }

            return paint.Colour is { } colour
                ? new SKPaint { IsAntialias = true, Color = colour.WithAlpha((byte)(colour.Alpha * alpha / 255)) }
                : null;
        }

        private SKShader? Gradient(string id, SKRect shapeBounds, byte alpha)
        {
            if (!context.ById.TryGetValue(id, out var element))
                return null;

            if (Is(element, "pattern"))
            {
                _notShown.Add("pattern");
                return null;
            }

            if (!(Is(element, "linearGradient") || Is(element, "radialGradient")))
                return null;

            if (!_stops.TryGetValue(id, out var stops))
            {
                stops = Stops(element, 0);
                _stops[id] = stops;
                _gradientStopParses++;
            }

            if (stops.Count == 0)
                return null;

            if (stops.Count == 1)
                return SKShader.CreateColor(stops[0].Colour.WithAlpha((byte)(stops[0].Colour.Alpha * alpha / 255)));

            var colours = stops.Select(s => s.Colour.WithAlpha((byte)(s.Colour.Alpha * alpha / 255))).ToArray();
            var positions = stops.Select(s => s.Offset).ToArray();
            var userSpace = string.Equals(Inherited(element, "gradientUnits", 0), "userSpaceOnUse", StringComparison.Ordinal);
            var tile = (Inherited(element, "spreadMethod", 0) ?? "pad") switch { "reflect" => SKShaderTileMode.Mirror, "repeat" => SKShaderTileMode.Repeat, _ => SKShaderTileMode.Clamp };
            var local = ParseTransform(Inherited(element, "gradientTransform", 0)) ?? SKMatrix.Identity;

            if (!userSpace)
            {
                if (shapeBounds.Width <= 0 || shapeBounds.Height <= 0)
                    return SKShader.CreateColor(colours[^1]);

                // Object-bounding-box units: the gradient is defined on the unit square, mapped onto the shape's bounds (the transform applies inside that square).
                var toBounds = SKMatrix.CreateScaleTranslation(shapeBounds.Width, shapeBounds.Height, shapeBounds.Left, shapeBounds.Top);
                local = toBounds.PreConcat(local);
            }

            if (Is(element, "linearGradient"))
            {
                var x1 = Coordinate(Inherited(element, "x1", 0), 0f, userSpace, 0);
                var y1 = Coordinate(Inherited(element, "y1", 0), 0f, userSpace, 1);
                var x2 = Coordinate(Inherited(element, "x2", 0), 1f, userSpace, 0);
                var y2 = Coordinate(Inherited(element, "y2", 0), 0f, userSpace, 1);
                return SKShader.CreateLinearGradient(new SKPoint(x1, y1), new SKPoint(x2, y2), colours, positions, tile, local);
            }

            var cx = Coordinate(Inherited(element, "cx", 0), 0.5f, userSpace, 0);
            var cy = Coordinate(Inherited(element, "cy", 0), 0.5f, userSpace, 1);
            var r = Coordinate(Inherited(element, "r", 0), 0.5f, userSpace, 2);
            var fx = Coordinate(Inherited(element, "fx", 0), cx, userSpace, 0);
            var fy = Coordinate(Inherited(element, "fy", 0), cy, userSpace, 1);

            if (r <= 0)
                return SKShader.CreateColor(colours[^1]);

            return fx == cx && fy == cy
                ? SKShader.CreateRadialGradient(new SKPoint(cx, cy), r, colours, positions, tile, local)
                : SKShader.CreateTwoPointConicalGradient(new SKPoint(fx, fy), 0, new SKPoint(cx, cy), r, colours, positions, tile, local);
        }

        private float Coordinate(string? text, float fallback, bool userSpace, int axis)
        {
            if (string.IsNullOrWhiteSpace(text))
                return fallback;

            text = text.Trim();

            if (text.EndsWith('%'))
                return Number(text[..^1]) is { } percent ? (userSpace ? percent / 100f * context.Viewport(axis) : percent / 100f) : fallback;

            return (userSpace ? Length(text, context, axis, DefaultFontSize) : Number(text)) ?? fallback;
        }

        /// <summary>A gradient attribute, read through the <c>href</c> chain when this gradient does not state it.</summary>
        private string? Inherited(XElement gradient, string attribute, int depth)
        {
            if (gradient.Attribute(attribute)?.Value is { } own)
                return own;

            if (depth >= MaxUseDepth || Href(gradient) is not { } href || !href.StartsWith('#') || !context.ById.TryGetValue(href[1..], out var parent))
                return null;

            return (Is(parent, "linearGradient") || Is(parent, "radialGradient")) ? Inherited(parent, attribute, depth + 1) : null;
        }

        private List<(float Offset, SKColor Colour)> Stops(XElement gradient, int depth)
        {
            var stops = new List<(float, SKColor)>();
            var last = 0f;

            foreach (var stop in gradient.Elements().Where(e => Is(e, "stop")))
            {
                context.Token.ThrowIfCancellationRequested();

                // F2: a stop list past the cap is cut, not refused: the shader takes the first stops, which is the gradient to the eye.
                if (stops.Count >= context.Limits.GradientStops)
                    break;

                var p = Properties(stop, context);
                var offsetText = (stop.Attribute("offset")?.Value ?? "0").Trim();
                var offset = offsetText.EndsWith('%') ? (Number(offsetText[..^1]) ?? 0) / 100f : Number(offsetText) ?? 0;
                offset = Math.Clamp(offset, 0f, 1f);
                offset = Math.Max(offset, last);
                last = offset;
                var colour = p.TryGetValue("stop-color", out var c) ? ParseColour(c, SKColors.Black) ?? SKColors.Black : SKColors.Black;

                if (p.TryGetValue("stop-opacity", out var o) && Opacity(o) is { } opacity)
                    colour = colour.WithAlpha((byte)Math.Round(colour.Alpha * opacity));

                stops.Add((offset, colour));
            }

            if (stops.Count > 0 || depth >= MaxUseDepth || Href(gradient) is not { } href || !href.StartsWith('#') || !context.ById.TryGetValue(href[1..], out var parent))
                return stops;

            return (Is(parent, "linearGradient") || Is(parent, "radialGradient")) ? Stops(parent, depth + 1) : stops;
        }

        private void DrawText(SKCanvas canvas, XElement text, Style style)
        {
            var x = Length(text.Attribute("x")?.Value, context, 0, style.FontSize) ?? 0;
            var y = Length(text.Attribute("y")?.Value, context, 1, style.FontSize) ?? 0;
            x += Length(text.Attribute("dx")?.Value, context, 0, style.FontSize) ?? 0;
            y += Length(text.Attribute("dy")?.Value, context, 1, style.FontSize) ?? 0;

            // The runs of the element: its own text and each tspan, each with the style it resolves to.
            var runs = new List<(string Text, Style Style, float? X, float? Y, float Dx, float Dy)>();
            Collect(text, style, runs, 0);

            if (runs.Count == 0)
                return;

            // Anchor applies to the whole line when no run restarts the position; a run with its own x or y starts a new line.
            var lines = new List<List<(string Text, Style Style, float? X, float? Y, float Dx, float Dy)>>();

            foreach (var run in runs)
            {
                if (lines.Count == 0 || run.X is not null || run.Y is not null)
                    lines.Add([]);

                lines[^1].Add(run);
            }

            var penX = x;
            var penY = y;

            foreach (var line in lines)
            {
                var first = line[0];
                penX = first.X ?? penX;
                penY = first.Y ?? penY;
                var width = 0f;
                var measured = new List<(SKPaint? Paint, string Text, float Width, float Dx, float Dy, float Size)>();

                foreach (var run in line)
                {
                    var typeface = Typeface(run.Style);
                    var paint = run.Style.Fill.IsNone ? null : MakePaint(run.Style.Fill, run.Style.FillOpacity, SKRect.Empty);
                    using var measure = new SKPaint { Typeface = typeface, TextSize = run.Style.FontSize };
                    var w = measure.MeasureText(run.Text);

                    if (paint is not null)
                    {
                        paint.Typeface = typeface;
                        paint.TextSize = run.Style.FontSize;
                        paint.SubpixelText = true;
                        paint.Style = SKPaintStyle.Fill;
                    }

                    measured.Add((paint, run.Text, w, run.Dx, run.Dy, run.Style.FontSize));
                    width += w + run.Dx;
                }

                var cursor = penX - (first.Style.Anchor switch { 1 => width / 2, 2 => width, _ => 0 });

                foreach (var (paint, run, w, dx, dy, size) in measured)
                {
                    cursor += dx;
                    penY += dy;

                    if (paint is not null && run.Length > 0)
                    {
                        canvas.DrawText(run, cursor, penY, paint);
                        bounds.Add(canvas.TotalMatrix.MapRect(new SKRect(cursor, penY - size, cursor + w, penY + (size * 0.3f))));
                    }

                    cursor += w;
                    paint?.Dispose();
                }

                penX = cursor;
            }
        }

        private void Collect(XElement element, Style style, List<(string, Style, float?, float?, float, float)> runs, int depth)
        {
            if (depth > context.Limits.Nesting)
                return;

            foreach (var node in element.Nodes())
            {
                context.Token.ThrowIfCancellationRequested();

                switch (node)
                {
                    case XText t:
                    {
                        var collapsed = Collapse(t.Value);

                        if (collapsed.Length > 0)
                            runs.Add((collapsed, style, null, null, 0, 0));
                        break;
                    }

                    case XElement e when Is(e, "textPath"):
                        // I2: text on a path is not laid out by this reader; it is left out and reported rather than drawn in a straight line somewhere.
                        _notShown.Add("textPath");
                        break;

                    case XElement e when Is(e, "tspan"):
                    {
                        var spanStyle = Resolve(style, e, context);

                        if (!spanStyle.Display || !spanStyle.Visible)
                            break;

                        var sx = Length(e.Attribute("x")?.Value, context, 0, spanStyle.FontSize);
                        var sy = Length(e.Attribute("y")?.Value, context, 1, spanStyle.FontSize);
                        var dx = Length(e.Attribute("dx")?.Value, context, 0, spanStyle.FontSize) ?? 0;
                        var dy = Length(e.Attribute("dy")?.Value, context, 1, spanStyle.FontSize) ?? 0;
                        var before = runs.Count;
                        Collect(e, spanStyle, runs, depth + 1);

                        if (runs.Count > before)
                        {
                            var head = runs[before];
                            runs[before] = (head.Item1, head.Item2, sx ?? head.Item3, sy ?? head.Item4, dx + head.Item5, dy + head.Item6);
                        }
                        break;
                    }

                    default:
                        break;
                }
            }
        }

        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.NonBacktracking, MatchTimeout);

        private static string Collapse(string text) => Whitespace.Replace(text, " ");

        /// <summary>The first installed face the style's family list names (of at most <see cref="Limits.FontFamilies"/>), else the platform's default face in that weight and slant; remembered per combination (F6).</summary>
        private SKTypeface Typeface(Style style)
        {
            var key = (style.FontFamily, style.Bold, style.Italic);

            if (_typefaces.TryGetValue(key, out var cached))
                return cached;

            _typefaceLookups++;
            var fontStyle = new SKFontStyle(style.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, style.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
            SKTypeface? found = null;

            foreach (var family in style.FontFamily.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(context.Limits.FontFamilies))
            {
                _familyLookups++;

                if (SKFontManager.Default.MatchFamily(family.Trim('"', '\''), fontStyle) is { } match)
                {
                    found = match;
                    break;
                }
            }

            found ??= SKTypeface.FromFamilyName(null, fontStyle) ?? SKTypeface.Default;
            _typefaces[key] = found;
            return found;
        }

        private void DrawImage(SKCanvas canvas, XElement image, Style style)
        {
            var href = Href(image);

            // Only an embedded image is drawn; a reference to anywhere else is never followed (the sanitiser blanks it, and this reader has no fetch path).
            if (href is null || !href.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return;

            if (!_images.TryGetValue(href, out var decoded))
            {
                decoded = Decode(href);
                _images[href] = decoded;
            }

            if (decoded is null)
                return;

            var x = Length(image.Attribute("x")?.Value, context, 0, style.FontSize) ?? 0;
            var y = Length(image.Attribute("y")?.Value, context, 1, style.FontSize) ?? 0;
            var width = Length(image.Attribute("width")?.Value, context, 0, style.FontSize) ?? decoded.Width;
            var height = Length(image.Attribute("height")?.Value, context, 1, style.FontSize) ?? decoded.Height;

            if (width <= 0 || height <= 0)
                return;

            var dest = Fit(SKRect.Create(0, 0, decoded.Width, decoded.Height), SKRect.Create(x, y, width, height), image.Attribute("preserveAspectRatio")?.Value)
                .MapRect(SKRect.Create(0, 0, decoded.Width, decoded.Height));
            using var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.Medium };

            // An immutable image is shared by the picture; a bitmap would be copied into it at every draw (F4).
            canvas.DrawImage(decoded, dest, paint);
            bounds.Add(canvas.TotalMatrix.MapRect(dest));
        }

        /// <summary>
        /// The decoded form of an embedded <c>data:</c> image, or <see langword="null"/> when it is not one this reader draws: not base64, over
        /// <see cref="Limits.ImageBytes"/>, not an image Skia decodes, an edge over <see cref="Limits.ImageEdge"/>, or more pixels than the
        /// document has left of <see cref="Limits.DecodedPixels"/> (F4).
        /// </summary>
        private SKImage? Decode(string href)
        {
            var comma = href.IndexOf(',', StringComparison.Ordinal);

            if (comma < 0 || !href[..comma].Contains(";base64", StringComparison.OrdinalIgnoreCase))
                return null;

            var payload = href[(comma + 1)..].Trim();

            if (payload.Length == 0 || payload.Length > context.Limits.ImageBytes * 4L / 3 + 4)
                return null;

            byte[] bytes;

            try
            {
                bytes = Convert.FromBase64String(payload);
            }
            catch (FormatException)
            {
                return null;
            }

            using var codec = SKCodec.Create(new MemoryStream(bytes));

            if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0 || codec.Info.Width > context.Limits.ImageEdge || codec.Info.Height > context.Limits.ImageEdge)
                return null;

            var pixels = (long)codec.Info.Width * codec.Info.Height;

            if (_decodedPixels + pixels > context.Limits.DecodedPixels)
            {
                _imagesSkipped++;
                return null;
            }

            using var bitmap = SKBitmap.Decode(codec);

            if (bitmap is null)
                return null;

            _decodedPixels += pixels;
            bitmap.SetImmutable();
            return SKImage.FromBitmap(bitmap);
        }
    }

    /// <summary>The transform that fits <paramref name="viewBox"/> into <paramref name="viewport"/> under <paramref name="preserveAspectRatio"/> (default <c>xMidYMid meet</c>).</summary>
    private static SKMatrix Fit(SKRect viewBox, SKRect viewport, string? preserveAspectRatio)
    {
        var parts = (preserveAspectRatio ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var align = parts.FirstOrDefault(p => p.StartsWith('x') || p == "none") ?? "xMidYMid";
        var slice = parts.Contains("slice", StringComparer.Ordinal);

        if (viewBox.Width <= 0 || viewBox.Height <= 0)
            return SKMatrix.Identity;

        float sx = viewport.Width / viewBox.Width, sy = viewport.Height / viewBox.Height;

        if (align != "none")
        {
            var s = slice ? Math.Max(sx, sy) : Math.Min(sx, sy);
            sx = sy = s;
        }

        var tx = viewport.Left - (viewBox.Left * sx);
        var ty = viewport.Top - (viewBox.Top * sy);

        if (align != "none")
        {
            var spareX = viewport.Width - (viewBox.Width * sx);
            var spareY = viewport.Height - (viewBox.Height * sy);
            tx += align.Contains("xMid", StringComparison.Ordinal) ? spareX / 2 : align.Contains("xMax", StringComparison.Ordinal) ? spareX : 0;
            ty += align.Contains("YMid", StringComparison.Ordinal) ? spareY / 2 : align.Contains("YMax", StringComparison.Ordinal) ? spareY : 0;
        }

        return SKMatrix.CreateScaleTranslation(sx, sy, tx, ty);
    }

    private static string? Href(XElement element) => element.Attribute("href")?.Value?.Trim() ?? element.Attribute(Xlink + "href")?.Value?.Trim();

    // ------------------------------------------------------------------
    // Values: numbers, lengths, colours, transforms
    // ------------------------------------------------------------------

    private static float? Number(string? text) =>
        text is not null && float.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && float.IsFinite(value) ? value : null;

    /// <summary>
    /// Every number in <paramref name="text"/> (a <c>points</c>, <c>viewBox</c> or transform argument list): an optional sign, digits with an
    /// optional point (or a point and digits), and an exponent only when digits follow it — the shape <c>[-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?</c>,
    /// scanned over the text without a match object per number (F8: five million numbers made as many allocations).
    /// </summary>
    internal static List<float> Numbers(string? text)
    {
        var numbers = new List<float>();

        if (text is null)
            return numbers;

        var span = text.AsSpan();
        var i = 0;

        while (i < span.Length)
        {
            var start = i;

            if (span[i] is '+' or '-')
                i++;

            var before = 0;

            while (i < span.Length && char.IsAsciiDigit(span[i]))
            {
                i++;
                before++;
            }

            var after = 0;

            if (i < span.Length && span[i] == '.')
            {
                i++;

                while (i < span.Length && char.IsAsciiDigit(span[i]))
                {
                    i++;
                    after++;
                }
            }

            if (before == 0 && after == 0)
            {
                // No number starts here: look again one character on, as the pattern would.
                i = start + 1;
                continue;
            }

            if (i < span.Length && span[i] is 'e' or 'E')
            {
                var exponent = i + 1;

                if (exponent < span.Length && span[exponent] is '+' or '-')
                    exponent++;

                if (exponent < span.Length && char.IsAsciiDigit(span[exponent]))
                {
                    i = exponent;

                    while (i < span.Length && char.IsAsciiDigit(span[i]))
                        i++;
                }
            }

            if (float.TryParse(span[start..i], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && float.IsFinite(value))
                numbers.Add(value);
        }

        return numbers;
    }

    /// <summary>A length in user units (pixels at 96 per inch); <paramref name="axis"/> 0, 1 or 2 says which viewport edge a percentage refers to (width, height, diagonal).</summary>
    private static float? Length(string? text, Context context, int axis, float fontSize)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Trim();
        var i = text.Length;

        while (i > 0 && (char.IsLetter(text[i - 1]) || text[i - 1] == '%'))
            i--;

        if (Number(text[..i]) is not { } value)
            return null;

        var unit = text[i..].ToLowerInvariant();
        float? length = unit switch
        {
            "" or "px" => value,
            "pt" => value * PixelsPerInch / 72f,
            "pc" => value * PixelsPerInch / 6f,
            "mm" => value * PixelsPerInch / 25.4f,
            "cm" => value * PixelsPerInch / 2.54f,
            "in" => value * PixelsPerInch,
            "em" => value * fontSize,
            "ex" => value * fontSize / 2f,
            "%" => value / 100f * context.Viewport(axis),
            _ => null,
        };

        // I1: a value that the unit takes past a float (-1e38in, an em chain) is no length, so the attribute falls back rather than placing a point at infinity.
        return length is { } l && float.IsFinite(l) ? l : null;
    }

    private static readonly Regex UrlPattern = new(@"^url\(\s*['""]?#([^'""\)\s]+)['""]?\s*\)\s*(.*)$", RegexOptions.NonBacktracking, MatchTimeout);

    private static Paint ParsePaint(string text, SKColor currentColor, Paint inherited)
    {
        text = text.Trim();

        if (text.Length == 0 || string.Equals(text, "inherit", StringComparison.OrdinalIgnoreCase))
            return inherited;

        if (string.Equals(text, "none", StringComparison.OrdinalIgnoreCase))
            return Paint.None;

        if (UrlPattern.Match(text) is { Success: true } url)
        {
            var fallback = url.Groups[2].Value.Trim();
            var colour = fallback.Length > 0 && !string.Equals(fallback, "none", StringComparison.OrdinalIgnoreCase) ? ParseColour(fallback, currentColor) : null;
            return new Paint(colour, url.Groups[1].Value);
        }

        return ParseColour(text, currentColor) is { } c ? new Paint(c, null) : inherited;
    }

    /// <summary>No lookaround or backreference, so the non-backtracking engine (linear in the input) reads it; before, a long run of spaces inside <c>rgb()</c> was tried in quadratically many ways (F1).</summary>
    private static readonly Regex RgbPattern = new(@"^rgba?\(\s*([^,\s/]+)\s*[,\s]\s*([^,\s/]+)\s*[,\s]\s*([^,\s/\)]+)\s*(?:[,/]\s*([^\s\)]+)\s*)?\)$", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking, MatchTimeout);

    /// <summary>The longest text that can be a CSS colour (<c>rgba(100%, 100%, 100%, 100%)</c> is 29 characters); anything longer is not one and is not examined (F1).</summary>
    public const int MaxColourLength = 64;

    /// <summary>A CSS colour, or <see langword="null"/> when <paramref name="text"/> is not one.</summary>
    internal static SKColor? ParseColour(string text, SKColor currentColor)
    {
        text = text.Trim();

        if (text.Length == 0 || text.Length > MaxColourLength)
            return null;

        if (string.Equals(text, "currentColor", StringComparison.OrdinalIgnoreCase))
            return currentColor;

        if (string.Equals(text, "transparent", StringComparison.OrdinalIgnoreCase))
            return new SKColor(0, 0, 0, 0);

        if (text[0] == '#')
        {
            var hex = text[1..];

            if (!hex.All(Uri.IsHexDigit))
                return null;

            static byte Pair(string s, int i) => byte.Parse(s.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            static byte Single(char c) => (byte)(Convert.ToByte(c.ToString(), 16) * 17);

            return hex.Length switch
            {
                3 => new SKColor(Single(hex[0]), Single(hex[1]), Single(hex[2])),
                4 => new SKColor(Single(hex[0]), Single(hex[1]), Single(hex[2]), Single(hex[3])),
                6 => new SKColor(Pair(hex, 0), Pair(hex, 2), Pair(hex, 4)),
                8 => new SKColor(Pair(hex, 0), Pair(hex, 2), Pair(hex, 4), Pair(hex, 6)),
                _ => null,
            };
        }

        if (RgbPattern.Match(text) is { Success: true } rgb)
        {
            static byte Channel(string s)
            {
                s = s.Trim();
                var percent = s.EndsWith('%');
                var n = Number(percent ? s[..^1] : s) ?? 0;
                return (byte)Math.Clamp(Math.Round(percent ? n / 100f * 255f : n), 0, 255);
            }

            var alpha = (byte)255;

            if (rgb.Groups[4].Success)
            {
                var a = rgb.Groups[4].Value.Trim();
                var n = a.EndsWith('%') ? (Number(a[..^1]) ?? 100) / 100f : Number(a) ?? 1f;
                alpha = (byte)Math.Clamp(Math.Round(n * 255), 0, 255);
            }

            return new SKColor(Channel(rgb.Groups[1].Value), Channel(rgb.Groups[2].Value), Channel(rgb.Groups[3].Value), alpha);
        }

        return NamedColours.TryGetValue(text, out var named) ? named : null;
    }

    private static readonly Regex TransformPattern = new(@"(matrix|translate|scale|rotate|skewX|skewY)\s*\(([^)]*)\)", RegexOptions.NonBacktracking, MatchTimeout);

    /// <summary>An SVG transform list as one matrix, or <see langword="null"/> when there is none.</summary>
    internal static SKMatrix? ParseTransform(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var matrix = SKMatrix.Identity;
        var any = false;

        foreach (Match m in TransformPattern.Matches(text))
        {
            var a = Numbers(m.Groups[2].Value);
            SKMatrix op;

            switch (m.Groups[1].Value)
            {
                case "matrix" when a.Count == 6:
                    op = new SKMatrix(a[0], a[2], a[4], a[1], a[3], a[5], 0, 0, 1);
                    break;

                case "translate" when a.Count is 1 or 2:
                    op = SKMatrix.CreateTranslation(a[0], a.Count == 2 ? a[1] : 0);
                    break;

                case "scale" when a.Count is 1 or 2:
                    op = SKMatrix.CreateScale(a[0], a.Count == 2 ? a[1] : a[0]);
                    break;

                case "rotate" when a.Count is 1 or 3:
                    op = a.Count == 3 ? SKMatrix.CreateRotationDegrees(a[0], a[1], a[2]) : SKMatrix.CreateRotationDegrees(a[0]);
                    break;

                case "skewX" when a.Count == 1:
                    op = SKMatrix.CreateSkew((float)Math.Tan(a[0] * Math.PI / 180), 0);
                    break;

                case "skewY" when a.Count == 1:
                    op = SKMatrix.CreateSkew(0, (float)Math.Tan(a[0] * Math.PI / 180));
                    break;

                default:
                    continue;
            }

            matrix = matrix.PreConcat(op);
            any = true;
        }

        return any ? matrix : null;
    }

    /// <summary>The CSS named colours.</summary>
    private static readonly Dictionary<string, SKColor> NamedColours = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aliceblue"] = new(240, 248, 255), ["antiquewhite"] = new(250, 235, 215), ["aqua"] = new(0, 255, 255), ["aquamarine"] = new(127, 255, 212),
        ["azure"] = new(240, 255, 255), ["beige"] = new(245, 245, 220), ["bisque"] = new(255, 228, 196), ["black"] = new(0, 0, 0),
        ["blanchedalmond"] = new(255, 235, 205), ["blue"] = new(0, 0, 255), ["blueviolet"] = new(138, 43, 226), ["brown"] = new(165, 42, 42),
        ["burlywood"] = new(222, 184, 135), ["cadetblue"] = new(95, 158, 160), ["chartreuse"] = new(127, 255, 0), ["chocolate"] = new(210, 105, 30),
        ["coral"] = new(255, 127, 80), ["cornflowerblue"] = new(100, 149, 237), ["cornsilk"] = new(255, 248, 220), ["crimson"] = new(220, 20, 60),
        ["cyan"] = new(0, 255, 255), ["darkblue"] = new(0, 0, 139), ["darkcyan"] = new(0, 139, 139), ["darkgoldenrod"] = new(184, 134, 11),
        ["darkgray"] = new(169, 169, 169), ["darkgreen"] = new(0, 100, 0), ["darkgrey"] = new(169, 169, 169), ["darkkhaki"] = new(189, 183, 107),
        ["darkmagenta"] = new(139, 0, 139), ["darkolivegreen"] = new(85, 107, 47), ["darkorange"] = new(255, 140, 0), ["darkorchid"] = new(153, 50, 204),
        ["darkred"] = new(139, 0, 0), ["darksalmon"] = new(233, 150, 122), ["darkseagreen"] = new(143, 188, 143), ["darkslateblue"] = new(72, 61, 139),
        ["darkslategray"] = new(47, 79, 79), ["darkslategrey"] = new(47, 79, 79), ["darkturquoise"] = new(0, 206, 209), ["darkviolet"] = new(148, 0, 211),
        ["deeppink"] = new(255, 20, 147), ["deepskyblue"] = new(0, 191, 255), ["dimgray"] = new(105, 105, 105), ["dimgrey"] = new(105, 105, 105),
        ["dodgerblue"] = new(30, 144, 255), ["firebrick"] = new(178, 34, 34), ["floralwhite"] = new(255, 250, 240), ["forestgreen"] = new(34, 139, 34),
        ["fuchsia"] = new(255, 0, 255), ["gainsboro"] = new(220, 220, 220), ["ghostwhite"] = new(248, 248, 255), ["gold"] = new(255, 215, 0),
        ["goldenrod"] = new(218, 165, 32), ["gray"] = new(128, 128, 128), ["green"] = new(0, 128, 0), ["greenyellow"] = new(173, 255, 47),
        ["grey"] = new(128, 128, 128), ["honeydew"] = new(240, 255, 240), ["hotpink"] = new(255, 105, 180), ["indianred"] = new(205, 92, 92),
        ["indigo"] = new(75, 0, 130), ["ivory"] = new(255, 255, 240), ["khaki"] = new(240, 230, 140), ["lavender"] = new(230, 230, 250),
        ["lavenderblush"] = new(255, 240, 245), ["lawngreen"] = new(124, 252, 0), ["lemonchiffon"] = new(255, 250, 205), ["lightblue"] = new(173, 216, 230),
        ["lightcoral"] = new(240, 128, 128), ["lightcyan"] = new(224, 255, 255), ["lightgoldenrodyellow"] = new(250, 250, 210), ["lightgray"] = new(211, 211, 211),
        ["lightgreen"] = new(144, 238, 144), ["lightgrey"] = new(211, 211, 211), ["lightpink"] = new(255, 182, 193), ["lightsalmon"] = new(255, 160, 122),
        ["lightseagreen"] = new(32, 178, 170), ["lightskyblue"] = new(135, 206, 250), ["lightslategray"] = new(119, 136, 153), ["lightslategrey"] = new(119, 136, 153),
        ["lightsteelblue"] = new(176, 196, 222), ["lightyellow"] = new(255, 255, 224), ["lime"] = new(0, 255, 0), ["limegreen"] = new(50, 205, 50),
        ["linen"] = new(250, 240, 230), ["magenta"] = new(255, 0, 255), ["maroon"] = new(128, 0, 0), ["mediumaquamarine"] = new(102, 205, 170),
        ["mediumblue"] = new(0, 0, 205), ["mediumorchid"] = new(186, 85, 211), ["mediumpurple"] = new(147, 112, 219), ["mediumseagreen"] = new(60, 179, 113),
        ["mediumslateblue"] = new(123, 104, 238), ["mediumspringgreen"] = new(0, 250, 154), ["mediumturquoise"] = new(72, 209, 204), ["mediumvioletred"] = new(199, 21, 133),
        ["midnightblue"] = new(25, 25, 112), ["mintcream"] = new(245, 255, 250), ["mistyrose"] = new(255, 228, 225), ["moccasin"] = new(255, 228, 181),
        ["navajowhite"] = new(255, 222, 173), ["navy"] = new(0, 0, 128), ["oldlace"] = new(253, 245, 230), ["olive"] = new(128, 128, 0),
        ["olivedrab"] = new(107, 142, 35), ["orange"] = new(255, 165, 0), ["orangered"] = new(255, 69, 0), ["orchid"] = new(218, 112, 214),
        ["palegoldenrod"] = new(238, 232, 170), ["palegreen"] = new(152, 251, 152), ["paleturquoise"] = new(175, 238, 238), ["palevioletred"] = new(219, 112, 147),
        ["papayawhip"] = new(255, 239, 213), ["peachpuff"] = new(255, 218, 185), ["peru"] = new(205, 133, 63), ["pink"] = new(255, 192, 203),
        ["plum"] = new(221, 160, 221), ["powderblue"] = new(176, 224, 230), ["purple"] = new(128, 0, 128), ["rebeccapurple"] = new(102, 51, 153),
        ["red"] = new(255, 0, 0), ["rosybrown"] = new(188, 143, 143), ["royalblue"] = new(65, 105, 225), ["saddlebrown"] = new(139, 69, 19),
        ["salmon"] = new(250, 128, 114), ["sandybrown"] = new(244, 164, 96), ["seagreen"] = new(46, 139, 87), ["seashell"] = new(255, 245, 238),
        ["sienna"] = new(160, 82, 45), ["silver"] = new(192, 192, 192), ["skyblue"] = new(135, 206, 235), ["slateblue"] = new(106, 90, 205),
        ["slategray"] = new(112, 128, 144), ["slategrey"] = new(112, 128, 144), ["snow"] = new(255, 250, 250), ["springgreen"] = new(0, 255, 127),
        ["steelblue"] = new(70, 130, 180), ["tan"] = new(210, 180, 140), ["teal"] = new(0, 128, 128), ["thistle"] = new(216, 191, 216),
        ["tomato"] = new(255, 99, 71), ["turquoise"] = new(64, 224, 208), ["violet"] = new(238, 130, 238), ["wheat"] = new(245, 222, 179),
        ["white"] = new(255, 255, 255), ["whitesmoke"] = new(245, 245, 245), ["yellow"] = new(255, 255, 0), ["yellowgreen"] = new(154, 205, 50),
    };
}
