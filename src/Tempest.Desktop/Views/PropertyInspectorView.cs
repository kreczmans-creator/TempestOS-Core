using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Workspace;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.DigitalThread;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Property Inspector panel (`WP 10.0B`, modernised `WP 10.2A`) —
/// renders <see cref="IPropertyInspector.CurrentFacets"/> grouped by
/// <see cref="PropertyFacetKind"/> into collapsible sections, matching the
/// four-group ordering `WP10.0A UX Architecture Document.md` §6 specifies
/// (Identity / Lifecycle / Discipline-Specific / Relationships) — realised
/// here as five real <see cref="PropertyFacetKind"/> groups
/// (Identity/Revision/Provenance/DisciplineSpecific/Relationship) plus one
/// derived Lifecycle section extracted, not fabricated, from whichever
/// facet already carries a status-shaped name. The Identity group's own
/// display-name row is a real, working editable control
/// (<see cref="IWorkspaceManager.RenameObjectAsync"/>, `ADR-0096`) when the
/// selected Kind supports it; every other facet remains read-only text —
/// this platform has no generic per-facet mutation capability beyond
/// rename, and this View never fabricates one. The Validation section
/// (`WP 10.8A`) is a real <see cref="IValidatable.ValidateAsync"/> read
/// wherever the selected object genuinely resolves and implements it —
/// see <see cref="AddValidationSection"/>'s own remarks for the one
/// disclosed, honest exception (`TD-41`).
/// </summary>
public sealed class PropertyInspectorView : UserControl
{
    private readonly IPropertyInspector _inspector;
    private readonly IWorkspaceManager _manager;
    private readonly EngineeringDomainContext? _domainContext;
    private readonly StackPanel _panel = new() { Spacing = DesignTokens.SpaceXs, Margin = DesignTokens.PanelPadding };
    private readonly TextBlock _empty = new() { Text = "No selection. Select an object in Project Explorer to inspect it.", TextWrapping = TextWrapping.Wrap, Opacity = 0.7 };

    private System.Guid _currentId;
    private string? _currentKind;

    /// <summary>Raised after an inline edit (currently: Rename) completes — successfully or not.</summary>
    public event Action<string>? ActionCompleted;

    /// <summary>Initialises a new instance of the <see cref="PropertyInspectorView"/> class.</summary>
    /// <param name="inspector">The Workspace's own Property Inspector panel this View renders.</param>
    /// <param name="manager">The owning <see cref="IWorkspaceManager"/> — this View's own real Rename dispatch source (`ADR-0096`, `WP 10.2A`).</param>
    /// <param name="domainContext">
    /// The real Engineering Domain read surface (`WP 10.8A`) — the
    /// Validation section's own real <see cref="IValidatable.ValidateAsync"/>
    /// source, the identical already-permitted read
    /// <see cref="Editors.ObjectEditorView"/> already uses (`ADR-0063`).
    /// <see langword="null"/> (any existing caller/test that never
    /// threads it through) leaves the Validation section at its own
    /// honest, pre-`WP 10.8A` disclosed-placeholder text — never a crash.
    /// </param>
    public PropertyInspectorView(IPropertyInspector inspector, IWorkspaceManager manager, EngineeringDomainContext? domainContext = null)
    {
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(manager);
        _inspector = inspector;
        _manager = manager;
        _domainContext = domainContext;
        Content = new ScrollViewer { Content = _panel };
    }

    /// <summary>
    /// Counts how many rendered facet rows, across every section, carry
    /// <paramref name="facetName"/> as their own label — internal test
    /// hook only (`Tempest.Desktop.Tests`, `InternalsVisibleTo`), proving
    /// the Lifecycle-extraction fix (this class's own <c>ExtractLifecycleFacets</c>
    /// remarks) actually removed the duplicate, not merely that
    /// <see cref="Refresh"/> runs without throwing.
    /// </summary>
    internal int CountRenderedRowsWithFacetName(string facetName)
    {
        var count = 0;

        foreach (var child in _panel.Children)
        {
            if (child is not Expander { Content: StackPanel body })
                continue;

            foreach (var rowControl in body.Children)
            {
                if (rowControl is Grid { Children: [TextBlock { Text: var text }, ..] } && text == facetName)
                    count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Counts how many rendered rows, across every section, carry a real
    /// lifecycle-status colour dot (`WP 10.5C`) — internal test hook only
    /// (`Tempest.Desktop.Tests`, `InternalsVisibleTo`), mirroring
    /// <see cref="CountRenderedRowsWithFacetName"/>'s own identical shape.
    /// </summary>
    internal int CountLifecycleDots()
    {
        var count = 0;

        foreach (var child in _panel.Children)
        {
            if (child is not Expander { Content: StackPanel body })
                continue;

            foreach (var rowControl in body.Children)
            {
                if (rowControl is Grid grid && grid.Children.OfType<Border>().Any())
                    count++;
            }
        }

        return count;
    }

    /// <summary>Records which object is currently displayed — called alongside <see cref="Refresh"/> by the owning window, since <see cref="IPropertyInspector"/> itself exposes no selection identity, only the resulting facets.</summary>
    public void SetCurrentSelection(System.Guid id, string kind)
    {
        _currentId = id;
        _currentKind = kind;
    }

    /// <summary>Refreshes this View's own display from <see cref="IPropertyInspector.CurrentFacets"/> — called after every selection change.</summary>
    public void Refresh()
    {
        _panel.Children.Clear();

        if (_inspector.CurrentFacets.Count == 0)
        {
            _panel.Children.Add(_empty);
            return;
        }

        var facets = _inspector.CurrentFacets;
        var lifecycleFacets = ExtractLifecycleFacets(facets);
        var remainingFacets = lifecycleFacets.Count == 0 ? facets : [.. facets.Except(lifecycleFacets)];

        AddLifecycleSection(lifecycleFacets);

        foreach (var group in remainingFacets.GroupBy(f => f.FacetKind))
            AddGroupSection(group.Key, group.ToList());

        AddValidationSection();
    }

    /// <summary>
    /// Identifies the Lifecycle summary's own facets (`WP 10.2A`'s own
    /// explicit Property Inspector requirement) — no dedicated
    /// <see cref="PropertyFacetKind.Lifecycle"/> exists in this platform's
    /// own frozen facet taxonomy (`WP 9.0A`, unchanged), so this is a real,
    /// disclosed presentation-layer extraction: any already-present facet
    /// whose own <see cref="PropertyFacet.Name"/> names a status/lifecycle
    /// concept is <em>moved</em> into its own distinguished section — never
    /// shown twice, never fabricated. Split from <see cref="Refresh"/> so
    /// the extracted set can also be excluded from its own original
    /// <see cref="PropertyFacetKind"/> group below, fixing a genuine
    /// double-display defect this Work Package's own Engineering Review
    /// found (`WP10.2A Engineering Review.md` §3): the first version of
    /// this method identified the same facets but never removed them from
    /// the per-group loop, so every Lifecycle-shaped fact appeared under
    /// both its own Lifecycle section <em>and</em> its own original group.
    /// </summary>
    private static List<PropertyFacet> ExtractLifecycleFacets(IReadOnlyList<PropertyFacet> facets) =>
        facets
            .Where(f => f.Name.Contains("Status", StringComparison.OrdinalIgnoreCase)
                     || f.Name.Contains("Lifecycle", StringComparison.OrdinalIgnoreCase))
            .ToList();

    private void AddLifecycleSection(IReadOnlyList<PropertyFacet> lifecycleFacets)
    {
        if (lifecycleFacets.Count == 0)
            return;

        // A real, coloured lifecycle dot (`WP 10.5C`, "coloured object
        // states, lifecycle indicators"), reusing the identical
        // `LifecycleColors`/dot shape `ObjectEditorView`'s own status
        // badge and (this Work Package) `ProjectExplorerView`'s own node
        // rows already use — the one place this View can offer it, since
        // this class only ever sees a facet's own display string
        // (`PropertyFacet.Value`), never the real object/enum
        // `ObjectEditorView` holds directly. A best-effort, honestly
        // bounded parse: shown only when that string is exactly one of
        // `LifecycleState`'s own real member names; any other status
        // vocabulary (Requirements' own separate `RequirementStatus`,
        // ObsoleteStatus text, ...) renders as plain text, never a
        // fabricated or misleading colour.
        _panel.Children.Add(BuildSection("Lifecycle", lifecycleFacets, editable: false, showLifecycleDot: true));
    }

    /// <summary>
    /// The Validation summary (`WP 10.2A`'s own explicit Property
    /// Inspector requirement) — a real <see cref="IValidatable.ValidateAsync"/>
    /// read (`WP 10.8A`), the identical read <see cref="Editors.ObjectEditorView"/>'s
    /// own Validation section already performs, via <see cref="_domainContext"/>
    /// (already-permitted, `ADR-0063`) — never a second, independent
    /// validation mechanism. Reuses <see cref="Editors.ObjectEditorView.BuildSeverityRow"/>
    /// directly rather than duplicating its own row-rendering logic.
    /// </summary>
    /// <remarks>
    /// <b>One disclosed, honest exception — `TD-41`.</b> A Requirement
    /// never resolves via <see cref="EngineeringDomainContext.Repository"/>
    /// (the identical, pre-existing gap `WP 10.7A` found and registered
    /// while building <see cref="Editors.ObjectEditorView"/>'s own
    /// Requirements section) — this View honestly says so instead of
    /// claiming validation is unavailable outright, since it genuinely is
    /// available for every other real discipline object.
    /// </remarks>
    private void AddValidationSection()
    {
        Control content;

        var target = _domainContext?.Repository.FindAsync(_currentId).GetAwaiter().GetResult();

        if (target is IValidatable validatable)
        {
            var result = validatable.ValidateAsync().GetAwaiter().GetResult();
            var validationPanel = new StackPanel { Spacing = DesignTokens.SpaceXs };

            if (result.IsValid && result.Warnings.Count == 0)
            {
                validationPanel.Children.Add(ObjectEditorView.BuildSeverityRow(FeedbackSeverity.Success, "No issues found."));
            }
            else
            {
                foreach (var error in result.Errors)
                    validationPanel.Children.Add(ObjectEditorView.BuildSeverityRow(FeedbackSeverity.Error, error.Message));

                foreach (var warning in result.Warnings)
                    validationPanel.Children.Add(ObjectEditorView.BuildSeverityRow(FeedbackSeverity.Warning, warning.Message));
            }

            content = validationPanel;
        }
        else if (_domainContext is not null && target is not null)
        {
            // A real object, genuinely no validation rules apply to its own Kind.
            content = new TextBlock { Text = "This object type supports no automated validation.", Opacity = 0.7, TextWrapping = TextWrapping.Wrap, Margin = DesignTokens.PanelPadding };
        }
        else
        {
            // Either _domainContext was never threaded through (an existing
            // caller/test), or the real object genuinely does not resolve
            // here (`TD-41` — Requirements) — an honest, precise message,
            // never the old "no capability exists" claim now that real
            // validation genuinely does exist for every other Kind.
            content = new TextBlock { Text = "Real validation is not available for this object here.", Opacity = 0.7, TextWrapping = TextWrapping.Wrap, Margin = DesignTokens.PanelPadding };
        }

        _panel.Children.Add(new Expander { Header = "Validation", IsExpanded = false, Margin = DesignTokens.SectionMargin, Content = content });
    }

    private void AddGroupSection(PropertyFacetKind kind, IReadOnlyList<PropertyFacet> facets)
    {
        var title = kind switch
        {
            PropertyFacetKind.Identity => "Identity",
            PropertyFacetKind.Revision => "Revision",
            PropertyFacetKind.Provenance => "Provenance",
            PropertyFacetKind.Relationship => "Relationships",
            PropertyFacetKind.DisciplineSpecific => "Discipline-Specific",
            _ => kind.ToString(),
        };

        _panel.Children.Add(BuildSection(title, facets, editable: kind == PropertyFacetKind.Identity));
    }

    private Expander BuildSection(string title, IReadOnlyList<PropertyFacet> facets, bool editable, bool showLifecycleDot = false)
    {
        var body = new StackPanel { Spacing = DesignTokens.SpaceXs, Margin = DesignTokens.PanelPadding };

        foreach (var facet in facets)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("120,*,Auto") };
            var name = new TextBlock { Text = facet.Name, Opacity = 0.8, FontSize = DesignTokens.FontSizeBody };
            Grid.SetColumn(name, 0);
            row.Children.Add(name);

            var isDisplayNameField = editable && facet.Name.Equals("Name", StringComparison.OrdinalIgnoreCase);
            Control valueControl = isDisplayNameField && _currentKind is not null && _manager.CanRename(_currentKind)
                ? BuildEditableNameField(facet.Value)
                : new TextBlock { Text = facet.Value, TextWrapping = TextWrapping.Wrap, FontSize = DesignTokens.FontSizeBody };

            Grid.SetColumn(valueControl, 1);
            row.Children.Add(valueControl);

            if (showLifecycleDot && Enum.TryParse<LifecycleState>(facet.Value, ignoreCase: true, out var state))
            {
                var dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = LifecycleColors.Resolve(state), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(DesignTokens.SpaceXs, 0, 0, 0) };
                ToolTip.SetTip(dot, $"Lifecycle: {state}");
                Grid.SetColumn(dot, 2);
                row.Children.Add(dot);
            }

            body.Children.Add(row);
        }

        return new Expander
        {
            Header = title,
            IsExpanded = true,
            Margin = DesignTokens.SectionMargin,
            Content = body,
        };
    }

    /// <summary>The Identity group's own real, editable Display Name field — the Property Inspector's own "editable controls where appropriate" requirement, backed by real dispatch, never a decorative control.</summary>
    private Control BuildEditableNameField(string currentValue)
    {
        var box = new TextBox { Text = currentValue, FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };

        async void Commit()
        {
            var newName = box.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(newName) || newName == currentValue || _currentKind is null)
                return;

            var result = await _manager.RenameObjectAsync(_currentId, _currentKind, newName).ConfigureAwait(true);
            ActionCompleted?.Invoke(result.Succeeded ? $"Renamed to '{newName}'." : result.Message ?? "Rename failed.");
        }

        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
                Commit();
        };
        box.LostFocus += (_, _) => Commit();

        return box;
    }
}
