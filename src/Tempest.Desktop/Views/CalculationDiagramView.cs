using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Core.Calculations.Modules;
using Tempest.Core.Calculations.Modules.Diagrams;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The calculator reference diagram (`PO-2`, work package B): draws a
/// calculation's <see cref="CalculationDiagramSpec"/> from plain Avalonia
/// shapes — members, supports, load arrows, dimension arrows and labels —
/// against the form as it stands, so a label reads <c>L = 2000 mm</c>, or
/// <c>L = ?</c> while the span cannot be read.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not to scale, inputs only.</b> Every drawing says so, on the sheet
/// and in its screen-reader text. Nothing computed is drawn: no deflected
/// shape, no stress marker (the Product Owner's decision of 2026-10-01).
/// </para>
/// <para>
/// <b>Bound both ways.</b> <see cref="Highlight"/> marks every shape of one
/// input (the form calls it as a field takes focus); pointing at a bound
/// shape marks it too and raises <see cref="InputHovered"/>, and clicking
/// one raises <see cref="InputActivated"/>, so the form can mark or focus
/// the field the shape stands for.
/// </para>
/// <para>
/// <b>Honest where there is nothing to draw.</b> Every product calculation
/// has a diagram (Engineering Principle 33); should a descriptor ever reach
/// this control without one, it shows <see cref="NoDiagramYetText"/>,
/// never a generic picture.
/// </para>
/// <para>
/// <b>Theme tokens, re-read on every theme change.</b> Ink, plates, loads,
/// dimensions and the highlight are <see cref="BrandPalette"/> and
/// <see cref="ApplicationPalette"/> keys, resolved against this control's
/// own theme variant the way <see cref="ThemeReactiveBrush"/> resolves them.
/// A plate is shaded with the strong hairline (visible on the panel in
/// both themes); a solid part — a bolt, a pin, a point — is filled in ink,
/// so it never reads as a hole. Labels are set in the body face at
/// <see cref="CalculationDiagramSpec.LabelFontSize"/>, so <c>T₁</c> and
/// <c>m²</c> read as the form writes them (the monospaced face draws its
/// subscripts too small to read).
/// </para>
/// <para>
/// <b>One thing for a screen reader.</b> The control carries the diagram in
/// words as its help text; the drawing and its labels are raw, so the
/// summary is not read three times over.
/// </para>
/// </remarks>
public sealed class CalculationDiagramView : UserControl
{
    /// <summary>This control's own automation name and heading.</summary>
    public const string Heading = "Reference diagram";

    /// <summary>The automation name of the drawing itself; its help text is the diagram in words.</summary>
    public const string DrawingAutomationName = "Reference diagram drawing";

    /// <summary>The marking every drawing carries.</summary>
    public const string NotToScaleText = "Not to scale · inputs only";

    /// <summary>What the panel says for a calculation that has no diagram yet.</summary>
    public const string NoDiagramYetText = "No diagram yet for this calculation. Hover or focus a field for its description.";

    private const double LabelFontSize = CalculationDiagramSpec.LabelFontSize;
    private const double ArrowHead = 8;

    private static readonly Lazy<Cursor> HandCursor = new(() => new Cursor(StandardCursorType.Hand));

    private readonly Canvas _sheet = new() { Width = CalculationDiagramSpec.Width, Height = CalculationDiagramSpec.Height, ClipToBounds = true };
    private readonly Viewbox _drawing;
    private readonly TextBlock _caption = new() { FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _notToScale = new() { Text = NotToScaleText, FontSize = DesignTokens.FontSizeCaption, FontWeight = DesignTokens.WeightLabel };
    private readonly TextBlock _fallback = new() { Text = NoDiagramYetText, FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly List<Part> _parts = [];

    private CalculationModuleDescriptor? _module;
    private Func<string, CalculationFormField?> _read = _ => null;
    private string? _focused;
    private string? _hovered;

    private enum Role
    {
        Ink,
        Plate,
        Solid,
        Hole,
        Load,
        Dimension,
        Text,
    }

    private sealed record Part(Control Visual, string? InputName, Role Role);

    /// <summary>Initialises a new instance of the <see cref="CalculationDiagramView"/> class.</summary>
    public CalculationDiagramView()
    {
        AutomationProperties.SetName(this, Heading);
        AutomationProperties.SetName(_sheet, DrawingAutomationName);
        AutomationProperties.SetAccessibilityView(_sheet, AccessibilityView.Raw);
        AutomationProperties.SetName(_fallback, "Reference diagram status");

        _drawing = new Viewbox { Stretch = Stretch.Uniform, Child = _sheet, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(_drawing, DrawingAutomationName);
        AutomationProperties.SetAccessibilityView(_drawing, AccessibilityView.Raw);

        var heading = new TextBlock { Text = Heading, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody + 1 };
        ThemeReactiveBrush.Bind(heading, TextBlock.ForegroundProperty, BrandPalette.HeadingTextBrushKey);
        ThemeReactiveBrush.Bind(_notToScale, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);
        ThemeReactiveBrush.Bind(_caption, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);
        ThemeReactiveBrush.Bind(_fallback, TextBlock.ForegroundProperty, BrandPalette.BodyTextBrushKey);

        var titleRow = new DockPanel();
        DockPanel.SetDock(_notToScale, Dock.Right);
        _notToScale.VerticalAlignment = VerticalAlignment.Center;
        titleRow.Children.Add(_notToScale);
        titleRow.Children.Add(heading);

        var column = new StackPanel { Spacing = DesignTokens.SpaceSm };
        column.Children.Add(titleRow);
        column.Children.Add(_drawing);
        column.Children.Add(_caption);
        column.Children.Add(_fallback);

        var frame = new Border
        {
            Child = column,
            Padding = DesignTokens.PanelPadding,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(DesignTokens.BadgeCornerRadius),
        };
        ThemeReactiveBrush.Bind(frame, Border.BackgroundProperty, BrandPalette.SurfaceBackgroundBrushKey);
        ThemeReactiveBrush.Bind(frame, Border.BorderBrushProperty, BrandPalette.HairlineStrongBrushKey);
        Content = frame;

        ActualThemeVariantChanged += (_, _) => Redraw();
        AttachedToVisualTree += (_, _) => Redraw();
        Show(null, _ => null);
    }

    /// <summary>Raised when a bound shape is clicked: the input it stands for.</summary>
    public event Action<string>? InputActivated;

    /// <summary>Raised when the pointer moves onto a bound shape (its input) or off it (<see langword="null"/>).</summary>
    public event Action<string?>? InputHovered;

    /// <summary>The diagram drawn, or <see langword="null"/> where the calculation has none yet (or none is chosen).</summary>
    public CalculationDiagramSpec? Spec { get; private set; }

    /// <summary>The drawing as last read against the form, or <see langword="null"/> with no diagram.</summary>
    public CalculationDiagramReading? Reading { get; private set; }

    /// <summary>Whether a diagram is drawn (rather than the no-diagram-yet text).</summary>
    public bool HasDiagram => Reading is not null;

    /// <summary>The input whose shapes are highlighted for focus, or <see langword="null"/>.</summary>
    public string? HighlightedInput => _focused;

    /// <summary>What a screen reader is told: the diagram in words, or that there is none yet.</summary>
    public string Summary => AutomationProperties.GetHelpText(this) ?? string.Empty;

    /// <summary>Shows the diagram of <paramref name="module"/>, reading each input through <paramref name="read"/>; <see langword="null"/> clears the panel.</summary>
    public void Show(CalculationModuleDescriptor? module, Func<string, CalculationFormField?> read)
    {
        ArgumentNullException.ThrowIfNull(read);

        _module = module;
        _read = read;
        _focused = null;
        _hovered = null;
        Spec = module is null ? null : CalculationDiagrams.For(module.Id);
        Refresh();
    }

    /// <summary>Re-reads the form and redraws: the variant its choices select, every label as now typed.</summary>
    public void Refresh()
    {
        Reading = Spec is not null && _module is not null ? CalculationDiagramReader.Read(Spec, _module, _read) : null;
        Redraw();
    }

    /// <summary>Highlights every shape of <paramref name="inputName"/> (a field took focus), or none.</summary>
    public void Highlight(string? inputName)
    {
        _focused = inputName;
        ApplyBrushes();
    }

    /// <summary>Whether a shape of <paramref name="inputName"/> is drawn highlighted.</summary>
    public bool IsHighlighted(string inputName) =>
        string.Equals(_focused, inputName, StringComparison.Ordinal) || string.Equals(_hovered, inputName, StringComparison.Ordinal);

    /// <summary>Whether the drawing binds any shape to <paramref name="inputName"/>.</summary>
    public bool Draws(string inputName) => _parts.Any(p => p.InputName == inputName);

    /// <summary>The shapes and labels drawn for <paramref name="inputName"/>.</summary>
    public IReadOnlyList<Control> ShapesFor(string inputName) => _parts.Where(p => p.InputName == inputName).Select(p => p.Visual).ToList();

    /// <summary>The label drawn for <paramref name="inputName"/>, as the sheet shows it, or <see langword="null"/>.</summary>
    public string? LabelFor(string inputName) =>
        _parts.Where(p => p.InputName == inputName && p.Role == Role.Text).Select(p => ((TextBlock)p.Visual).Text).FirstOrDefault();

    /// <summary>Points at the shapes of <paramref name="inputName"/> (or away, with <see langword="null"/>), as the pointer would.</summary>
    public void Hover(string? inputName)
    {
        if (string.Equals(_hovered, inputName, StringComparison.Ordinal))
            return;

        _hovered = inputName;
        ApplyBrushes();
        InputHovered?.Invoke(inputName);
    }

    /// <summary>Clicks a shape of <paramref name="inputName"/>, as the pointer would.</summary>
    public void Activate(string inputName) => InputActivated?.Invoke(inputName);

    // ---- Drawing ----

    private void Redraw()
    {
        _sheet.Children.Clear();
        _parts.Clear();

        var reading = Reading;
        _drawing.IsVisible = reading is not null;
        _caption.IsVisible = reading is not null;
        _notToScale.IsVisible = reading is not null;
        _fallback.IsVisible = reading is null;

        if (reading is null)
        {
            _fallback.Text = _module is null ? "Choose a calculation to see its reference diagram." : NoDiagramYetText;
            AutomationProperties.SetHelpText(this, _module is null ? _fallback.Text : $"{_module.Title}. {NoDiagramYetText}");
            return;
        }

        _caption.Text = reading.Variant.Caption;
        AutomationProperties.SetHelpText(this, reading.Summary);

        foreach (var shape in reading.Shapes)
            Draw(shape.Element);

        foreach (var shape in reading.Shapes.Where(s => s.Label is not null))
            DrawLabel(shape.Element, shape.Label!);

        ApplyBrushes();
    }

    private void Draw(DiagramElement element)
    {
        var input = element.InputName;
        switch (element)
        {
            case DiagramMember m:
                Add(new Line { StartPoint = P(m.From), EndPoint = P(m.To), StrokeThickness = m.Thickness, StrokeLineCap = PenLineCap.Flat }, input, Role.Ink);
                break;

            case DiagramPlate p:
            {
                var rectangle = new Rectangle { Width = p.Width, Height = p.Height, StrokeThickness = 1.5 };
                Canvas.SetLeft(rectangle, p.X);
                Canvas.SetTop(rectangle, p.Y);
                Add(rectangle, input, Role.Plate);
                break;
            }

            case DiagramCircle c:
            {
                var ellipse = new Ellipse { Width = c.Radius * 2, Height = c.Radius * 2, StrokeThickness = c.Filled ? 1.5 : 2.5 };
                Canvas.SetLeft(ellipse, c.Centre.X - c.Radius);
                Canvas.SetTop(ellipse, c.Centre.Y - c.Radius);
                Add(ellipse, input, c.Solid ? Role.Solid : c.Filled ? Role.Plate : Role.Hole);
                break;
            }

            case DiagramSupport s:
                DrawSupport(s);
                break;

            case DiagramPointLoad l:
                Arrow(P(l.Tail), P(l.Tip), input, Role.Load, 2);
                break;

            case DiagramDistributedLoad d:
            {
                var top = d.SurfaceY - d.Height;
                Add(new Line { StartPoint = new Point(d.FromX, top), EndPoint = new Point(d.ToX, top), StrokeThickness = 2 }, input, Role.Load);
                var count = Math.Max(2, (int)Math.Round((d.ToX - d.FromX) / 35) + 1);
                for (var i = 0; i < count; i++)
                {
                    var x = d.FromX + (d.ToX - d.FromX) * i / (count - 1);
                    Arrow(new Point(x, top), new Point(x, d.SurfaceY), input, Role.Load, 1.5);
                }

                break;
            }

            case DiagramMoment mo:
                DrawMoment(mo);
                break;

            case DiagramSpring sp:
                DrawSpring(sp);
                break;

            case DiagramDimension dim:
                DrawDimension(dim);
                break;

            case DiagramPolyline pl:
                DrawPolyline(pl);
                break;

            case DiagramAxes ax:
                DrawAxes(ax);
                break;

            case DiagramLabel:
                break; // the label alone; drawn with the others.

            default:
                throw new NotSupportedException($"The reference diagram cannot draw a {element.GetType().Name} ('{element.Id}'); teach {nameof(CalculationDiagramView)} to draw it.");
        }
    }

    private void DrawSupport(DiagramSupport s)
    {
        // Local frame: u along the ground, v towards it; mapped by the ground's side.
        Point At(double u, double v) => s.Ground switch
        {
            DiagramDirection.Up => new Point(s.At.X + u, s.At.Y - v),
            DiagramDirection.Left => new Point(s.At.X - v, s.At.Y + u),
            DiagramDirection.Right => new Point(s.At.X + v, s.At.Y + u),
            _ => new Point(s.At.X + u, s.At.Y + v),
        };

        void Stroke(Point a, Point b, double thickness = 1.5) =>
            Add(new Line { StartPoint = a, EndPoint = b, StrokeThickness = thickness }, s.InputName, Role.Ink);

        void Hatch(double v, double from, double to)
        {
            for (var u = from; u <= to; u += 7)
                Stroke(At(u, v), At(u - 5, v + 7), 1);
        }

        switch (s.Kind)
        {
            case DiagramSupportKind.Fixed:
                Stroke(At(-24, 0), At(24, 0), 2.5);
                Hatch(0, -22, 24);
                break;

            case DiagramSupportKind.Pinned:
                Add(new Polygon { Points = [At(0, 0), At(-10, 16), At(10, 16)], StrokeThickness = 1.5 }, s.InputName, Role.Plate);
                Stroke(At(-15, 16), At(15, 16));
                Hatch(16, -12, 15);
                break;

            case DiagramSupportKind.Roller:
            {
                Add(new Polygon { Points = [At(0, 0), At(-10, 12), At(10, 12)], StrokeThickness = 1.5 }, s.InputName, Role.Plate);
                foreach (var u in new[] { -5.0, 5.0 })
                {
                    var centre = At(u, 15);
                    var wheel = new Ellipse { Width = 6, Height = 6, StrokeThickness = 1.2 };
                    Canvas.SetLeft(wheel, centre.X - 3);
                    Canvas.SetTop(wheel, centre.Y - 3);
                    Add(wheel, s.InputName, Role.Hole);
                }

                Stroke(At(-15, 18), At(15, 18));
                break;
            }
        }
    }

    private void DrawMoment(DiagramMoment m)
    {
        // A three-quarter arc, opening towards the top right, its head at the end of the sweep.
        var start = Math.PI * 0.75;
        var sweep = Math.PI * 1.5 * (m.Clockwise ? 1 : -1);
        var end = start + sweep;
        Point On(double angle) => new(m.Centre.X + m.Radius * Math.Cos(angle), m.Centre.Y + m.Radius * Math.Sin(angle));

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(On(start), false);
            context.ArcTo(On(end), new Size(m.Radius, m.Radius), 0, true, m.Clockwise ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
            context.EndFigure(false);
        }

        Add(new Avalonia.Controls.Shapes.Path { Data = geometry, StrokeThickness = 2 }, m.InputName, Role.Load);

        var tip = On(end);
        var tangent = m.Clockwise ? new Vector(-Math.Sin(end), Math.Cos(end)) : new Vector(Math.Sin(end), -Math.Cos(end));
        Head(tip, tangent, m.InputName, Role.Load);
    }

    private void DrawSpring(DiagramSpring s)
    {
        var from = P(s.From);
        var to = P(s.To);
        var along = to - from;
        var length = Math.Sqrt(along.X * along.X + along.Y * along.Y);
        if (length < 1)
            return;

        var unit = along / length;
        var normal = new Vector(-unit.Y, unit.X);
        const double Lead = 8, Amplitude = 7;
        const int Teeth = 6;

        var points = new List<Point> { from, from + unit * Lead };
        var zig = length - 2 * Lead;
        for (var i = 0; i < Teeth; i++)
            points.Add(from + unit * (Lead + zig * (i + 0.5) / Teeth) + normal * (i % 2 == 0 ? Amplitude : -Amplitude));
        points.Add(to - unit * Lead);
        points.Add(to);

        Add(new Polyline { Points = points, StrokeThickness = 1.8 }, s.InputName, Role.Ink);
    }

    private void DrawDimension(DiagramDimension d)
    {
        var from = P(d.From);
        var to = P(d.To);
        var lineFrom = P(d.LineFrom);
        var lineTo = P(d.LineTo);

        if (Math.Abs(d.Offset) > 0.5)
        {
            var overshoot = Math.Sign(d.Offset) * 4;
            var extension = d.IsVertical ? new Vector(overshoot, 0) : new Vector(0, overshoot);
            Add(new Line { StartPoint = from, EndPoint = lineFrom + extension, StrokeThickness = 0.8 }, d.InputName, Role.Dimension);
            Add(new Line { StartPoint = to, EndPoint = lineTo + extension, StrokeThickness = 0.8 }, d.InputName, Role.Dimension);
        }

        Add(new Line { StartPoint = lineFrom, EndPoint = lineTo, StrokeThickness = 1 }, d.InputName, Role.Dimension);
        var direction = lineTo - lineFrom;
        var length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        if (length < 1)
            return;

        var unit = direction / length;
        Head(lineTo, unit, d.InputName, Role.Dimension, 6);
        Head(lineFrom, -unit, d.InputName, Role.Dimension, 6);
    }

    private void Arrow(Point tail, Point tip, string? input, Role role, double thickness)
    {
        var direction = tip - tail;
        var length = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        if (length < 1)
            return;

        var unit = direction / length;
        Add(new Line { StartPoint = tail, EndPoint = tip - unit * (ArrowHead * 0.8), StrokeThickness = thickness }, input, role);
        Head(tip, unit, input, role);
    }

    private void Head(Point tip, Vector unit, string? input, Role role, double size = ArrowHead)
    {
        var normal = new Vector(-unit.Y, unit.X);
        var basePoint = tip - unit * size;
        Add(new Polygon { Points = [tip, basePoint + normal * (size / 2), basePoint - normal * (size / 2)], StrokeThickness = 0 }, input, role is Role.Dimension or Role.Ink ? role : Role.Load, filledHead: true);
    }

    private void DrawLabel(DiagramElement element, string text)
    {
        var anchor = element.LabelAnchor;
        var leftAligned = element is DiagramDimension { IsVertical: true, LabelAt: null };

        // The box is all the room the sheet leaves around the anchor, so a label too long for it ends in "…" rather than being cut at the sheet's edge.
        var room = leftAligned ? CalculationDiagramSpec.Width - anchor.X : 2 * Math.Min(anchor.X, CalculationDiagramSpec.Width - anchor.X);
        var block = new TextBlock
        {
            Text = text,
            FontSize = LabelFontSize,
            FontFamily = DesignTokens.BodyFont,
            Width = Math.Max(0, room),
            TextAlignment = leftAligned ? TextAlignment.Left : TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis, // a long text input ends in "…", never cut mid-glyph
        };
        Canvas.SetLeft(block, leftAligned ? anchor.X : anchor.X - block.Width / 2);
        Canvas.SetTop(block, anchor.Y - LabelFontSize * 0.75);
        AutomationProperties.SetAccessibilityView(block, AccessibilityView.Raw);
        Add(block, element.InputName, Role.Text);
    }

    private void Add(Control visual, string? input, Role role, bool filledHead = false)
    {
        if (filledHead)
            visual.Tag = "head";

        if (input is not null)
        {
            visual.Cursor = HandCursor.Value;
            visual.PointerEntered += (_, _) => Hover(input);
            visual.PointerExited += (_, _) =>
            {
                if (string.Equals(_hovered, input, StringComparison.Ordinal))
                    Hover(null);
            };
            visual.PointerPressed += (_, e) =>
            {
                Activate(input);
                e.Handled = true;
            };
            ToolTip.SetTip(visual, _module?.Inputs.FirstOrDefault(i => i.Name == input)?.Label);
        }
        else
        {
            visual.IsHitTestVisible = false;
        }

        _sheet.Children.Add(visual);
        _parts.Add(new Part(visual, input, role));
    }

    private void ApplyBrushes()
    {
        var ink = Brush(BrandPalette.HeadingTextBrushKey, Brushes.Black);
        var plate = Brush(BrandPalette.HairlineStrongBrushKey, Brushes.LightGray);
        var surface = Brush(BrandPalette.SurfaceBackgroundBrushKey, Brushes.White);
        var load = Brush(BrandPalette.DangerBrushKey, Brushes.IndianRed);
        var dimension = Brush(BrandPalette.MutedTextBrushKey, Brushes.Gray);
        var highlight = Brush(ApplicationPalette.FocusRingBrushKey, Brushes.RoyalBlue);
        var highlightFill = Brush(ApplicationPalette.AccentPanelBackgroundBrushKey, Brushes.LightBlue);

        foreach (var part in _parts)
        {
            var lit = part.InputName is { } name && IsHighlighted(name);
            var stroke = lit ? highlight : part.Role switch
            {
                Role.Load => load,
                Role.Dimension => dimension,
                _ => ink,
            };

            switch (part.Visual)
            {
                case TextBlock text:
                    text.Foreground = lit ? highlight : ink;
                    text.FontWeight = lit ? FontWeight.Bold : FontWeight.Normal;
                    break;

                case Polygon head when Equals(head.Tag, "head"):
                    head.Fill = stroke;
                    break;

                case Shape shape:
                    shape.Stroke = stroke;
                    shape.Fill = part.Role switch
                    {
                        Role.Plate => lit ? highlightFill : plate,
                        Role.Solid => lit ? highlight : ink,
                        Role.Hole => surface,
                        _ => null,
                    };
                    break;
            }
        }
    }

    private IBrush Brush(string key, IBrush fallback) => ThemeReactiveBrush.Resolve(this, key) ?? fallback;

    private static Point P(DiagramPoint point) => new(point.X, point.Y);

    private void DrawPolyline(DiagramPolyline line)
    {
        if (line.Points.Count < 2)
            return;

        var polyline = new Polyline { Points = line.Points.Select(P).ToList(), StrokeThickness = line.Thickness, StrokeJoin = PenLineJoin.Round };
        if (line.Dashed)
            polyline.StrokeDashArray = [4, 3];
        Add(polyline, line.InputName, Role.Ink);
    }

    private void DrawAxes(DiagramAxes axes)
    {
        var origin = P(axes.Origin);
        Arrow(origin, new Point(axes.XEnd, origin.Y), null, Role.Ink, 1.5);
        Arrow(origin, new Point(origin.X, axes.YEnd), null, Role.Ink, 1.5);
    }
}
