# TempestOS — Project Status

**Last Updated:** 2026-07-29 (`WP 6.5` — Audit Framework, implemented)

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
`WP 6.1` (Permissions & Identity), `WP 6.4` (Settings Framework), and
`WP 6.5` (Audit Framework) are now all implemented — each ahead of its
own nominal numeric order (`WP 6.0` is listed first in
`WorkPackages.md`), per `Platform Service Implementation Order.md`'s
own explicit recommendation. `WP 6.5` reuses the Persistence abstraction
`WP 6.4` established, exactly as that recommendation anticipated, rather
than introducing a second storage mechanism. See Current Work Package,
below.

## Current Development Branch

**`feature/v0.6.0-platform-services`**, cut from `main` at the `v0.5.0`
tag. `WP 6.1` (Permissions & Identity), `WP 6.4` (Settings Framework),
and `WP 6.5` (Audit Framework) are implemented on this branch; no other
`v0.6.0` Work Package has begun. `feature/v0.5.0-developer-experience`
(`WP 5.0A` through `WP 5.4`) has been merged into `main` and is
retained, unmerged branches are never deleted per this project's own
convention.

## Current Release

**v0.5.0** ("Developer Experience") — released 2026-07-29. Root
`VERSION` reads `0.5.0`. `v0.4.0` ("Platform Foundation") is the release
before that; `v0.3.0` ("Runtime Foundation Complete") before that.

## Current Work Package

**`WP 6.5` — Audit Framework — implemented.** The third Work Package of
the Platform Services phase (`v0.6.0`) to ship real code, following
`WP 6.1`/`WP 6.4`'s own precedent of implementing directly against the
already-approved architecture and Contract Review packages, no separate
architecture phase. Delivers `Tempest.Core.Audit`
(`IAuditRecord`/`IAuditRecorder`/`IAuditQuery`/`AuditQueryCriteria`,
exactly as approved), reusing `WP 6.4`'s own `IPersistenceStore` rather
than introducing a second storage mechanism, exactly as directed.
`AuditRecorder.RecordAsync` resolves the current principal automatically
and is awaited (not literally fire-and-forget), so a storage failure
always propagates; `IAuditQuery.QueryAsync` is permission-gated through
the existing, single enforcement point (`IPermissionEvaluator`,
`ADR-0044`). Correlation identifiers are carried in `Detail` under a
well-known key, requiring no interface change. **Persistence Validation:**
`IPersistenceStore` was judged adequate for this release's own
correctness needs — no extension was made; `docs/releases/v0.6.0/Risk
Register.md`'s own `R8` is confirmed, not retired, its revisit trigger
now sharpened to a real, measured performance need. One new, permanent
Technical Debt item (`TD-12`) discloses the client-side-filtering
performance characteristic this confirms. One new ADR (`ADR-0045`),
`AuditSampleModule` (the tenth production sample module), 55 new tests
(773 total, 0 failures), 0 build warnings, both Debug and Release. This
Work Package's own repository review also found and fixed a genuine,
deterministic bug in `WP 6.1`/`WP 6.4`'s own already-committed
Host-registration tests (a `using`-scoped resource disposed before its
awaited operation actually completed) — see its own Lessons Learned. See
its own retrospective: `docs/academy/03 Work Packages/
WP6.5-audit-framework-implementation.md`.

## Next Planned Work Package

**`WP 6.0` — Reporting Framework**, next in `WorkPackages.md`'s own
nominal numeric order — see `docs/releases/v0.6.0/WorkPackages.md` for
the full, nine-Work-Package plan (`WP 6.0` through `WP 6.8`). Per this
Work Package's own explicit closing instruction, implementation stops
here pending engineering approval — no further Work Package is to begin
regardless of `Platform Service Implementation Order.md`'s own
recommended sequencing.

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
| Automated tests | 773 (0 failures) — **+55, `WP 6.5`**: unit, failure-injection, concurrency, query-filter, Host registration-validation, and sample-module integration/durability tests for Audit |
| ADRs | 44 (`ADR-0001`–`ADR-0039`, `ADR-0041`–`ADR-0045`), all Accepted — **+1, `WP 6.5`**: `ADR-0045` (Audit — orthogonality, recording model, permission gating, Persistence sufficiency). `ADR-0040` and `ADR-0046`–`ADR-0051` remain reserved, not yet authored, per `docs/releases/v0.6.0/Required ADRs.md` |
| Rejected Designs | 45 (`RD-0001`–`RD-0045`) — unchanged by `WP 6.5` (no rejected design produced; alternatives-considered sections recorded within `ADR-0045` itself) |
| Academy articles | 80 (see `docs/governance/Documentation/Academy Register.md`) — **+1, `WP 6.5`**: `WP6.5-audit-framework-implementation.md` |
| Governance registers | 27 (32 governance documents total), plus 4 standing security documents under `docs/security/` (not governance registers themselves, indexed from `Governance Index.md`'s Security section) |
| Architecture documents | 20 under `docs/architecture/` (22 including the two release-scoped documents) — unchanged by `WP 6.5` (Platform Service Map.md updated in place, not a new document) |
| Platform services | 21 catalogued — 18 Implemented, 2 not implemented as platform services, 1 developer-convenience layer — **+1, `WP 6.5`**: Audit |
| Modules (production) | 10 (`ClockModule`, `ClockLifecycleObserverModule`, `NavigationSampleModule`, `SecondaryNavigationSampleModule`, `DuplicateNavigationSampleModule`, `CommandSampleModule`, `DiagnosticsSampleModule`, `IdentitySampleModule`, `SettingsSampleModule`, `AuditSampleModule`) |
| Hosted services (production) | 0 — infrastructure fully implemented and tested; zero shipped consumers by deliberate scope decision |
| Plugins (production) | 0 — infrastructure fully implemented and tested; `src/Plugins/` empty by deliberate scope decision |
| Custom exception types | 40 — **+1, `WP 6.5`**: `AuditException` (base only — every current Audit failure mode is already covered by an existing exception type from another namespace) |
| Technical Debt Register items | 12 tracked — **+1, `WP 6.5`**: `TD-12` (`IPersistenceStore` has no native query/filter capability; disclosed, confirmed, not extended speculatively) |
| Commits (this release, `v0.5.0` → `v0.6.0`, so far) | 5 (`v0.6.0` branch/documentation preparation, `WP 6.1` implementation, `WP 6.4` implementation, `WP 6.5` implementation, plus this update) |
| Contributors | 1 (repository owner; all commits co-authored by Claude) |

*(This table is generated from `docs/governance/Quality/Repository Metrics
Register.md` and `docs/releases/v0.4.0/Release Notes.md` — update all
three together.)*

## Repository Health

- **Build:** Clean — 0 warnings, 0 errors (`dotnet build tests/Tempest.Core.Tests/Tempest.Core.Tests.csproj`, both Debug and Release configurations, verified directly by `WP 6.5`).
- **Tests:** 773/773 passing (+55, `WP 6.5`), verified in both Debug and
  Release configurations from a clean rebuild. This Work Package's own
  repository review also found and fixed a genuine, deterministic bug in
  two already-committed test files (`WP 6.1`'s `IdentityHostRegistrationTests.cs`
  was unaffected; `WP 6.4`'s `SettingsHostRegistrationTests.cs` and this
  Work Package's own `AuditHostRegistrationTests.cs` both had it) — a
  `using`-scoped `TempDirectory` disposed the instant a non-`async` test
  method returned its still-running `Task`, before the awaited operation
  inside it actually completed. Fixed by making every affected test
  method genuinely `async Task`, awaiting directly.
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

## Academy Status

77 articles across 7 categories (Introduction, Engineering Principles,
Runtime Architecture, Work Package retrospectives, Design Patterns, Case
Studies, Engineering Standards), plus `Academy Index.md`, `Academy
Masterclass Roadmap.md`, `Academy Audit Report.md`, and `Contributor
Learning Path.md`. Every completed Work Package has a matching
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
two prior Work Packages' own Host-registration tests.

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

1. **Await engineering approval before any further `v0.6.0`
   implementation begins.** `WP 6.1` (Permissions & Identity), `WP 6.4`
   (Settings Framework), and `WP 6.5` (Audit Framework) are all complete
   on `feature/v0.6.0-platform-services`; per `WP 6.5`'s own explicit
   closing instruction, no further Work Package is to begin next,
   regardless of `Platform Service Implementation Order.md`'s own
   recommended sequencing.
2. Once approved, the next Work Package is either `WP 6.0` (Reporting
   Framework, next in `WorkPackages.md`'s own nominal numeric order) or
   whichever Work Package engineering review directs — see
   `docs/releases/v0.6.0/WorkPackages.md` for the full, nine-Work-Package
   plan.
3. No merge to `main` is due until the Platform Services phase's Work
   Packages are complete (see `docs/releases/v0.6.0/WorkPackages.md`).

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

- `WP 6.0` — Reporting Framework. Not started.
- `WP 6.1` — Permissions & Identity. **Complete.**
- `WP 6.2` — Notification Framework. Not started.
- `WP 6.3` — REST API. Not started; blocked on `WP 6.1`, now satisfied.
- `WP 6.4` — Settings Framework. **Complete.**
- `WP 6.5` — Audit Framework. **Complete.**
- `WP 6.6` — Licensing Framework. Not started.
- `WP 6.7` — Export / Import. Not started.
- `WP 6.8` — Platform Services Integration Review (closing milestone
  audit, mirroring `WP 4.2D`/`WP 5.0S`'s own precedent). Not started.

## Long-Term Vision

TempestOS aims to be an extensible platform other people build on, not
merely a runtime that hosts a fixed set of built-in capabilities — see
`docs/releases/v0.4.0/ReleasePlan.md`'s own "From Runtime to Platform"
theme. The Platform Services phase (`v0.6.0`) is the next concrete step:
Reporting, Permissions & Identity, Notifications, a REST API, Settings,
Audit, Licensing, and Export/Import, each a domain-facing capability
built *on* the platform the first two releases established, not a
redesign of it. Two further, not-yet-designed platform services (Project
Engine, Requirements Engine) remain aspirational beyond that, each
requiring its own classification under ADR-0013 before design begins.
The governing constraint on all of it is `docs/releases/FOUNDATION.md`:
every future capability is a module or platform service running inside
the one Runtime Host this foundation established, never a second,
parallel execution model — and every future Work Package is expected to
build capability against that stable foundation rather than revisit it,
absent evidence that requires otherwise (see `docs/governance/Future Work
Package Guidelines.md`).

---

## Maintaining This Document

Update this file as part of the Definition of Done for any Work Package
that changes: the current branch, release, or Work Package; Foundation
status; a Repository Metrics figure; Repository Health; or a Known
Unknown being resolved. Keep it short — this is a dashboard, not a
narrative; link to the fuller document (Governance suite, Academy,
`WorkPackages.md`) rather than inlining detail that belongs there.
