using Avalonia.Controls;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Requirements;
using Tempest.Core.Audit;
using Tempest.Workspace;
using Tempest.Workspace.Calculations;
using Tempest.Workspace.Editors;

namespace Tempest.Desktop.Editors;

/// <summary>
/// One of <see cref="ObjectEditorView"/>'s own named sections (`WP 21.1B`,
/// `TD-109`'s own closure precedent, `WP 19.2A`) — the contract every
/// section file under <c>Editors/Sections/</c> implements, so the shell
/// (<see cref="ObjectEditorView"/>) never names a section's own fields or
/// controls directly. A section owns its own controls, its own automation
/// names, its own save path and its own reaction to a rename/revise/write —
/// exactly the same code that lived inline in <see cref="ObjectEditorView"/>
/// before this split, moved verbatim.
/// </summary>
/// <remarks>
/// <para>
/// <b>Build always runs; LoadAsync always runs too, unconditionally, for
/// every registered section, on every populate cycle</b> — exactly as the
/// pre-split <c>ObjectEditorView</c> called every one of its own
/// <c>Populate*</c> methods unconditionally on every populate cycle,
/// leaving each method to decide, internally, whether it applies. This
/// split changes nothing about that: <see cref="AppliesTo"/> is each
/// section's own faithful, self-contained restatement of the exact
/// condition its own <see cref="LoadAsync"/> already used to decide
/// visibility (Kind, a real object's own type, and — for a
/// declaration-gated section — <see cref="EditorSectionContext.Declarations"/>),
/// not a shell-side filter the shell uses to skip calling a section at
/// all. Skipping <see cref="Build"/> or <see cref="LoadAsync"/> based on
/// <see cref="AppliesTo"/> would remove that section's own <see cref="Expander"/>
/// from the logical tree entirely for an inapplicable Kind — a real,
/// detectable behaviour change from the pre-split shell, which always
/// built and always populated (into a collapsed, hidden Expander) every
/// section for every Kind. <see cref="EditorAutomationTreeWalker"/>'s own
/// golden trees (`tests/Tempest.Desktop.Tests/Editors/Golden/*.txt`) exist
/// to catch exactly this class of regression.
/// </para>
/// <para>
/// <b><see cref="EditorSectionContext"/> flows in once, through <see cref="Build"/>,
/// not through a constructor.</b> The shell constructs every section with
/// its own parameterless constructor (<c>EditorSections.All</c>), then
/// calls <see cref="Build"/> exactly once, synchronously, before the
/// editor's own constructor returns (mirroring the pre-split shell's own
/// single <c>BuildLayout()</c> call) — a section stores <paramref name="ctx"/>
/// (loosely, "ctx" throughout this contract's own implementers) in a
/// private field there, for <see cref="AppliesTo"/>/<see cref="LoadAsync"/>/
/// <see cref="React"/> to read afterwards; none of those three is ever
/// called before <see cref="Build"/>.
/// </para>
/// </remarks>
public interface IEditorSection
{
    /// <summary>This section's own heading — identical to the <see cref="HeaderedContentControl.Header"/> text of the <see cref="Expander"/> <see cref="Build"/> returns.</summary>
    string Title { get; }

    /// <summary>
    /// Whether this section is relevant for <paramref name="subject"/> — the
    /// real object <see cref="ObjectEditorView"/> is showing, or
    /// <see langword="null"/> in the Requirement-only population path
    /// (`TD-41`, `WP 19.10I`; no real <see cref="IEngineeringObject"/> ever
    /// backs a Requirement). The exact condition this section's own
    /// pre-split <c>Populate*</c> method used to decide its own
    /// <see cref="Expander.IsVisible"/> — see this interface's own remarks
    /// for why the shell never uses this to skip <see cref="Build"/> or
    /// <see cref="LoadAsync"/>.
    /// </summary>
    bool AppliesTo(IEngineeringObject? subject);

    /// <summary>
    /// Builds this section's own <see cref="Expander"/> — its controls,
    /// its automation names, its event wiring — exactly once, called by the
    /// shell while composing <see cref="ObjectEditorView"/>'s own layout.
    /// Captures <paramref name="ctx"/> for <see cref="AppliesTo"/>/
    /// <see cref="LoadAsync"/>/<see cref="React"/> to use afterwards (this
    /// interface's own remarks).
    /// </summary>
    Control Build(EditorSectionContext ctx);

    /// <summary>
    /// (Re)populates this section from <paramref name="subject"/> — called
    /// unconditionally by the shell on every populate cycle (construction,
    /// and every <see cref="ObjectEditorView.RefreshAsync"/>), exactly as
    /// the pre-split shell called every <c>Populate*</c> method
    /// unconditionally. Sets this section's own <see cref="Expander.IsVisible"/>
    /// from <see cref="AppliesTo"/> and returns early when it is
    /// <see langword="false"/> — the identical "check and return" shape
    /// every pre-split <c>Populate*</c> method already used.
    /// </summary>
    Task LoadAsync(IEngineeringObject? subject, CancellationToken ct);

    /// <summary>
    /// Reserved for a section's own targeted reaction to one
    /// <see cref="WorkspaceChange"/>, without a full <see cref="LoadAsync"/>
    /// re-populate. Unused by this Work Package's own shell, which still
    /// reloads via one full <see cref="ObjectEditorView.RefreshAsync"/> on
    /// any change touching the object it shows — exactly as the pre-split
    /// <c>OnWorkspaceChanged</c> already did — so every implementation here
    /// is an empty, inert method; changing that shell behaviour is outside
    /// this Work Package's own scope (brief-21.1B.md's own Scope item 1
    /// names the change feed as something the shell keeps, not something
    /// this split changes the shape of).
    /// </summary>
    void React(WorkspaceChange change);
}

/// <summary>
/// What every <see cref="IEditorSection"/> needs from <see cref="ObjectEditorView"/>
/// (`WP 21.1B`) — the domain/command/navigation seams every section already
/// closed over as private fields before this split, plus two callbacks
/// (<see cref="RefreshAsync"/>, <see cref="ReportAction"/>) standing in for
/// what a section used to reach directly on <c>this</c>
/// (<c>ObjectEditorView.RefreshAsync</c>/<c>ActionCompleted?.Invoke</c>).
/// Built once per editor, alongside <c>EditorSections.All(ctx)</c>, and
/// handed to every section's own <see cref="IEditorSection.Build"/>.
/// </summary>
/// <param name="ObjectId">The real object's own id (or the Requirement's own id, in the Requirement-only path) — <see cref="ObjectEditorView"/>'s own <c>_objectId</c>.</param>
/// <param name="ObjectKind">The canonical Kind this editor was opened for — <see cref="ObjectEditorView"/>'s own <c>_objectKind</c>; several sections gate on this directly rather than on <paramref name="subject"><c>subject</c></paramref>'s own runtime type (Owner/Priority, Calculation Due, the Calculation pointer — none of these has a type-level test to gate on).</param>
/// <param name="DomainContext">The real Engineering Domain read surface (`ADR-0063`) — relationship/where-used/object-reference reads.</param>
/// <param name="Manager">Reserved for parity with the shell's own constructor parameter list; no section reads this directly today (Rename/Revise gating stays on the shell's own Identity/Content, per that pair's own kill-switch remarks) — carried here so a future section never has to widen this record to reach it.</param>
/// <param name="CommandDispatcher">Dispatches every section's own write.</param>
/// <param name="NavigateToObject">Opens another object right up — a relationship row's or an object-reference row's own "Open".</param>
/// <param name="RequirementsService">Non-<see langword="null"/> only when a Requirement could be behind this editor — Owner/Priority and the Requirement-flow half of Relationships.</param>
/// <param name="CalculationTemplates">Non-<see langword="null"/> only when Execute/Recalculate has a real registry to read templates from.</param>
/// <param name="Declarations">The declaration-per-Kind registry (`WP 18.2A`) — Bill of Materials/Description/Where used/Commercial/Invoice Lines/Invoicing external/Quotation Lines all gate on this.</param>
/// <param name="EvidenceSupport">Non-<see langword="null"/> only when Evidence's own pickers/file picker are wired — also gates the Attachments section's own "Browse…" affordance for every Kind, not Evidence alone.</param>
/// <param name="AuditQuery">Non-<see langword="null"/> only when Evidence's own Audit section has a real read surface.</param>
/// <param name="CommercialSupport">Non-<see langword="null"/> only when the project Commercial section's own pickers/name resolvers are wired.</param>
/// <param name="OwnerSupport">Non-<see langword="null"/> only when the Owner drop-down's own People catalogue/Add-person prompt are wired.</param>
/// <param name="RefreshAsync">Re-reads the real object and reloads every section — a section calls this after every successful write, exactly as the pre-split shell's own <c>Populate*</c> methods called <c>ObjectEditorView.RefreshAsync</c> directly.</param>
/// <param name="ReportAction">Reports one action's own outcome — a section calls this exactly where its own pre-split method invoked <c>ActionCompleted</c> directly.</param>
public sealed record EditorSectionContext(
    Guid ObjectId,
    string ObjectKind,
    EngineeringDomainContext DomainContext,
    IWorkspaceManager Manager,
    ICommandDispatcher CommandDispatcher,
    Action<Guid, string> NavigateToObject,
    IRequirementsService? RequirementsService,
    CalculationTemplateRegistry? CalculationTemplates,
    IKindEditorDeclarationRegistry? Declarations,
    EvidenceEditorSupport? EvidenceSupport,
    IAuditQuery? AuditQuery,
    ProjectCommercialEditorSupport? CommercialSupport,
    RequirementOwnerEditorSupport? OwnerSupport,
    Func<Task> RefreshAsync,
    Action<string, ActionOutcome> ReportAction);
