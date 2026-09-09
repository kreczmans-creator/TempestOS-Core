# WP 8.2A — Engineering Domain Architecture — Engineering Object Interaction Diagrams

## Purpose

Diagrams making the Canonical Object Catalogue, Relationship Catalogue,
Lifecycle Specification, and Digital Thread Specification concrete —
conceptual only, no implementation technology implied by any diagram
below.

## 1. Canonical Object Families

```mermaid
graph TD
    Portfolio --> Programme --> Project
    Project --> Assembly
    Assembly --> SubAssembly --> Part
    Part --> Material
    Project --> Requirement
    Requirement --> RequirementSet[Requirement Set]
    Requirement -.calculatedBy.-> CalcResult[Calculation Result]
    Requirement -.verifiedBy.-> VerResult[Verification Result]
    Requirement -.allocatedTo.-> Assembly
    Part -.documentedBy.-> Drawing
    Drawing -.derivesFrom.-> CADModel[CAD Model]
    Part -.manufacturedBy.-> ManufOp[Manufacturing Operation]
    ManufOp -.documentedBy.-> WorkInstr[Work Instruction]
    Part -.suppliedBy.-> Supplier
    Project --> Risk
    Project --> Issue
    Project --> Decision
    Project --> ChangeRequest[Change Request]
    ChangeRequest -.derivesInto.-> EngChange[Engineering Change]
    Project --> Baseline
    Baseline -.composedOf.-> Assembly
    Baseline -.composedOf.-> Requirement
```

Solid arrows: Composition/Aggregation (structural containment). Dotted
arrows: named relationships from `Relationship Catalogue.md` §4 —
directed, but not structural containment.

## 2. Digital Thread Flow

```mermaid
flowchart LR
    Req[Requirement] -->|allocatedTo| Asm[Assembly / Part]
    Req -->|calculatedBy| Calc[Calculation Result]
    Calc -->|references| Mat[Material]
    Asm -->|documentedBy| CAD[CAD Model]
    CAD -->|source for| Drw[Drawing]
    Req -->|verifiedBy| Ver[Verification Result]
    Ver -->|basedOnCalculation| Calc
    Asm -->|manufacturedBy| Mfg[Manufacturing Operation]
    Mfg -->|documentedBy| WI[Work Instruction]
    Mfg -->|verifiedBy — Inspection| Ver2[Verification Result]
    Req -->|approvedBy — Acceptance| App[Approval]
    App -->|gates| Rel[Released — lifecycle state]
    Rel -->|composedOf| BL[Baseline]
    Ver -.->|evidence traversal| Ev[Evidence — composed read]
    Ver2 -.->|evidence traversal| Ev
    App -.->|evidence traversal| Ev
```

This is the controlling instruction's own named chain (Requirements →
Calculations → CAD → Verification → Manufacture → Inspection →
Acceptance → Release → Evidence), expressed entirely as
`Relationship Catalogue.md` hops — see `Digital Thread
Specification.md` §2 for the prose form.

## 3. Canonical Lifecycle States

```mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> InReview
    Draft --> Cancelled
    InReview --> Draft
    InReview --> Approved
    InReview --> Cancelled
    Approved --> Draft
    Approved --> Released
    Approved --> Cancelled
    Released --> Superseded
    Released --> Obsolete
    Released --> Archived
    Superseded --> Archived
    Obsolete --> Archived
    Archived --> [*]
    Cancelled --> [*]
```

Matches `Lifecycle Specification.md` §2 exactly — the canonical table
every object family specialises from (§4 of that document).

## 4. Worked Example: Requirement → Release (Already-Real Chain)

```mermaid
sequenceDiagram
    actor Engineer
    participant Req as Requirement (real)
    participant Calc as Calculation Result (real)
    participant Ver as Verification Result (real)
    participant BL as Baseline (conceptual)

    Engineer->>Req: Create, Draft
    Engineer->>Req: SetStatus(InReview)
    Engineer->>Calc: Execute calculation, referencing Material
    Engineer->>Req: LinkAsync(calculatedBy, Calc.Id)
    Engineer->>Ver: RecordAsync(Pass, basedOnCalculation: Calc.Id)
    Note over Req,Ver: verifiedBy link written subject to record
    Engineer->>Req: SetStatus(Approved) — requires Approved By (Approval)
    Engineer->>Req: SetStatus(Released)
    Engineer->>BL: Create Baseline, freezing Req + Calc + Ver at current revisions
```

Every step through `Ver` is real, shipped behaviour (`WP 7.3A`/
`WP 7.1D`/`WP 7.1E`); `Approved`/`Released`/`Baseline` steps are
canonical architecture, not yet implemented.

## 5. Worked Example: Physical Chain (Conceptual, Extending the Same Pattern)

```mermaid
sequenceDiagram
    actor Engineer
    participant Asm as Assembly (conceptual)
    participant Part as Part (conceptual)
    participant Drw as Drawing (conceptual)
    participant Mfg as Manufacturing Operation (conceptual)
    participant Insp as Inspection / Verification Result (conceptual)

    Engineer->>Asm: Create, Draft
    Engineer->>Part: Create, Composition child of Asm
    Engineer->>Drw: Create, documentedBy Part
    Engineer->>Part: SetStatus(Released)
    Engineer->>Mfg: Create, manufacturedBy Part
    Engineer->>Insp: RecordAsync(Pass) — Inspection Verification Activity
    Note over Mfg,Insp: verifiedBy link, Manufacturing Operation subject to Inspection record
    Engineer->>Asm: SetStatus(Released) once every child Part is Released
```

Identical shape to §4 — no new mechanism, only different `Kind` values
and relationship kinds, proving the canonical shape (`Engineering
Domain Architecture.md` §3) genuinely generalises across disciplines.

## Related Documents

`WP8.2A Engineering Domain Architecture.md`; `WP8.2A Canonical Object
Catalogue.md`; `WP8.2A Relationship Catalogue.md`; `WP8.2A Lifecycle
Specification.md`; `WP8.2A Digital Thread Specification.md`; `WP8.2A
Configuration Management Specification.md`.
