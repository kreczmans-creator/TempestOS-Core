# TempestOS v0.21.0 — Release Notes

**Status: in preparation on `release/v0.21.0`, cut from the `v0.20.0`
candidate (`88311649`) on 2026-09-15 at the Product Owner's instruction
to close every technical weakness named in the lead's assessment of that
afternoon.** Nothing in this document is certification. `v0.20.0` stays
under the Product Owner's manual test as the candidate, receiving the
`WP 20.10A`–`20.10G` fixes; every one of those is merged here too.

## Summary

**v0.21.0 is the recovery tranche** — the docking rewrite to `ADR-0153`,
Undo across commands, the editor split, documents from every template,
the Engineering Assets surfaces, typed calculation results with retained
inputs, the commercial edges, the viewer's remaining formats and markup,
the installer with upgrade, backup and restore, lazy rehydration, a
real-shell run in CI, and the mutation threshold met. See
`Execution Plan.md` for the packages and their waves.

## What shipped, by Work Package

| Work Package | Delivered | Merged |
|---|---|---|
| `WP 21.5B` `TD-88` — lazy, project-scoped materialisation over the index, closed | `EngineeringObjectRehydrationService.RehydrateAsync` no longer reconstructs the estate unconditionally: for every state with a known Kind and an existing document (the one document read that stays eager — a lightweight existence check, no revision content, needed for orphan detection), it registers the object lazily (`IEngineeringObjectRepository.RegisterLazy`), deferring revision-content reads and the rehydrator's own type-specific parsing to first access. `FindAsync` is the single-flight materialising loader — an id materialises once no matter how many callers ask concurrently, and joins the identity map permanently (no eviction shipped). `ListAllAsync`/`ListByKindAsync`/`ListChildrenAsync` answer `EngineeringObjectIndexEntry` rows from the index alone, computed live for a materialised object so a rename or delete is never stale. Every one of `WP 20.1C2`'s own named ~seventy/eighty callers across `Tempest.Core`/`Tempest.Workspace`/`Tempest.Desktop`/`Tempest.Samples` now either reads the index type directly (the compiler proves it needs nothing else) or materialises explicitly through the new `EngineeringObjectRepositoryExtensions.MaterialiseAsync<T>` seam before touching a type-specific field — no caller keeps an untyped "list then cast". Opening a project materialises its own subtree eagerly (`IEngineeringObjectRepository.MaterialiseSubtreeAsync`, wired at `ProjectContext.OpenAsync`/`LoadAsync`); closing one releases nothing. **Kill switch** (named, bounded): `WP 20.1A2`'s business-identifier index rebuild still materialises every enforced-Kind object eagerly inside `RehydrateAsync` — `BusinessIdentifier` is a computed projection absent from the index row, and `BusinessIdentifierScope.ResolveProjectId`'s own synchronous repository read (outside this Work Package's files, the business-identifier index's contract not to be touched) depends on an already-materialised ancestor chain — bounded to the enforced-Kind set, never the unenforced majority a large estate is mostly made of. See the `TD-88` row (`BACKLOG.md`, "Closed") for the full measured figures and their own disclosed caveat: `IndexBuilt`/project-open/first-read all improved (a 10,000-object estate: ~387 ms/~55 ms (300-object subtree)/~1.4 ms respectively); `RehydrateAsync` returning as a whole measured slower in raw total than `WP 20.1C2`'s own ~190 ms/1000 figure, dominated by relationship rebuilding this Work Package left unchanged and did not benchmark past 1,000 objects before. Three new tests (single-flight, identity-map preservation, a rehydrated revision chain read from its newest end) plus a benchmark; `tests/Tempest.Core.Tests` (4,673) and `tests/Tempest.Desktop.Tests` (660) green in Debug; both configurations build clean, warnings as errors; governance 5/5. | *(pending merge)* |

## Figures

*(re-derived at `WP 21.9.0`)*

## Warnings

- *(filled at each merge)*
- **Rehydration's own kill switch, `WP 21.5B`**: `WP 20.1A2`'s
  business-identifier index rebuild still materialises every enforced-Kind
  object (Part, Calculation/CalculationSet, Document/Drawing/CadModel, the
  three Manufacturing Kinds, VerificationActivity, Evidence) eagerly inside
  `RehydrateAsync`, because `BusinessIdentifier` is a computed projection
  not on the index row and `BusinessIdentifierScope.ResolveProjectId`'s own
  synchronous repository read (outside this Work Package's files) needs an
  already-materialised ancestor chain. Bounded to that Kind set, not the
  unenforced majority a large estate is mostly made of. Separately, the
  benchmark's own `RehydrateAsync`-returning figure (~514 ms/1000 objects)
  reads slower in raw total than `WP 20.1C2`'s own ~190 ms/1000 — disclosed
  rather than hidden: it is dominated by relationship rebuilding, which
  this Work Package left fully eager and unchanged (id-keyed, not
  materialisation), and which was never benchmarked past a 1,000-object
  estate before now.

## Related

- `docs/releases/v0.21.0/Execution Plan.md`
- `docs/releases/v0.20.0/Release Notes.md`
- `docs/adr/ADR-0153-tear-out-and-dock-everywhere-across-monitors.md`
