# WP 8.0A — Engineering Workspace — Object Relationship Diagrams

## Purpose

Diagrams of the engineering object model the Workspace presents, and
how its own relationships compose into the digital thread — expanding
`WP8.0A Workspace Architecture Document.md` §6 and §9. Every diagram
below reflects the shipped `Tempest.Core` contracts exactly as they
exist (`WP 7.1A`–`WP 7.3A`); nothing here proposes a new relationship
kind, a new document `Kind`, or a new storage mechanism.

## 1. The Shared Engineering Document Model

```mermaid
classDiagram
    class IEngineeringDocument {
        +Guid Id
        +string Kind
        +int CurrentRevisionNumber
    }
    class IDocumentRevision {
        +int RevisionNumber
        +string Content
        +string CreatedByPrincipalId
        +DateTimeOffset CreatedAt
    }
    class DocumentReference {
        +Guid SourceDocumentId
        +Guid TargetDocumentId
        +string RelationshipKind
    }

    IEngineeringDocument "1" --> "many" IDocumentRevision : revision history
    IEngineeringDocument "1" --> "many" DocumentReference : source of
    IEngineeringDocument "1" --> "many" DocumentReference : target of

    class Requirement {
        Kind = "Requirement"
        +string Identifier
        +string Statement
        +RequirementStatus Status
        +string? Category
    }
    class RequirementGroup {
        Kind = "RequirementGroup"
        +string Name
    }
    class RequirementCollection {
        Kind = "RequirementCollection"
        +string Name
    }
    class Material {
        Kind = "Material"
    }
    class CalculationRecord {
        Kind = "CalculationRecord"
    }
    class VerificationRecord {
        Kind = "VerificationRecord"
    }

    IEngineeringDocument <|-- Requirement
    IEngineeringDocument <|-- RequirementGroup
    IEngineeringDocument <|-- RequirementCollection
    IEngineeringDocument <|-- Material
    IEngineeringDocument <|-- CalculationRecord
    IEngineeringDocument <|-- VerificationRecord
```

Every box below `IEngineeringDocument` in this diagram is a `Kind`
string, not a distinct storage mechanism — the Workspace's own View
layer is what gives each `Kind` a distinct presentation, not a distinct
persistence path (`WP8.0A Workspace Architecture Document.md` §6).

## 2. Requirement Relationships (`RequirementRelationshipKinds`)

```mermaid
graph LR
    R[Requirement] -->|GroupedUnder| G[RequirementGroup]
    R -->|CollectedIn| C[RequirementCollection]
    R -->|DependsOn| R2[Requirement]
    R -->|DerivesFrom| R3[Requirement]
    R -->|AllocatedTo| D[Any IEngineeringDocument<br/>e.g. a future design element]
    R -->|References| Doc[Any IEngineeringDocument]
    R -->|Satisfies| R4[Requirement]
    VR[VerificationRecord] -.->|verifiedBy<br/>owned by Verification, not Requirements| R
```

Six relationship kinds belong to `Tempest.Core.Requirements`
(`RequirementRelationshipKinds`); the seventh (`verifiedBy`) already
belongs to `Tempest.Core.Verification` and is never duplicated
(`WP7.2C Relationship Model.md`). The Workspace's own Project Explorer
(§3.1 of `WP8.0A Navigation Specification.md`) renders `GroupedUnder`
as tree parentage and `CollectedIn` as the Collections filter view; the
Digital Thread panel (§3 below) renders every kind uniformly as a
navigable entry.

## 3. Digital Thread Composition (What `GetEvidenceAsync` Actually Reads)

```mermaid
flowchart TD
    subgraph "GetEvidenceAsync (existing, WP 7.3A)"
        A[IRequirementsService.GetEvidenceAsync] --> B[IVerificationService.GetVerificationHistoryAsync]
        A --> C[IEngineeringDocumentStore.GetReferencesAsync]
        B --> D[Composed RequirementEvidence]
        C --> D
    end
    D --> E["Digital Thread panel<br/>(Workspace View, new)"]
```

The Workspace introduces nothing new here — `GetEvidenceAsync` already
composes verification history and linked references into one read
(`WP7.2B Digital Thread Architecture.md`'s own central claim, proven in
code by `WP 7.3A`). The Digital Thread panel is a **View** over an
**already-existing composed read**, not a new traversal mechanism —
consistent with `ADR-0065`.

## 4. Project Explorer Tree — Requirements Area (Worked Example)

```mermaid
graph TD
    Root[Requirements] --> Groups[Groups]
    Root --> Ungrouped[Ungrouped Requirements]
    Root --> Collections[Collections]

    Groups --> GA["Group A"]
    GA --> GA1["Group A.1"]
    GA1 --> REQ1["REQ-0001"]
    GA1 --> REQ2["REQ-0002"]
    GA --> REQ3["REQ-0003"]

    Groups --> GB["Group B"]
    GB --> REQ4["REQ-0004"]

    Ungrouped --> REQ5["REQ-0005"]

    Collections --> CX["Collection X"]
    Collections --> CY["Collection Y"]
    CX -.->|"membership (CollectedIn)"| REQ1
    CX -.->|"membership (CollectedIn)"| REQ4
```

Dotted lines show `CollectedIn` membership — a requirement (`REQ-0001`)
appears once under its own `Group` (solid hierarchy) and is referenced,
not duplicated, from whichever Collection(s) it belongs to (§3.1 of
`WP8.0A Navigation Specification.md`).

## 5. Allocation to a Future Design Element (Disclosed Limitation)

```mermaid
graph LR
    R[Requirement] -->|AllocatedTo| Target["Guid-typed target only<br/>(an existing IEngineeringDocument)"]
    R -.->|"not supported —<br/>FCR-0037, disclosed WP 7.3A"| Pending["An open-string target<br/>for a not-yet-created<br/>design element"]
```

The Workspace's own Allocation view can only ever show a real, existing
target object, matching `IRequirementsService.LinkAsync`'s own shipped,
Guid-only signature. Presenting a pending, not-yet-created allocation
target is not possible until `FCR-0037` (string-based allocation
targets) is implemented — this architecture does not design around a
capability that does not yet exist.

## Related Documents

`WP8.0A Workspace Architecture Document.md`; `WP8.0A Navigation
Specification.md`; `docs/academy/02 Runtime Architecture/
16-requirements-engine.md`; `docs/releases/v0.7.0/WP7.2C Relationship
Model.md`; `docs/releases/v0.7.0/WP7.3A Digital Thread Assessment.md`.
