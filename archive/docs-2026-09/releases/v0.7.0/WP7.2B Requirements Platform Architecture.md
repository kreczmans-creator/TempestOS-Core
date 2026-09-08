# WP 7.2B — Requirements Platform Architecture

## Status

**Architecture only. No production code accompanies this document.**
Mirrors `WP7.0C Engineering Foundation Contracts.md`'s own role for the
Engineering Foundation programme — a proposed design, not a compiled
interface, deferred to its own owning implementation Work Package
(`WP7.2A Candidate Work Package Catalogue.md`'s own Candidate `K`/`L`,
now approved in principle by Product Approval's own acceptance of
Programme A). Every design decision below is justified against real,
existing repository evidence — the Engineering Core's own shipped
contracts — never against a hypothetical future need.

## 1. Purpose

Designs the complete architecture for the Requirements & Verification
Platform (`FCR-0027`) — the Systems Engineering Foundation continuing
TempestOS's Engineering Foundation programme. This Platform becomes the
canonical representation of engineering requirements, traceability,
allocation, and verification throughout TempestOS. It consumes the
Platform Core and Engineering Core; it introduces no discipline-specific
engineering behaviour; its architecture is designed to support every
future engineering discipline equally, not the two (Systems Engineering,
Project Management) that happen to have named platform-level hooks
today.

## 2. Classification (`ADR-0013`)

**The Requirements Engine is classified as a Platform Service**, exactly
as every one of its four Engineering Core siblings was classified.
`ADR-0013`'s own test — "does the rest of the platform, including other
modules, need this to exist before it can function at all?" — is
answered identically to how it was answered for `Tempest.Core.
Verification`: **no**, strictly speaking, nothing else requires it to
boot. But "Platform Service" in this repository's own established usage
(re-confirmed directly against `TempestHost.cs`'s own registration
order) has never been limited to boot-critical infrastructure — Materials,
Calculations, and Verification are all ordinary, container-constructed
DI singletons, registered in Phase 6 alongside Reporting, Audit, and
Settings, precisely because they are **shared infrastructure other
modules build on**, the same category `ADR-0013` itself names in its own
Future Considerations for exactly this capability ("a Requirements Engine
or Project Engine could plausibly be either a platform service or a set
of modules"). This architecture selects **platform service**, for the
identical reason every Engineering Core framework did: a Requirements
Engine is infrastructure every future discipline module would otherwise
each reinvent, not a single module's own private concern.

## 3. What This Platform Owns, and What It Explicitly Does Not

| Owns | Explicitly Does Not Own |
|---|---|
| Requirement identity, hierarchy, categorisation, lifecycle status | Discipline-specific engineering calculations (owned by `Tempest.Core.Calculations`) |
| Requirement collections, groups, and relationships (including allocation and traceability links) | Material specification data (owned by `Tempest.Core.Materials`) |
| Requirement revisioning (delegated to the Engineering Data Model, not reimplemented) | Verification outcome recording (delegated to `Tempest.Core.Verification`, not reimplemented) |
| Requirement evidence aggregation (a read-side view over already-recorded links, not a new evidence store) | Report layout/formatting (owned by `Tempest.Core.Reporting`) |
| Requirement search and lookup by business identifier | Export artifact framing (owned by `Tempest.Core.ExportImport`) |
| | Permission enforcement, action attribution (owned by `Tempest.Core.Identity`/`Tempest.Core.Audit`, composed at the calling layer, never internally) |
| | Any Mechanical, Structural, Electrical, HVAC, or Manufacturing engineering behaviour |

This table is the architecture's own single most important constraint:
**every capability this Platform needs, other than the requirement
concept itself, already exists somewhere in the Engineering Core or
Platform Core.** The Requirements Platform's own genuine, novel
contribution is narrow: a `Requirement` as a first-class engineering
entity, its hierarchy, its categorisation, and the specific relationship
vocabulary (allocation, traceability) that gives those requirements
meaning — everything else is composition, not invention.

## 4. Consumption of the Engineering Core and Platform Core

| Consumed Framework | Relationship | Evidence |
|---|---|---|
| `Tempest.Core.EngineeringData` | **Hard dependency.** A `Requirement` *is* an `IEngineeringDocument` (`Kind = "Requirement"`), exactly as a `MaterialSpecification` *is* one (`Kind = "MaterialSpecification"`, `ADR-0055`) and a `CalculationRecord<TResult>` *is* one (`Kind = "CalculationRecord"`, `ADR-0056`). Identity, revisioning, and reference storage are entirely delegated, never reimplemented. | `WP7.0C Cross-Framework Dependency Report.md`'s own Reuse Opportunities finding; `WP7.1F Engineering Core Architecture Conformance Report.md` §5 |
| `Tempest.Core.Verification` | **Hard dependency for verification recording.** Recording that a requirement has been demonstrated calls `IVerificationService.RecordAsync` directly against the requirement's own document Id — never a parallel, Requirements-owned verification mechanism. | `WP7.1E Future Capability Recommendations.md` Recommendation 1, stated directly for this exact integration |
| `Tempest.Core.Calculations` | **Soft, referenced-not-depended-on.** A requirement may cite a `CalculationRecord<TResult>` as supporting rationale or evidence — a bare `Guid` reference, validated only if `IEngineeringDocumentStore.FindAsync` confirms it exists, mirroring `CalculationContext.ReferenceMaterial`'s own open-reference precedent (`AT-16`). No compile-time dependency on `Tempest.Core.Calculations` is introduced. | `ADR-0056` Decision 6; `WP7.1F Engineering Core Consumption Matrix.md` |
| `Tempest.Core.Materials` | **Soft, referenced-not-depended-on.** Identical shape to Calculations above — a requirement may reference a material specification without a compile-time dependency, mirroring `VerificationContext.ReferenceMaterial` (`AT-17`). | `ADR-0057` Decision 5 |
| `Tempest.Core.UnitsAndQuantities` | **No relationship anticipated.** A requirement's own statement is text (`Content`, opaque `string`), not a dimensioned physical quantity. A future discipline-specific requirement type (e.g., "maximum deflection ≤ 5mm") would express that constraint in its own domain layer, consuming `Quantity<TDimension>` directly if needed — outside this Platform's own discipline-neutral scope. | `WP7.0C Cross-Framework Dependency Report.md`'s own Separation of Responsibilities table |
| `Tempest.Core.Identity` | **Hard dependency, calling-layer only.** `IPermissionEvaluator`/`ICurrentPrincipalAccessor` are composed by whatever caller invokes the Requirements Engine — the Engine itself never enforces authorization internally, mirroring `IReportingService`'s own explicit precedent. | `WP7.2B Dependency Analysis.md` |
| `Tempest.Core.Audit` | **Hard dependency, calling-layer only.** Recording that a requirement was created, revised, or allocated is the calling layer's own responsibility, mirroring every existing sample module's permission-check-then-audit-record pattern. | `WP7.2B Dependency Analysis.md` |
| `Tempest.Core.Reporting` | **Soft dependency, future consumer.** A Requirements Traceability Report is a plausible future `IReportDefinition`, consuming this Platform's own read APIs — this architecture does not design that report (`WP7.2B Platform Integration Report.md` §3). | This Work Package's own explicit "do not design report layouts" instruction |
| `Tempest.Core.ExportImport` | **Soft dependency, future consumer.** A Requirement Collection is a plausible `IExportable`/`IImportable` unit — this architecture names the integration point, not its implementation. | `ADR-0051` |
| `Tempest.Core.Api` | **Soft dependency, future consumer.** A future module may expose Requirements operations over REST via `IApiEndpointRegistry.MapCommand`, wrapping a Command exactly as every existing REST-exposed capability does — never a second, competing invocation mechanism. | `WP7.2B Platform Integration Report.md` §4 |
| `Tempest.Core.Settings`, `Tempest.Core.Notifications`, `Tempest.Core.Licensing` | **No dependency identified.** No concrete need for configurable settings, event notifications, or license-gated capability has been named for this Platform at architecture time. Not designed against speculatively. | `WP7.2B Dependency Analysis.md` |

## 5. Architectural Areas (Summary — Full Detail in Companion Documents)

Per this Work Package's own controlling instruction, the following
architectural areas are each designed at the responsibility level only,
detailed in `WP7.2B Requirements Domain Model.md` (domain concepts) and
`WP7.2B Systems Engineering Architecture.md` (layering and boundaries):

- **Requirements Engine** — the overall Platform Service; §2, above.
- **Requirement hierarchy** — parent/child grouping via `Requirement
  Group`, reusing `DocumentReference`/`LinkAsync` with a dedicated
  relationship kind, never a new tree-storage mechanism.
- **Requirement identity** — a stable `IEngineeringDocument` Guid plus a
  human-facing business identifier, mirroring `MaterialCatalog`'s own
  `materialId` index precedent (`ADR-0055` Decision 3).
- **Requirement lifecycle** — an explicit status value, distinct from
  verification outcome (a fact) — status is workflow position (a
  judgement), never derived automatically from a `VerificationRecord`.
- **Requirement categorisation** — an open, extensible string,
  mirroring `IMaterialSpecification.Category`'s own precedent — no
  closed taxonomy invented.
- **Requirement collections** — a named, purpose-built set of
  requirements (a baseline, a release scope), itself an
  `IEngineeringDocument` linking to its own members.
- **Requirement relationships** — the general-purpose typed link
  between two requirements, reusing `DocumentReference.RelationshipKind`
  directly.
- **Requirement allocation** — a specialised relationship, linking a
  requirement to an allocation target (a document of any `Kind`, or an
  open string when no target document exists yet) — deliberately
  discipline-agnostic.
- **Requirement traceability** — a specialised relationship expressing
  derivation ("derives from") and satisfaction ("satisfied by"),
  traversed via `GetReferencesAsync`, never a separate traceability
  store.
- **Requirement verification** — direct composition of
  `IVerificationService`; see §4, above. No duplicate mechanism.
- **Requirement evidence** — a read-side aggregation over a
  requirement's own linked `VerificationRecord`s, `CalculationRecord`s,
  and supporting documents — not a new stored entity.
- **Requirement reporting** — a future `IReportDefinition` consumer;
  layout not designed here.
- **Requirement metadata** — authorship and timestamp fields, inherited
  directly from `IEngineeringDocument`/`IDocumentRevision`, never
  duplicated.
- **Requirement revisioning** — inherited directly from
  `IDocumentRevision`; no second revision model.
- **Requirement search** — a typed index for business-identifier lookup
  (mirroring `MaterialCatalog`'s own direct `IPersistenceStore`
  dependency, `ADR-0055` Decision 3) plus client-side filtering for
  every other query shape, mirroring `IAuditQuery`'s own established,
  accepted `TD-12`-inheriting pattern — not a new query engine.
- **Requirement references** — external, non-platform references (a
  customer document, a named standard clause) as an open, unvalidated
  string, mirroring `MaterialPropertyProvenance.SourceReference`'s own
  precedent.
- **Requirement import/export** — a Requirement Collection implementing
  `IExportable`/`IImportable`, mirroring every existing Export/Import
  consumer; not designed further here.

## 6. What This Architecture Deliberately Does Not Design

- **Any specific requirement content, template, or discipline-specific
  requirement type** — this architecture is discipline-neutral by its
  own controlling instruction; a Mechanical or Electrical requirement's
  own specific shape is a future discipline module's own concern,
  building on this Platform, not designed by it.
- **Report layouts, REST endpoint routes, or security mechanisms** — all
  three are explicitly out of scope for this Work Package; see
  `WP7.2B Platform Integration Report.md` and `WP7.2B Security
  Architecture.md` for the architectural requirements each still names.
- **Any specific engineering standard's own compliance logic** — see
  `WP7.2B Standards Mapping.md` for architectural implications only,
  kept industry-neutral throughout.

## Related Documents

`ADR-0013`; `WP7.0C Engineering Foundation Contracts.md` (the format
precedent this document follows); `WP7.1E Future Capability
Recommendations.md`; `WP7.2A Recommended Programme.md`; `WP7.2B Systems
Engineering Architecture.md`; `WP7.2B Digital Thread Architecture.md`;
`WP7.2B Requirements Domain Model.md`; `WP7.2B Platform Integration
Report.md`; `WP7.2B Dependency Analysis.md`; `WP7.2B Security
Architecture.md`; `WP7.2B Standards Mapping.md`; `WP7.2B Required ADR
Catalogue.md`.
