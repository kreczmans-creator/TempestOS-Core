using Avalonia.Controls;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Editors;

namespace Tempest.Desktop.Editors.Sections;

/// <summary>
/// The <em>Where used</em> section (`WP 18.2A`, `TD-174`, `TD-175`) —
/// read-only, named, linked: the assembly this object sits in, moved
/// verbatim from <see cref="ObjectEditorView"/>'s own former
/// <c>PopulateWhereUsedAsync</c> (`WP 21.1B`).
/// </summary>
internal sealed class WhereUsedSection : IEditorSection
{
    private readonly StackPanel _panel = new() { Spacing = DesignTokens.SpaceXs };
    private Expander _expander = null!;
    private EditorSectionContext _ctx = null!;

    public string Title => "Where used";

    public bool AppliesTo(IEngineeringObject? subject) =>
        _ctx?.Declarations?.For(_ctx.ObjectKind) is { } declaration && declaration.HasSection(EditorSectionKeys.WhereUsed) && subject is IHasParent;

    public Control Build(EditorSectionContext ctx)
    {
        _ctx = ctx;
        _expander = EditorSectionHelpers.BuildSection(Title, _panel);
        _expander.IsVisible = false; // matches the pre-split BuildLayout's own default — never populated at all in the Requirement-only path, so LoadAsync's own IsVisible assignment is the only later write.
        return _expander;
    }

    public async Task LoadAsync(IEngineeringObject? subject, CancellationToken ct)
    {
        var applies = AppliesTo(subject);
        _expander.IsVisible = applies;

        if (!applies)
            return;

        _panel.Children.Clear();
        var hasParent = (IHasParent)subject!;

        if (hasParent.ParentId is not { } parentId)
        {
            _panel.Children.Add(new TextBlock { Text = "(top level — not used within any assembly)", Opacity = 0.7 });
            return;
        }

        _panel.Children.Add(await EditorSectionHelpers.BuildObjectReferenceRowAsync(_ctx, parentId).ConfigureAwait(true));
    }

    public void React(WorkspaceChange change)
    {
        // Reserved — see IEditorSection's own remarks.
    }
}
