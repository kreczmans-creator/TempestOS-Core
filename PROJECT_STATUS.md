# TempestOS — Project Status

**Last Updated:** 2026-07-30 (`v0.6.0` Release Engineering — merged to `main`, tagged, released)

This is the primary status dashboard for TempestOS. Read this first for
"where does the project stand right now" — for "why is it built this
way," read `docs/releases/FOUNDATION.md`; for "how do I get productive,"
read `docs/academy/Contributor Learning Path.md`.

---

## Current Repository Phase

**Developer Experience — complete and released as `v0.5.0`.** The
Foundation phase is complete and closed — Platform Formation, Academy
Formation, and Governance Formation are all done (see `docs/releases/
Platform Foundation Completion Report.md`), and `v0.4.0` ("Platform
Foundation") shipped exactly that scope. TempestOS then built *on* the
foundation — Navigation, a Command Framework, Diagnostics, and Developer
Experience tooling itself — through the **Developer Experience** phase.
`WP 5.0A` through `WP 5.0D` were this phase's first four completed Work
Packages — Navigation and the Shell are both fully implemented, and
`Tempest.App` now runs the real platform for the first time in this
project's history. `WP 5.0S` (Platform Security Baseline Audit) followed
as a dedicated, formal engineering audit — not a feature Work Package —
establishing the v0.5.0 Security Baseline every future Work Package's
Definition of Done is checked against. `WP 5.1A`/`WP 5.1B` (Command
Framework) followed — `ICommand`'s own contract (`WP 4.0`) now has a real
handler contract and dispatcher. `WP 5.2` (Diagnostics Improvements)
followed — a composite `ILogSink` closes a long-named debt (`TD-02`), and
a new `IDiagnosticsProvider` gives any DI-resolving consumer a read-only
view of the Host's own current lifecycle state. `WP 5.3` (Developer
Experience Improvements) closed the phase out — a `dotnet new` module
template, and a previously-only-documented Discovery pitfall now closed
with a clear, actionable error message. `WP 5.4` (v0.5.0 Release
Candidate & Engineering Sign-Off) then verified the entire release
directly — every ADR, every Work Package, every governance register —
before Product Approval authorised the release itself. **`v0.5.0` is now
released.** TempestOS is now in the **Platform Services** phase
(`v0.6.0`). The complete architecture package (`docs/releases/v0.6.0/
Release Architecture.md` and seven companion documents) and Contract
Review package (`Platform Service Contracts.md` and four companion
documents) were both produced and approved ahead of any implementation.
`WP 6.1` (Permissions & Identity), `WP 6.4` (Settings Framework), `WP
6.5` (Audit Framework), `WP 6.2` (Notification Framework), `WP 6.0`
(Reporting Framework), `WP 6.3` (REST API), `WP 6.7` (Export/Import),
and now `WP 6.6` (Licensing Framework) are all implemented — every
Work Package that ships real code this release is now complete, with
only `WP 6.8` (Platform Services Integration Review) remaining. `WP
6.0` and `WP 6.3` remain the only two of these eight to actually match
their own nominal position in `WorkPackages.md`'s own numbering, the
other six having each been implemented ahead of their own nominal
numeric order, per `Platform Service Implementation Order.md`'s own
explicit recommendation. `WP 6.5` reuses the Persistence abstraction
`WP 6.4` established, exactly as that recommendation anticipated,
rather than introducing a second storage mechanism. `WP 6.2` is built
on top of the existing Event Bus's own proven dispatch model, exactly
as `Required ADRs.md` anticipated for `ADR-0046`, rather than a second,
parallel publish/subscribe implementation. `WP 6.0` is explicitly
orthogonal to `WP 6.7` (Export/Import), per `ADR-0040`, and
demonstrates real, working integration with four already-completed
platform services (Identity, Settings, Audit, Notifications) entirely
at its own sample module's calling layer, never inside
`IReportingService` itself. `WP 6.3` adopts ASP.NET Core/Kestrel for
HTTP hosting (`ADR-0049`) — this platform's first substantial
dependency on a pre-built framework component beyond the bare .NET SDK
— confined entirely to one hosted-service type, and resolves this
platform's first genuinely concurrent, per-request scenario without
touching `CurrentPrincipalAccessor`'s own already-shipped ambient
design (`ADR-0052`), a decision verified empirically, not merely
reasoned about. `WP 6.7` completes the orthogonality `ADR-0040`
anticipated (`ADR-0051`): Export/Import is a user-facing, `Stream`-based,
portable-artifact I/O layer, explicitly distinct from the internal
Persistence abstraction, round-tripping two Settings values as a
single, multi-source artifact through a Kind-routed `IImportable`
registration mechanism dual-registered exactly as `ADR-0044`'s own
`CurrentPrincipalAccessor` precedent. `WP 6.6` resolves this release's
own last open architectural question (`Risk Register.md`'s own `R5`):
license validation is a pre-container, Host-startup gate, Host-fatal
for a broken license file but not for a missing one, which resolves to
a valid, unrestricted-but-uncapable default (`ADR-0050`) — proven not
to regress any of the 24 pre-existing tests that build a real
`TempestHost`. `WP 6.8` closes the phase: a certification review, not
an implementation Work Package, confirming the platform's own
architecture, integration, testing, documentation, and governance all
hold up under direct, independent re-verification, and recommending
**CERTIFIED WITH ACCEPTED TECHNICAL DEBT**. Product Approval was then
granted and `v0.6.0` was released in full: merged to `main`
(non-fast-forward, `99ed285`), tagged `v0.6.0`, and pushed. TempestOS is
now in the **Engineering Foundation** phase (`v0.7.0`), on
`feature/v0.7.0-engineering-foundation`, not yet scoped. See Current
Work Package, below.

## Current Development Branch

**`feature/v0.7.0-engineering-foundation`**, cut from `main` at the
`v0.6.0` tag, per `v0.6.0`'s own Release Engineering closing activity.
No Work Package has been scoped or begun on this branch — see
`docs/releases/v0.7.0/WorkPackages.md` for the candidate items awaiting
a dedicated Architecture, Planning, and Contract Review phase before any
implementation is authorised. `feature/v0.6.0-platform-services` (`WP
6.0` through `WP 6.8`) has been merged into `main` (non-fast-forward,
`99ed285`) and is retained; `feature/v0.5.0-developer-experience` (`WP
5.0A` through `WP 5.4`) remains merged and retained as well — unmerged
and merged feature branches are both never deleted per this project's
own convention.

## Current Release

**v0.6.0** ("Platform Services") — released 2026-07-30, tagged `v0.6.0`
(`99ed285`), `CERTIFIED WITH ACCEPTED TECHNICAL DEBT`. Root `VERSION`
reads `0.6.0`. `v0.5.0` ("Developer Experience") is the release before
that; `v0.4.0` ("Platform Foundation") before that.

## Current Work Package

**`WP 7.0A` — Future Capability Register & Product Vision.** The
Engineering Foundation phase's (`v0.7.0`) own first activity — an
architecture-and-governance Work Package, not an implementation one; no
production code is written. Establishes the permanent governance
artefacts every future Work Package's own selection is measured against:
`VISION.md` (repository root), `docs/governance/Future Capability
Register.md` (28 entries, `FCR-0001`–`FCR-0028`), `docs/governance/
Capability Categories.md` (15 categories, six Engineering Discipline
categories disclosed as currently empty), and `docs/governance/Product
Roadmap.md` (8 phases, only the first four scoped or shipped). See
`docs/releases/v0.7.0/WP7.0A Architecture Report.md` and its four
companion deliverables for the complete account.

**`PROJECT_STATUS.md`'s own "Next Planned Work Package" section below
now defers to `docs/governance/Future Capability Register.md` as the
authoritative source for what TempestOS builds next** — this dashboard
still names the current Work Package, but no longer re-derives roadmap
reasoning that register now owns.

### `v0.6.0` Closing Summary (for reference)

`WP 6.8` — Platform Services Integration Review & Release
Certification — certified. The closing Work Package of the Platform
Services phase (`v0.6.0`) — a certification review, not an
implementation exercise; no production code was written. Reviewed all
eleven in-scope services (Runtime Foundation, Host, Identity &
Permissions, Settings, Persistence, Audit, Notifications, Reporting,
REST API, Export/Import, Licensing) directly against the shipped
repository, re-verifying rather than re-reading each prior Work
Package's own claim.

**Certification outcome: CERTIFIED WITH ACCEPTED TECHNICAL DEBT.**
Zero findings rise to release-blocking. Architecture Review: zero
`Service → Module` violations, zero `Module → Module` violations beyond
one disclosed, constant-only exception (`ApiSampleModule`), zero
`Runtime → Feature` violations — confirmed by direct `grep`, not
assumed. One genuine, narrow, non-blocking architectural finding
disclosed for the first time: `Tempest.Core.Diagnostics` imports
`Tempest.Core.Runtime` for a single enum type (`HostState`), a mutual
namespace reference a literal reading of `ADR-0023` would flag —
shipped safely since `WP 5.2`, recommended for formal resolution in a
future release. Integration Review: every one of the eleven services
has at least one verified, real consumer (the REST API now has two
independent ones — `ApiSampleModule` and `LicensingSampleModule` — the
strongest evidence yet that `IApiEndpointRegistry`'s own design
generalises). Testing Review: 1016 tests, 0 failures, confirmed across
six full-suite runs (Debug and Release, from a clean rebuild), zero
instances of the known, disclosed `Console.Out`-capture flake.

**The largest single finding: three governance registers, stale since
`WP 5.2`, were fully backfilled.** `Interface Register.md`
(64 interfaces), `Dependency Injection Register.md` (26 named
registrations), and `Module Register.md` (all 15 production modules)
had each gone six Work Packages without an update — `WP 6.7` first
disclosed this as `Partial`, `WP 6.6` correctly added only its own new
entries, and `WP 6.8` performed the full backfill, closing the gap
completely. Two release-level risks (`R2`, `R3`) that had sat "Open"
despite being substantively resolved by their own owning Work Packages
were formally closed here with fresh, independent evidence (`git log`'s
own commit order for `R2`; a fresh `grep` of `RestApiHostedService.cs`
for `R3`). All eight risks in `Risk Register.md` are now Closed or
Mitigated, save one (`R8`) Remaining by deliberate, disclosed design
choice. Sixteen tracked debt items and thirteen disclosed trade-offs
were each classified Resolved, Accepted, or Deferred — zero Release
Blocking. See `WP6.8 Platform Certification Report.md` for the complete
decision and evidence, and its eight companion deliverables
(`Platform Architecture Conformance Report.md`, `Platform Consumption
Matrix.md`, `Definition of Done Audit.md`, `Technical Debt
Disposition.md`, `Risk Register Disposition.md`, `Release Readiness
Report.md`, `Executive Summary.md`) plus its own retrospective:
`docs/academy/03 Work Packages/WP6.8-platform-services-integration-
review.md`.

## Next Planned Work Package

**None yet approved.** **`docs/governance/Future Capability Register.md`
is now this project's own authoritative source for what comes next** —
28 identified capabilities (`FCR-0001`–`FCR-0028`), each traced to a
specific prior document, classified against `docs/governance/Capability
Categories.md`'s 15 categories, and sequenced at the phase level in
`docs/governance/Product Roadmap.md`. `docs/releases/v0.7.0/WP7.0A
Recommended v0.7 Candidate Work Packages.md` assesses four of them
(`FCR-0001`, `FCR-0003`/`FCR-0004`, `FCR-0005`, `FCR-0006` — all
Platform-category, all within Product Roadmap Phase 4, "Engineering
Foundation") as the strongest `v0.7` candidates by Strategic Value,
Technical Complexity, and Platform Readiness — but **none is yet an
approved Work Package**. Per this project's own standing discipline
(`FOUNDATION.md` §1), `v0.7.0`'s real implementation scope still
requires its own Architecture, Planning, and Contract Review phase,
mirroring the phase `v0.6.0` itself held before `WP 6.0` began, before
any implementation is authorised. Await Engineering Review of `WP 7.0A`
before beginning `WP 7.0B`.

## Foundation Status

**Complete.**

| Milestone | Status |
|---|---|
| Platform Formation (Runtime Host, six platform services, Plugins, Event Bus, Background Services) | Complete |
| Academy Formation (63 articles across 7 categories, formal maintenance obligation) | Complete |
| Governance Formation (27 registers, full traceability, zero outstanding debt) | Complete |

See `docs/releases/Platform Foundation Completion Report.md` for the full
closeout narrative and `docs/releases/v0.4.0/Release Notes.md` for the
release this milestone shipped as.

## Platform Summary

TempestOS is a modular runtime platform. The Runtime Host
(`TempestHost`/`TempestHostBuilder`) assembles and orchestrates: Discovery,
Registration, Lifecycle, Dependency Injection, Configuration, and Logging
(the original Runtime Foundation, v0.3.0); Plugin Manifest discovery and
loading; the Event Bus (publish/subscribe between modules); and
Background Services (Host-orchestrated hosted work with isolated/critical
failure classification); and Navigation (a DI-public registry of
navigable destinations, notified via the Event Bus). Five real modules
(`ClockModule`, `ClockLifecycleObserverModule`, `NavigationSampleModule`,
`SecondaryNavigationSampleModule`, `DuplicateNavigationSampleModule`)
exercise the complete pipeline end to end. `Tempest.App` now runs the
real platform for the first time in this project's history: its entry
point builds a `TempestHostBuilder`, constructs the Shell
(`TempestShell`, `Tempest.App.Shell`), and runs it — the Shell resolves
`INavigationProvider`/`IEventBus` through `ITempestHost.Services`
(`ADR-0033`–`ADR-0035`, implemented `WP 5.0D`) and presents a real,
interactive Navigation/Content region. The bootstrap-era
`BootstrapService`/`HostingService`/`ProjectService` code remains in the
repository, untouched and unmigrated, simply no longer referenced by
`Program.cs`. The Command Framework (`ICommand`, `v0.4.0`; `ICommandDispatcher`/
`ICommandRegistry`, `ADR-0036`–`ADR-0038`, `WP 5.1A`/`WP 5.1B`) is now
fully implemented — a type-keyed dispatcher and an Id-keyed registry,
both DI-public, mirroring the Event Bus and Navigation — proven by a
real reference module (`CommandSampleModule`) that also realises
ADR-0022's own Navigation-integration illustration for the first time.
Wiring the Shell's own input handling (keyboard shortcuts, a menu) to
the Command Framework is deferred to a later Work Package. Diagnostics
(`IDiagnosticsProvider`/`DiagnosticsProvider`, `Tempest.Core.Diagnostics`)
is now implemented — a read-only projection over the Host's own current
lifecycle state, constructed directly by `TempestHost` and registered via
`AddInstance` with `Func<T>` accessors (`ADR-0039`), proven by
`DiagnosticsSampleModule` and its `GetDiagnosticsSummaryCommand`. Logging
also gained `CompositeLogSink`, closing `TD-02`. A `dotnet new
tempest-module` template (`src/Templates/`) now lets a new contributor
scaffold a correctly-shaped module without hand-copying an existing one,
and Discovery's own long-documented parameterless-constructor pitfall now
fails with a clear, actionable message instead of a raw runtime
exception. Every Work Package originally scoped for the Developer
Experience phase is now complete.

## Repository Metrics

| Metric | Value |
|---|---|
| Automated tests | 1016 (0 failures) — unchanged by `WP 6.8` (a certification review, no production or test code written); re-run six times across both configurations as this Work Package's own validation, zero failures every time |
| ADRs | 52 (`ADR-0001`–`ADR-0052`, no gaps at all), all Accepted — unchanged by `WP 6.8` |
| Rejected Designs | 45 (`RD-0001`–`RD-0045`) — unchanged by `WP 6.8` |
| Academy articles | 86 (see `docs/governance/Documentation/Academy Register.md`) — **+1, `WP 6.8`**: `WP6.8-platform-services-integration-review.md` |
| Governance registers | 27 (32 governance documents total), plus 4 standing security documents under `docs/security/` (not governance registers themselves, indexed from `Governance Index.md`'s Security section) — count unchanged by `WP 6.8`, but three registers' own Coverage Status corrected from `Partial` to `Complete` (see Governance Status, below) |
| Architecture documents | 20 under `docs/architecture/` (22 including the two release-scoped documents) — unchanged by `WP 6.8` |
| Platform services | 26 catalogued — 23 Implemented, 2 not implemented as platform services, 1 developer-convenience layer — unchanged by `WP 6.8` |
| Modules (production) | 15 (`ClockModule`, `ClockLifecycleObserverModule`, `NavigationSampleModule`, `SecondaryNavigationSampleModule`, `DuplicateNavigationSampleModule`, `CommandSampleModule`, `DiagnosticsSampleModule`, `IdentitySampleModule`, `SettingsSampleModule`, `AuditSampleModule`, `NotificationSampleModule`, `ReportingSampleModule`, `ApiSampleModule`, `ExportImportSampleModule`, `LicensingSampleModule`) — unchanged by `WP 6.8`; all 15 now correctly listed in `Module Register.md` for the first time |
| Hosted services (production) | 2 — unchanged by `WP 6.8` |
| Plugins (production) | 0 — infrastructure fully implemented and tested; `src/Plugins/` empty by deliberate scope decision |
| Custom exception types | 52 — unchanged by `WP 6.8` |
| Technical Debt Register items | 16 tracked, 13 disclosed trade-offs (1 Retired) — unchanged by `WP 6.8`; all 29 items classified in `WP6.8 Technical Debt Disposition.md` (3 Resolved, 6 Accepted, 7 Deferred among tracked debt; 1 Resolved, 12 Accepted among trade-offs; **zero Release Blocking**) |
| Commits (`v0.5.0` → `v0.6.0`, final) | 13 — branch/documentation preparation, Architecture Package, Contract Review Package, `WP 6.1` implementation, `WP 6.4` implementation, `WP 6.5` implementation, `WP 6.2` implementation, `WP 6.0` implementation, `WP 6.3` implementation, `WP 6.7` implementation, `WP 6.6` implementation, `WP 6.8` certification review, and the non-fast-forward merge to `main` (`99ed285`) |
| Contributors | 1 (repository owner; all commits co-authored by Claude) |

*(This table is generated from `docs/governance/Quality/Repository Metrics
Register.md` and `docs/releases/v0.4.0/Release Notes.md` — update all
three together.)*

## Repository Health

- **Build:** Clean — 0 warnings, 0 errors (`dotnet build
  tests/Tempest.Core.Tests/Tempest.Core.Tests.csproj`, both Debug and
  Release configurations, from a fully-removed `bin`/`obj` tree —
  re-verified directly by `WP 6.8`, independent of `WP 6.6`'s own claim).
- **Tests:** 1016/1016 passing, re-verified by `WP 6.8` across **six**
  full-suite runs (three Debug, two Release, one further Debug),
  including two entirely clean rebuilds — the deepest test-stability
  verification any single Work Package this release has performed. Zero
  instances of the previously-disclosed, non-reproducible `Console.Out`-
  capture flake (`WP 6.3`'s own finding) were observed across any of the
  six runs. `WP 6.8`'s own certification review found no code-level
  regression of any kind — see `WP6.8 Release Readiness Report.md` for
  the complete, per-run evidence table.
- **Known regressions:** None.
- **Working tree:** Clean at every Work Package boundary — see
  `docs/governance/Quality/Validation Register.md`.

## Documentation Status

Mature and cross-referenced. Every architecture document, ADR, and
Academy article is indexed in `docs/governance/` and cross-checked
against its own source. `WP 5.0A` added `Navigation Framework
Architecture.md`, `ADR-0031`/`ADR-0032`, and a new Academy concept guide;
`WP 5.0B` updated each of them in place to reflect implementation. `WP
5.0C` added `Shell & Composition Framework Architecture.md` and
`ADR-0033`–`ADR-0035`; `WP 5.0D` updated each of them in place to reflect
implementation, following the identical documentation shape Navigation's
own implementation phase established. Two small, pre-existing drifts were
also found and fixed along the way, unrelated to this Work Package's own
changes: `Documentation Register.md` had gone stale since `WP 4.5B`, and
`Technical Debt Register.md`'s own TD-07 still described Navigation's
`Tempest.Core` placement as an open question, under its old `WP 4.6A`
number, three Work Packages after `ADR-0031` had already resolved it.
`docs/releases/v0.5.0/ReleasePlan.md` and `WorkPackages.md` carry the
renumbered Developer Experience scope (`WP 4.6A`–`WP 4.9` → `WP 5.0A`–
`WP 5.3`) forward, plus the new `WP 5.0C`/`WP 5.0D` pair inserted without
renumbering anything else (`D-016`); the old `v0.4.0` entries were
annotated with redirect notes rather than deleted, per this project's
"never delete, mark superseded" convention. `WP 5.0S` added a new
top-level documentation tree, `docs/security/` (four documents: `Threat
Model.md`, `Security Principles.md`, `Platform Security Review
v0.5.0.md`, `Security Roadmap.md`), indexed from `Governance Index.md`'s
new Security section and `Documentation Register.md`'s Directory Map —
the first new top-level `docs/` tree since `docs/governance/` itself
(`WP 4.5A`). `WP 5.1A` added `Command Framework Architecture.md` and
`ADR-0036`–`ADR-0038`; the existing "Command" entries in `Platform
Service Map.md` and `Engineering Glossary.md` were updated in place,
following the identical documentation shape Navigation's and the
Shell's own design phases established. A genuine, pre-existing drift was
found and corrected along the way, unrelated to `WP 5.1A`'s own design
work: `Ownership Matrix.md` had never received a row for Navigation, at
either `WP 5.0A` or `WP 5.0B` — added then, alongside `WP 5.1A`'s own new
Command Framework row. `WP 5.1B` updated every "Command Framework" status
line from "architected" to "implemented" (`Platform Service Map.md`,
`Ownership Matrix.md`, `Engineering Glossary.md`, `Architecture Document
Register.md`) and added an Implementation Note and Security Review
Update to `Command Framework Architecture.md` itself. Further
pre-existing drift was found and corrected during `WP 5.1B`'s own
repository review: `docs/releases/v0.5.0/WorkPackages.md` had never
gained an entry for `WP 5.0S`, and several Engineering registers
(`Platform Services Register.md`, `Interface Register.md`, `Namespace
Register.md`, `Dependency Injection Register.md`, `Exception Register.md`,
`Module Register.md`) and Delivery registers (`Feature Register.md`,
`Traceability Matrix.md`) had gone stale since `WP 5.0D` — all corrected
in the same commit as the change that prompted noticing them. `WP 5.2`
added `Diagnostics Architecture.md` and `ADR-0039`; `Platform Service
Map.md`, `Ownership Matrix.md`, and `Engineering Glossary.md` each gained
a new Diagnostics entry, following the identical documentation shape
Navigation's and the Command Framework's own design phases established.
A genuine, pre-existing drift was found and corrected along the way,
unrelated to `WP 5.2`'s own scope: `Architecture Document Register.md`
still read `Command Framework Architecture.md` as "implementation
pending... not yet started," two Work Packages after `WP 5.1B` had
actually completed it — corrected here. `WP 5.3` updated `Building a
Module.md` and `Sample Module Architecture.md` to reference the new
scaffolding template, and amended `Engineering Governance.md` §11 to
document `src/Templates/`. Three further, genuine, pre-existing drifts
were found and corrected during this Work Package's own repository
review, none related to its own scope: `Rejected Designs Register.md`
had added `RD-0042`–`RD-0044` (`WP 5.2`) without the corresponding full
entries ever being written into `docs/architecture/Rejected Designs.md`
itself; `Engineering Governance.md` §11 had not been updated when
`WP 5.2` added the `Tempest.Core.Diagnostics` namespace; and
`Governance Register.md`'s own Compliance Matrix had not been updated
since `WP 5.0D`, missing four completed Work Packages entirely (see
Governance Status, below, for the full account). `WP 5.4` produced this
release's own Release Candidate documentation — `docs/releases/v0.5.0/
CHANGELOG.md`, `Release Notes.md`, `ReleaseChecklist.md`, and the
top-level `docs/releases/v0.5.0.md` — and found three further,
significant, pre-existing drifts: `docs/releases/v0.5.0/ReleasePlan.md`
had not been updated since `WP 5.0C` and still read "in progress" with
only three of nine Work Packages marked complete; `docs/academy/
Contributor Learning Path.md` — the document a *new contributor* reads
first — still pointed at `v0.4.0/WorkPackages.md`, cited a 30-ADR count,
and never mentioned Navigation, the Shell, the Command Framework,
Diagnostics, or the new module template at all; and `docs/releases/
v0.4.0/Risks.md`'s own `R5`/`R7`/`R8`/`R9` rows had each been carrying a
"residual carries forward" caveat that was, by this point, already fully
resolved by a specific, named `v0.5.0` Work Package — all corrected.

**`WP 6.1` (Permissions & Identity)** — the release's own eight-document
architecture package (`docs/releases/v0.6.0/Release Architecture.md` and
companions) and five-document Contract Review package (`Platform Service
Contracts.md` and companions) were both produced and approved before this
Work Package began; neither was revised during implementation.
`docs/architecture/Platform Service Map.md` gained a new Identity &
Permissions entry, following the identical documentation shape every
prior new platform service's own entry has used. Two genuine
implementation-phase findings not anticipated by the architecture
package are disclosed in `ADR-0043`/`ADR-0044` rather than silently
absorbed: the config-sourced Role model and `IIdentityService` (neither
drafted in the original `Public Interface Catalogue.md`), and the
`CurrentPrincipalAccessor` ambient-field departure from that document's
own tentative `AsyncLocal<T>` suggestion, proven by a dedicated
regression test. `docs/governance/Quality/Technical Debt Register.md`'s
`TD-09`/`TD-10`/`TD-11` entries were each updated in place to record that
the authorization enforcement point now exists, without claiming any of
the three is resolved — none is, since retrofitting an enforcement call
into `NavigationService`, Command/Navigation registration, or plugin
loading was explicitly out of this Work Package's own scope.

**`WP 6.4` (Settings Framework)** — implemented directly against the
same, unrevised architecture and Contract Review packages.
`docs/architecture/Platform Service Map.md` gained two new entries,
Persistence and Settings, following the identical documentation shape
every prior new platform service's own entry has used. `ADR-0041`
formally ratifies the Persistence-abstraction recommendation the
architecture phase only tentatively proposed; `ADR-0042` confirms
Settings' own distinctness from Configuration and records two
genuine implementation-phase decisions the Contract Review left open:
an in-memory cache over `IPersistenceStore` (built, per that document's
own suggestion), and a deliberate choice *not* to add a sensitive-value
flag to `ISettingDefinition` in this release (a disclosed limitation,
not a defect, since doing so would have modified an approved public
interface for a speculative need). `docs/releases/v0.6.0/Risk
Register.md`'s `R4` (Persistence reinvented ad hoc) and `R8`
(Persistence too minimal for Audit's needs) were both updated in
place — `R4` retired (the abstraction now exists, exactly as
recommended), `R8` confirmed rather than retired (the minimal shape
shipped exactly as anticipated; whether it suffices for `WP 6.5` remains
open until that Work Package actually attempts to build against it).

**`WP 6.5` (Audit Framework)** — implemented directly against the same,
unrevised architecture and Contract Review packages, and directly
against `WP 6.4`'s own shipped `IPersistenceStore` — no new persistence
mechanism was introduced. `docs/architecture/Platform Service Map.md`
gained a new Audit entry. `ADR-0045` formally settles the Logging/
Diagnostics/Audit orthogonality `Required ADRs.md` anticipated, plus
three genuine implementation-phase decisions the Contract Review left
open: `RecordAsync` propagates failures rather than being literally
fire-and-forget (the performance goal is met by keeping the write
itself minimal, not by discarding the task); `IAuditQuery.QueryAsync`
is permission-gated through the existing enforcement point
(`IPermissionEvaluator`); and correlation identifiers are carried in
`Detail` under a well-known key, requiring no interface change. This
Work Package's own explicit Persistence Validation concluded
`IPersistenceStore` is adequate for this release's own correctness
needs — `docs/releases/v0.6.0/Risk Register.md`'s own `R8` is confirmed
again, not retired, and `docs/governance/Quality/Technical Debt
Register.md` gained a new, permanent item (`TD-12`) disclosing the
same client-side-filtering performance characteristic as a
cross-release concern, not merely a release-scoped risk. This Work
Package's own repository review also found and fixed a genuine,
deterministic bug in `WP 6.1`/`WP 6.4`'s own already-committed
Host-registration tests (see Repository Health, above).

**`WP 6.2` (Notification Framework)** — implemented directly against the
same, unrevised architecture and Contract Review packages.
`docs/architecture/Platform Service Map.md` gained a new Notifications
entry, following the identical documentation shape every prior new
platform service's own entry has used. `ADR-0046` formally settles the
Event-Bus-orthogonality question `Required ADRs.md` anticipated, plus
the genuine C# generic-constraint impossibility implementation
surfaced (literal delegation from `NotificationDispatcher` to
`IEventBus` is not possible without illegally tightening a generic
constraint or resorting to reflection — resolved by mirroring
`EventBus`'s own internal shape instead), the additive
`IPlatformNotification`/`NotificationSeverity`/`Category` elaboration,
the deliberate `Warning`-vs-`Error` logging-level departure from the
Event Bus's own convention, and the exact-static-type-dispatch defect
found and fixed against this Work Package's own sample consumers (see
Repository Health, above). `docs/governance/Quality/Technical Debt
Register.md`'s `AT-03` and `AT-07` entries were each annotated in
place — `AT-03` because the same exact-type-dispatch limitation now
also applies to Notifications; `AT-07` to disclose that a real,
non-infrastructure hosted service now exists without claiming its
retirement, which remains assigned to `WP 6.3`. One new, disclosed
trade-off (`AT-08`) records the deliberate absence of a persistent
notification model this release, per the approved contract's own
Persistence Requirements ("None"). This Work Package's own re-derivation
of `Namespace Register.md`'s per-namespace file counts, directly via
`grep` rather than trusting the existing table, found the `Tempest.Samples`
row itself had drifted stale since `WP 5.2` — corrected in the same
commit as the finding.

**`WP 6.0` (Reporting Framework)** — implemented directly against the
same, unrevised architecture and Contract Review packages, and the
first of `v0.6.0`'s five implemented Work Packages to match its own
nominal numeric position. `docs/architecture/Platform Service Map.md`
gained a new Reporting entry, following the identical documentation
shape every prior new platform service's own entry has used. `ADR-0040`
formally settles the Reporting-vs-Export/Import orthogonality
`Required ADRs.md` anticipated — the last remaining reserved-ADR-number
gap (`ADR-0040`) is now filled, so `docs/adr/` runs `ADR-0001` through
`ADR-0046` with no gaps at all — plus two genuine implementation-phase
decisions the Contract Review left open: the additive
`IReportTemplate<TDefinition>`/`PlainTextReportTemplate<TDefinition>`
elaboration (filling the brief's own "Template abstraction" deliverable
without touching any approved interface), and a deliberate decision
*not* to build an "Export abstraction" despite the brief naming it as
scope — doing so would duplicate `WP 6.7`'s own future scope and
contradict this very ADR's own orthogonality decision.
`docs/governance/Quality/Technical Debt Register.md` gained one new,
disclosed trade-off (`AT-09`, no delivery-channel abstraction or
durable report history this release) — matching the approved contract's
own Future Extension Points exactly, not a newly-discovered gap. This
Work Package's own dedicated `WP6.0 Platform Integration
Demonstration.md` records, for each of Identity/Settings/Persistence/
Audit/Notifications, whether it was used, why, and what the coupling
rationale is — Persistence is the one deliberately not consumed,
matching the approved contract's own "Persistence Requirements: None."

**`WP 6.3` (REST API)** — implemented directly against the same,
unrevised architecture and Contract Review packages, and the second of
`v0.6.0`'s six implemented Work Packages to match its own nominal
numeric position. `docs/architecture/Platform Service Map.md` gained a
new REST API entry, following the identical documentation shape every
prior new platform service's own entry has used. Four new ADRs:
`ADR-0047`/`ADR-0048`/`ADR-0049` formally settle the three questions
`Required ADRs.md` anticipated (hosted-service placement, Command
Framework dispatch, ASP.NET Core/Kestrel adoption); `ADR-0052` is
genuinely implementation-driven, not anticipated by `Required ADRs.md`
at all, resolving `Risk Register.md`'s own `R1` residual mitigation
("must decide whether `CurrentPrincipalAccessor` needs to become
request-scoped") — empirically, not by reasoning alone: an
`AsyncLocal<T>`-backed implementation was built and tested directly,
regressed 17 pre-existing tests, and was rejected in favour of a
per-request, non-mutating identity resolution that never touches the
shared ambient state. `docs/governance/Engineering/Hosted Services
Register.md` gained its first two entries and its own Coverage Status
correction — a disclosed governance-documentation finding: this
register was never updated when `WP 6.2` added
`NotificationSampleHostedService`, so its own "zero production hosted
services exist" text survived, stale, through an entire Work Package
that directly contradicted it. `docs/governance/Quality/Technical Debt
Register.md` gained three new tracked debt items (`TD-13` no real
authentication, `TD-14` no TLS, `TD-15` an ambient-principal
Audit-attribution gap under REST invocation) and one new trade-off
(`AT-10`, no request-parameter binding); `AT-07` ("Zero real hosted
services exist beyond the infrastructure") is updated from disclosed-
but-not-claimed (`WP 6.2`'s own wording) to genuinely Retired — this is
the Work Package that trade-off's own revisit trigger explicitly named
in advance; `TD-04` (the `IHostedService` naming-proximity concern) is
annotated, since real usage evidence (a genuine ASP.NET Core dependency
now coexisting with this platform's own, differently-shaped
`IHostedService`) has arrived for the first time.

**`WP 6.7` (Export/Import)** — implemented directly against the same,
unrevised architecture and Contract Review packages, completing the
Reporting/Export/Import orthogonality question `ADR-0040` first raised.
`docs/architecture/Platform Service Map.md` gained a new Export/Import
entry, following the identical documentation shape every prior new
platform service's own entry has used. One new ADR, `ADR-0051`, formally
settles the orthogonality question `Required ADRs.md` anticipated and
records two further genuine implementation-phase decisions the Contract
Review left open: the additive `IExportableKind`/`IImportable`
Kind-routing mechanism (closing the approved contract's own
multi-destination-import gap without changing `IImportService`'s own
shape, via a concrete-type dual registration reusing `ADR-0044`'s own
`CurrentPrincipalAccessor` precedent), and the additive
`IExportFormat`/`IExportPayloadSerializer` pair filling the brief's own
"Format abstraction"/"Serialization abstraction" scope. `docs/governance/
Quality/Technical Debt Register.md` gained two new trade-offs (`AT-11`
no compression/encryption of exported content, `AT-12` no
schema-upgrade/migration path) — both matching the approved contract's
own Future Extension Points exactly, not newly-discovered gaps. This
Work Package's own repository review found three further, genuine,
pre-existing drifts, none related to its own scope: `Platform Service
Map.md`'s own Audit and Notifications "Consumers" entries had read
"none yet implemented" since before `WP 6.0` first shipped a real
consumer of each — corrected here; and `docs/governance/Engineering/
Interface Register.md`, `Dependency Injection Register.md`, and `Module
Register.md` had each gone stale since `WP 5.2`, missing every public
interface, DI registration, and sample module `WP 6.1` through `WP 6.3`
added — each register's own Coverage Status is corrected from
"Complete" to "Partial," disclosing the exact gap, with only this Work
Package's own new entries added and the larger, six-Work-Package
backfill explicitly left for `WP 6.8` (Platform Services Integration
Review).

**`WP 6.6` (Licensing Framework)** — implemented directly against the
same, unrevised architecture and Contract Review packages, and the
final production implementation Work Package of `v0.6.0`.
`docs/architecture/Platform Service Map.md` gained a new Licensing
entry, following the identical documentation shape every prior new
platform service's own entry has used — the last such entry this
release will add. One new ADR, `ADR-0050`, formally settles the
placement question `Required ADRs.md` anticipated (pre-container leaf,
Host-fatal on invalid) and resolves a genuine question the architecture
phase left explicitly open: `Risk Register.md`'s own `R5` asked whether
every "invalid" category (missing, expired, malformed) warrants
Host-fatal treatment. `ADR-0050` answers precisely: missing is a valid,
unrestricted-but-uncapable default, never Host-fatal; expired and
malformed are Host-fatal, per `ADR-0013`'s existing classification,
unmodified. `docs/governance/Quality/Technical Debt Register.md` gained
one new tracked debt item (`TD-16`, no cryptographic license file
signature verification, mirroring `TD-13`'s own undisclosed-
authentication precedent for the REST API) and one new trade-off
(`AT-13`, no remote validation/activation, floating/seat-based
licensing, or renewal/grace-period model — all named explicitly in the
approved contract's own Future Extension Points). This Work Package's
own repository review re-derived every touched register directly and
found no further stale figures beyond what `WP 6.7`'s own review had
already disclosed (the `Interface Register.md`/`Dependency Injection
Register.md`/`Module Register.md` gap, still `Partial`, still left for
`WP 6.8`'s own backfill — this Work Package added only its own three
new interfaces, one new registration, and one new module to each,
exactly as `WP 6.7` did).

**`WP 6.8` (Platform Services Integration Review & Release
Certification)** — a certification review, not a feature Work Package;
no architecture or Contract Review document was revised, and no
production code was written. `Interface Register.md`, `Dependency
Injection Register.md`, and `Module Register.md` were each fully
backfilled — every one of the 64 public interfaces, 26 named DI
registrations, and 15 production modules TempestOS ships is now
correctly recorded, closing the gap `WP 6.7` first disclosed and `WP
6.6` correctly left in place. `docs/releases/v0.6.0/Risk Register.md`
gained four status updates (`R2`, `R3`, `R4` fully closed with fresh
evidence; `R6` updated to reflect its own partial, not perfect,
mitigation). No new ADR was produced — this Work Package audits
decisions already made; it does not make new ones. Nine completion
deliverables were produced under `docs/releases/v0.6.0/`, prefixed
`WP6.8`, culminating in a `CERTIFIED WITH ACCEPTED TECHNICAL DEBT`
recommendation.

## Academy Status

86 articles across 7 categories (Introduction, Engineering Principles,
Runtime Architecture, Work Package retrospectives, Design Patterns, Case
Studies, Engineering Standards), plus `Academy Index.md`, `Academy
Masterclass Roadmap.md`, `Academy Audit Report.md`, and `Contributor
Learning Path.md` — re-derived directly (`find docs/academy -name
"*.md"`) by `WP 6.8`, consistent with `WP 6.0`'s/`WP 6.2`'s/`WP 6.3`'s/
`WP 6.7`'s/`WP 6.6`'s own prior passes. Every completed Work Package has a matching
retrospective, including `WP 5.0A` through `WP 5.4`. Maintenance
obligation (Engineering Governance §6) verified honoured by two
independent audits (`WP 4.4F`, and the Academy Register built during
`WP 4.5A`); `WP 5.0A` updated two existing articles' (`06-platform-
layering.md`, `08-failure-isolation.md`) "Future Evolution" sections,
`WP 5.0B` confirmed those predictions against the real implementation
with no correction needed, `WP 5.0D` added a genuine, non-obvious
implementation finding (`const` fields not forcing assembly load) to the
Shell's own concept guide, `WP 5.0S` added a new "Security" category
teaching threat modelling, secure plugin architecture, trust boundaries,
and least privilege from first principles, `WP 5.1A` added a new
"Command Framework" category (a new concept guide,
`11-command-framework.md`) and updated `08-failure-isolation.md` with a
genuinely new, fifth failure-isolation case (Case 5 — Command Dispatch:
propagate, don't isolate) that document's own "Future Evolution" section
had explicitly anticipated testing, `WP 5.1B` added the matching
implementation retrospective and confirmed the concept guide's own
design against the real, working implementation, with one genuine,
non-obvious implementation finding (`CommandHandlerTable`) added to both,
`WP 5.2` added a new "Diagnostics" category (a new concept guide,
`12-diagnostics-and-composite-logging.md`) plus its own retrospective,
teaching the `Func<T>` lazy-accessor pattern and the two-named-debts
decision from first principles, `WP 5.3` updated `Building a
Module.md` in place (no new concept guide — this Work Package extends
already-covered material, not a new platform capability) plus its own
retrospective, teaching why a scoped tooling Work Package legitimately
has no preceding architecture phase, and `WP 5.4` added a `v0.5.0
Release Retrospective` — deliberately shaped around what was achieved,
architectural lessons, implementation lessons, repository maturity, and
recommendations for `v0.6.0`, rather than the standard 13-section
per-feature template, since a whole-release verification pass is not the
same kind of document as a single capability's own design retrospective
(disclosed explicitly in that document's own "What This Document Is").
`WP 6.1` added the 13-section `WP6.1-permissions-and-identity-
implementation.md` retrospective — the first Academy retrospective of
the Platform Services phase — teaching the config-sourced Role model,
the fail-closed-by-default identity resolution decision, and the
`AsyncLocal<T>`-vs-ambient-field finding from first principles. `WP 6.4`
added `WP6.4-settings-framework-implementation.md`, teaching the
shared-Persistence-abstraction decision, the per-key async-lock pattern
reused across two services, and the deliberate choice not to add a
sensitive-value flag to an approved interface for a need this release
does not yet have. `WP 6.5` added `WP6.5-audit-framework-
implementation.md`, teaching the Logging/Diagnostics/Audit
orthogonality decision, the fire-and-forget-vs-awaited recording-model
tension and its resolution, the correlation-identifier-via-`Detail`
convention, and — as a genuine, disclosed engineering-review finding,
not a planned lesson — the premature-resource-disposal bug found in
two prior Work Packages' own Host-registration tests. `WP 6.2` added
`WP6.2-notification-framework-implementation.md`, teaching the
Event-Bus-orthogonality decision, the genuine C# generic-constraint
impossibility that prevented literal delegation to `IEventBus` and its
resolution (mirror the internal shape instead), the additive
severity/category elaboration, the deliberate `Warning`-vs-`Error`
logging-level departure, and — as a genuine, disclosed engineering-review
finding, not a planned lesson — the exact-static-type-dispatch defect
found against this Work Package's own sample consumers while writing
their integration tests. `WP 6.0` added
`WP6.0-reporting-framework-implementation.md`, teaching the
Reporting-vs-Export/Import orthogonality decision, the additive
Template Strategy elaboration (data/layout/rendering separation without
touching any approved interface), the deliberate choice not to build an
"Export abstraction" despite the brief naming it as scope, and the
cross-service integration pattern (permission check, Audit record,
Notifications publish, all at the calling layer, never inside
`IReportingService` itself) as a concrete precedent any future
Reporting consumer can copy directly. `WP 6.3` added
`WP6.3-rest-api-implementation.md`, teaching the hosted-service/Command-
Framework/Kestrel-adoption decisions (`ADR-0047`/`ADR-0048`/`ADR-0049`),
the empirically-verified `CurrentPrincipalAccessor` decision
(`ADR-0052`) — including the 17-test regression the rejected
`AsyncLocal<T>` alternative actually produced, not merely a theoretical
concern — and the `Detail`-carried-caller-identity convention for
REST-originated Audit records. `WP 6.7` added
`WP6.7-export-import-implementation.md`, teaching the Kind-routing
mechanism that closes the approved contract's own multi-destination-
import gap without changing any approved interface, the reuse of
`ADR-0044`'s own dual-registration pattern for a structurally identical
problem, the two-abstraction (Format/Serialization) resolution to the
brief's own named scope, and — as a genuine, disclosed
engineering-review finding, not a planned lesson — the three
governance-documentation drifts (two Platform Service Map consumer
entries, and three registers stale since `WP 5.2`) found while
re-deriving every touched register directly. `WP 6.6` added
`WP6.6-licensing-framework-implementation.md`, teaching the
missing-file-vs-broken-file Host-fatal resolution to `Risk Register.md`'s
own `R5`, the pre-container leaf-construction pattern
`PlatformVersionProvider` already established and this Work Package
reused rather than reinvented, and — proven directly, not merely
assumed — that this design change regresses none of the 24 pre-existing
tests that build a real `TempestHost`. `WP 6.8` added
`WP6.8-platform-services-integration-review.md`, mirroring `WP 5.4`'s
own whole-release-review format (What Was Achieved, Architectural
Lessons, Implementation Lessons, Repository Maturity, Recommendations,
Key Takeaways, rather than the standard 13-section per-feature
template) — teaching that a closing, whole-release review is not a
formality, that re-verifying a risk's own claimed resolution against
fresh evidence is cheap and catches real staleness, and that a
"Certified With Accepted Technical Debt" outcome is more honest than a
bare "Certified for Release" whenever a release ships disclosed,
deliberate limitations.

## Governance Status

27 registers (32 governance documents total, including the Index,
Philosophy, Audit Report, Maturity Report, and Future Work Package
Guidelines), fully cross-referenced, re-verified during `v0.4.0` Release
Engineering and every Work Package since. Traceability for both
Navigation and the Shell is complete end to end — no Pending cells remain
(`docs/governance/Delivery/Traceability Matrix.md`). One stale, pre-
existing entry unrelated to `WP 5.0D`'s own scope was found and corrected
along the way: `Technical Debt Register.md`'s TD-07. `WP 5.0S` added two
new, disclosed debt items following its own security audit — `TD-09`
(plugin trust boundary) and `TD-10` (Navigation ownership gap) — both
Open, both requiring a future Architecture Work Package, neither a
regression of anything previously Resolved. `Decision Register.md`
gained `D-017` (conducting `WP 5.0S` as a dedicated security audit Work
Package). `WP 5.1A` added `TD-11` (command/navigation registration-order
squatting, `CMD-1`) and widened `TD-09`'s own scope to name the Command
Framework as a second affected surface; `Decision Register.md` gained
`D-018` (splitting `WP 5.1` into `WP 5.1A`/`WP 5.1B`); `Risks.md`'s R3 is
now Retired (the Event Bus/Command Framework cross-reference it required
now exists). `WP 5.1B` confirmed `TD-09`/`TD-11` present in the real
implementation exactly as designed (neither worsened, neither newly
introduced) and disclosed no new debt. Several Engineering and Delivery
registers (`Platform Services Register.md`, `Interface Register.md`,
`Namespace Register.md`, `Dependency Injection Register.md`, `Exception
Register.md`, `Module Register.md`, `Feature Register.md`, `Traceability
Matrix.md`) had drifted stale since `WP 5.0D` without any Work Package
since having touched them — found and corrected during `WP 5.1B`'s own
mandatory repository review. `WP 5.2` resolved `TD-02` (`CompositeLogSink`)
and reassessed `TD-01` (re-scoped forward again, not migrated —
`D-020`); `Decision Register.md` also gained `D-019` (the Event
Framework/Diagnostics premise redirect). ADR Register, Rejected Designs
Register, and every Engineering/Delivery register touched by this Work
Package's own new types were updated in the same commit; `Architecture
Document Register.md`'s stale Command Framework marker (see Documentation
Status, above) was corrected during this Work Package's own repository
review, unrelated to its own scope. `WP 5.3` added `RD-0045` (local-folder
vs. NuGet-packaged template distribution) and touched no Technical Debt
item (its own scope does not concern `TD-01`–`TD-11`). That Work
Package's own repository review found three further, genuine,
pre-existing drifts: (1) `RD-0042`–`RD-0044` had been added to
`Rejected Designs Register.md` during `WP 5.2` but never actually written
into `docs/architecture/Rejected Designs.md` itself — the register's own
declared Source of Truth — backfilled here, unchanged in content; (2)
`Engineering Governance.md` §11 had not been updated when `WP 5.2` added
the `Tempest.Core.Diagnostics` namespace; (3) `Governance Register.md`'s
own Compliance Matrix had not been updated since `WP 5.0D` — four
completed Work Packages (`WP 5.0S`, `WP 5.1A`, `WP 5.1B`, `WP 5.2`) were
missing entirely, and `WP 5.0D`'s own row still carried a
`*(this commit)*` placeholder never backfilled with its real hash. All
five rows backfilled, verified directly against `git log`.

**`WP 5.4` (v0.5.0 Release Candidate & Engineering Sign-Off)** performed
a full, independent repository review rather than a scope-limited one,
and found two further, genuine, silent arithmetic undercounts: the
Exception Register's own stated total (30) had never matched its own
Entries table (31, true since `WP 5.1B` first introduced the mismatch);
and the Academy Register's own "03 Work Packages" count had undercounted
its own table by one for at least two consecutive Work Packages, with
the academy-wide grand total inheriting the same undercount rather than
being independently re-derived each time. Both are now corrected, backed
by a direct file-system count rather than an incremented prior figure.
`docs/releases/v0.4.0/Risks.md`'s remaining four risks with a "residual
carries forward" caveat (`R5`, `R7`, `R8`, `R9`) were each confirmed
resolved by a specific, named `v0.5.0` Work Package and retired in
full — all ten risks in that register are now Retired. This is the
fourth Work Package in a row to find real, previously-unnoticed
governance drift during its own repository review — see `WP 5.4`'s own
retrospective, "Repository Maturity," for the standing-practice
recommendation this pattern produced.

**`WP 6.1` (Permissions & Identity)** added `ADR-0043` (Identity Model
Scope) and `ADR-0044` (Authorization Enforcement Point) — both Accepted,
both cross-referencing the `Required ADRs.md` catalogue entry each
formally authors. `Technical Debt Register.md`'s `TD-09`, `TD-10`, and
`TD-11` entries were each updated in place — none marked Resolved, since
this Work Package deliberately built only the enforcement mechanism, not
the three follow-on calls into `NavigationService`, Command/Navigation
registration, and plugin loading that would actually close them; each
entry now names `ADR-0044` as the mechanism a future, explicitly-scoped
Work Package should use. No new Technical Debt item was found stale or
drifted during this Work Package's own repository review.

**`WP 6.4` (Settings Framework)** added `ADR-0041` (Shared Persistence
Abstraction) and `ADR-0042` (Settings Distinct From Configuration) —
both Accepted, both formally authoring their own `Required ADRs.md`
catalogue entry. `docs/releases/v0.6.0/Risk Register.md`'s `R4` is now
Partially Retired (the abstraction exists; whether `WP 6.5` actually
reuses it remains open) and `R8` is confirmed, not retired (the
anticipated "minimal, key-lookup-only" limitation shipped exactly as
predicted). No new Technical Debt item was found stale or drifted
during this Work Package's own repository review.

**`WP 6.5` (Audit Framework)** added `ADR-0045` (Audit orthogonality,
recording model, permission gating, Persistence sufficiency) — Accepted,
formally authoring its own `Required ADRs.md` catalogue entry.
`docs/releases/v0.6.0/Risk Register.md`'s `R8` is confirmed a second
time, still not retired — this Work Package's own explicit Persistence
Validation judged `IPersistenceStore` adequate for correctness, with no
extension made, and named a concrete revisit trigger (a measured
performance problem or a named scale requirement) rather than leaving
the risk vague. `docs/governance/Quality/Technical Debt Register.md`
gained a new, permanent item, `TD-12`, disclosing the same
client-side-filtering characteristic as an ongoing, cross-release
concern. This Work Package's own repository review found and fixed a
genuine, previously-uncaught bug in two already-committed Work
Packages' own test files (`WP 6.4`'s `SettingsHostRegistrationTests.cs`;
`WP 6.1`'s own `IdentityHostRegistrationTests.cs` was checked and found
unaffected, since it never used a `TempDirectory`) — corrected in the
same commit as the finding, per this project's own standing practice of
fixing drift encountered along the way, not only drift a Work Package's
own brief names.

**`WP 6.2` (Notification Framework)** added `ADR-0046` (Notifications
derived from Events, not a replacement pub/sub — dispatch model,
severity/category elaboration, logging level) — Accepted, formally
authoring its own `Required ADRs.md` catalogue entry (`ADR-0040` and
`ADR-0047`–`ADR-0051` remain reserved). `docs/governance/Quality/
Technical Debt Register.md`'s `AT-03` and `AT-07` entries were each
annotated in place (see Documentation Status, above); one new trade-off,
`AT-08`, was added. This Work Package's own repository review, which
re-derived every register touched directly rather than trusting the
existing text, found and corrected three further, genuine, pre-existing
drifts unrelated to its own scope: `ADR Register.md`'s own commit count
("48") had not been re-derived since at least `WP 6.5`, undercounting
the real, `git log`-verified figure; `Namespace Register.md`'s
`Tempest.Samples` row had drifted stale at "14" since `WP 5.2`, three
intervening Work Packages having each added files without the row being
updated; and `PROJECT_STATUS.md`'s own Academy Status section had
drifted stale at "77 articles" since before `WP 6.1`, for the identical
reason. All three are corrected in this same commit, backed by direct
repository counts, not incremented prior figures.

**`WP 6.0` (Reporting Framework)** added `ADR-0040` (Reporting is
DI-public and orthogonal to Export/Import — template abstraction,
cross-service integration, scope boundaries) — Accepted, formally
authoring its own `Required ADRs.md` catalogue entry and filling the
last remaining reserved-ADR-number gap: `docs/adr/` now runs `ADR-0001`
through `ADR-0046` with no gaps. `docs/governance/Quality/Technical
Debt Register.md` gained one new, disclosed trade-off (`AT-09`) — no
delivery-channel abstraction or durable report history this release,
matching the approved contract's own Future Extension Points, not a
newly-discovered gap. No existing Technical Debt item required
annotation — Reporting introduces no instance of any previously-tracked
gap (`TD-01` through `TD-12`, `AT-01` through `AT-08`). This Work
Package's own repository review re-derived every touched register
directly and found no further stale figures beyond what `WP 6.2`'s own
review had already corrected.

**`WP 6.3` (REST API)** added `ADR-0047` (REST API is a hosted service),
`ADR-0048` (REST dispatches through the Command Framework), `ADR-0049`
(adopting ASP.NET Core/Kestrel) — all three Accepted, formally
authoring their own `Required ADRs.md` catalogue entry — and `ADR-0052`,
a fourth, genuinely implementation-driven ADR not anticipated by
`Required ADRs.md` at all, documenting the empirically-verified
`CurrentPrincipalAccessor` decision. `docs/governance/Quality/Technical
Debt Register.md` gained three new tracked debt items (`TD-13`, `TD-14`,
`TD-15`) and one new trade-off (`AT-10`); `AT-07` is updated to Retired
and `TD-04` is annotated (see Documentation Status, above).
`docs/governance/Engineering/Hosted Services Register.md` — Partial
coverage since `WP 4.5A`, and never actually updated when `WP 6.2`
shipped this codebase's first real hosted service — is corrected here,
populated with both `NotificationSampleHostedService` and
`RestApiHostedService`, and its own Coverage Status changed to Complete.
This Work Package's own repository review, re-deriving every touched
register directly, found this one further, genuine, pre-existing
governance-documentation drift beyond what `WP 6.0`'s own review had
already confirmed clean.

**`WP 6.7` (Export/Import)** added `ADR-0051` (Export/Import Is
Orthogonal to the Internal Persistence Abstraction — Kind Routing,
Format/Serialization Abstractions, and Scope Boundaries) — Accepted,
formally authoring its own `Required ADRs.md` catalogue entry:
`docs/adr/` then ran `ADR-0001` through `ADR-0049`, then
`ADR-0051`–`ADR-0052`, with only `ADR-0050` (Licensing) still reserved.
`docs/governance/Quality/Technical Debt Register.md` gained two new
trade-offs (`AT-11`, `AT-12`); no existing Technical Debt item required
annotation. This Work Package's own repository review, re-deriving
every touched register directly, found three further, genuine,
pre-existing governance-documentation drifts, none related to its own
scope: `docs/architecture/Platform Service Map.md`'s own Audit and
Notifications "Consumers" entries had read "none yet implemented" since
before `WP 6.0` first shipped a real consumer of each — corrected here.
More substantially, `docs/governance/Engineering/Interface Register.md`,
`Dependency Injection Register.md`, and `Module Register.md` had each
gone stale since `WP 5.2` — six consecutive Work Packages' worth of
interfaces (23), DI registration call sites (10), and sample modules
(6) were missing entirely from all three, each register's own "Coverage
Status: Complete" line surviving unchallenged the entire time. Each
register's own Coverage Status is corrected to "Partial," the exact gap
disclosed explicitly, with only this Work Package's own new entries
added — a full backfill is recommended as `WP 6.8` (Platform Services
Integration Review)'s own closing-audit task, exactly the kind of
accumulated, multi-Work-Package drift that Work Package exists to
catch.

**`WP 6.6` (Licensing Framework)** added `ADR-0050` (License Validation
Is a Host-Startup, Host-Fatal Gate — Except a Missing License File,
Which Is a Valid, Unrestricted Default) — Accepted, formally authoring
its own `Required ADRs.md` catalogue entry and filling the very last
remaining reserved-ADR-number gap: `docs/adr/` now runs `ADR-0001`
through `ADR-0052` with no gaps at all — every ADR `Required ADRs.md`
ever reserved a number for is now a real, Accepted file.
`docs/governance/Quality/Technical Debt Register.md` gained one new
tracked debt item (`TD-16`, no cryptographic license file signature
verification) and one new trade-off (`AT-13`, no remote validation/
activation, floating/seat-based licensing, or renewal/grace-period
model). This Work Package's own repository review, re-deriving every
touched register directly, found no further stale figures beyond the
`Interface Register.md`/`Dependency Injection Register.md`/`Module
Register.md` gap `WP 6.7` had already disclosed as `Partial` — this
Work Package added only its own three new interfaces, one new
registration, and one new module to each, leaving the larger,
six-Work-Package backfill exactly where `WP 6.7` left it, for `WP 6.8`'s
own closing audit.

**`WP 6.8` (Platform Services Integration Review & Release
Certification)** produced no new ADR — it is a certification review,
not a decision-making Work Package. `Interface Register.md`,
`Dependency Injection Register.md`, and `Module Register.md` were each
fully backfilled, closing the gap `WP 6.7` first disclosed: all 64
public interfaces, 26 named DI registrations (28 raw `Singleton`/
`AddInstance` call sites), and 15 production modules are now correctly
recorded, and each register's own Coverage Status is corrected from
`Partial` to `Complete`. A genuine, pre-existing arithmetic drift,
unrelated to the larger gap, was found and corrected in the same pass:
`Interface Register.md`'s own Classification Summary had read
"Host-owned = 6" while its own Entries table already listed 7 rows
marked Host-owned. `docs/releases/v0.6.0/Risk Register.md` gained four
updates: `R2` and `R3` fully closed with fresh, independent evidence
(`git log`'s own commit order; a fresh `grep` of
`RestApiHostedService.cs`); `R4` fully closed (Audit's own reuse of
Persistence re-confirmed directly); `R6` updated to disclose that its
own mitigation held only partially, not perfectly — exactly the
Interface/DI/Module Register drift this same risk predicted, now fully
corrected. Nine completion deliverables were produced, disposing of
every Technical Debt Register item (29 total: 16 tracked, 13 trade-offs
— zero Release Blocking) and every Risk Register entry (8 total: 5
Closed, 2 Mitigated, 1 Remaining by deliberate design) explicitly.

## Known Unknowns

Recorded honestly, not guessed at — full detail in `docs/governance/
Governance Audit Report.md`:

1. `docs/releases/v0.2.0/` — an empty directory; whether v0.2.0 was ever
   released, skipped, or reserved is unknown.
2. `docs/roadmap/`, `docs/diagrams/` — both empty, both unreferenced by
   any document reviewed; intended purpose unknown.
3. Exact original authorship of four pre-Claude namespaces
   (`Tempest.Core.Hosting`, `Bootstrap`, `Projects`, `Repositories`) and
   seven unnamespaced bootstrap-era files.
4. A five-day gap in earliest git history (2026-07-15 to 2026-07-21).
5. v0.1.0's full scope beyond its own commit message.
6. Intermediate historical test-count totals for `WP 4.1` and `WP 4.3`
   (each retrospective states only the tests it added, not a running
   total).

## Current Priorities

1. **`v0.6.0` is released.** Merged into `main` (non-fast-forward,
   `99ed285`), tagged `v0.6.0`, both pushed to `origin` — Product
   Approval was granted and Release Engineering executed in full. See
   `docs/releases/v0.6.0/WP6.8 Platform Certification Report.md` for
   the certification decision and evidence this release shipped under.
2. **`WP 7.0A` (Future Capability Register & Product Vision) is
   complete — awaiting Engineering Review.** `VISION.md`,
   `docs/governance/Future Capability Register.md`, `Capability
   Categories.md`, and `Product Roadmap.md` are all established.
   `WP 7.0B` (implementation Work Package selection) does not begin
   until Engineering Review of `WP 7.0A` completes, per that Work
   Package's own explicit closing instruction.
3. **Await Architecture/Planning/Contract Review for `v0.7.0`'s own
   implementation scope.** `docs/releases/v0.7.0/WorkPackages.md`
   records candidate items only, cross-referenced against `Future
   Capability Register.md` entries; none is yet an approved Work
   Package. No implementation is authorised until that review phase
   produces an approved scope, mirroring `v0.6.0`'s own
   pre-implementation discipline.
4. A GitHub Release for `v0.6.0` has not yet been created (`gh` CLI
   unavailable in this environment) — see the Release Summary for the
   exact command or manual steps to complete it.

## Near-Term Roadmap

Per `docs/releases/v0.5.0/WorkPackages.md`, the Developer Experience
phase is complete and released as `v0.5.0`, `WP 5.0A` through `WP 5.4`:

- `WP 5.0A` — Navigation Framework Architecture (design only). **Complete.**
- `WP 5.0B` — Navigation Framework Implementation. **Complete.**
- `WP 5.0C` — Shell & Composition Framework Architecture (design only). **Complete.**
- `WP 5.0D` — Shell & Composition Framework Implementation. **Complete.**
- `WP 5.0S` — Platform Security Baseline Audit (dedicated engineering
  audit, not a feature Work Package). **Complete.**
- `WP 5.1A` — Command Framework Architecture (design only). **Complete.**
- `WP 5.1B` — Command Framework Implementation. **Complete.**
- `WP 5.2` — Diagnostics Improvements (composite logging, `TD-01`
  reassessment, `IDiagnosticsProvider`). **Complete.**
- `WP 5.3` — Developer Experience Improvements (module template,
  clearer Discovery message). **Complete.**
- `WP 5.4` — v0.5.0 Release Candidate & Engineering Sign-Off (verification,
  not a feature Work Package). **Complete.**

Per `docs/releases/v0.6.0/WorkPackages.md`, the Platform Services phase
is under way:

- `WP 6.0` — Reporting Framework. **Complete.**
- `WP 6.1` — Permissions & Identity. **Complete.**
- `WP 6.2` — Notification Framework. **Complete.**
- `WP 6.3` — REST API. **Complete.**
- `WP 6.4` — Settings Framework. **Complete.**
- `WP 6.5` — Audit Framework. **Complete.**
- `WP 6.6` — Licensing Framework. **Complete.**
- `WP 6.7` — Export / Import. **Complete.**
- `WP 6.8` — Platform Services Integration Review & Release
  Certification (closing milestone audit, mirroring `WP 4.2D`/
  `WP 5.0S`'s own precedent). **Complete — CERTIFIED WITH ACCEPTED
  TECHNICAL DEBT.**

**The Platform Services phase is complete and released.** `v0.6.0` is
merged to `main`, tagged, and pushed. TempestOS is now in the
**Engineering Foundation** phase (`v0.7.0`). `WP 7.0A` (this Work
Package) is complete; its own implementation Work Package(s) are not
yet scoped — see `docs/releases/v0.7.0/WorkPackages.md` and
`docs/governance/Future Capability Register.md` for candidate items
pending Architecture/Planning/Contract Review.

- `WP 7.0A` — Future Capability Register & Product Vision (architecture
  and governance only; no production code). **Complete — awaiting
  Engineering Review.**

## Long-Term Vision

**`VISION.md` (repository root) is now the authoritative, complete
product vision document** — established by `WP 7.0A`, superseding the
brief paragraph this section previously held as the only vision
statement in the repository. This section is kept short deliberately;
read `VISION.md` for the full account (what TempestOS is, why it
exists, target users, engineering and architectural philosophy, product
principles, what it deliberately is not, the Platform-vs-Engineering-
Module boundary, and the vision beyond `v1.0`).

In summary: TempestOS aims to be an extensible platform other people
build on, not merely a runtime that hosts a fixed set of built-in
capabilities — see `docs/releases/v0.4.0/ReleasePlan.md`'s own "From
Runtime to Platform" theme, the origin of this ambition. The Platform
Services phase (`v0.6.0`) proved cross-service platform capability;
`VISION.md` now names what that platform is *for* — engineering-practice
capability across the nine Engineering Discipline categories
`docs/governance/Capability Categories.md` establishes, beginning with
Systems Engineering and Project Management (the "Requirements Engine"
and "Project Engine" `ADR-0013`'s own Future Considerations already
named), each still requiring its own explicit Platform-Service-vs-Module
classification before design begins. The governing constraint on all of
it remains `docs/releases/FOUNDATION.md`: every future capability is a
module or platform service running inside the one Runtime Host this
foundation established, never a second, parallel execution model — and
every future Work Package is expected to build capability against that
stable foundation rather than revisit it, absent evidence that requires
otherwise (see `docs/governance/Future Work Package Guidelines.md`).

---

## Maintaining This Document

Update this file as part of the Definition of Done for any Work Package
that changes: the current branch, release, or Work Package; Foundation
status; a Repository Metrics figure; Repository Health; or a Known
Unknown being resolved. Keep it short — this is a dashboard, not a
narrative; link to the fuller document (Governance suite, Academy,
`WorkPackages.md`) rather than inlining detail that belongs there.
