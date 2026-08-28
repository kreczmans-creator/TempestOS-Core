using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Companion.Theming;

namespace Tempest.Companion.Views;

/// <summary>
/// The Companion's Command Palette — the mobile representation of the
/// platform's first-class global entry point (`ADR-0070`): one search
/// field over every navigable destination and Companion action, opened
/// from the app bar on every page. Substring-filtered against entry
/// titles, exactly the matching rule the desktop palette applies; entries
/// are supplied by the shell, which owns navigation
/// (<c>ADR-0104</c>-style direct delegates, no mobile-local dispatcher).
/// Marked with the brand's command accent (Purple) — the palette's own
/// semantic colour.
/// </summary>
public sealed class CommandPaletteOverlay : Border
{
    /// <summary>One palette entry: a title, a category label, and the action executing it.</summary>
    /// <param name="Title">The entry's own display title.</param>
    /// <param name="Category">A short category label (<c>"Navigate"</c>, <c>"Project"</c>, <c>"Action"</c>).</param>
    /// <param name="Execute">The action run when the entry is chosen.</param>
    public sealed record PaletteEntry(string Title, string Category, Action Execute);

    private readonly TextBox _searchBox;
    private readonly ListBox _resultsList;
    private Func<IReadOnlyList<PaletteEntry>> _entriesSource = () => [];
    private List<PaletteEntry> _visible = [];

    /// <summary>Initialises a new instance of the <see cref="CommandPaletteOverlay"/> class.</summary>
    public CommandPaletteOverlay()
    {
        var app = Avalonia.Application.Current!;

        IsVisible = false;
        Background = BrandPalette.Brush(app, BrandPalette.PageBackgroundBrushKey);
        Padding = CompanionTokens.PagePadding;

        var column = new StackPanel { Spacing = CompanionTokens.SpaceLg };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new TextBlock
        {
            Text = "COMMAND",
            FontFamily = CompanionTokens.TitleFont,
            FontSize = CompanionTokens.FontSizeTitle,
            FontWeight = CompanionTokens.WeightHeading,
            LetterSpacing = CompanionTokens.WideTracking,
            Foreground = BrandPalette.Brush(app, BrandPalette.CommandAccentBrushKey),
            VerticalAlignment = VerticalAlignment.Center,
        });
        var close = BrandButtons.Quiet("Esc");
        close.MinWidth = CompanionTokens.MinTouchTarget;
        Avalonia.Automation.AutomationProperties.SetName(close, "Close command palette");
        close.Click += (_, _) => Close();
        Grid.SetColumn(close, 1);
        header.Children.Add(close);
        column.Children.Add(header);

        _searchBox = new TextBox
        {
            Watermark = "Jump to a screen, project, or action",
            MinHeight = CompanionTokens.MinTouchTarget,
            FontFamily = CompanionTokens.BodyFont,
            CornerRadius = new Avalonia.CornerRadius(CompanionTokens.ControlCornerRadius),
        };
        Avalonia.Automation.AutomationProperties.SetName(_searchBox, "Command search");
        _searchBox.TextChanged += (_, _) => Filter();
        _searchBox.KeyDown += OnSearchKeyDown;
        column.Children.Add(_searchBox);

        _resultsList = new ListBox { MaxHeight = 480 };
        _resultsList.DoubleTapped += (_, _) => ExecuteSelected();
        _resultsList.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                ExecuteSelected();
                e.Handled = true;
            }
        };
        column.Children.Add(_resultsList);
        column.Children.Add(new TextBlock
        {
            Text = "tap twice or press enter to run · esc closes",
            FontFamily = CompanionTokens.MonoFont,
            FontSize = 10,
            Foreground = BrandPalette.Brush(app, BrandPalette.SecondaryTextBrushKey),
        });

        Child = new Border
        {
            BorderThickness = new Avalonia.Thickness(0, 3, 0, 0),
            BorderBrush = BrandPalette.Brush(app, BrandPalette.CommandAccentBrushKey),
            Padding = new Avalonia.Thickness(0, CompanionTokens.SpaceLg, 0, 0),
            Child = column,
        };

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };
    }

    /// <summary>Gets whether an entry list is currently shown (test seam).</summary>
    public IReadOnlyList<PaletteEntry> VisibleEntries => _visible;

    /// <summary>Sets the provider of the palette's entries — re-evaluated on every open, so the list always reflects current state.</summary>
    public void SetEntriesSource(Func<IReadOnlyList<PaletteEntry>> entriesSource)
    {
        ArgumentNullException.ThrowIfNull(entriesSource);
        _entriesSource = entriesSource;
    }

    /// <summary>Opens the palette with an empty query and focuses the search field.</summary>
    public void Open()
    {
        _searchBox.Text = string.Empty;
        IsVisible = true;
        Filter();
        _searchBox.Focus();
    }

    /// <summary>Closes the palette.</summary>
    public void Close() => IsVisible = false;

    /// <summary>Applies the current query — substring match on title, ordinal-case-insensitive, the desktop palette's own rule.</summary>
    public void Filter()
    {
        var query = _searchBox.Text ?? string.Empty;

        _visible = _entriesSource()
            .Where(e => e.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _resultsList.ItemsSource = _visible
            .Select(e => new ListBoxItem
            {
                MinHeight = CompanionTokens.MinTouchTarget,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = CompanionTokens.SpaceMd,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = e.Category.ToUpperInvariant(),
                            FontFamily = CompanionTokens.MonoFont,
                            FontSize = 10,
                            Foreground = BrandPalette.Brush(Avalonia.Application.Current!, BrandPalette.SecondaryTextBrushKey),
                            VerticalAlignment = VerticalAlignment.Center,
                            MinWidth = 64,
                        },
                        new TextBlock
                        {
                            Text = e.Title,
                            FontFamily = CompanionTokens.BodyFont,
                            FontSize = CompanionTokens.FontSizeBody,
                            Foreground = BrandPalette.Brush(Avalonia.Application.Current!, BrandPalette.BodyTextBrushKey),
                            VerticalAlignment = VerticalAlignment.Center,
                        },
                    },
                },
            })
            .ToList();

        if (_visible.Count > 0)
            _resultsList.SelectedIndex = 0;
    }

    /// <summary>Executes the selected (or single-tapped) entry and closes.</summary>
    public void ExecuteSelected()
    {
        var index = _resultsList.SelectedIndex;
        if (index < 0 || index >= _visible.Count)
            return;

        var entry = _visible[index];
        Close();
        entry.Execute();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                ExecuteSelected();
                e.Handled = true;
                break;
            case Key.Down when _visible.Count > 0:
                _resultsList.SelectedIndex = Math.Min(_resultsList.SelectedIndex + 1, _visible.Count - 1);
                e.Handled = true;
                break;
            case Key.Up when _visible.Count > 0:
                _resultsList.SelectedIndex = Math.Max(_resultsList.SelectedIndex - 1, 0);
                e.Handled = true;
                break;
        }
    }
}
