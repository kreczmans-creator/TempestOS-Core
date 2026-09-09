# TempestOS v0.18.0 — Release Notes

**Status: release candidate in preparation on `release/v0.18.0`; figures
below are re-derived at `WP 18.9.0` before the tag.** Nothing in this
document is certification.

## Summary

**v0.18.0 is Evidence and Check** — the second release of the
[v1.0.0 Release Candidate Programme](../v1.0.0/WorkPackages.md), as
amended by [D-028](../v1.0.0/D-028%20Evidence%20is%20the%20product,%20calculation%20is%20where%20the%20engineer%20does%20it.md).
A calculation is done wherever the engineer does it; Tempest records it
as **evidence** on the project: the files, what it is about, the governed
reference records it cites at the revision held, its key figures, its
check and its issue to the client. The in-app calculation surfaces stay
as they were in `v0.17.0`, on the Product Owner's instruction of
2026-09-09; nothing new computes in Tempest.

## What a user will notice

- **Evidence** on the rail and inside every project: record a workbook,
  a drawing or a report as evidence by picking files or dropping them
  in; it opens right up.
- **Citations** pinned to released library records at the revision held;
  an unreleased record is refused and the refusal says why.
- **Declared figures** as typed quantities with a unit picker.
- **Check** with the checker's name and organisation typed in (the
  client's review, entered by hand). The independence rule (checker must
  not be the author) is built in and switched off by default; switched on
  in Settings, the same principal is refused.
- **Issue** with an issue sheet PDF attached to the record; supersession
  keeps the issued revision immutable.
- **Libraries**: the five reference libraries with their source citations,
  verified and released through one review flow.
- **Search** in the Command Palette over titles, identifiers and evidence
  references; **Recently changed** on the Home cockpit.
- The screen follows the store: no manual refresh anywhere, and the UI
  thread never blocks on persistence.
- A Part shows a read-only **Where used** and no Bill of Materials input.

## What shipped, by Work Package

| Work Package | Delivered | Merged |
|---|---|---|
| `WP 18.0A` Evidence record (ADR-0148) | `Evidence` Kind: classification, subject tag, citations with `ReferencePin`, declared figures, `Draft → Checked → Issued → Superseded`, `CheckRecord`, `IssueRecord`; `EvidenceService` with refusals as results; six `evidence.*` commands, explorer area, facets; independence rule via `Evidence:IndependentCheck` (configuration and settings) | 2026-09-09 |
| `WP 18.0B` Reference libraries with citation (ADR-0149) | `SourceCitation` on every reference record, carried forward on revise; supersession invariant proved by property test over all five libraries; all 41 seeded records cited; `R1`: every empty library is seeded at host start, so the Libraries tab opens with all 41 records (only Materials had a shipped seeding path before) | 2026-09-09 |
| `WP 18.0C` Archive P02–P07 | 32,308 lines out of `Tempest.Core` into `src/Frozen/`; kept: Organisation, Contact, Budget, Money, CurrencyCode, EffectivePeriod, RateCard, Verification, CalculationPacks, Templates and their governance roots; ADR-0127–0142 frozen | 2026-09-09 |
| `WP 18.1A` Async read surface | Store sequence advanced in the commit transaction; `IWorkspaceChanges` raised once per commit with the touched set; `WorkspaceSnapshot` reads in one read transaction (WAL snapshot isolation, proved deterministic); explorer, cockpit, inspector and editor subscribe; 19 of 27 manual refresh sites deleted (the rest are navigation and initial load, each commented); every blocking UI call removed bar one disclosed sync seam pinned by a structural guard; file-per-key `PersistenceStore` deleted, about 90 test files re-pointed | 2026-09-09 |
| `WP 18.1B` Findability | SQLite FTS5 `search_index` written inside the object-state transaction, self-healing rebuild on an empty index; Command Palette *Objects* section (asynchronous, latest query wins) opening the object right up; explorer filter matches business identifiers; cockpit *Recently changed* from the audit trail; every discipline's create command verified for placement (seven, tabled in the WP report) | 2026-09-09 |
| `WP 18.2A` Evidence workspace | *Evidence* rail area; `IFilePicker` over Avalonia's storage provider with a headless stub; `KindEditorDeclaration` registry, with Evidence, Part, Assembly and Component rendering from declarations (Part: *Where used*, no BOM input); `evidence.create-from-files` and drag-and-drop; citation, subject and declared-figure pickers; Libraries tab (Verify, Release, Add); Settings toggle for the independence rule; Evidence's own view factory (the record could not open before) | 2026-09-09 |
| `WP 18.2B` Independent check and issue sheet | Check form (name, organisation, statement, outcome, date) stored verbatim, with the independence rule refusing the author when switched on; Issue renders the sheet with SkiaSharp (A4, title and people blocks, citations and figures tables, signature block, paginated footer) and attaches it to the record; Open and Export; Revise on an issued record leaves the issued revision and its sheet immutable; subject retag after create; Libraries Revise | 2026-09-09 |
| `WP 18.9.0` Release | *(this document, the physical review, the tag)* | |

## Figures

| Measure | `v0.17.0` | `v0.18.0` |
|---|---|---|
| Live source lines (`src/`, excluding `Frozen/`) | 136,532 | *(18.9.0)* |
| Live test lines (`tests/`, excluding `Frozen/`) | 103,585 | *(18.9.0)* |
| `Tempest.Core` source lines | 85,151 | 52,843 after `WP 18.0C` |
| Core tests | 4,961 | 4,053 after wave 1 (981 archived with P02–P07) |
| Desktop tests | 508 | *(18.9.0)* |
| ADRs | 147 | 149 (0148, 0149 new; sixteen marked Frozen) |
| Commits on the release branch | 53 | *(18.9.0)* |

## What changed for a developer

- `IWorkspaceChanges` and `WorkspaceSnapshot` (`WP 18.1A`): a view takes
  one coherent read at a store sequence and reacts to the change feed;
  there are no manual refresh calls and a structural test forbids
  blocking calls in `Tempest.Desktop`.
- `PersistenceStore` (file-per-key) is deleted; `Persistence:RootPath`
  now lives on `SqlitePersistenceStore`.
- A Kind's editor is a declaration (`KindEditorDeclaration`), not a
  branch in `ObjectEditorView`; Evidence, Part, Assembly and Component
  render from theirs.
- `IFilePicker` wraps Avalonia's storage provider; the headless tests use
  a stub.
- The CI build-and-test leg has a 45-minute ceiling (was 30) and the
  advisory Linux launch smoke reads the rolling file log.

## Warnings

- **The independence rule ships switched off.** A one-person consultancy
  records the client's review by hand; switch `Evidence:IndependentCheck`
  on when a second member of staff has their own Windows account.
- **Evidence's subject is a tag.** No occurrence model, no where-used
  across assemblies, no change control: not an ERP, not a PLM (`D-028`).
- *(further warnings filled at `WP 18.9.0`)*

## Related

- [Execution Plan](Execution%20Plan.md) — how the release was built.
- [PHYSICAL_REVIEW.md](../../PHYSICAL_REVIEW.md) §7a — the evidence journey by hand.
- [v0.17.0 Release Notes](../v0.17.0/Release%20Notes.md).
