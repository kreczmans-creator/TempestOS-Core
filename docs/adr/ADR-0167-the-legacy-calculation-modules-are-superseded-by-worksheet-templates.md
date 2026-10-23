# ADR-0167: The Legacy Calculation Modules Are Superseded by Worksheet Templates

## Status

Accepted (v0.27.0, Phase 0). Owner request of 2026-10-23, recorded as D-38 and D-39; the comparison is `docs/reviews/legacy-calculators-review.md`.

## Context

Two calculation surfaces exist after v0.26.0. The legacy one (`Tempest.Core.Calculations`: five original definitions, eleven `WP 21.7A` modules, the v0.16.0 tolerance stack and two thermal modules; `CalculationModulesView` with Re-run and Compare) records named intermediates, never reaches Evidence, throws on invalid input and carries no clause references. The new one (`Tempest.Calc.*`, ADR-0165) shows every step as formula → substitution → result, carries provenance on every input, cites clauses, refuses instead of throwing, and issues through the Evidence lifecycle in one transaction. The owner asked that the lower-quality surface be replaced and the higher one raised where the other was better.

## Decision

1. **Every legacy module becomes a `Tempest.Calc` template** (mapping in the review): six are already covered by shipped templates and gain the legacy outputs they lack; nine are ported; two are retired as covered. Each port carries the legacy module's test vectors as known-answer cases with the same figures and the same five-significant-figure tolerance, so no expected value is lost.
2. **Stored legacy runs stay readable.** A migration reads every `CalculationRecord` (through `CalculationEngine.ListRecordsAsync` and the untyped record document) and writes a `CalcInstance` snapshot: status Draft, inputs with Manual provenance, the intermediates as recorded outputs, the original `ExecutedAt`, principal and revision kept in the snapshot's name and notes. The legacy engine, its persistence and its read-back, retention and corruption tests are kept until the migration passes the Grey upgrade test on real v0.25 and v0.26 databases; only then are the legacy definitions, descriptors, diagrams, the Desktop surface and their tests removed (non-negotiable #3: nothing is deleted to make something pass; the records are migrated, not dropped).
3. **The new surface is raised** where the legacy one was better: every template spec gains an "In plain terms" paragraph and the hand arithmetic of one worked example; the worksheet form fills material, fastener and bearing inputs from released reference records with a `FromReference` pin (the legacy descriptor's `Library` / `SourceProperty` pattern, carried over).
4. **Compare** (two runs side by side) is reimplemented over calc instances of one template before the legacy surface goes.

## Consequences

- The Desktop ends with one calculation surface (Worksheets) and one lifecycle (Evidence); the legacy "Engineering Calculators" node disappears in Phase 4.
- Phase 1 adds a migration step to the upgrade path; a database that holds legacy records opens with the same records as draft worksheets.
- The lean-Markdown rule (D-04, CONTRIBUTING) is kept: the spec prose is counted against the examples' README lines already in the tree.
- Until Phase 4, both surfaces ship; the release notes say which to use (Worksheets).

## Amendment 1 (2026-10-23, v0.27.0 Phase 4): the Calculators surface is retired

Preconditions met: every legacy module has a worksheet template (Phases 1–3); the record migration passes the Grey test on real v0.25.3 and v0.26.0-rc1 databases (`GreyLegacyDatabaseTests`, 46 records of all 19 calculations); Compare is reimplemented over calc instances (`CalcInstanceComparer`); and every template draws its reference diagram in the worksheet form (D-41, `CalcDiagrams`).

Retired: the Desktop `CalculationModulesView` and its coordinator, the Engineering → Calculators node (Worksheets takes its place), `CalculationModuleWorkbench` and `CalculationModuleService`. Kept: the legacy engine, its persistence and read-back tests (the migration reads through them); the fourteen module definitions, their descriptors and vector tests until Phase 5 Green confirms each vector is carried; the form record and the diagram model (`CalculationModuleForm`, `Diagrams/*`), which the reference diagrams and `ModuleGuards` still use.

Tests superseded, each by the test named:

| Retired test(s) | Superseded by |
|---|---|
| `CalculationModulesViewTests` (10: catalogue, generated form, record pickers, calculate, re-run, compare, status) | `CalcSheetViewTests` (catalogue, form, reference pickers, live run, archives, compare), `CompareTests`, `ReferenceFillTests` |
| `CalculationModuleServiceTests` (20: governed run on pinned records, refusals, fills) | `ReferenceFillTests` (released records only, pins, refusals) and each template's known-answer cases |
| `CalculationModuleWorkbenchTests` (7: named runs, re-run, compare) | `CompareTests`, `CalcInstanceServiceTests` (create, revise, register entry) |
| `CalculationDiagramViewTests` (11, on the legacy form) | The same 11 behaviours ported to the worksheet form in `CalculationDiagramViewTests`, plus `CalcDiagramsTests` |
| `DayOneReferenceLibraryTests.TheBeamCalculation_RunsOnTheSeededS355J2…` | `…TheSeededS355J2_SuppliesEAndFy_ToAReferenceFill…` and `DayOneReleaseAtSeedJourneyTests` (beam worksheet on S355J2: 3.96825 mm, unchanged) |
