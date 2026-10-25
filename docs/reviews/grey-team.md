# ⚪ Grey team — integration and upgrade

| Item | Evidence | Status |
|---|---|---|
| Migrate a database containing issued Evidence; it opens, stays immutable, new register links are correct | `UpgradeTests.A_Database_With_Issued_Evidence_Opens_Unchanged_And_Accepts_New_Calculations` (Library, real SQLite file in a temp root): phase A issues Evidence through the pre-calc two-step `IssueAsync`; phase B reopens the file with the v0.26.0 code and rehydrates every kind as the product does at start-up — the legacy record is Issued with its reference, a second issue is refused and leaves it unchanged, a calc instance is created in the same project, checked, issued in one transaction and linked `documentedBy` from its Calculation register object; phase C reopens again and finds both persisted. | Pass |
| Schema version bump | None needed: `SqlitePersistenceStore.SchemaVersion` stays 1; calc instances and packs are JSON documents in two new collections; no migration script (D-26). The existing pre-migration backup path (`BackupServiceTests`) is untouched. | Pass (no change) |
| Linux build | Every phase gate built the solution with 0 warnings; CI Linux jobs (Launch Smoke) green on every release push. | Pass |
| Windows build | CI runs `Build & Test` on `windows-2022`: Debug and Release builds succeed on every release push; two Windows-only test failures (CRLF sheet assertion; PDFs 650 KB because the platform default font was embedded whole) are fixed on the Phase 6 branch (LF everywhere; Body face subset). The verdict comes from the CI run on the next `release/v0.26.0` push and is recorded in STATUS. | Pending CI |
| Crash recovery after a kill during Issue | Fault injection at the commit boundary: `IssueTransactionFaultInjectionTests` (Core: the sheet bytes, attachment, issue record and status commit together or not at all), `Crash_Mid_Issue_Leaves_Instance_Checked_And_Evidence_Checked` and `Pack_Crash_Mid_Issue_Leaves_Every_Member_Unissued` (Library); the race variant `ConcurrentIssueTests` / `ConcurrencyTests`. SQLite WAL journalling (existing) covers a process kill between statements. | Pass |
| Performance baselines before / after | Core suite 7,202 → 7,221 tests, wall time unchanged (≈ 65 s); Desktop suite 1,150 → 1,184 (≈ 13–17 min on this 2-core container, same as the v0.25 baseline run at the start of the build); new budgets enforced by `PerformanceBudgetTests`: every sheet < 1 s (16 templates), 10,000-sample Monte Carlo < 2 s, 510-element frame < 1 s (measured 47 ms). | Pass |
| Reports path opens an issued calc sheet; page < 200 KB | `WorkedExamplePdfTests` renders all 24 examples through the Desktop renderer: max 187 KB, mean 168 KB on Linux; the issued sheet is a normal Evidence attachment the Reports view already lists. | Pass (Linux); Windows pending CI |

Findings: GR-01 (Low, Open until CI): the Windows PDF size depends on DirectWrite accepting the subset fonts (RR-01). GR-02 (Info): the upgrade test rehydrates with the Workspace registries plus the Evidence kind, mirroring `EngineeringWorkspaceComposer`; if the composer's kind list grows, the test's list must follow.

## Addendum A

Checked at `v0.26.0/addA-9-blue` (`efa30499`) against `6afead3`. v0.26.0 is not released and the `rc1` tag is not pushed (DA-01), so the "stored instances" below exist only in development and owner-test databases. Evidence is from the code and the git diff; the Windows and Linux CI verdict for this branch is not yet in.

| Item | Evidence | Status |
|---|---|---|
| Template version bumps | Seven templates that were in `6afead3` were raised: `bs.pipe-pump` 1.0.0 → 1.2.0, `elec.enclosure-heat` 1.0.0 → 1.2.0, `elec.motor-sizing` 1.0.0 → 1.2.0, `mech.bolt-group` 1.0.0 → 1.1.0, `mech.iso286-fit` 1.0.0 → 1.1.0, `struct.frame-2d` 1.0.0 → 1.1.0, `struct.en1993-member` 1.0.0 → 1.0.1 (`git diff` on the `*Calculator.cs` files). The twelve new templates (M4, M5, M6, S4, S5, S6, B4, B5, B6, E4, E5, E6) first appear in this release at versions from 1.0.0 to 1.2.0, so no released database holds an older one. | Pass |
| A stored draft or checked instance of an old version | The catalogue registers one version per template. `CalcCatalogue.Find("id@1.0.0")` returns nothing once 1.2.0 is registered. The instance is shown as stale ("has a newer version"). Edit, Check and Issue are refused with "Template '…' is no longer registered." So the old instance no longer runs. A draft has no upgrade path inside the product: Revise accepts only an issued calculation, or a checked one that has gone stale. The route is to export its inputs (CSV or JSON) and import them into a new sheet. No test pins this behaviour. | Pass with a gap (GR-A-01) |
| A stored issued instance of an old version | It stays immutable and readable: the issued sheet is stored text, with the template hash, engine version and known-answer result of the day. Revise is allowed and starts the next revision on the current template version (`ReviseCoreAsync` calls `Get(instance.TemplateId)`). Staleness names the newer version. | Pass |
| Stale propagation after a version bump | A downstream instance of an upstream on an old version is stale at every depth (Red R-A-03, fixed in Blue round 1). `StalenessForProjectAsync` answers for a whole project in one pass (R-A-07). Both are tested in `BlueRoundTests`. | Pass |
| Legacy bolt-shear values (ADR-0169) | The v0.25.2 `calc.bolt-shear-capacity` module is scoped as the shank-in-plane case (gross area, α_v 0.6 for every class). Picker values for existing classes are unchanged (4.6 → 240, 5.6 → 300, 8.8 → 480, 10.9 → 600, 12.9 → 720 MPa), so stored picks still match their labels. Classes 4.8, 5.8 and 6.8 are added. Only the labels and the stated scope changed, in two Core files (13 lines added). The threads-in-plane case is the S4 worksheet. | Pass |
| New sheets open on declared defaults | `CalcTemplateHasher.NewSheetInputs` takes a Quantity or Number `Default`, a Choice `DefaultChoice` and a Boolean, and leaves tables and anything else blank. The Library service and the Desktop form use it (G-A-02, G-B-06). `DefaultInputs` is kept for the template hash, known-answer runs and the derived ports, so the hash does not move for this reason. Stored instances keep the inputs they were saved with. Eleven templates now open blocked on a new sheet until the user supplies a table or a required value; six of them predate Addendum A (P-A-05). | Pass; UX gap (P-A-05) |
| Shaft density on a new sheet | A new shaft sheet opens at 7850 kg/m³, not 0 (the first test case's value). A new wet-heating pump motor opens at its declared 6000 rpm. Stored shaft instances keep ρ as saved. | Pass |
| Database | No schema change. `SqlitePersistenceStore.SchemaVersion` is untouched. Two new collections are used (`Calc.PackTemplates`, `Calc.DataRecords`). The instance key layout (`<project>/<id>`) is unchanged, so listing by key prefix reads the keys already stored. New record fields are optional: `EditedBy` is null on old instances and `Authors` falls back to the creator. `UpgradeTests` (phase 6) is unchanged. | Pass |
| Packs | Built-in pack templates are code (`CalcPackTemplates.All`), composed into the Desktop by `CalcPackTemplateComposition`. Stored pack templates (saved from a chain) stay readable: `CalcPackStep.Example`, `Omit`, `Hint` and `CalcPackBinding.Row`/`Column` are optional and nulls are not written. Saved packs keep their members. A checked pack can now be reopened as a draft under the next pack revision with new Evidence; the earlier check stays on the earlier Evidence (DA-23). A pack refused at instantiation now stores nothing (dry run, then write). | Pass |
| Instance and record JSON | Cell provenance is optional and null is not written, so an instance without cell links serialises as before. Cell provenance is in no hash (ADR-0168 amendment 2). Record revisions are never edited, and a licensed record's later revisions stay licensed (DA-17). | Pass |
| CSV compatibility | A CSV written before Blue round 1 has one blank row per table input and no rows. It still imports: the table comes back without rows, and nothing is invented. The new export writes `name`, `name[r]`, `name[r].column` with each cell's unit and provenance (cell links included). A gap in row numbers, a bad number or a formula-like cell blocks the import. A value or table withheld on export comes back withheld, with a warning. Proved over every worked example (`CsvRoundTripTests`, 44/44). Gap: a licensed scalar withheld on export re-imports as the template default (P-A-01). | Pass; one gap (P-A-01) |
| JSON compatibility | The 32 earlier worked examples' `inputs.json` files are unchanged in git. 17 new `inputs.json` files were added (12 new calculators' examples, five packs). A JSON flag written as text `"no"` is still refused (B-A-20(b)). Pack `inputs.json` is an object keyed by step; it is not a single-sheet import (B-A-22(a), README wording still to be fixed). | Pass; two open items |
| Worked examples and PDFs | Fourteen sheets and PDFs of the earlier examples were regenerated, two for each of the seven templates whose version moved (the sheet footer carries the template version). Their inputs did not change. Twelve new examples and their PDFs were added. Known answers still pass. The per-sheet PDF test skips pack folders. | Pass |
| Reports path and PDF size | The pack issue sheet and each member's own sheet open from Reports as normal Evidence attachments. A member's own sheet now shows the pack's checker and client (B-A-13). Licensed table rows are withheld from every sheet, preview and PDF (R-A-01). The Windows PDF size check (RR-01) still waits for the CI run. | Pass; Windows pending |
| Event bus and project lists | Staleness events publish on update and supersede only. The Desktop relist reads one project's keys, with no whole-collection scan (R-A-07). A pack holds the calculations of one project; the no-project list is its own (R-A-12). | Pass |
| Test totals | At the last build gate (`LOG.md`): Engine 384, Sheets 93, Calculators 721, Library 132 (+1 skipped), Core 7,230 (+15 skipped), Desktop 1,221; 0 failures; 0 warnings. Purple ran filtered suites after Blue round 1 (Library 113, Sheets 33, Calculators `CsvRoundTripTests` 44). The full gate follows Blue round 2. | Pass (build gate); full gate pending |
| Windows build and CI | Not run for Addendum A in this review. The `windows-2022` run on the next push of `release/v0.26.0` decides. | Pending CI |

Findings:

- GR-A-01 (Low, Open): a draft on an old template version cannot be edited, checked or revised. Add a "start a new sheet from these inputs" action to the stale marker, or let Revise take a draft. Add a test that pins the refusal text.
- GR-A-02 (Low, Open): `Revise` calls `_catalogue.Get(TemplateId)`. A template removed from the catalogue (not renamed) would throw instead of refusing. No template is removed in this release.
- GR-A-03 (Info): when v0.26.0 merges forward into `release/v0.27.0` (DA-02), three templates exist in both (`mech.bearing-life`, `bs.plane-wall`, `struct.fillet-weld`). Their ids and versions must be compared before the merge, or stored instances will point at the wrong version.
- GR-A-04 (Low, Open): P-A-01 and P-A-05 are the two upgrade-visible defects of Blue round 1. Both are in the round 2 brief.

## Addendum B

Checked at `34f196cc` (`v0.26.0/addB-4-workflows`, not yet merged into `release/v0.26.0`) against `510f337c` (end of Addendum A). No v0.26.0 tag exists (DA-01). The "stored instances" below exist only in development and owner-test databases. S8: reviewed separately before B-5 merges.

| Item | Evidence | Status |
|---|---|---|
| Template version bumps | 14 new templates (versions 1.0.1 to 1.3.0). Seven earlier ones raised: `bs.pipe-pump` 1.5.0, `elec.motor-sizing` 1.4.0, `elec.enclosure-heat` 1.4.0, `elec.fault-protection` 1.2.0, `bs.psychrometrics` 1.1.0, `bs.room-heat-loss` 1.2.0, `mech.tolerance-stack` 2.1.0. `bs.sealed-system` moved onto the gas-law kernel with identical output and kept 1.2.0 (B-0 gate: B6 tests unchanged). | Pass; drafts on the old versions stop (GR-B-01) |
| Database | No schema change. Two new collections: `Calc.ImportProfiles` and `Calc.ScanProfiles` (versioned, immutable). Import records live on the instance, capped at 20, and are kept while an input cites them. Record values gain an optional `Source` (source, edition, page, licence class, entered by, date, evidence SHA-256); old records have none and stay unlicensed unless flagged. `UpgradeTests` unchanged. | Pass |
| Material library (W3) | Materials are reference records of kind `material.properties`. Four generic seeds are written on first use, idempotent and indicative. A pin `record:<id>@<n>` goes stale on a new revision. Exports withhold licensed values. A material licensed from any revision is locked in every revision (SEC-B-02, fixed in `051647fb`). | Pass |
| New discipline | Fluid Power (ADR-0170): `Tempest.Calc.FluidPower` and its test project, `DisciplineCatalogue`, the explorer module (37 modules pinned), the Desktop composition. The Core change is a comment only. Dashboard export v3 gains a fourth `disciplines` entry with no schema bump (GR-B-05). | Pass |
| New assemblies and CI | `Tempest.Calc.Import` and `Tempest.Calc.Tests.Import`. Both new test projects run on both CI legs (`ci.yml:309, 331`). Licence Check job, required by the CI Gate (DB-08). `Svg.Custom` MS-PL flagged for the owner. | Pass |
| Import files | Read from picker streams only. 5 MB per file; W2 up to 64 files and 20 MB. Nothing is written beside the source. Profiles are stored in the database, not in a folder. No import is logged (GR-B-02). | Pass; logging gap |
| Feature flags (S8) | On the S8 branch the flag is read only from `appsettings.json` in the install folder (`Calc:Features:Lifting`); off by default. S8: reviewed separately before B-5 merges. | Pending B-5 |
| Test totals at `6a189078` (verification, Linux, 4 cores) | Engine 457, Sheets 135, Calculators 1515, FluidPower 94, Import 625, Library 343 (+1 skipped): all pass. Desktop `WorkedExamplePdfTests` 64 of 64 (Y-B-01 fixed; 8 PDFs stale again, Yellow Y-B-12). At `34f196cc` the Calculators suite failed one timing test under load (Y-B-11, fixed) and the PDF test failed 4 (Y-B-01, fixed). Core and the full Desktop suite were not run. | Pass |
| Performance | `PerformanceBudgetTests` covers every registered template, Fluid Power included. Worst Addendum B cases (Security): F3 at 100 000 samples 0.6 s, M9 at 3600 steps 48 ms, S9 Navier at the 1024-term cap 21 ms. | Pass |
| Worked-example PDFs | 16 new PDFs, the largest 189 KiB (LTB, GR-B-06). The four M9/E9 PDFs are missing (Y-B-01). | Pass (Linux) with one gap |
| Windows build and CI | Last Windows green: `510f337c` (Addendum A). Not run for Addendum B. | Pending CI |
| STATUS | Final line still "READY FOR OWNER TESTING" while Addendum B is 14/19 (GR-B-03). | Open |

Findings:

- GR-B-01 (Low, Closed by `d1141f45`: a draft on an older version opens as a new sheet on the current one): seven Addendum A templates were raised again, so Addendum A owner-test drafts on them stop running. Fix GR-A-01, or tell the owner to start new sheets.
- GR-B-02 (Low, Open: fix now if the Desktop logger is at hand, otherwise record as accepted): CAD and scan imports are not logged. Log the refusal code, file kind, size and a hash prefix; never content.
- GR-B-03 (Low, Open: fix now, one line): STATUS's final line should say Addendum B is in progress until §B9 is met.
- GR-B-04 (Info, accepted): the material seeds are written on first use; the `Indicative` origin cannot be read by an older build (no downgrade path, as before).
- GR-B-05 (Info, accepted): the dashboard export gains a Fluid Power entry in `disciplines` with no schema version change.
- GR-B-06 (Info, open until the stale LTB PDF is regenerated and Windows runs): the LTB PDF is 189 KiB against the 200 KiB cap; watch the Windows run (GR-01).

### Verification at `6a189078`

| ID | Fix | Verdict |
|---|---|---|
| GR-B-01 | `d1141f45`: a draft on an older template version opens as a new sheet on the current one, with its inputs and a clear message; the old draft is left unchanged. | Verified: `CalcInstanceServiceTests.Grey_GR_B_01_*`, `WorksheetSavedAndPacksTests.Grey_GR_B_01_*`. Closed; this also covers GR-A-01 for drafts. |
| GR-B-02 | — | Open: no logging added. |
| GR-B-03 | — | Open: STATUS's last line still reads "READY FOR OWNER TESTING", and its header "Addendum B: 14/19". |
| GR-B-06 | — | Open: the LTB PDF is one of the 8 stale ones (Yellow Y-B-12); recheck its size after regeneration. |

Test runs at `6a189078` (Linux, 4 cores) are in `scratchpad/b9colour/verify-suites.log`. Core and the full Desktop suite were not run.

S8: reviewed separately before B-5 merges.
