# TempestOS v0.20.0 — Release Notes

**Status: in progress — this file is started by `WP 20.1A1` alone, on its
own worktree/branch off `release/v0.19.1` head `4f5ee83a`; several other
Work Packages (`WP 20.0A`, `20.0B`, `20.1A2`, `20.1B`, `20.2B`, `20.3A`,
among others) are running concurrently on their own worktrees tonight
and have not yet merged. The lead reconciles this document — the table
below, the Figures and the Gate section — once every Work Package's
branch lands.** Nothing in this document is certification.

## Summary

v0.20.0 continues the technical-debt closure the `v0.19.1` overnight
audit (`docs/releases/v0.19.1/Technical Debt Rationalisation — Part 1
and 2.md`) scoped for the next tranche, alongside the Product Owner's
tear-out/docking and document-template work. This entry covers only
`WP 20.1A1`; the rest of the table is the lead's to complete from the
other Work Packages' own reports.

## What shipped, by Work Package

| Work Package | Delivered | Merged |
|---|---|---|
| `WP 20.1A1` `TD-28`: every Requirements write reaches the change bus | `RequirementsService`'s fourteen mutating methods (`CreateAsync`, `ReviseAsync`, `SetStatusAsync`, `SetOwnerAsync`, `SetPriorityAsync`, `DeleteAsync`, `MoveToGroupAsync`, `LinkAsync`, `CreateCollectionAsync`, `AddToCollectionAsync`, `DeleteCollectionAsync`, `CreateGroupAsync`, `MoveGroupAsync`, `DeleteGroupAsync`) now commit through `IQueryablePersistenceStore.ExecuteInTransactionAsync` — the only operation that advances the store's own sequence counter — and publish one `WorkspaceChange` once that transaction has committed, through an optional `IWorkspaceChangePublisher` constructor collaborator resolved to the same `WorkspaceChangeFeed` instance `EngineeringDomainContext` already uses; `MoveToGroupAsync`/`MoveGroupAsync` and `CreateCollectionAsync`/`CreateGroupAsync` now combine what were two separate non-transactional writes into one transaction each, as a natural consequence of needing a real commit to read a valid sequence number from. A docked Requirements Explorer or Cockpit, and `BulkSetRequirementStatusCommand` (which needed no publish code of its own), now see a Requirements write without re-entry. Fifteen new `Tempest.Core.Tests` (one per mutator, plus a refused-write-publishes-nothing test), the bulk-command integration test extended to assert the change bus fires once per item, and one new `Tempest.Desktop.Tests` integration test through the real `WorkspaceHost` composition root | pending |

## Gate — `WP 20.1A1`'s own worktree (`D:/tempest-wt/20.1A1`, branch `wp/20.1A1`)

- Build: 0 warnings, 0 errors, Debug and Release, `TreatWarningsAsErrors`
- Core tests: 4,465 passed, 0 failed, Debug and Release
- Desktop tests: pending in this document — see `WP 20.1A1`'s own report for the run in progress at the time this was written
- Governance health check: pending in this document — see `WP 20.1A1`'s own report

## Warnings

- **This file is a per-Work-Package draft, not a reconciled release
  document.** Only `WP 20.1A1`'s own row, and its own gate figures, are
  filled in; every other Work Package running concurrently tonight owns
  its own report and has not yet contributed here. The lead assembles
  the final version once every branch merges.

## Related

- `docs/releases/v0.19.1/Release Notes.md`
- `docs/releases/v0.19.1/Technical Debt Rationalisation — Part 1.md`
- `docs/releases/v0.19.1/Technical Debt Rationalisation — Part 2.md`
- `BACKLOG.md`
