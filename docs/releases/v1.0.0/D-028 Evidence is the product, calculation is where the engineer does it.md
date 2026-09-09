# D-028 — Evidence is the product; calculation is where the engineer does it

**Decided by:** the Product Owner, 2026-09-09, in conversation with the chief engineer of record for the `v0.17.0` line.
**Status:** Decided. Applied to `docs/releases/v1.0.0/WorkPackages.md` the same day.
**Amended the same day:** the in-app calculation surfaces are **not** retired. The Product Owner: "Keep the calculation capability within the software for now. I'd rather have it in place and we can pivot and strip out later than have to build it later." `WP 18.3A` is withdrawn, decision 2 reads accordingly, `v0.18.0` is 37 developer-days and 85 remain.
**Supersedes in part:** the `v0.18.0` scope of `WorkPackages.md` as proposed on 2026-09-08 ("Calculation as Document"), and `ADR-0148`/`ADR-0149` as reserved there.

## The question

After the second Windows smoke test of `v0.17.0`, the Product Owner asked:

> Is this the place for doing calculations? Or should they be done in separate software (an Excel workbook for example) and the evidence then loaded into Tempest as a record of that? Should Tempest be a dedicated customer project management system that we then tag evidence to rather than trying to be too much all at once?

and, on the object model the same day:

> We need to be very careful here not to reinvent the system as an ERP system or a PLM system. This is not the intent.

## The decision

1. **TempestOS `v1.0` is a client project system of record for an engineering consultancy, that evidence is tagged to.** Calculations are done wherever the engineer does them today (a workbook, a hand sheet, a package). Tempest records the result as evidence: the files, what it is about, the governed reference records it cites at the revision held, its key figures, its independent check and its issue to the client. **Nothing new computes in Tempest in `v1.0`.**
2. **`v0.18.0` is "Evidence and Check", not "Calculation as Document".** The expression grammar, the cell-grid editor and the run-by-run diff are not built. The in-app calculation surfaces (the *Engineering Calculations* rail entry, the *Calculations* discipline tab, the template registry and its JSON execute path) stay in the software as shipped in `v0.17.0`, unextended, so they can be stripped later if the evidence model makes them redundant rather than rebuilt if it does not (amendment of 2026-09-09).
3. **TempestOS is not an ERP and not a PLM system.** The product structure stays a single-parent tree used as tags; there is no part-occurrence model, no multi-assembly usage tracking, no change control on lines, no procurement, supplier, cost or stock field, no workflow engine. An attribute earns its place only if a calc sheet cites it, a drawing's title block shows it, or the invoice seam needs it.
4. **Every substrate stays.** The transactional SQLite store, the dimension-vector units, governed reference data with revision pins, identity and audit, and the command framework are what the evidence record is built on. This decision changes what is built on them, not them.

## Why

- The design-freeze review of 2026-09-08 found the substrates sound and the surfaces composed from platform primitives rather than from the user's questions. About a third of the remaining programme (34 developer-days) was a calculation tool that would compete with Excel, Mathcad and SMath on the engineer's own desk, and it contained the programme's highest-risk Work Package (a frozen expression grammar written before its parser).
- Every capability a consultancy needs from evidence (attached files as verified bytes, governed references pinned to a released revision with an unreleased one refused, an independent checker who cannot be the author, an issue record, an audit row for each act) already exists in the substrate and needs no engine.
- The first thing the Product Owner saw once a created object opened was that a Part carried none of the data an engineer would put on it; the first proposal to fix that drifted toward PLM within an hour. An evidence-centred definition makes the object graph a set of tags and removes that drift structurally.

## Consequences for the programme

| | Before (2026-09-08) | After (`D-028`) |
|---|---|---|
| `v0.18.0` | Calculation as Document, 42 days, critical path through a frozen grammar | Evidence and Check, 37 days, including Findability and the archive pulled forward |
| `v0.19.0` | 36 days | 34 days (the archive moved to `v0.18.0`); the rail is Home, Projects, Evidence, Timesheets, Invoicing, Reports, Settings |
| `v1.0.0` RC | 14 days | 14 days; golden-example coverage becomes evidence-journey coverage |
| Remaining after `v0.17.0` | 106–109 developer-days (review §5.2) | 85 developer-days |
| Critical path | `17.1A → 17.1B → 18.0A → 18.2A → 18.2B → 18.3A → 19.0A → 19.1A → RC.0A → RC.0E` | `17.1A → 17.1B → 18.0A → 18.2A → 18.2B → 19.0A → 19.1A → RC.0A → RC.0E` |
| `TD-174`, `TD-175` | A Part-model Work Package (`18.1C`, 5 days) | Dissolved into `18.0A` (material is cited on evidence) and `18.2A` (Part shows *Where used*, no BOM input) |

## What this decision does not do

It does not remove the calculation engine, its surfaces, the governed bracket check or their tests from the build; it stops investing in them. It does not change any ADR from `0001` to `0147`; `ADR-0148` and `ADR-0149` are re-scoped before they are written. It does not change `v0.17.0`, which the Product Owner accepted by smoke test the same day and which is merged to `main`, tagged and published as built.
