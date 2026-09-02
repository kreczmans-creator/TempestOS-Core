# Engineering Vocabulary Register

## Register Metadata

| Field | Value |
|---|---|
| **Register Name** | Engineering Vocabulary Register |
| **Purpose** | The single, platform-wide catalogue of every live Kind, `Classification`, and `RelationshipKind` string value TempestOS uses — its value, the class that declares it as a named constant (its one canonical owner), and a one-line meaning. Realises `ADR-0105`, generalising `WP8.2A Canonical Object Catalogue.md`/`WP8.2A Relationship Catalogue.md`'s own already-proven documentation-layer shape, extended for the first time to also cover `Classification`, and given the same continuous-review discipline every other register in this directory already carries. |
| **Scope** | Every Kind, `Classification`, and `RelationshipKind` string value actually constructed, assigned, or linked anywhere in `src/` today — not the full ~50-item aspirational canonical object catalogue `WP8.2A` names, most of which have no implementation yet. A value with no canonical declaring class (used only by convention, in a sample module, or by a read-side consumer) is still listed, honestly marked **Undeclared**, never silently omitted. |
| **Owner** | Project Maintainer. |
| **Source of Truth** | The declaring classes themselves (see each entry's own "Declaring Class" column); `ADR-0105`; `docs/architecture/Classification & Relationship Vocabulary Safety Net Architecture.md`. |
| **Review Frequency** | Updated whenever a new Kind, `Classification`, or `RelationshipKind` value becomes live anywhere in `src/` — in practice, every Work Package that adds a new canonical object, a new `Classification` sub-value, or a new relationship kind. Cross-checked automatically, in part, by `EngineeringVocabularyConsistencyTests` (`Tempest.Desktop.Tests`) — see Related Documents. |
| **Last Reviewed** | 2026-08-29 (`WP — Project Tasks & Delivery Workflow`) — adds one `RelationshipKind`, `contributesTo`, owned by the new `Tempest.Core.EngineeringDomain.TaskRelationshipKinds` and written by a real `LinkAsync` call in `EngineeringTask.ContributeToAsync`; also mapped in `RelationshipKindCategoryMap` as an `Allocation`. 11 → 12 declared `RelationshipKind` values, 67 → 68 values total, 12 → 13 declaring classes. No Kind or `Classification` value changed: this Work Package created no new Kind, deliberately — Task, Action, Milestone and Deliverable were already declared by `CanonicalObjectKinds` and are used as they stand. Previously reviewed 2026-08-29 (`WP — Production Rehydration & Principal Boundary`) — adds the **21 canonical Kind entries** below owned by the new `Tempest.App.Workspace.CanonicalObjectKinds`, taking this register from 23 Kind values to 44 (67 values total, across 12 declaring classes). These 21 were live in `src/` before this Work Package but had **no canonical declaring class**: twelve existed only as string literals inside `Tempest.Samples` (the vocabulary duplication `TD-93` describes) and nine were never declared anywhere at all — so this is the register recording a real change of ownership in `src/`, not a retrospective correction. `CanonicalObjectKinds` is explicitly a temporary home: as each Kind gains a discipline workspace, its constant and its Declaring Class entry here move to that discipline's own registry. Previously reviewed 2026-08-12 (`WP 12.1B` Architecture Review Follow-Up, documentation only) — corrects a narrative-count error found by the `WP 12.1B` architecture/code review: the entry below claimed a declaring-class count of "12"; independently re-derived directly from this register's own three Entries tables (manual enumeration, cross-checked by an automated extraction of every `Declaring Class` column entry, deduplicated), the true count is **11** — `Tempest.Core.Requirements.RequirementsService`, `Tempest.Core.Verification.VerificationService`, `Tempest.Core.Calculations.CalculationEngine`, `Tempest.Core.Materials.MaterialCatalog`, `Tempest.App.Workspace.Mechanical.MechanicalObjectFactoryRegistry`, `Tempest.App.Workspace.Documents.DocumentObjectFactoryRegistry`, `Tempest.App.Workspace.Manufacturing.ManufacturingObjectFactoryRegistry`, `Tempest.App.Workspace.Calculations.CalculationObjectFactoryRegistry`, `Tempest.App.Workspace.Verification.VerificationActivityFactoryRegistry`, `Tempest.Core.Requirements.RequirementRelationshipKinds`, `Tempest.App.Workspace.Calculations.CalculationTemplateRegistry`. The original "12" over-counted `VerificationService`, which appears once in the Kind table and three times in the RelationshipKind table but is one class. All 46 individual values and their declaring classes were themselves already correct — this is a narrative-arithmetic correction only, not a change to any entry's Value/Declaring Class/Meaning. The same "12" figure, propagated from this field, is also corrected in `PROJECT_STATUS.md`, `docs/releases/v0.12.0/WorkPackages.md`, `Documentation Register.md`, `Academy Register.md`, `ADR Register.md`, and the `WP 12.1B` Academy retrospective. Documentation only; zero `src/`/`tests/` files touched; `ADR-0105` not reopened. Previously reviewed 2026-08-12 (`WP 12.1B`, Classification & Relationship Vocabulary Safety Net Implementation) — register created and populated for the first time, from a full, direct repository scan (every `public const string` Kind/`Classification`/`RelationshipKind` declaration in `Tempest.Core`/`Tempest.App`, cross-checked against every real `LinkAsync`/`CreateAsync` call site). 46 declared values (23 Kind, 12 Classification, 11 RelationshipKind) across 11 declaring classes, plus 7 `RelationshipKind` values used only by convention today, honestly marked Undeclared rather than omitted. Realises `ADR-0105`; retrofits the two confirmed defects `WP 12.1A` named (Mechanical's own eight Kinds; `DigitalThreadGraphModel`'s own `VerifiedByRelationshipKind`/`VerificationRecordKind`/`VerificationActivityKind` cross-layer duplicates) plus every further confirmed gap (Documents'/Manufacturing's own base Kinds; Calculations' own Workspace-layer Kinds). |
| **Related Documents** | `ADR-0105`; `docs/architecture/Classification & Relationship Vocabulary Safety Net Architecture.md`; `docs/releases/v0.11.0/WP11.0A Platform Architecture Review.md` (Finding `A-6`); `docs/releases/v0.8.0/WP8.2A Canonical Object Catalogue.md`; `docs/releases/v0.8.0/WP8.2A Relationship Catalogue.md`; `tests/Tempest.Desktop.Tests/EngineeringVocabularyConsistencyTests.cs` (Component 3 — the additive consistency check that reflects this register's own listed classes/fields and flags register/code drift or duplicate declarations). |
| **Related ADRs** | ADR-0072; ADR-0073; ADR-0076; ADR-0088; ADR-0090; ADR-0091; ADR-0105. |
| **Coverage Status** | Complete for every value with a real write path in `src/` today, confirmed by direct repository scan. Not claimed complete for values that may exist only in documentation (`WP8.2A Relationship Catalogue.md`'s own broader, aspirational vocabulary) with no real code behind them yet — those remain that document's own scope, not duplicated here. |

---

## Governing Rules

1. **One row per (Value, Vocabulary) pair per declaring class.** A value legitimately declared by more than one class (see `references`, below — a disclosed, pre-existing exception, not a defect) gets one row per declaring class, never merged into one row that hides the duplication.
2. **Never validated, never enforced.** This register is a coordination and discoverability aid only, per `ADR-0105`'s own explicit rule — nothing in this platform rejects a write because a value is absent from this register, and nothing here is intended to become a validation gate (`ADR-0105`'s own Future Considerations).
3. **"Declaring Class" is the sole canonical owner.** Per `ADR-0105`'s own ownership rule, the declaring class is whichever component *writes* the value (calls `CreateAsync`/`LinkAsync` with it) — never merely whichever project happens to compile the underlying object's own type (see `ADR-0078`'s "one Kind, one owner" precedent, applied one level down).
4. **A value with no declaring class is marked Undeclared, not omitted.** Discoverability is this register's whole purpose; hiding an undisciplined value would defeat it.

## Entries — Kind

| Value | Declaring Class | Meaning |
|---|---|---|
| `Requirement` | `Tempest.Core.Requirements.RequirementsService.RequirementDocumentKind` | A single Requirement. |
| `RequirementCollection` | `Tempest.Core.Requirements.RequirementsService.RequirementCollectionDocumentKind` | A named collection of Requirements. |
| `RequirementGroup` | `Tempest.Core.Requirements.RequirementsService.RequirementGroupDocumentKind` | A hierarchical Requirement grouping node. |
| `VerificationRecord` | `Tempest.Core.Verification.VerificationService.VerificationRecordDocumentKind` | A recorded Verification result, linked `verifiedBy` from its own subject. |
| `CalculationRecord` | `Tempest.Core.Calculations.CalculationEngine.CalculationRecordDocumentKind` | A recorded Calculation execution result. |
| `MaterialSpecification` | `Tempest.Core.Materials.MaterialCatalog.MaterialSpecificationDocumentKind` | A cataloged Material specification. |
| `Project` | `Tempest.App.Workspace.Mechanical.MechanicalObjectFactoryRegistry.Project` | A Mechanical Product Structure Project (root). |
| `Assembly` | `Tempest.App.Workspace.Mechanical.MechanicalObjectFactoryRegistry.Assembly` | A Mechanical Assembly. |
| `SubAssembly` | `Tempest.App.Workspace.Mechanical.MechanicalObjectFactoryRegistry.SubAssembly` | A Mechanical Sub-Assembly — always nested within a parent Assembly. |
| `Part` | `Tempest.App.Workspace.Mechanical.MechanicalObjectFactoryRegistry.Part` | A discrete Mechanical Part. |
| `Component` | `Tempest.App.Workspace.Mechanical.MechanicalObjectFactoryRegistry.Component` | A Mechanical Component. |
| `Configuration` | `Tempest.App.Workspace.Mechanical.MechanicalObjectFactoryRegistry.Configuration` | A working, mutable set of member revisions. |
| `Baseline` | `Tempest.App.Workspace.Mechanical.MechanicalObjectFactoryRegistry.Baseline` | A frozen snapshot of member revisions. |
| `Release` | `Tempest.App.Workspace.Mechanical.MechanicalObjectFactoryRegistry.Release` | A formally released set of member revisions. |
| `Document` | `Tempest.App.Workspace.Documents.DocumentObjectFactoryRegistry.Document` | A plain Document — Specification/Report/Procedure/Standard/Datasheet/External Reference/Resource/Tooling/Fixture are all this Kind, distinguished only by `Classification` (`ADR-0088`). |
| `Drawing` | `Tempest.App.Workspace.Documents.DocumentObjectFactoryRegistry.Drawing` | An Engineering Drawing — its own real Domain Kind, not a `Classification` value. |
| `CadModel` | `Tempest.App.Workspace.Documents.DocumentObjectFactoryRegistry.CadModel` | A CAD Model — its own real Domain Kind, not a `Classification` value. |
| `ManufacturingOperation` | `Tempest.App.Workspace.Manufacturing.ManufacturingObjectFactoryRegistry.ManufacturingOperationKind` | A Manufacturing Operation — Routing/Operation/Supplier Operation are all this Kind, distinguished only by `Classification` (`ADR-0091`). |
| `WorkInstruction` | `Tempest.App.Workspace.Manufacturing.ManufacturingObjectFactoryRegistry.WorkInstructionKind` | A Work Instruction against a Manufacturing Operation. |
| `Inspection` | `Tempest.App.Workspace.Manufacturing.ManufacturingObjectFactoryRegistry.InspectionKind` | An Inspection Operation — also a real, compiled `VerificationActivity` subtype. |
| `Calculation` | `Tempest.App.Workspace.Calculations.CalculationObjectFactoryRegistry.CalculationKind` | A single Engineering Calculation. |
| `CalculationSet` | `Tempest.App.Workspace.Calculations.CalculationObjectFactoryRegistry.CalculationSetKind` | A frozen set of member Calculations. |
| `VerificationActivity` | `Tempest.App.Workspace.Verification.VerificationActivityFactoryRegistry.SupportedKind` | A Verification Plan or Activity, distinguished from each other only by `LifecycleState` (`ADR-0090`, deliberately never `Classification`). |
| `Portfolio` | `Tempest.App.Workspace.CanonicalObjectKinds.Portfolio` | A portfolio of programmes — the top of the delivery hierarchy. |
| `Programme` | `Tempest.App.Workspace.CanonicalObjectKinds.Programme` | A programme of projects within a portfolio. |
| `Risk` | `Tempest.App.Workspace.CanonicalObjectKinds.Risk` | An identified risk, carrying likelihood and severity. |
| `Hazard` | `Tempest.App.Workspace.CanonicalObjectKinds.Hazard` | A safety hazard — a compiled `Risk` specialisation, and its own Kind. |
| `Issue` | `Tempest.App.Workspace.CanonicalObjectKinds.Issue` | An issue raised against the engineering work. |
| `Decision` | `Tempest.App.Workspace.CanonicalObjectKinds.Decision` | A recorded engineering decision, with its rationale. |
| `Assumption` | `Tempest.App.Workspace.CanonicalObjectKinds.Assumption` | A recorded assumption the engineering work depends on. |
| `Task` | `Tempest.App.Workspace.CanonicalObjectKinds.Task` | An engineering task (`EngineeringTask`). |
| `Action` | `Tempest.App.Workspace.CanonicalObjectKinds.Action` | An action arising from a review or meeting — a compiled `EngineeringTask` specialisation, and its own Kind. |
| `Milestone` | `Tempest.App.Workspace.CanonicalObjectKinds.Milestone` | A programme or project milestone, with a target date. |
| `Deliverable` | `Tempest.App.Workspace.CanonicalObjectKinds.Deliverable` | A deliverable due against a `Milestone`. |
| `ChangeRequest` | `Tempest.App.Workspace.CanonicalObjectKinds.ChangeRequest` | A request for an engineering change. |
| `EngineeringChange` | `Tempest.App.Workspace.CanonicalObjectKinds.EngineeringChange` | An engineering change being carried out against a `ChangeRequest`. |
| `Approval` | `Tempest.App.Workspace.CanonicalObjectKinds.Approval` | A formal approval record. |
| `Review` | `Tempest.App.Workspace.CanonicalObjectKinds.Review` | A formal review record. |
| `Supplier` | `Tempest.App.Workspace.CanonicalObjectKinds.Supplier` | A supplier. |
| `PurchaseItem` | `Tempest.App.Workspace.CanonicalObjectKinds.PurchaseItem` | An item purchased from a `Supplier`. |
| `ExternalSystemLink` | `Tempest.App.Workspace.CanonicalObjectKinds.ExternalSystemLink` | A link to an object held in an external system. |
| `Simulation` | `Tempest.App.Workspace.CanonicalObjectKinds.Simulation` | An engineering simulation against a subject object. |
| `Test` | `Tempest.App.Workspace.CanonicalObjectKinds.Test` | A test — a compiled `VerificationActivity` specialisation, and its own Kind. |
| `Verification` | `Tempest.App.Workspace.CanonicalObjectKinds.Verification` | The bare verification marker Kind (`WP 8.2C`), distinct from `VerificationActivity`. |

## Entries — Classification

| Value | Declaring Class | Meaning |
|---|---|---|
| `Specification` | `Tempest.App.Workspace.Documents.DocumentObjectFactoryRegistry.Specification` | A `Document` realising a formal Specification. |
| `Report` | `Tempest.App.Workspace.Documents.DocumentObjectFactoryRegistry.Report` | A `Document` realising a Report. |
| `Procedure` | `Tempest.App.Workspace.Documents.DocumentObjectFactoryRegistry.Procedure` | A `Document` realising a Procedure. |
| `Standard` | `Tempest.App.Workspace.Documents.DocumentObjectFactoryRegistry.Standard` | A `Document` realising a Standard. |
| `Datasheet` | `Tempest.App.Workspace.Documents.DocumentObjectFactoryRegistry.Datasheet` | A `Document` realising a Datasheet. |
| `External Reference` | `Tempest.App.Workspace.Documents.DocumentObjectFactoryRegistry.ExternalReference` | A `Document` realising an External Reference. |
| `Resource` | `Tempest.App.Workspace.Documents.DocumentObjectFactoryRegistry.Resource` | A `Document` realising a Manufacturing Resource (`WP 9.5A`). |
| `Tooling` | `Tempest.App.Workspace.Documents.DocumentObjectFactoryRegistry.Tooling` | A `Document` realising Manufacturing Tooling (`WP 9.5A`). |
| `Fixture` | `Tempest.App.Workspace.Documents.DocumentObjectFactoryRegistry.Fixture` | A `Document` realising a Manufacturing Fixture (`WP 9.5A`). |
| `Routing` | `Tempest.App.Workspace.Manufacturing.ManufacturingObjectFactoryRegistry.Routing` | A `ManufacturingOperation` used as a structural container for sequenced steps. |
| `Operation` | `Tempest.App.Workspace.Manufacturing.ManufacturingObjectFactoryRegistry.Operation` | A `ManufacturingOperation` realising a standalone or sequenced step. |
| `Supplier Operation` | `Tempest.App.Workspace.Manufacturing.ManufacturingObjectFactoryRegistry.SupplierOperation` | A `ManufacturingOperation` linked `manufacturedBy` to a real Supplier. |

## Entries — RelationshipKind

| Value | Declaring Class | Meaning |
|---|---|---|
| `groupedUnder` | `Tempest.Core.Requirements.RequirementRelationshipKinds.GroupedUnder` | Requirement Group hierarchy — child group/requirement to parent group. |
| `collects` | `Tempest.Core.Requirements.RequirementRelationshipKinds.CollectedIn` | Requirement Collection membership — collection to member requirement. |
| `dependsOn` | `Tempest.Core.Requirements.RequirementRelationshipKinds.DependsOn` | A general dependency between two requirements. |
| `derivesFrom` | `Tempest.Core.Requirements.RequirementRelationshipKinds.DerivesFrom` | Backward traceability (derivation). |
| `allocatedTo` | `Tempest.Core.Requirements.RequirementRelationshipKinds.AllocatedTo` | Requirement allocation to a target of any Kind. |
| `references` † | `Tempest.Core.Requirements.RequirementRelationshipKinds.References` | A non-owning cross-reference between two requirements. |
| `satisfies` | `Tempest.Core.Requirements.RequirementRelationshipKinds.Satisfies` | Forward traceability (satisfaction) — satisfying target to requirement. |
| `verifiedBy` | `Tempest.Core.Verification.VerificationService.VerifiedByRelationshipKind` | Subject document to its own recorded Verification result. |
| `references` † | `Tempest.Core.Verification.VerificationService.ReferencesRelationshipKind` | Verification record to an additional linked document. |
| `basedOnCalculation` | `Tempest.Core.Verification.VerificationService.BasedOnCalculationRelationshipKind` | Verification record to a linked Calculation record. |
| `calculatedBy` | `Tempest.App.Workspace.Calculations.CalculationTemplateRegistry.CalculatedByRelationshipKind` | A Calculation to the template it was executed from. |
| `contributesTo` | `Tempest.Core.EngineeringDomain.TaskRelationshipKinds.ContributesTo` | A Task or Action to the Milestone or Deliverable it contributes to. One kind for both targets: a Deliverable already knows its own `MilestoneId`, so a second kind would be a second answer. |
| `manufacturedBy` | **Undeclared** — used only by sample modules and `RelationshipKindCategoryMap`'s own conventional mapping | Object to the Supplier that manufactures it. |
| `documentedBy` | **Undeclared** — used only by sample modules, read-side facet providers, and `RelationshipKindCategoryMap`'s own conventional mapping | Object to a Document/Drawing describing it. |
| `blocks` | **Undeclared** — conventional only, `RelationshipKindCategoryMap`-recognised, no confirmed write site | A dependency that blocks another object. |
| `relatedTo` | **Undeclared** — conventional only, `RelationshipKindCategoryMap`-recognised, no confirmed write site | A generic, non-owning cross-reference. |
| `supersedes` | **Undeclared** — conventional only, `RelationshipKindCategoryMap`-recognised, no confirmed write site | An object that supersedes another. |
| `duplicates` | **Undeclared** — conventional only, `RelationshipKindCategoryMap`-recognised, no confirmed write site | An object that duplicates another. |
| `approvedBy` | **Undeclared** — conventional only, `RelationshipKindCategoryMap`-recognised, no confirmed write site | An object to its own approving record. |

**† `references` — disclosed, intentional dual ownership, not a defect.** `RequirementRelationshipKinds.References` and `VerificationService.ReferencesRelationshipKind` independently declare the identical value, for a genuinely shared, conventional meaning ("a generic cross-reference") neither discipline exclusively owns — unlike `verifiedBy`/`VerificationRecord`/`VerificationActivity`, which each have exactly one true writer. This mirrors `ADR-0073`'s own already-accepted "vocabulary drift" risk ("two modules independently inventing `blockedBy` and `blocks` for the same concept... a disclosed, accepted cost of extensibility") — recorded here explicitly rather than silently tolerated, and excluded by name from `EngineeringVocabularyConsistencyTests`'s own duplicate-declaration check for this exact reason.

## Cross-Reference Check

- Every declared Kind above is confirmed, by direct read, to be the literal argument passed to a real `EngineeringObjectFactory<T>` (or, for the five Domain-Foundation Kinds, `IEngineeringDocumentStore.CreateAsync`) somewhere in its own declaring class — never an aspirational, unimplemented catalogue entry.
- Every declared `Classification` value above is confirmed to be pattern-matched by its own discipline's `XCategory.Of` mapping function (`DocumentCategory.Of`/`ManufacturingCategory.Of`), never orphaned metadata.
- Every declared `RelationshipKind` value above is confirmed to be the literal argument passed to a real `LinkAsync` call somewhere in its own declaring class.
- `EngineeringVocabularyConsistencyTests` (`Tempest.Desktop.Tests`) reflects every class named above and confirms, at test time: every listed (Value, Declaring Class) pair still exists in code with the recorded value; every reflected constant is still listed here; and no two declaring classes state the identical (Value, Vocabulary) pair except the one disclosed `references` exception, above.
