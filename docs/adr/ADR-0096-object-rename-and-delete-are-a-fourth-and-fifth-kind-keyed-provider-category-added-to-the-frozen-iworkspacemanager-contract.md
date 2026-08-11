# ADR-0096: Object Rename and Delete Dispatch Are a Fourth and Fifth Kind-Keyed Provider Category, Added to the Frozen `IWorkspaceManager` Contract as New, Additive Members

## Status

Accepted — `v0.10.0` "User Experience & Desktop Application", `WP 10.2A` (Workspace Modernisation), 2026-08-07. Recorded separately from `ADR-0082` because it extends the same frozen contract (`IWorkspaceManager`, `WP8.0B`) a second time, for a different concern (mutating dispatch, not a read-side provider).

## Context

Every real discipline (`WP 9.0A`–`WP 9.5A`) already implements a concrete, uniform `Rename*Command`/`Delete*Command` pair implementing `IWorkspaceCommand` (`TargetObjectId`, `TargetKind`), registered against `ICommandDispatcher` at composition-root time. `MechanicalWorkspaceRegistration`'s own `WP 9.0A` remarks state plainly that `createDefault` is deliberately omitted from every such descriptor — "none of these nine commands has a meaningful parameterless default in a shell with no pre-selected object context... dispatched with real data through `ICommandDispatcher.DispatchAsync<TCommand>` by a caller that already has it (**a future context-menu action**)." No such caller has existed until now: neither the console `WorkspaceShell` nor `Tempest.Desktop` (`WP 10.0B`/`WP 10.1A`) ever dispatches a discipline's own real, parameterised mutating command against an arbitrary selected object — every existing invocation path (the Command Palette, `ICommandRegistry.InvokeAsync`) is `CreateDefault`-based and parameterless by contract, incapable of carrying a runtime-selected target Id and a user-typed new name.

`WP 10.2A`'s own controlling instruction requires "inline rename" (Project Explorer) and "editable controls where appropriate" (Property Inspector) as real, working capabilities — not decorative UI with nothing to dispatch to, per this project's own "never fabricate" discipline. Building them honestly requires a real, generic, Kind-agnostic dispatch path from the Desktop presentation layer through to each discipline's own already-real, already-tested command handler.

## Decision

**`IWorkspaceManager` gains four new members**, mirroring `RegisterFacetProvider`'s own `ADR-0082` shape exactly: `RegisterRenameFactory(string kind, Func<Guid, string, string, IWorkspaceCommand> factory)`, `RegisterDeleteFactory(string kind, Func<Guid, string, IWorkspaceCommand> factory)`, `CanRename(string kind)`/`CanDelete(string kind)` (honest pre-check predicates), and `RenameObjectAsync(Guid id, string kind, string newDisplayName, ...)`/`DeleteObjectAsync(Guid id, string kind, ...)` (the dispatch verbs themselves) — additive only: every existing member is untouched, and `WorkspaceManager`'s own established `Dictionary<string, T>` + `TryAdd` + `DuplicateWorkspaceRegistrationException` registration pattern is reused verbatim.

**Dispatch reuses the existing, unmodified `CommandHandlerTable.DispatchAsync(ICommand, CancellationToken)` primitive** — the same runtime-type-keyed lookup `ICommandRegistry.InvokeAsync` already uses internally for an identical reason (a caller with only a runtime-typed `ICommand`, not a compile-time generic parameter). No new dispatch mechanism, no reflection, is introduced.

**Every discipline's own registration class now registers its own already-existing `Rename*Command`/`Delete*Command` as a factory**, per Kind it genuinely supports — no new command class was written for this Work Package; every factory constructs a command type that already compiled, was already tested, and already had a registered handler before this ADR. Requirements registers Delete only (three Kind-specific factories, one per its own three dedicated Delete commands) and no Rename factory for any of its three Kinds — genuinely, honestly: no `Rename*Command` exists for this discipline (a Requirement's own mutable field is its `Statement`, mutated via `ReviseRequirementCommand`, not a `DisplayName`/`IRenamable` concept the other five disciplines share). Manufacturing's own `"WorkInstruction"`/`"Inspection"` Kinds reuse Documents'/Verification's own already-registered commands directly, mirroring the identical View/Facet Provider reuse `ManufacturingWorkspaceRegistration` already establishes. Calculations' own synthetic `"CalculationTemplate"` Kind registers neither — it is never an `EngineeringDomainContext.Repository` object, so both handlers would always fail against it; honestly never offered, rather than a menu item that can never succeed.

## Consequences

**Positive:**

- Realises the "future context-menu action" `WP 9.0A` itself anticipated and built for, closing a real, disclosed platform gap: before this change, no interactive surface (console or desktop) could ever successfully rename or delete an arbitrary selected engineering object, despite every discipline's own command and handler having compiled and been unit-tested since its own Work Package.
- `CanRename`/`CanDelete` give the Desktop presentation layer an honest, pre-check surface — a context menu or inline-edit affordance is shown enabled only for a Kind that genuinely has a registered factory, never fabricated or always-enabled.
- Zero behavioural change to any existing registration, view, facet, or command handler — every discipline's own registration file gains only new calls, none removed or altered.

**Negative:**

- `IWorkspaceManager` is now extended a second time beyond its own `WP8.0B` frozen shape (after `ADR-0082`'s own `RegisterFacetProvider`) — a continuing, disclosed pattern of this contract needing more surface than any Work Package before `WP 9.0A` anticipated, worth naming again rather than treating as settled.
- Two disciplines (Requirements' three Kinds; Calculations' `"CalculationTemplate"`) are honestly incomplete — real, disclosed absences, not defects, consistent with this platform's own established "disclose, don't fabricate" precedent.

## Alternatives Considered

**Extend `ICommandDescriptor.CreateDefault` to accept an ambient "current selection" context** — considered and rejected. Would entangle the Command Framework's own deliberately selection-agnostic contract (`Command Framework Architecture.md`) with Workspace-specific selection state, a layering violation `ADR-0036`/`ADR-0037` do not permit.

**A single, non-generic `IWorkspaceManager.ExecuteObjectCommandAsync(string verb, Guid id, string kind, params object[] args)`** — considered and rejected. Loses compile-time argument shape for the one verb (`Rename`) that needs an extra parameter beyond `(id, kind)`, and reads as a disguised reflection call rather than the explicit, typed factory pattern `RegisterFacetProvider` already established as this platform's own idiom.

## Related Documents

`ADR-0067`; `ADR-0082`; `WP8.0B Workspace Contracts.md`; `src/Tempest.App/Workspace/IWorkspaceCommand.cs`; `src/Tempest.App/Workspace/IWorkspaceManager.cs`; `src/Tempest.App/Workspace/WorkspaceManager.cs`; `src/Tempest.App/Workspace/Mechanical/MechanicalWorkspaceRegistration.cs`; `docs/releases/v0.10.0/WP10.2A Implementation Report.md`.
