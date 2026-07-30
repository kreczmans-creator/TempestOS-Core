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
| **Last Reviewed** | 2026-07-30 (WP 7.1C, Materials Framework) — ADR-0055 added (Accepted). Previously reviewed 2026-07-30 (WP 7.1B, Units & Quantities Framework) — ADR-0054 added (Accepted). Previously reviewed 2026-07-30 (WP 7.1A, Engineering Data Model) — ADR-0053 added (Accepted); disclosed a small, previously-uncorrected staleness in this very field (it had not been updated since WP 6.6, despite WP 7.0C's own edit to this register's Numbering Integrity narrative in the interim). Previously reviewed 2026-07-29 (WP 6.6, Licensing). |
| **Related Documents** | `docs/academy/06 Engineering Standards/Engineering Governance.md` (§5, ADR Creation Rules); `Decision Register.md`; `Rejected Designs Register.md`; `Traceability Matrix.md`; `docs/releases/v0.6.0/Required ADRs.md`. |
| **Related ADRs** | All 55 — this register's entire subject matter. |
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

**Total: 55 ADRs, all Accepted, none superseded or reversed (Verified — no
ADR file in `docs/adr/` carries a Superseded/Deprecated/Rejected status
line).**

## Numbering Integrity

Sequential and complete, `ADR-0001` through `ADR-0055`, with no gaps
at all. `docs/releases/v0.7.0/WP7.0C Required ADR Catalogue.md` reserved
`ADR-0053` through `ADR-0057` for the five Engineering Foundation
frameworks' own anticipated architectural decisions, one per framework
— `ADR-0053` (Engineering Data Model), `ADR-0054` (Units & Quantities),
and `ADR-0055` (Materials) are now real, Accepted files, each implemented
exactly as that catalogue anticipated, each also resolving one genuine
question the catalogue did not itself anticipate (`ADR-0054`:
affine/offset unit conversion, see its own "Temperature Deliberately
Deferred" section; `ADR-0055`: `IMaterialCatalog`'s own direct
`IPersistenceStore` dependency for its own `materialId` index, see its
own Decision 3).
`ADR-0056` and `ADR-0057` (Calculation, Verification & Validation) remain
reserved, not yet Accepted, awaiting their own owning Work Packages —
exactly as `ADR-0040`–`ADR-0052` were once only a catalogue entry before
their own owning Work Packages implemented them.
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
