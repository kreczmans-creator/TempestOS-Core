using Tempest.App.Workspace;
using Tempest.App.Workspace.Calculations;
using Tempest.App.Workspace.Documents;
using Tempest.App.Workspace.Manufacturing;
using Tempest.App.Workspace.Mechanical;
using Tempest.App.Workspace.Requirements;
using Tempest.App.Workspace.Verification;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Requirements;
using Tempest.Core.Verification;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Composition;

/// <summary>
/// Wires every real Ribbon object-action handler — Create/Duplicate/
/// status-transition, per discipline — into <see cref="RibbonView.ObjectCreationHandlers"/>
/// — extracted, `WP 12.0B` (`ADR-0103`), from <see cref="MainWindow"/>'s
/// own previous ~450-line handler-population block, unmodified in
/// behaviour (`WP 10.5B`'s Mechanical Create/Duplicate; `WP 10.7A`'s
/// Feature Completion pass across the other five disciplines). A
/// collaborator under `ADR-0103`: constructed once by
/// <see cref="MainWindow"/> (the composition root), declaring only the
/// dependencies it actually needs, never DI-registered, never
/// referencing <see cref="MainWindow"/> or any sibling collaborator
/// back — the single largest, most mechanical extraction (~29% of the
/// pre-decomposition source file).
/// </summary>
internal sealed class RibbonObjectActionHandlers
{
    /// <summary>Initialises a new instance of the <see cref="RibbonObjectActionHandlers"/> class, populating every handler onto <paramref name="ribbon"/>'s own <see cref="RibbonView.ObjectCreationHandlers"/> dictionary.</summary>
    public RibbonObjectActionHandlers(
        RibbonView ribbon, IWorkspace workspace, ICommandDispatcher commandDispatcher, StatusBarView statusBar, ToastHost toastHost,
        ProjectExplorerView explorerView, CockpitView cockpitView, ConfirmationDialog confirmationDialog, InputDialog inputDialog)
    {
        ArgumentNullException.ThrowIfNull(ribbon);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(statusBar);
        ArgumentNullException.ThrowIfNull(toastHost);
        ArgumentNullException.ThrowIfNull(explorerView);
        ArgumentNullException.ThrowIfNull(cockpitView);
        ArgumentNullException.ThrowIfNull(confirmationDialog);
        ArgumentNullException.ThrowIfNull(inputDialog);

        // A real, working "Create Object" flow (`WP 10.5B`, Dialog
        // Framework/"object creation experience") — honestly scoped to
        // Mechanical only (`CreateMechanicalObjectCommand`'s own
        // constructor shape is the simplest Ribbon-Create-friendly one of
        // the eight real Create commands across six disciplines; the
        // other seven have genuinely different constructor shapes —
        // Requirements alone has three — extending this to all of them is
        // real, disclosed future work, `WP10.5B Implementation Report.md`
        // §8/`FCR`). Defaults every new object to Kind `"Part"` — the
        // most common creation — rather than offering a Kind picker
        // `InputDialog`'s own single-field shape cannot collect.
        ribbon.ObjectCreationHandlers["mechanical.create"] = async () =>
        {
            var name = await inputDialog.PromptAsync(
                "Create Part",
                "Name for the new Part:",
                validate: value => value.Length > 200 ? "Name is too long (200 characters max)." : null).ConfigureAwait(true);
            if (name is null)
                return;

            var result = await commandDispatcher.DispatchAsync(new CreateMechanicalObjectCommand("Part", name), CancellationToken.None).ConfigureAwait(true);
            statusBar.SetText(result.Succeeded ? $"Created Part '{name}'." : result.Message ?? "Create failed.");
            toastHost.Show(result.Succeeded ? $"Created Part '{name}'." : result.Message ?? "Create failed.", result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded)
            {
                await explorerView.LoadAsync().ConfigureAwait(true);
                cockpitView.Refresh();
            }
        };

        // A real "Duplicate workflow" (`WP 10.5B` scope) — genuinely
        // simpler than Create: `DuplicateMechanicalObjectCommand` needs
        // only the already-selected object's own Id/Kind, no additional
        // input to collect, so a plain confirmation (never an
        // `InputDialog`) is the complete, honest interaction.
        ribbon.ObjectCreationHandlers["mechanical.duplicate"] = async () =>
        {
            var selection = workspace.Selection.Current;
            if (selection is null)
            {
                statusBar.SetText("Select an object to duplicate first.");
                return;
            }

            var confirmed = await confirmationDialog.ConfirmAsync(
                "Duplicate?",
                $"Create a duplicate of the selected {selection.Kind}?",
                "Duplicate").ConfigureAwait(true);
            if (!confirmed)
                return;

            var result = await commandDispatcher.DispatchAsync(new DuplicateMechanicalObjectCommand(selection.ObjectId, selection.Kind), CancellationToken.None).ConfigureAwait(true);
            statusBar.SetText(result.Succeeded ? $"Duplicated the selected {selection.Kind}." : result.Message ?? "Duplicate failed.");
            toastHost.Show(result.Succeeded ? $"Duplicated the selected {selection.Kind}." : result.Message ?? "Duplicate failed.", result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded)
            {
                await explorerView.LoadAsync().ConfigureAwait(true);
                cockpitView.Refresh();
            }
        };

        // WP 10.7A — Feature Completion. Closes the WP10.6D-audited gap
        // where every Ribbon lifecycle/organize button beyond Mechanical's
        // own Create/Duplicate fell through to the honest-but-permanent
        // "needs additional input this ribbon does not yet collect"
        // message, even though every command dispatched below already
        // exists and is already registered — only Mechanical had a real
        // Ribbon handler wired before this Work Package. Every handler
        // below follows the identical four-step shape the two Mechanical
        // handlers above already established: confirm/prompt if needed,
        // dispatch the already-registered command via commandDispatcher,
        // report via StatusBar+Toast, refresh Explorer+Cockpit on success.

        // A shared factory for every discipline's own Approve/Archive/
        // Lock/Unlock/Request-Review/Release button — Calculations/
        // Documents/Verification/Manufacturing's own Set{X}StatusCommand
        // all share the identical (Guid, string, LifecycleState) shape.
        Func<string, LifecycleState, Func<Guid, string, LifecycleState, IWorkspaceCommand>, Func<Task>> statusHandler = (verbLabel, status, factory) => async () =>
        {
            var selection = workspace.Selection.Current;
            if (selection is null)
            {
                statusBar.SetText($"Select an object to {verbLabel.ToLowerInvariant()} first.");
                return;
            }

            var result = await commandDispatcher.DispatchAsync(factory(selection.ObjectId, selection.Kind, status), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"'{verbLabel}' applied to the selected {selection.Kind}." : result.Message ?? $"'{verbLabel}' failed.";
            statusBar.SetText(message);
            toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded)
            {
                await explorerView.LoadAsync().ConfigureAwait(true);
                cockpitView.Refresh();
            }
        };

        ribbon.ObjectCreationHandlers["calculations.lock"] = statusHandler("Lock", LifecycleState.Approved, static (id, kind, status) => new SetCalculationStatusCommand(id, kind, status));
        ribbon.ObjectCreationHandlers["calculations.unlock"] = statusHandler("Unlock", LifecycleState.Draft, static (id, kind, status) => new SetCalculationStatusCommand(id, kind, status));
        ribbon.ObjectCreationHandlers["calculations.request-review"] = statusHandler("Request Review", LifecycleState.InReview, static (id, kind, status) => new SetCalculationStatusCommand(id, kind, status));
        ribbon.ObjectCreationHandlers["calculations.approve"] = statusHandler("Approve", LifecycleState.Approved, static (id, kind, status) => new SetCalculationStatusCommand(id, kind, status));
        ribbon.ObjectCreationHandlers["calculations.archive"] = statusHandler("Archive", LifecycleState.Archived, static (id, kind, status) => new SetCalculationStatusCommand(id, kind, status));

        ribbon.ObjectCreationHandlers["documents.request-review"] = statusHandler("Request Review", LifecycleState.InReview, static (id, kind, status) => new SetDocumentStatusCommand(id, kind, status));
        ribbon.ObjectCreationHandlers["documents.approve"] = statusHandler("Approve", LifecycleState.Approved, static (id, kind, status) => new SetDocumentStatusCommand(id, kind, status));
        ribbon.ObjectCreationHandlers["documents.release"] = statusHandler("Release", LifecycleState.Released, static (id, kind, status) => new SetDocumentStatusCommand(id, kind, status));

        ribbon.ObjectCreationHandlers["verification.request-review"] = statusHandler("Request Review", LifecycleState.InReview, static (id, kind, status) => new SetVerificationActivityStatusCommand(id, kind, status));
        ribbon.ObjectCreationHandlers["verification.approve"] = statusHandler("Approve", LifecycleState.Approved, static (id, kind, status) => new SetVerificationActivityStatusCommand(id, kind, status));
        ribbon.ObjectCreationHandlers["verification.archive"] = statusHandler("Archive", LifecycleState.Archived, static (id, kind, status) => new SetVerificationActivityStatusCommand(id, kind, status));

        ribbon.ObjectCreationHandlers["manufacturing.release"] = statusHandler("Release", LifecycleState.Released, static (id, kind, status) => new SetManufacturingObjectStatusCommand(id, kind, status));
        ribbon.ObjectCreationHandlers["manufacturing.archive"] = statusHandler("Archive", LifecycleState.Archived, static (id, kind, status) => new SetManufacturingObjectStatusCommand(id, kind, status));

        // Requirements' own SetRequirementStatusCommand has a genuinely
        // different shape (RequirementStatus, not LifecycleState; no
        // targetKind parameter) — its own dedicated handler, not the
        // shared factory above. No status picker control exists, so a
        // validated free-text prompt (mirrors Create's own length-
        // validated prompt) is the honest minimum interaction.
        ribbon.ObjectCreationHandlers["requirements.set-status"] = async () =>
        {
            var selection = workspace.Selection.Current;
            if (selection is null) { statusBar.SetText("Select a Requirement to set status on first."); return; }

            var validStatuses = string.Join(", ", Enum.GetNames<RequirementStatus>());
            var statusText = await inputDialog.PromptAsync(
                "Set Requirement Status",
                $"New status ({validStatuses}):",
                validate: value => Enum.TryParse<RequirementStatus>(value, ignoreCase: true, out _) ? null : $"Must be one of: {validStatuses}.").ConfigureAwait(true);
            if (statusText is null) return;

            var status = Enum.Parse<RequirementStatus>(statusText, ignoreCase: true);
            var result = await commandDispatcher.DispatchAsync(new SetRequirementStatusCommand(selection.ObjectId, status), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Requirement status set to {status}." : result.Message ?? "Set status failed.";
            statusBar.SetText(message);
            toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await explorerView.LoadAsync().ConfigureAwait(true); cockpitView.Refresh(); }
        };

        ribbon.ObjectCreationHandlers["requirements.set-owner"] = async () =>
        {
            var selection = workspace.Selection.Current;
            if (selection is null) { statusBar.SetText("Select a Requirement to set an owner on first."); return; }

            var owner = await inputDialog.PromptAsync("Set Requirement Owner", "Owner:").ConfigureAwait(true);
            if (owner is null) return;

            var result = await commandDispatcher.DispatchAsync(new SetRequirementOwnerCommand(selection.ObjectId, owner), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Owner set to '{owner}'." : result.Message ?? "Set owner failed.";
            statusBar.SetText(message);
            toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await explorerView.LoadAsync().ConfigureAwait(true); cockpitView.Refresh(); }
        };

        ribbon.ObjectCreationHandlers["requirements.set-priority"] = async () =>
        {
            var selection = workspace.Selection.Current;
            if (selection is null) { statusBar.SetText("Select a Requirement to set a priority on first."); return; }

            var validPriorities = string.Join(", ", Enum.GetNames<RequirementPriority>());
            var priorityText = await inputDialog.PromptAsync(
                "Set Requirement Priority",
                $"Priority ({validPriorities}):",
                validate: value => Enum.TryParse<RequirementPriority>(value, ignoreCase: true, out _) ? null : $"Must be one of: {validPriorities}.").ConfigureAwait(true);
            if (priorityText is null) return;

            var priority = Enum.Parse<RequirementPriority>(priorityText, ignoreCase: true);
            var result = await commandDispatcher.DispatchAsync(new SetRequirementPriorityCommand(selection.ObjectId, priority), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Priority set to {priority}." : result.Message ?? "Set priority failed.";
            statusBar.SetText(message);
            toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await explorerView.LoadAsync().ConfigureAwait(true); cockpitView.Refresh(); }
        };

        // A shared factory for every discipline's own Duplicate button —
        // mirrors "mechanical.duplicate" above exactly; Calculations/
        // Documents/Verification/Manufacturing's own Duplicate{X}Command
        // all need only the selected object's own Id/Kind (an optional
        // newIdentifier parameter on Calculations/Documents, left null,
        // exactly like Mechanical's own).
        Func<Func<Guid, string, IWorkspaceCommand>, Func<Task>> duplicateHandler = factory => async () =>
        {
            var selection = workspace.Selection.Current;
            if (selection is null) { statusBar.SetText("Select an object to duplicate first."); return; }

            var confirmed = await confirmationDialog.ConfirmAsync("Duplicate?", $"Create a duplicate of the selected {selection.Kind}?", "Duplicate").ConfigureAwait(true);
            if (!confirmed) return;

            var result = await commandDispatcher.DispatchAsync(factory(selection.ObjectId, selection.Kind), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Duplicated the selected {selection.Kind}." : result.Message ?? "Duplicate failed.";
            statusBar.SetText(message);
            toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await explorerView.LoadAsync().ConfigureAwait(true); cockpitView.Refresh(); }
        };

        ribbon.ObjectCreationHandlers["calculations.duplicate"] = duplicateHandler(static (id, kind) => new DuplicateCalculationObjectCommand(id, kind));
        ribbon.ObjectCreationHandlers["documents.duplicate"] = duplicateHandler(static (id, kind) => new DuplicateDocumentObjectCommand(id, kind));
        ribbon.ObjectCreationHandlers["verification.duplicate"] = duplicateHandler(static (id, kind) => new DuplicateVerificationActivityCommand(id, kind));
        ribbon.ObjectCreationHandlers["manufacturing.duplicate"] = duplicateHandler(static (id, kind) => new DuplicateManufacturingObjectCommand(id, kind));

        // Requirements' own DuplicateRequirementCommand requires a new
        // identifier (not optional, unlike every other discipline's own
        // Duplicate command) — its own dedicated handler.
        ribbon.ObjectCreationHandlers["requirements.duplicate"] = async () =>
        {
            var selection = workspace.Selection.Current;
            if (selection is null) { statusBar.SetText("Select a Requirement to duplicate first."); return; }

            var newIdentifier = await inputDialog.PromptAsync(
                "Duplicate Requirement",
                "New identifier for the duplicate:",
                validate: value => string.IsNullOrWhiteSpace(value) ? "An identifier is required." : null).ConfigureAwait(true);
            if (newIdentifier is null) return;

            var result = await commandDispatcher.DispatchAsync(new DuplicateRequirementCommand(selection.ObjectId, newIdentifier), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Duplicated as '{newIdentifier}'." : result.Message ?? "Duplicate failed.";
            statusBar.SetText(message);
            toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await explorerView.LoadAsync().ConfigureAwait(true); cockpitView.Refresh(); }
        };

        // Create — the four disciplines with one Create{X}ObjectCommand
        // needing only a name default every other optional constructor
        // parameter, mirroring Mechanical's own "defaults to Kind Part"
        // minimal-viable precedent.
        ribbon.ObjectCreationHandlers["calculations.create"] = async () =>
        {
            var name = await inputDialog.PromptAsync("Create Calculation", "Name for the new Calculation:", validate: value => value.Length > 200 ? "Name is too long (200 characters max)." : null).ConfigureAwait(true);
            if (name is null) return;

            var result = await commandDispatcher.DispatchAsync(new CreateCalculationObjectCommand("Calculation", name), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Created Calculation '{name}'." : result.Message ?? "Create failed.";
            statusBar.SetText(message);
            toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await explorerView.LoadAsync().ConfigureAwait(true); cockpitView.Refresh(); }
        };

        ribbon.ObjectCreationHandlers["documents.create"] = async () =>
        {
            var name = await inputDialog.PromptAsync("Create Document", "Name for the new Document:", validate: value => value.Length > 200 ? "Name is too long (200 characters max)." : null).ConfigureAwait(true);
            if (name is null) return;

            var result = await commandDispatcher.DispatchAsync(new CreateDocumentObjectCommand("Document", name), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Created Document '{name}'." : result.Message ?? "Create failed.";
            statusBar.SetText(message);
            toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await explorerView.LoadAsync().ConfigureAwait(true); cockpitView.Refresh(); }
        };

        ribbon.ObjectCreationHandlers["manufacturing.create"] = async () =>
        {
            var name = await inputDialog.PromptAsync("Create Manufacturing Operation", "Name for the new Manufacturing Operation:", validate: value => value.Length > 200 ? "Name is too long (200 characters max)." : null).ConfigureAwait(true);
            if (name is null) return;

            var result = await commandDispatcher.DispatchAsync(new CreateManufacturingObjectCommand("ManufacturingOperation", name), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Created Manufacturing Operation '{name}'." : result.Message ?? "Create failed.";
            statusBar.SetText(message);
            toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await explorerView.LoadAsync().ConfigureAwait(true); cockpitView.Refresh(); }
        };

        // Verification Create genuinely means "verify the object I have
        // selected" — SubjectId is the current selection's own Id, not a
        // fabricated/default one; Method has no picker anywhere in this
        // platform, defaulted to a fixed, honest "Inspection" (the same
        // word this platform's own Manufacturing "Inspection" Kind
        // already uses for the identical concept).
        ribbon.ObjectCreationHandlers["verification.create"] = async () =>
        {
            var selection = workspace.Selection.Current;
            if (selection is null) { statusBar.SetText("Select the object to verify first."); return; }

            var name = await inputDialog.PromptAsync(
                "Create Verification Activity",
                $"Name for the new Verification Activity (verifying the selected {selection.Kind}):",
                validate: value => value.Length > 200 ? "Name is too long (200 characters max)." : null).ConfigureAwait(true);
            if (name is null) return;

            var result = await commandDispatcher.DispatchAsync(new CreateVerificationActivityCommand(name, selection.ObjectId, "Inspection"), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Created Verification Activity '{name}'." : result.Message ?? "Create failed.";
            statusBar.SetText(message);
            toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await explorerView.LoadAsync().ConfigureAwait(true); cockpitView.Refresh(); }
        };

        // Manufacturing's own "Record Inspection Result" (`WP 10.8A`) —
        // disclosed cross-Work-Package reuse, exactly as
        // ManufacturingWorkspaceRegistration's own remarks already
        // document: dispatches Verification.RecordVerificationResultCommand
        // directly, the identical command/handler the Object Editor's own
        // Verification Record Result section (`WP 10.7A`) already uses —
        // no duplicate command, no duplicate handler. No Outcome-picker
        // control exists at the Ribbon level, so a validated InputDialog
        // prompt (mirroring "requirements.set-status"'s own identical
        // shape) is the honest minimum interaction.
        ribbon.ObjectCreationHandlers["manufacturing.record-inspection-result"] = async () =>
        {
            var selection = workspace.Selection.Current;
            if (selection is null) { statusBar.SetText("Select an Inspection to record a result for first."); return; }

            var validOutcomes = string.Join(", ", Enum.GetNames<VerificationOutcome>());
            var outcomeText = await inputDialog.PromptAsync(
                "Record Inspection Result",
                $"Outcome ({validOutcomes}):",
                validate: value => Enum.TryParse<VerificationOutcome>(value, ignoreCase: true, out _) ? null : $"Must be one of: {validOutcomes}.").ConfigureAwait(true);
            if (outcomeText is null) return;

            var method = await inputDialog.PromptAsync("Record Inspection Result", "Method:", initialValue: "Inspection").ConfigureAwait(true);
            if (method is null) return;

            var outcome = Enum.Parse<VerificationOutcome>(outcomeText, ignoreCase: true);
            var result = await commandDispatcher.DispatchAsync(new RecordVerificationResultCommand(selection.ObjectId, selection.Kind, outcome, method), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Result recorded: {outcome}." : result.Message ?? "Record result failed.";
            statusBar.SetText(message);
            toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await explorerView.LoadAsync().ConfigureAwait(true); cockpitView.Refresh(); }
        };

        // Requirements Create — three distinct commands/descriptors
        // ("requirements.create"/"requirements.create-group"/
        // "requirements.create-collection"), each genuinely different.
        // CreateRequirementCommand needs both an identifier and a
        // statement — two sequential prompts, still InputDialog, no new
        // dialog type.
        ribbon.ObjectCreationHandlers["requirements.create"] = async () =>
        {
            var identifier = await inputDialog.PromptAsync("Create Requirement", "Identifier (e.g. REQ-001):", validate: value => string.IsNullOrWhiteSpace(value) ? "An identifier is required." : null).ConfigureAwait(true);
            if (identifier is null) return;

            var statement = await inputDialog.PromptAsync("Create Requirement", "Statement:", validate: value => string.IsNullOrWhiteSpace(value) ? "A statement is required." : null).ConfigureAwait(true);
            if (statement is null) return;

            var result = await commandDispatcher.DispatchAsync(new CreateRequirementCommand(identifier, statement), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Created Requirement '{identifier}'." : result.Message ?? "Create failed.";
            statusBar.SetText(message);
            toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await explorerView.LoadAsync().ConfigureAwait(true); cockpitView.Refresh(); }
        };

        ribbon.ObjectCreationHandlers["requirements.create-group"] = async () =>
        {
            var name = await inputDialog.PromptAsync("Create Requirement Group", "Name for the new group:", validate: value => string.IsNullOrWhiteSpace(value) ? "A name is required." : null).ConfigureAwait(true);
            if (name is null) return;

            var result = await commandDispatcher.DispatchAsync(new CreateRequirementGroupCommand(name), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Created Requirement Group '{name}'." : result.Message ?? "Create failed.";
            statusBar.SetText(message);
            toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await explorerView.LoadAsync().ConfigureAwait(true); cockpitView.Refresh(); }
        };

        ribbon.ObjectCreationHandlers["requirements.create-collection"] = async () =>
        {
            var name = await inputDialog.PromptAsync("Create Requirement Collection", "Name for the new collection:", validate: value => string.IsNullOrWhiteSpace(value) ? "A name is required." : null).ConfigureAwait(true);
            if (name is null) return;

            var result = await commandDispatcher.DispatchAsync(new CreateRequirementCollectionCommand(name), CancellationToken.None).ConfigureAwait(true);
            var message = result.Succeeded ? $"Created Requirement Collection '{name}'." : result.Message ?? "Create failed.";
            statusBar.SetText(message);
            toastHost.Show(message, result.Succeeded ? FeedbackSeverity.Success : FeedbackSeverity.Error);
            if (result.Succeeded) { await explorerView.LoadAsync().ConfigureAwait(true); cockpitView.Refresh(); }
        };
    }
}
