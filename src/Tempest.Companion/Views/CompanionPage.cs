using Avalonia.Controls;
using Avalonia.Media;
using Tempest.Companion.Offline;
using Tempest.Companion.Theming;

namespace Tempest.Companion.Views;

/// <summary>
/// The base of every Companion page: one scrollable column of cards over
/// the navy page ground with the pack's blueprint-grid texture behind it
/// (`WP 14.1A` — the grid never sits behind body text; every card above
/// it is opaque), with the intentional loading → (banner +) content /
/// empty / error state machine every screen must have — implemented once
/// here so no page can ship without an offline or error state.
/// </summary>
public abstract class CompanionPage : UserControl
{
    private readonly ScrollViewer _scroller;

    /// <summary>Initialises a new instance of the <see cref="CompanionPage"/> class.</summary>
    /// <param name="title">The page's own title, shown by the shell's app bar.</param>
    protected CompanionPage(string title)
    {
        Title = title;
        Background = BrandPalette.Brush(Avalonia.Application.Current!, BrandPalette.PageBackgroundBrushKey);

        _scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Padding = CompanionTokens.PagePadding,
        };

        // A ScrollViewer lays a child out at its desired width even with
        // horizontal scrolling disabled, so one unbreakable line (a long
        // mono watermark) would push its card past the viewport. Cap the
        // body to the real viewport width instead - structurally, here,
        // so no page can regress it.
        _scroller.SizeChanged += (_, _) => ConstrainBodyWidth();

        var layers = new Panel();
        layers.Children.Add(new BlueprintGridControl());
        layers.Children.Add(_scroller);
        Content = layers;
    }

    /// <summary>Gets the page's own title.</summary>
    public string Title { get; }

    /// <summary>Refreshes the page's own data and re-renders.</summary>
    public abstract Task RefreshAsync();

    /// <summary>Shows the shared loading state.</summary>
    protected void ShowLoading() => ShowContent(new LoadingStateView { MinHeight = 320 });

    /// <summary>Shows <paramref name="content"/> as the page body.</summary>
    protected void ShowContent(Control content)
    {
        _scroller.Content = content;
        ConstrainBodyWidth();
    }

    private void ConstrainBodyWidth()
    {
        if (_scroller.Content is Control body && _scroller.Bounds.Width > 0)
            body.MaxWidth = Math.Max(0, _scroller.Bounds.Width - CompanionTokens.PagePadding.Left - CompanionTokens.PagePadding.Right);
    }

    /// <summary>
    /// Renders a <see cref="SnapshotResult{T}"/> with the uniform state
    /// treatment: unavailable → error state with Retry; otherwise the
    /// freshness banner (when not live) above <paramref name="render"/>'s
    /// own cards.
    /// </summary>
    protected void ShowResult<T>(SnapshotResult<T> result, Func<T, IEnumerable<Control>> render)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(render);

        if (result.Data is null)
        {
            ShowContent(new ErrorStateView(result.Error ?? "Tempest OS is unavailable.", () => _ = RefreshAsync()) { MinHeight = 320 });
            return;
        }

        var column = new StackPanel { Spacing = CompanionTokens.CardSpacing };

        if (result.Freshness != DataFreshness.Live)
            column.Children.Add(new FreshnessBanner(result.Freshness, result.FetchedAtUtc, result.Error));

        foreach (var control in render(result.Data))
            column.Children.Add(control);

        ShowContent(column);
    }

    /// <summary>
    /// Formats a moment as machine data, per the pack: UTC with a
    /// trailing <c>Z</c> — time-only for today (UTC), dated otherwise.
    /// </summary>
    protected static string FormatMoment(DateTimeOffset moment)
    {
        var utc = moment.ToUniversalTime();
        return utc.Date == DateTimeOffset.UtcNow.Date ? $"{utc:HH:mm}Z" : $"{utc:yyyy-MM-dd HH:mm}Z";
    }

    /// <summary>Builds a status readout — the pack's status dot plus the status text itself in Space Mono (never colour alone).</summary>
    protected static Control StatusChip(string status, IBrush colour)
    {
        var chip = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = CompanionTokens.SpaceSm };
        chip.Children.Add(new TextBlock { Text = "●", FontSize = 9, Foreground = colour, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        chip.Children.Add(new TextBlock
        {
            Text = status.ToUpperInvariant(),
            FontFamily = CompanionTokens.MonoFont,
            FontSize = CompanionTokens.FontSizeCaption,
            Foreground = colour,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        });
        return chip;
    }
}
