using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Tempest.Workspace;
using Tempest.Workspace.Calculations;
using Tempest.Workspace.Documents;
using Tempest.Workspace.Editors;
using Tempest.Workspace.Evidence;
using Tempest.Workspace.Files;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Projects;
using Tempest.Workspace.Requirements;
using Tempest.Workspace.Verification;
using Tempest.Core.Audit;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Evidence;
using Tempest.Core.ReferenceData;
using Tempest.Core.Requirements;
using Tempest.Core.Verification;
using Tempest.Desktop.DigitalThread;
using Tempest.Desktop.Icons;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Editors;

/// <summary>
/// The Evidence-specific collaborators the declaration-per-Kind Evidence
/// sections need (`WP 18.2A`, §4): a file picker for the Files section's
/// own "add via picker" affordance, and the two picker delegates the
/// Citations/Declared-figures sections use. Each delegate mirrors
/// <see cref="Views.RibbonView.ParameterPrompt"/>'s own stubbable shape,
/// so a journey test can drive Cite/Declare with no dialog ever on
/// screen. <see langword="null"/> (any test that constructs an editor
/// directly without it) leaves every Evidence-writing action honestly
/// unavailable rather than run without asking — the identical discipline
/// <see cref="ObjectEditorView.WorkspaceChanges"/> already established.
/// </summary>
public sealed record EvidenceEditorSupport(
    IFilePicker FilePicker,
    Func<CancellationToken, Task<EvidenceLibraryRow?>> PickCitationAsync,
    Func<CancellationToken, Task<DeclaredFigureInput?>> PickDeclaredFigureAsync,
    Func<CancellationToken, Task<Guid?>> PickSubjectAsync,
    Func<CancellationToken, Task<CheckEntryInput?>> PickCheckAsync,
    Func<CancellationToken, Task<IssueEntryInput?>> PickIssueAsync);

/// <summary>
/// The project Commercial section's own pickers (`WP 19.0A`, `ADR-0150`):
/// <see cref="PickClientOrganisationIdAsync"/> returns the chosen
/// organisation's own record id, an empty string for "Clear", or
/// <see langword="null"/> for cancelled — <see cref="OrganisationPicker.PickAsync"/>'s
/// own exact three-way shape; <see cref="PickRateCardIdAsync"/> returns the
/// chosen Released card's own record id, or <see langword="null"/> for
/// cancelled — <see cref="RateCardPicker.PickAsync"/>'s own shape.
/// <see cref="CurrentPrincipalIdentityId"/> is the identity id the Project
/// Manager field's own "Use Me" affordance offers by default, since there
/// is no principal enumeration (`WP 19.0A` brief §3). <see langword="null"/>
/// (any test that constructs this editor directly without it) leaves the
/// Commercial section's own Change/Pin/Use Me affordances honestly
/// unavailable rather than run without asking — the identical discipline
/// <see cref="ObjectEditorView.WorkspaceChanges"/> already established.
/// </summary>
public sealed record ProjectCommercialEditorSupport(
    Func<CancellationToken, Task<string?>> PickClientOrganisationIdAsync,
    Func<CancellationToken, Task<string?>> PickRateCardIdAsync,
    Func<string?> CurrentPrincipalIdentityId);

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
/// resolves the real object too (`WP 10.8A`) for every Kind except a
/// Requirement (`TD-41`). This class holds the real object directly, so it
/// can call the real method — informational only, never blocking Save (see class remarks
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
    private readonly IKindEditorDeclarationRegistry? _declarations;
    private readonly EvidenceEditorSupport? _evidenceSupport;
    private readonly IAuditQuery? _auditQuery;
    private readonly ProjectCommercialEditorSupport? _commercialSupport;

    private readonly TextBlock _identityReadout = new() { Opacity = 0.7, FontSize = DesignTokens.FontSizeCaption };
    private readonly TextBox _nameBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _contentBox = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 140, FontSize = DesignTokens.FontSizeBody };
    private readonly ToggleButton _readOnlyToggle = new() { Content = "Editable", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _saveButton = new() { Content = "Save", MinHeight = DesignTokens.MinControlSize };
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
    private readonly Button _bomSaveButton = new() { Content = "Save BOM Line", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _bomStatusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _contentSection = null!;
    private Expander _bomSection = null!;

    private readonly TextBox _requirementOwnerBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly ComboBox _requirementPriorityBox = new() { MinHeight = DesignTokens.MinControlSize, ItemsSource = new[] { "(none)", "Low", "Medium", "High", "Critical" } };
    private readonly Button _requirementSaveButton = new() { Content = "Save Owner/Priority", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _requirementStatusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _requirementSection = null!;

    private readonly ComboBox _calculationTemplatePicker = new() { MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _calculationInputJsonBox = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80, FontSize = DesignTokens.FontSizeBody, Watermark = "{ ... }" };
    private readonly Button _calculationExecuteButton = new() { Content = "Execute", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _calculationStatusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _calculationSection = null!;
    private Expander _calculationPointerSection = null!;

    /// <summary>What the editor says on a Calculation instead of offering a JSON box (`WP 17.9.1`).</summary>
    public const string CalculationPointerGuidance =
        "Calculations are run, named and traced in the Engineering Calculations workspace — open it from the rail on the left. "
        + "This editor holds the calculation's identity, lifecycle and attachments.";
    private IReadOnlyList<CalculationTemplateDescriptor> _availableTemplates = [];
    private bool _calculationHasBeenExecuted;

    private readonly Button _verificationPassButton = new() { Content = "Pass", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _verificationFailButton = new() { Content = "Fail", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _verificationConditionalButton = new() { Content = "Conditional", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _verificationMethodBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _verificationStatusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _verificationResultSection = null!;

    private readonly StackPanel _attachmentsListPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly TextBox _attachmentFileNameBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _attachmentContentTypeBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _attachmentSizeBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _attachmentAddButton = new() { Content = "Attach", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _addFileViaPickerButton = new() { Content = "Add File…", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly TextBlock _attachmentStatusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _attachmentsSection = null!;

    // `WP 18.2A` — declaration-per-Kind: the Description (read-only
    // mechanical metadata) and Where-used sections Part/Assembly/Component
    // declare (`TD-174`, `TD-175`).
    private readonly StackPanel _descriptionPanel = new() { Spacing = DesignTokens.SpaceXs };
    private Expander _descriptionSection = null!;
    private readonly StackPanel _whereUsedPanel = new() { Spacing = DesignTokens.SpaceXs };
    private Expander _whereUsedSection = null!;
    private Expander _lifecycleSection = null!;

    // `WP 18.2A` — Evidence's own declared sections (`ADR-0148`, §4).
    private readonly StackPanel _evidenceSubjectPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly Button _changeSubjectButton = new() { Content = "Change Subject", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly TextBlock _evidenceSubjectStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _evidenceSubjectSection = null!;

    private readonly StackPanel _evidenceCitationsPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly Button _citeButton = new() { Content = "Cite", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly TextBlock _evidenceCitationsStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _evidenceCitationsSection = null!;

    private readonly StackPanel _evidenceFiguresPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly Button _declareFigureButton = new() { Content = "Declare Figure", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly TextBlock _evidenceFiguresStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _evidenceFiguresSection = null!;

    // Status + Check + Issue (`WP 18.2B` builds the Check, Issue and
    // Revise actions themselves) — Evidence's own specialised replacement
    // for the generic Lifecycle section, suppressed for this Kind so a
    // reader is never shown the eight-value canonical vocabulary this
    // Kind's own four-value one specialises (`Evidence.Status`'s own
    // remarks). Each action button is visible only when
    // `EvidenceStatusTransitions` actually permits it from the record's
    // own current status (`Check` from Draft, `Issue` from Checked,
    // `Revise` from Issued) — never a disabled button offering a move the
    // service would refuse anyway.
    private readonly StackPanel _evidenceLifecyclePanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly Button _checkButton = new() { Content = "Check", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Button _issueButton = new() { Content = "Issue", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Button _reviseButton = new() { Content = "Revise", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly TextBlock _evidenceLifecycleStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _evidenceLifecycleSection = null!;

    private readonly StackPanel _evidenceAuditPanel = new() { Spacing = DesignTokens.SpaceXs };
    private Expander _evidenceAuditSection = null!;

    // `WP 19.0A` (`ADR-0150`) — the project Commercial section: client and
    // rate card each with a picker and a Change/Pin action (mirrors
    // Evidence's own Subject "Change" affordance), purchase order
    // reference/budget/dates/project manager each a plain field with its
    // own Save action, dispatching the six already-registered
    // `project.*` commands directly.
    private readonly StackPanel _commercialClientPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly Button _changeClientButton = new() { Content = "Change Client", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly TextBlock _commercialClientStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    private readonly TextBox _commercialPurchaseOrderBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _commercialPurchaseOrderSaveButton = new() { Content = "Save Purchase Order", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _commercialPurchaseOrderStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    private readonly TextBox _commercialBudgetBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize, Watermark = "amount currency" };
    private readonly Button _commercialBudgetSaveButton = new() { Content = "Save Budget", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _commercialBudgetStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    private readonly StackPanel _commercialRateCardPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly Button _changeRateCardButton = new() { Content = "Pin Rate Card", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly TextBlock _commercialRateCardStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    private readonly DatePicker _commercialStartDate = new() { MinHeight = DesignTokens.MinControlSize };
    private readonly DatePicker _commercialTargetDate = new() { MinHeight = DesignTokens.MinControlSize };
    private readonly Button _commercialDatesSaveButton = new() { Content = "Save Dates", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _commercialDatesStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    private readonly TextBox _commercialProjectManagerBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _commercialUseMeButton = new() { Content = "Use Me", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Button _commercialProjectManagerSaveButton = new() { Content = "Save Project Manager", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _commercialProjectManagerStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    private Expander _commercialSection = null!;
    private readonly StackPanel _invoiceLinesPanel = new() { Spacing = DesignTokens.SpaceXs };
    private Expander _invoiceLinesSection = null!;
    private readonly StackPanel _invoiceExternalPanel = new() { Spacing = DesignTokens.SpaceXs };
    private Expander _invoiceExternalSection = null!;

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

    private IWorkspaceChanges? _workspaceChanges;

    /// <summary>
    /// The change feed this editor reloads from (`WP 18.1A`) — set once by
    /// <see cref="TryCreate"/>. <see langword="null"/> (the default) is a
    /// legitimate, silent no-op — an editor built directly by a test that
    /// never threads this through behaves exactly as every prior Work
    /// Package's editor did: it refreshes only after its own Save.
    /// Reloads from source only when the commit's own touched set includes
    /// the object this editor is showing, mirroring
    /// <see cref="Tempest.Desktop.Views.PropertyInspectorView.WorkspaceChanges"/>'s
    /// own identical filter. Unsubscribes itself once this control leaves
    /// the visual tree (its own tab closed), so a session that opens and
    /// closes many editors over its lifetime does not keep every one of
    /// them alive through this subscription alone.
    /// </summary>
    public IWorkspaceChanges? WorkspaceChanges
    {
        get => _workspaceChanges;
        set
        {
            if (ReferenceEquals(_workspaceChanges, value))
                return;

            if (_workspaceChanges is not null)
                _workspaceChanges.Changed -= OnWorkspaceChanged;

            _workspaceChanges = value;

            if (_workspaceChanges is not null)
                _workspaceChanges.Changed += OnWorkspaceChanged;
        }
    }

    private void OnWorkspaceChanged(WorkspaceChange change)
    {
        if (!change.Entries.Any(e => e.ObjectId == _objectId))
            return;

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await RefreshAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ActionCompleted?.Invoke($"Refresh failed: {ex.Message}", ActionOutcome.Failed);
            }
        });
    }

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
    /// audit: population used to complete before <see cref="TryCreate"/>
    /// returned, so the shell could not possibly have subscribed by the
    /// time the attachment rows were first built (`WP 18.1A` moved
    /// population to run in the background instead, for an unrelated
    /// reason — see <see cref="TryCreate"/>'s own remarks — which makes
    /// this accessor's own re-population fallback reachable slightly less
    /// often but no less necessary: nothing here guarantees which finishes
    /// first) — and the rows only carry an Open button when something can
    /// handle it. The button therefore never
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
                _ = PopulateAttachmentsSafelyAsync(_populatedTarget);
        }

        remove => _openAttachmentRequested -= value;
    }

    private ObjectEditorView(
        Guid objectId, string objectKind, EngineeringDomainContext domainContext, IWorkspaceManager manager, Action<Guid, string> navigateToObject,
        ICommandDispatcher commandDispatcher, IRequirementsService? requirementsService, CalculationTemplateRegistry? calculationTemplates,
        IKindEditorDeclarationRegistry? declarations, EvidenceEditorSupport? evidenceSupport, IAuditQuery? auditQuery,
        ProjectCommercialEditorSupport? commercialSupport)
    {
        _objectId = objectId;
        _objectKind = objectKind;
        _domainContext = domainContext;
        _manager = manager;
        _navigateToObject = navigateToObject;
        _commandDispatcher = commandDispatcher;
        _requirementsService = requirementsService;
        _calculationTemplates = calculationTemplates;
        _declarations = declarations;
        _evidenceSupport = evidenceSupport;
        _auditQuery = auditQuery;
        _commercialSupport = commercialSupport;

        // `WP 18.1A`: once this tab closes and the control leaves the
        // visual tree, drop the change-feed subscription — see
        // WorkspaceChanges's own remarks.
        this.DetachedFromVisualTree += (_, _) => WorkspaceChanges = null;

        Content = BuildLayout();

        // Chrome treatments (`WP-Z4` Productisation Phase 1) — this editor
        // predates ChromeStyles/IconGeometry (`WP 10.0A`) and had never
        // been brought forward; every button now carries the same three
        // treatments the rest of the shell uses, rather than the
        // unstyled default FluentTheme button it rendered as before.
        _readOnlyToggle.Classes.Add(ChromeStyles.Subtle);
        _saveButton.Classes.Add(ChromeStyles.Primary);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);
        _bomSaveButton.Classes.Add(ChromeStyles.Primary);
        _requirementSaveButton.Classes.Add(ChromeStyles.Primary);
        _calculationExecuteButton.Classes.Add(ChromeStyles.Primary);
        _verificationPassButton.Classes.Add(ChromeStyles.Primary);
        _verificationFailButton.Classes.Add(ChromeStyles.Danger);
        _verificationConditionalButton.Classes.Add(ChromeStyles.Subtle);
        _attachmentAddButton.Classes.Add(ChromeStyles.Primary);
        _addFileViaPickerButton.Classes.Add(ChromeStyles.Primary);
        _citeButton.Classes.Add(ChromeStyles.Primary);
        _declareFigureButton.Classes.Add(ChromeStyles.Primary);
        _changeSubjectButton.Classes.Add(ChromeStyles.Subtle);
        _checkButton.Classes.Add(ChromeStyles.Primary);
        _issueButton.Classes.Add(ChromeStyles.Primary);
        _reviseButton.Classes.Add(ChromeStyles.Subtle);

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
        _addFileViaPickerButton.Click += async (_, _) => await OnAddFileViaPickerAsync().ConfigureAwait(true);
        _citeButton.Click += async (_, _) => await OnCiteAsync().ConfigureAwait(true);
        _declareFigureButton.Click += async (_, _) => await OnDeclareFigureAsync().ConfigureAwait(true);
        _changeSubjectButton.Click += async (_, _) => await OnChangeSubjectAsync().ConfigureAwait(true);
        _checkButton.Click += async (_, _) => await OnCheckAsync().ConfigureAwait(true);
        _issueButton.Click += async (_, _) => await OnIssueAsync().ConfigureAwait(true);
        _reviseButton.Click += async (_, _) => await OnReviseAsync().ConfigureAwait(true);

        // `WP 19.0A` (`ADR-0150`) — the project Commercial section.
        _changeClientButton.Click += async (_, _) => await OnChangeClientAsync().ConfigureAwait(true);
        _changeRateCardButton.Click += async (_, _) => await OnChangeRateCardAsync().ConfigureAwait(true);
        _commercialPurchaseOrderSaveButton.Click += async (_, _) => await OnSaveCommercialPurchaseOrderAsync().ConfigureAwait(true);
        _commercialBudgetSaveButton.Click += async (_, _) => await OnSaveCommercialBudgetAsync().ConfigureAwait(true);
        _commercialDatesSaveButton.Click += async (_, _) => await OnSaveCommercialDatesAsync().ConfigureAwait(true);
        _commercialUseMeButton.Click += (_, _) => _commercialProjectManagerBox.Text = _commercialSupport?.CurrentPrincipalIdentityId() ?? _commercialProjectManagerBox.Text;
        _commercialProjectManagerSaveButton.Click += async (_, _) => await OnSaveCommercialProjectManagerAsync().ConfigureAwait(true);
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
    /// <remarks>
    /// <para>
    /// <b>`WP 18.1A`'s one disclosed exception to removing blocking calls.</b>
    /// This method's own signature is fixed by <see cref="DocumentAreaView"/>'s
    /// synchronous <c>Func&lt;IWorkspaceView, Control&gt;</c> content-builder
    /// contract: <see cref="Composition.WorkspaceViewCoordinator.BuildDocumentContent"/>
    /// calls this and must return a <see cref="Control"/> immediately,
    /// with no <see langword="await"/> available anywhere on that path.
    /// Making this method itself asynchronous would mean making
    /// <c>DocumentAreaView.ShowTab</c> asynchronous, which cascades to
    /// every one of its own callers —
    /// <c>Composition/QuickAccessToolbarFactory.cs</c> and
    /// <c>MainWindow.cs</c> among them — none of it owned by this Work
    /// Package, and none of it a one-file, forced-by-a-signature-change
    /// ripple the way <c>DigitalThreadGraphView.cs</c>'s was. The existence
    /// check below is the one <c>GetAwaiter().GetResult()</c> this file
    /// still carries; the structural guard names it explicitly. Population
    /// itself no longer blocks this call: the constructed editor is
    /// returned empty and fills in from <see cref="PopulateFromAsync"/>,
    /// fire-and-forget, typically before the tab is even visible since
    /// every read it awaits resolves against the in-memory object cache.
    /// </para>
    /// </remarks>
    public static ObjectEditorView? TryCreate(
        Guid objectId, string objectKind, EngineeringDomainContext domainContext, IWorkspaceManager manager, Action<Guid, string> navigateToObject,
        ICommandDispatcher commandDispatcher, IRequirementsService? requirementsService = null, CalculationTemplateRegistry? calculationTemplates = null,
        IWorkspaceChanges? workspaceChanges = null, IKindEditorDeclarationRegistry? declarations = null,
        EvidenceEditorSupport? evidenceSupport = null, IAuditQuery? auditQuery = null, ProjectCommercialEditorSupport? commercialSupport = null)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(navigateToObject);
        ArgumentNullException.ThrowIfNull(commandDispatcher);

        // The one disclosed exception — see this method's own remarks.
        var target = domainContext.Repository.FindAsync(objectId).GetAwaiter().GetResult();
        if (target is null)
            return null;

        var editor = new ObjectEditorView(
            objectId, objectKind, domainContext, manager, navigateToObject, commandDispatcher, requirementsService, calculationTemplates,
            declarations, evidenceSupport, auditQuery, commercialSupport)
        {
            WorkspaceChanges = workspaceChanges,
        };
        editor.PopulateInBackground(target);
        return editor;
    }

    /// <summary>
    /// Runs <see cref="PopulateFromAsync"/> fire-and-forget, from a
    /// synchronous caller that cannot await it (`WP 18.1A`) — a failure is
    /// reported through <see cref="ActionCompleted"/> rather than thrown
    /// into the void.
    /// </summary>
    private void PopulateInBackground(IEngineeringObject target)
    {
        _ = RunAsync();

        async Task RunAsync()
        {
            try
            {
                await PopulateFromAsync(target).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ActionCompleted?.Invoke($"Failed to load: {ex.Message}", ActionOutcome.Failed);
            }
        }
    }

    /// <summary>Re-reads the real object and refreshes every section — never a cached copy, mirroring <see cref="IWorkspaceView.RefreshAsync"/>'s own identical discipline.</summary>
    public async Task RefreshAsync()
    {
        var target = await _domainContext.Repository.FindAsync(_objectId).ConfigureAwait(true);
        if (target is not null)
            await PopulateFromAsync(target).ConfigureAwait(true);
    }

    private Control BuildLayout()
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto") };
        var titleStack = new StackPanel();
        var titleText = new TextBlock
        {
            Text = $"{IconRegistry.Resolve(_objectKind)} {_objectKind}",
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeTitle,
            FontWeight = DesignTokens.WeightHeading,
        };
        ThemeReactiveBrush.Bind(titleText, TextBlock.ForegroundProperty, BrandPalette.HeadingTextBrushKey);
        titleStack.Children.Add(titleText);
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
        _contentSection = BuildSection("Content", _contentBox);
        _lifecycleSection = BuildSection("Lifecycle", _lifecyclePanel);
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

        _calculationPointerSection = BuildSection("Calculation", new StackPanel
        {
            Spacing = DesignTokens.SpaceXs,
            Children = { new TextBlock { Text = CalculationPointerGuidance, TextWrapping = TextWrapping.Wrap, Opacity = 0.85, FontSize = DesignTokens.FontSizeBody } },
        });
        _calculationPointerSection.IsVisible = false;

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
        attachmentsPanel.Children.Add(_addFileViaPickerButton);
        attachmentsPanel.Children.Add(_attachmentStatusMessage);
        _attachmentsSection = BuildSection("Attachments", attachmentsPanel);
        _attachmentsSection.IsVisible = false;

        // `WP 18.2A` — declaration-per-Kind (Part/Assembly/Component): a
        // read-only mechanical-metadata block and the "Where used" facet,
        // named and linked (`TD-174`, `TD-175`).
        _descriptionSection = BuildSection("Description", _descriptionPanel);
        _descriptionSection.IsVisible = false;
        _whereUsedSection = BuildSection("Where used", _whereUsedPanel);
        _whereUsedSection.IsVisible = false;

        // `WP 18.2A` — Evidence's own declared sections (`ADR-0148`, §4).
        var subjectBody = new StackPanel { Spacing = DesignTokens.SpaceXs };
        subjectBody.Children.Add(_evidenceSubjectPanel);
        subjectBody.Children.Add(_changeSubjectButton);
        subjectBody.Children.Add(_evidenceSubjectStatus);
        _evidenceSubjectSection = BuildSection("Subject", subjectBody);
        _evidenceSubjectSection.IsVisible = false;

        var citationsBody = new StackPanel { Spacing = DesignTokens.SpaceXs };
        citationsBody.Children.Add(_citeButton);
        citationsBody.Children.Add(_evidenceCitationsPanel);
        citationsBody.Children.Add(_evidenceCitationsStatus);
        _evidenceCitationsSection = BuildSection("Citations", citationsBody);
        _evidenceCitationsSection.IsVisible = false;

        var figuresBody = new StackPanel { Spacing = DesignTokens.SpaceXs };
        figuresBody.Children.Add(_declareFigureButton);
        figuresBody.Children.Add(_evidenceFiguresPanel);
        figuresBody.Children.Add(_evidenceFiguresStatus);
        _evidenceFiguresSection = BuildSection("Declared figures", figuresBody);
        _evidenceFiguresSection.IsVisible = false;

        // `WP 18.2B`, §1/§2/§3: the Check/Issue/Revise actions themselves,
        // each visible only when the record's own current status permits
        // it (`PopulateEvidenceSectionsAsync`'s own gate).
        var lifecycleActions = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        lifecycleActions.Children.Add(_checkButton);
        lifecycleActions.Children.Add(_issueButton);
        lifecycleActions.Children.Add(_reviseButton);
        var lifecycleBody = new StackPanel { Spacing = DesignTokens.SpaceXs };
        lifecycleBody.Children.Add(_evidenceLifecyclePanel);
        lifecycleBody.Children.Add(lifecycleActions);
        lifecycleBody.Children.Add(_evidenceLifecycleStatus);
        _evidenceLifecycleSection = BuildSection("Lifecycle", lifecycleBody);
        _evidenceLifecycleSection.IsVisible = false;

        _evidenceAuditSection = BuildSection("Audit", _evidenceAuditPanel);
        _evidenceAuditSection.IsVisible = false;

        // `WP 19.0A` (`ADR-0150`) — the project Commercial section: client
        // and rate card each read-only with a Change/Pin action; purchase
        // order reference, budget, dates and project manager each a plain
        // field with its own Save action.
        var commercialBody = new StackPanel { Spacing = DesignTokens.SpaceMd };
        var clientGroup = new StackPanel { Spacing = DesignTokens.SpaceXs };
        clientGroup.Children.Add(new TextBlock { Text = "Client", Opacity = 0.8, FontSize = DesignTokens.FontSizeBody });
        clientGroup.Children.Add(_commercialClientPanel);
        clientGroup.Children.Add(_changeClientButton);
        clientGroup.Children.Add(_commercialClientStatus);
        commercialBody.Children.Add(clientGroup);

        var poGroup = new StackPanel { Spacing = DesignTokens.SpaceXs };
        poGroup.Children.Add(LabeledRow("Purchase Order Reference", _commercialPurchaseOrderBox));
        poGroup.Children.Add(_commercialPurchaseOrderSaveButton);
        poGroup.Children.Add(_commercialPurchaseOrderStatus);
        commercialBody.Children.Add(poGroup);

        var budgetGroup = new StackPanel { Spacing = DesignTokens.SpaceXs };
        budgetGroup.Children.Add(LabeledRow("Budget", _commercialBudgetBox));
        budgetGroup.Children.Add(_commercialBudgetSaveButton);
        budgetGroup.Children.Add(_commercialBudgetStatus);
        commercialBody.Children.Add(budgetGroup);

        var rateCardGroup = new StackPanel { Spacing = DesignTokens.SpaceXs };
        rateCardGroup.Children.Add(new TextBlock { Text = "Rate Card", Opacity = 0.8, FontSize = DesignTokens.FontSizeBody });
        rateCardGroup.Children.Add(_commercialRateCardPanel);
        rateCardGroup.Children.Add(_changeRateCardButton);
        rateCardGroup.Children.Add(_commercialRateCardStatus);
        commercialBody.Children.Add(rateCardGroup);

        var datesGroup = new StackPanel { Spacing = DesignTokens.SpaceXs };
        datesGroup.Children.Add(LabeledRow("Start Date", _commercialStartDate));
        datesGroup.Children.Add(LabeledRow("Target Date", _commercialTargetDate));
        datesGroup.Children.Add(_commercialDatesSaveButton);
        datesGroup.Children.Add(_commercialDatesStatus);
        commercialBody.Children.Add(datesGroup);

        var pmGroup = new StackPanel { Spacing = DesignTokens.SpaceXs };
        pmGroup.Children.Add(LabeledRow("Project Manager", _commercialProjectManagerBox));
        var pmButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceXs };
        pmButtons.Children.Add(_commercialUseMeButton);
        pmButtons.Children.Add(_commercialProjectManagerSaveButton);
        pmGroup.Children.Add(pmButtons);
        pmGroup.Children.Add(_commercialProjectManagerStatus);
        commercialBody.Children.Add(pmGroup);

        _commercialSection = BuildSection("Commercial", commercialBody);
        _commercialSection.IsVisible = false;

        // `WP 19.1A`: an invoice request's lines and its connector fields are
        // read-only projections of the record; the actions live on the
        // Invoicing area, not here.
        _invoiceLinesSection = BuildSection("Lines", _invoiceLinesPanel);
        _invoiceLinesSection.IsVisible = false;
        _invoiceExternalSection = BuildSection("Connector", _invoiceExternalPanel);
        _invoiceExternalSection.IsVisible = false;

        var body = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        body.Children.Add(header);
        body.Children.Add(_statusMessage);
        body.Children.Add(new Separator());
        body.Children.Add(identitySection);
        body.Children.Add(_descriptionSection);
        body.Children.Add(_commercialSection);
        body.Children.Add(_invoiceLinesSection);
        body.Children.Add(_contentSection);
        body.Children.Add(_evidenceSubjectSection);
        body.Children.Add(_bomSection);
        body.Children.Add(_requirementSection);
        body.Children.Add(_calculationSection);
        body.Children.Add(_calculationPointerSection);
        body.Children.Add(_verificationResultSection);
        body.Children.Add(_evidenceCitationsSection);
        body.Children.Add(_evidenceFiguresSection);
        body.Children.Add(_attachmentsSection);
        body.Children.Add(_whereUsedSection);
        body.Children.Add(_lifecycleSection);
        body.Children.Add(_invoiceExternalSection);
        body.Children.Add(_evidenceLifecycleSection);
        body.Children.Add(relationshipsSection);
        body.Children.Add(validationSection);
        body.Children.Add(_evidenceAuditSection);

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

    private async Task PopulateFromAsync(IEngineeringObject target)
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

        // `WP 17.9.3`: a Kind that cannot be revised and has no content shows
        // no Content box at all, rather than a disabled empty one (the design-freeze
        // surface audit found this on RequirementGroup and RequirementCollection).
        _contentSection.IsVisible = _manager.CanRevise(_objectKind) || !string.IsNullOrEmpty(_originalContent);

        PopulateBom(target);
        await PopulateRequirementAsync(target).ConfigureAwait(true);
        await PopulateCalculationExecutionAsync(target).ConfigureAwait(true);
        PopulateVerificationResult(target);
        await PopulateAttachmentsAsync(target).ConfigureAwait(true);
        PopulateDescription(target);
        await PopulateWhereUsedAsync(target).ConfigureAwait(true);
        PopulateCommercial(target);
        PopulateInvoiceRequest(target);

        PopulateLifecycle(target);
        await PopulateRelationshipsAsync(target).ConfigureAwait(true);
        await PopulateValidationAsync(target).ConfigureAwait(true);

        // `WP 18.2A`: Evidence renders from its own declaration — Subject,
        // Citations, Declared figures, its own Status/Check/Issue
        // (replacing the generic Lifecycle section, which speaks the
        // wrong vocabulary for this Kind), and Audit.
        if (target is Core.Evidence.Evidence evidence)
        {
            _lifecycleSection.IsVisible = false;
            await PopulateEvidenceSectionsAsync(evidence).ConfigureAwait(true);
        }
        else
        {
            _evidenceSubjectSection.IsVisible = false;
            _evidenceCitationsSection.IsVisible = false;
            _evidenceFiguresSection.IsVisible = false;
            _evidenceLifecycleSection.IsVisible = false;
            _evidenceAuditSection.IsVisible = false;
        }

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
    private async Task PopulateRelationshipsAsync(IEngineeringObject target)
    {
        _relationshipsPanel.Children.Clear();

        if (target is IHasRelationships hasRelationships)
        {
            var outgoing = await hasRelationships.GetRelationshipsAsync().ConfigureAwait(true);
            foreach (var relationship in outgoing)
                _relationshipsPanel.Children.Add(await BuildRelationshipRowAsync(relationship.TargetId, relationship.RelationshipKind, "→").ConfigureAwait(true));
        }

        var incoming = await _domainContext.RelationshipRepository.GetIncomingAsync(_objectId).ConfigureAwait(true);
        foreach (var relationship in incoming)
            _relationshipsPanel.Children.Add(await BuildRelationshipRowAsync(relationship.SourceId, relationship.RelationshipKind, "←").ConfigureAwait(true));

        if (_relationshipsPanel.Children.Count == 0)
            _relationshipsPanel.Children.Add(new TextBlock { Text = "No relationships recorded.", Opacity = 0.7 });
    }

    /// <summary>Builds one relationship row — "Navigation between related objects" (`WP 10.3A`), reusing <see cref="INavigationService.OpenAsync"/> via the injected navigate callback, never a new navigation mechanism.</summary>
    private async Task<Control> BuildRelationshipRowAsync(Guid otherId, string relationshipKind, string direction)
    {
        var other = await _domainContext.Repository.FindAsync(otherId).ConfigureAwait(true);
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
            var openContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceXs };
            openContent.Children.Add(new TextBlock { Text = "Open", FontSize = DesignTokens.FontSizeCaption, VerticalAlignment = VerticalAlignment.Center });
            openContent.Children.Add(IconGeometry.Build(IconGeometry.ChevronRight, 11));
            var openButton = new Button { Content = openContent, Padding = new Avalonia.Thickness(DesignTokens.SpaceSm, DesignTokens.SpaceXs) };
            openButton.Classes.Add(ChromeStyles.Flat);
            Avalonia.Automation.AutomationProperties.SetName(openButton, "Open");
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
    private async Task PopulateValidationAsync(IEngineeringObject target)
    {
        _validationPanel.Children.Clear();

        if (target is not IValidatable validatable)
        {
            _validationPanel.Children.Add(new TextBlock { Text = "This object supports no validation.", Opacity = 0.7 });
            return;
        }

        var result = await validatable.ValidateAsync().ConfigureAwait(true);

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
    /// <summary>
    /// The Kinds a Bill-of-Materials line means something for. Every canonical
    /// object implements <see cref="IHasBomLine"/> (ADR-0075's facet plumbing),
    /// which is why the first Windows review of `v0.17.0` saw Quantity, Find
    /// Number and Reference Designator on a Project and on a Calculation.
    /// The editor shows the section only where a person would expect it
    /// (`WP 17.9.1`); the facet itself is untouched.
    /// </summary>
    internal static readonly HashSet<string> BomKinds = new(StringComparer.Ordinal)
    {
        MechanicalObjectFactoryRegistry.Assembly, MechanicalObjectFactoryRegistry.SubAssembly, MechanicalObjectFactoryRegistry.Part, MechanicalObjectFactoryRegistry.Component, MechanicalObjectFactoryRegistry.Configuration,
    };

    private void PopulateBom(IEngineeringObject target)
    {
        // `WP 18.2A`: where a Kind has a real declaration, the declaration
        // decides — a Part shows no BOM input at all (`TD-175`), even
        // though it still structurally implements `IHasBomLine`. A Kind
        // with no declaration keeps today's own <see cref="BomKinds"/> gate,
        // unchanged.
        var declaration = _declarations?.For(_objectKind);
        var showBom = declaration is not null
            ? declaration.HasSection(EditorSectionKeys.BillOfMaterials)
            : _objectKind is not null && BomKinds.Contains(_objectKind);

        if (target is not IHasBomLine bomLine || !showBom)
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
            await RefreshAsync().ConfigureAwait(true);
        _bomStatusMessage.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(result.Succeeded));
    }

    /// <summary>
    /// The Description section (`WP 18.2A`, declaration-per-Kind) — the
    /// mechanical fields the model has today (<see cref="IHasMetadata"/>),
    /// read-only: this section shows what the Property Inspector already
    /// reads, consolidated into the editor, never a new writable field or
    /// a new command (`D-028`: no attribute is added that no calc sheet,
    /// drawing or the invoice seam needs).
    /// </summary>
    private void PopulateDescription(IEngineeringObject target)
    {
        var declaration = _declarations?.For(_objectKind);

        if (declaration is null || !declaration.HasSection(EditorSectionKeys.Description) || target is not IHasMetadata metadata)
        {
            _descriptionSection.IsVisible = false;
            return;
        }

        _descriptionSection.IsVisible = true;
        _descriptionPanel.Children.Clear();
        _descriptionPanel.Children.Add(DescriptionRow("Owner", metadata.Owner));
        _descriptionPanel.Children.Add(DescriptionRow("Discipline", metadata.Discipline));
        _descriptionPanel.Children.Add(DescriptionRow("Classification", metadata.Classification));
        _descriptionPanel.Children.Add(DescriptionRow("Tags", metadata.Tags.Count > 0 ? string.Join(", ", metadata.Tags) : null));
        _descriptionPanel.Children.Add(DescriptionRow("Notes", metadata.Notes));
    }

    private static Control DescriptionRow(string label, string? value) =>
        LabeledRow(label, new TextBlock { Text = value ?? "(none)", Opacity = value is null ? 0.5 : 1.0, TextWrapping = TextWrapping.Wrap, FontSize = DesignTokens.FontSizeBody });

    /// <summary>
    /// The <em>Where used</em> section (`WP 18.2A`, `TD-174`, `TD-175`) —
    /// read-only, named, linked: the assembly this object sits in, via
    /// <see cref="IHasParent.ParentId"/>, exactly as
    /// <see cref="MechanicalPropertyFacetProvider"/>'s own identical
    /// "Where Used" facet already resolves it for the Property Inspector.
    /// </summary>
    private async Task PopulateWhereUsedAsync(IEngineeringObject target)
    {
        var declaration = _declarations?.For(_objectKind);

        if (declaration is null || !declaration.HasSection(EditorSectionKeys.WhereUsed) || target is not IHasParent hasParent)
        {
            _whereUsedSection.IsVisible = false;
            return;
        }

        _whereUsedSection.IsVisible = true;
        _whereUsedPanel.Children.Clear();

        if (hasParent.ParentId is not { } parentId)
        {
            _whereUsedPanel.Children.Add(new TextBlock { Text = "(top level — not used within any assembly)", Opacity = 0.7 });
            return;
        }

        _whereUsedPanel.Children.Add(await BuildObjectReferenceRowAsync(parentId).ConfigureAwait(true));
    }

    /// <summary>Builds one read-only, named, linked row for an object referenced by id — shared by <em>Where used</em> and Evidence's own <em>Subject</em>.</summary>
    private async Task<Control> BuildObjectReferenceRowAsync(Guid referencedId)
    {
        var referenced = await _domainContext.Repository.FindAsync(referencedId).ConfigureAwait(true);
        var name = (referenced as IHasBusinessIdentifier)?.DisplayName ?? referencedId.ToString();
        var kind = referenced?.Kind ?? _objectKind;

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, DesignTokens.SpaceXs) };
        var icon = new TextBlock { Text = IconRegistry.Resolve(kind), Margin = new Thickness(0, 0, DesignTokens.SpaceSm, 0) };
        var text = new TextBlock { Text = $"{name} ({kind})", TextWrapping = TextWrapping.Wrap, FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(icon, 0);
        Grid.SetColumn(text, 1);
        row.Children.Add(icon);
        row.Children.Add(text);

        if (referenced is not null)
        {
            var openButton = new Button { Content = "Open", Padding = new Thickness(10, 1), FontSize = DesignTokens.FontSizeBody };
            openButton.Classes.Add(ChromeStyles.Flat);
            Avalonia.Automation.AutomationProperties.SetName(openButton, $"Open {name}");
            openButton.Click += (_, _) => _navigateToObject(referencedId, kind);
            Grid.SetColumn(openButton, 2);
            row.Children.Add(openButton);
        }

        return row;
    }

    /// <summary>
    /// The project Commercial section (`WP 19.0A`, `ADR-0150`): client and
    /// rate card, each read-only with a Change/Pin action opening a real
    /// picker; purchase order reference, budget, dates and project manager,
    /// each editable and dispatching its own already-registered
    /// <c>project.*</c> command directly.
    /// </summary>
    private void PopulateInvoiceRequest(IEngineeringObject target)
    {
        var declaration = _declarations?.For(_objectKind);

        if (declaration is null || target is not Core.Invoicing.InvoiceRequest request)
        {
            _invoiceLinesSection.IsVisible = false;
            _invoiceExternalSection.IsVisible = false;
            return;
        }

        _invoiceLinesSection.IsVisible = declaration.HasSection(Tempest.Workspace.Editors.EditorSectionKeys.InvoiceLines);
        _invoiceLinesPanel.Children.Clear();
        if (request.Lines.Count == 0)
        {
            _invoiceLinesPanel.Children.Add(new TextBlock { Text = "(no lines)", Opacity = 0.5, FontSize = DesignTokens.FontSizeBody });
        }

        foreach (var line in request.Lines)
        {
            _invoiceLinesPanel.Children.Add(new TextBlock
            {
                Text = $"{line.Description}  •  {line.Quantity:0.##} × {line.UnitRate}  =  {line.Amount}",
                TextWrapping = TextWrapping.Wrap,
                FontSize = DesignTokens.FontSizeBody,
            });
        }

        _invoiceLinesPanel.Children.Add(new TextBlock
        {
            Text = $"Total {request.Total}",
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeBody,
            Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0),
        });

        _invoiceExternalSection.IsVisible = declaration.HasSection(Tempest.Workspace.Editors.EditorSectionKeys.InvoicingExternal);
        _invoiceExternalPanel.Children.Clear();
        foreach (var (label, value) in new[]
        {
            ("Connector", request.Connector),
            ("External Id", request.ExternalId),
            ("External Invoice Number", request.ExternalInvoiceNumber),
            ("External Status", request.ExternalStatus),
            ("Issued Date", request.IssuedDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
            ("Paid Date", request.PaidDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
            ("Last Error", request.LastError),
        })
        {
            _invoiceExternalPanel.Children.Add(new TextBlock
            {
                Text = $"{label}: {value ?? "—"}",
                Opacity = value is null ? 0.6 : 1.0,
                TextWrapping = TextWrapping.Wrap,
                FontSize = DesignTokens.FontSizeBody,
            });
        }
    }

    private void PopulateCommercial(IEngineeringObject target)
    {
        var declaration = _declarations?.For(_objectKind);

        if (declaration is null || !declaration.HasSection(EditorSectionKeys.Commercial) || target is not IProject project)
        {
            _commercialSection.IsVisible = false;
            return;
        }

        _commercialSection.IsVisible = true;

        _commercialClientPanel.Children.Clear();
        _commercialClientPanel.Children.Add(new TextBlock
        {
            Text = project.ClientOrganisationId ?? "(no client set)",
            Opacity = project.ClientOrganisationId is null ? 0.5 : 1.0,
            TextWrapping = TextWrapping.Wrap,
            FontSize = DesignTokens.FontSizeBody,
        });
        _changeClientButton.IsVisible = _commercialSupport is not null;
        _commercialClientStatus.Text = string.Empty;

        _commercialPurchaseOrderBox.Text = project.PurchaseOrderReference ?? string.Empty;
        _commercialPurchaseOrderStatus.Text = string.Empty;

        _commercialBudgetBox.Text = project.Budget?.ToString() ?? string.Empty;
        _commercialBudgetStatus.Text = string.Empty;

        _commercialRateCardPanel.Children.Clear();
        _commercialRateCardPanel.Children.Add(new TextBlock
        {
            Text = project.RateCardPin is { } pin ? pin.ToString() : "(no rate card pinned)",
            Opacity = project.RateCardPin is null ? 0.5 : 1.0,
            TextWrapping = TextWrapping.Wrap,
            FontSize = DesignTokens.FontSizeBody,
        });
        _changeRateCardButton.IsVisible = _commercialSupport is not null;
        _commercialRateCardStatus.Text = string.Empty;

        _commercialStartDate.SelectedDate = project.StartDate is { } start ? new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue)) : null;
        _commercialTargetDate.SelectedDate = project.TargetDate is { } targetDate ? new DateTimeOffset(targetDate.ToDateTime(TimeOnly.MinValue)) : null;
        _commercialDatesStatus.Text = string.Empty;

        _commercialProjectManagerBox.Text = project.ProjectManagerIdentityId ?? string.Empty;
        _commercialUseMeButton.IsVisible = _commercialSupport is not null;
        _commercialProjectManagerStatus.Text = string.Empty;
    }

    private async Task OnChangeClientAsync()
    {
        if (_commercialSupport is null)
            return;

        var picked = await _commercialSupport.PickClientOrganisationIdAsync(CancellationToken.None).ConfigureAwait(true);
        if (picked is null)
        {
            _commercialClientStatus.Text = "Change client was cancelled.";
            return;
        }

        var organisationId = picked.Length == 0 ? null : picked;
        var result = await _commandDispatcher
            .DispatchAsync(new SetProjectClientCommand(_objectId, _objectKind, organisationId), CancellationToken.None)
            .ConfigureAwait(true);

        await ReportCommercialAsync(_commercialClientStatus, result).ConfigureAwait(true);
    }

    private async Task OnChangeRateCardAsync()
    {
        if (_commercialSupport is null)
            return;

        var picked = await _commercialSupport.PickRateCardIdAsync(CancellationToken.None).ConfigureAwait(true);
        if (picked is null)
        {
            _commercialRateCardStatus.Text = "Pin rate card was cancelled.";
            return;
        }

        var result = await _commandDispatcher
            .DispatchAsync(new PinProjectRateCardCommand(_objectId, _objectKind, picked), CancellationToken.None)
            .ConfigureAwait(true);

        await ReportCommercialAsync(_commercialRateCardStatus, result).ConfigureAwait(true);
    }

    private async Task OnSaveCommercialPurchaseOrderAsync()
    {
        var result = await _commandDispatcher
            .DispatchAsync(new SetProjectPurchaseOrderCommand(_objectId, _objectKind, NullIfEmpty(_commercialPurchaseOrderBox.Text)), CancellationToken.None)
            .ConfigureAwait(true);

        await ReportCommercialAsync(_commercialPurchaseOrderStatus, result).ConfigureAwait(true);
    }

    private async Task OnSaveCommercialBudgetAsync()
    {
        var text = _commercialBudgetBox.Text?.Trim();
        Money? budget = null;

        if (!string.IsNullOrWhiteSpace(text))
        {
            if (!TryParseMoney(text, out var parsed))
            {
                _commercialBudgetStatus.Text = "Budget must be \"<amount> <currency>\" (e.g. \"50000 GBP\"), or blank to clear.";
                return;
            }

            budget = parsed;
        }

        var result = await _commandDispatcher
            .DispatchAsync(new SetProjectBudgetCommand(_objectId, _objectKind, budget), CancellationToken.None)
            .ConfigureAwait(true);

        await ReportCommercialAsync(_commercialBudgetStatus, result).ConfigureAwait(true);
    }

    private async Task OnSaveCommercialDatesAsync()
    {
        var startDate = _commercialStartDate.SelectedDate is { } start ? DateOnly.FromDateTime(start.Date) : (DateOnly?)null;
        var targetDate = _commercialTargetDate.SelectedDate is { } target ? DateOnly.FromDateTime(target.Date) : (DateOnly?)null;

        var result = await _commandDispatcher
            .DispatchAsync(new SetProjectDatesCommand(_objectId, _objectKind, startDate, targetDate), CancellationToken.None)
            .ConfigureAwait(true);

        await ReportCommercialAsync(_commercialDatesStatus, result).ConfigureAwait(true);
    }

    private async Task OnSaveCommercialProjectManagerAsync()
    {
        var result = await _commandDispatcher
            .DispatchAsync(new SetProjectManagerCommand(_objectId, _objectKind, NullIfEmpty(_commercialProjectManagerBox.Text)), CancellationToken.None)
            .ConfigureAwait(true);

        await ReportCommercialAsync(_commercialProjectManagerStatus, result).ConfigureAwait(true);
    }

    /// <summary>Reports a Commercial field's own write outcome — mirrors <see cref="OnSaveBomAsync"/>'s own "refresh before the message survives" discipline.</summary>
    private async Task ReportCommercialAsync(TextBlock statusMessage, CommandResult result)
    {
        var message = result.Succeeded ? "Saved." : result.Message ?? "Save failed.";
        if (result.Succeeded)
            await RefreshAsync().ConfigureAwait(true);
        statusMessage.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(result.Succeeded));
    }

    private static bool TryParseMoney(string value, out Money money)
    {
        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 2 && decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            try
            {
                money = new Money(amount, new Tempest.Core.BusinessGovernance.CurrencyCode(parts[1]));
                return true;
            }
            catch (ArgumentException)
            {
                // Falls through to the failure return below.
            }
        }

        money = default;
        return false;
    }

    /// <summary>
    /// Evidence's own declared sections (`WP 18.2A`, `ADR-0148`, §4):
    /// Subject; Citations (Cite/Remove); Declared figures (Declare);
    /// Lifecycle (status, and the check and issue records read-only — the
    /// Check and Issue actions themselves are `WP 18.2B`); Audit.
    /// </summary>
    private async Task PopulateEvidenceSectionsAsync(Core.Evidence.Evidence evidence)
    {
        // Subject.
        _evidenceSubjectSection.IsVisible = true;
        _evidenceSubjectPanel.Children.Clear();
        _evidenceSubjectPanel.Children.Add(evidence.SubjectId is { } subjectId
            ? await BuildObjectReferenceRowAsync(subjectId).ConfigureAwait(true)
            : new TextBlock { Text = "(no subject tagged)", Opacity = 0.7 });
        _changeSubjectButton.IsVisible = _evidenceSupport is not null;
        _evidenceSubjectStatus.Text = string.Empty;

        // Citations.
        _evidenceCitationsSection.IsVisible = true;
        _evidenceCitationsPanel.Children.Clear();
        _citeButton.IsVisible = _evidenceSupport is not null;
        if (evidence.Citations.Count == 0)
        {
            _evidenceCitationsPanel.Children.Add(new TextBlock { Text = "No citations recorded.", Opacity = 0.7 });
        }
        else
        {
            foreach (var citation in evidence.Citations)
                _evidenceCitationsPanel.Children.Add(BuildCitationRow(citation));
        }
        _evidenceCitationsStatus.Text = string.Empty;

        // Declared figures.
        _evidenceFiguresSection.IsVisible = true;
        _evidenceFiguresPanel.Children.Clear();
        _declareFigureButton.IsVisible = _evidenceSupport is not null;
        if (evidence.DeclaredFigures.Count == 0)
        {
            _evidenceFiguresPanel.Children.Add(new TextBlock { Text = "No figures declared.", Opacity = 0.7 });
        }
        else
        {
            foreach (var figure in evidence.DeclaredFigures)
            {
                _evidenceFiguresPanel.Children.Add(new TextBlock
                {
                    Text = $"{figure.Name} ({figure.Role}) = {figure.Quantity}",
                    FontSize = DesignTokens.FontSizeBody,
                    TextWrapping = TextWrapping.Wrap,
                });
            }
        }
        _evidenceFiguresStatus.Text = string.Empty;

        // Lifecycle: status, and the check and issue records, read-only.
        _evidenceLifecycleSection.IsVisible = true;
        _evidenceLifecyclePanel.Children.Clear();
        _evidenceLifecyclePanel.Children.Add(new TextBlock { Text = $"Status: {evidence.Status}", FontWeight = FontWeight.SemiBold, FontSize = DesignTokens.FontSizeBody });
        _evidenceLifecyclePanel.Children.Add(new TextBlock
        {
            Text = evidence.Check is { } check
                ? $"Check: {check.Outcome} by {check.CheckerName} ({check.CheckerOrganisation}) on {check.DateUtc:u} — \"{check.Statement}\""
                : "Check: (not yet checked)",
            TextWrapping = TextWrapping.Wrap,
            FontSize = DesignTokens.FontSizeBody,
        });
        _evidenceLifecyclePanel.Children.Add(new TextBlock
        {
            Text = evidence.Issue is { } issue
                ? $"Issue: '{issue.IssueReference}' rev '{issue.Revision}' to '{issue.Client}' on {issue.DateUtc:u}"
                : "Issue: (not yet issued)",
            TextWrapping = TextWrapping.Wrap,
            FontSize = DesignTokens.FontSizeBody,
        });

        // `WP 18.2B`: each action visible only when
        // `EvidenceStatusTransitions` actually permits it from here —
        // Check from Draft, Issue from Checked, Revise from Issued.
        _checkButton.IsVisible = _evidenceSupport is not null && evidence.Status == Core.Evidence.EvidenceStatus.Draft;
        _issueButton.IsVisible = _evidenceSupport is not null && evidence.Status == Core.Evidence.EvidenceStatus.Checked;
        _reviseButton.IsVisible = _evidenceSupport is not null && evidence.Status == Core.Evidence.EvidenceStatus.Issued;
        _evidenceLifecycleStatus.Text = string.Empty;

        // Audit.
        _evidenceAuditSection.IsVisible = true;
        _evidenceAuditPanel.Children.Clear();
        if (_auditQuery is null)
        {
            _evidenceAuditPanel.Children.Add(new TextBlock { Text = "Audit is unavailable.", Opacity = 0.7 });
        }
        else
        {
            var records = await _auditQuery.QueryAsync(new AuditQueryCriteria(objectId: evidence.Id)).ConfigureAwait(true);
            if (records.Count == 0)
            {
                _evidenceAuditPanel.Children.Add(new TextBlock { Text = "No audit rows recorded yet.", Opacity = 0.7 });
            }
            else
            {
                foreach (var record in records.OrderByDescending(r => r.OccurredAt).Take(25))
                {
                    var detail = record.Detail.Count == 0 ? string.Empty : " — " + string.Join("; ", record.Detail.Select(kv => $"{kv.Key}: {kv.Value}"));
                    _evidenceAuditPanel.Children.Add(new TextBlock
                    {
                        Text = $"{record.OccurredAt:u}  {record.Action}  by {record.ActorId}{detail}",
                        FontSize = DesignTokens.FontSizeCaption,
                        Opacity = 0.85,
                        TextWrapping = TextWrapping.Wrap,
                    });
                }
            }
        }
    }

    private Control BuildCitationRow(EvidenceCitation citation)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, DesignTokens.SpaceXs) };

        var text = new TextBlock
        {
            Text = $"{citation.Pin} — {citation.RecordDisplayName}" + (citation.SourceCitationSnapshot is null ? string.Empty : $" — {citation.SourceCitationSnapshot}"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = DesignTokens.FontSizeBody,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(text, 0);
        row.Children.Add(text);

        var remove = new Button { Content = "Remove", Padding = new Thickness(10, 1), FontSize = DesignTokens.FontSizeBody };
        remove.Classes.Add(ChromeStyles.Subtle);
        remove.Click += async (_, _) => await OnRemoveCitationAsync(citation.Pin).ConfigureAwait(true);
        Grid.SetColumn(remove, 1);
        row.Children.Add(remove);

        return row;
    }

    private async Task OnRemoveCitationAsync(ReferencePin pin)
    {
        var result = await _commandDispatcher.DispatchAsync(new RemoveEvidenceCitationCommand(_objectId, _objectKind, pin), CancellationToken.None).ConfigureAwait(true);

        var message = result.Succeeded ? result.Message ?? "Citation removed." : result.Message ?? "Remove failed.";
        if (result.Succeeded)
            await RefreshAsync().ConfigureAwait(true);
        _evidenceCitationsStatus.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(result.Succeeded));
    }

    /// <summary>
    /// Cite: collects a released record via <see cref="EvidenceEditorSupport.PickCitationAsync"/>
    /// and dispatches <see cref="CiteEvidenceCommand"/>. A refusal (an
    /// unreleased record — never offered by the real picker, but reachable
    /// through the command directly, `WP 18.2A` acceptance 2) shows here
    /// and, via <see cref="ActionCompleted"/>, in the shell's own status
    /// bar, naming the record and its state — exactly as
    /// <see cref="EvidenceCitationResult.Reason"/> already says it.
    /// </summary>
    private async Task OnCiteAsync()
    {
        if (_evidenceSupport is null)
            return;

        var picked = await _evidenceSupport.PickCitationAsync(CancellationToken.None).ConfigureAwait(true);
        if (picked is null)
        {
            _evidenceCitationsStatus.Text = "Cite was cancelled.";
            return;
        }

        var result = await _commandDispatcher.DispatchAsync(
            new CiteEvidenceCommand(_objectId, _objectKind, picked.Library, picked.RecordId), CancellationToken.None).ConfigureAwait(true);

        var message = result.Succeeded ? result.Message ?? "Cited." : result.Message ?? "The citation was refused.";
        if (result.Succeeded)
            await RefreshAsync().ConfigureAwait(true);
        _evidenceCitationsStatus.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(result.Succeeded));
    }

    private async Task OnDeclareFigureAsync()
    {
        if (_evidenceSupport is null)
            return;

        var input = await _evidenceSupport.PickDeclaredFigureAsync(CancellationToken.None).ConfigureAwait(true);
        if (input is null)
        {
            _evidenceFiguresStatus.Text = "Declare was cancelled.";
            return;
        }

        var result = await _commandDispatcher.DispatchAsync(
            new DeclareEvidenceFigureCommand(_objectId, _objectKind, input.Name, input.Role, input.Quantity), CancellationToken.None).ConfigureAwait(true);

        var message = result.Succeeded ? result.Message ?? "Declared." : result.Message ?? "Declare failed.";
        if (result.Succeeded)
            await RefreshAsync().ConfigureAwait(true);
        _evidenceFiguresStatus.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(result.Succeeded));
    }

    /// <summary>
    /// Change Subject: collects a Part, Assembly, Requirement or
    /// Deliverable via the real Subject picker
    /// (<see cref="EvidenceEditorSupport.PickSubjectAsync"/>, the identical
    /// <see cref="Views.SubjectPicker"/> `18.2A`'s own Create form uses)
    /// and dispatches <see cref="SetEvidenceSubjectCommand"/>. A refusal —
    /// the record is Issued — shows here and, via
    /// <see cref="ActionCompleted"/>, in the shell's own status bar
    /// (`WP 18.2B`, closing a gap `WP 18.2A` disclosed).
    /// </summary>
    private async Task OnChangeSubjectAsync()
    {
        if (_evidenceSupport is null)
            return;

        var picked = await _evidenceSupport.PickSubjectAsync(CancellationToken.None).ConfigureAwait(true);

        var result = await _commandDispatcher.DispatchAsync(
            new SetEvidenceSubjectCommand(_objectId, _objectKind, picked), CancellationToken.None).ConfigureAwait(true);

        var message = result.Succeeded ? result.Message ?? "Subject changed." : result.Message ?? "The subject change was refused.";
        if (result.Succeeded)
            await RefreshAsync().ConfigureAwait(true);
        _evidenceSubjectStatus.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(result.Succeeded));
    }

    /// <summary>
    /// Check: collects the checker's name, organisation, statement and
    /// outcome via <see cref="EvidenceEditorSupport.PickCheckAsync"/> and
    /// dispatches <see cref="RecordEvidenceCheckCommand"/> — the client's
    /// own review, entered by hand, unless <c>Evidence:IndependentCheck</c>
    /// is on, in which case the acting principal (resolved server-side, never
    /// asked here) stands as the checker and the same principal as the
    /// author is refused (`WP 18.2B`, §1).
    /// </summary>
    private async Task OnCheckAsync()
    {
        if (_evidenceSupport is null)
            return;

        var input = await _evidenceSupport.PickCheckAsync(CancellationToken.None).ConfigureAwait(true);
        if (input is null)
        {
            _evidenceLifecycleStatus.Text = "Check was cancelled.";
            return;
        }

        var result = await _commandDispatcher.DispatchAsync(
            new RecordEvidenceCheckCommand(_objectId, _objectKind, input.CheckerName, input.CheckerOrganisation, input.Statement, input.Outcome),
            CancellationToken.None).ConfigureAwait(true);

        var message = result.Succeeded ? result.Message ?? "Checked." : result.Message ?? "The check was refused.";
        if (result.Succeeded)
            await RefreshAsync().ConfigureAwait(true);
        _evidenceLifecycleStatus.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(result.Succeeded));
    }

    /// <summary>
    /// Issue: collects the issue reference, revision and client via
    /// <see cref="EvidenceEditorSupport.PickIssueAsync"/> and dispatches
    /// <see cref="IssueEvidenceCommand"/>, which — where a renderer is
    /// available — also renders and attaches the issue sheet
    /// (`WP 18.2B`, §2). The attached sheet appears in the Files section's
    /// own generic Attachments list (unchanged from `WP 18.2A`), openable
    /// there through the existing document viewer and exportable through
    /// the existing file picker; nothing new is built here for either.
    /// </summary>
    private async Task OnIssueAsync()
    {
        if (_evidenceSupport is null)
            return;

        var input = await _evidenceSupport.PickIssueAsync(CancellationToken.None).ConfigureAwait(true);
        if (input is null)
        {
            _evidenceLifecycleStatus.Text = "Issue was cancelled.";
            return;
        }

        var result = await _commandDispatcher.DispatchAsync(
            new IssueEvidenceCommand(_objectId, _objectKind, input.IssueReference, input.Revision, input.Client),
            CancellationToken.None).ConfigureAwait(true);

        var message = result.Succeeded ? result.Message ?? "Issued." : result.Message ?? "The issue was refused.";
        if (result.Succeeded)
            await RefreshAsync().ConfigureAwait(true);
        _evidenceLifecycleStatus.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(result.Succeeded));
    }

    /// <summary>
    /// Revise: reopens the selected, issued evidence as a new Draft
    /// revision (<see cref="ReviseEvidenceCommand"/>); the issued revision
    /// stays readable, unchanged, via its own revision history
    /// (`WP 18.2B`, §3). No form of its own — nothing needs collecting.
    /// </summary>
    private async Task OnReviseAsync()
    {
        var result = await _commandDispatcher.DispatchAsync(
            new ReviseEvidenceCommand(_objectId, _objectKind), CancellationToken.None).ConfigureAwait(true);

        var message = result.Succeeded ? result.Message ?? "Revised." : result.Message ?? "The revision was refused.";
        if (result.Succeeded)
            await RefreshAsync().ConfigureAwait(true);
        _evidenceLifecycleStatus.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(result.Succeeded));
    }

    /// <summary>Files' own "add via picker" affordance (`WP 18.2A`, §4) — reads real bytes through <see cref="IFilePicker"/> and attaches them directly (<see cref="IHasAttachments.AttachContentAsync"/>), never the metadata-only mini-form below it.</summary>
    private async Task OnAddFileViaPickerAsync()
    {
        if (_evidenceSupport is null || _populatedTarget is not IHasAttachments attachable)
            return;

        var picked = await _evidenceSupport.FilePicker.PickFilesAsync(
            new FilePickerRequest("Pick files to attach", AllowMultiple: true)).ConfigureAwait(true);

        if (picked.Count == 0)
            return;

        foreach (var file in picked)
        {
            var content = await file.ReadAsync().ConfigureAwait(true);
            await attachable.AttachContentAsync(file.Name, file.ContentType, content).ConfigureAwait(true);
        }

        await RefreshAsync().ConfigureAwait(true);
        var message = $"Attached {picked.Count} file(s).";
        _attachmentStatusMessage.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(true));
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
    private async Task PopulateRequirementAsync(IEngineeringObject target)
    {
        _ = target;

        if (_requirementsService is null || _objectKind != RequirementsService.RequirementDocumentKind)
        {
            _requirementSection.IsVisible = false;
            return;
        }

        var requirement = await _requirementsService.FindAsync(_objectId).ConfigureAwait(true);
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
        await RefreshAsync().ConfigureAwait(true);
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
    private async Task PopulateCalculationExecutionAsync(IEngineeringObject target)
    {
        // `WP 17.9.1`: the raw-JSON Execute box is retired from this editor.
        // It was a developer seam — a template picker over a JSON textbox —
        // and the first Windows review of `v0.17.0` met it as the first thing
        // offered on a Calculation. Calculations are run, named and traced
        // in the Engineering Calculations workspace (rail); `WP 18.2A`
        // replaces both surfaces with the calc-sheet editor. The section stays
        // in the tree, hidden, so the command wiring behind it is untouched.
        _calculationSection.IsVisible = false;
        _calculationPointerSection.IsVisible = _objectKind is "Calculation" or "CalculationSet";

        if (_calculationTemplates is null || _objectKind is not ("Calculation" or "CalculationSet"))
            return;

        _availableTemplates = _calculationTemplates.Templates;
        _calculationTemplatePicker.ItemsSource = _availableTemplates.Select(t => $"{t.Metadata.Name} ({t.CalculationId})").ToList();
        if (_availableTemplates.Count > 0)
            _calculationTemplatePicker.SelectedIndex = 0;

        _calculationHasBeenExecuted = target is IHasRelationships hasRelationships
            && (await hasRelationships.GetRelationshipsAsync().ConfigureAwait(true))
                .Any(r => r.RelationshipKind == CalculationTemplateRegistry.CalculatedByRelationshipKind);

        _calculationExecuteButton.Content = _calculationHasBeenExecuted ? "Recalculate" : "Execute";
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
            await RefreshAsync().ConfigureAwait(true);
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
            await RefreshAsync().ConfigureAwait(true);
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
    /// <summary>
    /// Runs <see cref="PopulateAttachmentsAsync"/> fire-and-forget, for the
    /// <see cref="OpenAttachmentRequested"/> accessor's own synchronous
    /// re-population on first subscriber (`WP 18.1A`) — a failure is
    /// reported through <see cref="ActionCompleted"/> rather than thrown
    /// into the void, mirroring <see cref="PopulateInBackground"/>.
    /// </summary>
    private async Task PopulateAttachmentsSafelyAsync(IEngineeringObject target)
    {
        try
        {
            await PopulateAttachmentsAsync(target).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ActionCompleted?.Invoke($"Failed to load attachments: {ex.Message}", ActionOutcome.Failed);
        }
    }

    private async Task PopulateAttachmentsAsync(IEngineeringObject target)
    {
        if (target is not IHasAttachments attachable)
        {
            _attachmentsSection.IsVisible = false;
            return;
        }

        _attachmentsSection.IsVisible = true;
        _attachmentsListPanel.Children.Clear();

        // `WP 18.2A`, §4: Evidence's own Files section shows size and hash,
        // and — when a picker is wired — real bytes can be added directly,
        // never only the metadata-only mini-form below.
        _addFileViaPickerButton.IsVisible = _evidenceSupport is not null && string.Equals(_objectKind, Core.Evidence.Evidence.CanonicalKind, StringComparison.Ordinal);

        var attachments = await attachable.GetAttachmentsAsync().ConfigureAwait(true);
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
                        IconGeometry.Build(IconGeometry.Paperclip, 13),
                        new TextBlock
                        {
                            Text = $"{attachment.FileName}  ({attachment.ContentType}, {attachment.SizeInBytes:N0} bytes"
                                + (attachment.ContentHash is { } hash ? $", sha256 {hash}" : string.Empty) + ")",
                            FontSize = DesignTokens.FontSizeBody,
                            TextWrapping = TextWrapping.Wrap,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        },
                    },
                };

                if (_openAttachmentRequested is not null)
                {
                    var open = new Button { Content = "Open", Padding = new Thickness(10, 1), FontSize = DesignTokens.FontSizeBody };
                    open.Classes.Add(ChromeStyles.Flat);
                    var captured = attachment;

                    // `WP 16.5A-R2`. Named explicitly, and named per file.
                    //
                    // Without this the button had no accessible name of its
                    // own at all: `ContentControlAutomationPeer.GetNameCore()`
                    // fell back to `Content?.ToString()`, which reads "Open"
                    // only because `Content` happens to be that literal
                    // string today. An object with several attachments
                    // therefore announced "Open, button" once per row with
                    // nothing to tell them apart, and any redesign that made
                    // the content a panel or a glyph — the shape the sibling
                    // relationship-row button already uses — would have
                    // silently degraded the name to a type name with no test
                    // to notice.
                    //
                    // Including the file name also avoids reproducing
                    // `TD-132` here: that row's identical-name problem is the
                    // reason this one is named for its target rather than for
                    // its verb.
                    Avalonia.Automation.AutomationProperties.SetName(open, $"Open {captured.FileName}");
                    open.Click += (_, _) => _openAttachmentRequested?.Invoke(attachable, captured);
                    row.Children.Add(open);
                }

                // `WP 18.2B`, §2: "Export saves [the issue sheet] through
                // IFilePicker.PickSavePathAsync" — offered for every
                // attachment on an Evidence record, not the issue sheet
                // alone, since nothing distinguishes it from any other
                // attached file once it is one, and a second, narrower
                // mechanism naming just that one attachment would only
                // duplicate this one.
                if (_evidenceSupport is not null && string.Equals(_objectKind, Core.Evidence.Evidence.CanonicalKind, StringComparison.Ordinal))
                {
                    var export = new Button { Content = "Export", Padding = new Thickness(10, 1), FontSize = DesignTokens.FontSizeBody };
                    export.Classes.Add(ChromeStyles.Flat);
                    var captured = attachment;
                    Avalonia.Automation.AutomationProperties.SetName(export, $"Export {captured.FileName}");
                    export.Click += async (_, _) => await OnExportAttachmentAsync(attachable, captured).ConfigureAwait(true);
                    row.Children.Add(export);
                }

                _attachmentsListPanel.Children.Add(row);
            }
        }

        _attachmentFileNameBox.Text = string.Empty;
        _attachmentContentTypeBox.Text = string.Empty;
        _attachmentSizeBox.Text = string.Empty;
        _attachmentStatusMessage.Text = string.Empty;
    }

    /// <summary>
    /// Export: reads one attachment's own verified bytes and saves them
    /// through <see cref="IFilePicker.PickSavePathAsync"/> (`WP 18.2B`,
    /// §2) — the mechanism the issue sheet exports through, offered for
    /// every Evidence attachment rather than that one alone (this row's
    /// own remarks).
    /// </summary>
    private async Task OnExportAttachmentAsync(IHasAttachments attachable, IAttachment attachment)
    {
        if (_evidenceSupport is null)
            return;

        var destination = await _evidenceSupport.FilePicker
            .PickSavePathAsync(new SavePickerRequest($"Export {attachment.FileName}", attachment.FileName), CancellationToken.None)
            .ConfigureAwait(true);

        if (destination is null)
        {
            _attachmentStatusMessage.Text = "Export was cancelled.";
            return;
        }

        var content = await attachable.ReadAttachmentContentAsync(attachment.Id).ConfigureAwait(true);
        if (!content.IsAvailable)
        {
            _attachmentStatusMessage.Text = $"'{attachment.FileName}' could not be read — its stored content is {content.Status}.";
            ActionCompleted?.Invoke(_attachmentStatusMessage.Text, ActionOutcome.Failed);
            return;
        }

        await File.WriteAllBytesAsync(destination, content.Bytes, CancellationToken.None).ConfigureAwait(true);

        // Reading and saving a copy changes nothing in the domain — never
        // `ActionOutcome.Changed`, which would (wrongly) tell a WorkspaceChanged
        // subscriber to reload as though a write had happened.
        var message = $"Exported '{attachment.FileName}' to '{destination}'.";
        _attachmentStatusMessage.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.NoChange);
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
            await RefreshAsync().ConfigureAwait(true);
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
        _readOnlyToggle.Content = readOnly ? "Read-Only" : "Editable";

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

        await RefreshAsync().ConfigureAwait(true);
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
