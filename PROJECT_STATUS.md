# TempestOS — Project Status

**Last Updated:** 2026-07-30 (`WP 8.1A` — Workspace Shell)

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
(non-fast-forward, `99ed285`), tagged `v0.6.0`, and pushed. TempestOS
then completed the **Engineering Foundation** phase (`v0.7.0`) in full —
thirteen Work Packages (`WP 7.0A`–`WP 7.4.0`) across two sequential
programmes, closed by `WP 7.4.0`'s own release-readiness review
recommending **APPROVED**. Product Approval was granted and `v0.7.0`
was released in full: merged to `main` (non-fast-forward, `61fb2db`),
tagged `v0.7.0`, and pushed. TempestOS is now in the **Engineering
Workspace** phase (`v0.8.0`), on
`feature/v0.8.0-engineering-workspace`. See Current Work Package,
below.

## Current Development Branch

**`feature/v0.8.0-engineering-workspace`**, cut from `main` at the
`v0.7.0` tag, per `v0.7.0`'s own Release Engineering closing activity
(`VERSION` bumped to `0.7.0`, matching the tagged release, mirroring
`v0.6.0`'s own identical precedent). `WP 8.0A` (Engineering Workspace
Architecture) is this branch's first Work Package — architecture and
design only, no implementation. See `docs/releases/v0.8.0/
WorkPackages.md` for current scope.

`feature/v0.7.0-engineering-foundation` (`WP 7.0A` through `WP 7.4.0`,
all thirteen Work Packages of the Engineering Foundation phase) has
been merged into `main` (non-fast-forward, `61fb2db`), tagged `v0.7.0`,
pushed, and is retained; `feature/v0.6.0-platform-services`
(`WP 6.0` through `WP 6.8`) has been merged into `main`
(non-fast-forward, `99ed285`) and is retained; `feature/v0.5.0-developer-experience`
(`WP 5.0A` through `WP 5.4`) remains merged and retained as well —
unmerged and merged feature branches are both never deleted per this
project's own convention.

## Current Release

**v0.7.0** ("Engineering Foundation") — released 2026-07-30, tagged
`v0.7.0` (merge `61fb2db`), recommended **APPROVED** by `WP 7.4.0`'s own
release-readiness review. Root `VERSION` reads `0.7.0`, bumped
immediately after the tag as part of preparing
`feature/v0.8.0-engineering-workspace`, per this project's own
established "bump after tag" precedent. `v0.6.0` ("Platform Services")
is the release before that (tagged `v0.6.0`, `99ed285`, `CERTIFIED WITH
ACCEPTED TECHNICAL DEBT`); `v0.5.0` ("Developer Experience") before
that; `v0.4.0` ("Platform Foundation") before that.

## Current Work Package

**`WP 8.1A` — Workspace Shell.** `v0.8.0`'s own first implementation
Work Package, and its third overall — the shell only, no engineering
functionality, per this Work Package's own explicit constraint.
Approved to begin directly following `WP 8.0B`'s own completion,
mirroring the Requirements Engine's own `WP 7.2C` → `WP 7.3A` sequence
exactly.

Implements all twelve `WP 8.0B`-approved contracts, compiled exactly as
specified in a new `Tempest.App.Workspace` namespace, with zero
signature change: `IWorkspace`, `IWorkspaceManager`, `IWorkspaceView`,
`IWorkspacePanel`, `IWorkspaceLayout`, `INavigationService`,
`ISelectionService`, `IWorkspaceContext`, `IWorkspaceState`,
`IProjectExplorer`, `IPropertyInspector`, `IWorkspaceCommand`.
**`WorkspaceManager`/`WorkspaceShell` are now `Tempest.App`'s own
default launch target** (`ADR-0068`) — running TempestOS presents the
five-region Workspace shell (Areas, Project Explorer, Documents,
Properties, Status Bar); console `TempestShell` remains in the
repository, fully intact, fully tested, simply no longer the default.

Two disclosed implementation-phase findings, neither requiring an
architectural revisit: `ISettingsProvider` is `string`-only, not the
generic contract `WP 8.0B` proposed (`WorkspaceState` serializes its own
DTO to JSON, mirroring `RequirementDto`'s own precedent); `ITempestHost`
is explicitly single-use, not restart-tolerant as
`WP8.0B Lifecycle Definitions.md` assumed (`WorkspaceManager.StartAsync`
now throws on a second call). 27 new production files, 91 new tests
(1406 → 1497, both configurations, clean rebuild, stable across four
runs). One new ADR (`ADR-0068`). **Zero new Technical Debt** — every
scope limitation is either already-disclosed from `WP 8.0A` or a direct
consequence of "no engineering functionality." One completion
deliverable produced under `docs/releases/v0.8.0/`
(`WP8.1A Implementation Report.md`). **Stops here, awaiting further
Product Owner instruction before the next Work Package begins.**

### `WP 8.0B` Summary (for reference)

**`WP 8.0B` — Workspace Contracts.** `v0.8.0`'s own second Work Package
— contract review only, no implementation. Defined the complete public
contract for all twelve named Workspace interfaces, each fully
specified in proposed C#, plus the supporting types they genuinely
need. Both ADRs `WP 8.0A` reserved resolved: `ADR-0066` (terminal-based
presentation, not a graphical desktop framework) and `ADR-0067`
(Kind-keyed registration for both object views and Project Explorer
nodes, mirroring `IReportDefinition`/`IReportRenderer<T>`). Four
completion deliverables, prefixed `WP8.0B` — see `docs/releases/v0.8.0/
WP8.0B Workspace Contracts.md`.

### `WP 8.0A` Summary (for reference)

**`WP 8.0A` — Engineering Workspace Architecture.** `v0.8.0`'s own first
Work Package — architecture and design only, no implementation.
Designed the complete Engineering Workspace across all twelve named
areas (workspace philosophy, user journeys, main window layout,
navigation model, Project Explorer, engineering object hierarchy,
docking strategy, view architecture, digital thread visualisation,
workspace state management, extensibility model, interaction patterns)
— a multi-panel evolution of `Tempest.App`'s own composition root,
additive to console `TempestShell`, introducing zero new Platform
Service (`ADR-0062`). Views read Engineering Core/Platform services
directly; mutations dispatch through the existing Command Framework
(`ADR-0063`); layout/session state persists via the existing
`ISettingsProvider` (`ADR-0064`); Digital Thread visualisation composes
existing reads (`ADR-0065`). `ADR-0066`/`ADR-0067` reserved, resolved
by `WP 8.0B` (above). Five completion deliverables, prefixed `WP8.0A` —
see `docs/releases/v0.8.0/WP8.0A Workspace Architecture Document.md`.

### `WP 7.4.0` Summary (for reference)

**`WP 7.4.0` — Release Preparation & Product Baseline.** A release
preparation exercise only — no new platform functionality, architecture
change, bug fix, refactoring, or feature development was performed,
per this Work Package's own explicit constraint. Approved to begin
after `WP 7.3A`'s own completion, per its own closing instruction
awaiting Product Owner instruction for whatever comes next.

Performed a complete release readiness review across seventeen named
areas (repository health, build, test, documentation, Academy,
governance registers, ADR consistency, Work Package traceability,
version consistency, dependency consistency, module/platform-service/
interface/DI inventories, Technical Debt Register, Future Capability
Register, Known Issues, Release Notes). **Build**: 5/5 projects, 0
warnings, 0 errors, both Debug and Release, clean rebuild. **Tests**:
1406/1406 passing, four consecutive full-suite runs, zero flakes.
**Version**: confirmed the root `VERSION` file correctly still reads
`0.6.0` — not a defect, matching this project's own established
precedent that `VERSION` bumps to a new tag only *after* that tag is
cut, a Product-Owner-executed step this Work Package correctly did not
pre-empt.

Found and corrected five governance/documentation staleness findings:
`Documentation Register.md`'s own Directory Map (four stale counts,
`docs/adr/` alone reading 39 against an actual 61); `Governance
Register.md`'s own Compliance Matrix (stale since `WP 6.8`, missing all
twelve `v0.7.0` Work Packages, now backfilled). Disclosed, but did not
fix (outside this Work Package's own scope): `Platform Services
Register.md`/`Platform Service Map.md` still missing rows for the four
Engineering Foundation frameworks (found by `WP 7.3A`, reconfirmed
still open). Populated two previously-stale release-document skeletons
in full: `docs/releases/v0.7.0/ReleaseNotes.md` and `Retrospective.md`.
Corrected `docs/releases/v0.7.0/WorkPackages.md`'s own long-stale
"not started" status, marking the original candidate list superseded
by what `v0.7.0` actually delivered, not deleted.

**Recommendation: `v0.7.0` APPROVED** — see `docs/releases/v0.7.0/
WP7.4.0 Product Approval Report.md`. Five completion deliverables
produced under `docs/releases/v0.7.0/`, prefixed `WP7.4.0`. **Product
Approval was subsequently granted; `v0.7.0` was merged to `main`
(non-fast-forward, `61fb2db`), tagged, and pushed by the Product
Owner.**

### `WP 7.3A` Summary (for reference)

**`WP 7.3A` — Requirements Engine.** The first implementation Work
Package of the Systems Engineering Foundation phase — approved to begin
after Engineering Review APPROVED `WP 7.2C`'s own complete public
contracts.

Implemented `Tempest.Core.Requirements` exactly as `WP7.2C Requirements
Platform Contracts.md` approved, with zero architectural deviation.
Every requirement, collection, and group is an `IEngineeringDocument`;
every relationship (grouping, collection membership, allocation,
traceability) is a `DocumentReference` via `LinkAsync`/
`GetReferencesAsync` — zero new storage or traversal mechanism
introduced anywhere. `RequirementStatusTransitions` enforces the
approved seven-state lifecycle's own exact permitted-transition table,
with zero code path connecting it to `VerificationOutcome` — the
Status/Verification-Outcome separation is now demonstrated in running,
tested code. `GetEvidenceAsync` composes
`IVerificationService.GetVerificationHistoryAsync` with
`GetReferencesAsync` into one read, proving `WP7.2B Digital Thread
Architecture.md`'s own central claim (no new mechanism required) in
code for the first time.

Ratified all four reserved ADRs as its own first act: **`ADR-0058`**
(Platform Service classification, Engineering Data Model reuse),
**`ADR-0059`** (independent representation decisions for status,
identifier, category), **`ADR-0060`** (no compare-and-swap concurrency
mechanism, accepted as new Technical Debt `TD-25`), **`ADR-0061`** (no
internal permission gating, mirroring Materials'/Calculations' own
precedent, articulating a reusable "evidentiary vs. ordinary
operational content" deciding test). 20 new production files, 131 new
tests (1275 → 1406), a new sample module
(`RequirementsSampleModule`, the platform's twentieth). Extended
`docs/engineering/Engineering Principles.md` with four further
principles (29-32) and added a new Academy concept guide
(`16-requirements-engine.md`). Third Work Package overall to include a
dedicated Security Review — zero Release Blocking findings.

One disclosed finding, not a deviation: `WP7.2B`'s own broader
architectural vision for Allocation targets (either a document reference
or an open string) was never carried into `WP7.2C`'s own approved
`LinkAsync` contract (Guid-only) — implemented exactly as `WP7.2C`
approved, with the gap disclosed as two new Future Capability candidates
(`FCR-0037`, `FCR-0038`) rather than silently absorbed. `FCR-0027`
(Requirements Engine) is now **Implemented**.

Eight completion deliverables produced under `docs/releases/v0.7.0/`,
prefixed `WP7.3A`. See `docs/releases/v0.7.0/WP7.3A Implementation
Report.md` for the complete file-by-file account. **Does not begin
WP 7.3B. Stops here, per this Work Package's own explicit closing
instruction, awaiting Product Approval for what comes next.**

### `WP 7.2C` Summary (for reference)

**`WP 7.2C` — Requirements & Verification Platform Contract Review.**
Contract review only — no production code was written. Approved to
begin after Engineering Review APPROVED `WP 7.2B`'s own complete
architecture.

Defined the complete public contracts for the Requirements &
Verification Platform — full proposed C# interfaces for all thirteen
named domain concepts (`IRequirementsService`, `IRequirement`,
`IRequirementCollection`, `IRequirementGroup`, the relationship
mechanism, `IRequirementEvidence`, `RequirementStatus`, and the
remaining simpler concepts), each answering the same seventeen
questions this Work Package's own controlling instruction named.
Defined a full seven-state **Requirement Lifecycle Model**, confirming
`RequirementStatus` is never automatically derived from a
`VerificationRecord`'s own `Outcome` — the two remain separate,
caller-driven actions. Reviewed all seven named relationship kinds
(**Relationship Model**), confirming six belong in the initial
implementation and the seventh — "Verified By" — already exists,
unmodified, inside `Tempest.Core.Verification`. Confirmed all five
traceability dimensions reuse existing Engineering Core capability with
zero new mechanism (**Traceability Contract**), while disclosing one
genuine, structural limitation: reverse allocation traceability does not
resolve when an allocation target is an open string rather than a real
document. Re-confirmed `ADR-0057`'s own circular-dependency avoidance
holds unmodified at the contract level (**Verification Integration
Contract**).

**Security Review** found zero new issues beyond `WP 7.2B`'s own
architecture-level review, but one new open question — whether
`IRequirementsService` should gate any of its own methods internally,
mirroring `IVerificationService.GetVerificationHistoryAsync`, or remain
calling-layer-enforced throughout, mirroring `IReportingService` — now
reserved as `ADR-0061`. **Standards Architecture** confirmed the
proposed contracts can support all seven named standard families
without redesign. **Engineering Principles review** again found no
extension warranted — contracts, not implementation.

Reserved `ADR-0058`–`ADR-0060` carried forward unchanged from `WP 7.2B`;
`ADR-0061` newly reserved. Twelve completion deliverables produced under
`docs/releases/v0.7.0/`, prefixed `WP7.2C`. See `docs/releases/v0.7.0/
WP7.2C Requirements Platform Contracts.md` for the complete design.
**Engineering Review APPROVED**, authorising `WP 7.3A`.

### `WP 7.2B` Summary (for reference)

**`WP 7.2B` — Requirements & Verification Platform Architecture.**
Architecture and planning only — no production code was written.
Designed the complete architecture for the Requirements & Verification
Platform — twelve domain concepts, a three-layer **Engineering Core →
Systems Engineering Foundation → Engineering Discipline Modules** model,
a digital thread design, and an eleven-service dependency analysis
finding zero new platform capability required. Security Architecture
found one new Technical Debt item (no concurrency-conflict detection on
`ReviseAsync`); Standards Mapping reviewed seven illustrative standard
families industry-neutrally. Reserved `ADR-0058`–`ADR-0060`. Eleven
completion deliverables produced, prefixed `WP7.2B`. **Engineering
Review APPROVED**, authorising `WP 7.2C`.

### `WP 7.2A` Summary (for reference)

**`WP 7.2A` — Strategic Roadmap Selection & Programme Architecture.**
Architecture, governance, and roadmap planning only — no production code
was written. Evaluated seven candidate programmes against eleven
criteria using repository evidence exclusively and recommended Programme
A — Requirements & Verification Platform (`FCR-0027`), scoring 46 of 55,
the highest of all seven candidates. Programme F (Platform Hardening)
scored second (36/55), recommended next at `v0.9.0`, not rejected.
Programmes B, C, D, E (Mechanical, Building Services/HVAC, Structural,
Electrical) each scored 14/55 — no identified capability in any of the
four. Programme G (AI & Engineering Intelligence) scored 19/55 — its own
capability already works structurally. Ten completion deliverables
produced, prefixed `WP7.2A`. **Engineering Review and Product Approval
accepted this recommendation**, authorising `WP 7.2B`.

### `WP 7.1F` Summary (for reference)

**`WP 7.1F` — Engineering Core Integration Review & Certification.** The
Engineering Foundation phase's (`v0.7.0`) ninth and closing activity — a
certification review, not an implementation Work Package, mirroring
`WP 6.8`'s own identical role for `v0.6.0`. Approved to begin after
Engineering Review of `WP 7.0A` through `WP 7.1E` all passed. No
production code was written; the two findings requiring a fix were each
a documentation or governance-register correction.

**Certification outcome: ENGINEERING CORE CERTIFIED WITH ACCEPTED
TECHNICAL DEBT.** Architecture Review: zero circular dependencies within
the Engineering Core or between it and any Platform Service, zero
`Service → Module`/`Module → Module`/`Runtime → Feature` violations —
confirmed directly against real code, not assumed. Integration Review:
every one of the five frameworks has at least one real, tested consumer;
Engineering Data Model is consumed by all four siblings, the broadest
consumption of any of the five. Security Review: zero Release Blocking
findings across both dedicated Security Reviews (`WP 7.1D`, `WP 7.1E`)
plus a cross-framework check this Work Package performed itself, which
found the identical unvalidated-material-reference design (`AT-16`,
`AT-17`) independently reached by two frameworks — corroborating
evidence the boundary is principled, not an oversight. Testing Review:
1275 tests, 0 failures, confirmed across four full-suite runs (Debug and
Release, from a clean rebuild) plus a dedicated 224-test run scoped to
the five Engineering Core namespaces. Definition of Done Audit: all
eight Engineering Foundation Work Packages satisfy every criterion, with
exactly one disclosed shortfall, now closed (below).

**Two genuine, non-blocking findings, found and closed in this same
Work Package.** First: a **repeat of `WP 6.8`'s own exact
governance-drift pattern** — `Interface Register.md` (64 → 75),
`Dependency Injection Register.md` (26 → 30 named registrations), and
`Module Register.md` (15 → 19) had each gone stale since `WP 6.8` itself,
undetected across all five Engineering Foundation Work Packages (11
interfaces, 4 registrations, 4 sample modules unrecorded). `FCR-0005`
(Governance Register Health-Check Tooling)'s own priority is raised
Medium → High as a result — this is now a confirmed, third recurrence of
the identical failure mode. Second: `WP7.0C Academy Plan.md`'s own
required Engineering Data Model concept guide, named as this programme's
"highest-priority new Academy content," was never written by `WP 7.1A`
and never disclosed as missing by any of `WP 7.1B`–`WP 7.1E` — written
here (`02 Runtime Architecture/15-engineering-data-model.md`).

Ten completion deliverables produced (`WP7.1F Engineering Core
Certification Report.md` and nine companions) plus
`ENGINEERING_CORE_COMPLETION_REPORT.md` (repository root), the
programme's own permanent historical milestone document. See
`docs/releases/v0.7.0/WP7.1F Engineering Core Certification Report.md`
for the complete decision and evidence.

### `WP 7.1E` Summary (for reference)

**`WP 7.1E` — Verification Framework.** The Engineering Foundation
phase's eighth activity, and its fifth and final implementation Work
Package — approved to begin after Engineering Review of `WP 7.0A`,
`WP 7.0B`, `WP 7.0C`, and `WP 7.1A` through `WP 7.1D` all passed.
**Completed the entire Engineering Foundation implementation programme**
— all five frameworks (`FCR-0029`–`FCR-0033`) became Implemented.
Implemented `Tempest.Core.Verification` (`IVerificationService`,
`IVerificationRecord`, `VerificationOutcome`) exactly as `WP7.0C
Engineering Foundation Contracts.md` proposed, extended with a
structured `VerificationContext` (explicit criteria, evidence, linked
documents, linked calculation records, referenced materials) resolving
`ADR-0057`'s own two reserved questions (Audit orthogonality confirmed;
`method` remains open) plus one genuine implementation finding:
verification history is queried entirely through the Engineering Data
Model's own existing `LinkAsync`/`GetReferencesAsync` mechanism, needing
no new index and no direct `IPersistenceStore` dependency at all — the
simplest dependency shape of any Engineering Foundation framework. Zero
new exception types — `EngineeringDocumentNotFoundException` is reused
directly. 9 new production files (the smallest of the five frameworks);
a new sample module (`VerificationSampleModule`, the platform's
nineteenth). 49 new tests (1275/1275 passing, both Debug and Release,
clean rebuild). Extended `docs/engineering/Engineering Principles.md`
with five further principles (24-28) and added a new Academy concept
guide (`14-verification-framework.md`). Second consecutive Work Package
to include a dedicated Security Review — two new disclosed debt items
(`TD-23`, `TD-24`) and one new accepted trade-off (`AT-17`), neither
Release Blocking, plus `FCR-0036`.

### `WP 7.1D` Summary (for reference)

**`WP 7.1D` — Engineering Calculation Framework.** The Engineering
Foundation phase's fourth implementation Work Package. Implemented
`Tempest.Core.Calculations` (`ICalculationDefinition<TInput, TResult>`,
`ICalculationEngine`) exactly as `WP7.0C Engineering Foundation
Contracts.md` proposed, substantially extended with `CalculationMetadata`
(assumptions, constraints), `CalculationContext` (intermediate results,
constraint checks, material references), and an expanded
`CalculationRecord<TResult>` (stable identity, assumptions, validation
outcome, revision number) — resolving `ADR-0056`'s own two reserved
questions (convention-only purity enforcement, confirmed; mandatory
Engineering Data Model integration) plus the `Calculate`-signature
extension this Work Package's own "engineering evidence, not merely a
numerical answer" requirement demanded. Every execution is durably
recorded as an `IEngineeringDocument` of `Kind = "CalculationRecord"`;
no direct `IPersistenceStore` dependency needed. 17 new production
files; a new sample module (`CalculationSampleModule`, the platform's
eighteenth). 52 new tests. Extended `docs/engineering/Engineering
Principles.md` with seven further principles (17-23) and added a new
Academy concept guide (`13-calculation-framework.md`). First Engineering
Foundation Work Package to include a dedicated Security Review — two
new disclosed debt items (`TD-21`, `TD-22`) and one new accepted
trade-off (`AT-16`), neither Release Blocking, plus `FCR-0035`.

### `WP 7.1C` Summary (for reference)

**`WP 7.1C` — Materials Framework.** The Engineering Foundation phase's
third implementation Work Package. Implemented `Tempest.Core.Materials`
(`IMaterialCatalog`, `IMaterialSpecification`) exactly as `WP7.0C
Engineering Foundation Contracts.md` proposed, extended with a
structured, provenance-carrying `MaterialProperty` value type
(replacing the contract's own bare `object` property value) resolving
`ADR-0055`'s own reserved property-typing question, plus `ReviseAsync`
(new — revision support). Every engineering property carries mandatory
`MaterialPropertyProvenance` (source reference, revision, validation
status, confidence level, applicable conditions, notes) — never
omissible by construction. Consumes both the Engineering Data Model and
Units & Quantities. One genuine implementation finding (`ADR-0055`):
`MaterialCatalog` depends directly, not only indirectly, on
`IPersistenceStore`, for its own `materialId` index. 14 new production
files; a new sample module (`MaterialsSampleModule`, the platform's
seventeenth, using only clearly-fictional, explicitly-disclosed test
data). 55 new tests. Extended `docs/engineering/Engineering
Principles.md` with four further principles (13-16); no new Academy
concept guide. One new disclosed debt item (`TD-20`) and one new
accepted trade-off (`AT-15`), neither Release Blocking.

### `WP 7.1B` Summary (for reference)

**`WP 7.1B` — Units & Quantities Framework.** The Engineering Foundation
phase's second implementation Work Package. Implemented `Tempest.Core.
UnitsAndQuantities` (`Quantity<TDimension>`, `Unit<TDimension>`,
`IUnitConverter`) exactly as `WP7.0C Engineering Foundation
Contracts.md` proposed, extended (not changed) with arithmetic,
comparison, formatting, parsing, and JSON serialization support,
resolving `ADR-0054`: `double`-backed, no DI registration,
`IUnitConverter` built as a stateless, non-DI-registered wrapper. Seven
starting dimensions (Length, Mass, Duration, Force, Pressure, Area,
Volume), each purely multiplicative; Temperature (an affine dimension)
deliberately deferred (`TD-19`, `FCR-0034`). 20 new production files;
zero modified files — the first Engineering Foundation Work Package to
touch neither `TempestHost.cs` nor any other existing file. 67 new
tests. Extended `docs/engineering/Engineering Principles.md` with six
further principles (7-12) and added a new Design Patterns concept guide
(`Phantom-Type Dimension Safety`). One new disclosed debt item
(`TD-19`) and one new accepted trade-off (`AT-14`), neither Release
Blocking.

### `WP 7.1A` Summary (for reference)

**`WP 7.1A` — Engineering Data Model.** The Engineering Foundation
phase's first implementation Work Package. Implemented `Tempest.Core.
EngineeringData` (`IEngineeringDocumentStore`, `IEngineeringDocument`,
`IDocumentRevision`, `DocumentReference`) exactly as `WP7.0C Engineering
Foundation Contracts.md` proposed, resolving `ADR-0053`: built directly
on `IPersistenceStore` (`WP 6.4`), no new storage abstraction. One
disclosed, minor deviation (the exception base class's modifier,
corrected to match universal existing convention) — see `WP7.1A
Implementation Report.md`. 13 new production files; a new sample
module (`EngineeringDataSampleModule`, the platform's sixteenth); 36 new
tests. Established `docs/engineering/Engineering Principles.md` — a
new, permanent, top-level document. Two new, disclosed debt items
(`TD-17`, `TD-18`), neither Release Blocking.

### `WP 7.0A`/`WP 7.0B`/`WP 7.0C` Summary (for reference)

`WP 7.0A` — Future Capability Register & Product Vision — established
`VISION.md`, `docs/governance/Future Capability Register.md` (28
entries as of that Work Package), `Capability Categories.md`, and
`Product Roadmap.md`. `WP 7.0B` — Engineering Foundation Planning &
Capability Architecture — added `FCR-0029`–`FCR-0033` (33 entries
total), a full dependency graph and six-programme grouping, an honest
conclusion that five of nine Engineering Discipline categories cannot
yet be sequenced from existing evidence, a non-binding release-number
recommendation, and ten candidate Work Packages (`A`–`J`). `WP 7.0C` —
Engineering Foundation Contract Review — proposed public C# contracts
for all five Engineering Foundation frameworks and reserved
`ADR-0053`–`ADR-0057`. All three passed Engineering Review before `WP
7.1A` began. See each Work Package's own deliverables under
`docs/releases/v0.7.0/`.

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

**None yet approved.** `WP 8.1A` implemented the Workspace shell —
`IWorkspace`/`IWorkspaceManager` and every supporting contract, empty
Project Explorer/Properties/Content, session restore, and the new
default launch target (`ADR-0068`) — with no engineering functionality
yet. The natural next step is the **first real
`IWorkspaceViewFactory`/`IProjectExplorerNodeProvider` pair**, most
naturally for Requirements (the only Implemented Systems Engineering
Foundation capability), proving `ADR-0067`'s own extensibility
mechanism against a real engineering discipline for the first time.
Two Future Capability candidates raised by `WP 7.3A` (`FCR-0037`
string-based allocation targets, `FCR-0038` requirement baselining) and
Programme F (Platform Hardening, recommended second, at `v0.9.0` — see
`WP7.2A Recommended Programme.md`) all remain open, unscheduled
alternatives. **Per this project's own standing discipline
(`FOUNDATION.md` §1) and `WP 8.1A`'s own explicit closing instruction,
no further Work Package begins until the Product Owner gives further
instruction.**

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
| Automated tests | 1497 (0 failures) — **+91, `WP 8.1A`**: real-collaborator tests for every one of the twelve compiled Workspace contracts, both configurations, clean rebuild, stable across four runs |
| ADRs | 68 (`ADR-0001`–`ADR-0068`, no gaps at all), all Accepted — **+1, `WP 8.1A`**: `ADR-0068` (`Tempest.App`'s own default launch target) — a genuinely new decision, not a reserved number answered |
| Rejected Designs | 45 (`RD-0001`–`RD-0045`) — unchanged by `WP 8.1A` |
| Academy articles | 109 (see `docs/governance/Documentation/Academy Register.md`) — **+1, `WP 8.1A`**: `WP8.1A-workspace-shell-implementation.md` (standard 13-section implementation retrospective); `02 Runtime Architecture/17-engineering-workspace.md` updated in place a second time, not counted as new |
| Governance registers | 27 (32 governance documents total), plus 4 standing security documents under `docs/security/` and 1 standing engineering document (`docs/engineering/Engineering Principles.md`, confirmed requiring no extension by this Work Package) — unchanged in count by `WP 8.1A` |
| Architecture documents | 20 under `docs/architecture/` (22 including the two release-scoped documents) — unchanged by `WP 8.1A` |
| Platform services | 27 catalogued — unchanged by `WP 8.1A` (introduces zero new Platform Service, per `ADR-0062`, confirmed at compile time; `WorkspaceManager` consumes four existing Platform Services, none modified) |
| Modules (production) | 20 — unchanged by `WP 8.1A` (`WorkspaceManager` is a composition-root component, not a discovered module, mirroring `TempestShell`) |
| Hosted services (production) | 2 — unchanged by `WP 8.1A` |
| Plugins (production) | 0 — infrastructure fully implemented and tested; `src/Plugins/` empty by deliberate scope decision |
| Custom exception types (`src/Tempest.Core/`) | 66 — unchanged by `WP 8.1A`; **+3 new, scoped separately**: `WorkspaceException` (abstract base), `DuplicateWorkspaceRegistrationException`, `WorkspaceViewFactoryNotFoundException`, all under `src/Tempest.App/Workspace/`, outside this Core-scoped metric's own definition |
| Public interfaces (`src/Tempest.Core/`) | 80 — unchanged by `WP 8.1A`; **+12 new, scoped separately**: all twelve `Tempest.App.Workspace` interfaces now compiled and running, outside this Core-scoped metric's own definition (`Interface Register.md` remains explicitly `Tempest.Core`-only) |
| DI registrations (`TempestHost.cs` Phase 6) | 33 raw call sites, 31 named registrations — unchanged by `WP 8.1A` (`WorkspaceManager` is never DI-registered, per `ADR-0062`) |
| Technical Debt Register items | 25 tracked, 17 disclosed trade-offs — unchanged by `WP 8.1A`; zero new items raised |
| Commits (`v0.6.0` → `v0.7.0`) | 17 total, release complete: `v0.6.0` release-branch preparation (2 commits), merge from `main`, `WP 7.0A`–`WP 7.4.0` (14 commits), the `v0.7.0` merge to `main` (non-fast-forward, `61fb2db`) — tagged `v0.7.0`, pushed |
| Commits (`v0.7.0` → `v0.8.0`, so far) | 3 — `WP 8.0A`, `WP 8.0B`, `WP 8.1A` (this commit); `VERSION` bumped to `0.7.0` as part of branch preparation, not counted as a separate Work Package commit |
| Contributors | 1 (repository owner; all commits co-authored by Claude) |

*(This table is generated from `docs/governance/Quality/Repository Metrics
Register.md` and `docs/releases/v0.4.0/Release Notes.md` — update all
three together.)*

## Repository Health

- **Build:** Clean — 0 warnings, 0 errors (`dotnet build
  tests/Tempest.Core.Tests/Tempest.Core.Tests.csproj`, both Debug and
  Release configurations, from a fully-removed `bin`/`obj` tree —
  re-verified directly by `WP 7.1F`, independent of any prior Work
  Package's own claim).
- **Tests:** 1275/1275 passing, re-verified by `WP 7.1F` across **four**
  full-suite runs (two Debug, two Release, each from a clean rebuild)
  plus one further, dedicated 224-test run scoped to the five Engineering
  Core namespaces specifically. No instance of the `v0.6.0`-era,
  previously-disclosed `Console.Out`-capture flake (`WP 6.3`'s own
  finding) was observed across any run. `WP 7.1F`'s own certification
  review found no code-level regression of any kind — see `WP7.1F
  Engineering Core Certification Report.md` for the complete, per-run
  evidence table.
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

**`WP 7.1F` (Engineering Core Integration Review & Certification)** — a
certification review of the complete Engineering Core, not a feature
Work Package; no production code was written. Found the identical
governance-register-drift pattern `WP 6.8` itself found and closed for
`v0.6.0`, recurring a second time: `Interface Register.md` (64 → 75),
`Dependency Injection Register.md` (26 → 30 named registrations), and
`Module Register.md` (15 → 19) had each gone stale since `WP 6.8`,
undetected across all five Engineering Foundation Work Packages — now
fully backfilled a second time. Also found and wrote the Engineering
Data Model's own missing concept guide
(`02 Runtime Architecture/15-engineering-data-model.md`), required by
`WP7.0C Academy Plan.md` since `WP 7.1A` and never produced or
disclosed as missing. No new ADR was produced. Ten completion
deliverables were produced under `docs/releases/v0.7.0/`, prefixed
`WP7.1F`, plus `ENGINEERING_CORE_COMPLETION_REPORT.md` (repository
root, the programme's own permanent historical milestone document),
culminating in an `ENGINEERING CORE CERTIFIED WITH ACCEPTED TECHNICAL
DEBT` recommendation.

**`WP 7.3A` (Requirements Engine)** — the first implementation Work
Package of the Systems Engineering Foundation phase. `docs/architecture/
Platform Service Map.md` gained a new, fully-populated Requirements
Engine entry (replacing its own "planned, no code exists" placeholder),
following the identical documentation shape every prior new platform
service's own entry has used. Four new ADRs (`ADR-0058`–`ADR-0061`)
ratify every question `WP7.2C Required ADR Catalogue.md` reserved, with
zero further genuine implementation-phase question left unanswered.
This Work Package's own repository review found and disclosed (without
fixing, being outside its own scope) a genuine, pre-existing drift:
`docs/governance/Engineering/Platform Services Register.md` and
`Platform Service Map.md` itself had never gained rows for the four
Engineering Foundation frameworks (`WP 7.1A`–`WP 7.1E`), a gap
`WP 7.1F`'s own certification review did not check — see `Platform
Services Register.md`'s own disclosure note. `Interface Register.md`
(75 → 80), `Dependency Injection Register.md` (30 → 31 named
registrations), and `Module Register.md` (19 → 20) were each kept
current directly at implementation time, not backfilled afterward — the
first Work Package since `WP 7.1F` established the practice to actually
follow it.

**`WP 7.4.0` (Release Preparation & Product Baseline)** — a complete
release-preparation review, not a feature Work Package; no production
code written. Found and corrected `Documentation Register.md`'s own
long-disclosed stale Directory Map counts (`docs/adr/` 39 → 61,
`02 Runtime Architecture/` 11 → 16, `03 Work Packages/` 32 → 57 at time
of correction, `04 Design Patterns/` 4 → 5) — the full re-derivation
this register's own "Last Reviewed" field had recommended since `v0.6.0`
Release Engineering, closing that specific `FCR-0005` instance. Two
previously-stale release-document skeletons (`docs/releases/v0.7.0/
ReleaseNotes.md`, `Retrospective.md`) fully populated;
`WorkPackages.md`'s own long-stale "not started" status corrected,
marked superseded, not deleted.

**`WP 8.0A` (Engineering Workspace Architecture)** — an architecture-
only Work Package; no production code written. Added
`docs/releases/v0.8.0/` (five new deliverables: `Workspace Architecture
Document.md`, `UI Architecture.md`, `Navigation Specification.md`,
`Object Relationship Diagrams.md`, `User Workflow Diagrams.md`) and the
prepared-in-advance `v0.8.0` release skeletons (`WorkPackages.md`,
`ReleaseNotes.md`, `Retrospective.md`), mirroring `v0.6.0`→`v0.7.0`'s
own branch-preparation precedent exactly.

**`WP 8.0B` (Workspace Contracts)** — a contract-review-only Work
Package; no production code, no compiled interface. Added four further
`docs/releases/v0.8.0/` deliverables (`Workspace Contracts.md`,
`Sequence Diagrams.md`, `Lifecycle Definitions.md`, `Dependency
Rules.md`).

**`WP 8.1A` (Workspace Shell)** — the first implementation Work Package
of `v0.8.0`; 27 new production files under `src/Tempest.App/Workspace/`
plus `src/Tempest.App/AssemblyInfo.cs`, 1 modified (`Program.cs`). Added
one further `docs/releases/v0.8.0/` deliverable
(`WP8.1A Implementation Report.md`).

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

**`WP 7.0A`** added `WP7.0A-future-capability-register-and-product-vision.md`,
teaching that a product-vision document benefits from the same
evidentiary discipline as an ADR, and that a future-capability
register's honesty is measured by what it refuses to invent. **`WP
7.0B`** added `WP7.0B-engineering-foundation-planning-and-capability-architecture.md`,
teaching that a dependency graph is an analytical tool, not just a
diagram — it surfaced a real asymmetry between the Requirements Engine
and Verification & Validation capabilities that a flat list had left
implicit. **`WP 7.0C`** added
`WP7.0C-engineering-foundation-contract-review.md`, teaching that
contract-level design can resolve an ambiguity a capability-level
dependency graph left open. **`WP 7.1A`** added
`WP7.1A-engineering-data-model-implementation.md` — the standard
13-section template, this phase's first, since it is this phase's first
implementation Work Package — teaching that a Contract Review's
proposed signatures are a strong default, not a guarantee (one base-
exception class modifier was corrected to match universal existing
convention), and that a design decision not specified in a contract (how
revision history is looked up) can still matter architecturally.
`docs/engineering/Engineering Principles.md`, a new top-level document,
was also established by `WP 7.1A`. **`WP 7.1B`** added
`WP7.1B-units-and-quantities-framework-implementation.md` — the second
standard 13-section implementation retrospective of this phase —
teaching that `System.Text.Json` requires an explicit `[JsonConstructor]`
attribute for a value type with a hand-written (non-positional-record)
constructor, that a framework with zero Platform Service dependency is a
genuinely different implementation experience (no `TempestHost.cs`
change at all), and that a real architectural gap (Temperature's affine
conversion) can surface during a framework's own implementation even
after contract review found no issue at the interface-signature level.
Also added a new Design Patterns concept guide,
`04 Design Patterns/05-phantom-type-dimension-safety.md` — this
platform's first phantom-type pattern — and extended
`docs/engineering/Engineering Principles.md` with six further
principles (7-12). **`WP 7.1C`** added
`WP7.1C-materials-framework-implementation.md` — the third standard
13-section implementation retrospective of this phase — teaching that a
provenance requirement can resolve a reserved property-typing question
more decisively than the question alone, that a "thin index over an
existing store" can still need its own storage dependency once a
lookup-by-string requirement is actually implemented, and that bounding
a heterogeneous property value to an already-established, small set
(the seven Units & Quantities dimensions) avoids both an unsafe
general-purpose polymorphic mechanism and a premature, invented
property-name taxonomy. No new concept guide — Materials is presented
as a worked example of the Data Model, per `WP7.0C Academy Plan.md`'s
own finding, not a new pattern in its own right. Further extended
`docs/engineering/Engineering Principles.md` with four further
principles (13-16). **`WP 7.1D`** added
`WP7.1D-engineering-calculation-framework-implementation.md` — the
fourth standard 13-section implementation retrospective of this
phase, and the first to include a dedicated Security Review — teaching
that an evidentiary requirement can justify a signature change a
contract review could not have anticipated, that reusing a sibling
framework's own dispatch pattern (the Command Framework's type-erased
registry) works cleanly provided the one deciding property (purity) is
kept explicit and tested, and that a calculation's own true evidentiary
value comes from what it discloses (assumptions, intermediate results,
validation outcome) rather than its final number alone. Also added a
new "02 Runtime Architecture" concept guide,
`13-calculation-framework.md`, distinguishing the Calculation Framework
from the Command Framework — the required output `WP7.0C Academy
Plan.md` itself named. Further extended `docs/engineering/Engineering
Principles.md` with seven further principles (17-23). **`WP 7.1E`**
added `WP7.1E-verification-framework-implementation.md` — the fifth and
final standard 13-section implementation retrospective of this phase,
and the second to include a dedicated Security Review — teaching that a
cross-cutting framework's own best design decision can be reusing an
existing mechanism completely rather than extending it (verification
history needed no new index at all, only the Data Model's own existing
`LinkAsync`/`GetReferencesAsync`), that validating some links while
leaving others open is legitimate when the asymmetry tracks a real
dependency difference, and that narrow, explicitly-named scope
exclusions (Validation, Requirements Management) produced the same
close-to-automatic scope discipline every prior Engineering Foundation
Work Package also reported — now a five-for-five pattern across the
entire programme. Also added a new "02 Runtime Architecture" concept
guide, `14-verification-framework.md`, distinguishing Verification from
both Audit and Calculation Record. Further extended
`docs/engineering/Engineering Principles.md` with five further
principles (24-28), completing that document's own Engineering
Foundation contribution — all five frameworks have now extended it.
**`WP 7.1F`** added `WP7.1F-engineering-core-integration-review-and-
certification.md` — a closing certification retrospective, mirroring
`WP 6.8`'s own whole-release review format, not the standard 13-section
per-feature template. Also wrote `02 Runtime Architecture/
15-engineering-data-model.md`, the Engineering Data Model's own concept
guide — required output of `WP 7.1A`, named by `WP7.0C Academy Plan.md`
as this programme's "highest-priority new Academy content," never
written, and never disclosed as missing by `WP 7.1A` or any of
`WP 7.1B`–`WP 7.1E`. No further Engineering Principles added — this
Work Package audits, it does not implement.
**`WP 7.2A`** added `WP7.2A-strategic-roadmap-selection-and-programme-
architecture.md` — a whole-review retrospective mirroring `WP 7.0A`/
`WP 7.0B`'s own format, not the standard 13-section per-feature
template. No new concept guide (a planning/governance milestone, not a
feature); no Engineering Principles added.
**`WP 7.2B`** added `WP7.2B-requirements-and-verification-platform-
architecture.md` — a whole-review retrospective mirroring `WP7.0C
Engineering Foundation Contracts.md`'s own format, not the standard
13-section per-feature template. No new concept guide — `WP7.2B Academy
Plan.md` recommends one for the owning implementation Work Package, once
real code exists to derive worked examples from. Reviewed whether
`docs/engineering/Engineering Principles.md` requires extension:
**found no extension warranted** — this Work Package produced
architecture only, and every existing principle was derived from real,
shipped code, never asserted in advance of it.
**`WP 7.2C`** added `WP7.2C-requirements-and-verification-platform-
contract-review.md` — a whole-review retrospective mirroring the same
format, extended to a seventeen-question-per-concept contract review.
No new concept guide — `WP7.2C Academy Plan.md` confirms and extends
`WP7.2B Academy Plan.md`'s own recommendation, now naming two required
concept-guide sections (the primary Requirements pattern, and the
relationship/traceability vocabulary newly detailed at contract level)
for the owning implementation Work Package. Engineering Principles
review re-confirmed: **no extension warranted**, unchanged from
`WP 7.2B`.
**`WP 7.3A`** added `WP7.3A-requirements-engine-implementation.md` (the
standard 13-section implementation retrospective) and
`02 Runtime Architecture/16-requirements-engine.md` (the two
concept-guide sections `WP7.2C Academy Plan.md` named: the three-layer
Requirement-as-Document pattern, and the relationship-kind/traceability
vocabulary). `docs/engineering/Engineering Principles.md` extended with
four further principles (29-32) — the first extension since `WP 7.1E`
completed the Engineering Foundation programme's own five-Work-Package
contribution.
**`WP 7.4.0`** added `WP7.4.0-release-preparation-and-product-baseline.md`
— a whole-review retrospective mirroring `WP 5.4`/`WP 6.8`/`WP 7.1F`'s
own format, reviewing all twelve `v0.7.0` Work Packages together. No new
concept guide — release preparation only. `docs/engineering/Engineering
Principles.md` confirmed requiring no extension, unchanged from
`WP 7.3A`.
**`WP 8.0A`** added `WP8.0A-engineering-workspace-architecture.md` (a
whole-review retrospective mirroring `WP7.2B`'s own architecture-only
format) and `02 Runtime Architecture/17-engineering-workspace.md` (a
new concept guide, written at the architecture stage, to be updated at
implementation — mirroring `10-shell-and-application-composition.md`'s
own precedent). `docs/engineering/Engineering Principles.md` reviewed:
**no extension warranted** — this Work Package produced architecture
only, no implementation to derive a genuine engineering principle from.
**`WP 8.0B`** added `WP8.0B-workspace-contracts.md` (a whole-review
retrospective mirroring `WP7.2C`'s own contract-review-only format) and
updated `02 Runtime Architecture/17-engineering-workspace.md` in place
(second update — not a new file) to reflect the twelve now-frozen
contracts and both resolved ADRs. `docs/engineering/Engineering
Principles.md` reviewed: **no extension warranted**, unchanged from
`WP 8.0A` — contract review produced no implementation to derive a
genuine engineering principle from.
**`WP 8.1A`** added `WP8.1A-workspace-shell-implementation.md` (a
standard 13-section implementation retrospective) and updated
`02 Runtime Architecture/17-engineering-workspace.md` in place a third
time to reflect the now-compiled shell and `ADR-0068`.
`docs/engineering/Engineering Principles.md` reviewed: **no extension
warranted** — every design decision this Work Package made was already
anticipated by `WP 8.0A`/`WP 8.0B`'s own approved architecture and
contracts; no genuinely new engineering principle emerged from
implementing them.

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

**`WP 7.0A`** established `docs/governance/Future Capability
Register.md` (28 entries), `Capability Categories.md`, and `Product
Roadmap.md` — no new Technical Debt or Risk item, since it identified
future capability, not present-day gaps. **`WP 7.0B`** added
`FCR-0029`–`FCR-0033` to the Future Capability Register (33 entries
total). **`WP 7.0C`** produced no new governance register entries — a
contract review audits proposed decisions, it does not itself decide
them. **`WP 7.1A`** added `ADR-0053` (Accepted, the register's 53rd),
`TD-17` and `TD-18` to `Technical Debt Register.md` (18 tracked items
total), and marked `FCR-0029` **Implemented** — the first entry in the
Future Capability Register to leave "Identified" status. `WP 7.1A` also
found and corrected a small, previously-undisclosed staleness in `ADR
Register.md`'s own "Last Reviewed" field (unchanged since `WP 6.6`
despite `WP 7.0C`'s own edit to that register's Numbering Integrity
narrative in the interim) — a minor, disclosed instance of the same
register-staleness pattern this project has found and corrected
several times before. **`WP 7.1B`** added `ADR-0054` (Accepted, the
register's 54th), `TD-19` and `AT-14` to `Technical Debt Register.md`
(19 tracked items, 14 disclosed trade-offs total), and marked `FCR-0030`
**Implemented** — the second entry in the Future Capability Register to
leave "Identified" status. `WP 7.1B` also added `FCR-0034` (Affine Unit
Conversion / Temperature) — the first Future Capability Register entry
sourced from a real implementation Work Package's own disclosed finding
rather than planning-stage inference, bringing the register to 34
entries total. **`WP 7.1C`** added `ADR-0055` (Accepted, the register's
55th), `TD-20` and `AT-15` to `Technical Debt Register.md` (20 tracked
items, 15 disclosed trade-offs total), and marked `FCR-0031`
**Implemented** — the third entry in the Future Capability Register to
leave "Identified" status. No new `FCR` entry was added this time —
`WP 7.1C`'s own genuine implementation finding (`MaterialCatalog`'s
direct `IPersistenceStore` dependency) was recorded directly in
`ADR-0055` and `TD-20`, not as a new roadmap-facing capability.
**`WP 7.1D`** added `ADR-0056` (Accepted, the register's 56th), `TD-21`,
`TD-22`, and `AT-16` to `Technical Debt Register.md` (22 tracked items,
16 disclosed trade-offs total), and marked `FCR-0032` **Implemented** —
the fourth entry in the Future Capability Register to leave
"Identified" status. `WP 7.1D` also added `FCR-0035` (Calculation
Execution Timeout & Cancellation Support) — sourced directly from this
Work Package's own required Security Review, the first Future
Capability Register entry sourced from a security review rather than an
implementation report's own disclosed finding, bringing the register to
35 entries total. **`WP 7.1E`** added `ADR-0057` (Accepted, the
register's 57th, closing the entire `ADR-0053`–`ADR-0057` reserved
range), `TD-23`, `TD-24`, and `AT-17` to `Technical Debt Register.md`
(24 tracked items, 17 disclosed trade-offs total), and marked `FCR-0033`
**Implemented** — the fifth entry in the Future Capability Register to
leave "Identified" status, **completing the entire Engineering
Foundation programme** (`FCR-0029`–`FCR-0033` all now Implemented).
`WP 7.1E` also added `FCR-0036` (Transactional Multi-Document Operations
for the Engineering Data Model) — sourced directly from this Work
Package's own required Security Review, the second such entry after
`FCR-0035`, bringing the register to 36 entries total.
**`WP 7.1F`** added no new ADR (an audit, not a decision) and no new
Technical Debt or trade-off item (`TD-18` was reassessed with a more
precise disposition, not newly disclosed). Fully backfilled `Interface
Register.md`, `Dependency Injection Register.md`, and `Module
Register.md` — each stale since `WP 6.8`, the exact drift pattern
`FCR-0005` exists to prevent, recurring a second time. `FCR-0005`'s own
priority raised Medium → High as a direct result — three recurrences of
this failure mode across three separate release phases is now a
confirmed pattern, not a single observation. The register remains at 36
entries total — no new capability was identified.
**`WP 7.2A`** added no new ADR, no new Technical Debt or trade-off item,
and no new Future Capability Register entry. Reviewed all 36 existing
entries against seven candidate next-programme options; annotated
`FCR-0027` (Requirements Engine) with this Work Package's own
recommendation (Status: Identified → recommended; Priority: Unknown →
High) — a recommendation, not an approval, per `docs/governance/Future
Capability Register.md`'s own updated entry. `docs/governance/Product
Roadmap.md` updated in place: Phase 4 (Engineering Foundation) marked
Complete, with the divergence between its own original working premise
and what was actually built disclosed explicitly rather than silently
reconciled; Phase 5 (Engineering Modules) sequencing recommended
(Systems Engineering first).
**`WP 7.2B`** added no new ADR (three reserved, not written —
`ADR-0058`–`ADR-0060`), no new Technical Debt Register entry (one new
item identified at architecture level, recommended for formal
registration once implementation begins, not registered by this
Work Package itself), and no new Future Capability Register entry.
`FCR-0027`'s own entry updated in place: Status annotated "architecture
complete, not yet Implemented," `ADR-0013` classification decided at
architecture level (Platform Service). Zero governance registers
required backfilling — this Work Package's own Dependency Analysis
confirmed every integration point it needs already exists, proven,
unmodified.
**`WP 7.2C`** added no new ADR (a fourth reserved, not written —
`ADR-0061`, alongside `ADR-0058`–`ADR-0060` carried forward unanswered),
no new Technical Debt Register entry (the one item identified at
`WP 7.2B`'s own architecture level re-confirmed unchanged at contract
level, still not formally registered), and no new Future Capability
Register entry. `FCR-0027`'s own entry updated in place: Status
annotated "complete public contracts defined, not yet Implemented."
Zero governance registers required backfilling — this Work Package's
own Security Review confirmed no new issue beyond `WP 7.2B`'s own
architecture-level review.
**`WP 7.3A`** added four new ADRs (`ADR-0058`–`ADR-0061`, all Accepted,
57 → 61, closing `WP7.2C Required ADR Catalogue.md`'s own entire
reserved range), one new Technical Debt Register entry (`TD-25`, no
concurrency-conflict detection on `ReviseAsync`/`SetStatusAsync`,
formally registered directly from `ADR-0060`'s own accepted
disposition, 24 → 25), and two new Future Capability Register entries
(`FCR-0037` string-based allocation targets, `FCR-0038` requirement
baselining, 36 → 38). `FCR-0027`'s own entry updated in place: Status
changed to **Implemented**. `Interface Register.md`, `Dependency
Injection Register.md`, and `Module Register.md` were each kept current
directly at implementation time (75 → 80 interfaces, 30 → 31 named
registrations, 19 → 20 modules) — no backfill needed, since this Work
Package recorded its own additions as it made them. `Platform Services
Register.md`'s own Requirements Engine row was updated Planned →
Implemented; a genuine, pre-existing, unrelated drift was found and
disclosed there (not fixed, being outside this Work Package's own
scope): the register and `Platform Service Map.md` had never gained
rows for the four Engineering Foundation frameworks (`WP 7.1A`–`WP
7.1E`), a gap `WP 7.1F`'s own certification review did not check.
**`WP 7.4.0`** added no new ADR, no new Technical Debt or Future
Capability Register entry (release preparation only; zero new platform
functionality). Found and fully backfilled `Governance Register.md`'s
own Compliance Matrix (stale since `WP 6.8`, missing all twelve
`v0.7.0` Work Packages plus `v0.6.0` Release Engineering) and
`Documentation Register.md`'s own Directory Map (four stale counts,
carried forward unchanged since `WP 5.3`/`v0.6.0` Release Engineering).
`FCR-0005` (Governance Register Health-Check Tooling) reconfirmed still
open, its own priority annotation updated to record a fourth and fifth
recurrence found this release. `Platform Services Register.md`'s own
Coverage Status corrected from "Complete" to "Partial," disclosing
rather than hiding the still-open four-framework gap `WP 7.3A` first
found.
**`WP 8.0A`** added four new ADRs (`ADR-0062`–`ADR-0065`, all Accepted,
61 → 65) — the first ADRs of the `v0.8.0` release, each a genuine,
locked-in architectural boundary decision rather than an implementation
detail. `ADR-0066`/`ADR-0067` newly reserved for a future Contract
Review Work Package. No new Technical Debt or Future Capability
Register entry (architecture only; no implementation to disclose a
defect or capability gap from). `Academy Register.md`, `Documentation
Register.md` (directory-map counts for `docs/adr/`,
`02 Runtime Architecture/`, `03 Work Packages/`) kept current directly
at documentation time, not backfilled.
**`WP 8.0B`** added two new ADRs (`ADR-0066`/`ADR-0067`, both Accepted,
65 → 67), resolving both ADRs `WP 8.0A` reserved — zero
reserved-but-unwritten ADR number remains outstanding anywhere in the
register. No new Technical Debt or Future Capability Register entry
(contract review only; no implementation to disclose a defect or
capability gap from). `Academy Register.md`, `Documentation Register.md`
kept current directly at documentation time, not backfilled.
**`WP 8.1A`** added one new ADR (`ADR-0068`, Accepted, 67 → 68) — a
genuinely new decision (`Tempest.App`'s own default launch target), not
a reserved number answered. Zero new Technical Debt item — every scope
limitation is either already-disclosed from `WP 8.0A` or a direct
consequence of "no engineering functionality." No new Future Capability
Register entry. `Academy Register.md`, `Documentation Register.md` kept
current directly at implementation time, not backfilled. Confirmed,
disclosed: `Interface Register.md`/`Dependency Injection Register.md`/
`Module Register.md` remain correctly unchanged — all twelve new public
interfaces and `WorkspaceManager` fall outside each register's own
explicit `Tempest.Core`/`TempestHost.cs`/discovered-module scope,
mirroring `TempestShell`'s own identical, long-standing exclusion.

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
2. **`WP 7.0A`, `WP 7.0B`, and `WP 7.0C` are all complete — Engineering
   Review APPROVED all three.** `VISION.md`, `Future Capability
   Register.md` (33 entries), `Capability Categories.md`, `Product
   Roadmap.md`, a full dependency graph, six engineering programmes,
   proposed public C# contracts for all five Engineering Foundation
   frameworks, and `ADR-0053`–`ADR-0057` catalogued are all established
   and approved.
3. **`WP 7.1A` through `WP 7.1E` — all five Engineering Foundation
   frameworks — are complete and Engineering-Review-approved.**
   `Tempest.Core.EngineeringData` (`ADR-0053`),
   `Tempest.Core.UnitsAndQuantities` (`ADR-0054`),
   `Tempest.Core.Materials` (`ADR-0055`), `Tempest.Core.Calculations`
   (`ADR-0056`), and `Tempest.Core.Verification` (`ADR-0057`) are all
   implemented; 1275/1275 tests passing, both configurations, clean
   rebuild.
4. **`WP 7.1F` — Engineering Core Integration Review & Certification —
   is complete, Engineering Review APPROVED.** The Engineering Core is
   **CERTIFIED WITH ACCEPTED TECHNICAL DEBT** — see
   `ENGINEERING_CORE_COMPLETION_REPORT.md` and `docs/releases/v0.7.0/
   WP7.1F Engineering Core Certification Report.md`. This closed the
   entire Engineering Foundation implementation programme.
5. **`WP 7.2A` — Strategic Roadmap Selection & Programme Architecture —
   is complete, Engineering Review APPROVED.** Recommended Programme A
   (Requirements & Verification Platform, `FCR-0027`), scoring 46/55 —
   see `docs/releases/v0.7.0/WP7.2A Recommended Programme.md`.
   **Engineering Review and Product Approval accepted this
   recommendation**, authorising `WP 7.2B`.
6. **`WP 7.2B` — Requirements & Verification Platform Architecture —
   is complete, Engineering Review APPROVED.** Designed the complete
   architecture for the Requirements & Verification Platform — twelve
   domain concepts, a three-layer Engineering Core/Systems Engineering
   Foundation/Engineering Discipline Modules model, a digital thread
   design, an eleven-service dependency analysis (zero new platform
   capability required), a security classification (one new Technical
   Debt item found), and an industry-neutral standards mapping. Reserved
   `ADR-0058`–`ADR-0060` — see `docs/releases/v0.7.0/WP7.2B Requirements
   Platform Architecture.md`. **Engineering Review accepted this
   architecture**, authorising `WP 7.2C`.
7. **`WP 7.2C` — Requirements & Verification Platform Contract Review —
   is complete, Engineering Review APPROVED.** Defined full proposed C#
   contracts for all thirteen named domain concepts, a seven-state
   Requirement Lifecycle Model, a Relationship Model (six of seven
   relationship kinds confirmed for the initial implementation), a
   Traceability Contract (disclosing one genuine reverse-allocation-
   traceability limitation), and a Verification Integration Contract
   re-confirming `ADR-0057`'s own circular-dependency avoidance holds
   unmodified. Reserved `ADR-0061`, alongside `ADR-0058`–`ADR-0060`
   carried forward unanswered — see `docs/releases/v0.7.0/WP7.2C
   Requirements Platform Contracts.md`. **Engineering Review accepted
   these contracts**, authorising `WP 7.3A`.
8. **`WP 7.3A` — Requirements Engine — is complete, Engineering Review
   APPROVED.** Implemented `Tempest.Core.Requirements` exactly as
   `WP 7.2C` approved, zero architectural deviation. Ratified all four
   reserved ADRs (`ADR-0058`–`ADR-0061`); disclosed new Technical Debt
   (`TD-25`) and two new Future Capability candidates (`FCR-0037`,
   `FCR-0038`). `FCR-0027` (Requirements Engine) is now **Implemented**
   — see `docs/releases/v0.7.0/WP7.3A Implementation Report.md`. This
   Work Package's own explicit closing instruction ("Do not begin
   `WP 7.3B`. Stop after WP 7.3A has been fully implemented and
   reviewed.") is honoured — no further Work Package begins until
   Product Approval authorises what comes next.
9. **`WP 7.4.0` — Release Preparation & Product Baseline — is complete.**
   A release-preparation review only, no new platform functionality:
   5/5 projects build clean (0 warnings, 0 errors) in both
   configurations; 1406/1406 tests passing across four full-suite runs;
   `VERSION` confirmed correctly unchanged (`0.6.0`, bumped only after
   the Product Owner's own tag, per established precedent). Five
   governance/documentation staleness findings corrected
   (`Documentation Register.md`, `Governance Register.md`'s Compliance
   Matrix); one further finding disclosed, not fixed (`Platform Services
   Register.md`/`Platform Service Map.md`'s own missing four-framework
   rows). **Recommendation: `v0.7.0` APPROVED** — see
   `docs/releases/v0.7.0/WP7.4.0 Product Approval Report.md`.
10. **`v0.7.0` is released.** The Product Owner accepted `WP 7.4.0`'s own
    recommendation, merged `feature/v0.7.0-engineering-foundation` into
    `main` (non-fast-forward, `61fb2db`), tagged `v0.7.0`, and pushed
    both to `origin`. `feature/v0.8.0-engineering-workspace` was then cut
    from `main` at the `v0.7.0` tag, `VERSION` bumped to `0.7.0`,
    mirroring `v0.6.0`'s own identical precedent.
11. **`WP 8.0A` — Engineering Workspace Architecture — is complete.**
    The complete architecture for TempestOS's first user-facing
    engineering product surface, across all twelve named areas — see
    `docs/releases/v0.8.0/WP8.0A Workspace Architecture Document.md`.
    Four new ADRs (`ADR-0062`–`ADR-0065`); `ADR-0066`/`ADR-0067`
    reserved for a Contract Review Work Package. **No implementation
    was performed — zero code written.**
12. **`WP 8.0B` — Workspace Contracts — is complete.** The complete
    public contract for all twelve named Workspace interfaces — see
    `docs/releases/v0.8.0/WP8.0B Workspace Contracts.md`. Both reserved
    ADRs resolved (`ADR-0066` terminal-based presentation, `ADR-0067`
    Kind-keyed extensibility registration). **No implementation was
    performed — zero code compiled.**
13. **`WP 8.1A` — Workspace Shell — is complete.** All twelve contracts
    compiled and running in `Tempest.App.Workspace`, zero signature
    change — see Current Work Package, above, and
    `docs/releases/v0.8.0/WP8.1A Implementation Report.md`. The
    Workspace is now `Tempest.App`'s own default launch target
    (`ADR-0068`); console `TempestShell` remains, untouched, no longer
    default. 91 new tests (1406 → 1497). **No engineering functionality
    — shell only, per this Work Package's own explicit constraint.**
14. **Await Product Owner instruction for what comes next.** The first
    real `IWorkspaceViewFactory`/`IProjectExplorerNodeProvider` pair
    (most naturally for Requirements) is the natural next step, but per
    this project's own standing discipline (`FOUNDATION.md` §1) and
    `WP 8.1A`'s own explicit closing instruction, no further Work
    Package begins until the Product Owner gives further instruction.
15. A GitHub Release for `v0.6.0` (and now `v0.7.0`) has not yet been
    created (`gh` CLI unavailable in this environment) — see the Release
    Summary for the exact command or manual steps to complete it.

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
**Engineering Foundation** phase (`v0.7.0`). `WP 7.0A` through `WP 7.3A`
are all complete — the entire Engineering Foundation implementation
programme is certified, Programme A (Requirements & Verification
Platform) has been recommended and accepted, its complete architecture
and public contracts were defined, and the Requirements Engine itself
(`FCR-0027`) is now **Implemented** — see `docs/releases/v0.7.0/WP7.3A
Implementation Report.md` and `docs/governance/Future Capability
Register.md` for the current state pending Product Approval on what
comes next.

- `WP 7.0A` — Future Capability Register & Product Vision (architecture
  and governance only; no production code). **Complete — Engineering
  Review APPROVED.**
- `WP 7.0B` — Engineering Foundation Planning & Capability Architecture
  (architecture and planning only; no production code). **Complete —
  Engineering Review APPROVED.**
- `WP 7.0C` — Engineering Foundation Contract Review (contract review
  only; no production code, no compiled interface). **Complete —
  Engineering Review APPROVED.**
- `WP 7.1A` — Engineering Data Model (first implementation Work Package
  of this phase; `Tempest.Core.EngineeringData`, `ADR-0053`). **Complete
  — Engineering Review APPROVED.**
- `WP 7.1B` — Units & Quantities Framework (second implementation Work
  Package of this phase; `Tempest.Core.UnitsAndQuantities`, `ADR-0054`).
  **Complete — Engineering Review APPROVED.**
- `WP 7.1C` — Materials Framework (third implementation Work Package of
  this phase; `Tempest.Core.Materials`, `ADR-0055`). **Complete —
  Engineering Review APPROVED.**
- `WP 7.1D` — Engineering Calculation Framework (fourth implementation
  Work Package of this phase, and the first to include a dedicated
  Security Review; `Tempest.Core.Calculations`, `ADR-0056`). **Complete
  — Engineering Review APPROVED.**
- `WP 7.1E` — Verification Framework (fifth and final Engineering
  Foundation implementation Work Package, and the second to include a
  dedicated Security Review; `Tempest.Core.Verification`, `ADR-0057`).
  **Complete — Engineering Review APPROVED.** Closed the Engineering
  Foundation implementation programme — see `docs/governance/Future
  Capability Register.md` (`FCR-0029` through `FCR-0033`, all now
  Implemented).
- `WP 7.1F` — Engineering Core Integration Review & Certification
  (closing certification review of the complete Engineering Core,
  mirroring `WP 6.8`'s own role for `v0.6.0`; no production code).
  **Complete — Engineering Review APPROVED.** **ENGINEERING CORE
  CERTIFIED WITH ACCEPTED TECHNICAL DEBT** — see
  `ENGINEERING_CORE_COMPLETION_REPORT.md` and `docs/releases/v0.7.0/
  WP7.1F Engineering Core Certification Report.md`. Found and closed a
  repeat of `WP 6.8`'s own governance-register-drift finding and a
  missing Academy concept guide four Work Packages overdue.
- `WP 7.2A` — Strategic Roadmap Selection & Programme Architecture
  (architecture, governance, and roadmap planning only; no production
  code). **Complete — Engineering Review APPROVED.** Recommended
  Programme A (Requirements & Verification Platform, `FCR-0027`) as
  `v0.8.0`'s own scope, scoring 46/55 against seven candidates — see
  `docs/releases/v0.7.0/WP7.2A Recommended Programme.md`. Programme F
  (Platform Hardening) recommended second, at `v0.9.0`. **Engineering
  Review and Product Approval accepted this recommendation.**
- `WP 7.2B` — Requirements & Verification Platform Architecture
  (architecture and planning only; no production code). **Complete —
  Engineering Review APPROVED.** Designed the complete architecture for
  the Requirements & Verification Platform — twelve domain concepts, a
  three-layer Engineering Core/Systems Engineering Foundation/Engineering
  Discipline Modules model, a digital thread design, and an
  eleven-service dependency analysis finding zero new platform capability
  required — see `docs/releases/v0.7.0/WP7.2B Requirements Platform
  Architecture.md`. Reserved `ADR-0058`–`ADR-0060`.
- `WP 7.2C` — Requirements & Verification Platform Contract Review
  (contract review only; no production code, no compiled interface).
  **Complete — Engineering Review APPROVED.** Defined full proposed C#
  contracts for all thirteen named domain concepts, a Requirement
  Lifecycle Model, a Relationship Model, a Traceability Contract, and a
  Verification Integration Contract re-confirming `ADR-0057`'s own
  circular-dependency avoidance holds unmodified — see
  `docs/releases/v0.7.0/WP7.2C Requirements Platform Contracts.md`.
  Reserved `ADR-0061`.
- `WP 7.3A` — Requirements Engine (first implementation Work Package of
  the Systems Engineering Foundation phase; `Tempest.Core.Requirements`,
  `ADR-0058`–`ADR-0061`). **Complete — Engineering Review APPROVED.**
  Implemented exactly as `WP 7.2C` approved, zero architectural
  deviation; 20 new production files, 131 new tests (1275 → 1406),
  new Academy concept guide (`16-requirements-engine.md`). Ratified all
  four reserved ADRs; disclosed `TD-25` and two new Future Capability
  candidates (`FCR-0037`, `FCR-0038`) — see `docs/releases/v0.7.0/WP7.3A
  Implementation Report.md`. `FCR-0027` is now **Implemented**. Does not
  begin `WP 7.3B`, per this Work Package's own explicit closing
  instruction.
- `WP 7.4.0` — Release Preparation & Product Baseline (release
  preparation only; no production code, no architectural change).
  **Complete.** Full release readiness review across seventeen named
  areas: clean build (5/5 projects, 0 warnings/errors, both
  configurations), 1406/1406 tests (four full-suite runs), `VERSION`
  confirmed correct, twelve Work Packages' governance traceability
  backfilled. Five documentation/governance staleness findings
  corrected; one further finding disclosed, not fixed (outside scope) —
  see `docs/releases/v0.7.0/WP7.4.0 Release Readiness Report.md`.
  **Recommendation: `v0.7.0` APPROVED** — see `docs/releases/v0.7.0/
  WP7.4.0 Product Approval Report.md`.

**`v0.7.0` is released.** All thirteen Work Packages plus its own
closing release-preparation review are complete. The Product Owner
merged `feature/v0.7.0-engineering-foundation` into `main`
(non-fast-forward, `61fb2db`), tagged `v0.7.0`, and pushed both to
`origin`.

Per `docs/releases/v0.8.0/WorkPackages.md`, the Engineering Workspace
phase is under way, on `feature/v0.8.0-engineering-workspace` (cut from
`main` at the `v0.7.0` tag):

- `WP 8.0A` — Engineering Workspace Architecture (architecture and
  design only; no implementation, no production code). **Complete.**
  Designed the complete Engineering Workspace across all twelve named
  areas — TempestOS's first user-facing engineering product surface,
  a multi-panel evolution of `Tempest.App`'s own composition
  root, additive to console `TempestShell` (`ADR-0062`). Views read
  Engineering Core/Platform services directly; mutations dispatch
  through the existing Command Framework (`ADR-0063`); layout/session
  state persists via the existing `ISettingsProvider` (`ADR-0064`);
  Digital Thread visualisation composes existing reads, introducing no
  new traversal mechanism (`ADR-0065`) — see `docs/releases/v0.8.0/
  WP8.0A Workspace Architecture Document.md` and its four companion
  deliverables. `ADR-0066` (UI rendering technology) and `ADR-0067`
  (object-view extensibility contract) reserved for a Contract Review
  Work Package.
- `WP 8.0B` — Workspace Contracts (contract review only; no
  implementation, no compiled interface). **Complete.** Defined the
  complete public contract for all twelve named interfaces
  (`IWorkspace`, `IWorkspaceManager`, `IWorkspaceView`,
  `IWorkspacePanel`, `IWorkspaceLayout`, `INavigationService`,
  `ISelectionService`, `IWorkspaceContext`, `IWorkspaceState`,
  `IProjectExplorer`, `IPropertyInspector`, `IWorkspaceCommand`) — see
  `docs/releases/v0.8.0/WP8.0B Workspace Contracts.md` and its three
  companion deliverables. Resolved both reserved ADRs: `ADR-0066`
  (terminal-based presentation, not a graphical desktop framework —
  this platform's first-ever GUI dependency deliberately not taken on)
  and `ADR-0067` (Kind-keyed registration for both object views and
  Project Explorer nodes, mirroring `IReportDefinition`/
  `IReportRenderer<T>`'s own established pattern).
- `WP 8.1A` — Workspace Shell (implementation; shell only, no
  engineering functionality). **Complete.** All twelve contracts
  compiled and running in a new `Tempest.App.Workspace` namespace, zero
  signature change — see `docs/releases/v0.8.0/WP8.1A Implementation
  Report.md`. `WorkspaceManager`/`WorkspaceShell` are now `Tempest.App`'s
  own default launch target (`ADR-0068`); console `TempestShell`
  remains, untouched, no longer default. 27 new production files, 91
  new tests (1406 → 1497). Two disclosed implementation-phase findings
  (`ISettingsProvider`'s own `string`-only contract;
  `ITempestHost`'s own single-use constraint), neither requiring an
  architectural revisit. Zero new Technical Debt.

**`v0.8.0` is in progress.** Its architecture, contract, and first
implementation phases are all complete — a real, running, empty
Workspace shell now exists; no engineering-functionality Work Package
has been scoped or approved yet.

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
