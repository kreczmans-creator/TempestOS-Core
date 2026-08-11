# ADR-0098: Undo/Redo Is a Desktop-Local `UndoableAction` Delegate Stack, Not a New `ICommand` Contract

## Status

Accepted — `v0.10.0` "User Experience & Desktop Application", `WP 10.6A` (Command Execution & Productivity Experience), 2026-08-10.

## Context

`WP 10.6A`'s own controlling instruction requires a real "Undo/Redo
architecture." The obvious first design — an `IUndoableCommand : ICommand`
contract, with a method returning the command's own inverse — was
considered first, and rejected by direct inspection of how a real command
is actually shaped in this platform: `RenameMechanicalObjectCommand`'s own
constructor carries `(targetObjectId, targetKind, newDisplayName)` only —
no "old name." Every discipline's own Rename/Status/Priority/Owner command
is identical in this respect (confirmed by direct read of all such
commands across all six disciplines): a command is forward-facing data
only, supplied by whichever caller already decided what the new state
should be. Inverting a command safely needs the *previous* state, which
the command itself never carries and, by `ADR-0063`'s own "commands are
data" design, never should — that would conflate a command with an audit
record.

The data needed to invert an action is, however, already sitting in the
UI call site that is about to perform it: `ObjectEditorView`'s own commit
path already reads `_originalName` before ever dispatching a Rename.
`IWorkspaceManager.RenameObjectAsync(id, kind, newName)` is also already
Kind-agnostic (`ADR-0096`) — the identical method, called with the old
name instead of the new one, *is* the correct undo operation, for any of
the six disciplines, with no per-Kind logic at all.

## Decision

**Undo/Redo is realised as a plain, additive Desktop/App-layer type,
`UndoableAction(string Description, Func<CancellationToken,
Task<CommandResult>> Undo, Func<CancellationToken, Task<CommandResult>>
Redo)`** (`Tempest.App.Workspace`) — a Do/Undo delegate pair a call site
builds from data it already has, mirroring this platform's own
established "small, additive delegate bundle" pattern
(`RibbonView.ObjectCreationHandlers`, `MainWindow.ConfirmDeleteAsync`)
rather than a new Command Framework contract. `IUndoRedoStack`/
`UndoRedoStack` (two bounded stacks, capacity 50, session-only — never
persisted across a restart) records these actions and reverses/re-applies
the most recent one on request.

**Wired real, broadly**: `ObjectEditorView`'s own Rename commit path
(shared by all six disciplines) captures the pre-commit name and records
`Undo: RenameObjectAsync(id, kind, oldName)` / `Redo:
RenameObjectAsync(id, kind, newName)` — one call site, six disciplines,
zero per-discipline special-casing, since it reuses `ADR-0096`'s own
Kind-agnostic dispatch exactly as designed. **Wired real, second
example**: the new Favourite/Un-favourite toggle (`WP 10.6A`) — a purely
local state mutation, trivially self-inverting (toggling twice is a
no-op), so `Undo`/`Redo` share one identical delegate.

**Not wired**: Create/Delete/Duplicate/Move, and every Set-Status/
Set-Priority/Set-Owner command across every discipline. Delete is already
a soft delete (`IDeletable.DeleteAsync` sets `IsDeleted`, `WP10.5B
Implementation Report.md` §10) with no "restore" operation anywhere in
this platform to invert into; Create's own correct inverse semantics
(delete-then-purge? soft-delete-and-hide?) are undefined; Move/Duplicate/
Status changes would each need their own captured pre-state at their own
call sites, none of which this Work Package's own time budget reached.
Disclosed directly (`Technical Debt Register.md`, new `AT` entry;
`WP10.6A Implementation Report.md` §8) — real, bounded future work, not a
silently narrowed claim.

## Consequences

**Positive:**

- Zero changes to `Tempest.Core.Commands` — `ICommand`/`ICommandDispatcher`/
  `ICommandRegistry`/`ICommandHandler<T>` are all untouched; Undo/Redo is
  entirely additive, entirely at the Desktop/App layer.
- Rename Undo/Redo works identically across all six disciplines from one
  wired call site, for free — a direct consequence of reusing `ADR-0096`'s
  own already-Kind-agnostic dispatch rather than building a parallel,
  per-Kind inversion mechanism.
- `UndoableAction` treats a real Command dispatch (Rename) and a plain
  local state mutation (Favourite toggle) identically — the stack itself
  never needs to know which kind of action it is holding.

**Negative:**

- Only two kinds of action are genuinely undoable today — a real,
  disclosed scope boundary, not a defect; every other mutating command
  in this platform remains dispatched exactly as before, simply without
  an Undo entry.
- Undo/Redo history is session-only — closing and reopening the
  application loses it, matching most desktop applications' own
  established convention, but a real, disclosed limitation nonetheless.

## Alternatives Considered

**`IUndoableCommand : ICommand` with a `CreateInverse()` method**, added
to the Command Framework itself. Rejected — see Context: no real
command in this platform carries the previous-state data its own
inversion would need; adding the method would either force every command
constructor to carry state it does not otherwise need (a Command
Framework-wide change well beyond this Work Package's own scope) or
return `null` from every real command that exists today, making the
contract dead weight from day one.

**A generic "command interceptor" wrapping `ICommandDispatcher` to
record every dispatch automatically.** Considered and rejected — still
cannot solve the "commands don't carry prior state" problem (an
interceptor sees the same forward-only data the command itself carries),
and would touch the Command Framework's own dispatch pipeline, which
`ADR-0037` establishes as deliberately minimal and not a general
extension point.

## Related Documents

`ADR-0036`; `ADR-0037`; `ADR-0063`; `ADR-0096`; `Technical Debt
Register.md`; `src/Tempest.App/Workspace/UndoableAction.cs`;
`src/Tempest.App/Workspace/IUndoRedoStack.cs`;
`src/Tempest.App/Workspace/UndoRedoStack.cs`;
`src/Tempest.Desktop/Editors/ObjectEditorView.cs`;
`src/Tempest.Desktop/MainWindow.cs`;
`docs/releases/v0.10.0/WP10.6A Implementation Report.md`.
