using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views.Dashboards;

/// <summary>
/// The one shared chart-drawing helper every dashboard draws through (`WP
/// 19.7B`, brief scope item 6): a horizontal bar chart, a line chart, and
/// the Gantt's own shared time axis — each built from plain Avalonia
/// shapes (<see cref="Rectangle"/>, <see cref="Polyline"/>,
/// <see cref="Ellipse"/>) bound to theme-token colours
/// (<see cref="ThemeReactiveBrush"/>) so both themes read, never a
/// charting library. Every figure a chart draws is also rendered as plain
/// text alongside it — the Product Owner's own "never a zero that lies"
/// guard extends to "never a figure only a screen can see": the
/// automation/layout walk, and a screen reader, need the same numbers a
/// sighted user reads off the drawing.
/// </summary>
internal static class DashboardChart
{
    private const double BarTrackWidth = 200;
    private const double BarHeight = 14;
    private const double LineWidth = 620;
    private const double LineHeight = 110;
    private const double PxPerDay = 4;
    private const double GanttLabelWidth = 180;
    private const double GanttTrailingWidth = 170;

    /// <summary>One row of a <see cref="HorizontalBars"/> chart.</summary>
    public readonly record struct Bar(string Label, int Value, string ColorBrushKey);

    /// <summary>One point of a <see cref="Line"/> chart. <paramref name="DisplayText"/> is the exact text the parity list shows — defaults to <see cref="Value"/> formatted plainly when not given.</summary>
    public readonly record struct Point(string Label, decimal Value, string? DisplayText = null);

    /// <summary>One row of a <see cref="Gantt"/> — a project's own schedule bar. <see cref="Start"/>/<see cref="Target"/> both <see langword="null"/> draws no bar, only the label and trailing text (an honest "no dates recorded" rather than a fabricated span).</summary>
    public readonly record struct GanttRow(string Label, DateOnly? Start, DateOnly? Target, string TrailingText, string ColorBrushKey);

    /// <summary>
    /// A horizontal bar per <paramref name="bars"/> row — a label, a
    /// filled <see cref="Rectangle"/> proportional to the largest value
    /// present, and the value itself as text so the figure is never only
    /// a pixel width. An all-zero set still renders every label and a
    /// literal "0", never a misleading full or empty bar.
    /// </summary>
    public static Control HorizontalBars(IReadOnlyList<Bar> bars)
    {
        var rows = new StackPanel { Spacing = DesignTokens.SpaceSm };
        var max = bars.Count == 0 ? 0 : bars.Max(b => b.Value);

        foreach (var bar in bars)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("140,Auto,Auto"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, DesignTokens.SpaceXs) };

            var label = new TextBlock { Text = bar.Label, VerticalAlignment = VerticalAlignment.Center, FontSize = DesignTokens.FontSizeBody, TextTrimming = TextTrimming.CharacterEllipsis };
            ThemeReactiveBrush.Bind(label, TextBlock.ForegroundProperty, BrandPalette.BodyTextBrushKey);
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            var trackHost = new Grid { Width = BarTrackWidth, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            var track = new Border
            {
                Height = BarHeight,
                CornerRadius = new CornerRadius(DesignTokens.BadgeCornerRadius),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            ThemeReactiveBrush.Bind(track, Border.BackgroundProperty, BrandPalette.HairlineStrongBrushKey);
            trackHost.Children.Add(track);

            var fillWidth = max <= 0 ? 0 : BarTrackWidth * bar.Value / max;
            var fill = new Rectangle
            {
                Width = Math.Max(0, fillWidth),
                Height = BarHeight,
                RadiusX = DesignTokens.BadgeCornerRadius,
                RadiusY = DesignTokens.BadgeCornerRadius,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            ThemeReactiveBrush.Bind(fill, Shape.FillProperty, bar.ColorBrushKey);
            trackHost.Children.Add(fill);
            Grid.SetColumn(trackHost, 1);
            row.Children.Add(trackHost);

            var value = new TextBlock
            {
                Text = bar.Value.ToString(CultureInfo.InvariantCulture),
                Margin = new Thickness(DesignTokens.SpaceMd, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = DesignTokens.MonoFont,
            };
            ThemeReactiveBrush.Bind(value, TextBlock.ForegroundProperty, BrandPalette.HeadingTextBrushKey);
            Grid.SetColumn(value, 2);
            row.Children.Add(value);

            rows.Children.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// A line across <paramref name="points"/>, drawn with a
    /// <see cref="Polyline"/> plus a marker per point, and — beneath the
    /// drawing — the identical label/value pairs as text, so a chart with
    /// no visual access still carries every figure it draws.
    /// </summary>
    public static Control Line(IReadOnlyList<Point> points, string strokeBrushKey = BrandPalette.AccentBrushKey)
    {
        var stack = new StackPanel { Spacing = DesignTokens.SpaceSm };

        if (points.Count == 0)
        {
            var empty = new TextBlock { Text = "No data.", FontSize = DesignTokens.FontSizeCaption, Opacity = 0.7 };
            stack.Children.Add(empty);
            return stack;
        }

        var min = Math.Min(0m, points.Min(p => p.Value));
        var max = points.Max(p => p.Value);
        if (max <= min)
            max = min + 1m;

        var canvas = new Canvas { Width = LineWidth, Height = LineHeight };

        var zeroY = LineHeight - (double)((0m - min) / (max - min)) * LineHeight;
        if (zeroY >= 0 && zeroY <= LineHeight)
        {
            var zeroLine = new Line { StartPoint = new Point2(0, zeroY), EndPoint = new Point2(LineWidth, zeroY), StrokeThickness = 1 };
            ThemeReactiveBrush.Bind(zeroLine, Shape.StrokeProperty, BrandPalette.HairlineStrongBrushKey);
            canvas.Children.Add(zeroLine);
        }

        var polyline = new Polyline { StrokeThickness = 2 };
        ThemeReactiveBrush.Bind(polyline, Shape.StrokeProperty, strokeBrushKey);

        var stepX = points.Count > 1 ? LineWidth / (points.Count - 1) : 0;
        var linePoints = new Points();
        for (var i = 0; i < points.Count; i++)
        {
            var x = stepX * i;
            var t = (double)((points[i].Value - min) / (max - min));
            var y = LineHeight - (t * LineHeight);
            linePoints.Add(new Point2(x, y));
        }

        polyline.Points = linePoints;
        canvas.Children.Add(polyline);

        for (var i = 0; i < points.Count; i++)
        {
            var dot = new Ellipse { Width = 6, Height = 6 };
            ThemeReactiveBrush.Bind(dot, Shape.FillProperty, strokeBrushKey);
            Canvas.SetLeft(dot, linePoints[i].X - 3);
            Canvas.SetTop(dot, linePoints[i].Y - 3);
            canvas.Children.Add(dot);
        }

        stack.Children.Add(canvas);

        var textWrap = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var point in points)
        {
            var text = new TextBlock
            {
                Text = $"{point.Label}: {point.DisplayText ?? point.Value.ToString("N0", CultureInfo.InvariantCulture)}",
                FontSize = DesignTokens.FontSizeCaption,
                Margin = new Thickness(0, 0, DesignTokens.SpaceMd, DesignTokens.SpaceXs),
            };
            ThemeReactiveBrush.Bind(text, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);
            textWrap.Children.Add(text);
        }

        stack.Children.Add(textWrap);
        return stack;
    }

    /// <summary>
    /// The simple Gantt (Product Owner comment item 6, sheet 3): one row
    /// per <paramref name="rows"/>, a bar from its own start to target date
    /// on a shared axis spanning <paramref name="axisStart"/> to
    /// <paramref name="axisEnd"/>, today marked with a vertical line, and
    /// the row's own trailing text (quoted/recorded hours) printed at its
    /// end. Returns a control wider than most viewports on purpose — the
    /// caller wraps it in its own horizontally-scrolling container (the
    /// brief's own words), never scrolled internally here.
    /// </summary>
    public static Control Gantt(IReadOnlyList<GanttRow> rows, DateOnly axisStart, DateOnly axisEnd, DateOnly today)
    {
        var totalDays = Math.Max(1, axisEnd.DayNumber - axisStart.DayNumber);
        var axisWidth = totalDays * PxPerDay;
        var todayX = Math.Clamp((today.DayNumber - axisStart.DayNumber) * PxPerDay, 0, axisWidth);
        var columns = FormattableString.Invariant($"{(int)GanttLabelWidth},{(int)axisWidth},{(int)GanttTrailingWidth}");

        var stack = new StackPanel { Spacing = DesignTokens.SpaceXs };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions(columns) };
        var headerLane = new Canvas { Width = axisWidth, Height = 16 };
        headerLane.Children.Add(AxisLabel(axisStart.ToString("d", CultureInfo.InvariantCulture), 0));
        headerLane.Children.Add(AxisLabel("today", todayX - 14));
        headerLane.Children.Add(AxisLabel(axisEnd.ToString("d", CultureInfo.InvariantCulture), axisWidth - 60));
        Grid.SetColumn(headerLane, 1);
        header.Children.Add(headerLane);
        stack.Children.Add(header);

        foreach (var row in rows)
        {
            var line = new Grid { ColumnDefinitions = new ColumnDefinitions(columns), Margin = new Thickness(0, DesignTokens.SpaceXs) };

            var label = new TextBlock { Text = row.Label, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            ThemeReactiveBrush.Bind(label, TextBlock.ForegroundProperty, BrandPalette.BodyTextBrushKey);
            Grid.SetColumn(label, 0);
            line.Children.Add(label);

            var lane = new Canvas { Width = axisWidth, Height = 18 };
            var todayMarker = new Line { StartPoint = new Point2(todayX, 0), EndPoint = new Point2(todayX, 18), StrokeThickness = 1 };
            ThemeReactiveBrush.Bind(todayMarker, Shape.StrokeProperty, BrandPalette.HairlineStrongBrushKey);
            lane.Children.Add(todayMarker);

            if (row.Start is { } start && row.Target is { } target)
            {
                var barStartX = Math.Clamp((start.DayNumber - axisStart.DayNumber) * PxPerDay, 0, axisWidth);
                var barEndX = Math.Clamp((target.DayNumber - axisStart.DayNumber) * PxPerDay, 0, axisWidth);
                var bar = new Rectangle
                {
                    Width = Math.Max(2, barEndX - barStartX),
                    Height = 12,
                    RadiusX = DesignTokens.BadgeCornerRadius,
                    RadiusY = DesignTokens.BadgeCornerRadius,
                };
                ThemeReactiveBrush.Bind(bar, Shape.FillProperty, row.ColorBrushKey);
                Canvas.SetLeft(bar, barStartX);
                Canvas.SetTop(bar, 3);
                lane.Children.Add(bar);
            }

            Grid.SetColumn(lane, 1);
            line.Children.Add(lane);

            var trailing = new TextBlock
            {
                Text = row.TrailingText,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = DesignTokens.FontSizeCaption,
                Margin = new Thickness(DesignTokens.SpaceMd, 0, 0, 0),
            };
            ThemeReactiveBrush.Bind(trailing, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);
            Grid.SetColumn(trailing, 2);
            line.Children.Add(trailing);

            stack.Children.Add(line);
        }

        return stack;
    }

    private static TextBlock AxisLabel(string text, double left)
    {
        var block = new TextBlock { Text = text, FontSize = DesignTokens.FontSizeLabel };
        ThemeReactiveBrush.Bind(block, TextBlock.ForegroundProperty, BrandPalette.FaintTextBrushKey);
        Canvas.SetLeft(block, Math.Max(0, left));
        Canvas.SetTop(block, 0);
        return block;
    }
}

/// <summary>
/// An alias for <see cref="Avalonia.Point"/> local to this file — this
/// file's own <see cref="DashboardChart.Point"/> record already claims the
/// short name for the chart data shape, so every geometric use below
/// spells it out through this alias instead of a fully-qualified name at
/// every call site.
/// </summary>
internal readonly struct Point2
{
    private readonly Point _value;

    public Point2(double x, double y) => _value = new Point(x, y);

    public double X => _value.X;

    public double Y => _value.Y;

    public static implicit operator Point(Point2 value) => value._value;
}
