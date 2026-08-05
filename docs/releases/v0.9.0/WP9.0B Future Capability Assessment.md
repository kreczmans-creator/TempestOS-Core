# WP 9.0B — Product Configuration & BOM Management — Future Capability Assessment

## Purpose

Records candidate future capabilities this Work Package's own
implementation surfaced but deliberately did not build.

## FCR-0044 — Product Variant Resolution

This Work Package's own controlling instruction named Product Variants
as "placeholder architecture only." The design note in the
Implementation Report describes the shape a future implementation would
take: a named variant axis composing alongside `IHasBomLine`/
`IConfiguration`, resolved at read time by the Workspace layer, never a
second structural tree. **Recommended for a future Work Package**, once
a real, demonstrated need for variant-specific BOM lines exists —
building it speculatively now would be exactly the "no architectural
redesign ahead of real need" this project's own convention warns
against.

## FCR-0045 — Unit of Measure Canonicalisation

`UnitOfMeasure` is free text (`ADR-0083`'s own disclosed trade-off) —
`"EA"` and `"ea"` are different strings today. A small, closed
vocabulary/lookup (not a dependency on `Tempest.Core.UnitsAndQuantities`,
which remains the wrong tool for this — see `ADR-0083`) would let the
Workspace flag an inconsistent unit string without forcing dimensional
typing onto a display-only field. **Recommended once real multi-
contributor BOM data makes inconsistent unit strings a real, observed
problem** — no such data exists yet; the sample module's own units are
all written by one composition root.

## FCR-0046 — Cost Roll-Up Over the BOM Hierarchy

A Part's own unit cost (were one ever tracked — no such field exists
anywhere in the Domain today) times `IHasBomLine.Quantity`, summed
recursively up the tree, is a natural next BOM capability once
`PurchaseItem` (`WP8.2C`, already real, not yet Workspace-presented)
gains real Workspace presentation of its own. **Not recommended before
`FCR-0042`** (a second Engineering Discipline Module, `WP 9.0A`'s own
Future Capability Assessment) — cost data belongs to Supply Chain, not
Product Structure, and forcing it in early would blur that boundary.

## FCR-0047 — Configuration Management Workflow

A guided create → review → approve → release process over a Baseline/
Release, rather than the direct `EngineeringObjectFactory<T>`/
`TransitionAsync` calls this Work Package's own representative data
uses. Explicitly out of this Work Package's own scope ("No configuration
management workflows" — carried forward from `WP 9.0A`'s own identical
constraint). **Not recommended until a real multi-approver release
process is a demonstrated need** — today's direct creation already
satisfies every named scope item (Configuration Items, Baselines,
Released/Working configurations).

## Not Recommended: Validating `UnitOfMeasure` Against `Tempest.Core.UnitsAndQuantities`

Considered directly during `ADR-0083`'s own analysis: rejecting an
unrecognised unit string against `UnitsAndQuantities`'s own known unit
symbols would silently exclude every legitimate non-physical BOM unit
(`"EA"`, "lot", "set") that framework was never meant to represent.
**Not recommended** — `FCR-0045`'s own closed vocabulary, scoped to BOM
units specifically, is the right-sized answer; `UnitsAndQuantities`
itself stays exactly as `ADR-0054` scoped it.

## Verdict

Four candidates recorded (`FCR-0044`–`FCR-0047`); none built
speculatively ahead of genuine need.

## Related Documents

`docs/governance/Future Capability Register.md`; `ADR-0083`; `WP9.0A
Future Capability Assessment.md` (`FCR-0039`–`FCR-0043`); `WP9.0B
Engineering Review Report.md`.
