using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Projects;

namespace Tempest.Desktop.Views;

/// <summary>
/// The object picker FCR-0073 named (`WP 20.2A`, S2-2/TD-115): a
/// general-purpose object-reference picker over every live Engineering
/// object, listed by Kind with a filter box, the current project's own
/// objects first — the parameter-collection seam the fifteen
/// object-picker-unavailable bindings declared before this Work Package
/// (<see cref="Tempest.Core.Commands.CommandParameter.ObjectPickerKinds"/>).
/// Modelled directly on <see cref="ProjectPicker"/>/<see cref="SubjectPicker"/>
/// (modal discipline, panel styling, real modal behaviour via
/// <see cref="DialogModality"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Returns a string, like <see cref="InputDialog"/> — never a
/// <see cref="Guid"/>?.</b> <see cref="PickAsync"/> is substituted directly
/// for <see cref="InputDialog.PromptAsync(string, string, string, System.Func{string, string?}?, bool)"/>
/// inside <see cref="Composition.DesktopCommandPrompt"/>'s own per-parameter
/// loop: <see langword="null"/> means the person cancelled (the whole
/// command is declined, exactly as declining a text prompt already means),
/// and an empty string is a value — "no destination" (top level, ungrouped)
/// — never a third outcome the collection contract would need to grow a
/// case for.
/// </para>
/// <para>
/// <b>No pre-filtering by circularity.</b> This lists every live candidate
/// of the requested Kinds; it never excludes the object being moved or its
/// own descendants. <c>IHasParent.MoveAsync</c> already refuses a circular
/// assignment with its own clear reason
/// (<see cref="EngineeringDomain.CircularParentAssignmentException"/>), and
/// the Project Explorer's own drag-and-drop reparenting
/// (<c>WorkspaceViewCoordinator.ObjectMoveRequested</c>) has never
/// pre-filtered this either — a doomed choice fails cleanly downstream
/// rather than needing a second, duplicated tree-walk here.
/// </para>
/// <para>
/// <b>Kind never restricts correctness, only choice.</b> The Domain itself
/// places no Kind restriction on a new parent beyond circularity, so
/// <paramref name="kinds"/> (in <see cref="PickAsync"/>) is offered purely
/// to narrow a long list to the ones a caller expects — an empty list
/// offers every Kind.
/// </para>
/// </remarks>
public sealed class ObjectPickerDialog : Border
{
    /// <summary>The pseudo-row every call offers, regardless of Kind scope or filter text — "no destination" is always a legitimate choice; a parameter's own <see cref="Tempest.Core.Commands.CommandParameter.Validate"/> decides whether this command accepts it.</summary>
    internal const string TopLevelLabel = "(Top level — no parent)";

    private readonly EngineeringDomainContext _domainContext;
    private readonly IProjectContext? _projectContext;

    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeTitle, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading };
    private readonly TextBox _filter = new() { Watermark = "Filter…", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, DesignTokens.SpaceSm) };
    private readonly ListBox _list = new() { MaxHeight = 320 };
    private readonly Button _chooseButton = new() { Content = "Choose", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private IReadOnlyList<Candidate> _candidates = [];
    private TaskCompletionSource<string?>? _pending;

    /// <summary>Initialises a new instance of the <see cref="ObjectPickerDialog"/> class, initially hidden.</summary>
    /// <param name="domainContext">Read to list every live candidate object.</param>
    /// <param name="projectContext">
    /// Read only to order the currently open project's own objects first;
    /// <see langword="null"/> (a fixture with no project concept, or
    /// standalone Engineering) simply leaves every candidate in Kind/name
    /// order.
    /// </param>
    public ObjectPickerDialog(EngineeringDomainContext domainContext, IProjectContext? projectContext = null)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        _domainContext = domainContext;
        _projectContext = projectContext;

        IsVisible = false;
        IsHitTestVisible = true;
        MinWidth = 460;
        MaxWidth = 580;
        MaxHeight = 560;
        CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius);
        Padding = DesignTokens.DialogPadding;
        BorderThickness = new Thickness(1);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceMd, 0, 0) };
        buttons.Children.Add(_cancelButton);
        buttons.Children.Add(_chooseButton);

        var body = new StackPanel();
        body.Children.Add(_title);
        body.Children.Add(_filter);
        body.Children.Add(_list);
        body.Children.Add(buttons);
        Child = body;

        _chooseButton.Classes.Add(ChromeStyles.Primary);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(_filter, "Filter…");
        AutomationProperties.SetName(_list, "Object candidates");
        AutomationProperties.SetName(_chooseButton, "Choose");
        AutomationProperties.SetName(_cancelButton, "Cancel");
        ToolTip.SetTip(_chooseButton, "Choose");
        ToolTip.SetTip(_cancelButton, "Cancel");

        _filter.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) ApplyFilter(); };
        _list.DoubleTapped += (_, _) => TryComplete();
        _chooseButton.Click += (_, _) => TryComplete();
        _cancelButton.Click += (_, _) => Complete(null);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Complete(null);
        };

        DialogModality.Install(this);
    }

    /// <summary>
    /// Shows this picker over every live object of <paramref name="kinds"/>
    /// (or every Kind, if empty), and returns the id chosen — as text, so
    /// this substitutes directly for <see cref="InputDialog.PromptAsync"/>
    /// in a <c>CommandParameterPrompt</c>'s own per-parameter loop —
    /// <see langword="null"/> if the user cancelled, or an empty string for
    /// the "top level/no destination" row.
    /// </summary>
    /// <param name="title">This invocation's own title — typically the command's <see cref="Tempest.Core.Commands.CommandDescriptor.DisplayName"/>.</param>
    /// <param name="kinds">The Kinds to offer, or empty for every Kind.</param>
    /// <param name="cancellationToken">Observed only while the candidate list itself is read.</param>
    public async Task<string?> PickAsync(string title, IReadOnlyList<string> kinds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(kinds);

        _pending?.TrySetResult(null);

        _title.Text = title;
        _filter.Text = string.Empty;
        _candidates = await LoadCandidatesAsync(kinds, cancellationToken).ConfigureAwait(true);
        ApplyFilter();
        IsVisible = true;
        _filter.Focus();

        _pending = new TaskCompletionSource<string?>();
        return await _pending.Task.ConfigureAwait(true);
    }

    private async Task<IReadOnlyList<Candidate>> LoadCandidatesAsync(IReadOnlyList<string> kinds, CancellationToken cancellationToken)
    {
        var all = await _domainContext.Repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        var live = all.Where(o => o is not IDeletable { IsDeleted: true }).ToList();

        var scoped = kinds.Count == 0
            ? live
            : live.Where(o => kinds.Contains(o.Kind, StringComparer.Ordinal)).ToList();

        // Built once, over every live object (not just the scoped subset),
        // so a candidate's own ancestry can be walked purely in memory —
        // one repository read serves both the listing and this ordering.
        var parentById = live.ToDictionary(o => o.Id, o => (o as IHasParent)?.ParentId);
        var openProjectId = _projectContext?.Current?.Id;

        bool InOpenProject(Guid candidateId)
        {
            if (openProjectId is not { } wantProjectId)
                return false;

            // Bounded rather than unbounded: a real cycle is already a
            // Domain defect (MoveAsync refuses one at write time), but a
            // picker listing candidates must never hang scanning one.
            Guid? current = candidateId;
            for (var depth = 0; depth < 64 && current is { } id; depth++)
            {
                if (id == wantProjectId)
                    return true;

                current = parentById.TryGetValue(id, out var parent) ? parent : null;
            }

            return false;
        }

        return [.. scoped
            .Select(o => new Candidate(o.Id, o.Kind, (o as IHasBusinessIdentifier)?.DisplayName ?? o.Id.ToString(), InOpenProject(o.Id)))
            .OrderByDescending(c => c.InOpenProject)
            .ThenBy(c => c.Kind, StringComparer.Ordinal)
            .ThenBy(c => c.DisplayName, StringComparer.Ordinal)];
    }

    private void ApplyFilter()
    {
        var text = _filter.Text ?? string.Empty;

        var matches = string.IsNullOrWhiteSpace(text)
            ? _candidates
            : [.. _candidates.Where(c => c.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase) || c.Kind.Contains(text, StringComparison.OrdinalIgnoreCase))];

        // The "top level" row is always offered, filter or no — see this
        // class's own remarks on why Validate, not this list, is what
        // decides whether a particular command accepts it.
        var items = new List<ListBoxItem> { new() { Content = TopLevelLabel, Tag = string.Empty } };
        items.AddRange(matches.Select(c => new ListBoxItem { Content = $"{c.Kind} — {c.DisplayName}", Tag = c.Id.ToString() }));
        _list.ItemsSource = items;
    }

    private void TryComplete()
    {
        if (_list.SelectedItem is ListBoxItem { Tag: string value })
            Complete(value);
    }

    private void Complete(string? value)
    {
        IsVisible = false;
        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(value);
    }

    private sealed record Candidate(Guid Id, string Kind, string DisplayName, bool InOpenProject);
}
