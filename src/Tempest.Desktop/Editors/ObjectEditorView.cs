using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
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
using Tempest.Core.People;
using Tempest.Core.ReferenceData;
using Tempest.Core.Requirements;
using Tempest.Core.Verification;
using Tempest.Desktop.DigitalThread;
using Tempest.Desktop.Editors.Sections;
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
/// <see cref="ResolveClientNameAsync"/>/<see cref="ResolveRateCardAsync"/>
/// (`WP 19.1A-R1` disclosure #4, wired by `WP 19.2B`) resolve,
/// asynchronously and never blocking, what the Commercial section actually
/// shows for the client and the rate card — the organisation's own name
/// and the card's own code, name and pinned revision, rather than the bare
/// id/<see cref="Tempest.Core.ReferenceData.ReferencePin"/> a reader cannot
/// otherwise place. Both are optional trailing parameters, not required
/// alongside the three above: <see langword="null"/> (any test, or a
/// composition that has not yet wired a resolver), or a resolver answering
/// <see langword="null"/> for a record no longer in its catalogue, leaves
/// the section showing the id/pin exactly as before, honestly unresolved,
/// never blocking or throwing over it.
/// </summary>
public sealed record ProjectCommercialEditorSupport(
    Func<CancellationToken, Task<string?>> PickClientOrganisationIdAsync,
    Func<CancellationToken, Task<string?>> PickRateCardIdAsync,
    Func<string?> CurrentPrincipalIdentityId,
    Func<string, CancellationToken, Task<string?>>? ResolveClientNameAsync = null,
    Func<ReferencePin, CancellationToken, Task<(string Code, string Name)?>>? ResolveRateCardAsync = null);

/// <summary>
/// The requirement Owner section's own collaborators (`WP 20.10F`, Product
/// Owner finding D8): <see cref="Persons"/> is read fresh every time the
/// section populates, so a person released anywhere is offered the next
/// time this editor opens or refreshes — never cached, mirroring
/// <see cref="Views.LibrariesView"/>'s own identical "read fresh, never
/// cached" discipline. <see cref="AddPersonAsync"/> is the drop-down's own
/// <b>Add person…</b> affordance: registers, verifies and releases a new
/// person in one action (<see cref="Views.PersonAddPrompt"/>) and returns
/// its record id and display name, or <see langword="null"/> if the user
/// cancelled. <see langword="null"/> (any test that constructs this editor
/// directly without it) leaves the Owner control showing only whatever
/// free text a requirement already carries, exactly as before this Work
/// Package — the identical discipline <see cref="EvidenceEditorSupport"/>
/// and <see cref="ProjectCommercialEditorSupport"/> already established.
/// </summary>
public sealed record RequirementOwnerEditorSupport(
    IPersonCatalog Persons,
    Func<CancellationToken, Task<(string RecordId, string DisplayName)?>> AddPersonAsync);

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
/// <b>The shell, and twenty-three sections behind one contract (`WP
/// 21.1B`, `TD-109`'s own closure precedent, `WP 19.2A`).</b> This class
/// is now the shell alone: the header, Identity and Content (the one pair
/// with a hidden coupling through this class's own Save/Cancel/read-only
/// state — see <see cref="OnSaveAsync"/>/<see cref="ApplyReadOnlyState"/>
/// — which is why that pair, alone, never became its own
/// <see cref="IEditorSection"/>), the change subscription, and
/// <see cref="EditorSections"/>'s own ordered list, built once in
/// <see cref="BuildLayout"/> and driven uniformly by
/// <see cref="PopulateFromAsync"/>/<see cref="PopulateFromRequirementAsync"/>.
/// Every other section — Lifecycle, Relationships, Validation, Bill of
/// Materials, Owner/Priority, Execute/the Calculation pointer/Due, Record
/// Result, Attachments, Description, Where used, Evidence's own five
/// (Subject/Citations/Declared figures/Lifecycle/Audit), Commercial,
/// Invoice Lines/Connector, Quotation Lines — lives in its own file under
/// <c>Editors/Sections/</c>, moved verbatim from what used to be one
/// constructor and one class here.
/// </para>
/// <para>
/// <b>Reads directly, mutates only through Commands (`ADR-0063`,
/// unchanged).</b> Every read (<see cref="IHasRevisions.Content"/>,
/// <see cref="IHasLifecycle.Status"/>/<see cref="IHasLifecycle.History"/>,
/// <see cref="IHasRelationships.GetRelationshipsAsync"/>,
/// <see cref="IValidatable.ValidateAsync"/>) is a direct call against the
/// real Domain object — <see cref="EngineeringDomainContext.Repository"/>'s
/// own already-permitted read surface. Every write (Rename, Revise) goes
/// through <see cref="IWorkspaceManager.RenameObjectAsync"/>/
/// <see cref="IWorkspaceManager.ReviseObjectAsync"/> (`ADR-0096`/`ADR-0097`)
/// — a real, registered <see cref="IWorkspaceCommand"/>, dispatched
/// exactly as every other Workspace mutation already is. Neither this
/// class nor any section calls a mutating Domain method directly.
/// </para>
/// <para>
/// <b>"Editable properties" — two generic fields on the shell, plus five
/// real, discipline-specific sections (`WP 10.7A`, Feature Completion,
/// closing `FCR-0068`).</b> Name (<see cref="IWorkspaceManager.CanRename"/>)
/// and Content (<see cref="IWorkspaceManager.CanRevise"/>) remain the two
/// uniformly-real fields every Kind shares, gated per-Kind exactly like
/// <see cref="Tempest.Desktop.Views.PropertyInspectorView"/>'s own
/// established "editable only where a real command exists" discipline.
/// Five further sections are each Kind-gated on the real object itself (a
/// C# <see langword="is"/> type-check, the identical idiom every
/// section's own <c>AppliesTo</c> uses, never a Kind-string switch) or on
/// the Kind directly where the data lives in a service the object graph
/// itself does not expose (Requirements Owner/Priority, Calculations
/// Execute): Mechanical BOM (<see cref="IHasBomLine"/>, <see cref="Sections.BillOfMaterialsSection"/>),
/// Requirements Owner/Priority (<see cref="IRequirementsService"/>,
/// <see cref="Sections.RequirementOwnerPrioritySection"/>), Calculations
/// Execute/Recalculate (<see cref="CalculationTemplateRegistry"/>,
/// <see cref="Sections.CalculationExecuteSection"/>), Verification Record
/// Result (<see cref="IVerificationActivity"/>, <see cref="Sections.VerificationResultSection"/>),
/// Documents Attachments (<see cref="IHasAttachments"/>, <see cref="Sections.AttachmentsSection"/>)
/// — each dispatches its own already-registered command directly via
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
/// resolves the real object too (`WP 10.8A`). This class holds the real
/// object directly, so it can call the real method — informational only,
/// never blocking Save (see class remarks on <see cref="OnSaveAsync"/>).
/// A Requirement carries no <see cref="IValidatable"/> of its own
/// (<see cref="IRequirementsService"/> exposes no validation-equivalent
/// read), so its own Validation section honestly shows the identical
/// "supports no validation" fallback every non-<see cref="IValidatable"/>
/// Kind already shows — see <see cref="PopulateFromRequirementAsync"/>.
/// </para>
/// <para>
/// <b>A Requirement opens in this same editor too (`TD-41`, `WP 19.10I`)</b>
/// — <see cref="TryCreate"/> falls back to <see cref="IRequirementsService"/>
/// when <see cref="EngineeringDomainContext.Repository"/> has nothing for
/// the id, since a Requirement is a separate aggregate that never lives
/// there. <see cref="PopulateFromRequirementAsync"/> is the Requirement-only
/// analogue of <see cref="PopulateFromAsync"/>, populating only the
/// sections a Requirement can honestly answer: Identity (its own
/// Identifier/Id/Revision/Category), Content (its own Statement, revised
/// through the identical <see cref="IWorkspaceManager.ReviseObjectAsync"/>
/// path every other Kind's Content field already uses), Owner/Priority
/// (already real, see above), and Relationships (both "allocated to" and
/// "verified by" are recorded as outgoing references from the requirement
/// itself, so <see cref="IRequirementsService.GetRelationshipsAsync"/>
/// alone — no incoming-relationship query, which this discipline's own
/// storage has no capability for — renders real data). Every section with
/// no honest reading for a Requirement (BOM, Calculation, Verification
/// Result, Attachments, Description, Where used, Commercial, Invoice,
/// Quotation Lines, Evidence) stays collapsed at its own already-hidden
/// <see cref="BuildLayout"/> default, never populated with a "not
/// applicable" placeholder.
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
    private readonly RequirementOwnerEditorSupport? _ownerSupport;

    private readonly TextBlock _identityReadout = new() { Opacity = 0.7, FontSize = DesignTokens.FontSizeCaption };
    private readonly TextBox _nameBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _contentBox = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 140, FontSize = DesignTokens.FontSizeBody };
    private readonly ToggleButton _readOnlyToggle = new() { Content = "Editable", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _saveButton = new() { Content = "Save", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _statusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    /// <summary>What the editor says on a Calculation instead of offering a JSON box (`WP 17.9.1`).</summary>
    public const string CalculationPointerGuidance =
        "Calculations are run, named and traced in the Engineering Calculations workspace — open it from the rail on the left. "
        + "This editor holds the calculation's identity, lifecycle and attachments.";

    // `WP 21.1B`, brief scope item 3 ("Registration, not a switch"): every
    // section but Identity and Content (this pair's own hidden coupling
    // through the header's Save/Cancel/read-only state is the one thing
    // this split's Kill Switch keeps on the shell) comes from
    // EditorSections.All — built once, here, in BuildLayout, and driven
    // uniformly by PopulateFromAsync/PopulateFromRequirementAsync.
    private IReadOnlyList<IEditorSection> _sections = [];
    private EditorSectionContext _sectionContext = null!;
    private Expander _contentSection = null!;

    // The one section the shell still needs a concrete reference to
    // (found in `_sections` once, in the constructor, below) —
    // `AttachFilesAsync`/`PopulateAttachmentsAsync` are compatibility
    // seams an existing test reaches by name (see AttachmentsSection's own
    // remarks), and `OpenAttachmentRequested`'s own accessor asks this
    // section to reload on its first subscriber.
    private AttachmentsSection _attachmentsSection = null!;

    private string _originalName = string.Empty;
    private string _originalContent = string.Empty;
    private bool _isDirty;
    private bool _suppressDirtyTracking;

    private Action<IHasAttachments, IAttachment>? _openAttachmentRequested;

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

    private readonly WorkspaceChangesSubscription _workspaceChanges;

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
        get => _workspaceChanges.Feed;
        set => _workspaceChanges.Feed = value;
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

            // `WP 21.1B`: the "was anything already populated" guard now
            // lives inside AttachmentsSection.ReloadIfPopulatedAsync itself
            // (its own _lastSubject, the section's local mirror of the
            // pre-split shell's own _populatedTarget field).
            if (hadNone && _openAttachmentRequested is not null)
                _ = _attachmentsSection.ReloadIfPopulatedAsync();
        }

        remove => _openAttachmentRequested -= value;
    }

    private ObjectEditorView(
        Guid objectId, string objectKind, EngineeringDomainContext domainContext, IWorkspaceManager manager, Action<Guid, string> navigateToObject,
        ICommandDispatcher commandDispatcher, IRequirementsService? requirementsService, CalculationTemplateRegistry? calculationTemplates,
        IKindEditorDeclarationRegistry? declarations, EvidenceEditorSupport? evidenceSupport, IAuditQuery? auditQuery,
        ProjectCommercialEditorSupport? commercialSupport, RequirementOwnerEditorSupport? ownerSupport)
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
        _ownerSupport = ownerSupport;

        // `WP 18.1A`: once this tab closes and the control leaves the
        // visual tree, drop the change-feed subscription — see
        // WorkspaceChanges's own remarks. `WP 19.7C`: the same
        // <see cref="WorkspaceChangesSubscription"/> helper every sibling
        // view now takes — a Document Area tab left for another one is
        // detached, not closed, so this editor shared the reattach defect
        // (the helper's own remarks; the v0.19.1 warning on hidden editors).
        _workspaceChanges = new WorkspaceChangesSubscription(this, OnWorkspaceChanged);

        // `WP 21.1B`: what every extracted IEditorSection needs — built
        // once, here, since every field it reads is already assigned above
        // and RefreshAsync/ActionCompleted are both reachable on `this`.
        _sectionContext = new EditorSectionContext(
            _objectId, _objectKind, _domainContext, _manager, _commandDispatcher, _navigateToObject,
            _requirementsService, _calculationTemplates, _declarations, _evidenceSupport, _auditQuery,
            _commercialSupport, _ownerSupport,
            RefreshAsync: () => RefreshAsync(),
            ReportAction: (message, outcome) => ActionCompleted?.Invoke(message, outcome),
            GetOpenAttachmentHandler: () => _openAttachmentRequested);

        Content = BuildLayout();

        // Chrome treatments (`WP-Z4` Productisation Phase 1) — this editor
        // predates ChromeStyles/IconGeometry (`WP 10.0A`) and had never
        // been brought forward; every button now carries the same three
        // treatments the rest of the shell uses, rather than the
        // unstyled default FluentTheme button it rendered as before.
        _readOnlyToggle.Classes.Add(ChromeStyles.Subtle);
        _saveButton.Classes.Add(ChromeStyles.Primary);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);

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
        // buffered-edit lifecycle). `WP 21.1B`: Bill of Materials,
        // Owner/Priority, Execute, Due, Record Result, the project
        // Commercial section, all five Evidence sections and Attachments
        // now wire their own actions inside their own Build().
    }

    /// <summary>Gets whether this editor holds local, buffered edits (Name and/or Content) not yet committed via Save — this Work Package's own genuine, buffered dirty-state (distinct from and unrelated to <see cref="IWorkspaceView.IsDirty"/>, which remains permanently <see langword="false"/>, by design, unchanged — see class remarks).</summary>
    public bool IsDirty => _isDirty;

    /// <summary>
    /// Attempts to build a real Object Editor for <paramref name="objectId"/>/
    /// <paramref name="objectKind"/> — returns <see langword="null"/> if
    /// neither the engineering-object repository nor (for a Requirement)
    /// <see cref="IRequirementsService"/> could possibly back the Kind (a
    /// synthetic, non-repository Kind such as Calculations' own
    /// <c>"CalculationTemplate"</c>, or the Sample Explorer's own fixed,
    /// fictional content) — the caller's own signal to fall back to the
    /// existing generic three-line document body instead.
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
    /// <para>
    /// <b>A Requirement (`TD-41`, `WP 19.10I`) is never in the
    /// repository above — it is a separate aggregate owned entirely by
    /// <see cref="IRequirementsService"/> (a genuinely different store,
    /// `ADR-0058`).</b> Rather than spend a second synchronous existence
    /// check here (the Desktop test suite's own <c>NoBlockingPersistenceCallsTests</c>
    /// pins this file to exactly the one above), <paramref name="objectKind"/> alone
    /// is the honest routing signal once the repository has nothing: every
    /// document tab ever opened at <see cref="RequirementsService.RequirementDocumentKind"/>
    /// is opened by the real Requirements discipline (the Explorer, the
    /// Create/Revise/Link commands, the Quote tab's own "Open requirement"),
    /// never a synthetic placeholder the way <c>CalculationTemplate</c> or
    /// the Sample Explorer's fixed content are. The existence read itself
    /// still happens, honestly, inside <see cref="PopulateRequirementInBackground"/> —
    /// asynchronously, exactly like every other Kind's own population — and
    /// reports failure through <see cref="ActionCompleted"/> rather than
    /// silently doing nothing on the rare id that turns out not to resolve.
    /// <paramref name="requirementsService"/> is <see langword="null"/> for
    /// any caller that has never threaded it through (an existing test,
    /// chiefly), which still falls back to the generic body exactly as
    /// before.
    /// </para>
    /// </remarks>
    public static ObjectEditorView? TryCreate(
        Guid objectId, string objectKind, EngineeringDomainContext domainContext, IWorkspaceManager manager, Action<Guid, string> navigateToObject,
        ICommandDispatcher commandDispatcher, IRequirementsService? requirementsService = null, CalculationTemplateRegistry? calculationTemplates = null,
        IWorkspaceChanges? workspaceChanges = null, IKindEditorDeclarationRegistry? declarations = null,
        EvidenceEditorSupport? evidenceSupport = null, IAuditQuery? auditQuery = null, ProjectCommercialEditorSupport? commercialSupport = null,
        RequirementOwnerEditorSupport? ownerSupport = null)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(navigateToObject);
        ArgumentNullException.ThrowIfNull(commandDispatcher);

        // The one disclosed exception — see this method's own remarks.
        var target = domainContext.Repository.FindAsync(objectId).GetAwaiter().GetResult();
        if (target is not null)
        {
            var realEditor = new ObjectEditorView(
                objectId, objectKind, domainContext, manager, navigateToObject, commandDispatcher, requirementsService, calculationTemplates,
                declarations, evidenceSupport, auditQuery, commercialSupport, ownerSupport)
            {
                WorkspaceChanges = workspaceChanges,
            };
            realEditor.PopulateInBackground(target);
            return realEditor;
        }

        // TD-41 — see this method's own remarks.
        if (requirementsService is null || objectKind != RequirementsService.RequirementDocumentKind)
            return null;

        var requirementEditor = new ObjectEditorView(
            objectId, objectKind, domainContext, manager, navigateToObject, commandDispatcher, requirementsService, calculationTemplates,
            declarations, evidenceSupport, auditQuery, commercialSupport, ownerSupport)
        {
            WorkspaceChanges = workspaceChanges,
        };
        requirementEditor.PopulateRequirementInBackground(objectId);
        return requirementEditor;
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

    /// <summary>
    /// The Requirement-only analogue of <see cref="PopulateInBackground"/>
    /// (`TD-41`, `WP 19.10I`) — <see cref="TryCreate"/>'s own remarks
    /// explain why the existence read happens here, asynchronously,
    /// rather than as a second synchronous check inside <see cref="TryCreate"/>
    /// itself. A requirement id that turns out not to resolve (the rare
    /// case — see <see cref="TryCreate"/>'s own remarks) reports honestly
    /// through <see cref="ActionCompleted"/> rather than leaving the
    /// editor silently blank.
    /// </summary>
    private void PopulateRequirementInBackground(Guid requirementId)
    {
        _ = RunAsync();

        async Task RunAsync()
        {
            try
            {
                var requirement = await _requirementsService!.FindAsync(requirementId).ConfigureAwait(true);
                if (requirement is null)
                {
                    ActionCompleted?.Invoke("Requirement not found.", ActionOutcome.Failed);
                    return;
                }

                await PopulateFromRequirementAsync(requirement).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ActionCompleted?.Invoke($"Failed to load: {ex.Message}", ActionOutcome.Failed);
            }
        }
    }

    /// <summary>Test-only (`WP 19.7C`, <c>WorkspaceChangesReattachTests</c>): counts every <see cref="RefreshAsync"/> call, proving a reattached view's subscription still reaches <see cref="OnWorkspaceChanged"/>.</summary>
    internal int RefreshCount { get; private set; }

    /// <summary>
    /// Re-reads the real object and refreshes every section — never a
    /// cached copy, mirroring <see cref="IWorkspaceView.RefreshAsync"/>'s
    /// own identical discipline. Falls back to <see cref="IRequirementsService"/>
    /// (`TD-41`, `WP 19.10I`) exactly as <see cref="TryCreate"/> does, for
    /// the identical reason — a Requirement is never in the repository
    /// read first.
    /// </summary>
    public async Task RefreshAsync()
    {
        RefreshCount++;
        var target = await _domainContext.Repository.FindAsync(_objectId).ConfigureAwait(true);
        if (target is not null)
        {
            await PopulateFromAsync(target).ConfigureAwait(true);
            return;
        }

        if (_requirementsService is not null && _objectKind == RequirementsService.RequirementDocumentKind)
        {
            var requirement = await _requirementsService.FindAsync(_objectId).ConfigureAwait(true);
            if (requirement is not null)
                await PopulateFromRequirementAsync(requirement).ConfigureAwait(true);
        }
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

        // `WP 21.1B`, brief scope item 3: "Registration, not a switch" —
        // every section but Identity/Content (see class remarks on that
        // pair's own hidden coupling) comes from one ordered factory now,
        // built here in one pass; EditorSections.All's own remarks name
        // the exact splice point Content sits at.
        _sections = EditorSections.All(_sectionContext);
        _attachmentsSection = _sections.OfType<AttachmentsSection>().Single();
        var sectionExpanders = _sections.Select(section => section.Build(_sectionContext)).ToList();

        var body = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        body.Children.Add(header);
        body.Children.Add(_statusMessage);
        body.Children.Add(new Separator());
        body.Children.Add(identitySection);
        for (var i = 0; i < EditorSections.SectionsBeforeContent; i++)
            body.Children.Add(sectionExpanders[i]);
        body.Children.Add(_contentSection);
        for (var i = EditorSections.SectionsBeforeContent; i < sectionExpanders.Count; i++)
            body.Children.Add(sectionExpanders[i]);

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

        // `WP 21.1B`, brief scope item 3: every section but Identity/Content
        // is called unconditionally, in registration order — each one's own
        // AppliesTo (Kind, a real object's own type, or a declaration) is
        // what actually decides its own visibility, inside its own
        // LoadAsync; the shell never re-decides that here.
        foreach (var section in _sections)
            await section.LoadAsync(target, CancellationToken.None).ConfigureAwait(true);

        _isDirty = false;
        _statusMessage.Text = string.Empty;
        ApplyReadOnlyState();

        _suppressDirtyTracking = false;
    }

    /// <summary>
    /// The Requirement-only analogue of <see cref="PopulateFromAsync"/>
    /// (`TD-41`, `WP 19.10I`) — see the class remarks for which sections
    /// apply to a Requirement and why. <paramref name="requirement"/> is
    /// <c>Tempest.Core.Requirements.IRequirement</c>, never the unrelated,
    /// same-named <c>Tempest.Core.EngineeringDomain.IRequirement</c> this
    /// class's every other method means by a bare "target" — there is no
    /// real <see cref="IEngineeringObject"/> behind a Requirement at all.
    /// </summary>
    private async Task PopulateFromRequirementAsync(Tempest.Core.Requirements.IRequirement requirement)
    {
        _suppressDirtyTracking = true;

        // No real IEngineeringObject backs a Requirement (see class
        // remarks) — the uniform loop below calls AttachmentsSection.LoadAsync(null, ...)
        // like every other section, which sets its own _lastSubject to
        // null (AppliesTo(null) is false, so nothing else about it
        // changes), so AttachmentsSection.ReloadIfPopulatedAsync still has
        // nothing to re-populate from, honestly, if
        // OpenAttachmentRequested's first subscriber fires while this
        // editor is showing a Requirement.

        // Identity: the requirement's own Identifier/Id/Revision, plus
        // Category (the brief's own fourth named field) — there is no
        // separate UI slot for Category, so it rides along in the same
        // readout the generic Identity section already shows for every
        // Kind. "Name" has no Requirement meaning of its own (no
        // Rename command exists for this Kind — RequirementsWorkspaceRegistration's
        // own remarks — Statement is the mutable field), so the Name box
        // honestly shows the Identifier, disabled by the identical
        // `_manager.CanRename` read every other Kind already uses.
        var categoryText = requirement.Category ?? "(none)";
        _identityReadout.Text = $"{requirement.Identifier}  •  Id: {requirement.Id}  •  Revision {requirement.RevisionNumber}  •  Category: {categoryText}";

        _originalName = requirement.Identifier;
        _nameBox.Text = _originalName;
        _nameBox.IsEnabled = _manager.CanRename(_objectKind);

        // Content: the requirement's own Statement — revised through the
        // identical IWorkspaceManager.ReviseObjectAsync -> ReviseRequirementCommand
        // path OnSaveAsync already dispatches by Kind (CanRevise("Requirement")
        // is true; RequirementsWorkspaceRegistration registers a Revise
        // factory, no Rename factory, for this Kind).
        _originalContent = requirement.Statement;
        _contentBox.Text = _originalContent;
        _contentBox.IsEnabled = _manager.CanRevise(_objectKind);
        _contentSection.IsVisible = _manager.CanRevise(_objectKind) || !string.IsNullOrEmpty(_originalContent);

        // Every section but Identity/Content is still called unconditionally
        // here too, with a null subject (`WP 21.1B`) — the same uniform
        // discipline PopulateFromAsync follows. Only Owner/Priority
        // (already real, `WP 10.7A`, reading through _requirementsService
        // with no target of its own — the section TryCreate's own gate made
        // unreachable before that fix) and Relationships (both "allocated
        // to"/LinkRequirementCommand and "verified by"/VerificationService.RecordAsync
        // are recorded as outgoing references from the requirement itself,
        // so the requirement register's own GetRelationshipsAsync alone
        // covers both) and Lifecycle/Validation (a Requirement implements
        // neither IHasLifecycle nor IValidatable — genuinely true, not
        // merely untested — so both show the identical honest fallback
        // text their own LoadAsync already shows for any Kind that fails
        // the same type-check) do real work for a null subject; every
        // other section's own AppliesTo(null) is false, so calling it is a
        // pure no-op that only reconfirms its own already-collapsed
        // Build()-time default (IsVisible = false) — identical, section by
        // section, to this method's own pre-split hand-picked four calls.
        foreach (var section in _sections)
            await section.LoadAsync(null, CancellationToken.None).ConfigureAwait(true);

        _isDirty = false;
        _statusMessage.Text = string.Empty;
        ApplyReadOnlyState();

        _suppressDirtyTracking = false;
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
    /// The Kinds a Bill-of-Materials line means something for. Every canonical
    /// object implements <see cref="IHasBomLine"/> (ADR-0075's facet plumbing),
    /// which is why the first Windows review of `v0.17.0` saw Quantity, Find
    /// Number and Reference Designator on a Project and on a Calculation.
    /// The editor shows the section only where a person would expect it
    /// (`WP 17.9.1`); the facet itself is untouched. Stays on the shell
    /// (rather than moving to <see cref="Sections.BillOfMaterialsSection"/>
    /// itself) because <c>ObjectEditorViewTests</c> reads it by this exact
    /// name, `ObjectEditorView.BomKinds`.
    /// </summary>
    internal static readonly HashSet<string> BomKinds = new(StringComparer.Ordinal)
    {
        MechanicalObjectFactoryRegistry.Assembly, MechanicalObjectFactoryRegistry.SubAssembly, MechanicalObjectFactoryRegistry.Part, MechanicalObjectFactoryRegistry.Component, MechanicalObjectFactoryRegistry.Configuration,
    };

    /// <summary>
    /// Delegates to <see cref="AttachmentsSection.AttachFilesAsync"/> —
    /// kept here, under its exact original name and signature, purely
    /// because <c>AttachmentsSectionTests</c> calls
    /// <c>editor.AttachFilesAsync(...)</c> directly as an <c>internal</c>
    /// member (`InternalsVisibleTo`); see <see cref="Sections.AttachmentsSection"/>'s
    /// own remarks (`WP 21.1B`).
    /// </summary>
    internal Task AttachFilesAsync(IReadOnlyList<string> paths) => _attachmentsSection.AttachFilesAsync(paths);

    /// <summary>
    /// Delegates to <see cref="AttachmentsSection.LoadAsync"/> — kept here,
    /// under its exact original name and signature, purely because
    /// <c>AttachmentsSectionTests.ObjectThatIsNotIHasAttachments_ShowsNoAttachmentsSection</c>
    /// finds and invokes this by name through reflection (no live Kind
    /// reachable through the real Repository implements nothing but
    /// <see cref="IEngineeringObject"/>, so that test drives the gate's own
    /// negative branch with a bare stand-in instead); see
    /// <see cref="Sections.AttachmentsSection"/>'s own remarks (`WP 21.1B`).
    /// </summary>
    private Task PopulateAttachmentsAsync(IEngineeringObject target) => _attachmentsSection.LoadAsync(target, CancellationToken.None);

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
