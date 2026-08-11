using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Busy Overlay (`WP 10.5A` scope: "loading indicators, progress
/// indicators, busy overlays") — a real, semi-transparent, message-
/// bearing overlay shown over a surface while a genuinely-awaited async
/// operation runs, initially hidden. Deliberately simple — a message and
/// an indeterminate <see cref="ProgressBar"/>, never a fabricated
/// progress percentage no real operation here reports.
/// </summary>
public sealed class BusyOverlay : Border
{
    private readonly TextBlock _message = new() { FontSize = DesignTokens.FontSizeBody, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly ProgressBar _progress = new() { IsIndeterminate = true, Width = 180, Margin = new Thickness(0, DesignTokens.SpaceMd, 0, 0) };

    /// <summary>Initialises a new instance of the <see cref="BusyOverlay"/> class, initially hidden.</summary>
    public BusyOverlay()
    {
        IsVisible = false;
        IsHitTestVisible = true;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.OverlayBackgroundBrushKey);

        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(_message);
        stack.Children.Add(_progress);
        Child = stack;
    }

    /// <summary>Gets whether this overlay is currently shown.</summary>
    public bool IsBusy => IsVisible;

    /// <summary>Shows the overlay with <paramref name="message"/> (e.g. "Loading…", "Saving…").</summary>
    public void Show(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _message.Text = message;
        IsVisible = true;
    }

    /// <summary>Hides the overlay. A no-op if already hidden.</summary>
    public void Hide() => IsVisible = false;

    /// <summary>
    /// Runs <paramref name="operation"/> with the overlay shown for its
    /// own duration, guaranteeing <see cref="Hide"/> even if
    /// <paramref name="operation"/> throws — the real, safe pattern every
    /// caller should use rather than manually pairing <see cref="Show"/>/
    /// <see cref="Hide"/>.
    /// </summary>
    public async Task RunAsync(string message, Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Show(message);
        try
        {
            await operation().ConfigureAwait(true);
        }
        finally
        {
            Hide();
        }
    }
}
