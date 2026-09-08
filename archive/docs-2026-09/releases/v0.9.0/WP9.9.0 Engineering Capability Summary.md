# WP 9.9.0 — Release Preparation & Product Baseline — Engineering Capability Summary

## Purpose

A snapshot of what a user can actually *do* with the Engineering
Workspace exactly as it ships in `v0.9.0` — verified directly against
the running Host and the passing test suite, not assumed from any prior
Work Package's own claim. New deliverable type for this release,
combining the "what the Workspace shows" and "what the Engineering
Domain now realises" angles `v0.8.0`'s own `WP8.9.0 Workspace Baseline
Summary.md`/`WP8.9.0 Engineering Domain Baseline Summary.md` each
covered separately — folded into one document here per this Work
Package's own named deliverable list.

## What Ships

`v0.8.0` shipped a real Workspace shell around a fixed, fictional
Project Explorer tree — the mechanism proven, but no real Engineering
discipline wired into it. `v0.9.0` is the release that changes that:
**six real Engineering Disciplines, end to end**, each with a browsable
Explorer area, a Property Inspector showing real facets, a full command
set, real Digital Thread links, and real Engineering Cockpit KPIs.

| Discipline | Capability | Status | Originating Work Package |
|---|---|---|---|
| **Mechanical Product Structure** | Browse Assemblies/Sub-Assemblies/Parts/Components; Rename/Move/Delete; real `ParentId` structural mutation | Real, running | `WP 9.0A` |
| | Bill of Materials (Quantity/UoM/Find Number/Item Number/Reference Designator); Baseline capture; Configuration comparison; validation rules | Real, running | `WP 9.0B` |
| **Requirements Management** | Browse Requirement Groups/Collections; Create/Edit/Delete/Move; lifecycle (Draft→Reviewed→Approved→Allocated→Verified→Satisfied→Obsolete); bulk status/owner/priority operations; multi-selection | Real, running | `WP 9.1A` |
| | Requirements validation (duplicate identifiers, orphans, missing verification/allocation, advisory relationship kinds) | Real, running | `WP 9.1A` |
| **Engineering Calculations** | Browse Calculations/Calculation Sets; Create/Edit/Delete/Move/Copy/Duplicate; Execute/Recalculate against the real `ICalculationEngine`; Lock/Unlock/Review/Approve/Archive | Real, running | `WP 9.2A` |
| | Real, evidentiary Calculation Records — every execution durably stored, re-derivable, cross-linked to Requirements/Materials | Real, running | `WP 9.2A` |
| **Engineering Documents** | Browse Drawings/CAD Models/Specifications/Reports/Procedures/Standards/Datasheets/External References; Create/Edit/Delete/Move/Copy/Duplicate/SetStatus/Attach | Real, running | `WP 9.4A` |
| | Real Attachments (metadata); real Digital Thread links to Requirements/Mechanical/Calculations/Decisions | Real, running | `WP 9.4A` |
| **Verification Management** | Browse Verification Activities by Method (Inspection/Analysis/Test/Demonstration); Create/Edit/Delete/Move/Copy/Duplicate; Record a real Pass/Fail/Conditional result with criteria and evidence; Review/Approve/Archive | Real, running | `WP 9.3A` |
| | Real, evidentiary Verification Records, cross-linked to Requirements/Calculations/Mechanical/Documents | Real, running | `WP 9.3A` |
| **Manufacturing** | Browse Routings/Operations/Supplier Operations/Work Instructions/Inspections; Create/Edit/Delete/Move/Copy/Duplicate/Release/Archive; sequenced Routing steps | Real, running | `WP 9.5A` |
| | Manufacturing BOM (reuses Mechanical's own `SetBomLineCommand` unmodified, zero new code); Supplier linkage; real recorded Inspection results (reuses Verification's own command unmodified) | Real, running | `WP 9.5A` |
| **Engineering Cockpit** | Real, derived health status and dedicated KPI card set for five of six disciplines (Requirements/Calculations/Documents/Verification/Manufacturing); real conditional Attention Items/Open Actions for all six | Real, running | `WP 9.0A`–`WP 9.5A` |
| **Digital Thread** | Real, live, queryable cross-discipline links spanning all six real disciplines plus Materials/Risks/Decisions, all via already-mapped relationship kinds | Real, running | `WP 9.0A`–`WP 9.5A` |
| Properties/Inspector panel | Real, running (six disciplines' worth of Kind-keyed facet providers) | Was "Not yet built" at `v0.8.0` | `WP 9.0A`–`WP 9.5A` |
| Command Palette | Real, running, discipline-scoped commands for all six disciplines, plus every `v0.8.0`-era global command | Real, running | `WP 9.0A`–`WP 9.5A` |
| Search | Real, running, generalises across all six disciplines with zero new code (`ProjectExplorer.FilterAsync`, `WP8.1B`, already Kind-agnostic) | Real, running | Proven anew by every one of the six |
| Project Dashboard (distinct from the Cockpit) | Not yet built | Unchanged from `v0.8.0` | Open |

## Engineering Domain — What Is Now Genuinely Consumed

`v0.8.0` compiled 38 concrete Engineering Domain object classes and
confirmed, by direct search, that **zero** of them had ever been
instantiated by anything beyond a sample module's own representative
graph. `v0.9.0` is the release that puts them to real, Workspace-driven
use:

| Metric | v0.8.0 | v0.9.0 | Verification |
|---|---|---|---|
| Concrete Engineering Domain classes with a real Workspace presence | 0 | 15 (`Assembly`/`SubAssembly`/`Part`/`Component`/`Configuration`/`Baseline`/`Release`, `Requirement`-family via `IRequirementsService`, `Calculation`/`CalculationSet`, `Document`/`Drawing`/`CadModel`, `VerificationActivity`, `ManufacturingOperation`/`WorkInstruction`/`Inspection`) | Direct count against each Work Package's own Implementation Report |
| Concrete Engineering Domain classes confirmed still never instantiated anywhere | 38 | 2 (`Test`, the bare `Verification` marker Kind — both deliberately unused, disclosed by `WP 9.3A`/`WP 9.5A` as scope-consistent, not gaps) | Direct repository-wide search, reconfirmed at this review |
| `IHasBomLine`-consuming commands proven Kind-agnostic across foreign Kinds | 0 | 1 (`Mechanical.SetBomLineCommand`, proven against `"ManufacturingOperation"` by a dedicated `WP 9.5A` test) | Direct test inspection |
| Cross-Work-Package Workspace-layer type reuse instances | 0 | 2 (`"WorkInstruction"`→`DocumentsPropertyFacetProvider`/`DocumentsWorkspaceView(Factory)`; `"Inspection"`→`VerificationActivityPropertyFacetProvider`/`VerificationActivityWorkspaceView(Factory)`, both `WP 9.5A`) | Direct source inspection, proven by dedicated tests |

## Verified Against the Running Host

- Every one of the six disciplines' own `*WorkspaceIntegrationTests.cs`
  file (seven files including Mechanical's, part of the 2026/2026
  passing suite) confirms the Workspace starts, reaches
  `HostState.Running`, navigates into the real discipline area, selects
  a real seeded object, and reads real Property Inspector facets — not
  a hand-assembled test pipeline.
- Every Cockpit KPI card for the five disciplines with a dedicated set
  either delegates to a real Engineering Domain read (`EngineeringDomainContext
  .Repository`/`RelationshipRepository`) or, for Mechanical's own
  original `WP 8.1C`-era reads, to `NavigationService`/`ICommandRegistry`
  — no fabricated data anywhere, confirmed directly against
  `EngineeringCockpit.cs` and its own six dedicated test files.
- Every discipline's own representative sample module seeds real,
  cross-linked data — none isolated; every sample module's own Digital
  Thread links resolve to another real, live discipline's own seeded
  object, confirmed by direct inspection of all seven sample modules.

## Known Limitations (Disclosed, Not Blocking)

1. **Properties/Inspector panel exists, but only for the six real
   disciplines** — a Materials/Risk/Decision/Hazard/Assumption Workspace
   presence remains unbuilt (`FCR-0056`, `WP 9.4A`).
2. **No governed Approval/Review workflow exists** — every discipline's
   own "Approval State"/"Review" is `LifecycleState` alone, not a
   queryable sign-off record (`TD-30`, `FCR-0052`/`FCR-0058`).
3. **No file/URL attachment storage service exists** — Document/
   Manufacturing Attachments carry descriptive metadata only, never
   actual file bytes (`TD-31`, `FCR-0054`).
4. **Project Dashboard (distinct from the Cockpit) remains unbuilt** —
   unchanged from `v0.8.0`, not part of any `v0.9.0` Work Package's own
   scope.
5. **Command Palette remains reachable from the Cockpit only** —
   unchanged, disclosed partial realisation of `ADR-0070` first
   disclosed at `v0.8.0`.

None of the five rises to Release Blocking — each is a named, disclosed,
deliberately-out-of-scope gap with a corresponding Future Capability
candidate, not a defect in what was actually built.

## Verdict

Six real Engineering Disciplines, end to end, sharing one proven
Kind-keyed extension model, one shared Digital Thread, and one
Engineering Cockpit — the Engineering Workspace's own core promise
(`WP 8.0A`) is now substantively delivered, not merely architected. No
further capability work is recommended before Product Approval.

## Related Documents

`docs/releases/v0.9.0/WP9.9.0 Release Readiness Report.md`;
`docs/releases/v0.9.0/WP9.9.0 Architecture Baseline Summary.md`;
`docs/governance/Future Capability Register.md`;
`docs/academy/02 Runtime Architecture/17-engineering-workspace.md`;
`docs/academy/02 Runtime Architecture/18-engineering-domain-architecture.md`.
