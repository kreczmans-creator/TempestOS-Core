# Command Execution & Productivity Experience

## 1. Introduction

`WP 10.6A`'s own concept guide — how TempestOS built a real Undo/Redo
architecture, a Background Task Framework, Command History, Recent/
Favourite Objects, a User Command Macro foundation, and a Macro &
Controller Abstraction, all without touching the Command Framework
(`ICommand`/`ICommandDispatcher`/`ICommandRegistry`) or any of the
twelve frozen `WP8.0B` Workspace contracts.

## 2. Purpose

Explains why Undo/Redo is a plain delegate stack rather than a new
Command Framework contract, why a Macro is realised as an ordinary
registered Command rather than its own execution path, and why External
Controller integration is one small interface plus one real (Keyboard)
and one simulated (`StubExternalControllerProvider`) provider, never a
vendor SDK.

## 3. Background

By `WP 10.5B`, `Tempest.Desktop` had a full Dialog Framework, real
window/panel persistence, a graceful-shutdown gate, and a real object-
creation workflow — but no way to undo a mistake, no visibility into
what commands had already run, no background-task tracking, and no way
to bind a command to anything other than the fixed handful of shortcuts
`KeyboardShortcuts` already hard-coded. The Command Framework itself
(`ADR-0036`/`ADR-0037`) had been stable since `WP 4.7` — every real
discipline command dispatches through it unchanged. This Work Package's
own controlling instruction asked for the professional workflow layer
that sits on top: command execution/progress reporting, a background
task framework, Undo/Redo, keyboard productivity, command history,
recent/favourite objects, a macro foundation, and an external controller
abstraction — explicitly ruling out any real vendor SDK, hardware
integration, or new scripting language.

## 4. The Problem

Before this Work Package: no Undo/Redo existed anywhere in this
platform. `EngineeringCockpit.FavouriteProjects` was explicitly disclosed
as "always empty... no `IsFavourite` concept exists anywhere." A
repository-wide `grep` for `createDefault:` confirmed only
`Tempest.Samples` commands were ever registered as invokable-by-Id — no
real discipline command could be macro'd or bound to anything without
first collecting UI context. No background task tracking existed at all.
`KeyboardShortcuts` bound exactly five fixed actions, with no mechanism
for a user, a macro, or any external device to bind an arbitrary command.

## 5. The Design

**Undo/Redo is a plain `UndoableAction` delegate pair, not a Command
Framework contract (`ADR-0098`).** A command's own constructor never
carries the "old" state its own inversion would need —
`RenameMechanicalObjectCommand(id, kind, newName)` has no "old name" —
but the UI call site about to dispatch it already does.
`ObjectEditorView`'s own commit path captures the pre-commit name and
records `Undo: RenameObjectAsync(id, kind, oldName)` / `Redo:
RenameObjectAsync(id, kind, newName)` — one call site, reused across
all six disciplines for free, since `RenameObjectAsync` is already
Kind-agnostic (`ADR-0096`). `IUndoRedoStack` (two bounded stacks,
capacity 50, session-only) is the whole mechanism; `Tempest.Core.Commands`
is untouched.

**A Macro is a registered Command (`ADR-0099`).** `RunMacroCommand`
carries a `MacroId`; `RunMacroCommandHandler` resolves the macro via
`IMacroManager` and sequentially `ICommandRegistry.InvokeAsync`s each of
its own step Ids, stopping at the first failure. `IMacroManager` registers
one real `CommandDescriptor` per macro (`Id = "macro:{Guid}"`), so the
Command Palette — and any future `IInputBindingProvider` — invokes a
macro through exactly the same path as any other command. The disclosed
constraint this inherits, not introduces: a macro step must itself be
`CreateDefault`-eligible — today, only Sample commands qualify.

**External Controller integration is `IInputBindingProvider`/
`IExternalControllerProvider`/`IInputBindingRegistry` (`ADR-0100`)** —
mirroring `IEventBus`'s own established "many producers, one dispatch
surface, one bad producer isolated" shape (`ADR-0028`). `InputBindingRouter`
routes every registered provider's own `CommandRequested` event to
`ICommandRegistry.InvokeAsync`. `KeyboardCommandBindingProvider` is the
one real implementation — a genuine, working, tested `gesture → Command
Id` map. `StubExternalControllerProvider` (test-only) simulates a
Stream-Deck-shaped device, proving the identical router drives a real
external-controller-shaped provider with zero Command Framework changes
— explicitly never a real vendor SDK, per this Work Package's own
Out-of-Scope.

**Command History, Recent/Favourite Objects, Background Tasks are all
Desktop-local state**, mirroring `UserSettings`'/`WindowUiState`'s own
established `ISettingsProvider`-JSON-DTO pattern. `CommandHistoryLog`
(bounded, session-only) is appended to at every existing `ActionCompleted`
surface `MainWindow` already has — not a global `ICommandDispatcher`
interception. `RecentObjectsState`/`FavouriteObjectsState` (persisted,
capacity-bounded) surface as two new sections in `ProjectExplorerView`,
mirroring its own existing Recent Searches section exactly.
`IBackgroundTaskRunner` tracks a title and coarse state
(Running/Succeeded/Failed/Cancelled) — never a percentage, since no
`ICommandHandler<TCommand>` anywhere in this platform carries an
`IProgress<T>` parameter to report one from. The one real consumer: the
Command Palette routes a Macro's own multi-step invocation through it,
the one genuinely "could take a moment" case in this platform today.
Both new logs surface as two new sections in the existing
`OutputPanelView` — no new dock panel.

## 6. Alternatives Considered

- **`IUndoableCommand : ICommand` with a `CreateInverse()` method** —
  considered, rejected; see `ADR-0098`. No real command in this platform
  carries the prior-state data its own inversion would need.
- **`IMacroManager.RunAsync(macroId)` called directly by each UI
  surface** — considered, rejected; see `ADR-0099`. Would have required
  a separate macro-aware branch in every present and future
  command-invoking surface, contradicting "without changing the Command
  Framework."
- **A hard-coded `enum InputSource` with one router method per case** —
  considered, rejected; see `ADR-0100`. Adding a future input source
  would mean editing the router itself.
- **Building a real Stream Deck plugin now** — rejected outright by this
  Work Package's own explicit Out-of-Scope instruction;
  `StubExternalControllerProvider` is the disclosed substitute.
- **A `CommandHistoryLog` fed by an `ICommandDispatcher` decorator** —
  considered, rejected as too large a change for this Work Package;
  recording at the five existing `ActionCompleted` surfaces was judged
  sufficient real coverage for today's dispatch paths.

## 7. Why This Solution Was Chosen

Every alternative that would have touched the Command Framework itself
(a new command contract, a dispatcher decorator, a hard-coded input
enum) was rejected in favour of additive, Desktop/App-layer mechanisms
that reuse what already exists — `RenameObjectAsync`'s own Kind-agnostic
dispatch, `ICommandRegistry.InvokeAsync`'s own existing entry point, the
`ActionCompleted` event shape every Desktop view already has. The result
is six real, working, tested capabilities that add up to zero changes in
`Tempest.Core.Commands` and zero changes to any frozen Workspace
contract — the same "extend additively, never reopen a frozen shape"
discipline `ADR-0080`/`ADR-0082`/`ADR-0096` each already established,
applied here across a much larger named scope.

## 8. Architectural Principles

- **Invert at the call site, not in the command** — a command is
  forward-facing data only; the data needed to undo it already lives in
  whichever UI call site is about to dispatch it.
- **A macro is data flowing through the existing Command Framework, not
  a second execution path** — the same discipline that keeps this
  platform's Command Framework small and auditable.
- **Isolate one producer's failure from every other** — the identical
  subscriber-isolation shape `IEventBus` already established, reapplied
  to `IInputBindingRegistry`.
- **Reuse an existing, Kind-agnostic dispatch path instead of building a
  parallel one** — Rename Undo/Redo works across all six disciplines
  from a single call site because `RenameObjectAsync` was already
  Kind-agnostic before this Work Package began.

## 9. Benefits

Every future input source — a real Stream Deck, a MIDI controller, a
game controller — needs to implement one small interface and register
once; no Command Framework change, no per-surface special-casing. Every
future Rename across any future discipline gets real Undo/Redo for free.
A Macro's own steps are invoked through the identical, already-tested
path every other command already uses. Command History, Recent Objects,
and Favourites all reuse the exact persistence pattern `UserSettings`
already established, so a future Desktop-local preference has a proven
template to follow.

## 10. Trade-offs

- Undo/Redo covers Rename and Favourite toggle only — Create/Delete/
  Duplicate/Move/Status changes remain un-undoable (`FCR-0078`).
- Background Task progress is coarse state only, never a percentage
  (`FCR-0079`).
- Macro steps are limited to `CreateDefault`-eligible commands — today,
  Sample commands only (`FCR-0080`).
- Command History records only what reaches existing UI surfaces, with
  a disclosed "contains 'fail'" heuristic for success/failure
  (`FCR-0081`).
- Undo/Redo and Command History are session-only, never persisted
  (`FCR-0082`).
- `KeyboardCommandBindingProvider` ships with zero default bindings and
  no remapping UI; no real external controller vendor integration
  exists (`FCR-0083`).

## 11. Common Mistakes

- Assuming a command's own constructor carries enough data to invert
  it — it does not, by design (`ADR-0063`'s own "commands are data,"
  not audit records); capture prior state at the UI call site instead.
- Assuming every registered `CommandDescriptor` can be invoked by Id —
  only those with `CreateDefault` set can; confirmed, before this Work
  Package began, that this is true of no real discipline command today.
- Assuming `ICommandRegistry` can unregister a descriptor — it cannot
  (frozen `ADR-0037` contract); a deleted Macro's own descriptor stays
  registered, and its handler must fail gracefully against it, not
  assume it will simply disappear.
- Routing a background task through `Task.Run` "to make it real" — every
  handler in this platform is already `async`/non-CPU-bound; a second OS
  thread adds Avalonia UI-thread marshalling risk for no real benefit.

## 12. Future Evolution

- Broader Undo/Redo coverage — Create/Delete (once a real "restore"
  capability exists)/Duplicate/Move/Status (`FCR-0078`).
- Real percentage progress reporting, once `ICommandHandler<TCommand>`
  itself is extended with an `IProgress<T>` parameter (`FCR-0079`).
- Extending macro-step eligibility to real discipline commands
  (`FCR-0080`).
- A real `ICommandDispatcher`-level Command History, not a UI-surface
  aggregation (`FCR-0081`).
- Persisted, cross-session Undo/Redo and Command History (`FCR-0082`).
- A keyboard remapping UI, and a real Stream Deck/MIDI/game-controller
  integration once a vendor SDK dependency is commissioned (`FCR-0083`).

## 13. Key Takeaways

A Work Package instruction naming six major, independently-shippable
capabilities is best served by finding, for each one, the smallest real
mechanism that reuses what the platform already has — `RenameObjectAsync`'s
own Kind-agnostic dispatch for Undo/Redo, `ICommandRegistry.InvokeAsync`
for macros, `IEventBus`'s own isolation shape for the input router —
rather than inventing six independent new subsystems. Every one of the
six ships real and tested; every scope boundary is disclosed directly,
in six new Future Capability entries and six new Accepted Trade-offs, not
silently narrowed.

## Related Documents

- `WP10.6A Implementation Report.md`, `WP10.6A Engineering Review.md`,
  `WP10.6A Architecture Review.md`, `WP10.6A Technical Debt Review.md`.
- `ADR-0098`, `ADR-0099`, `ADR-0100`.
- Future Capability Register — `FCR-0078` through `FCR-0083` (all
  Identified here).
- `29-desktop-workflow-and-professional-interaction.md` — the Work
  Package whose own Dialog Framework/`UserSettings` persistence pattern
  this one reused directly for Recent/Favourite Objects.
