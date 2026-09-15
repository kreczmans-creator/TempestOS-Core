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
| `WP 20.3A` Persistence hygiene: issue in one transaction (B2), export schema versions migrate, BOM units are a vocabulary | Issuing evidence's sheet, record and pointer were three commits, so a crash between the first two left an Issued record with no sheet; the sheet (bytes and metadata, one transaction, `WP 17.1B`) is now attached before the issue record that names it is ever written, so a fault anywhere leaves the evidence merely Checked, never falsely Issued — `IEvidenceService.IssueAsync` gains an `attachIssueSheetAsync` callback, invoked once every refusal check has passed. `IExportSchemaMigration` and `ImportService.RegisterMigration` close ADR-0051's own disclosed reject-only gap: a section behind the registered schema version is walked forward one registered migration at a time before the exact-equality check, refusing (naming the artifact's own original version) the moment a step is missing; compression and encryption stay out of scope. `BomUnitsOfMeasure` closes ADR-0083's own disclosed gap: a small, closed, twelve-symbol vocabulary (`EA`, `SET`, `PR`, `BOX`, `ROLL`, `SHT`, `M`, `MM`, `KG`, `G`, `L`, `HR`) that `SetBomLineAsync` canonicalises against before it writes and the `UnitOfMeasure` getter canonicalises leniently on every read, so `"ea"`/`"EA"`/`"Each"` are one unit whether written today or years ago, and an unrecognised unit is refused naming the known list. | Not merged — three commits on `wp/20.3A`, a throwaway worktree off `release/v0.19.1`; never pushed. |
| `WP 20.1A1` `TD-28`: every Requirements write reaches the change bus | `RequirementsService`'s fourteen mutating methods (`CreateAsync`, `ReviseAsync`, `SetStatusAsync`, `SetOwnerAsync`, `SetPriorityAsync`, `DeleteAsync`, `MoveToGroupAsync`, `LinkAsync`, `CreateCollectionAsync`, `AddToCollectionAsync`, `DeleteCollectionAsync`, `CreateGroupAsync`, `MoveGroupAsync`, `DeleteGroupAsync`) now commit through `IQueryablePersistenceStore.ExecuteInTransactionAsync` — the only operation that advances the store's own sequence counter — and publish one `WorkspaceChange` once that transaction has committed, through an optional `IWorkspaceChangePublisher` constructor collaborator resolved to the same `WorkspaceChangeFeed` instance `EngineeringDomainContext` already uses; `MoveToGroupAsync`/`MoveGroupAsync` and `CreateCollectionAsync`/`CreateGroupAsync` now combine what were two separate non-transactional writes into one transaction each, as a natural consequence of needing a real commit to read a valid sequence number from. A docked Requirements Explorer or Cockpit, and `BulkSetRequirementStatusCommand` (which needed no publish code of its own), now see a Requirements write without re-entry. Fifteen new `Tempest.Core.Tests` (one per mutator, plus a refused-write-publishes-nothing test), the bulk-command integration test extended to assert the change bus fires once per item, and one new `Tempest.Desktop.Tests` integration test through the real `WorkspaceHost` composition root | pending |
| `WP 20.3D` CI: shard the Desktop test suite so the Build & Test ceiling could come back down | `.github/workflows/ci.yml`'s `build-and-test` matrix gains a `shard` dimension (`core`, `desktop-1`, `desktop-2`, `desktop-3`): `Tempest.Core.Tests` runs once per configuration, unsharded; `Tempest.Desktop.Tests` runs as three `--filter`-selected shards, split by the first letter of each class's own namespace segment (A-D / E-P / Q-Z, 195 / 219 / 214 of 628 Desktop tests measured by `--list-tests` on this branch head) so a future test class always lands in exactly one shard with no filter list to maintain; `Category=LayoutWalk` (5 tests) stays entirely inside the `desktop-2` shard, and the existing dedicated Release-only screenshot rerun is pinned to that one shard rather than every Release shard. Coverage collection (`WP 17.0C`/`WP 19.9.1`) stays Release-only, now per shard; the step summary is scoped to each shard's own `TestResults/<configuration>/<shard>` directory; the shipped-application and Internal Engineering Harness build-output artefacts are published from the `core` shard only, not duplicated four times, to avoid re-hitting the artefact storage quota named in that upload step's own remark; build logs and test-result artefacts do carry a shard suffix, since their content genuinely differs per shard. `timeout-minutes` returns to 45 (from 90); the CI Gate job's `needs:` already covered every matrix combination without a code change (documented, not altered). Not yet re-measured on a hosted runner — no push access from this worktree — so the first real per-shard timings belong in this table once the lead records them on the candidate. | (not yet merged — `wp/20.3D`) |

## Figures

*(re-derived at `WP 20.9.0`)*

## Warnings

- *(filled at each merge)*

## Related

- `docs/releases/v0.20.0/Execution Plan.md`
- `docs/releases/v0.19.1/Release Notes.md`
- `docs/releases/v0.19.1/Product Owner Decisions 2026-09-15.md`
- `docs/releases/v0.19.1/Technical Debt Rationalisation — Part 1.md` and `Part 2.md`
