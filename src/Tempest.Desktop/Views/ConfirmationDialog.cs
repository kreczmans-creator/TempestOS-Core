using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Confirmation Dialog Framework (`WP 10.5A` scope: "confirmation
/// dialogs") — a real, modal-style Yes/No overlay, initially hidden,
/// used wherever this platform needs to ask before an irreversible or
/// data-losing action proceeds. Its own first, real consumer closes
/// `TD-40` directly: <see cref="MainWindow"/> now asks before discarding
/// a dirty <see cref="Editors.ObjectEditorView"/> tab's own unsaved edits
/// (`WP10.5A Implementation Report.md` §4).
/// </summary>
public sealed class ConfirmationDialog : Border
{
    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeHeading, FontWeight = DesignTokens.WeightHeading };
    private readonly TextBlock _message = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, DesignTokens.SpaceLg) };
    private readonly Button _confirmButton = new() { MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private TaskCompletionSource<bool>? _pending;

    /// <summary>Initialises a new instance of the <see cref="ConfirmationDialog"/> class, initially hidden.</summary>
    public ConfirmationDialog()
    {
        IsVisible = false;
        IsHitTestVisible = true;
        MaxWidth = 420;
        CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius);
        Padding = DesignTokens.DialogPadding;
        BorderThickness = new Thickness(1);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(_cancelButton);
        buttons.Children.Add(_confirmButton);

        var body = new StackPanel();
        body.Children.Add(_title);
        body.Children.Add(_message);
        body.Children.Add(buttons);
        Child = body;

        _confirmButton.Classes.Add(ChromeStyles.Danger);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);
        _title.FontFamily = DesignTokens.TitleFont;
        _title.FontSize = DesignTokens.FontSizeTitle;
        _cancelButton.Click += (_, _) => Complete(false);
        _confirmButton.Click += (_, _) => Complete(true);
        KeyDown += OnKeyDown;
    }

    /// <summary>
    /// <c>Escape</c> cancels from anywhere in the dialog (mirroring
    /// <see cref="InputDialog"/>'s identical convention). <c>Enter</c>
    /// needs no explicit handling here — Avalonia's own <see cref="Button"/>
    /// already invokes <c>Click</c> on a focused button's own <c>Enter</c>,
    /// and <see cref="ConfirmAsync"/> deliberately focuses
    /// <see cref="_cancelButton"/> first, so pressing <c>Enter</c> before
    /// tabbing anywhere takes the safe action, never the (often
    /// irreversible) confirm one.
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Complete(false);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Shows this dialog with <paramref name="title"/>/<paramref name="message"/>,
    /// returning <see langword="true"/> if the user confirms,
    /// <see langword="false"/> if they cancel. Only one confirmation may
    /// be pending at a time — a second call while one is already showing
    /// completes the first as cancelled before starting the new one,
    /// rather than leaving an orphaned, un-awaited prior request.
    /// </summary>
    public Task<bool> ConfirmAsync(string title, string message, string confirmText = "Discard")
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);

        _pending?.TrySetResult(false);

        _title.Text = title;
        _message.Text = message;
        _confirmButton.Content = confirmText;
        IsVisible = true;
        // The safe action gets initial focus — pressing Enter before
        // tabbing anywhere cancels, never confirms (this dialog is used
        // for irreversible/data-losing actions as often as benign ones).
        _cancelButton.Focus();

        _pending = new TaskCompletionSource<bool>();
        return _pending.Task;
    }

    private void Complete(bool result)
    {
        IsVisible = false;
        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(result);
    }
}
