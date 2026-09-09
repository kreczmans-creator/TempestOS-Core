# v0.18.0 Evidence and Check — Execution Plan

**Written by:** the chief engineer of record, 2026-09-09, the day `v0.17.0`
was accepted and released.
**Governs:** how the seven Work Packages of `docs/releases/v1.0.0/WorkPackages.md`
§`v0.18.0` (as amended by `D-028`) are executed as one set of work, by
sub-agents at the lowest useful model, to a release candidate the Product
Owner can test by hand.
**Method:** the `v0.17.0` method. One explicit worktree per Work Package
off `release/v0.18.0`, a compiling checkpoint committed early, the full
gate at every merge, `--no-ff` merges by the lead, findings and warnings
reported as they arise, and a kill switch on every Work Package.

## 1. What the Product Owner gets

A build titled `TempestOS 0.18.0 (<commit>)` in which:

1. The rail has an **Evidence** entry. The *Engineering Calculations*
   entry and the *Calculations* tab stay exactly as in `v0.17.0`
   (Product Owner, 2026-09-09: keep the capability in place; `WP 18.3A`
   withdrawn).
2. Inside a project, **Evidence** lists every piece of evidence with its
   classification, subject, status, checker and issue reference.
   **Create** opens a real file picker (or accepts a dragged file), asks
   for classification and an optional subject, and opens the new record
   right up.
3. The record shows its files, its **citations** (picked from released
   library records only, pinned to the revision held), its **declared
   figures** (typed quantities with a unit picker), its lifecycle and
   its audit trail. A Part shows a read-only *Where used* and no Bill of
   Materials input.
4. The **Libraries** tab of Evidence lists the 41 seeded reference
   records (materials, fasteners, bearings, standards, constants) with
   their source citation, and lets a principal with the permission verify
   and release them through the one review flow.
5. A check can be recorded with the checker's name and organisation typed
   in (the client's review, entered by hand). The independence rule
   (checker ≠ author by identity id) is built in and switched off by
   default; switched on, the same principal is refused. **Issue** produces
   an issue sheet PDF attached to the record and openable from it;
   supersession keeps the issued revision immutable.
6. Global search in the Command Palette finds objects and evidence by
   title, identifier and evidence reference; the cockpit shows *Recently
   changed*.
7. Everything that changed on disk is reflected on screen without a
   manual refresh, and the UI thread never blocks on persistence.
8. 31,000 lines of unreachable P02–P07 code sit under `src/Frozen/` and
   do not compile; the file-per-key persistence backend is gone.

The manual test script is §7. The gate is the `v0.17.0` gate: build with
warnings as errors in both configurations, every test, the governance
health check, three consecutive green CI runs.

## 2. Waves

Work Packages are grouped into waves by dependency and by the files they
touch, so that agents in one wave never edit the same file. A wave merges
in the order listed; the gate runs after every merge.

| Wave | Work Package | Depends on | Model | Files it owns (nobody else in the wave touches these) |
|---|---|---|---|---|
| 1 | `WP 18.0A` Evidence record (ADR-0148) | — | Sonnet | new `src/Tempest.Core/Evidence/*`, new `src/Tempest.Workspace/Workspace/Evidence/*` (registration, factory, create/cite/declare/check/issue/supersede commands, node provider, facet provider), `DisciplineAreas.cs` (one line), `EngineeringDomainContext` (registration of the Kind), ADR-0148, tests |
| 1 | `WP 18.0B` Reference libraries with citation (ADR-0149) | — | Sonnet | `src/Tempest.Core/ReferenceData/*` (`SourceCitation`, supersession invariant), the five catalogues' definitions, `Seeding/Datasets/*` (except `EngineeringAssetSeed.cs`), ADR-0149, tests. Desktop part deferred to `18.2A` |
| 1 | `WP 18.0C` Archive P02–P07 | — | Sonnet | the six namespaces, their tests and seeds, `TempestHost.cs` (DI region), `EngineeringTraceRegister.cs`, `WorkspaceHost.cs` (catalog region), ADR Register rows 0127–0142, `src/Frozen/README.md` |
| 2 | `WP 18.1A` Async read surface and change notification | — | Sonnet | `SqlitePersistenceStore.cs`, new `IWorkspaceChanges`/`WorkspaceSnapshot`, `ProjectExplorerView.cs`, `CockpitView.cs`, `UndoRedoCoordinator.cs`, `WorkspaceViewCoordinator.cs` (refresh region), `MainWindow.cs` (refresh region), `DigitalThreadGraphModel.cs`, `PropertyInspectorView.cs`, `ObjectEditorView.cs` (blocking-call region), `PersistenceStore.cs` deletion and its 26 test call sites |
| 3 | `WP 18.2A` Evidence workspace | 18.0A, 18.0B, 18.1A | Sonnet (Opus if a second attempt is needed) | new `Views/EvidenceWorkspaceView.cs`, `Views/LibrariesView.cs`, `IFilePicker` + Avalonia implementation + headless stub, `ShellArea`/`NavigationAvailability`/`GlobalNavigationRail`/`MainWindow` (Evidence area), `ObjectEditorView.cs` (declaration-per-Kind, Where-used, BOM input removal from Part), `MechanicalPropertyFacetProvider.cs`, journey tests |
| 3 | `WP 18.1B` Findability | 18.1A | Sonnet | new `SearchIndex` (SQLite FTS5) in `Persistence/`, `CommandPaletteOverlay.cs`, `ProjectExplorerView.cs` (filter only), `CockpitView.cs` (Recently changed card), tests |
| 4 | `WP 18.2B` Independent check and issue sheet | 18.2A | Sonnet | `Evidence/Check*`, `Evidence/Issue*`, new `IssueSheetRenderer` (PDF), `EvidenceWorkspaceView.cs` (check/issue region), journey tests with two principals |
| 5 | `WP 18.9.0` Release | all | lead + Haiku for counts | `PHYSICAL_REVIEW.md` §8, Release Notes, `PROJECT_STATUS.md`, `BACKLOG.md`, `VERSION`, tag |

Waves 1 and 3 run three and two agents in parallel; waves 2, 4 and 5
are serial. The lead merges, gates and re-briefs; no agent merges.

## 3. Engineering decisions taken while planning

These follow from the seam maps taken at the `v0.17.0` head. They are
recorded so the Product Owner can overrule any of them before work starts.

1. **`EngineeringAssets` keeps `Verification`, `CalculationPacks` and
   `Templates`.** `BracketEngineeringRecordService` (live, part of the
   dormant workbench), `EngineeringTraceRegister` and `WorkspaceHost`
   depend on the pack and template catalogues. Archiving them would mean
   cutting the workbench that `D-028` says stays. The archive exception
   list for P05 widens accordingly; everything else in P05 moves.
2. **Mixed files are split before they move.** `Crm/CrmCatalogs.cs` holds
   the Organisation and Contact catalogues (kept) and the Interaction
   catalogue (archived); `Finance/FinanceCatalogs.cs` holds Budget (kept)
   and FinancialEntry (archived); `EngineeringAssetSeed.cs` seeds both
   kept and archived kinds; `PricingTests.cs` tests both. Each is split
   into a kept file and an archived file in the same commit.
3. **The calculation surfaces are untouched.** `WP 18.3A` was withdrawn
   by the Product Owner before work started. No file under
   `Workspace/Calculations`, no calculation view and none of their 76
   tests change in this release; decision 1 follows from the same fact.
4. **The material-release screen inside the calculation workspace stays.**
   The *Libraries* tab (decision 7) is where all five libraries are
   reviewed and released, because they are what evidence cites.
5. **Evidence's check is not a fourth verification model.** `CheckRecord`
   and `IssueRecord` are values on the Evidence Kind, written through the
   same transaction as its state, mirroring how `ReferenceReviewService`
   writes reviewer identity and date. The three existing verification
   models are untouched.
6. **A file picker goes behind an interface.** No file picker exists in
   the shell today; attachments are typed metadata. `IFilePicker` wraps
   Avalonia's `IStorageProvider`; the headless tests use a stub that
   returns bytes from a temp file, so the journey test attaches a real
   workbook without a dialog.
7. **Reference libraries live in the Evidence area as a *Libraries* tab.**
   The records are what evidence cites, so they sit beside it, and
   `v0.19.0`'s rail keeps them there.
8. **The issue sheet is rendered with SkiaSharp's PDF document**, which
   already ships in the Desktop build under `PDFtoImage` (Product Owner,
   2026-09-09). The sheet is regenerated from the record and never edited.
9. **`PersistenceStore` deletion moves from `18.9.0` into `18.1A`**, which
   is already inside the store. `RootPathConfigurationKey` relocates to
   `SqlitePersistenceStore` first; the dual-backend test fixture and 25
   test call sites move to the SQLite store.
10. **Evidence citations refuse an unreleased record as a result, not an
    exception**, exactly as `GovernedBracketCheckService` does; the
    Object Editor shows the refusal in the status bar.
11. **Declared figures use the runtime-dimension `Quantity`** (ADR-0147)
    serialised as `"<value> <unit symbol>"` text in the object state, with
    a unit picker in the editor that lists the catalogue for the chosen
    dimension. No new column: the store is one JSON document per object.

## 4. The Product Owner's answers (2026-09-09)

1. **Independent check.** Built in, switched off by default
   (`Evidence:IndependentCheck`): a one-person consultancy has one login
   and enters the client's review by hand until there is a second member
   of staff. Tests prove both settings; the manual script runs with the
   rule off.
2. **Issue sheet PDF library.** SkiaSharp.
3. **Findability.** Full-text search, as stated.
4. **Archive.** Proceed: what is not utilised is archived.
5. **Branches.** Once `v0.17.0` is released, every branch related to
   `v0.16.0` or `v0.17.0` is closed, local and remote; then
   `release/v0.18.0` is created and **all work stays on that single
   branch**. Agents still work in worktrees, but on throwaway local
   branches that are merged into `release/v0.18.0` and deleted the same
   hour, never pushed; `origin` sees one branch.
6. **Calculation surfaces stay** (answered before the plan was
   published): `WP 18.3A` withdrawn.
5. **Release branch.** `release/v0.18.0` branches from `main` at the
   `v0.17.0` tag; Work Packages merge into it; one PR to `main` at the
   end, as for `v0.17.0`. *Default:* proceed.

## 5. Method

- **Worktrees.** `git worktree add D:/tempest-wt/<wp> -b wp/<id> release/v0.18.0`
  by the lead; the agent works only inside that path; the lead merges
  `--no-ff` into `release/v0.18.0`, deletes the local branch and removes
  the worktree. Nothing but `release/v0.18.0` is ever pushed (answer 5).
- **Models.** Sonnet for implementation and audits; Haiku for counts and
  mechanical checks; Opus only where a Sonnet attempt has failed twice on
  the same Work Package. Every brief carries the seam map, the acceptance
  test, the files it owns, the files it must not touch, and the kill
  switch: stop and report if the acceptance test cannot be met without
  touching a file outside the list.
- **Checkpoints.** An agent commits a compiling checkpoint within its
  first hour and at every green test run.
- **Gate at every merge.** `dotnet build src/TempestOS.slnx -c Debug|Release -p:TreatWarningsAsErrors=true`
  with every `error|warning` line grepped; `dotnet test --no-build` for
  both test projects in both configurations; `governance-healthcheck.ps1`
  5/5. Nothing is built while a `--no-build` test run is in progress.
- **Reporting.** Findings, remediations, warnings and blocks are reported
  as they arise; the release candidate report lists every gate figure,
  every deviation from this plan, and every known gap.

## 6. Risks

| Risk | Where | Mitigation |
|---|---|---|
| Merge conflicts between `18.2A` and `18.1A` in `MainWindow.cs` and `ObjectEditorView.cs` | wave 3 | `18.1A` merges before `18.2A` starts; `18.2A` is briefed against the merged head |
| The archive breaks a hidden dependency the map missed | wave 2 | the agent moves one namespace per commit and builds after each; a namespace that will not move cleanly is reported, not forced |
| Declaration-per-Kind refactor of the Object Editor regresses Part, Document, Requirement editing | wave 3 | the existing editor tests stay green throughout; the refactor is applied to Evidence, Part, Assembly, Component only |
| Two agents' tests contend for the persistence root | all waves | worktrees have separate roots; tests use isolated roots already |
| FTS5 unavailable in the bundled SQLite | wave 3 | `Microsoft.Data.Sqlite` bundles `e_sqlite3` with FTS5; the agent asserts `PRAGMA compile_options` in a test before building on it |
| Agent killed mid-rewrite by a rate limit | all waves | early checkpoints; the lead resumes from the last green commit |

## 7. Manual test script for the release candidate (becomes `PHYSICAL_REVIEW.md` §8)

1. Launch per `PHYSICAL_REVIEW.md` §3; the title bar reads
   `TempestOS 0.18.0 (<commit>)`. The rail shows Evidence and still shows
   Engineering Calculations; the Engineering workspace still has its
   Calculations tab.
2. Open a project. Rail → Evidence. The list is empty and says so.
3. **Create** → the file picker opens; pick a workbook (`.xlsx`) or a PDF.
   Choose classification *Calculation*, leave the subject empty. The
   record opens right up with the file listed, size and hash shown.
4. In the record, **Cite** → the picker lists only released records from
   the five libraries. Pick two materials. Each citation shows library,
   record, revision and source. Try a Draft record: refused, with the
   refusal in the status bar.
5. **Declare a figure**: name `Utilisation`, value `0.82`, unit
   dimensionless; a second: `Max stress`, `142 MPa`. Both persist.
6. Restart the application. Open the project → Evidence. The record, its
   file, citations and figures are all there. Command Palette: type part
   of the record's title; it is found and opens.
7. Tag the record to a Part (subject picker over the project's own
   structure). Open the Part: it shows *Where used* (its assembly) and
   no Bill of Materials input; its editor shows only Part fields.
8. **Check** → enter the client's reviewer name and organisation, the
   statement and the outcome; they are stored verbatim and the record
   moves to Checked, with you as *recorded by*. Settings → switch
   *Independent check* on → Check again on a new revision: refused,
   because you are the author.
9. **Issue** → issue reference, revision, client, date. The issue sheet
   PDF is attached and opens; it lists the citations and figures. The
   record is Issued.
10. **Revise** the issued record (new file). The issued revision stays
    readable and unchanged; the new revision is Draft.
11. Evidence → Libraries: the 41 records are listed with source citations;
    verify and release one Draft material; it now appears in the citation
    picker.
12. Home cockpit shows *Recently changed* with the evidence at the top.
