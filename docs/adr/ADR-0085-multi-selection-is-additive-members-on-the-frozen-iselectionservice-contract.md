# ADR-0085: Multi-Selection Is Additive Members on the Frozen `ISelectionService`/`IWorkspaceContext` Contracts — Single-Selection Behaviour Is Completely Unchanged

## Status

Accepted — `v0.9.0` "Mechanical Foundation", `WP 9.1A` (Requirements Management Workspace), 2026-08-05.

## Context

`WP 9.0A`'s own controlling instruction listed multi-selection under Requirement Editing with an explicit "if supported" qualifier — `ISelectionService` (`WP8.0B Workspace Contracts.md`, frozen) supports only a single `Current` selection, and `WP 9.0A` left multi-selection unbuilt, naming it `FCR-0039` in its own Future Capability Assessment for exactly this reason: no real consumer needed it yet. `WP 9.1A`'s own controlling instruction names Multi-selection under Requirement Editing without that qualifier — Bulk editing (`BulkSetRequirementStatusCommand` and its Owner/Priority equivalents) is a real, concrete consumer, so `FCR-0039` is resolved here, not deferred again.

`ISelectionService` is a frozen `WP8.0B` contract. `ADR-0082` already established the precedent for extending a frozen Workspace contract additively rather than reopening it — there, a third Kind-keyed provider category added to `IWorkspaceManager`. The identical reasoning applies here to a different frozen interface.

## Decision

**`ISelectionService` gains two new members, `IWorkspaceContext` gains one, both purely additive:**

- **`IReadOnlyList<WorkspaceSelection> SelectedItems { get; }`** (on both `ISelectionService` and `IWorkspaceContext`) — every currently selected item, in selection order. Defaults to mirroring `Current` alone: `SelectAsync`/`ClearAsync` (both unchanged in their own existing behaviour) now also keep `SelectedItems` a consistent singleton/empty set, so every caller that has only ever used single-selection observes no behavioural change whatsoever — `SelectedItems` simply reports what `Current` already implied.
- **`Task ToggleSelectionAsync(Guid objectId, string kind, ...)`** (on `ISelectionService` only) — adds the item if absent, removes it if present. `Current` becomes the newly toggled-in item on an add, or the most-recently-toggled-in survivor on a remove (`null` if the set becomes empty) — real multi-selection, reachable only through this one new method; nothing about `SelectAsync`'s own single-item-replace semantics changes.

**A new `WorkspaceSelectionSetChangedEvent`** is published (through the same, unmodified `IEventBus`, no new pub/sub mechanism) whenever the selection *set* changes — fired alongside the existing, unchanged `WorkspaceSelectionChangedEvent`, never in its place, on every mutation path (`SelectAsync`, `ClearAsync`, `ToggleSelectionAsync` alike). A subscriber written only against the frozen `WP8.0B` `WorkspaceSelectionChangedEvent` continues to receive exactly the events it always has, in exactly the shape it always has.

`WorkspaceContext`'s own backing storage replaces its selection-set field wholesale on each mutation (never mutates a shared list in place), so a caller holding an earlier `SelectedItems` reference sees a stable, un-mutating snapshot — the same "never a service locator, never returns a live-mutating collection" discipline `WP8.0B Dependency Rules.md` §5 already requires of `IWorkspaceContext`.

## Consequences

**Positive:**

- Every existing `ISelectionService`/`IWorkspaceContext` consumer — `PropertyInspector`, `WorkspaceStatusBar`, every `WP8.0B`/`WP8.1x`/`WP 9.0A` test — is completely unaffected; nothing existing changed shape or behaviour.
- `BulkSetRequirementStatusCommand`/`BulkSetRequirementOwnerCommand`/`BulkSetRequirementPriorityCommand` (`WP 9.1A`) have a real selection surface to read `IReadOnlyList<Guid>` from at a future call site, rather than requiring their own caller to invent one.
- `FCR-0039` is resolved with a real consumer driving the design, rather than speculatively ahead of one.

**Negative:**

- A caller now has two selection reads to reason about (`Current`, `SelectedItems`) rather than one — mitigated by the documented invariant that `SelectedItems` always mirrors `Current` alone unless `ToggleSelectionAsync` has been used at least once.
- `WorkspaceSelectionSetChangedEvent` is a second event firing on every selection change, a small additional subscription surface — accepted as the cost of never breaking the existing event's own frozen shape, the same trade-off `ADR-0082` already accepted for its own third provider category.

## Alternatives Considered

**Reshape `WorkspaceSelectionChangedEvent` itself to carry a set instead of a single item** — considered and rejected. This would be a genuine reopening of a frozen `WP8.0B` event contract, breaking every existing subscriber's own assumption that `Current`/`Previous` are single items, for a capability only Requirements needs today.

**A bespoke `IReadOnlyList<Guid>` parameter threaded through each Bulk command instead of a real `ISelectionService` capability** — considered and rejected. This would leave "multi-selection" as a per-command convention invented three times (Status/Owner/Priority) rather than one Workspace-wide capability every future bulk operation can reuse, and would not satisfy this Work Package's own explicit "Multi-selection" scope item as a genuine Workspace capability.

## Related Documents

`ADR-0082`; `WP8.0B Workspace Contracts.md`; `WP8.0B Dependency Rules.md` §5; `WP 9.0A Future Capability Assessment.md` (`FCR-0039`); `src/Tempest.App/Workspace/ISelectionService.cs`; `src/Tempest.App/Workspace/SelectionService.cs`; `src/Tempest.App/Workspace/WorkspaceSelectionSetChangedEvent.cs`.
