# WP 9.0B — Product Configuration & BOM Management

> This file satisfies `WP 9.0B`'s own two named Academy deliverables —
> "Academy Concept Guide" and "Academy Implementation Retrospective" — as
> two clearly headed parts within one file, mirroring
> `WP9.0A-mechanical-product-structure.md`'s own identical, disclosed
> documentation-structure decision, preserving this folder's own
> established one-file-per-Work-Package convention.

# Part I — Concept Guide

## 1. Introduction

`WP 9.0B` is `v0.9.0`'s own second Work Package — Bill of Materials and
Configuration Management over the Mechanical Product Structure `WP 9.0A`
delivered. Where `WP 9.0A` proved the Kind-keyed Workspace extension
model and the additive-facet Domain model each generalise to real use,
`WP 9.0B` proves both generalise a *second* time, with a smaller, more
targeted set of additions than the first.

## 2. Purpose

To give a Part, Sub-Assembly, or Component a real place in a Bill of
Materials — a quantity, a unit, a find number, an item number, a
reference designator — and to give the platform's own already-real
Configuration/Baseline/Release concepts (`WP8.2C`) their first real
Workspace presentation and their first real use of two extension points
that had sat unused since they were built.

## 3. Background

By the time this Work Package began, `Configuration`/`Baseline`/
`Release` already existed as real, tested `WP8.2C` concrete classes,
completely unpresented anywhere in the Workspace. `ValidationRuleSet`
had a `Register` method whose own documentation described exactly the
extension `WP 9.0B` needed, never called by anything but a test.
`IReferenceIntegrityChecker.CheckBaselineMembersAsync` existed, real and
tested, never called by anything outside a test either. Three ready
extension points, zero real consumers.

## 4. The Problem

Two distinct problems again, echoing `WP 9.0A`'s own shape:

**Presentation and wiring.** Surfacing already-real data and already-
real validation through the Workspace — genuinely mechanical work.

**A new kind of data with nowhere to live.** Quantity, Unit of Measure,
Find Number, Item Number, Reference Designator describe a child's own
*usage* under its current parent — a concept that did not exist
anywhere in the Domain, frozen or otherwise, before this Work Package.

## 5. The Design

`IHasBomLine` — a fourth facet in the `ADR-0080` composition family,
composed into the same four Kinds `IHasParent` already reaches. Its own
five fields are structural metadata, mutated in place via
`SetBomLineAsync`, never triggering a new revision — the same reasoning
`IRenamable` already established for `DisplayName`. Unit of Measure is a
plain string, not `Tempest.Core.UnitsAndQuantities.Quantity<TDimension>`
— a deliberate non-reuse, reasoned through in `ADR-0083`, not an
oversight.

The existing Product Structure tree became the BOM hierarchy by
extension, not replacement: `MechanicalProductStructureNodeProvider`'s
own node-title construction gained a BOM-aware prefix, and its own
sibling ordering gained an Item-Number-aware sort, both additive to code
that already existed.

## 6. Alternatives Considered

**BOM data on the parent, as a list (mirroring `ConfigurationMember`)**
— considered and rejected; would duplicate `IHasParent.ParentId`'s own
already-live source of truth, risking the two falling out of sync after
a `Move`. See `ADR-0083` §Alternatives Considered.

**`Quantity<TDimension>` for Unit of Measure** — considered and
rejected; a category mismatch between calculation-grade dimensional
safety and BOM display. See `ADR-0083`.

**A guided Configuration Management workflow** — considered and
rejected as out of scope; the WP's own controlling instruction named no
such workflow, and direct `EngineeringObjectFactory<T>`/`TransitionAsync`
calls already satisfy every named scope item.

## 7. Why This Solution Was Chosen

Every alternative either duplicated an already-live source of truth or
imported complexity a display-only need never asked for. The chosen
design costs nothing extra for any Kind that does not need it, and
proves — for a fourth time — that `ADR-0075`'s original composition
decision keeps paying for itself as the Domain grows.

## 8. Architectural Principles

**An extension point built ahead of need is only proven once used.**
`ValidationRuleSet.Register`/`CheckBaselineMembersAsync` were both
correct, complete, and completely unused for two Work Packages —
`WP 9.0B` is the first real evidence either was actually built right.

**A BOM is not a second tree.** The instinct to build a dedicated "BOM
view" alongside the Product Structure tree was resisted in favour of
enriching the one tree that already exists — one structure, one source
of truth, decorated, never duplicated.

## 9. Files Added

`src/Tempest.Core/EngineeringDomain/Contracts/BillOfMaterials.cs`;
`src/Tempest.Core/EngineeringDomain/Implementation/BillOfMaterialsValidationRules.cs`;
three new files under `src/Tempest.App/Workspace/Mechanical/`. See
`WP9.0B Implementation Report.md` for the complete list including edited
files.

## 10. Trade-offs

`UnitOfMeasure`/`FindNumber`/`ItemNumber`/`ReferenceDesignator` are
unvalidated free text — accepted, disclosed, `ADR-0083`, in favour of
not forcing a closed vocabulary or a dimensional type system onto a
display-only need before real multi-contributor data ever makes
inconsistency an observed problem.

## 11. Common Mistakes

Assuming an extension point's own existence means it works — both reused
extension points here were correct, but neither had ever been exercised
by real, non-test code before this Work Package; treat "built but
unused" as "unproven," not "done."

## 12. Future Evolution

Product Variants (`FCR-0044`), Unit of Measure canonicalisation
(`FCR-0045`), and a future cost roll-up once Supply Chain gains real
Workspace presentation of its own (`FCR-0046`) are all named, deliberate
non-scope for this Work Package.

## 13. Key Takeaways

The second use of a new architectural pattern is the real test of
whether it generalises — `ADR-0080`'s composition model, and `ADR-0067`'s
Kind-keyed registration, both passed that test a second time this Work
Package, at genuinely lower design cost than their first use.

# Part II — Implementation Retrospective

## What Was Planned vs. What Was Built

The plan called for one new facet, three new commands, and reuse of
everything else. What was built matched that plan exactly, plus two
things the plan did not anticipate: a validation-code collision and a
`ReviseAsync` data-loss bug, both found during implementation and both
fixed, with regression tests, before this Work Package's own
documentation was written — disclosed fully in the Implementation
Report and Technical Debt Assessment rather than silently absorbed.

## Verification Rigour

43 new tests, 1738/1738 passing, across six full clean rebuild-and-test
runs (two Debug, two Release, plus two ad hoc full-suite reruns while
chasing a flaky test down to its actual root cause —
`ConcurrentDictionary` iteration order — rather than accepting a
"probably fine" retry-and-move-on outcome).

## Governance Discipline

One new ADR (`ADR-0083`) records the one genuine new architectural
decision this Work Package made. Two genuine implementation defects were
fixed in place, precisely because neither had ever been part of a commit
or tagged release — the "never silently modify historical records"
principle was applied correctly, not overzealously, by recognising that
uncommitted working-tree code is not yet a historical record at all.

## Retrospective Verdict

The additive-facet model and the platform's own two previously-unused
extension points both proved themselves under real, second-time use.
Building real data (not only unit tests) surfaced a genuine correctness
defect a narrower testing strategy would likely have shipped — the
strongest practical argument yet for this project's own "representative
data, not placeholders" standard being a verification technique in its
own right, not just a presentation nicety.

## Related Documents

`WP9.0B Implementation Report.md`; `WP9.0B Lessons Learned.md`;
`ADR-0083`; `WP9.0A-mechanical-product-structure.md`.
