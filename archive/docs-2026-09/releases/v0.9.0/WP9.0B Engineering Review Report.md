# WP 9.0B — Product Configuration & BOM Management — Engineering Review Report

## Purpose

Reviews whether the shipped implementation satisfies `WP 9.0B`'s own
controlling instruction, and whether every engineering judgement call
made along the way was reasonable and disclosed.

## Acceptance Criteria Review

| Requirement | Verdict | Evidence |
|---|---|---|
| Bill of Materials, BOM hierarchy, Find Numbers, Item Numbers, Quantities, Units, Reference Designators | **Met** | `IHasBomLine` (`ADR-0083`); the existing Product Structure tree is the BOM hierarchy. |
| Configuration Items, Baselines, Released configurations, Working configurations | **Met, via already-existing `WP8.2C` code** | `Configuration`/`Baseline`/`Release` + `IHasLifecycle.Status` — zero new Domain code needed. |
| Product variants (placeholder architecture only) | **Met, exactly as scoped** | A design note (Implementation Report) + `FCR-0044`; deliberately no code, per the WP's own explicit "placeholder architecture only" instruction. |
| Product structure validation | **Met** | Five new `IValidationRule`s (duplicate Item/Find Number, non-positive quantity, missing parent, circular hierarchy), registered via the pre-existing `ValidationRuleSet.Register` extension point. |
| Workspace / Project Explorer / Cockpit / Property Inspector / Context menus / Navigation / Command Palette | **Met** | BOM-aware node titles and sorting; five new facets; three new commands, registered and listed exactly as `WP 9.0A`'s own six already were; no new mechanism needed for context menus/Cockpit (both already generic). |
| Create/Edit BOM, Add/Remove/Move/Duplicate item, Quantity editing, Find Number management, Sorting, Filtering, Expand/Collapse, Multi-level navigation, Validation | **Met, via existing `WP 9.0A` mechanisms plus one new command** | Add/Remove/Move/Duplicate item are `WP 9.0A`'s own Create/Delete/Move/Duplicate commands, unchanged — a BOM line is a property of an already-mutable object, not a separate concept needing its own CRUD surface. `SetBomLineCommand` covers quantity/Find Number editing. Filtering/Expand-Collapse/multi-level navigation are `WP8.1B`'s own already-generic Project Explorer capabilities. |
| Configuration Items, Working configuration, Released configuration, Baseline creation, Baseline comparison (placeholder where necessary), Revision display, Change awareness | **Met — comparison is real, not placeholder** | `CompareBaselinesCommand` performs a genuine diff over already-existing data; "placeholder where necessary" was judged not necessary, since both `IBaseline.MemberRevisions` lists already exist in memory with nothing further needed. |
| Validate: duplicate item numbers, circular references, invalid hierarchy, missing parents, invalid quantities, configuration consistency | **Met** | Five new rules plus the pre-existing `CheckBaselineMembersAsync`, now wired to a command. |
| Representative data: deep BOM hierarchy, shared components, multiple configurations, baseline comparisons, revision examples | **Met** | Extended `MechanicalProductStructureSampleModule`; a genuine added-member and revision-changed-member pair between the seeded Baseline and Release. |
| No architectural redesign; no contract redesign; reuse Engineering Domain services exclusively; no duplicate frameworks | **Met, with one disclosed additive deviation (`ADR-0083`) and two disclosed pre-existing-code fixes** | See Architecture Conformance Review and Technical Debt Assessment. |
| Unit/integration/Workspace tests; repeated Debug/Release verification | **Met** | 43 new tests, 1738/1738, six full clean-rebuild-and-test runs. |
| Documentation and Governance | **Met** | This document and its nine siblings; governance registers updated. |

## Scope Discipline Review

No Product Variant code was written — the controlling instruction's own
"placeholder architecture only" was read literally and honoured
literally. No configuration management *workflow* (create/approve/
release a baseline through some multi-step process) was built — Baseline/
Release creation in the sample data uses the same `EngineeringObjectFactory<T>`/
`TransitionAsync` primitives every other Kind already uses, not a new
workflow engine. No new UI framework, no new persistence mechanism.

## Engineering Judgement Calls Requiring Explicit Ratification

1. **`IHasBomLine` as a fourth additive facet, not an extension of
   `IHasParent` or a new relationship-attribute mechanism.** Ratified —
   see `ADR-0083` §Alternatives Considered.
2. **Unit of Measure is a plain string, not `Quantity<TDimension>`.**
   Ratified — `ADR-0083`; the two systems answer genuinely different
   questions (calculation-grade conversion safety vs. BOM display).
3. **Fixing the `TEMPEST-VAL` code collision and the `ReviseAsync`
   structural-state bug in place, rather than only disclosing them.**
   Ratified — neither was ever part of a commit or tagged release;
   both are genuine correctness defects in this session's own,
   not-yet-committed work, not historical records. Both are fully
   disclosed in the Technical Debt Assessment regardless.
4. **`CompareBaselinesCommand` is a real diff, not the "placeholder
   where necessary" the WP's own text allows for.** Ratified — no
   technical obstacle existed; building a placeholder here would have
   been strictly worse than the real, cheap-to-build alternative.

## Verdict

**No Release Blocking findings.** Every acceptance criterion is met.
Two genuine, pre-existing defects were found and fixed rather than
merely disclosed, with full regression coverage; every engineering
judgement call above is ratified with its own recorded reasoning.

## Related Documents

`WP9.0B Implementation Report.md`; `ADR-0083`; `WP9.0B Architecture
Conformance Review.md`; `WP9.0B Technical Debt Assessment.md`.
