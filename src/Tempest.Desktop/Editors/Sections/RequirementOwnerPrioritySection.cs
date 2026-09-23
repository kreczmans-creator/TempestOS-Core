using Avalonia.Automation;
using Avalonia.Controls;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Requirements;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Requirements;

namespace Tempest.Desktop.Editors.Sections;

/// <summary>
/// The Requirements Owner/Priority section (`WP 10.7A`; `WP 20.10F`,
/// Product Owner finding D8 — the Owner drop-down of Released people) —
/// gated on <see cref="EditorSectionContext.ObjectKind"/>, never a C#
/// type-check: the data lives entirely in <see cref="IRequirementsService"/>'s
/// own <see cref="IRequirement"/>, unrelated to the real
/// <see cref="IEngineeringObject"/> every other section reads from. Moved
/// verbatim from <see cref="ObjectEditorView"/>'s own former
/// <c>PopulateRequirementAsync</c>/<c>PopulateOwnerOptionsAsync</c>/
/// <c>RebuildOwnerItemsAsync</c>/<c>OnRequirementOwnerSelectionChangedAsync</c>/
/// <c>OnSaveRequirementAsync</c> (`WP 21.1B`).
/// </summary>
/// <remarks>
/// Takes no real use of <paramref name="subject"/> at all — unlike every
/// other section, nothing here ever comes from a real
/// <see cref="IEngineeringObject"/>; a Requirement is never found through
/// the repository (`TD-41`, `WP 19.10I`), so <see cref="LoadAsync"/> is
/// called identically with <see langword="null"/> from both the real-object
/// and Requirement-only population paths — the real-object path's own call
/// is a no-op every time in practice, since <see cref="AppliesTo"/> can
/// only be <see langword="true"/> when <c>ObjectKind</c> is the Requirement
/// Kind, which a real, repository-backed object is never opened as.
/// </remarks>
internal sealed class RequirementOwnerPrioritySection : IEditorSection
{
    private readonly ComboBox _ownerBox = new() { MinHeight = DesignTokens.MinControlSize };
    private readonly ComboBox _priorityBox = new() { MinHeight = DesignTokens.MinControlSize, ItemsSource = new[] { "(none)", "Low", "Medium", "High", "Critical" } };
    private readonly Button _saveButton = new() { Content = "Save Owner/Priority", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _statusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _expander = null!;
    private EditorSectionContext _ctx = null!;

    /// <summary>The literal owner text and person-record-id a fresh <see cref="LoadAsync"/> read last — what <see cref="OnSaveAsync"/> falls back to when the drop-down's own current selection resolves to neither a real person nor the legacy item.</summary>
    private (string? Owner, string? OwnerPersonId) _currentOwner;

    /// <summary>The <see cref="ComboBoxItem.Tag"/> the Owner drop-down's own trailing <b>Add person…</b> row carries.</summary>
    private const string AddPersonOwnerTag = "__wp2010f_add_person__";

    /// <summary>The <see cref="ComboBoxItem.Tag"/> the Owner drop-down's own legacy "(not in People)" row carries.</summary>
    private const string LegacyOwnerTag = "__wp2010f_legacy_owner__";

    /// <summary>The <see cref="ComboBoxItem.Tag"/> a real, Released-person row of the Owner drop-down carries — the record id together with the display name to store on Save.</summary>
    private sealed record OwnerOptionTag(string RecordId, string DisplayName);

    public string Title => "Owner / Priority";

    public bool AppliesTo(IEngineeringObject? subject) =>
        _ctx?.RequirementsService is not null && _ctx.ObjectKind == RequirementsService.RequirementDocumentKind;

    public Control Build(EditorSectionContext ctx)
    {
        _ctx = ctx;

        var panel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        panel.Children.Add(EditorSectionHelpers.LabeledRow("Owner", _ownerBox));
        panel.Children.Add(EditorSectionHelpers.LabeledRow("Priority", _priorityBox));
        panel.Children.Add(_saveButton);
        panel.Children.Add(_statusMessage);

        _saveButton.Classes.Add(ChromeStyles.Primary);
        _saveButton.Click += async (_, _) => await OnSaveAsync().ConfigureAwait(true);
        // `WP 20.10F`: picking the drop-down's own trailing "Add person…"
        // row is an immediate action, not a value to save.
        _ownerBox.SelectionChanged += async (_, _) => await OnOwnerSelectionChangedAsync().ConfigureAwait(true);
        AutomationProperties.SetName(_ownerBox, "Owner");

        _expander = EditorSectionHelpers.BuildSection(Title, panel);
        _expander.IsVisible = false;
        return _expander;
    }

    public async Task LoadAsync(IEngineeringObject? subject, CancellationToken ct)
    {
        var applies = AppliesTo(subject);
        _expander.IsVisible = applies;

        if (!applies)
            return;

        var requirement = await _ctx.RequirementsService!.FindAsync(_ctx.ObjectId).ConfigureAwait(true);
        if (requirement is null)
        {
            _expander.IsVisible = false;
            return;
        }

        _expander.IsVisible = true;
        _currentOwner = (requirement.Owner, requirement.OwnerPersonId);
        await RebuildOwnerItemsAsync(requirement.OwnerPersonId).ConfigureAwait(true);
        _priorityBox.SelectedItem = requirement.Priority?.ToString() ?? "(none)";
        _statusMessage.Text = string.Empty;
    }

    /// <summary>
    /// (Re)builds the Owner drop-down's own items: every Released, active
    /// person, read fresh every time — never cached; the requirement's own
    /// current free-text <see cref="IRequirement.Owner"/> as a leading
    /// "(not in People)" row where it matches no Released person; and,
    /// where <see cref="EditorSectionContext.OwnerSupport"/> is wired, a
    /// trailing <b>Add person…</b> row. <paramref name="preferPersonId"/>
    /// is selected if a matching Released person is found; otherwise the
    /// legacy row is selected when there is a stored owner, and nothing is
    /// selected otherwise.
    /// </summary>
    private async Task RebuildOwnerItemsAsync(string? preferPersonId)
    {
        var items = new List<ComboBoxItem>();
        ComboBoxItem? selected = null;

        if (_ctx.OwnerSupport is not null)
        {
            var released = await _ctx.OwnerSupport.Persons.FindSelectableAsync().ConfigureAwait(true);

            foreach (var record in released)
            {
                var label = string.IsNullOrWhiteSpace(record.Definition.Role)
                    ? record.Definition.DisplayName
                    : $"{record.Definition.DisplayName} ({record.Definition.Role})";
                var item = new ComboBoxItem { Content = label, Tag = new OwnerOptionTag(record.Id, record.Definition.DisplayName) };
                items.Add(item);

                if (preferPersonId is not null && string.Equals(preferPersonId, record.Id, StringComparison.Ordinal))
                    selected = item;
            }
        }

        // An existing requirement whose typed owner matches no person still
        // shows its own text, suffixed "(not in People)" — the drop-down
        // still offers to pick a real person instead.
        if (selected is null && !string.IsNullOrWhiteSpace(_currentOwner.Owner))
        {
            var legacy = new ComboBoxItem { Content = $"{_currentOwner.Owner} (not in People)", Tag = LegacyOwnerTag };
            items.Insert(0, legacy);
            selected = legacy;
        }

        if (_ctx.OwnerSupport is not null)
            items.Add(new ComboBoxItem { Content = "Add person…", Tag = AddPersonOwnerTag });

        _ownerBox.ItemsSource = items;
        _ownerBox.SelectedItem = selected;
    }

    /// <summary>
    /// Picking the trailing "Add person…" row opens
    /// <see cref="RequirementOwnerEditorSupport.AddPersonAsync"/> right
    /// away — never a value to save, an action taken the moment it is
    /// selected. Any other selection (a real person, the legacy row, or
    /// none) is a plain no-op here: it is <see cref="OnSaveAsync"/>'s own
    /// job to read it, not this handler's.
    /// </summary>
    private async Task OnOwnerSelectionChangedAsync()
    {
        if (_ctx.OwnerSupport is null || _ownerBox.SelectedItem is not ComboBoxItem { Tag: AddPersonOwnerTag })
            return;

        var added = await _ctx.OwnerSupport.AddPersonAsync(CancellationToken.None).ConfigureAwait(true);

        // Rebuilds either way: added — the new person now exists, Released,
        // and is preferred/selected below; cancelled — rebuilding from the
        // requirement's own last-known owner is what stops the sentinel
        // row itself from ever being left sitting selected.
        await RebuildOwnerItemsAsync(added?.RecordId ?? _currentOwner.OwnerPersonId).ConfigureAwait(true);

        if (added is { } newPerson)
            _statusMessage.Text = $"Added and released '{newPerson.DisplayName}'.";
    }

    private async Task OnSaveAsync()
    {
        var (owner, ownerPersonId) = _ownerBox.SelectedItem switch
        {
            ComboBoxItem { Tag: OwnerOptionTag option } => ((string?)option.DisplayName, (string?)option.RecordId),
            ComboBoxItem { Tag: LegacyOwnerTag } => (_currentOwner.Owner, (string?)null),
            _ => (_currentOwner.Owner, _currentOwner.OwnerPersonId),
        };

        var ownerResult = await _ctx.CommandDispatcher.DispatchAsync(new SetRequirementOwnerCommand(_ctx.ObjectId, owner, ownerPersonId), CancellationToken.None).ConfigureAwait(true);
        if (!ownerResult.Succeeded)
        {
            _statusMessage.Text = ownerResult.Message ?? "Set owner failed.";
            _ctx.ReportAction(_statusMessage.Text, ActionOutcome.Failed);
            return;
        }

        var priorityText = _priorityBox.SelectedItem as string;
        RequirementPriority? priority = priorityText is null or "(none)" ? null : Enum.Parse<RequirementPriority>(priorityText);
        var priorityResult = await _ctx.CommandDispatcher.DispatchAsync(new SetRequirementPriorityCommand(_ctx.ObjectId, priority), CancellationToken.None).ConfigureAwait(true);
        if (!priorityResult.Succeeded)
        {
            _statusMessage.Text = priorityResult.Message ?? "Set priority failed.";

            // The Owner half already dispatched successfully above, so the
            // workspace did change even though this action failed overall.
            _ctx.ReportAction(_statusMessage.Text, new ActionOutcome(Succeeded: false, WorkspaceChanged: true));
            return;
        }

        // Refresh() before the final message — see BillOfMaterialsSection's own identical remarks.
        await _ctx.RefreshAsync().ConfigureAwait(true);
        _statusMessage.Text = "Owner/Priority saved.";
        _ctx.ReportAction(_statusMessage.Text, ActionOutcome.Changed);
    }

    public void React(WorkspaceChange change)
    {
        // Reserved — see IEditorSection's own remarks.
    }
}
