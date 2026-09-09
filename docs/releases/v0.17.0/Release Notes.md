# TempestOS v0.17.0 — Release Notes

**Status: accepted by the Product Owner's Windows smoke tests on
2026-09-09 (four hotfix rounds, `17.9.1` to `17.9.4`, listed below);
merged to `main`, tagged `v0.17.0` and published by `release.yml`.** Nothing in this document is certification.

**This release also carries `v0.16.0`.** `v0.16.0` was engineered,
merged to `main` in part, and never tagged or published; its integration
tree (`f19e231`) went 219 commits beyond `main` without a clean release
point. Rather than manufacture one after the fact, `v0.17.0` is built on
that tree and the `v0.17.0` tag will be the first tag after `v0.15.0`.
The `v0.16.0` section below records what that line delivered; its own
Release Notes remain at `docs/releases/v0.16.0/Release Notes.md`.

---

## Summary

**v0.17.0 is Reset and Substrates** — the first release of the
[v1.0.0 Release Candidate Programme](../v1.0.0/WorkPackages.md). It adds
one user-visible fix and no new capability. Its job is to replace the
ground the next two releases build on: persistence that survives a power
cut and commits several writes as one, a unit system with real
dimensions, a platform trimmed to what a single-user consultancy desktop
needs, a test suite that runs in minutes, and a governance process a
second person could pick up on day one.

## What a user will notice

- **The Engineering Calculations workspace renders correctly.** In
  `v0.16.0` both columns were drawn in the same Grid column, one over
  the other (`Grid.Column` was set on the panels inside the two
  ScrollViewers rather than on the viewers). Fixed; a layout test now
  fails if two sibling controls overlap.
- **Data lives in one SQLite database.** `persistence-data/` holds
  `tempest.db` (with `-wal`/`-shm` companions), `tempest.lock`, and a
  `logs/` folder. The file-per-key store is gone from the default path.
  **No data is migrated**: the Product Owner ruled the previous
  `persistence-data/` folders were test data; a `v0.17.0` install starts
  clean.
- **A second TempestOS on the same data folder is refused** with a
  message naming the folder, rather than sharing it.
- **A daily log file** at `persistence-data/logs/tempest-yyyyMMdd.log`,
  14 days retained. The Desktop no longer writes to a console it does not
  have.
- **Operator configuration exists.** `appsettings.json` beside the
  executable or in the working directory, `TEMPEST_`-prefixed environment
  variables, and command-line arguments, in that precedence. Every key is
  documented in `src/Tempest.Desktop/appsettings.sample.json`.
- **You are who the OS says you are.** The session principal's identity
  id is the Windows SID (the OS user id elsewhere) and cannot be changed
  by configuration; only the display name and the role (Engineer or
  Checker) can.
- **Closing mid-command is safe.** The workspace waits, bounded, for any
  command still executing before the platform is disposed under it.
- **Entering Engineering always shows the Project Explorer and Properties
  panels** (`WP 17.9.1`). The first Windows review of this release entered
  Engineering from a project and saw neither until an "Engineering layout"
  preset was applied by hand. The cause on that machine could not be
  reproduced under the headless test harness (a journey test that walks the
  same path passes with both panels placed), so the fix is a guarantee
  rather than a diagnosis: whatever a saved layout says, entering
  Engineering restores both panels to their home edges if they are absent.
- **People are shown by name, not by SID.** *Last Revised By*, *Created By*,
  *Latest Executed By*, *Latest Verified By* and the calculation
  workspace's *Executed* and *Performed by* readouts show a Windows
  account name. The stored value is still the identity id.
- **The object editor shows sections by Kind.** The Bill of Materials
  section (Quantity, Find Number, Reference Designator) appears only on
  Assembly, Sub-Assembly, Part, Component and Configuration, not on a
  Project or a Calculation. The editor's "Execute" section with its
  "Input (JSON)" box is retired: a Calculation now carries a note
  directing you to the Engineering Calculations workspace on the rail,
  where calculations are actually run, named and traced.
- **A created object lands where you are standing** (`WP 17.9.2`). "Create
  Mechanical Object" from the Ribbon or the Palette puts the new object
  under the selected Assembly, Sub-Assembly or Project, else under the
  open project. The status message names the object and where it went.
  The first Windows review created a Part that hung from nothing and
  could not be found: the command never supplied a parent and the
  Project Explorer, which roots on Projects, had no path to it. Any
  structural object that still hangs from nothing is now listed under a
  **Not in any project** node so it can be found.
- **You can add your own material** (`WP 17.9.2`). The Engineering
  Calculations workspace has an **Add a Material** section: name,
  designation, family, yield strength, density and the source it came
  from. The record is added as Draft and released through the same
  verify-and-release review as a shipped one; a record that names no
  source is refused. The **Material** picker now sits on the Inputs panel
  beside the figures it drives, offers only released records, and says
  where to go when nothing is released yet.
- **Every discipline's "Create" puts the object where you are standing**
  (`WP 17.9.3`). A Document or a Calculation created from the Ribbon or
  Palette goes under the selected part, assembly or project, else under
  the open project, and is still listed in its own tab (the Documents
  tree lists a document under its category unless it is nested under
  another document; the Calculations tree lists a calculation as a root
  unless it is inside a set). A Requirement created with nothing selected
  is listed under a new **Ungrouped** node; created with a group selected,
  it goes into that group. "Create Manufacturing Object" takes the Part
  from your selection, and with nothing suitable selected tells you what
  to select instead of failing on a missing id. A parent that no longer
  exists is refused before anything is written.
- **Properties shows a parent by name**, "Bracket Assembly (Assembly)",
  not as a GUID (`WP 17.9.3`). An id that resolves to nothing is shown as
  stored.
- **A verification result reaches the Requirement it verifies**
  (`WP 17.9.3`, `TD-173`). Recording a result against a Verification
  Activity also links the record from the Activity's subject, so the
  Requirement's *Verification Coverage* now shows it.
- **Releasing reference data is a permission** (`WP 17.9.3`, hazard H8 of
  the design-freeze review). Verifying needs `reference.verify` and
  releasing needs `reference.release`; the session's roles hold both by
  default, so nothing changes in the flow, but the gate exists and a
  refusal reads like every other review refusal.
- **The object editor no longer shows an empty, disabled Content box** on
  a Kind that cannot be revised, such as a requirement group or
  collection (`WP 17.9.3`).
- **Anything you create opens right up** (`WP 17.9.4`). After any Create
  from the Ribbon or the Palette, the Project Explorer switches to the
  discipline that lists the new object, expands the path to it and
  selects it, and the object opens in an editor tab with its fields in
  front of you, whichever tab you were on. The second Windows smoke test
  created a Part with the Explorer on another discipline and the project
  node collapsed, and nothing opened. A reload of the Explorer now also
  keeps whatever you had expanded. A Requirement opens in its own
  discipline view rather than the generic editor (`TD-41`).
- **The build is in the title bar**: "TempestOS 0.17.0 (9e52a53)", the
  version and the short commit (`WP 17.9.4`). The second smoke test was
  run against a stale clone's executable and nothing on screen said so.

## What shipped, by Work Package

| WP | Delivered |
|---|---|
| 17.0A | Calculation-view column fix; overlap-aware layout assertions; write-through fsync on the file store; critical hosted service whose constructor throws is Host-fatal; DI default-parameter fallback restricted to nullable parameters and logged; test deadlines scale by `TEMPEST_TEST_TIMEOUT_FACTOR`; no test touches the real persistence root; stray run artefacts removed and ignored. |
| 17.0B | 807 governance files moved to `archive/docs-2026-09/` with history intact; `PROJECT_STATUS.md` cut to one screen; `CONTRIBUTING.md` and `BACKLOG.md` (101 debt rows triaged: 27 live, 61 owned by a programme WP, the rest archived with their layer or closed); health check reduced from 16 checks to 5 source-derived ones plus a Markdown-must-not-exceed-code check; branch protection configured on `main` (strict `CI Gate`, PR required, no force pushes). |
| 17.0C | One `RunningHostFixture` replaces 93 copied host-polling loops; console capture removed from 83 test classes so they run in parallel; a real parallelism hazard in dynamic plugin-assembly emission fenced; exact-count fixture pins converted to property assertions; six tests that passed on known bugs deleted; Stryker.NET configured for the calculation and units namespaces on a scheduled CI job; coverage and per-assembly duration published in the CI summary. |
| 17.1A | `SqlitePersistenceStore` (WAL, `synchronous=FULL`, exact case-sensitive keys) behind the unchanged four-method interfaces plus `IQueryablePersistenceStore` (prefix listing, read-all, read-many, `ExecuteInTransactionAsync`); one instance registered under all three interfaces and disposed by the host; instance lock file; `Persistence:Backend` switch keeping the file store for exactly this release. 10,000 keys: seed 127 ms, list 5 ms, read-all 8 ms. ADR-0144. |
| 17.1B | One store is authoritative. Every mutator and both factories write object state, document revisions, references, attachment bytes and one audit row in a single transaction under one domain-wide lock; memory is applied only after commit, inside the lock. Deleted: rollback-on-failure, the mutation rollback point, the attachment write-intent store and the reconciliation sweep (1,802 lines). `EngineeringObjectBase` 1,819 → 821 lines plus a 203-line state partial. TD-147 (the Release Blocking row) closed, with TD-142/144/145/146/148. Two defects found in the WP's own first attempt and fixed. ADR-0145. |
| 17.2A | Plugin trust, signing, capability enforcement, the inbound REST API and Licensing frozen to `src/Frozen/` (41 files) and `tests/Frozen/` (37 files) outside the build; their seams removed from EventBus, Commands, Identity, Navigation, Modules, Hosted Services and the Host (about 1,000 lines); manifest discovery stays. Microsoft.Extensions.Configuration as the operator source; rolling file log sink and an M.E.Logging bridge; per-call DI, event and command chatter demoted to Debug; `ISessionPrincipal` with an OS-derived identity id; audit rows on calculation execution and reference verify/release. ADR-0146. |
| 17.2B | `Tempest.App` becomes `Tempest.Workspace` (class library, root namespace `Tempest.Workspace`) and `Tempest.Harness` (console exe); `InternalsVisibleTo` to the Desktop removed by promoting `IWorkspace.Cockpit` and the cockpit types to public; dependency-direction tests rewritten for the four-project graph. ADR-0101 amended. |
| 17.3A | Units are a runtime seven-exponent dimension vector; same-dimension quantities add, subtract and compare with automatic conversion (reversing ADR-0054's exact-unit rule); cross-dimension multiply and divide; temperature deltas; eight new dimensions including second moment of area and section modulus; the generic `Quantity<TDimension>` kept as a typed facade; 37 property-based tests (CsCheck) including result invariance under input-unit change for all six calculations. ADR-0147. |
| Unplanned | Command invocations are tracked from request to completion; `WorkspaceManager` drains them before disposal; the ribbon's report-then-refresh tail tolerates a disposed platform. Found by an intermittent Desktop failure that became deterministic once the store became disposable. The native SQLite provider is bound eagerly after a first-use race was reproduced once under parallel tests. The store's dispose clears only its own connection pool: the process-wide `ClearAllPools` it first used disposed the native handle under every other live store in the process, which the release gate exposed as one Core failure in roughly every four runs and which any two hosts in one process could have hit. |
| 17.9.1 | Hotfixes from the first Windows review (2026-09-08). Entering Engineering guarantees the Project Explorer and Properties panels are present (`WorkspaceDockingComposer.EnsureCorePanelsPresent`); a journey test walks Home → Projects → create → open → Engineering and asserts both panels placed. `IPrincipalDirectory` resolves stored identity ids to names (session principal first, then the Windows account via SID translation, else the id verbatim); a `Principal` facet kind marks the eight "…By" facets and the Properties panel, calculation traceability and verification readouts show names. The object editor shows the Bill of Materials section only on the five mechanical Kinds and retires the "Execute" / "Input (JSON)" section, showing a Calculation a pointer to the Engineering Calculations workspace instead. `SampleSeparationTests` no longer counts project files inside a nested clone. Not fixed here, recorded as `TD-172` for `WP 18.1A`: an object created from the palette while another discipline tab is active is not shown where the user is looking. |
| 17.9.2 | Second round from the first Windows review (2026-09-08). `CommandContext.ProjectId` carries the shell's open project; the Ribbon, Palette and input bindings supply it. `MechanicalCreateParentPolicy` places a created object under the selected container, else the open project; the create handler's message names the object and its parent. `MechanicalProductStructureNodeProvider` lists parentless structural objects under a "Not in any project" category and roots their ancestry there. `BracketCalculationWorkbench.AddMaterialAsync` registers an engineer's own material as Draft with the source it names (refusing a blank source, a non-positive number, or a duplicate designation); the calculation view gains an Add-a-Material section, a Material picker on the Inputs panel offering released records only, and guidance when none is released. Tests: parent policy (6), orphan listing (4), create-from-the-shell journey (2), add-own-material journey (1). Not fixed here: `TD-172`. Also observed while fixing: the sample module seeds three structural objects with no parent that had never been visible in the tree. |
| 17.9.3 | The design-freeze review's two high substrate hazards and the surface small wins it found (2026-09-08, overnight). `ReferenceReviewService` takes `IPermissionEvaluator` and requires `reference.verify` / `reference.release` (ADR-0143 amended; both held by the session roles). `IEngineeringObjectRepository.ListChildrenAsync` and `ParentChanged` with a by-parent index in the in-memory repository, maintained by `Register` and by `MoveAsync` after commit, self-healing on read; five tree providers, both BOM rules and the delete guard use it. `CreationPlacement.ParentFor` generalises the Mechanical rule to Documents and Calculations; their trees list project-placed objects; the three factories refuse a missing parent before writing. Requirements gain an **Ungrouped** category and create-into-selected-group. `manufacturing.create` resolves `PartId` / `ManufacturingOperationId` / `SubjectId` from the selection and asks for the right selection when it is missing. `PropertyFacetKind.ObjectReference` resolves the Parent facet to a name. The record-result handler links evidence from the Activity's subject (`TD-173`). The editor hides an unrevisable empty Content box. Six stale ADR statuses amended and the ADR Register brought to 147. Tests: permission gate (5), children index (5), subject link (2), creation-placement journeys (5). |
| 17.9.4 | Second Windows smoke test (2026-09-09): a created object must open right up. `CommandResult` carries `SubjectId`/`SubjectKind`; every create handler reports what it made. `RibbonView.ObjectCreated` and the Palette handler call `MainWindow.OpenCreatedObjectAsync`, which switches the Explorer to the area `DisciplineAreas.AreaFor(kind)` names, reloads, reveals (selects with every ancestor expanded, via a two-way `IsExpanded` binding on the tree item), selects in the workspace, opens the object's tab and refreshes the inspector. The Explorer keeps expansion across reloads. Tests: the ribbon path with the Explorer on another discipline, and the palette path for a Requirement. |

## Figures

| Measure | `v0.16.0` tree (`f19e231`) | `v0.17.0` |
|---|---|---|
| Live source lines (`src/`, excluding `Frozen/`) | 138,244 | 136,532 |
| Live test lines (`tests/`, excluding `Frozen/`) | 118,042 | 103,585 |
| Frozen out of the build | — | 42 source, 37 test files |
| Core tests | 5,153 | 4,961 |
| Desktop tests | 500 | 508 |
| Core suite duration (local, Debug) | ~5 min | ~20 s to 1 m 20 s |
| Desktop suite duration (local, Debug) | ~15 min | ~3 min |
| Live documentation files | 1,062 | 257 |
| ADRs | 143 | 147 (four new: 0144 to 0147; ten marked Frozen) |
| Technical debt rows | 170 in one 468 KB register | 27 live in `BACKLOG.md` |
| Release Blocking rows | 1 (TD-147) | 0 |
| Commits on the release branch | — | 53 |

## What changed for a developer

- Build and test commands are unchanged. The harness is now
  `dotnet run --project src/Tempest.Harness`.
- The first NuGet packages in `Tempest.Core`: `Microsoft.Data.Sqlite`
  and the `Microsoft.Extensions.Configuration.*` and
  `Logging.Abstractions` packages. Test projects add `CsCheck`.
- `EngineeringDomainContext` requires an `IQueryablePersistenceStore`;
  hand-assembled test contexts use the one in-memory double at
  `tests/Tempest.Core.Tests/Persistence/InMemoryQueryablePersistenceStore.cs`.
- `IIdentityService`, `RoleProvider`, `Role` and the `Identity:Roles:*`
  configuration are gone. `ICurrentPrincipalAccessor` and
  `IPermissionEvaluator` remain.
- Every Work Package is a branch and a PR; see `CONTRIBUTING.md`.

## Warnings

- **CI first ran on this branch at release time.** By decision, nothing
  was pushed until the Product Owner's Windows verification, so every
  gate figure above is local. The push of `release/v0.17.0` and its PR
  ran the full pipeline, and `main`'s branch protection required the
  `CI Gate` check green on the merged head; `release.yml` rebuilt and
  re-tested the tag before publishing. The `mutation` job runs only on
  schedule or dispatch.
- **The `files` persistence backend is not transactional.** It is
  retained for one release behind `Persistence:Backend=files` and gives
  up what ADR-0145 guarantees. It is deleted in `v0.18.0`.
- **Three verification models remain** (`Core/Verification`,
  `EngineeringDomain/RequirementsVerification`,
  `EngineeringAssets/Verification`). Collapsing them was deferred from
  WP 17.1B to WP 18.2B as `TD-171`.
- **Stryker has a configuration and no score yet.** The first scoped run
  did not complete inside its budget locally; the scheduled CI job owns
  the first timed run.
- **The intermittent seen during WP 17.1B is explained.**
  `ReconciliationHostRegistrationTests` failing in 2 of 6 worktree runs
  "on SQLite lock contention" was the process-wide pool clear above, not
  lock contention; after the fix the Core suite passed eight consecutive
  local runs and the release gate's three Debug runs plus one Release run
  with no failure. Watch the first CI runs regardless.
- **Test counts fell by design.** Console-capture scaffolding, six
  defect-characterisation facts, frozen-layer tests and duplicated
  doubles are gone; behaviour coverage is not.
- **Documentation comments still say `Tempest.App`** in a handful of
  Core, Samples, Validation, governance and security files that never
  referenced the project; left as prose.
- **Known surface gaps, found by the design-freeze audit of 2026-09-08
  and left for the programme.** An object created while another
  discipline tab is active is not shown where the user is looking
  (`TD-172` residual, `WP 18.1A`). Twelve Move and Copy commands and six
  others are listed but unavailable, by design, until an object picker
  exists. Five of the ten rail entries are "not yet implemented" cards.
  There is no search for an object by name. See
  `docs/reviews/design-freeze-2026-09/`; the creation-placement and
  verification-coverage defects that audit found were fixed by
  `WP 17.9.3`.

## What `v0.16.0` delivered (rolled into this release)

v1.0 Readiness Hygiene, `WP 16.0A` to `WP 16.9.0` and the `WP 16.4B`
R1 to R7 remediation rounds: durable state schema versioning
(ADR-0120) with a golden corpus; the CI gate enforced through the
governance health check and `release.yml`; registers re-derived from
source; fifty-two Academy retrospectives; test determinism work
(TD-34, 83, 100, 119); durability hardening of the object model (the
refuse-after-mutate class, TD-135 to TD-150, characterised and
partly closed, the remainder closed here by WP 17.1B); an accessibility
baseline; the Linux launch spike (Avalonia 11.3.20); the Engineering
Calculations workspace over the governed `Calculation` object; the first
governed engineering calculation with a pinned reference revision
(ADR-0143); and the Group A to F and P04 reference, intelligence,
governance, commercial, assets and knowledge programmes, most of which
this release freezes or leaves untouched pending WP 19.0B. The
`v1.0.0 Release Candidate Audit` and the programme that replaced its
plan are in `docs/releases/v1.0.0/`.

## Related

`docs/releases/v1.0.0/WorkPackages.md`; `BACKLOG.md`; `CONTRIBUTING.md`;
`PHYSICAL_REVIEW.md`; ADR-0144, ADR-0145, ADR-0146, ADR-0147.
