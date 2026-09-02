using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Calculations;
using Tempest.App.Workspace.Documents;
using Tempest.App.Workspace.Mechanical;
using Tempest.App.Workspace.Requirements;
using Tempest.App.Workspace.Verification;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Requirements;
using Tempest.Core.Verification;
using Tempest.Desktop.DigitalThread;
using Tempest.Desktop.Icons;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Editors;

/// <summary>
/// The Object Editor Framework's own real, tabbed editor control (`WP
/// 10.3A`) — one generic engine, applied uniformly to every real
/// Engineering Domain object across all six disciplines
/// (Mechanical/Requirements/Calculations/Verification/Documents/
/// Manufacturing), realising "Requirements editor"/"Mechanical object
/// editor"/"Calculation editor"/"Verification editor"/"Document
/// editor"/"Manufacturing editor" as the identical, real, working editor
/// applied to each discipline's own real Kind(s) — never six independently
/// hand-built, duplicated editors.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reads directly, mutates only through Commands (`ADR-0063`,
/// unchanged).</b> Every read below (<see cref="IHasRevisions.Content"/>,
/// <see cref="IHasLifecycle.Status"/>/<see cref="IHasLifecycle.History"/>,
/// <see cref="IHasRelationships.GetRelationshipsAsync"/>,
/// <see cref="IValidatable.ValidateAsync"/>) is a direct call against the
/// real Domain object — <see cref="EngineeringDomainContext.Repository"/>'s
/// own already-permitted read surface. Every write (Rename, Revise) goes
/// through <see cref="IWorkspaceManager.RenameObjectAsync"/>/
/// <see cref="IWorkspaceManager.ReviseObjectAsync"/> (`ADR-0096`/`ADR-0097`)
/// — a real, registered <see cref="IWorkspaceCommand"/>, dispatched
/// exactly as every other Workspace mutation already is. This class never
/// calls a mutating Domain method directly.
/// </para>
/// <para>
/// <b>"Editable properties" — two generic fields, plus five real,
/// discipline-specific sections (`WP 10.7A`, Feature Completion, closing
/// `FCR-0068`).</b> Name (<see cref="IWorkspaceManager.CanRename"/>) and
/// Content (<see cref="IWorkspaceManager.CanRevise"/>) remain the two
/// uniformly-real fields every Kind shares, gated per-Kind exactly like
/// <see cref="Tempest.Desktop.Views.PropertyInspectorView"/>'s own
/// established "editable only where a real command exists" discipline.
/// Five further sections are Kind-gated on the real object itself (a C#
/// <see langword="is"/> type-check, the identical idiom
/// <see cref="PopulateLifecycle"/>/<see cref="PopulateRelationships"/>/
/// <see cref="PopulateValidation"/> already use, never a Kind-string
/// switch) or on <see cref="_objectKind"/> directly where the data lives
/// in a service the object graph itself does not expose (Requirements
/// Owner/Priority, Calculations Execute): Mechanical BOM
/// (<see cref="IHasBomLine"/>), Requirements Owner/Priority
/// (<see cref="IRequirementsService"/>), Calculations Execute/Recalculate
/// (<see cref="CalculationTemplateRegistry"/>), Verification Record
/// Result (<see cref="IVerificationActivity"/>), Documents Attachments
/// (<see cref="IHasAttachments"/>) — each dispatches its own
/// already-registered command directly via
/// <see cref="Tempest.Core.Commands.ICommandDispatcher"/>, invisible/
/// collapsed (<c>Expander.IsVisible = false</c>) for every object the
/// gate does not match, never a "not applicable" placeholder row.
/// </para>
/// <para>
/// <b>Validation feedback is real, for the first time in this
/// platform's Workspace/Desktop layer</b> — <see cref="IValidatable.ValidateAsync"/>
/// already existed at the Domain layer (`ADR-0075`) but was never
/// reachable from any Workspace/Desktop surface;
/// <see cref="Tempest.Desktop.Views.PropertyInspectorView"/>'s own "Validation" section
/// remains the disclosed placeholder it always was (unmodified), since
/// it only ever sees <see cref="PropertyFacet"/>s, never the real object.
/// This class holds the real object directly, so it can call the real
/// method — informational only, never blocking Save (see class remarks
/// on <see cref="OnSaveAsync"/>).
/// </para>
/// </remarks>
public sealed class ObjectEditorView : UserControl
{
    private readonly Guid _objectId;
    private readonly string _objectKind;
    private readonly EngineeringDomainContext _domainContext;
    private readonly IWorkspaceManager _manager;
    private readonly Action<Guid, string> _navigateToObject;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly IRequirementsService? _requirementsService;
    private readonly CalculationTemplateRegistry? _calculationTemplates;

    private readonly TextBlock _identityReadout = new() { Opacity = 0.7, FontSize = DesignTokens.FontSizeCaption };
    private readonly TextBox _nameBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _contentBox = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 140, FontSize = DesignTokens.FontSizeBody };
    private readonly ToggleButton _readOnlyToggle = new() { Content = "🔓 Editable", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _saveButton = new() { Content = "💾 Save", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _statusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private readonly StackPanel _lifecyclePanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _relationshipsPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _validationPanel = new() { Spacing = DesignTokens.SpaceXs };

    // WP 10.7A — Feature Completion: five real, discipline-specific
    // sections (see class remarks). Each section's own Expander is
    // collapsed-and-hidden (IsVisible = false) by default and made
    // visible only when PopulateFrom's own gate matches the real target.

    private readonly TextBox _bomQuantityBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _bomUnitOfMeasureBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _bomFindNumberBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _bomItemNumberBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _bomReferenceDesignatorBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _bomSaveButton = new() { Content = "💾 Save BOM Line", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _bomStatusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _bomSection = null!;

    private readonly TextBox _requirementOwnerBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly ComboBox _requirementPriorityBox = new() { MinHeight = DesignTokens.MinControlSize, ItemsSource = new[] { "(none)", "Low", "Medium", "High", "Critical" } };
    private readonly Button _requirementSaveButton = new() { Content = "💾 Save Owner/Priority", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _requirementStatusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _requirementSection = null!;

    private readonly ComboBox _calculationTemplatePicker = new() { MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _calculationInputJsonBox = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80, FontSize = DesignTokens.FontSizeBody, Watermark = "{ ... }" };
    private readonly Button _calculationExecuteButton = new() { Content = "▶ Execute", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _calculationStatusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _calculationSection = null!;
    private IReadOnlyList<CalculationTemplateDescriptor> _availableTemplates = [];
    private bool _calculationHasBeenExecuted;

    private readonly Button _verificationPassButton = new() { Content = "✅ Pass", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _verificationFailButton = new() { Content = "❌ Fail", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _verificationConditionalButton = new() { Content = "⚠ Conditional", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _verificationMethodBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _verificationStatusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _verificationResultSection = null!;

    private readonly StackPanel _attachmentsListPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly TextBox _attachmentFileNameBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _attachmentContentTypeBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _attachmentSizeBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _attachmentAddButton = new() { Content = "📎 Attach", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _attachmentStatusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _attachmentsSection = null!;

    private string _originalName = string.Empty;
    private string _originalContent = string.Empty;
    private bool _isDirty;
    private bool _suppressDirtyTracking;

    private Action<IHasAttachments, IAttachment>? _openAttachmentRequested;

    // The object the sections were last built from, so the attachment rows
    // can be rebuilt when OpenAttachmentRequested gains its first
    // subscriber without going back to the repository for a second read.
    private IEngineeringObject? _populatedTarget;

    /// <summary>Raised whenever <see cref="IsDirty"/> changes.</summary>
    public event Action<bool>? DirtyChanged;

    /// <summary>Raised after Save/Cancel completes, carrying a human-readable status message and its <see cref="ActionOutcome"/> — the caller's own hook to refresh the Status Bar/Cockpit/Property Inspector, mirroring every other Desktop View's own identical <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>
    /// Raised after a successful Rename — carries a ready-to-record
    /// <see cref="UndoableAction"/> (`WP 10.6A`, `ADR-0099`). Reuses
    /// <see cref="IWorkspaceManager.RenameObjectAsync"/>'s own already-
    /// Kind-agnostic dispatch (`ADR-0096`) for both <c>Undo</c> (renames
    /// back to the pre-commit name) and <c>Redo</c> (renames forward to
    /// the new name again) — works identically across all six
    /// disciplines with no per-Kind special-casing, since this is the one
    /// commit path every discipline's own Object Editor already shares.
    /// </summary>
    public event Action<UndoableAction>? UndoableActionRecorded;

    /// <summary>
    /// Raised when the user asks to view one of this object's attachments
    /// (`TD-80`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// An event rather than a direct call into the viewer: this editor
    /// knows an object and its attachments, and deliberately not the
    /// workspace it is docked in. The shell decides where a document
    /// opens, which is what keeps the editor usable outside the docked
    /// workspace and keeps the viewer out of its dependencies.
    /// </para>
    /// <para>
    /// A custom accessor, for one reason found by the `TD-80` visual
    /// audit: <see cref="TryCreate"/> populates the editor before it
    /// returns, so the shell cannot possibly have subscribed by the time
    /// the attachment rows are built — and the rows only carry an Open
    /// button when something can handle it. The button therefore never
    /// existed in the running application, and the whole viewer was
    /// unreachable from the UI until some later refresh happened to rebuild
    /// the section. Re-populating on the first subscriber closes that
    /// ordering hazard where it lives, rather than requiring every caller
    /// to remember to refresh after wiring up.
    /// </para>
    /// </remarks>
    public event Action<IHasAttachments, IAttachment>? OpenAttachmentRequested
    {
        add
        {
            var hadNone = _openAttachmentRequested is null;
            _openAttachmentRequested += value;

            if (hadNone && _openAttachmentRequested is not null && _populatedTarget is not null)
                PopulateAttachments(_populatedTarget);
        }

        remove => _openAttachmentRequested -= value;
    }

    private ObjectEditorView(
        Guid objectId, string objectKind, EngineeringDomainContext domainContext, IWorkspaceManager manager, Action<Guid, string> navigateToObject,
        ICommandDispatcher commandDispatcher, IRequirementsService? requirementsService, CalculationTemplateRegistry? calculationTemplates)
    {
        _objectId = objectId;
        _objectKind = objectKind;
        _domainContext = domainContext;
        _manager = manager;
        _navigateToObject = navigateToObject;
        _commandDispatcher = commandDispatcher;
        _requirementsService = requirementsService;
        _calculationTemplates = calculationTemplates;

        Content = BuildLayout();

        // PropertyChanged, not the TextChanged routed event — fires
        // reliably for every Text value change regardless of source (real
        // user keystrokes or a direct `.Text =` assignment, the latter
        // being how both this class's own Cancel/PopulateFrom and its own
        // headless tests set text), where TextChanged does not always fire
        // for a purely programmatic assignment.
        _nameBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) UpdateDirty(); };
        _contentBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) UpdateDirty(); };
        _readOnlyToggle.IsCheckedChanged += (_, _) => ApplyReadOnlyState();
        _saveButton.Click += async (_, _) => await OnSaveAsync().ConfigureAwait(true);
        _cancelButton.Click += (_, _) => OnCancel();

        // WP 10.7A — Feature Completion: the five new sections' own Save/
        // Execute/Record/Attach actions, each independent of the main
        // Name/Content Save above (a different command, a different
        // buffered-edit lifecycle).
        _bomSaveButton.Click += async (_, _) => await OnSaveBomAsync().ConfigureAwait(true);
        _requirementSaveButton.Click += async (_, _) => await OnSaveRequirementAsync().ConfigureAwait(true);
        _calculationExecuteButton.Click += async (_, _) => await OnExecuteCalculationAsync().ConfigureAwait(true);
        _verificationPassButton.Click += async (_, _) => await OnRecordVerificationResultAsync(VerificationOutcome.Pass).ConfigureAwait(true);
        _verificationFailButton.Click += async (_, _) => await OnRecordVerificationResultAsync(VerificationOutcome.Fail).ConfigureAwait(true);
        _verificationConditionalButton.Click += async (_, _) => await OnRecordVerificationResultAsync(VerificationOutcome.Conditional).ConfigureAwait(true);
        _attachmentAddButton.Click += async (_, _) => await OnAttachAsync().ConfigureAwait(true);
    }

    /// <summary>Gets whether this editor holds local, buffered edits (Name and/or Content) not yet committed via Save — this Work Package's own genuine, buffered dirty-state (distinct from and unrelated to <see cref="IWorkspaceView.IsDirty"/>, which remains permanently <see langword="false"/>, by design, unchanged — see class remarks).</summary>
    public bool IsDirty => _isDirty;

    /// <summary>
    /// Attempts to build a real Object Editor for <paramref name="objectId"/>/
    /// <paramref name="objectKind"/> — returns <see langword="null"/> if no
    /// Engineering Domain object with that Id is found (a synthetic,
    /// non-repository Kind such as Calculations' own <c>"CalculationTemplate"</c>,
    /// or the Sample Explorer's own fixed, fictional content) — the
    /// caller's own signal to fall back to the existing generic
    /// three-line document body instead.
    /// </summary>
    public static ObjectEditorView? TryCreate(
        Guid objectId, string objectKind, EngineeringDomainContext domainContext, IWorkspaceManager manager, Action<Guid, string> navigateToObject,
        ICommandDispatcher commandDispatcher, IRequirementsService? requirementsService = null, CalculationTemplateRegistry? calculationTemplates = null)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(navigateToObject);
        ArgumentNullException.ThrowIfNull(commandDispatcher);

        var target = domainContext.Repository.FindAsync(objectId).GetAwaiter().GetResult();
        if (target is null)
            return null;

        var editor = new ObjectEditorView(objectId, objectKind, domainContext, manager, navigateToObject, commandDispatcher, requirementsService, calculationTemplates);
        editor.PopulateFrom(target);
        return editor;
    }

    /// <summary>Re-reads the real object and refreshes every section — never a cached copy, mirroring <see cref="IWorkspaceView.RefreshAsync"/>'s own identical discipline.</summary>
    public void Refresh()
    {
        var target = _domainContext.Repository.FindAsync(_objectId).GetAwaiter().GetResult();
        if (target is not null)
            PopulateFrom(target);
    }

    private Control BuildLayout()
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto") };
        var titleStack = new StackPanel();
        titleStack.Children.Add(new TextBlock { Text = $"{IconRegistry.Resolve(_objectKind)} {_objectKind}", FontSize = DesignTokens.FontSizeTitle, FontWeight = FontWeight.Bold });
        titleStack.Children.Add(_identityReadout);
        Grid.SetColumn(titleStack, 0);
        Grid.SetColumn(_readOnlyToggle, 1);
        Grid.SetColumn(_cancelButton, 2);
        Grid.SetColumn(_saveButton, 3);
        header.Children.Add(titleStack);
        header.Children.Add(_readOnlyToggle);
        header.Children.Add(_cancelButton);
        header.Children.Add(_saveButton);

        var identitySection = BuildSection("Identity", new StackPanel { Spacing = DesignTokens.SpaceXs, Children = { LabeledRow("Name", _nameBox) } });
        var contentSection = BuildSection("Content", _contentBox);
        var lifecycleSection = BuildSection("Lifecycle", _lifecyclePanel);
        var relationshipsSection = BuildSection("Relationships", _relationshipsPanel);
        var validationSection = BuildSection("Validation", _validationPanel);

        // WP 10.7A — Feature Completion: five real, discipline-specific
        // sections (see class remarks) — each collapsed-and-hidden by
        // default, made visible only for the Kind/object it genuinely
        // applies to.
        var bomPanel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        bomPanel.Children.Add(LabeledRow("Quantity", _bomQuantityBox));
        bomPanel.Children.Add(LabeledRow("Unit of Measure", _bomUnitOfMeasureBox));
        bomPanel.Children.Add(LabeledRow("Find Number", _bomFindNumberBox));
        bomPanel.Children.Add(LabeledRow("Item Number", _bomItemNumberBox));
        bomPanel.Children.Add(LabeledRow("Reference Designator", _bomReferenceDesignatorBox));
        bomPanel.Children.Add(_bomSaveButton);
        bomPanel.Children.Add(_bomStatusMessage);
        _bomSection = BuildSection("Bill of Materials", bomPanel);
        _bomSection.IsVisible = false;

        var requirementPanel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        requirementPanel.Children.Add(LabeledRow("Owner", _requirementOwnerBox));
        requirementPanel.Children.Add(LabeledRow("Priority", _requirementPriorityBox));
        requirementPanel.Children.Add(_requirementSaveButton);
        requirementPanel.Children.Add(_requirementStatusMessage);
        _requirementSection = BuildSection("Owner / Priority", requirementPanel);
        _requirementSection.IsVisible = false;

        var calculationPanel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        calculationPanel.Children.Add(LabeledRow("Template", _calculationTemplatePicker));
        calculationPanel.Children.Add(new TextBlock { Text = "Input (JSON):", Opacity = 0.8, FontSize = DesignTokens.FontSizeBody });
        calculationPanel.Children.Add(_calculationInputJsonBox);
        calculationPanel.Children.Add(_calculationExecuteButton);
        calculationPanel.Children.Add(_calculationStatusMessage);
        _calculationSection = BuildSection("Execute", calculationPanel);
        _calculationSection.IsVisible = false;

        var verificationResultPanel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        verificationResultPanel.Children.Add(LabeledRow("Method", _verificationMethodBox));
        var verificationButtonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceXs };
        verificationButtonRow.Children.Add(_verificationPassButton);
        verificationButtonRow.Children.Add(_verificationFailButton);
        verificationButtonRow.Children.Add(_verificationConditionalButton);
        verificationResultPanel.Children.Add(verificationButtonRow);
        verificationResultPanel.Children.Add(_verificationStatusMessage);
        _verificationResultSection = BuildSection("Record Result", verificationResultPanel);
        _verificationResultSection.IsVisible = false;

        var attachmentsPanel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        attachmentsPanel.Children.Add(_attachmentsListPanel);
        attachmentsPanel.Children.Add(new Separator());
        attachmentsPanel.Children.Add(LabeledRow("File Name", _attachmentFileNameBox));
        attachmentsPanel.Children.Add(LabeledRow("Content Type", _attachmentContentTypeBox));
        attachmentsPanel.Children.Add(LabeledRow("Size (bytes)", _attachmentSizeBox));
        attachmentsPanel.Children.Add(_attachmentAddButton);
        attachmentsPanel.Children.Add(_attachmentStatusMessage);
        _attachmentsSection = BuildSection("Attachments", attachmentsPanel);
        _attachmentsSection.IsVisible = false;

        var body = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        body.Children.Add(header);
        body.Children.Add(_statusMessage);
        body.Children.Add(new Separator());
        body.Children.Add(identitySection);
        body.Children.Add(contentSection);
        body.Children.Add(_bomSection);
        body.Children.Add(_requirementSection);
        body.Children.Add(_calculationSection);
        body.Children.Add(_verificationResultSection);
        body.Children.Add(_attachmentsSection);
        body.Children.Add(lifecycleSection);
        body.Children.Add(relationshipsSection);
        body.Children.Add(validationSection);

        return new ScrollViewer { Content = body };
    }

    private static Expander BuildSection(string title, Control content) => new()
    {
        Header = title,
        IsExpanded = true,
        Margin = DesignTokens.SectionMargin,
        Content = content,
    };

    private static Control LabeledRow(string label, Control valueControl)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("100,*") };
        var text = new TextBlock { Text = label, Opacity = 0.8, FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(text, 0);
        Grid.SetColumn(valueControl, 1);
        row.Children.Add(text);
        row.Children.Add(valueControl);
        return row;
    }

    private void PopulateFrom(IEngineeringObject target)
    {
        _suppressDirtyTracking = true;
        _populatedTarget = target;

        var identifier = (target as IHasBusinessIdentifier)?.Identifier;
        _identityReadout.Text = identifier is null
            ? $"Id: {target.Id}  •  Revision {target.CurrentRevisionNumber}"
            : $"{identifier}  •  Id: {target.Id}  •  Revision {target.CurrentRevisionNumber}";

        _originalName = (target as IHasBusinessIdentifier)?.DisplayName ?? _objectKind;
        _nameBox.Text = _originalName;
        _nameBox.IsEnabled = _manager.CanRename(_objectKind);

        _originalContent = (target as IHasRevisions)?.Content ?? string.Empty;
        _contentBox.Text = _originalContent;
        _contentBox.IsEnabled = _manager.CanRevise(_objectKind);

        PopulateBom(target);
        PopulateRequirement(target);
        PopulateCalculationExecution(target);
        PopulateVerificationResult(target);
        PopulateAttachments(target);

        PopulateLifecycle(target);
        PopulateRelationships(target);
        PopulateValidation(target);

        _isDirty = false;
        _statusMessage.Text = string.Empty;
        ApplyReadOnlyState();

        _suppressDirtyTracking = false;
    }

    private void PopulateLifecycle(IEngineeringObject target)
    {
        _lifecyclePanel.Children.Clear();

        if (target is not IHasLifecycle lifecycle)
        {
            _lifecyclePanel.Children.Add(new TextBlock { Text = "This object carries no lifecycle.", Opacity = 0.7 });
            return;
        }

        // A real, coloured status badge (`WP 10.5A`, "improved lifecycle
        // presentation") — reuses `LifecycleColors` exactly as built for
        // the Digital Thread graph (`WP 10.4A`), so a status reads
        // identically whichever surface shows it, never two different
        // colour languages for the same `LifecycleState`.
        var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        statusRow.Children.Add(new Border { Width = 10, Height = 10, Background = LifecycleColors.Resolve(lifecycle.Status), CornerRadius = new CornerRadius(5), VerticalAlignment = VerticalAlignment.Center });
        statusRow.Children.Add(new TextBlock { Text = $"Status: {lifecycle.Status}", FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        _lifecyclePanel.Children.Add(statusRow);

        if (lifecycle.History.Count == 0)
        {
            _lifecyclePanel.Children.Add(new TextBlock { Text = "No transitions recorded yet.", Opacity = 0.7, FontSize = DesignTokens.FontSizeCaption });
            return;
        }

        foreach (var record in lifecycle.History.TakeLast(5).Reverse())
        {
            _lifecyclePanel.Children.Add(new TextBlock
            {
                Text = $"{record.From} → {record.To}   ({record.OccurredAt:yyyy-MM-dd HH:mm} UTC)",
                FontSize = DesignTokens.FontSizeCaption,
                Opacity = 0.8,
            });
        }
    }

    /// <summary>
    /// The Relationship summary (`WP 10.3A`) — a real, flat list, both
    /// directions (outgoing via <see cref="IHasRelationships.GetRelationshipsAsync"/>,
    /// incoming via <see cref="EngineeringDomainContext.RelationshipRepository"/>
    /// directly, both already-permitted reads, `ADR-0063`). Deliberately
    /// flat, never a node-link graph — <c>ADR-0093</c>'s own Digital Thread
    /// graph is explicitly out of this Work Package's own scope; each row
    /// is independently, honestly presented, never composed into a
    /// traversable structure.
    /// </summary>
    private void PopulateRelationships(IEngineeringObject target)
    {
        _relationshipsPanel.Children.Clear();

        if (target is IHasRelationships hasRelationships)
        {
            var outgoing = hasRelationships.GetRelationshipsAsync().GetAwaiter().GetResult();
            foreach (var relationship in outgoing)
                _relationshipsPanel.Children.Add(BuildRelationshipRow(relationship.TargetId, relationship.RelationshipKind, "→"));
        }

        var incoming = _domainContext.RelationshipRepository.GetIncomingAsync(_objectId).GetAwaiter().GetResult();
        foreach (var relationship in incoming)
            _relationshipsPanel.Children.Add(BuildRelationshipRow(relationship.SourceId, relationship.RelationshipKind, "←"));

        if (_relationshipsPanel.Children.Count == 0)
            _relationshipsPanel.Children.Add(new TextBlock { Text = "No relationships recorded.", Opacity = 0.7 });
    }

    /// <summary>Builds one relationship row — "Navigation between related objects" (`WP 10.3A`), reusing <see cref="INavigationService.OpenAsync"/> via the injected navigate callback, never a new navigation mechanism.</summary>
    private Control BuildRelationshipRow(Guid otherId, string relationshipKind, string direction)
    {
        var other = _domainContext.Repository.FindAsync(otherId).GetAwaiter().GetResult();
        var displayName = (other as IHasBusinessIdentifier)?.DisplayName ?? otherId.ToString();
        var otherKind = other?.Kind ?? _objectKind;

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Avalonia.Thickness(0, DesignTokens.SpaceXs) };

        var icon = new TextBlock { Text = IconRegistry.Resolve(otherKind), Margin = new Avalonia.Thickness(0, 0, DesignTokens.SpaceSm, 0) };
        var text = new TextBlock { Text = $"{direction} {relationshipKind} — {displayName}", TextWrapping = TextWrapping.Wrap, FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Center };

        Grid.SetColumn(icon, 0);
        Grid.SetColumn(text, 1);
        row.Children.Add(icon);
        row.Children.Add(text);

        if (other is not null)
        {
            var openButton = new Button { Content = "Open →", FontSize = DesignTokens.FontSizeCaption, Padding = new Avalonia.Thickness(DesignTokens.SpaceSm, DesignTokens.SpaceXs) };
            openButton.Click += (_, _) => _navigateToObject(otherId, otherKind);
            Grid.SetColumn(openButton, 2);
            row.Children.Add(openButton);
        }

        return row;
    }

    /// <summary>
    /// The Validation summary (`WP 10.3A`) — a real, live
    /// <see cref="IValidatable.ValidateAsync"/> read, genuinely closing
    /// the gap <see cref="Tempest.Desktop.Views.PropertyInspectorView"/>'s own disclosed
    /// placeholder names ("no per-object validation-result read exists
    /// anywhere in the Workspace layer") — that gap was true of the
    /// Workspace/Property-Facet layer specifically; the underlying Domain
    /// capability (`ADR-0075`) always existed. Informational only.
    /// </summary>
    private void PopulateValidation(IEngineeringObject target)
    {
        _validationPanel.Children.Clear();

        if (target is not IValidatable validatable)
        {
            _validationPanel.Children.Add(new TextBlock { Text = "This object supports no validation.", Opacity = 0.7 });
            return;
        }

        var result = validatable.ValidateAsync().GetAwaiter().GetResult();

        if (result.IsValid && result.Warnings.Count == 0)
        {
            _validationPanel.Children.Add(BuildSeverityRow(FeedbackSeverity.Success, "No issues found."));
            return;
        }

        foreach (var error in result.Errors)
            _validationPanel.Children.Add(BuildSeverityRow(FeedbackSeverity.Error, error.Message));

        foreach (var warning in result.Warnings)
            _validationPanel.Children.Add(BuildSeverityRow(FeedbackSeverity.Warning, warning.Message));
    }

    /// <summary>
    /// One validation-summary row (`WP 10.5A`, "improved validation
    /// display") — a real severity glyph and colour
    /// (<see cref="SeverityColors"/>), the identical vocabulary Toast/
    /// ConfirmationDialog now share platform-wide, replacing this
    /// method's own previously-inconsistent, partly emoji-based glyphs
    /// (<c>✅</c>/<c>⛔</c>) and locally-hardcoded colours. `internal`,
    /// not `private` (`WP 10.8A`) — <see cref="Tempest.Desktop.Views.PropertyInspectorView"/>'s
    /// own real Validation section reuses this exact row shape rather
    /// than duplicating it, the identical "no duplicated logic"
    /// discipline this Work Package's own controlling instruction names.
    /// </summary>
    internal static Control BuildSeverityRow(FeedbackSeverity severity, string message)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm, Margin = new Thickness(0, DesignTokens.SpaceXs) };
        row.Children.Add(new TextBlock { Text = SeverityColors.Glyph(severity), Foreground = SeverityColors.Resolve(severity), FontSize = DesignTokens.IconSizeSmall, VerticalAlignment = VerticalAlignment.Top });
        row.Children.Add(new TextBlock { Text = message, Foreground = SeverityColors.Resolve(severity), TextWrapping = TextWrapping.Wrap, FontSize = DesignTokens.FontSizeBody, MaxWidth = 420 });
        return row;
    }

    /// <summary>
    /// The Mechanical BOM section (`WP 10.7A`) — gated on
    /// <see cref="IHasBomLine"/>, the identical <see langword="is"/>
    /// type-check idiom every other section already uses. Reads the real
    /// object directly (already-permitted, `ADR-0063`); writes through
    /// <see cref="SetBomLineCommand"/>, dispatched via
    /// <see cref="ICommandDispatcher"/> — the same command
    /// <see cref="Tempest.Desktop.Views.PropertyInspectorView"/>'s own
    /// read-only BOM display (`MechanicalPropertyFacetProvider`) already
    /// reads the identical fields from, now given a real write path here
    /// for the first time.
    /// </summary>
    private void PopulateBom(IEngineeringObject target)
    {
        if (target is not IHasBomLine bomLine)
        {
            _bomSection.IsVisible = false;
            return;
        }

        _bomSection.IsVisible = true;
        _bomQuantityBox.Text = bomLine.Quantity.ToString(CultureInfo.InvariantCulture);
        _bomUnitOfMeasureBox.Text = bomLine.UnitOfMeasure ?? string.Empty;
        _bomFindNumberBox.Text = bomLine.FindNumber ?? string.Empty;
        _bomItemNumberBox.Text = bomLine.ItemNumber ?? string.Empty;
        _bomReferenceDesignatorBox.Text = bomLine.ReferenceDesignator ?? string.Empty;
        _bomStatusMessage.Text = string.Empty;
    }

    private async Task OnSaveBomAsync()
    {
        if (!decimal.TryParse(_bomQuantityBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
        {
            _bomStatusMessage.Text = "Quantity must be a positive number.";
            return;
        }

        var result = await _commandDispatcher.DispatchAsync(
            new SetBomLineCommand(
                _objectId, _objectKind, quantity,
                NullIfEmpty(_bomUnitOfMeasureBox.Text), NullIfEmpty(_bomFindNumberBox.Text),
                NullIfEmpty(_bomItemNumberBox.Text), NullIfEmpty(_bomReferenceDesignatorBox.Text)),
            CancellationToken.None).ConfigureAwait(true);

        // Refresh() first — it re-runs PopulateBom, which resets this
        // section's own status message to empty as part of a clean
        // re-read; setting the real outcome message only after Refresh()
        // returns is what makes it actually survive to be seen, rather
        // than being immediately overwritten by the same success path
        // that produced it.
        var message = result.Succeeded ? "BOM line saved." : result.Message ?? "Save failed.";
        if (result.Succeeded)
            Refresh();
        _bomStatusMessage.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(result.Succeeded));
    }

    /// <summary>
    /// The Requirements Owner/Priority section (`WP 10.7A`) — gated on
    /// <see cref="_objectKind"/> (never a C# type-check: the data lives
    /// entirely in <see cref="IRequirementsService"/>'s own
    /// <c>Tempest.Core.Requirements.IRequirement</c>, a genuinely
    /// different, unrelated interface from the
    /// <c>Tempest.Core.EngineeringDomain.IRequirement</c> <paramref name="target"/>
    /// itself satisfies — casting <paramref name="target"/> can never
    /// expose Owner/Priority). <see langword="null"/> <see cref="_requirementsService"/>
    /// (any existing test/caller that never threads it through) leaves
    /// this section honestly hidden, never a crash.
    /// </summary>
    private void PopulateRequirement(IEngineeringObject target)
    {
        _ = target;

        if (_requirementsService is null || _objectKind != RequirementsService.RequirementDocumentKind)
        {
            _requirementSection.IsVisible = false;
            return;
        }

        var requirement = _requirementsService.FindAsync(_objectId).GetAwaiter().GetResult();
        if (requirement is null)
        {
            _requirementSection.IsVisible = false;
            return;
        }

        _requirementSection.IsVisible = true;
        _requirementOwnerBox.Text = requirement.Owner ?? string.Empty;
        _requirementPriorityBox.SelectedItem = requirement.Priority?.ToString() ?? "(none)";
        _requirementStatusMessage.Text = string.Empty;
    }

    private async Task OnSaveRequirementAsync()
    {
        var owner = NullIfEmpty(_requirementOwnerBox.Text);
        var ownerResult = await _commandDispatcher.DispatchAsync(new SetRequirementOwnerCommand(_objectId, owner), CancellationToken.None).ConfigureAwait(true);
        if (!ownerResult.Succeeded)
        {
            _requirementStatusMessage.Text = ownerResult.Message ?? "Set owner failed.";
            ActionCompleted?.Invoke(_requirementStatusMessage.Text, ActionOutcome.Failed);
            return;
        }

        var priorityText = _requirementPriorityBox.SelectedItem as string;
        RequirementPriority? priority = priorityText is null or "(none)" ? null : Enum.Parse<RequirementPriority>(priorityText);
        var priorityResult = await _commandDispatcher.DispatchAsync(new SetRequirementPriorityCommand(_objectId, priority), CancellationToken.None).ConfigureAwait(true);
        if (!priorityResult.Succeeded)
        {
            _requirementStatusMessage.Text = priorityResult.Message ?? "Set priority failed.";

            // The Owner half already dispatched successfully above, so the
            // workspace did change even though this action failed overall.
            ActionCompleted?.Invoke(_requirementStatusMessage.Text, new ActionOutcome(Succeeded: false, WorkspaceChanged: true));
            return;
        }

        // Refresh() before the final message — see OnSaveBomAsync's own identical remarks.
        Refresh();
        _requirementStatusMessage.Text = "Owner/Priority saved.";
        ActionCompleted?.Invoke(_requirementStatusMessage.Text, ActionOutcome.Changed);
    }

    /// <summary>
    /// The Calculations Execute/Recalculate section (`WP 10.7A`) — gated
    /// on <see cref="_objectKind"/> and a non-null <see cref="_calculationTemplates"/>.
    /// Whether the target has ever been executed is read from its own
    /// already-established <c>"calculatedBy"</c> relationship (the same
    /// read <see cref="PopulateRelationships"/> already performs), never
    /// a separate mechanism; the label/command chosen (Execute vs
    /// Recalculate) follows directly, honestly, from that real read.
    /// <c>TD-29</c> (Technical Debt Register) already discloses the
    /// executed input cannot be recovered/pre-filled — the JSON field
    /// starts empty every time, never a fabricated "same as last time"
    /// default.
    /// </summary>
    private void PopulateCalculationExecution(IEngineeringObject target)
    {
        if (_calculationTemplates is null || _objectKind is not ("Calculation" or "CalculationSet"))
        {
            _calculationSection.IsVisible = false;
            return;
        }

        _calculationSection.IsVisible = true;
        _availableTemplates = _calculationTemplates.Templates;
        _calculationTemplatePicker.ItemsSource = _availableTemplates.Select(t => $"{t.Metadata.Name} ({t.CalculationId})").ToList();
        if (_availableTemplates.Count > 0)
            _calculationTemplatePicker.SelectedIndex = 0;

        _calculationHasBeenExecuted = target is IHasRelationships hasRelationships
            && hasRelationships.GetRelationshipsAsync().GetAwaiter().GetResult()
                .Any(r => r.RelationshipKind == CalculationTemplateRegistry.CalculatedByRelationshipKind);

        _calculationExecuteButton.Content = _calculationHasBeenExecuted ? "↻ Recalculate" : "▶ Execute";
        _calculationInputJsonBox.Text = string.Empty;
        _calculationStatusMessage.Text = string.Empty;
    }

    private async Task OnExecuteCalculationAsync()
    {
        if (_calculationTemplatePicker.SelectedIndex < 0 || _calculationTemplatePicker.SelectedIndex >= _availableTemplates.Count)
        {
            _calculationStatusMessage.Text = "Choose a Calculation Template first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_calculationInputJsonBox.Text))
        {
            _calculationStatusMessage.Text = "Input (JSON) is required.";
            return;
        }

        var calculationId = _availableTemplates[_calculationTemplatePicker.SelectedIndex].CalculationId;
        IWorkspaceCommand command = _calculationHasBeenExecuted
            ? new RecalculateCalculationCommand(_objectId, _objectKind, calculationId, _calculationInputJsonBox.Text)
            : new ExecuteCalculationCommand(_objectId, _objectKind, calculationId, _calculationInputJsonBox.Text);

        var result = await _commandDispatcher.DispatchAsync(command, CancellationToken.None).ConfigureAwait(true);
        // Refresh() before the final message — see OnSaveBomAsync's own identical remarks.
        var message = result.Succeeded ? "Executed." : result.Message ?? "Execution failed.";
        if (result.Succeeded)
            Refresh();
        _calculationStatusMessage.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(result.Succeeded));
    }

    /// <summary>
    /// The Verification Record Result section (`WP 10.7A`) — gated on
    /// <see cref="IVerificationActivity"/>. Criteria/Evidence/linked-Id
    /// lists are left at <see cref="RecordVerificationResultCommand"/>'s
    /// own empty defaults — an honest minimum-viable interaction (Outcome
    /// + Method), never a partial fake one collecting fields it cannot
    /// yet honestly present.
    /// </summary>
    private void PopulateVerificationResult(IEngineeringObject target)
    {
        if (target is not IVerificationActivity verificationActivity)
        {
            _verificationResultSection.IsVisible = false;
            return;
        }

        _verificationResultSection.IsVisible = true;
        _verificationMethodBox.Text = verificationActivity.Method;
        _verificationStatusMessage.Text = string.Empty;
    }

    private async Task OnRecordVerificationResultAsync(VerificationOutcome outcome)
    {
        var method = string.IsNullOrWhiteSpace(_verificationMethodBox.Text) ? "Inspection" : _verificationMethodBox.Text;

        var result = await _commandDispatcher.DispatchAsync(
            new RecordVerificationResultCommand(_objectId, _objectKind, outcome, method),
            CancellationToken.None).ConfigureAwait(true);

        // Refresh() before the final message — see OnSaveBomAsync's own identical remarks.
        var message = result.Succeeded ? $"Result recorded: {outcome}." : result.Message ?? "Record result failed.";
        if (result.Succeeded)
            Refresh();
        _verificationStatusMessage.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(result.Succeeded));
    }

    /// <summary>
    /// The Documents Attachments section (`WP 10.7A`) — gated on
    /// <see cref="IHasAttachments"/>. Lists already-attached metadata via
    /// the real <see cref="IHasAttachments.GetAttachmentsAsync"/> read;
    /// the Attach mini-form collects the metadata an attachment carries.
    ///
    /// `TD-80`: each attachment now also offers <b>Open</b>, which is the
    /// entry point to the real viewer. It is offered for every attachment
    /// rather than only for those with stored content, because "this
    /// attachment has no content" is something the viewer says clearly and
    /// a disabled button does not — a greyed-out Open leaves the user
    /// guessing whether the file is missing, the format is unsupported, or
    /// the application is broken.
    /// </summary>
    private void PopulateAttachments(IEngineeringObject target)
    {
        if (target is not IHasAttachments attachable)
        {
            _attachmentsSection.IsVisible = false;
            return;
        }

        _attachmentsSection.IsVisible = true;
        _attachmentsListPanel.Children.Clear();

        var attachments = attachable.GetAttachmentsAsync().GetAwaiter().GetResult();
        if (attachments.Count == 0)
        {
            _attachmentsListPanel.Children.Add(new TextBlock { Text = "No attachments recorded.", Opacity = 0.7 });
        }
        else
        {
            foreach (var attachment in attachments)
            {
                var row = new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"📎 {attachment.FileName}  ({attachment.ContentType}, {attachment.SizeInBytes:N0} bytes)",
                            FontSize = DesignTokens.FontSizeBody,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        },
                    },
                };

                if (_openAttachmentRequested is not null)
                {
                    var open = new Button { Content = "Open", Padding = new Thickness(10, 1), FontSize = DesignTokens.FontSizeBody };
                    var captured = attachment;
                    open.Click += (_, _) => _openAttachmentRequested?.Invoke(attachable, captured);
                    row.Children.Add(open);
                }

                _attachmentsListPanel.Children.Add(row);
            }
        }

        _attachmentFileNameBox.Text = string.Empty;
        _attachmentContentTypeBox.Text = string.Empty;
        _attachmentSizeBox.Text = string.Empty;
        _attachmentStatusMessage.Text = string.Empty;
    }

    private async Task OnAttachAsync()
    {
        if (string.IsNullOrWhiteSpace(_attachmentFileNameBox.Text))
        {
            _attachmentStatusMessage.Text = "A file name is required.";
            return;
        }

        if (!long.TryParse(_attachmentSizeBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var sizeInBytes) || sizeInBytes < 0)
        {
            _attachmentStatusMessage.Text = "Size (bytes) must be a non-negative whole number.";
            return;
        }

        var contentType = NullIfEmpty(_attachmentContentTypeBox.Text) ?? "application/octet-stream";

        var result = await _commandDispatcher.DispatchAsync(
            new AttachDocumentCommand(_objectId, _objectKind, _attachmentFileNameBox.Text, contentType, sizeInBytes),
            CancellationToken.None).ConfigureAwait(true);

        // Refresh() before the final message — see OnSaveBomAsync's own identical remarks.
        var message = result.Succeeded ? "Attached." : result.Message ?? "Attach failed.";
        if (result.Succeeded)
            Refresh();
        _attachmentStatusMessage.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(result.Succeeded));
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private void UpdateDirty()
    {
        if (_suppressDirtyTracking)
            return;

        var newDirty = _nameBox.Text != _originalName || _contentBox.Text != _originalContent;
        if (newDirty == _isDirty)
            return;

        _isDirty = newDirty;
        ApplySaveEnabled();
        DirtyChanged?.Invoke(_isDirty);
    }

    private void ApplyReadOnlyState()
    {
        var readOnly = _readOnlyToggle.IsChecked == true;
        _readOnlyToggle.Content = readOnly ? "🔒 Read-Only" : "🔓 Editable";

        if (readOnly && _isDirty)
            OnCancel();

        _nameBox.IsEnabled = !readOnly && _manager.CanRename(_objectKind);
        _contentBox.IsEnabled = !readOnly && _manager.CanRevise(_objectKind);
        ApplySaveEnabled();
    }

    private void ApplySaveEnabled() => _saveButton.IsEnabled = _isDirty && _readOnlyToggle.IsChecked != true;

    /// <summary>
    /// Commits buffered edits via real Commands (`ADR-0063`) — Rename
    /// first (<see cref="IWorkspaceManager.RenameObjectAsync"/>,
    /// `ADR-0096`), then Revise (<see cref="IWorkspaceManager.ReviseObjectAsync"/>,
    /// `ADR-0097`), only for whichever field actually changed. Validation
    /// feedback (<see cref="PopulateValidation"/>) is informational only
    /// and never blocks Save — this platform's <see cref="IValidationRuleSet"/>
    /// has no notion of "which errors are caused by this specific edit,"
    /// so blocking on any pre-existing finding, including ones unrelated
    /// to the field just changed, would be surprising rather than helpful
    /// — a disclosed, deliberate scope decision (`WP10.3A UX Review.md`
    /// §3), not an oversight.
    /// </summary>
    private async Task OnSaveAsync()
    {
        var nameChanged = _nameBox.Text != _originalName;
        var contentChanged = _contentBox.Text != _originalContent;

        if (nameChanged && _manager.CanRename(_objectKind))
        {
            var oldName = _originalName;
            var newName = _nameBox.Text ?? string.Empty;
            var result = await _manager.RenameObjectAsync(_objectId, _objectKind, newName).ConfigureAwait(true);
            if (!result.Succeeded)
            {
                _statusMessage.Text = result.Message ?? "Rename failed.";
                ActionCompleted?.Invoke(_statusMessage.Text, ActionOutcome.Failed);
                return;
            }

            var objectId = _objectId;
            var objectKind = _objectKind;
            var manager = _manager;
            UndoableActionRecorded?.Invoke(new UndoableAction(
                $"Rename to '{newName}'",
                undo: ct => manager.RenameObjectAsync(objectId, objectKind, oldName, ct),
                redo: ct => manager.RenameObjectAsync(objectId, objectKind, newName, ct)));
        }

        if (contentChanged && _manager.CanRevise(_objectKind))
        {
            var result = await _manager.ReviseObjectAsync(_objectId, _objectKind, _contentBox.Text ?? string.Empty).ConfigureAwait(true);
            if (!result.Succeeded)
            {
                _statusMessage.Text = result.Message ?? "Revise failed.";

                // A rename in the same Save may already have been applied
                // above, in which case the workspace did change even
                // though this Save failed overall.
                var renameApplied = nameChanged && _manager.CanRename(_objectKind);
                ActionCompleted?.Invoke(_statusMessage.Text, new ActionOutcome(Succeeded: false, WorkspaceChanged: renameApplied));
                return;
            }
        }

        Refresh();
        DirtyChanged?.Invoke(false);
        _statusMessage.Text = "Saved.";
        ActionCompleted?.Invoke(_statusMessage.Text, ActionOutcome.Changed);
    }

    private void OnCancel()
    {
        _suppressDirtyTracking = true;
        _nameBox.Text = _originalName;
        _contentBox.Text = _originalContent;
        _suppressDirtyTracking = false;

        if (_isDirty)
        {
            _isDirty = false;
            ApplySaveEnabled();
            DirtyChanged?.Invoke(false);
        }

        _statusMessage.Text = string.Empty;
    }
}
