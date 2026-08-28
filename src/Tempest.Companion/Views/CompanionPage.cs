using Avalonia.Controls;
using Avalonia.Media;
using Tempest.Companion.Offline;
using Tempest.Companion.Theming;

namespace Tempest.Companion.Views;

/// <summary>
/// The base of every Companion page: one scrollable column of cards over
/// the page background, with the intentional loading → (banner +) content
/// / empty / error state machine every screen must have (`WP 14.0A`) —
/// implemented once here so no page can accidentally ship without an
/// offline or error state.
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
        Content = _scroller;
    }

    /// <summary>Gets the page's own title.</summary>
    public string Title { get; }

    /// <summary>Refreshes the page's own data and re-renders.</summary>
    public abstract Task RefreshAsync();

    /// <summary>Shows the shared loading state.</summary>
    protected void ShowLoading() => _scroller.Content = new LoadingStateView { MinHeight = 320 };

    /// <summary>Shows <paramref name="content"/> as the page body.</summary>
    protected void ShowContent(Control content) => _scroller.Content = content;

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
            ShowContent(new ErrorStateView(result.Error ?? "TempestOS is unavailable.", () => _ = RefreshAsync()) { MinHeight = 320 });
            return;
        }

        var column = new StackPanel { Spacing = CompanionTokens.CardSpacing };

        if (result.Freshness != DataFreshness.Live)
            column.Children.Add(new FreshnessBanner(result.Freshness, result.FetchedAtUtc, result.Error));

        foreach (var control in render(result.Data))
            column.Children.Add(control);

        ShowContent(column);
    }

    /// <summary>Formats a UTC moment for display in local time — short for today, dated otherwise.</summary>
    protected static string FormatMoment(DateTimeOffset utc)
    {
        var local = utc.ToLocalTime();
        return local.Date == DateTimeOffset.Now.Date ? local.ToString("HH:mm") : local.ToString("yyyy-MM-dd HH:mm");
    }

    /// <summary>Builds a small status chip — coloured dot glyph plus the status text itself (never colour alone).</summary>
    protected static Control StatusChip(string status, IBrush colour)
    {
        var chip = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = CompanionTokens.SpaceSm };
        chip.Children.Add(new TextBlock { Text = "●", FontSize = 10, Foreground = colour, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        chip.Children.Add(new TextBlock
        {
            Text = status,
            FontFamily = CompanionTokens.MonoFont,
            FontSize = CompanionTokens.FontSizeCaption,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        });
        return chip;
    }
}
