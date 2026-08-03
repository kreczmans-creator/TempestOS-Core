# WP 7.1F — Engineering Core Architecture Conformance Report

## Purpose

An evidence-based confirmation that the complete, shipped Engineering
Core — `Tempest.Core.EngineeringData`, `Tempest.Core.UnitsAndQuantities`,
`Tempest.Core.Materials`, `Tempest.Core.Calculations`,
`Tempest.Core.Verification` — conforms to its own approved architecture
(`WP7.0B Engineering Foundation Architecture.md`, `WP7.0C Cross-Framework
Dependency Report.md`) and to this project's standing architectural rules
(`ADR-0023`), verified by direct inspection of the real, compiled
repository — mirroring `WP6.8 Platform Architecture Conformance
Report.md`'s own role for the `v0.6.0` platform. Every statement below is
backed by a command or file reference a reader can re-run.

## 1. Dependency Graph — Verified Against Real Code

`WP7.0C Cross-Framework Dependency Report.md` proposed this graph before
any implementation existed:

```
Materials --> Engineering Data Model
Materials --> Units & Quantities
Calculation -.by convention.-> Units & Quantities
Verification --> Engineering Data Model
```

Re-derived directly against the shipped code
(`grep -rhoP "^using Tempest\.Core\.[A-Za-z]+;" src/Tempest.Core/<namespace>`,
per framework, self-references excluded):

| Namespace | Depends On (real, compiled) |
|---|---|
| `EngineeringData` | `Concurrency`, `Identity`, `Logging`, `Persistence` |
| `UnitsAndQuantities` | *(none — zero dependencies of any kind)* |
| `Materials` | `Concurrency`, `EngineeringData`, `Logging`, `Persistence`, `UnitsAndQuantities` |
| `Calculations` | `EngineeringData`, `Identity`, `Logging` |
| `Verification` | `EngineeringData`, `Identity`, `Logging` |

**Conforms, with two confirmations beyond the original proposal:**

- `Calculations` does not import `UnitsAndQuantities` at all — the
  proposed "by convention, not a hard type constraint" relationship is
  now proven, not merely proposed: no `using` statement exists, only a
  documented expectation that a calculation's own input/output types
  are built from `Quantity<TDimension>` where dimensioned. `ADR-0056`
  Decision 5 confirms this was a deliberate choice, not an oversight.
- `Verification` depends on `EngineeringData` alone, never on
  `Calculations` or `Materials`, even though `VerificationContext`
  exposes `LinkCalculationRecord`/`ReferenceMaterial` — both are typed
  as a bare `Guid`/`string` respectively, validated (where validated at
  all) purely through `IEngineeringDocumentStore`'s own existence check,
  never through a compile-time reference to either sibling framework's
  own types. This is the structural avoidance `WP7.0C Cross-Framework
  Dependency Report.md` itself named as deliberately preventing a future
  circular dependency between Verification and a future Requirements
  Engine (`FCR-0027`), now confirmed unchanged through four further
  Work Packages of implementation.

`Materials`' own direct `Persistence` dependency (for its own
`materialId` index, `ADR-0055` Decision 3) and `EngineeringData`'s own
direct `Persistence`/`Identity` dependencies (`ADR-0053`) were not shown
in the pre-implementation graph, which depicted only Engineering
Foundation-to-Engineering-Foundation edges — both are downward
references to already-shipped `v0.6.0` Platform Services, not upward or
sideways references, and conform to `ADR-0023` without qualification.

## 2. Circular Dependency Analysis

**Zero circular dependencies exist within the Engineering Core, and zero
exist between the Engineering Core and any `v0.6.0` Platform Service.**
Traced explicitly:

- `EngineeringData` → `Persistence`, `Identity`, `Logging`, `Concurrency`
  (all terminal, all pre-existing `v0.6.0` Platform Services or
  leaves). No Platform Service imports anything under `EngineeringData`,
  `Materials`, `Calculations`, or `Verification` — confirmed directly
  (`grep -rl "Tempest.Core.EngineeringData\|Tempest.Core.Materials\|
  Tempest.Core.Calculations\|Tempest.Core.Verification" src/Tempest.Core/
  Audit src/Tempest.Core/Settings src/Tempest.Core/Persistence
  src/Tempest.Core/Identity` returns no matches).
- `UnitsAndQuantities` → nothing. The only Engineering Foundation
  framework, and one of only three namespaces in the entire `Tempest.Core`
  dependency graph (alongside `Concurrency` and `Hosting`), with zero
  internal dependencies of any kind.
- `Materials` → `EngineeringData`, `UnitsAndQuantities` (both terminal
  within the Engineering Core).
- `Calculations` → `EngineeringData` (terminal).
- `Verification` → `EngineeringData` (terminal).

No path exists from any Engineering Core namespace back to itself
through any other namespace. `EngineeringData` and `UnitsAndQuantities`
remain the two terminal frameworks a real implementation could begin
with in either order — confirmed exactly as `WP7.0C Cross-Framework
Dependency Report.md` anticipated, now proven against real code rather
than a proposed contract.

## 3. Four-Layer Dependency Rules (`ADR-0023`)

### Service → Module

**Verified: zero violations.** `grep -rl "Tempest.Samples"
src/Tempest.Core/EngineeringData src/Tempest.Core/UnitsAndQuantities
src/Tempest.Core/Materials src/Tempest.Core/Calculations
src/Tempest.Core/Verification` returns no matches. No Engineering Core
framework references `Tempest.Samples` in any form.

### Module → Module

**Not applicable to the Engineering Core's own framework code** — no
Engineering Core namespace is itself a module. The four new sample
modules (`EngineeringDataSampleModule`, `MaterialsSampleModule`,
`CalculationSampleModule`, `VerificationSampleModule`) were checked for
the one pre-existing, disclosed `Module → Module` exception
(`ApiSampleModule` → `ReportingSampleModule`, `WP 6.8`): none of the four
references another module's own type or members — confirmed directly
(`grep -l "Tempest.Samples\." src/Samples/Tempest.Samples/
EngineeringDataSampleModule.cs src/Samples/Tempest.Samples/
MaterialsSampleModule.cs src/Samples/Tempest.Samples/
CalculationSampleModule.cs src/Samples/Tempest.Samples/
VerificationSampleModule.cs` returns no matches beyond each module's own
file). No new `Module → Module` exception was introduced by the
Engineering Foundation programme.

### Runtime → Feature

**Verified: unchanged.** `TempestHost.cs`'s own registration block adds
four ordinary `Singleton<TInterface, TImplementation>()` calls
(`IEngineeringDocumentStore`, `IMaterialCatalog`, `ICalculationEngine`,
`IVerificationService`) in Phase 6, in the same pattern every prior
Platform Service registration already follows — no Engineering-Core-
specific conditional logic exists anywhere in `Tempest.Core.Runtime`.

## 4. Separation of Responsibilities — Verified Against Implementation

`WP7.0C Cross-Framework Dependency Report.md`'s own proposed responsibility
table, re-checked against what each framework actually implements:

| Framework | Owns (confirmed) | Confirmed Not to Own |
|---|---|---|
| Engineering Data Model | Document identity, revisioning, typed references | No calculation logic anywhere in `src/Tempest.Core/EngineeringData/` (`grep` for arithmetic/formula logic finds none); `Content` remains an opaque, uninterpreted `string` |
| Units & Quantities | Dimensioned value representation, unit conversion | No document storage, no DI registration at all — confirmed: zero `Singleton`/`AddInstance` calls reference `UnitsAndQuantities` types in `TempestHost.cs` |
| Materials | Named material catalogue, provenance-carrying properties | No design allowable, no safety factor, no calculation logic (`grep` of `src/Tempest.Core/Materials/` for either finds none, Principle 15) |
| Calculations | Registration/dispatch, execution records, evidentiary metadata | No specific formula of its own — `grep` of `src/Tempest.Core/Calculations/` finds dispatch/context/recording infrastructure only, never a concrete calculation (Principle 23) |
| Verification | Pass/Fail/Conditional outcome recording against a document | No Validation logic, no Requirements Management, no report formatting — confirmed by `grep` of `src/Tempest.Core/Verification/` for any of the three, finds none (Principle 28) |

**Finding: Satisfied, with zero overlap.** No two Engineering Core
frameworks claim the same responsibility; each framework's own exclusion
list (named explicitly in its own controlling instruction) was checked
directly against its own source tree, not merely against its own
retrospective's claim.

## 5. No Duplicated Capability

- Verification does not reinvent Audit's "who did what, when" — it
  answers a structurally different question ("was the claim
  demonstrated") and depends on neither `IAuditRecorder` nor
  `IAuditQuery`; both mechanisms are expected to be composed at a
  calling layer, never merged (`ADR-0057` Decision 1, `WP7.0C
  Cross-Framework Dependency Report.md`'s own Reuse Opportunities
  finding).
- Materials introduces no second storage or revisioning mechanism — it
  is a thin, typed index over `IEngineeringDocumentStore`, confirmed
  directly: `MaterialCatalog`'s own `FindAsync`/`ReviseAsync`
  implementations delegate to `IEngineeringDocumentStore` for every
  document-level operation.
- Calculation execution records reuse `EngineeringData.IEngineeringDocument`
  for durability rather than inventing a second identity/revision
  scheme — `CalculationRecord<TResult>.Id` is a real, usable
  `IEngineeringDocument` Id, confirmed directly by
  `ExecuteAsync_RecordId_IsDirectlyRetrievableThroughEngineeringDocumentStore`.
- Verification's own history query reuses `IEngineeringDocumentStore.
  GetReferencesAsync`/`LinkAsync` rather than building a new index — the
  single most significant architectural finding of the entire programme
  (`ADR-0057` Decision 3), confirmed directly: no `IPersistenceStore`
  dependency exists anywhere in `Tempest.Core.Verification`.

## 6. Public Interface Stability

**Zero approved-interface signature deviations across all five
Engineering Foundation Work Packages.** Every one of the eleven
Engineering Core interfaces (`IEngineeringDocument`, `IDocumentRevision`,
`IEngineeringDocumentStore`, `IDimension`, `IUnitConverter`,
`IMaterialCatalog`, `IMaterialSpecification`, `ICalculationDefinition<TInput,
TResult>`, `ICalculationEngine`, `IVerificationRecord`,
`IVerificationService`) was implemented matching `WP7.0C Engineering
Foundation Contracts.md`'s own proposed shape, in every case extended
additively (new members, new supporting types) rather than modified —
confirmed directly against each Work Package's own Implementation Report,
independently re-verified here by cross-checking every interface's own
current signature in `src/Tempest.Core/` against its own originating
contract-review document. No breaking change to any of the eleven was
made by a later Work Package in this programme.

## 7. Governance Register Backfill — A Repeat of `WP 6.8`'s Own Finding, Found and Closed

**The single most significant finding of this Work Package's own
Architecture Review.** `Interface Register.md`, `Dependency Injection
Register.md`, and `Module Register.md` — the same three registers
`WP 6.8` fully backfilled for `v0.6.0` — had each gone stale again,
silently, across all five Engineering Foundation Work Packages:

- **`Interface Register.md`** still read 64 interfaces (the `WP 6.8`
  count). The real, current count, verified directly
  (`grep -rhoP "^public interface \w+(<[^>]+>)?" src/Tempest.Core | sort -u
  | wc -l`), is **75** — the 11 Engineering Core interfaces had never
  been recorded.
- **`Dependency Injection Register.md`** still read 26 named
  registrations (28 raw call sites). The real, current count, verified
  directly (`grep -c "services\.\(Singleton\|AddInstance\)"
  src/Tempest.Core/Runtime/TempestHost.cs`), is **32** raw call sites (30
  named registrations) — the four Engineering Core registrations
  (`IEngineeringDocumentStore`, `IMaterialCatalog`, `ICalculationEngine`,
  `IVerificationService`) had never been recorded.
- **`Module Register.md`** still read 15 production modules. The real,
  current count, verified directly against
  `ClockModuleDiscoveryTests.DiscoverModules_ScopedToSampleAssembly_
  FindsEveryRealSampleModule`'s own assertion, is **19** — the four
  Engineering Core sample modules (`EngineeringDataSampleModule`,
  `MaterialsSampleModule`, `CalculationSampleModule`,
  `VerificationSampleModule`) had never been recorded.

**This is not a new failure mode — it is the identical pattern `WP 6.8`
itself found and closed for `v0.6.0`'s own six Work Packages, recurring
across five further Work Packages with no owning Work Package's own
scope including register maintenance.** `FCR-0005` (Governance Register
Health-Check Tooling), raised by `WP 6.8` specifically to catch this
class of drift before a third occurrence, was never built — this is now
the second occurrence its own risk register (`GR-1`, `WP7.0B Roadmap Risk
Register.md`) anticipated. All three registers have been fully backfilled
in this same Work Package (see the registers themselves for the complete,
corrected entries); `FCR-0005`'s own priority is raised accordingly in
`WP7.1F Future Capability Register Review.md`.

**Not release-blocking.** Every one of the eleven interfaces, four
registrations, and four modules was already correctly implemented,
tested, and documented in its own owning Work Package's own Implementation
Report and Academy retrospective — only this cross-cutting index had
drifted, exactly mirroring `WP 6.8`'s own finding that the underlying
capability was never in doubt, only its own governance record of it.

## 8. Documentation Gap Found and Closed: The Missing Engineering Data Model Concept Guide

`WP7.0C Academy Plan.md` named a new Engineering Data Model concept guide
as "the highest-priority new Academy content this entire programme
produces" — required output of `WP 7.1A`. No such guide existed anywhere
under `docs/academy/` before this Work Package: confirmed directly, no
file in `02 Runtime Architecture/` addressed the Engineering Data Model,
and no `### Engineering Data Model` section existed in `Academy Index.md`.
Four further Work Packages (`WP 7.1B`–`WP 7.1E`) built directly on the
Engineering Data Model without ever disclosing the gap. This Work Package
wrote `02 Runtime Architecture/15-engineering-data-model.md`, closing it —
see `Academy Register.md`'s own updated `Last Reviewed` entry for the
full disclosure.

## 9. API Stability Classification (Engineering Core Interfaces)

| Classification | Interfaces |
|---|---|
| **Stable** — approved, zero deviation, exercised by at least one real consumer (a sample module, a test, or a sibling framework), no disclosed intention to change | `IEngineeringDocument`, `IDocumentRevision`, `IEngineeringDocumentStore`, `IMaterialCatalog`, `IMaterialSpecification`, `ICalculationDefinition<TInput, TResult>`, `ICalculationEngine`, `IVerificationRecord`, `IVerificationService` |
| **Provisional** — real, tested, but a reserved/illustrative shape not yet proven against a second, independent design need | `IDimension` (a phantom-type marker with no members — its own "stability" is definitionally trivial, but the seven-dimension catalogue it bounds is disclosed as extensible, `TD-19`/`FCR-0034`) |
| **Reserved, not load-bearing** | `IUnitConverter` — declared per `WP7.0C Engineering Foundation Contracts.md`'s own proposed shape, but the framework's own actual conversion path is `Quantity<TDimension>.ConvertTo`, never this interface; no registration, no implementation beyond what the contract review itself anticipated as a placeholder |

## 10. Overall Verdict

**Conforms.** Zero `Service → Module`, `Module → Module`, or
`Runtime → Feature` violations exist anywhere the Engineering Core
touches. Zero circular dependencies exist within the Engineering Core or
between it and any `v0.6.0` Platform Service. Every approved interface
matches its own original design exactly, across all five Work Packages,
with zero unauthorised changes. One repeat governance-register-drift
finding and one missing-concept-guide finding were surfaced and fully
closed in this same Work Package — neither reflects a defect in the
Engineering Core's own architecture or implementation, both reflect
exactly the class of cross-cutting documentation drift a closing
certification review exists to catch.

## Related Documents

`ADR-0023`; `ADR-0053`–`ADR-0057`; `WP7.0B Engineering Foundation
Architecture.md`; `WP7.0C Cross-Framework Dependency Report.md`;
`docs/governance/Engineering/Interface Register.md`, `Dependency
Injection Register.md`, `Module Register.md` (all three fully backfilled
by this Work Package); `docs/academy/02 Runtime Architecture/
15-engineering-data-model.md` (written by this Work Package);
`WP7.1F Engineering Core Certification Report.md`; `WP7.1F Engineering
Core Consumption Matrix.md`.
