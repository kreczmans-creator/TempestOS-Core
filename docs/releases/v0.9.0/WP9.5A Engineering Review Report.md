# WP 9.5A — Manufacturing Workspace — Engineering Review Report

## Purpose

Reviews whether the shipped implementation satisfies `WP 9.5A`'s own
controlling instruction, and whether every engineering judgement call
made along the way was reasonable and disclosed.

## Acceptance Criteria Review

| Requirement | Verdict | Evidence |
|---|---|---|
| Manufacturing BOM, Manufacturing Assemblies, Manufacturing Parts, Operations, Routings, Work Instructions, Manufacturing Resources, Tooling, Fixtures, Supplier Operations, Inspection Operations, Manufacturing Readiness, Production Status | **Met, with disclosed representations for four items** | BOM via existing `IHasBomLine`/`SetBomLineCommand` (`WP 9.0B`, zero new code); Assemblies/Parts via existing Mechanical Kinds (`WP 9.0A`); Routings/Operations/Supplier Operations are `Classification`-tagged `ManufacturingOperation` (`ADR-0091`); Resources/Tooling/Fixtures are `Classification`-tagged `"Document"` (extends `ADR-0088`); Work Instructions/Inspection Operations are the real, already-compiled `WorkInstruction`/`Inspection` Kinds (`WP 8.2C`); Readiness/Production Status are Cockpit-only, computed live. |
| Workspace / Cockpit / Project Explorer / Property Inspector / Navigation / Command Palette / Search | **Met** | `ManufacturingNodeProvider`/`ManufacturingWorkspaceView(Factory)`/`ManufacturingOperationPropertyFacetProvider`, plus disclosed direct reuse of `DocumentsPropertyFacetProvider`/`VerificationActivityPropertyFacetProvider` for `"WorkInstruction"`/`"Inspection"`; 10 registered commands; real Cockpit KPIs. Search needed zero new code (`ProjectExplorer.FilterAsync`, `WP8.1B`, already generic). |
| Create/Edit/Delete/Copy/Duplicate/Move/Release/Archive | **Met** | Eight command classes; "Release"/"Archive" map 1:1 onto `LifecycleState.Released`/`.Archived`, no aliasing trick needed, mirroring `WP 9.4A`'s own identical finding. |
| Digital Thread traceability: Requirements, Mechanical Structure, Calculations, Verification, Documents, Manufacturing | **Met** | Real, live links to all six, all via already-mapped relationship kinds (`"references"`/`"manufacturedBy"`/`"documentedBy"`/`"verifiedBy"`) — see Implementation Report. |
| Engineering Cockpit real KPIs (Manufacturing Objects/Manufacturing Readiness/Released Items/Open Operations/Supplier Status/Inspection Status/Production Health) | **Met** | `ManufacturingKpiCards`/`ManufacturingStatus`; every card a real read, disclosed bucketing rules stated in the Implementation Report. |
| Representative data: Manufacturing Assembly, Routing, Operation sequence, Tooling, Fixture, Work Instruction — linked to existing engineering data | **Met** | `EngineeringManufacturingWorkspaceSampleModule` — one Routing with three sequenced Operation steps, one Supplier Operation, one Tooling and one Fixture Document, one Work Instruction, one recorded-Pass Inspection, all real links to the Mechanical/Requirements/Calculations/Documents sample data. |
| Quality: existing architecture/layering/contracts, Digital Thread compatibility, Workspace consistency | **Met** | See Architecture Conformance Review. |
| Unit/integration/Workspace tests; repeated Debug/Release verification | **Met** | 54 new tests, 2026/2026, four full clean-rebuild-and-test runs. |
| Documentation and Governance | **Met** | This document and its siblings; governance registers updated; the `WP 9.6A`–`WP 9.8A` skip disclosed plainly. |
| No architectural redesign; no contract redesign; no duplicate framework; reuse existing Engineering Objects exclusively | **Met, one disclosed additive Workspace-layer decision (`ADR-0091`), zero Domain-layer changes** | See Architecture Conformance Review. |

## Scope Discipline Review

**Routings/Operations/Supplier Operations share one Domain Kind,
distinguished by `Classification` alone.** Confirmed directly: no
`Routing`/`SupplierOperation` Kind is declared anywhere in `WP 8.2B`'s
own frozen contract catalogue. Building either now would reopen a
closed catalogue for a distinction the existing metadata vocabulary
already expresses — the identical engineering call `ADR-0088` already
made for Document classification.

**`"Test"` (a real, compiled `VerificationActivity` subtype) is never
constructed.** This Work Package's own scope names "Inspection
Operations" explicitly, never "Test Operations" — building a Test
object anyway would be scope expansion with no controlling-instruction
basis. Disclosed directly in `ManufacturingObjectFactoryRegistry`'s own
XML documentation.

**Manufacturing Resources have no dedicated representation distinct
from Tooling/Fixtures beyond `Classification`.** All three are plain
`"Document"` objects; a Resource is not further distinguished by
category, type, or capacity — no such field exists anywhere in the
Documents Domain shape, and inventing one would be the "contract
redesign" this Work Package's own controlling instruction forbids.

## Engineering Judgement Calls Requiring Explicit Ratification

1. **No new Domain-layer container type built for Routings.** Ratified — a plain `ManufacturingOperation` used as a structural parent, its own real children ordered via the existing `IHasBomLine.ItemNumber`, satisfies the scope without any new Domain concept.
2. **`Inspection`/`WorkInstruction` reuse Verification's/Documents' own Property Facet Provider and Workspace View types directly, constructed with a different `Kind` string, rather than duplicating equivalent Manufacturing-specific versions.** Ratified — both types were already generic over their own `Kind` parameter; duplicating them would be exactly the "duplicate framework" this Work Package's own controlling instruction forbids. Verified genuinely correct, not merely assumed compatible, by dedicated tests constructing each provider with the Manufacturing Kind string and asserting real facet output.
3. **Manufacturing's own eight commands are not reused from Documents/Verification, despite the read-side reuse above.** Ratified — Command Palette category clarity (a Manufacturing object showing a `"Documents"`/`"Verification"` category would mislead), and neither existing factory can construct a `"WorkInstruction"`/`"Inspection"` at all without Manufacturing-specific required fields their own constructors never accept.
4. **No constructor dependency on `EngineeringVerificationWorkspaceSampleModule`.** Ratified — caught during implementation planning, before any code was written: that module's own id sorts after this Work Package's own sample module id, so the dependency the initial plan listed would have been a genuine `ModuleLifecycleManager` ordering defect, not merely unneeded. Corrected before implementation began; see Lessons Learned.
5. **`ManufacturingKpiCards`'s own two coverage cards do not reuse `EngineeringCockpit.FormatCoverage`.** Ratified — that shared helper's own zero-denominator text is hardcoded Requirements-specific, a pre-existing minor inaccuracy out of this Work Package's own scope to fix; formatting locally avoids compounding it with a third, equally-inaccurate instance.

## Verdict

**No Release Blocking findings.** Every acceptance criterion is met;
every engineering judgement call above is ratified with its own
recorded reasoning; the controlling instruction's own `WP 9.6A`–`WP 9.8A`
skip is recorded plainly, not silently absorbed; the pre-existing
`FormatCoverage` inaccuracy found during this Work Package is disclosed
rather than either silently fixed (out of scope) or silently ignored.

## Related Documents

`WP9.5A Implementation Report.md`; `ADR-0091`; `WP9.5A Architecture
Conformance Review.md`; `WP9.5A Technical Debt Assessment.md`.
