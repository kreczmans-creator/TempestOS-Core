# WP 9.0A — Mechanical Product Structure — Future Capability Assessment

## Purpose

Records candidate future capabilities this Work Package's own
implementation surfaced but deliberately did not build — each weighed
against genuine need versus speculative scope expansion.

## FCR-0039 — Multi-Selection in the Project Explorer

`ISelectionService` (`WP8.0B`, frozen) tracks exactly one current
selection. Multi-select-and-bulk-act (move/delete several objects at
once) is a natural Product Structure operation this Work Package's own
controlling instruction explicitly named as conditional — "if supported
by current UI technology." It is not supported by the current contract.
**Recommended for a future Work Package**, scoped as its own contract
change (extending or replacing `ISelectionService`), not bundled
speculatively into this one.

## FCR-0040 — Drag-and-Drop Reparenting

`ADR-0066` commits this platform to a terminal-based Workspace
presentation; no drag gesture exists in that medium. Genuinely blocked
on a future rendering-technology decision, not a Mechanical Product
Structure gap. **Not recommended until `ADR-0066` itself is revisited**
— tracked here so the dependency is explicit, not to imply it is due.

## FCR-0041 — Real Invoke-by-Id Execution for Object-Targeted Commands

All six Mechanical `CommandDescriptor`s omit `createDefault`, so none is
invokable by bare Id through `ICommandRegistry.InvokeAsync` today — real
dispatch requires a caller with actual target data
(`ICommandDispatcher.DispatchAsync`). A context-menu-driven UI (once one
exists beyond `WorkspaceShell`'s own current `menu <N>` listing) would
supply that data naturally. **Recommended once a richer interaction
surface exists** — premature today, since the only present caller
(`WorkspaceShell`) has no selection-aware command invocation path yet
either.

## FCR-0042 — A Second Engineering Discipline Module Reusing This Work Package's Own Three Provider Categories

`MechanicalProductStructureNodeProvider`/`MechanicalWorkspaceViewFactory`/
`MechanicalPropertyFacetProvider` are the first real (non-sample)
implementations of all three Kind-keyed provider interfaces. A second
discipline (Documentation & Design, or Supply Chain — both already real,
`WP8.2C` Kinds with no Workspace presentation yet) would prove the
pattern generalises rather than being an accidental one-off.
**Recommended as the natural next Engineering Discipline Module** —
named here for continuity, not as an instruction to begin it.

## FCR-0043 — Structural Mutation for Documentation & Design / Supply Chain Kinds

`IRenamable`/`IHasParent`/`IDeletable` are composed only into the five
Product Structure Kinds today. A future Work Package extending Workspace
presentation to Drawing/CAD Model/Supplier/Purchase Item could compose
the same three facets rather than inventing new ones — the additive
extension model `ADR-0080` establishes generalises directly.
**Recommended, contingent on FCR-0042.**

## Not Recommended: A Generic, Kind-Agnostic "Structural Object" Base Type

Considered during implementation: rather than composing three named
facets per Kind, a single `IStructuralObject` uniting all of
`IRenamable`/`IHasParent`/`IDeletable`/`IHasBusinessIdentifier` could
reduce interface declarations. **Not recommended** — would reintroduce
exactly the "large, kitchen-sink interface" shape `ADR-0075` already
rejected in favour of small, individually composable facets; the
composition model's own value is in each facet staying independently
meaningful.

## Verdict

Five candidates recorded (`FCR-0039`–`FCR-0043`); none built speculatively
ahead of genuine need, consistent with this Work Package's own "no
architectural redesign" constraint.

## Related Documents

`docs/governance/Future Capability Register.md`; `ADR-0066`; `ADR-0075`;
`ADR-0080`; `WP9.0A Engineering Review Report.md`.
