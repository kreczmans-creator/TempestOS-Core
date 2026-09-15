# ADR-0099: A Macro Is Realised as a Registered Command (`RunMacroCommand` over `IMacroManager`)

## Status

Accepted — `v0.10.0` "User Experience & Desktop Application", `WP 10.6A` (Command Execution & Productivity Experience), 2026-08-10.

## Context

`WP 10.6A`'s own controlling instruction asks for "User command macros
(foundation)": a user-authored, ordered sequence of existing registered
commands, explicitly "no new scripting language." It also asks for a
Macro & Controller Abstraction such that "every Tempest command can later
be bound to keyboard shortcut, user macro, Stream Deck button,
programmable keypad, mouse buttons, game controller, MIDI device...
without changing the Command Framework."

Read together, these two requirements point at the same design question:
how does a macro's own execution reach the Command Framework? A naive
design gives `IMacroManager` its own `RunAsync(macroId)` method, called
directly by whichever UI surface offers "run this macro" — the Command
Palette, a future Ribbon button, a future `IInputBindingProvider`. That
design works, but means every one of those surfaces needs its own,
separate "is this a macro or a command?" branch — exactly the kind of
Command-Framework-adjacent special-casing the brief's own "without
changing the Command Framework" language is written to avoid.

## Decision

**A macro is registered as an ordinary `CommandDescriptor`, identical in
every respect to any other invokable-by-Id command.** `RunMacroCommand :
ICommand` (carrying only `MacroId`) is the one command type every macro's
own descriptor dispatches; `RunMacroCommandHandler` resolves the macro via
`IMacroManager.FindAsync` and sequentially `ICommandRegistry.InvokeAsync`s
each of its own `StepCommandIds`, stopping at the first failure.
`IMacroManager.CreateAsync`/`LoadAsync` register one `CommandDescriptor`
per macro (`Id = "macro:{Guid}"`, `Category = "Macros"`, `CreateDefault:
() => new RunMacroCommand(macroId)`) against the shared
`ICommandRegistry` — the identical registry every real discipline command
already registers against (`ADR-0070`).

The direct consequence: the Command Palette already invokes any
`CreateDefault`-eligible descriptor via `ICommandRegistry.InvokeAsync` —
a macro's own descriptor is invoked through that exact, unmodified path,
with zero macro-specific code inside `CommandPaletteOverlay` itself
beyond an optional `InvokeOverride` hook (used only to route the
potentially-multi-step invocation through the Background Task Runner,
`WP 10.6A` §4 — itself orthogonal to *how* the macro is invoked). The
identical reasoning extends to `IInputBindingRegistry`/
`IInputBindingProvider` (`ADR-0100`): any provider that raises a
Command Id already reaches a macro's own execution the moment that Id is
`"macro:{Guid}"` — no separate "is this a macro" branch exists, or is
needed, inside the router either.

**Disclosed, load-bearing limitation, not introduced by this decision**:
a macro step must itself be a `CreateDefault`-eligible descriptor.
Confirmed by direct repository-wide `grep` for `createDefault:` before
this Work Package began: only `Tempest.Samples` commands set it — no
real Engineering discipline command (Create/Rename/Revise/Delete/
Set-Status, etc.) does, since each needs UI-collected context
(`CommandPaletteOverlay`'s own remarks already document this identical
fact for the Palette). A macro can today sequence Sample commands
end-to-end, and any future command a Work Package chooses to make
Id-invokable — extending that eligibility to real discipline commands is
its own, larger, future Work Package, not attempted here.

`ICommandRegistry` exposes no method to unregister a descriptor
(confirmed, frozen `ADR-0037` contract) — deleting a macro
(`IMacroManager.DeleteAsync`) therefore cannot remove its own already-
registered `CommandDescriptor`. `RunMacroCommandHandler` handles the
resulting stale-descriptor case explicitly: `IMacroManager.FindAsync`
returns `null`, and the handler returns a graceful `CommandResult.Failure`
("Macro '...' no longer exists.") rather than throwing.

## Consequences

**Positive:**

- Zero changes to `ICommand`/`ICommandDispatcher`/`ICommandRegistry`/
  `CommandDescriptor` — a macro is data flowing through the exact,
  unmodified Command Framework `ADR-0036`/`ADR-0037` already established.
- Every present and future command-invoking surface (Command Palette,
  a future Ribbon binding, `IInputBindingProvider`/`IInputBindingRegistry`)
  gains macro support automatically, with no surface-specific macro
  branch to write or maintain.
- `RunMacroCommandHandler` is one, small, ordinary
  `ICommandHandler<RunMacroCommand>` — reviewable and testable in
  complete isolation from every UI surface that might invoke it.

**Negative:**

- Macro steps are constrained to the `CreateDefault`-eligible subset of
  registered commands — today, a real but small set (Sample commands
  only). Disclosed directly, not fabricated as broader capability.
- A deleted macro's own `CommandDescriptor` remains permanently
  registered for the life of the running process — a small, permanent
  "ghost" list entry (harmless: it fails gracefully, never silently
  succeeds against stale data) rather than a clean removal.

## Alternatives Considered

**`IMacroManager.RunAsync(macroId)`, called directly by each UI surface.**
Considered and rejected — see Context: this is the design that would
have required a separate macro-aware branch in the Command Palette, a
future Ribbon binding, and the Input Binding Router alike, directly
contradicting the brief's own "without changing the Command Framework"
objective for every *other* future binding target it names.

**A macro step referencing an already-constructed `ICommand` instance
directly (serialised), rather than a Command Id string.** Considered and
rejected — `ICommand` instances are not required to be serialisable
(no such constraint exists on the interface, and several real commands
carry non-primitive constructor arguments); a Command Id string, resolved
through the already-existing `ICommandRegistry.InvokeAsync` machinery,
needs no new serialisation contract at all.

## Addendum (`WP 20.2C`) — both disclosed gaps closed: `Unregister` exists, and a step can record a real command's own values

The Technical Debt Rationalisation audit of 2026-09-14 restated this
ADR's own two disclosed limitations, unclosed since `v0.10.0`: `ICommandRegistry`
exposed no way to remove a descriptor, so a deleted macro's own
`CommandDescriptor` stayed registered permanently; and a macro step
needed a `CreateDefault`-eligible descriptor, which no production
discipline command has ever set, so a macro could only ever sequence
`Tempest.Samples` demo commands. Both close here.

**`ICommandRegistry.Unregister(string id)`.** Removes a descriptor
outright. `Items` was already read fresh by every consumer — the
Ribbon rebuilds its tabs from it, the Command Palette re-filters it on
every keystroke — so no second change-notification mechanism was
needed for a removal to be immediately visible everywhere; the frozen
`ADR-0037` contract this ADR's own Decision cited no longer applies,
since `WP 19.10R` (`TD-179`'s residual) had already re-opened
`CommandRegistry.cs` to add the archived-project guard `Evaluate`
consults, and this Work Package's own brief instructed adding the new
method cleanly beside it. `IMacroManager.DeleteAsync` calls it: a
deleted macro's own descriptor is now genuinely gone, not merely left
to fail gracefully — `RunMacroCommandHandler`'s own graceful "no
longer exists" outcome stays, guarding the one remaining path that can
still reach a macro Id nothing (or nothing any more) resolves to: a
`RunMacroCommand` dispatched directly, bypassing the registry's own Id
lookup.

**A macro step now records what a person supplied, and replays it.**
TD-77 Stage 5 (`v0.19.0`) had already widened step eligibility once,
from "has a `CreateDefault`" to "the binding needs nobody present"
(`CommandBinding.RequiresPrompt` false) — real, but still excluded
every Create, every Rename, every Set-something, because each declares
a value only a person can supply. `MacroStep` (`src/Tempest.Core/Macros/MacroStep.cs`)
is the missing piece: a step is now a command Id *and* the values
recorded for it — collected once, at record time
(`MacroManagerDialog.AddStepAsync`), through the identical
`CommandParameterPrompt` seam a live invocation already uses
(`DesktopCommandPrompt`), never a second collection mechanism.
`IMacroManager.CreateAsync` gains an overload taking
`IReadOnlyList<MacroStep>` (the single-string-Id overload remains,
wrapping each Id in a `MacroStep` recording nothing, so every existing
caller is unaffected); it refuses a step whose own binding is declared
`Unavailable` — today, the object-picker set `WP 20.2A` has not yet
closed — naming the binding's own reason, before the macro is ever
created. `MacroManagerDialog.IsMacroEligible` widens accordingly: a
parameterised binding is offered now; a binding declaring a
confirmation still is not, because no recording can stand in for a
person's "yes" — the one place this Work Package leaves this ADR's own
"never runs unattended" principle exactly as `RunMacroCommand`'s own
remarks already stated it.

`RunMacroCommandHandler` replays each step through a prompt built for
it: every value the step's own `RecordedValues` carries answers
silently; a value the binding declares that recording did not capture
is asked of an optional, constructor-supplied fallback prompt — never
a confirmation, which a recording never answers and this seam never
asks for either — and with no fallback supplied (every production
composition today), the framework's own unchanged "needs additional
input, and no input surface was supplied" is what a step still missing
something reports, exactly as it did before this Work Package. A macro
therefore still never interrupts a run with a live dialog in
production; what changed is that a step recording everything its own
binding needs no longer has to be told anything at all.

**Consequence for a stored macro.** `MacroManager`'s own persisted
shape (`MacroDto`) gains an additive, optional `StepValues` field,
positionally aligned with the existing `StepCommandIds` — a macro
persisted before this Work Package deserialises with `StepValues`
absent, which every step reads as "recorded nothing," the exact shape
it already had.

## Addendum (`WP 21.1A`) — compensation on the result: how Undo/Redo reaches Create, Delete, Move, Copy and status changes

`ADR-0098` (Undo/Redo is a desktop-local `UndoableAction` delegate stack)
disclosed at `v0.10.0` that only Rename and the Favourite toggle were
ever wired — every other mutating command, including a macro's own run,
had no inverse a Do/Undo pair could be built from, because a command's
own constructor carries forward-facing data only (`ADR-0063`) and this
platform's Command Framework itself (`ADR-0036`/`ADR-0037`) is frozen
against a general `IUndoableCommand` extension. This Work Package closes
that gap the same way `WP 20.2C`'s own addendum above closed the
identical "commands don't carry prior state" problem for a macro step's
own redo — not by widening the Command Framework, but by handing the one
collaborator that already knows what changed (the handler) a place to
say how to reverse it.

**`CommandResult` gains an optional `Compensation`.** A handler that
knows how to reverse its own successful outcome returns
`CommandResult.Success(message, subjectId, subjectKind, compensation)`,
where `compensation` (`CommandCompensation`, `Tempest.Core.Commands`) is
a description plus an `Undo`/`Redo` delegate pair — the identical shape
`Tempest.Workspace.UndoableAction` already has, deliberately: the
Desktop's own reporting path converts one into the other with a one-line
bridge at the point it records it, never a second definition of what
Undo/Redo mean. A handler that cannot reverse its own outcome (most of
this platform, unchanged) simply does not set it, exactly as before this
Work Package.

**A compensation is itself a real command, dispatched directly, never
replayed by Id.** The natural design this ADR's own macro-replay
machinery suggests — record a command Id and a set of values, replay
them later through `ICommandRegistry.InvokeAsync` — was tried first and
rejected for two reasons specific to a compensation, neither of which
applies to a macro step: first, a compensation's own inverse is often
not "the same command with different values" (Delete's own inverse is
`IDeletable.UndeleteAsync`, a capability with no command id or binding
of its own before this Work Package, and none this Work Package
registers publicly either); second, replaying through the registry would
put a compensation through the Ribbon- and Palette-visible Id-to-binding
machinery for something that must never appear in either. Instead, a
compensation's `Undo`/`Redo` each dispatch a concrete `ICommand` instance
directly through `ICommandDispatcher.DispatchAsync` — reusing the exact
command type (and, where the reverse needs one, a new but never
publicly-registered type, `UndeleteDocumentObjectCommand` and its four
siblings) the original handler's own discipline already has a handler
for — after an explicit call to the identical archived-project guard
(`WP 19.10R`, `TD-179`'s residual) `ICommandRegistry.Evaluate` consults
for a `Mutates` binding, checked by hand here because dispatching
directly bypasses `Evaluate` entirely. `WorkspaceCommandBindings`
(`Tempest.Workspace`) carries the one shared implementation
(`RunCompensationAsync` and its four per-family builders) every
discipline's own `Create*/Delete*/Move*/Copy*/Set*Status*Command.cs`
handler calls, so the guard check and the "never a direct repository
write" rule live in exactly one place rather than thirty.

**Recorded wherever the real `CommandResult` is still held.** The
Desktop's own reporting path had already, before this Work Package,
reduced a `CommandResult` to a `(message, ActionOutcome)` pair at
several call sites — `RibbonView`'s own two dispatch paths chief among
them — discarding the object a compensation lives on before it could
ever be recorded. Each such site now also records
`result.Compensation` directly, alongside (not instead of) its own
existing message-and-outcome report; `CommandPaletteOverlay`'s own
`CommandInvoked` event already carried the real result, so nothing in
that view changed — the identical recording lives at its one existing
subscriber instead.

**A macro's own run is one compound action, not one per step.**
`RunMacroCommandHandler` collects each step's own compensation as it
runs; a fully-compensable run's own result carries one compound
`CommandCompensation` whose `Undo` reverses every step in reverse order
and whose `Redo` replays them forward, stopping at (and naming) the
first step that refuses — the identical honesty this handler's own
pre-existing "stopped at step N" failure message already gives a run
that fails partway through. A run where any step carries no
compensation reports why on `CommandResult.UndoUnavailableReason`
instead, naming the step, rather than offering a partial undo.

**Boundaries are stated, not hidden.** A status transition the
platform-wide `LifecycleTransitionTable` will not permit reversing
(`Approved` → `Released`, and any other one-way move) carries no
compensation; `CommandResult.UndoUnavailableReason` is what a caller
reports instead of silently offering nothing. A refused or failed
Undo/Redo — the archived-project guard, a parent since deleted, a
lifecycle rule the domain itself enforces — leaves
`Tempest.Workspace.IUndoRedoStack` exactly as it was before the attempt
(the action goes back to the stack it came from, never the other one) —
a stack-consistency fix this Work Package made to `UndoRedoStack` itself,
not a new rule invented for this addendum alone. The stack also gains
`Clear()`, called once, by `UndoRedoCoordinator`, on every project
switch (`ProjectContextChangedEvent`) — an action recorded against one
project has nowhere safe to replay against once a different one is open.

**Disclosed, not silently narrowed: Requirements.** Every discipline
built against `EngineeringDomainContext.Repository`/`EngineeringObjectBase`
(Documents, Manufacturing, Calculations, Verification, Mechanical) gained
this Work Package's own compensation in full. Requirements runs on
`IRequirementsService`/`Tempest.Core.EngineeringData.IEngineeringDocumentStore`
instead — a materially different persistence path the archived-project
guard (which reads `EngineeringDomainContext.Repository` directly) may
not even resolve a Requirement through, and one with no restore
capability of its own to invert a delete into. Left untouched rather
than built against uncertain foundations in one night; see `BACKLOG.md`'s
own `WP 21.1A` entry for the reasoning in full.

## Related Documents

`ADR-0036`; `ADR-0037`; `ADR-0070`; `ADR-0100`; `ADR-0098`;
`src/Tempest.Core/Commands/ICommandRegistry.cs`;
`src/Tempest.Core/Commands/CommandResult.cs`;
`src/Tempest.Core/Commands/CommandCompensation.cs`;
`src/Tempest.Core/Macros/ICommandMacro.cs`;
`src/Tempest.Core/Macros/IMacroManager.cs`;
`src/Tempest.Core/Macros/MacroManager.cs`;
`src/Tempest.Core/Macros/MacroStep.cs`;
`src/Tempest.Core/Macros/RunMacroCommand.cs`;
`src/Tempest.Desktop/Views/MacroManagerDialog.cs`;
`src/Tempest.Workspace/Workspace/WorkspaceCommandBindings.cs`;
`src/Tempest.Workspace/Workspace/UndoRedoStack.cs`;
`src/Tempest.Desktop/Composition/UndoRedoCoordinator.cs`;
`docs/releases/v0.10.0/WP10.6A Implementation Report.md`.
