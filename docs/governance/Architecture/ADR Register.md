# ADR Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | ADR Register |
| **Purpose** | The complete, authoritative index of every Architecture Decision Record TempestOS has produced — what each one decided, which Work Package produced it, and whether it remains in force. |
| **Scope** | Every file in `docs/adr/`, from `ADR-0001` through the highest-numbered ADR present at time of review. |
| **Owner** | Project Maintainer — sole contributor of record across all 77 repository commits (git author `kreczmans-creator`; no separate architecture-review board or team structure exists as of this baseline). |
| **Source of Truth** | `docs/adr/` (the ADR files themselves). This register is a governance index over that source, not a replacement for it — the full Context/Decision/Consequences reasoning lives only in each ADR file. |
| **Review Frequency** | Updated whenever a new ADR is created, superseded, or reversed (Engineering Governance §5) — in practice, once per Work Package that meets the §5 ADR criteria. |
| **Last Reviewed** | 2026-08-04 (WP 8.2A, Engineering Domain Architecture) — ADR-0072 through ADR-0074 added (all Accepted) — three genuinely new platform-wide decisions, each formalising a pattern the Engineering Core's own four already-shipped frameworks had independently converged on; 71 → 74 ADR total. Previously reviewed 2026-08-04 (WP 8.1B, Navigation & Project Explorer) — ADR-0071 added (Accepted) — corrects ADR-0067's own worked registration example against the real Host/Workspace boundary ADR-0062 already established; 70 → 71 ADR total. Previously reviewed 2026-08-04 (WP 8.0C, Engineering Workspace UX Specification) — ADR-0069/ADR-0070 added (both Accepted) — two genuinely new decisions surfaced by UX specification, not reserved numbers answered; 68 → 70 ADR total. Previously reviewed 2026-07-30 (WP 8.1A, Workspace Shell) — ADR-0068 added (Accepted) — a genuinely new decision (`Tempest.App`'s own default launch target), not a reserved number answered; 67 → 68 ADR total. Previously reviewed 2026-07-30 (WP 8.0B, Workspace Contracts) — ADR-0066/ADR-0067 added (both Accepted), resolving both ADRs `WP 8.0A` reserved — zero reserved-but-unwritten ADR numbers remain. Previously reviewed 2026-07-30 (WP 8.0A, Engineering Workspace Architecture) — ADR-0062 through ADR-0065 added (all Accepted), the first ADRs of the `v0.8.0` release; ADR-0066/ADR-0067 newly reserved for a future Contract Review Work Package, not yet written. Previously reviewed 2026-07-30 (WP 7.3A, Requirements Engine) — ADR-0058 through ADR-0061 added (all Accepted), closing the entire reserved range `WP7.2C Required ADR Catalogue.md` named. Previously reviewed 2026-07-30 (WP 7.1E, Verification Framework) — ADR-0057 added (Accepted) — the fifth and final Engineering Foundation framework ADR, closing the `ADR-0053`–`ADR-0057` range `WP7.0C Required ADR Catalogue.md` reserved. Previously reviewed 2026-07-30 (WP 7.1D, Engineering Calculation Framework) — ADR-0056 added (Accepted). Previously reviewed 2026-07-30 (WP 7.1C, Materials Framework) — ADR-0055 added (Accepted). Previously reviewed 2026-07-30 (WP 7.1B, Units & Quantities Framework) — ADR-0054 added (Accepted). Previously reviewed 2026-07-30 (WP 7.1A, Engineering Data Model) — ADR-0053 added (Accepted); disclosed a small, previously-uncorrected staleness in this very field (it had not been updated since WP 6.6, despite WP 7.0C's own edit to this register's Numbering Integrity narrative in the interim). Previously reviewed 2026-07-29 (WP 6.6, Licensing). |
| **Related Documents** | `docs/academy/06 Engineering Standards/Engineering Governance.md` (§5, ADR Creation Rules); `Decision Register.md`; `Rejected Designs Register.md`; `Traceability Matrix.md`; `docs/releases/v0.6.0/Required ADRs.md`. |
| **Related ADRs** | All 74 — this register's entire subject matter. |
| **Related Academy Articles** | Every Work Package retrospective under `docs/academy/03 Work Packages/` cites the ADR(s) it produced or realised; see each retrospective's own "ADR references" or "Architectural Principles" section. |
| **Coverage Status** | Complete — every ADR file present in `docs/adr/` at time of review is listed below. |

---

## How to Read This Register

Each ADR is **Verified** directly from its own file: number, title, Status
line, and originating Work Package are all read from the file itself, not
inferred or assumed. No ADR in this repository has been superseded or
reversed as of this baseline — every one carries a **Status: Accepted**
line, verified directly.

## Entries

| ADR | Title | Status | Originating Work Package | Date | Verification |
|---|---|---|---|---|---|
| ADR-0001 | RuntimeModule Is Immutable | Accepted | WP 2.2 | 2026-07-22 | Verified |
| ADR-0002 | Lifecycle State Is Managed Externally, Not On the Module | Accepted | WP 2.3 | 2026-07-22 | Verified |
| ADR-0003 | Module Constructors Must Be Side-Effect-Free | Accepted | WP 2.1 (reaffirmed WP 2.3, WP 2.4) | 2026-07-21/22 | Verified |
| ADR-0004 | Dispose Is Permitted From Every State Except Disposed | Accepted | WP 2.3 (reviewed under architectural review, WP 2.7B) | 2026-07-22 | Verified |
| ADR-0005 | Build a Custom, Minimal Dependency Injection Container | Accepted | WP 2.4 | 2026-07-22 | Verified |
| ADR-0006 | Constructor Injection Only | Accepted | WP 2.4 | 2026-07-22 | Verified |
| ADR-0007 | The Service Provider Owns All Module Construction | Accepted | WP 2.4 | 2026-07-22 | Verified |
| ADR-0008 | Discovery Does Not Depend on the Dependency Injection Container | Accepted | WP 2.1 (reaffirmed WP 2.4) | 2026-07-21/22 | Verified |
| ADR-0009 | The Composition Root Owns Externally-Created Services | Accepted | WP 2.5 | 2026-07-22 | Verified |
| ADR-0010 | The Module Pipeline Depends on the Logging Abstraction, Not a Concrete Logger | Accepted | WP 2.6 | 2026-07-22 | Verified |
| ADR-0011 | Discovery and Registration Precede Dependency Injection Container Construction | Accepted | WP 2.7 (architecture) | 2026-07-22 | Verified |
| ADR-0012 | The Runtime Host Owns Its Own, Independent State Machine | Accepted | WP 2.7 (architecture) | 2026-07-22 | Verified |
| ADR-0013 | Platform-Service Failures Abort Host Startup; Module Failures Remain Isolated | Accepted | WP 2.7 (architecture) | 2026-07-22 | Verified |
| ADR-0014 | Cancellation and Shutdown-Request Are Distinct Signals | Accepted | WP 2.7 (architecture) | 2026-07-22 | Verified |
| ADR-0015 | Runtime Hosts Are Not Restartable | Accepted | WP 2.7 (Open Question 2) | 2026-07-22 | Verified |
| ADR-0016 | The Host Lives in Tempest.Core.Runtime, Distinct From Tempest.Core.Hosting | Accepted | WP 2.7 (Open Question 3) | 2026-07-22 | Verified |
| ADR-0017 | Discovery, Registration, and Lifecycle Remain Host-Owned Collaborators, Not Public DI Services | Accepted | WP 2.7 (Open Question 4) | 2026-07-22 | Verified |
| ADR-0018 | Startup Cancellation Transitions to Controlled Shutdown | Accepted | WP 2.7 (final open question) | 2026-07-22 | Verified |
| ADR-0019 | Host Disposal Is Always an Explicit, Idempotent Call | Accepted | WP 2.7B | 2026-07-22 | Verified |
| ADR-0020 | The Event Bus Is a DI-Public Platform Service | Accepted | v0.4.0 planning (WP 4.0 / WP 4.4) | 2026-07-23 | Verified |
| ADR-0021 | Background Service Failures Are Isolated by Default; Criticality Is Opt-In | Accepted | v0.4.0 planning (WP 4.0 / WP 4.5) | 2026-07-23 | Verified |
| ADR-0022 | Navigation and Commands Are Orthogonal Platform Services | Accepted | v0.4.0 planning (WP 4.0 / WP 4.6A / WP 4.7) | 2026-07-23 | Verified |
| ADR-0023 | Platform Layering — Dependencies Flow Downward Only | Accepted | v0.4.0 planning (platform-wide) | 2026-07-23 | Verified |
| ADR-0024 | Platform Contracts Are Packaged by Capability, Not a Shared Contracts Namespace | Accepted | WP 4.0 | 2026-07-23 | Verified |
| ADR-0025 | Plugin Failure Classification | Accepted | WP 4.2B | 2026-07-23 | Verified |
| ADR-0026 | Plugin Discovery and Plugin Loading Lifecycle Placement | Accepted | WP 4.2C | 2026-07-23 | Verified |
| ADR-0027 | A Declarative `ModuleMetadataAttribute` Decouples Discovery From Construction | Accepted | WP 4.4A | 2026-07-24 | Verified |
| ADR-0028 | Event Bus Dispatch, Subscription, and Failure Model | Accepted | WP 4.4 (architecture) | 2026-07-25 | Verified |
| ADR-0029 | Background Service Discovery, Ownership, and Orchestration Model | Accepted | WP 4.5 (design phase) | 2026-07-25 | Verified |
| ADR-0030 | Background Service Host Lifecycle Placement | Accepted | WP 4.5 (design phase) | 2026-07-25 | Verified |
| ADR-0031 | Navigation Contracts Belong in Tempest.Core; Rendering Remains an Application Responsibility | Accepted | WP 5.0A (Navigation Framework Architecture) | 2026-07-27 | Verified |
| ADR-0032 | Navigation Is a DI-Public Platform Service, Registered Imperatively, Reusing the Event Bus | Accepted | WP 5.0A (Navigation Framework Architecture) | 2026-07-27 | Verified |
| ADR-0033 | The Shell Is a Composition Root Layered Above the Runtime Host, Not a Module or a Hosted Service | Accepted | WP 5.0C (Shell & Composition Framework Architecture) | 2026-07-27 | Verified |
| ADR-0034 | ITempestHost Exposes a Read-Only Service Resolution Surface for External Consumers | Accepted | WP 5.0C (Shell & Composition Framework Architecture) | 2026-07-27 | Verified |
| ADR-0035 | The Shell Owns Page/View Construction, Independent of the Platform's DI Container | Accepted | WP 5.0C (Shell & Composition Framework Architecture) | 2026-07-27 | Verified |
| ADR-0036 | The Command Framework Is a DI-Public Platform Service | Accepted | WP 5.1A (Command Framework Architecture) | 2026-07-28 | Verified |
| ADR-0037 | Commands Register Imperatively, in Two Parts — a Type-Keyed Handler and an Id-Keyed Descriptor | Accepted | WP 5.1A (Command Framework Architecture) | 2026-07-28 | Verified |
| ADR-0038 | Command Dispatch Propagates Handler Exceptions to the Caller, Diverging Deliberately from the Event Bus's Per-Subscriber Isolation | Accepted | WP 5.1A (Command Framework Architecture) | 2026-07-28 | Verified |
| ADR-0039 | Diagnostics Is a DI-Public, Lazily-Projected Read-Only Service Over Host-Owned Lifecycle State | Accepted | WP 5.2 (Diagnostics Improvements) | 2026-07-28 | Verified |
| ADR-0040 | Reporting Is DI-Public and Orthogonal to Export/Import — Template Abstraction, Cross-Service Integration, and Scope Boundaries | Accepted | WP 6.0 (Reporting Framework) | 2026-07-29 | Verified |
| ADR-0041 | A Shared Persistence Abstraction Serves Settings and Audit | Accepted | WP 6.4 (Settings Framework) | 2026-07-29 | Verified |
| ADR-0042 | Settings Is DI-Public and Distinct From Configuration | Accepted | WP 6.4 (Settings Framework) | 2026-07-29 | Verified |
| ADR-0043 | Identity Model Scope Is Local-Only, Extensible | Accepted | WP 6.1 (Permissions & Identity) | 2026-07-29 | Verified |
| ADR-0044 | `IPermissionEvaluator` Is the Single Authorization Enforcement Point; `CurrentPrincipalAccessor` Is Ambient, Not Request-Scoped | Accepted | WP 6.1 (Permissions & Identity) | 2026-07-29 | Verified |
| ADR-0045 | Audit Is a Durable, Queryable, Append-Only Record, Distinct From Logging and Diagnostics — Recording Model, Permission Gating, and Persistence Sufficiency | Accepted | WP 6.5 (Audit Framework) | 2026-07-29 | Verified |
| ADR-0046 | Notifications Are Derived From Events, Not a Replacement Pub/Sub — Dispatch Model, Severity/Category Elaboration, and Logging Level | Accepted | WP 6.2 (Notification Framework) | 2026-07-29 | Verified |
| ADR-0047 | The REST API Is a Background Hosted Service | Accepted | WP 6.3 (REST API) | 2026-07-29 | Verified |
| ADR-0048 | REST Endpoints Dispatch Through the Existing Command Framework | Accepted | WP 6.3 (REST API) | 2026-07-29 | Verified |
| ADR-0049 | Adopting ASP.NET Core/Kestrel for the REST API | Accepted | WP 6.3 (REST API) | 2026-07-29 | Verified |
| ADR-0050 | License Validation Is a Host-Startup, Host-Fatal Gate — Except a Missing License File, Which Is a Valid, Unrestricted Default | Accepted | WP 6.6 (Licensing Framework) | 2026-07-29 | Verified |
| ADR-0051 | Export/Import Is Orthogonal to the Internal Persistence Abstraction — Kind Routing, Format/Serialization Abstractions, and Scope Boundaries | Accepted | WP 6.7 (Export/Import) | 2026-07-29 | Verified |
| ADR-0052 | The REST API Resolves Identity Per-Request Without Touching the Ambient Current Principal — Empirically Verified | Accepted | WP 6.3 (REST API) | 2026-07-29 | Verified |
| ADR-0053 | The Engineering Data Model Is Built Directly on the Existing Persistence Abstraction — No New Storage Mechanism | Accepted | WP 7.1A (Engineering Data Model) | 2026-07-30 | Verified |
| ADR-0054 | Units & Quantities — Representation, Precision, and Registration Model | Accepted | WP 7.1B (Units & Quantities Framework) | 2026-07-30 | Verified |
| ADR-0055 | Materials Framework — Property Typing and Platform-Service Classification | Accepted | WP 7.1C (Materials Framework) | 2026-07-30 | Verified |
| ADR-0056 | Calculation Framework — Purity Enforcement and Dispatch Model | Accepted | WP 7.1D (Engineering Calculation Framework) | 2026-07-30 | Verified |
| ADR-0057 | Verification Framework — Relationship to Audit and Method Vocabulary | Accepted | WP 7.1E (Verification Framework) | 2026-07-30 | Verified |
| ADR-0058 | Requirements Engine Classification, Storage, and Relationship to the Engineering Data Model | Accepted | WP 7.3A (Requirements Engine) | 2026-07-30 | Verified |
| ADR-0059 | Requirement Identity, Status, and Category Representation | Accepted | WP 7.3A (Requirements Engine) | 2026-07-30 | Verified |
| ADR-0060 | Requirement Concurrency and Traceability Integrity Model | Accepted | WP 7.3A (Requirements Engine) | 2026-07-30 | Verified |
| ADR-0061 | Requirements Engine — Internal vs. Calling-Layer Permission Enforcement | Accepted | WP 7.3A (Requirements Engine) | 2026-07-30 | Verified |
| ADR-0062 | Engineering Workspace Is a Graphical Evolution of the Composition Root, Additive to the Console Shell | Accepted | WP 8.0A (Engineering Workspace Architecture) | 2026-07-30 | Verified |
| ADR-0063 | Workspace Views Read Directly; Mutations Dispatch Through the Command Framework | Accepted | WP 8.0A (Engineering Workspace Architecture) | 2026-07-30 | Verified |
| ADR-0064 | Workspace Layout and Session State Is Persisted via the Existing Settings Service | Accepted | WP 8.0A (Engineering Workspace Architecture) | 2026-07-30 | Verified |
| ADR-0065 | Digital Thread Visualisation Composes Existing Reads, Introduces No New Traversal Mechanism | Accepted | WP 8.0A (Engineering Workspace Architecture) | 2026-07-30 | Verified |
| ADR-0066 | Engineering Workspace Presentation Is Terminal-Based, Not a Graphical Desktop Framework | Accepted | WP 8.0B (Workspace Contracts) | 2026-07-30 | Verified |
| ADR-0067 | Workspace Extensibility Is Kind-Keyed Registration, for Both Views and Explorer Nodes | Accepted | WP 8.0B (Workspace Contracts) | 2026-07-30 | Verified |
| ADR-0068 | Engineering Workspace Is `Tempest.App`'s Own Default Launch Target | Accepted | WP 8.1A (Workspace Shell) | 2026-07-30 | Verified |
| ADR-0069 | The Engineering Cockpit Is the Workspace's Own Default Landing Screen | Accepted | WP 8.0C (Engineering Workspace UX Specification) | 2026-08-04 | Verified |
| ADR-0070 | The Command Palette Is a First-Class, Global Entry Point | Accepted | WP 8.0C (Engineering Workspace UX Specification) | 2026-08-04 | Verified |
| ADR-0071 | Workspace Extensibility Registrations Are Made by the Composition Root, Not by Discovered Modules | Accepted | WP 8.1B (Navigation & Project Explorer) | 2026-08-04 | Verified |
| ADR-0072 | Every Canonical Engineering Object Is an `IEngineeringDocumentStore`-Backed `Kind`, Never a New Storage/Type Hierarchy | Accepted | WP 8.2A (Engineering Domain Architecture) | 2026-08-04 | Verified |
| ADR-0073 | Relationships Between Engineering Objects Are Open-String `DocumentReference`s, Platform-Wide | Accepted | WP 8.2A (Engineering Domain Architecture) | 2026-08-04 | Verified |
| ADR-0074 | Lifecycle Status Is a Common Canonical Vocabulary, Specialised Per Object Family | Accepted | WP 8.2A (Engineering Domain Architecture) | 2026-08-04 | Verified |

**Total: 74 ADRs, all Accepted, none superseded or reversed (Verified — no
ADR file in `docs/adr/` carries a Superseded/Deprecated/Rejected status
line). Both ADRs `WP 8.0A` reserved (`ADR-0066`, `ADR-0067`) are now
resolved by `WP 8.0B` — no reserved-but-unwritten ADR number remains
outstanding.**

## Numbering Integrity

Sequential and complete, `ADR-0001` through `ADR-0074`, with no gaps at
all. `ADR-0066`/`ADR-0067`, reserved by `WP 8.0A`, were resolved by the
very next Work Package (`WP 8.0B`, its own Contract Review), the same
one-Work-Package-later cadence `ADR-0058`–`ADR-0061` established for
the Requirements Engine (reserved `WP 7.2B`/`WP 7.2C`, answered
`WP 7.3A`) — here compressed even further, since both were answered by
the Contract Review stage itself rather than waiting for
implementation. `ADR-0068` was not reserved by any prior Work Package —
a genuinely new question (`Tempest.App`'s own default launch target)
that only became answerable once both composition roots
(`TempestShell`, the Workspace) were real, compiled code, which did not
happen until `WP 8.1A` itself. `ADR-0069`/`ADR-0070` were likewise not
reserved by any prior Work Package — both are genuinely new product/UX
decisions (default landing screen, global command discoverability)
that only became answerable once the full target experience was
specified (`WP 8.0C`), not anticipated at the architecture or contract
stage. `ADR-0071` was likewise not reserved — it is a correction,
surfaced by `WP 8.1B`'s own first real registration against `ADR-0067`'s
own mechanism, of a worked example inside `ADR-0067` itself that does
not hold against the real Host/Workspace boundary `ADR-0062` already
established. `ADR-0067` remains Accepted and unmodified — its own core
Kind-keyed-registration decision is unaffected; only its illustrative
example was wrong, and `ADR-0071` records the correction as a new,
separate ADR rather than editing an already-Accepted one, per Engineering
Governance §5. `ADR-0072`–`ADR-0074` were likewise not reserved by any
prior Work Package — each formalises, as binding platform-wide
architecture, a pattern the Engineering Core's own four already-shipped
frameworks (`Tempest.Core.Requirements`/`Verification`/`Materials`/
`Calculations`) had independently converged on without coordination;
`WP 8.2A`'s own contribution is naming that convergence once, not
inventing a new decision from nothing. `docs/releases/v0.7.0/WP7.0C Required ADR Catalogue.md` reserved
`ADR-0053` through `ADR-0057` for the five Engineering Foundation
frameworks' own anticipated architectural decisions, one per framework
— all five (`ADR-0053` Engineering Data Model, `ADR-0054` Units &
Quantities, `ADR-0055` Materials, `ADR-0056` Calculation, `ADR-0057`
Verification) are now real, Accepted files, each implemented exactly as
that catalogue anticipated, each also resolving at least one genuine
question the catalogue did not itself anticipate (`ADR-0054`:
affine/offset unit conversion; `ADR-0055`: `IMaterialCatalog`'s own
direct `IPersistenceStore` dependency; `ADR-0056`: `Calculate`'s own
signature change to accept a `CalculationContext`; `ADR-0057`:
verification history queried via the Data Model's own existing
reference mechanism, requiring no new index or Persistence dependency
at all). This closes `WP7.0C Required ADR Catalogue.md`'s own entire
reserved range — every Engineering Foundation ADR it anticipated is now
Accepted, exactly as `ADR-0040`–`ADR-0052` were once only a catalogue
entry before their own owning Work Packages implemented them.

`docs/releases/v0.7.0/WP7.2C Required ADR Catalogue.md` reserved
`ADR-0058` through `ADR-0061` for the Requirements Engine's own
anticipated implementation decisions — all four are now real, Accepted
files, each implemented exactly as that catalogue anticipated
(`ADR-0058`: Platform Service classification and Engineering Data Model
reuse; `ADR-0059`: independent representation decisions for status,
identifier, and category; `ADR-0060`: no compare-and-swap concurrency
mechanism, accepted as `TD-25`; `ADR-0061`: no internal permission
gating, mirroring Materials'/Calculations' own precedent). This closes
`WP7.2C Required ADR Catalogue.md`'s own entire reserved range.
`docs/releases/v0.6.0/Required ADRs.md` reserved `ADR-0040` through
`ADR-0051` in advance, as a catalogue of anticipated decisions, one
range per `v0.6.0` Work Package, before any of those Work Packages began
implementation. `WP 6.1`, `WP 6.4`, `WP 6.5`, `WP 6.2`, `WP 6.0`, `WP
6.3`, `WP 6.7`, and now `WP 6.6` are all eight of those Work Packages,
each having now implemented; their own reserved numbers
(`ADR-0040`–`ADR-0051`) are now real, Accepted files. `ADR-0052` is new,
genuinely implementation-driven — not anticipated by `Required ADRs.md`
at all — documenting a decision `WP 6.3`'s own brief authorised ("if
deviation is required... produce the appropriate ADR"): identity
resolution and audit attribution for the REST API, resolved without
touching `CurrentPrincipalAccessor`'s own already-shipped design (see
that ADR's own Context for the empirical verification behind this).
Verified by direct enumeration of `docs/adr/` cross-checked against
that table. Per Engineering Governance §5, a superseded ADR would be
marked as such in its own Status section with a new ADR created
referencing it, rather than renumbered or deleted; no such case exists
yet in this repository.

## Cross-Reference Check

- Every ADR above is cited by at least one entry in `Decision Register.md`
  and at least one Work Package retrospective (`Traceability Matrix.md`
  gives the full chain for each major feature). Confirmed by direct
  grep of `docs/academy/03 Work Packages/` for each ADR number — no
  orphaned ADR (one cited nowhere outside its own file) was found.
- ADR-0021 (Background Service failure classification) was decided during
  original v0.4.0 planning, *before* WP 4.5 existed as a named Work
  Package — this register records its originating WP as "v0.4.0 planning
  (WP 4.0 / WP 4.5)" rather than forcing it into a single WP, matching
  how `CHANGELOG.md` and `WorkPackages.md` themselves describe it.
