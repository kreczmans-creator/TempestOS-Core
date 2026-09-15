# TempestOS v0.20.0 — Release Notes

**Status: in progress — created by `WP 20.3A` on its own worktree
(`D:/tempest-wt/20.3A`, branch `wp/20.3A`, off `release/v0.19.1`) because
no `docs/releases/v0.20.0/` directory existed yet. Shaped after
`docs/releases/v0.19.1/Release Notes.md`; only `WP 20.3A`'s own row is
filled in below — every other section (Summary, the rest of What
Shipped, Figures, Gate, Warnings) is this Work Package's own tranche's
to write as its Work Packages land, not this one's alone to complete.**
Nothing in this document is certification.

## Summary

*(To be written once more of the `v0.20.0` tranche has landed — a single
Work Package's own worktree is not positioned to summarise a whole
release.)*

## What shipped, by Work Package

| Work Package | Delivered | Merged |
|---|---|---|
| `WP 20.3A` Persistence hygiene: issue in one transaction (B2), export schema versions migrate, BOM units are a vocabulary | Issuing evidence's sheet, record and pointer were three commits, so a crash between the first two left an Issued record with no sheet; the sheet (bytes and metadata, one transaction, `WP 17.1B`) is now attached before the issue record that names it is ever written, so a fault anywhere leaves the evidence merely Checked, never falsely Issued — `IEvidenceService.IssueAsync` gains an `attachIssueSheetAsync` callback, invoked once every refusal check has passed. `IExportSchemaMigration` and `ImportService.RegisterMigration` close ADR-0051's own disclosed reject-only gap: a section behind the registered schema version is walked forward one registered migration at a time before the exact-equality check, refusing (naming the artifact's own original version) the moment a step is missing; compression and encryption stay out of scope. `BomUnitsOfMeasure` closes ADR-0083's own disclosed gap: a small, closed, twelve-symbol vocabulary (`EA`, `SET`, `PR`, `BOX`, `ROLL`, `SHT`, `M`, `MM`, `KG`, `G`, `L`, `HR`) that `SetBomLineAsync` canonicalises against before it writes and the `UnitOfMeasure` getter canonicalises leniently on every read, so `"ea"`/`"EA"`/`"Each"` are one unit whether written today or years ago, and an unrecognised unit is refused naming the known list. | Not merged — three commits on `wp/20.3A`, a throwaway worktree off `release/v0.19.1`; never pushed. |

## Figures

*(Deferred to whichever Work Package closes out the `v0.20.0` candidate
— re-derived once, over the whole tranche, the way `WP 19.9.1` did for
`v0.19.1`, not incrementally by each Work Package.)*

## Gate on `WP 20.3A`'s own worktree head

- Build: 0 warnings, 0 errors, Debug and Release, `TreatWarningsAsErrors`
- Core tests: 4,496 passed, 0 failed, Debug and Release
- Desktop tests: 628 passed, 0 failed, Debug (18 m 20 s)
- Governance health check: 5 of 5

## Warnings

- None newly disclosed by `WP 20.3A` itself; see the `ADR-0051` and
  `ADR-0083` addenda for the two closed gaps' own full account and the
  `BACKLOG.md` closure entry naming `B2` alongside them.

## Related

- `docs/releases/v0.19.1/Release Notes.md`
- `docs/adr/ADR-0051-export-import-is-orthogonal-to-the-internal-persistence-abstraction.md` (addendum, `WP 20.3A`)
- `docs/adr/ADR-0083-bom-line-is-a-fourth-additive-facet-unit-of-measure-is-a-plain-string.md` (addendum, `WP 20.3A`)
- `BACKLOG.md` (closure entry, `WP 20.3A`)
