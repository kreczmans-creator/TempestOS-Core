# TempestOS v0.17.0 — Release Notes

**Status: engineering complete on `release/v0.17.0`; awaiting Windows
verification by the Product Owner, then merge to `main`, tag and
publish.** Nothing in this document is certification.

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
| 17.9.1 | Hotfixes from the first Windows review (2026-09-08). Entering Engineering guarantees the Project Explorer and Properties panels are present (`WorkspaceDockingComposer.EnsureCorePanelsPresent`); a journey test walks Home → Projects → create → open → Engineering and asserts both panels placed. `IPrincipalDirectory` resolves stored identity ids to names (session principal first, then the Windows account via SID translation, else the id verbatim); a `Principal` facet kind marks the eight "…By" facets and the Properties panel, calculation traceability and verification readouts show names. The object editor shows the Bill of Materials section only on the five mechanical Kinds and retires the "Execute" / "Input (JSON)" section, showing a Calculation a pointer to the Engineering Calculations workspace instead. `SampleSeparationTests` no longer counts project files inside a nested clone. Not fixed here, recorded as `TD-171` for `WP 18.1A`: an object created from the palette while another discipline tab is active is not shown where the user is looking. |

## Figures

| Measure | `v0.16.0` tree (`f19e231`) | `v0.17.0` |
|---|---|---|
| Live source lines (`src/`, excluding `Frozen/`) | 138,244 | 136,532 |
| Live test lines (`tests/`, excluding `Frozen/`) | 118,042 | 103,585 |
| Frozen out of the build | — | 42 source, 37 test files |
| Core tests | 5,153 | 4,931 |
| Desktop tests | 500 | 498 |
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

- **CI has not run on this branch.** By decision, nothing was pushed
  until Windows verification. Every gate figure here is local. The first
  push will run the full pipeline; the `mutation` job runs only on
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
