using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Desktop.DigitalThread;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Editors.Sections;

/// <summary>
/// The generic Lifecycle section — a real, coloured status badge
/// (`WP 10.5A`) plus the last five transitions, moved verbatim from
/// <see cref="ObjectEditorView"/>'s own former <c>PopulateLifecycle</c>
/// (`WP 21.1B`).
/// </summary>
/// <remarks>
/// Visible for every Kind by default (the pre-split shell never set this
/// Expander's own <c>IsVisible</c> to <see langword="false"/> anywhere
/// except the one Evidence branch, which shows its own specialised
/// Lifecycle instead), including the Requirement-only population path
/// (<paramref name="subject"/> <see langword="null"/>) — see
/// <see cref="ValidationSection"/>'s own identical remarks for why calling
/// this section's own <see cref="LoadAsync"/> with no target already
/// reproduces the pre-split shell's own hand-duplicated fallback text with
/// no duplication needed here.
/// </remarks>
internal sealed class LifecycleSection : IEditorSection
{
    private readonly StackPanel _panel = new() { Spacing = DesignTokens.SpaceXs };
    private Expander _expander = null!;

    public string Title => "Lifecycle";

    public bool AppliesTo(IEngineeringObject? subject) => subject is not Tempest.Core.Evidence.Evidence;

    public Control Build(EditorSectionContext ctx) => _expander = EditorSectionHelpers.BuildSection(Title, _panel);

    public Task LoadAsync(IEngineeringObject? subject, CancellationToken ct)
    {
        _expander.IsVisible = AppliesTo(subject);
        _panel.Children.Clear();

        if (subject is not IHasLifecycle lifecycle)
        {
            _panel.Children.Add(new TextBlock { Text = "This object carries no lifecycle.", Opacity = 0.7 });
            return Task.CompletedTask;
        }

        // A real, coloured status badge (`WP 10.5A`) — reuses
        // `LifecycleColors` exactly as the Digital Thread graph does, so a
        // status reads identically wherever it is shown.
        var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        statusRow.Children.Add(new Border { Width = 10, Height = 10, Background = LifecycleColors.Resolve(lifecycle.Status), CornerRadius = new CornerRadius(5), VerticalAlignment = VerticalAlignment.Center });
        statusRow.Children.Add(new TextBlock { Text = $"Status: {lifecycle.Status}", FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        _panel.Children.Add(statusRow);

        if (lifecycle.History.Count == 0)
        {
            _panel.Children.Add(new TextBlock { Text = "No transitions recorded yet.", Opacity = 0.7, FontSize = DesignTokens.FontSizeCaption });
            return Task.CompletedTask;
        }

        foreach (var record in lifecycle.History.TakeLast(5).Reverse())
        {
            _panel.Children.Add(new TextBlock
            {
                Text = $"{record.From} → {record.To}   ({record.OccurredAt:yyyy-MM-dd HH:mm} UTC)",
                FontSize = DesignTokens.FontSizeCaption,
                Opacity = 0.8,
            });
        }

        return Task.CompletedTask;
    }

    public void React(WorkspaceChange change)
    {
        // Reserved — see IEditorSection's own remarks.
    }
}
