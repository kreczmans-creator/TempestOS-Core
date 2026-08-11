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

## Related Documents

`ADR-0036`; `ADR-0037`; `ADR-0070`; `ADR-0100`;
`src/Tempest.Core/Macros/ICommandMacro.cs`;
`src/Tempest.Core/Macros/IMacroManager.cs`;
`src/Tempest.Core/Macros/MacroManager.cs`;
`src/Tempest.Core/Macros/RunMacroCommand.cs`;
`src/Tempest.Desktop/Views/MacroManagerDialog.cs`;
`docs/releases/v0.10.0/WP10.6A Implementation Report.md`.
