using Avalonia.Controls;
using Avalonia.Media;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Editors;

namespace Tempest.Desktop.Editors.Sections;

/// <summary>
/// The Description section (`WP 18.2A`, declaration-per-Kind) — the
/// mechanical fields the model has today (<see cref="IHasMetadata"/>),
/// read-only, moved verbatim from <see cref="ObjectEditorView"/>'s own
/// former <c>PopulateDescription</c> (`WP 21.1B`).
/// </summary>
internal sealed class DescriptionSection : IEditorSection
{
    private readonly StackPanel _panel = new() { Spacing = DesignTokens.SpaceXs };
    private Expander _expander = null!;
    private EditorSectionContext _ctx = null!;

    public string Title => "Description";

    public bool AppliesTo(IEngineeringObject? subject) =>
        _ctx?.Declarations?.For(_ctx.ObjectKind) is { } declaration && declaration.HasSection(EditorSectionKeys.Description) && subject is IHasMetadata;

    public Control Build(EditorSectionContext ctx)
    {
        _ctx = ctx;
        _expander = EditorSectionHelpers.BuildSection(Title, _panel);
        _expander.IsVisible = false; // matches the pre-split BuildLayout's own default — never populated at all in the Requirement-only path, so LoadAsync's own IsVisible assignment is the only later write.
        return _expander;
    }

    public Task LoadAsync(IEngineeringObject? subject, CancellationToken ct)
    {
        var applies = AppliesTo(subject);
        _expander.IsVisible = applies;

        if (!applies)
            return Task.CompletedTask;

        var metadata = (IHasMetadata)subject!;
        _panel.Children.Clear();
        _panel.Children.Add(Row("Owner", metadata.Owner));
        _panel.Children.Add(Row("Discipline", metadata.Discipline));
        _panel.Children.Add(Row("Classification", metadata.Classification));
        _panel.Children.Add(Row("Tags", metadata.Tags.Count > 0 ? string.Join(", ", metadata.Tags) : null));
        _panel.Children.Add(Row("Notes", metadata.Notes));
        return Task.CompletedTask;
    }

    private static Control Row(string label, string? value) =>
        EditorSectionHelpers.LabeledRow(label, new TextBlock { Text = value ?? "(none)", Opacity = value is null ? 0.5 : 1.0, TextWrapping = TextWrapping.Wrap, FontSize = DesignTokens.FontSizeBody });

    public void React(WorkspaceChange change)
    {
        // Reserved — see IEditorSection's own remarks.
    }
}
