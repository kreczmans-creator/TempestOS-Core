# TempestOS v0.20.0 — Release Notes

**Status: in preparation on `release/v0.20.0`, cut from the `v0.19.1`
candidate (`4f5ee83a`) on 2026-09-15 at the Product Owner's instruction
to close the P1–P3 technical debt before the release rather than after
it.** Nothing in this document is certification. `v0.19.1` stays as
pushed and gated as the fallback candidate.

## Summary

**v0.20.0 is the debt tranche** — every item the technical-debt
rationalisation of 2026-09-14 rated P1 to P3 that a gate can prove,
plus the two Product Owner definitions of 2026-09-15 (payment terms per
client; a calculation is a task from creation) and the draft ADR for
tear-out and dock everywhere. No new surface beyond what those closures
require.

## What shipped, by Work Package

| Work Package | Delivered | Merged |
|---|---|---|
| `WP 20.1C2` `TD-88` — index-first rehydration, kill switch invoked | `EngineeringObjectRehydrationService.RehydrateAsync` builds a deterministic `EngineeringObjectIndexEntry` (id, Kind, Identifier, DisplayName, ParentId, Status, IsDeleted) for the whole persisted estate straight from `EngineeringObjectState`, then raises `IndexBuilt` with it before any document is read or object materialised — the hook `WP 20.1A2`'s business-identifier index rebuild runs from. Full materialisation stays eager: dozens of existing read-surface callers (`EngineeringCockpit.PrimeAsync`, `InvoicingService.ListCarriedSourcesAsync`, `MechanicalPropertyFacetProvider.GetBaselineDisplayAsync` and roughly sixty more) read full, type-specific object state directly off `ListAllAsync`/`ListByKindAsync`/`ListChildrenAsync` results with no intervening `FindAsync`, all outside this Work Package's files-you-own list, so deferring materialisation there could not be proven behaviourally equivalent in scope — `TD-88` stays open, not closed. A measured 1,000-object/10-project estate showed no material wall-clock change (~185ms before and after over three runs each) because the unchanged eager materialisation loop still dominates the cost. Four new tests pin the index stage (fields populated before materialisation, an unregistered Kind still indexed, determinism across two startups, no-subscriber behaviour unchanged); every existing `ProductionRehydrationTests`/`EngineeringObjectRehydrationTests`/`RevisionRehydrationEquivalenceTests`/`ObjectRehydrationAcceptanceTests` fact passes unchanged | *(pending merge)* |

## Figures

*(re-derived at `WP 20.9.0`)*

## Warnings

- *(filled at each merge)*

## Related

- `docs/releases/v0.20.0/Execution Plan.md`
- `docs/releases/v0.19.1/Release Notes.md`
- `docs/releases/v0.19.1/Product Owner Decisions 2026-09-15.md`
- `docs/releases/v0.19.1/Technical Debt Rationalisation — Part 1.md` and `Part 2.md`
