# WP 8.2B — Engineering Domain Contracts — Sequence Diagrams

## Purpose

The eight named scenarios, each as a mermaid `sequenceDiagram` over the
contracts `WP8.2B Interface Catalogue.md`/`Relationship Contract
Specification.md`/`Lifecycle Contract Specification.md`/`Validation
Contract Specification.md`/`Digital Thread Contract Specification.md`
define — conceptual only, no implementation technology implied.

## 1. Object Creation

```mermaid
sequenceDiagram
    actor Caller
    participant Factory as IEngineeringObjectFactory
    participant Obj as IEngineeringObject

    Caller->>Factory: CreateAsync(initialContent)
    Factory->>Obj: (constructs, Kind fixed, Id assigned)
    Factory-->>Caller: IEngineeringObject
    Note over Obj: Status = LifecycleState.Draft (default, WP8.2A Lifecycle Specification.md §1)
```

## 2. Relationship Creation

```mermaid
sequenceDiagram
    actor Caller
    participant Source as IHasRelationships
    participant Validator as IRelationshipValidator
    participant Factory as IEngineeringRelationshipFactory

    Caller->>Validator: ValidateAsync(proposed relationship)
    Validator-->>Caller: IValidationResult (no self-reference check, §5)
    Caller->>Factory: CreateAsync(sourceId, targetId)
    Factory-->>Caller: IEngineeringRelationship
    Caller->>Source: LinkAsync(targetId, relationshipKind)
```

## 3. Lifecycle Transition

```mermaid
sequenceDiagram
    actor Caller
    participant Obj as IHasLifecycle
    participant Table as ILifecycleTransitionTable
    participant Rule as ILifecycleValidationRule

    Caller->>Table: IsPermitted(Obj.Status, target)
    Table-->>Caller: true/false
    Caller->>Rule: ValidateTransitionAsync(Obj.Id, Obj.Status, target)
    Rule-->>Caller: IValidationResult
    Caller->>Obj: TransitionAsync(target)
    Obj-->>Caller: (ILifecycleTransitionRecord appended to History)
```

## 4. Approval

```mermaid
sequenceDiagram
    actor Approver
    participant Gate as IApprovalGate
    participant Approval as IApproval
    participant Obj as IHasLifecycle

    Approver->>Approval: (created, ApproverPrincipalId = Approver)
    Obj->>Obj: LinkAsync(Approval.Id, "approvedBy")
    Note over Obj: Approved By relationship written (Relationship Catalogue.md §4)
    Obj->>Gate: IsSatisfiedAsync(Obj.Id)
    Gate-->>Obj: true
    Obj->>Obj: TransitionAsync(LifecycleState.Approved)
```

## 5. Revision

```mermaid
sequenceDiagram
    actor Engineer
    participant Obj as IHasRevisions

    Engineer->>Obj: ReviseAsync(newContent, changeSummary)
    Obj-->>Engineer: IHasRevisions (new CurrentRevisionNumber)
    Engineer->>Obj: GetRevisionHistoryAsync()
    Obj-->>Engineer: IReadOnlyList~IRevisionRecord~ (append-only, unchanged prior entries)
```

## 6. Baseline Creation

```mermaid
sequenceDiagram
    actor Engineer
    participant Checker as IReferenceIntegrityChecker
    participant Baseline as IBaseline

    Engineer->>Checker: CheckBaselineMembersAsync(proposed Baseline)
    Checker-->>Engineer: IValidationResult (every member must resolve, Validation Contract Specification.md §5)
    Engineer->>Baseline: (constructed, MemberRevisions frozen at current RevisionNumber per member)
    Engineer->>Baseline: TransitionAsync(LifecycleState.Released)
    Note over Baseline: Now an IRelease (Interface Catalogue.md §12)
```

## 7. Traceability Navigation

```mermaid
sequenceDiagram
    actor Reviewer
    participant Discovery as IRelationshipDiscovery
    participant Composer as IEvidenceComposer
    participant Ver as IVerificationResult
    participant Calc as ICalculationResult

    Reviewer->>Composer: ComposeAsync(subjectId)
    Composer->>Discovery: GetOutgoingAsync(subjectId)
    Discovery-->>Composer: relationships
    loop each Verification-category relationship
        Composer->>Ver: (resolve)
    end
    loop each Calculation-category relationship
        Composer->>Calc: (resolve)
    end
    Composer-->>Reviewer: IEvidence
```

## 8. Validation

```mermaid
sequenceDiagram
    actor Caller
    participant Obj as IValidatable
    participant RuleSet as IValidationRuleSet

    Caller->>Obj: ValidateAsync()
    Obj->>RuleSet: GetRulesFor(Obj.Kind)
    RuleSet-->>Obj: IReadOnlyList~IValidationRule~
    loop each rule
        Obj->>RuleSet: EvaluateAsync(Obj)
    end
    RuleSet-->>Obj: IValidationResult (Errors + Warnings, §1)
    Obj-->>Caller: IValidationResult
```

## Related Documents

`WP8.2B Engineering Domain Contracts.md` and its six other companion
deliverables; `WP8.2A Engineering Object Interaction Diagrams.md` (the
`WP 8.2A` precedent this document extends to the contract layer).
