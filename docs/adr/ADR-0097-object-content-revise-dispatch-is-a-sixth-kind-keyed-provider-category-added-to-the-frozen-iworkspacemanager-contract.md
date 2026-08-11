# ADR-0097: Object Content-Revise Dispatch Is a Sixth Kind-Keyed Provider Category, Added to the Frozen `IWorkspaceManager` Contract as New, Additive Members

## Status

Accepted — `v0.10.0` "User Experience & Desktop Application", `WP 10.3A` (Engineering Object Editors), 2026-08-09. Recorded separately from `ADR-0082`/`ADR-0096` because it extends the same frozen contract (`IWorkspaceManager`, `WP8.0B`) a third time, for a different concern (content-revise dispatch, not rename/delete).

## Context

Every real discipline already implements a concrete `Revise*Command` wrapping `IHasRevisions.ReviseAsync` (`Calculations`/`Documents`/`Manufacturing`/`Verification`, `WP 9.x`; `Requirements`' own `ReviseRequirementCommand` wraps `IRequirementsService.ReviseAsync` instead) — every one already compiled, already tested, already registered against `ICommandDispatcher`, reachable only through the Command Palette's own parameterless `CreateDefault` path (`AT-10`'s own identical limitation), never with a runtime-selected target Id and user-typed new content. Mechanical alone had no `Revise*Command` at all, despite every concrete Mechanical Kind already implementing `IHasRevisions` unconditionally (`EngineeringObjectBase`, `ADR-0075`) — a missing Workspace-layer command wrapper, not a missing Domain capability.

`WP 10.3A`'s own controlling instruction requires "Editable properties" and a real "Save/Cancel workflow" for the Object Editor Framework — not decorative fields with nothing to dispatch to. `ADR-0063` requires every mutation to dispatch through the Command Framework, never a direct call to a mutating Domain method — so the Object Editor Framework's own Content field needs the identical kind of real, generic, Kind-agnostic dispatch path `ADR-0096` already built for Rename/Delete, applied a third time for Revise.

## Decision

**`IWorkspaceManager` gains three new members**, mirroring `RegisterRenameFactory`/`CanRename`/`RenameObjectAsync`'s own `ADR-0096` shape exactly: `RegisterReviseFactory(string kind, Func<Guid, string, string, IWorkspaceCommand> factory)`, `CanRevise(string kind)`, `ReviseObjectAsync(Guid id, string kind, string newContent, ...)` — additive only; every existing member, including `ADR-0096`'s own five, is untouched.

**Every discipline's own already-existing `Revise*Command` becomes a factory**, reusing `WorkspaceManager`'s own established `Dictionary<string, T>` + `TryAdd` + `DuplicateWorkspaceRegistrationException` pattern and `CommandHandlerTable.DispatchAsync` primitive, unmodified since `ADR-0096`. **One genuinely new command was written**: `ReviseMechanicalObjectCommand`/`ReviseMechanicalObjectCommandHandler`, mirroring `ReviseCalculationCommand`'s own identical shape exactly — the missing sixth discipline, closed here rather than left asymmetric. Requirements registers a revise factory for its own `Requirement` Kind only (`ReviseRequirementCommand`, wrapping `IRequirementsService.ReviseAsync` instead of `IHasRevisions.ReviseAsync` directly — the identical, already-established asymmetry `ADR-0096` itself disclosed for this discipline, just the complementary half of it: Requirements has Revise but no Rename, every other discipline has both) — `RequirementGroup`/`RequirementCollection` are structural containers with no Content concept, honestly not registered. Manufacturing's own `"WorkInstruction"`/`"Inspection"` Kinds reuse Documents'/Verification's own already-registered `Revise*Command` factories directly, mirroring the identical View/Facet Provider/Rename/Delete reuse `ManufacturingWorkspaceRegistration` already establishes. Calculations' own synthetic `"CalculationTemplate"` Kind registers neither — never an `EngineeringDomainContext.Repository` object, so the handler would always fail against it.

## Consequences

**Positive:**

- Realises the Object Editor Framework's own Content field as a real, working, dispatched capability across all six disciplines — every real Kind's own object can have its own descriptive content revised through a real Command, not a decorative text box.
- Closes a genuine, disclosed pre-existing asymmetry: Mechanical was the only discipline of six with no Revise command at all, despite the underlying Domain capability (`IHasRevisions`) already existing identically for it since `ADR-0075`.
- `CanRevise` gives the Desktop presentation layer the identical honest, pre-check surface `CanRename`/`CanDelete` already provide — a Content field is shown editable only for a Kind that genuinely has a registered factory.
- Zero behavioural change to any existing registration, view, facet, or command handler — every discipline's own registration file gains only new calls (plus one genuinely new command, Mechanical's), none removed or altered.

**Negative:**

- `IWorkspaceManager` is now extended a third time beyond its own `WP8.0B` frozen shape (after `ADR-0082`, `ADR-0096`) — a continuing, disclosed pattern, worth naming again rather than treating as settled, exactly as `ADR-0096` itself named its own second extension.
- Two Requirements Kinds (`RequirementGroup`/`RequirementCollection`) remain honestly incomplete — a real, disclosed absence, not a defect, consistent with this platform's established "disclose, don't fabricate" precedent.

## Alternatives Considered

**A direct call from `ObjectEditorView` to `IHasRevisions.ReviseAsync`, bypassing the Command Framework entirely.** Considered and rejected outright — a direct violation of `ADR-0063`'s own explicit "every mutation dispatches through the Command Framework" decision, which names exactly this class of View-layer temptation in its own Context section.

**A single, non-generic `IWorkspaceManager.ExecuteObjectCommandAsync(string verb, ...)` covering Rename/Delete/Revise together, retrofitted over `ADR-0096`'s own two verbs.** Considered and rejected — `ADR-0096` already considered and rejected the equivalent shape for its own two verbs, for the same reason (loses compile-time argument shape, reads as a disguised reflection call); revisiting that decision now, for a third verb alone, would not resolve the reasoning that rejected it the first time.

## Related Documents

`ADR-0063`; `ADR-0075`; `ADR-0082`; `ADR-0096`; `WP8.0B Workspace Contracts.md`; `src/Tempest.App/Workspace/IWorkspaceManager.cs`; `src/Tempest.App/Workspace/WorkspaceManager.cs`; `src/Tempest.App/Workspace/Mechanical/ReviseMechanicalObjectCommand.cs`; `src/Tempest.Desktop/Editors/ObjectEditorView.cs`; `docs/releases/v0.10.0/WP10.3A Implementation Report.md`.
