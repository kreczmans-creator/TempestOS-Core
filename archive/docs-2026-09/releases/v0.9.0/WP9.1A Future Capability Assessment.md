# WP 9.1A — Requirements Management Workspace — Future Capability Assessment

## Purpose

Records candidate future capabilities this Work Package's own
implementation surfaced but deliberately did not build, and confirms
disposition of the one prior candidate this Work Package resolves.

## `FCR-0039` — Resolved

`WP 9.0A`'s own Future Capability Assessment recorded `FCR-0039`
(multi-selection over `ISelectionService`) as "not recommended before a
real consumer needs it." `WP 9.1A`'s own Bulk editing scope item is that
real consumer — `FCR-0039` is resolved by `ADR-0085`/`SelectedItems`/
`ToggleSelectionAsync`, not deferred again.

## `FCR-0048` — Requirement Collection Membership Removal

`AddRequirementToCollectionCommand` has no symmetric "remove" —
`IEngineeringDocumentStore` has no unlink primitive to build one on
(confirmed directly, unchanged since `WP 7.3A`). A future
`IEngineeringDocumentStore.UnlinkAsync` (or an equivalent "supersede a
relationship" mechanism, since this platform's own append-only ethos
may prefer marking a relationship inactive over erasing it) would let a
real "remove from collection" command exist. **Recommended once a real,
demonstrated need for collection membership correction exists** —
building the Domain-level unlink primitive speculatively now would be
exactly the "no architectural redesign ahead of real need" this
project's own convention warns against, and would affect every
`DocumentReference` consumer platform-wide, not only Requirements.

## `FCR-0049` — Domain-Level Search Generalised Beyond `IEngineeringObject`

`Contracts/Search.cs`'s own `ISearchQuery`/`ISearchResult` (`WP8.2B`)
remain unimplemented anywhere; `ISearchResult.Matches` is typed
`IReadOnlyList<IEngineeringObject>`, which neither Mechanical's own
Kinds fully needed (Workspace-layer filtering already sufficed) nor
Requirements types implement at all. A real implementation would need
`Matches` retyped to something Kind-agnostic — a genuine contract
reopening, not attempted by either `WP 9.0A`/`WP 9.0B` or this Work
Package. **Recommended once cross-discipline search (a single query
spanning both Mechanical and Requirements results together) is a real,
demonstrated need** — today, each discipline's own `ProjectExplorer.FilterAsync`
scoped-to-current-area search already satisfies every named scope item
across three consecutive Work Packages.

## `FCR-0050` — Multi-Target Workspace View Refresh

`IWorkspaceCommand`'s own generic post-dispatch `RefreshAsync` call
targets exactly one `TargetObjectId`. `TD-28` (this Work Package's own
Technical Debt Assessment) discloses that the three Bulk Requirements
commands, touching many targets at once, refresh none of them
automatically. A `IWorkspaceCommand`-sibling contract carrying
`IReadOnlyList<Guid> TargetObjectIds` (or a Workspace-side "refresh
every open view matching any of these Ids" helper, callable directly by
a bulk command handler without a new contract at all) would close this.
**Recommended once a real user reports the stale-view symptom** — no
such report exists yet, and the underlying data is always correct
immediately; only an already-open view's own cached display can lag.

## Not Recommended: A Second, Dedicated "Copy Requirement" Command

Considered directly during implementation: Requirements have no
`IHasParent`-style single structural parent the way Mechanical objects
do, so "Copy under a different parent" and "Duplicate in place" reduce
to the same operation once Group is the only positional concept a
requirement carries. `DuplicateRequirementCommand`, optionally followed
by `MoveRequirementCommand`, already covers both. **Not recommended** —
building a second, near-identical command would duplicate behaviour
`WP 9.0A`'s own `Copy`/`Duplicate` split needed only because Mechanical
objects have a real single parent to distinguish "same parent" from
"different parent" against; Requirements do not.

## Verdict

One prior candidate resolved (`FCR-0039`); three new candidates recorded
(`FCR-0048`–`FCR-0050`); none built speculatively ahead of genuine need.

## Related Documents

`docs/governance/Future Capability Register.md`; `ADR-0084`; `ADR-0085`;
`WP9.0A Future Capability Assessment.md` (`FCR-0039`); `WP9.0B Future
Capability Assessment.md` (`FCR-0044`–`FCR-0047`); `WP9.1A Technical
Debt Assessment.md` (`TD-28`); `WP9.1A Engineering Review Report.md`.
