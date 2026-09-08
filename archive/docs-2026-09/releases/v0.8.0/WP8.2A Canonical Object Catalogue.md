# WP 8.2A — Engineering Domain Architecture — Canonical Object Catalogue

## Purpose

Every Engineering Object TempestOS recognises, grouped into thirteen
families. Each entry names a proposed `Kind` string, whether it is
already implemented (and, if so, its real, shipped `Kind` constant),
its family, a one-line description, and its own key relationships (full
definitions in `Relationship Catalogue.md`). Every object shares the
canonical shape `WP8.2A Engineering Domain Architecture.md` §3 defines
— this catalogue does not repeat identity/metadata/lifecycle mechanics
per object, only what is specific to it.

**Reading the Status column:** `Implemented` means a real `Kind` ships
in `Tempest.Core` today, reconciled here, not redesigned.
`Conceptual` means the object is architecturally defined now, with no
implementation — the ordinary, expected state for almost every object
in this catalogue, per this Work Package's own explicit constraint.

## 1. Programme Hierarchy

The scope containers every other Engineering Object ultimately sits
within — deliberately thin: none of the three owns engineering content
directly, only groups other Engineering Objects.

| Object | Kind (proposed) | Status | Description | Key Relationships |
|---|---|---|---|---|
| Portfolio | `Portfolio` | Conceptual | The broadest scope container — a set of Programmes managed together | Parent of Programme |
| Programme | `Programme` | Conceptual | A set of related Projects sharing objectives or resources | Child of Portfolio; Parent of Project |
| Project | `Project` | Conceptual | The scope every other Engineering Object is, in practice, created within | Child of Programme; Parent of everything created within it |

## 2. Physical & Configuration

The physical-product hierarchy. `Configuration` is deliberately not a
physical object itself — it is a named, point-in-time arrangement of
other physical objects (§ `Configuration Management Specification.md`).

| Object | Kind (proposed) | Status | Description | Key Relationships |
|---|---|---|---|---|
| Assembly | `Assembly` | Conceptual | A physical product composed of Sub-Assemblies and/or Parts | Composition parent of Sub-Assembly/Part; Documented By Drawing/CAD Model |
| Sub-Assembly | `SubAssembly` | Conceptual | An Assembly nested within a larger Assembly — structurally identical to Assembly, distinguished only by its own parent relationship | Composition child of Assembly; Composition parent of Part |
| Part | `Part` | Conceptual | A single, non-decomposed physical item | Composition child of Assembly/Sub-Assembly; Manufactured By Manufacturing Operation; made of Material |
| Component | `Component` | Conceptual | A general term for Part or Assembly where the distinction does not matter to the referencing object (e.g. a Purchase Item referencing "a Component") | Alias family for Part/Assembly — never a third physical-hierarchy level |
| Configuration | `Configuration` | Conceptual | A named, point-in-time arrangement of specific Part/Assembly revisions | References specific revisions of Assembly/Part; realised via the Baseline mechanism, `Configuration Management Specification.md` §3 |

## 3. Requirements & Verification

**Already substantially implemented** (`Tempest.Core.Requirements`,
`WP 7.3A`; `Tempest.Core.Verification`, `WP 7.1E`) — reconciled here,
not redesigned. `WP7.2B Requirements Domain Model.md`'s own twelve
domain concepts remain the authoritative detail; this table only maps
the brief's own naming onto what already ships.

| Object | Kind (proposed) | Status | Description | Key Relationships |
|---|---|---|---|---|
| Requirement | `Requirement` | **Implemented** (`RequirementsService.RequirementDocumentKind = "Requirement"`) | A statement of need, opaque `Statement` text, closed 7-state lifecycle | Grouped Under Requirement Group; Collected In Requirement Set; Verified By Verification; Allocated To any Kind |
| Requirement Set | `RequirementCollection` / `RequirementGroup` | **Implemented** — two shipped shapes: `RequirementCollection` (named, non-hierarchical membership) and `RequirementGroup` (hierarchical) | A named grouping of Requirements — the brief's single "Requirement Set" name covers both already-shipped shapes; no third grouping type introduced | Collects/Groups Requirement |
| Verification | `Verification` | Conceptual — an umbrella term over the two real, narrower concepts below; not itself a distinct `Kind` | The general concept of confirming a Requirement is met | Umbrella for Verification Activity/Verification Result |
| Verification Activity | — | Conceptual (not yet a distinct `Kind` — see Note) | The planned or performed act of verifying (a test, inspection, analysis, or demonstration) | Verifies Requirement; Uses Test/Inspection |
| Verification Result | `VerificationRecord` | **Implemented** (`VerificationService.VerificationRecordDocumentKind = "VerificationRecord"`) | The recorded outcome of a Verification Activity — `Pass`/`Fail`/`Conditional`, criteria, evidence | Verified By relationship, subject → record; Based On Calculation; Documented By linked documents |

**Note (disclosed, not a defect):** the shipped `IVerificationService.RecordAsync`
combines "activity" and "result" into one `VerificationRecord` written
at the moment verification is recorded — there is no separate,
persisted "planned but not yet performed" Verification Activity object
today. The brief's own three-way split (Verification / Activity /
Result) is retained here as the target canonical shape; closing the gap
(a genuinely separate, revisable Verification Activity object,
distinct from its own eventual Result) is named as a candidate for a
future Work Package, not designed further here (architecture only).

## 4. Calculations

**Already implemented** (`Tempest.Core.Calculations`, `WP 7.1D`).

| Object | Kind (proposed) | Status | Description | Key Relationships |
|---|---|---|---|---|
| Calculation | `Calculation` | Conceptual — the general concept; realised today as a calculation *definition* (a pure function plus metadata), not itself a stored `Kind` | The method/model performing an engineering calculation | Produces Calculation Result; References Material |
| Calculation Set | — | Conceptual | A named grouping of related Calculations (mirrors Requirement Set) | Groups Calculation |
| Calculation Result | `CalculationRecord` | **Implemented** (`CalculationEngine.CalculationRecordDocumentKind = "CalculationRecord"`) | The recorded output of one Calculation execution — result, assumptions, intermediate results, validation, referenced Materials | Calculated By relationship, subject → record; References Material; Referenced By Verification Result |

## 5. Materials

**Already implemented** (`Tempest.Core.Materials`, `WP 7.1C`).

| Object | Kind (proposed) | Status | Description | Key Relationships |
|---|---|---|---|---|
| Material | `MaterialSpecification` | **Implemented** (`MaterialCatalog.MaterialSpecificationDocumentKind = "MaterialSpecification"`) | A named material specification — properties, each with mandatory provenance | Referenced By Part/Calculation Result/Verification Result |

## 6. Documentation & Design

| Object | Kind (proposed) | Status | Description | Key Relationships |
|---|---|---|---|---|
| Document | `Document` | Conceptual | A general engineering document with no more specific canonical shape (a specification, a report, a memo) | Documents any Engineering Object; may itself be Reviewed/Approved |
| Drawing | `Drawing` | Conceptual | An engineering drawing — a specialised Document with a defined graphical content convention | Documents Assembly/Part; Derived From CAD Model |
| CAD Model | `CadModel` | Conceptual | A 3D or 2D computer-aided-design model | Documents Assembly/Part; Source For Drawing |
| Simulation | `Simulation` | Conceptual | A computational simulation run (structural, thermal, fluid, etc.) — a specialised Calculation Result with simulation-specific metadata | Calculated By relationship over Assembly/Part; References Material |

## 7. Test & Manufacturing

| Object | Kind (proposed) | Status | Description | Key Relationships |
|---|---|---|---|---|
| Test | `Test` | Conceptual | A physical or analytical test performed as a Verification Activity | Verifies Requirement; Produces Verification Result |
| Inspection | `Inspection` | Conceptual | A physical inspection performed as a Verification Activity or as an acceptance step | Verifies Requirement or Manufacturing Operation output; Produces Verification Result |
| Manufacturing Operation | `ManufacturingOperation` | Conceptual | A single step in producing a Part | Manufactured By relationship, Part → operation; Uses Work Instruction |
| Work Instruction | `WorkInstruction` | Conceptual | A documented procedure directing a Manufacturing Operation | Documents Manufacturing Operation |

## 8. Supply Chain

| Object | Kind (proposed) | Status | Description | Key Relationships |
|---|---|---|---|---|
| Supplier | `Supplier` | Conceptual | An external organisation providing Parts, Materials, or services | Manufactured By relationship, Part → Supplier |
| Purchase Item | `PurchaseItem` | Conceptual | A procured Part, Material, or Component, with commercial metadata | References Part/Component/Material; Supplied By Supplier |

## 9. Governance & Risk

| Object | Kind (proposed) | Status | Description | Key Relationships |
|---|---|---|---|---|
| Issue | `Issue` | Conceptual | A recorded problem requiring resolution | Blocks/Related To any Engineering Object |
| Risk | `Risk` | Conceptual | A recorded potential future problem, with likelihood/severity metadata | Related To any Engineering Object; may produce Action |
| Hazard | `Hazard` | Conceptual | A recorded source of potential harm (safety-specific specialisation of Risk) | Related To Assembly/Part/Manufacturing Operation |
| Decision | `Decision` | Conceptual | A recorded engineering or programme decision, with rationale | Related To any Engineering Object; Approved By Approval |
| Assumption | `Assumption` | Conceptual | A recorded assumption underlying a Requirement, Calculation, or Decision | Related To Requirement/Calculation Result/Decision |

## 10. Process & Approval

| Object | Kind (proposed) | Status | Description | Key Relationships |
|---|---|---|---|---|
| Task | `Task` | Conceptual | A unit of planned work | Related To any Engineering Object; owned by a principal (`Metadata Specification.md`) |
| Action | `Action` | Conceptual | A unit of work arising from a Review, Issue, or Risk — narrower than Task, always traceable to what raised it | Derived From Review/Issue/Risk |
| Review | `Review` | Conceptual | A recorded review event over one or more Engineering Objects | Reviews any Engineering Object; produces Action |
| Approval | `Approval` | Conceptual | A recorded approval decision, gating a lifecycle transition | Approved By relationship, object → Approval (`Lifecycle Specification.md` §Approval Gates) |
| Milestone | `Milestone` | Conceptual | A significant, dated programme event | Related To Deliverable/Release |
| Deliverable | `Deliverable` | Conceptual | A named output owed at a Milestone | Related To Milestone; may reference any Engineering Object as its own content |

## 11. Change & Release

| Object | Kind (proposed) | Status | Description | Key Relationships |
|---|---|---|---|---|
| Change Request | `ChangeRequest` | Conceptual | A proposed change, not yet approved or implemented | Related To the object(s) it proposes to change; Approved By Approval |
| Engineering Change | `EngineeringChange` | Conceptual | An approved, implemented change — the realised outcome of a Change Request | Derived From Change Request; Supersedes the prior revision/Configuration |
| Release | `Release` | Conceptual | A named, frozen set of Engineering Objects made available for use downstream | Composed of a Baseline (`Configuration Management Specification.md` §3) |
| Baseline | `Baseline` | Conceptual | A named, frozen Configuration — realised via the same named-collection pattern `RequirementCollection` already ships | Freezes specific revisions of its own member objects |

## 12. Evidence & Reference

| Object | Kind (proposed) | Status | Description | Key Relationships |
|---|---|---|---|---|
| Evidence | — | Conceptual — a composed read, never a stored `Kind` (mirrors `IRequirementEvidence`) | The aggregated proof that a claim (verification, approval, compliance) holds | Composed by traversal from Verification Result/Approval/Review |
| Reference | — (realised as `DocumentReference.RelationshipKind = "references"`) | **Implemented** as a relationship, not an object | A non-owning cross-reference between two Engineering Objects | See `Relationship Catalogue.md` §References |
| External System Link | `ExternalSystemLink` | Conceptual | A recorded pointer to an object in a system outside TempestOS | Related To the external system's own identifier (opaque string, no integration built) |
| Attachment | `Attachment` | Conceptual | A file or binary payload associated with an Engineering Object, distinct from its own opaque `Content` | Documents the object it is attached to |

## 13. Classification & Extensibility

| Object | Kind (proposed) | Status | Description | Key Relationships |
|---|---|---|---|---|
| Tag | — (metadata field, not an object) | Conceptual | A free-text label — part of the common metadata envelope (`Metadata Specification.md`), never a first-class Engineering Object requiring its own identity | N/A |
| Classification | — (metadata field, not an object) | Conceptual | A security/sensitivity classification value — part of the common metadata envelope, per `WP8.2A Engineering Domain Architecture.md` §5 | N/A |
| Custom Object extension mechanism | *(caller-defined)* | Architectural mechanism, not an object | Any module may mint a new `Kind` value honouring the canonical shape (§3) without platform review — `ADR-0072`'s own extensibility consequence | N/A — this row documents the mechanism, not a single object |

## Cross-Reference Check

49 named objects from the controlling instruction, all accounted for
above: 3 (Programme Hierarchy) + 5 (Physical & Configuration) + 5
(Requirements & Verification) + 3 (Calculations) + 1 (Materials) + 4
(Documentation & Design) + 4 (Test & Manufacturing) + 2 (Supply Chain)
+ 5 (Governance & Risk) + 6 (Process & Approval) + 4 (Change & Release)
+ 4 (Evidence & Reference) + 3 (Classification & Extensibility) = 49.

Five entries are already real, shipped `Kind` values (`Requirement`,
`RequirementCollection`/`RequirementGroup`, `VerificationRecord`,
`CalculationRecord`, `MaterialSpecification`); one (`Reference`) is
already real as a relationship rather than an object; two (`Tag`,
`Classification`) are metadata fields, not objects requiring their own
identity; one (`Custom Object extension mechanism`) documents a
mechanism, not a single object. The remaining 40 are Conceptual —
architecturally defined, not implemented — the expected, explicitly
required state for this Work Package.

## Related Documents

`WP8.2A Engineering Domain Architecture.md`; `WP8.2A Relationship
Catalogue.md`; `WP8.2A Lifecycle Specification.md`;
`docs/releases/v0.7.0/WP7.2B Requirements Domain Model.md`.
