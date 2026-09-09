# WP 9.0B — Product Configuration & BOM Management — Systems Engineering Review

## Purpose

Reviews `WP 9.0B` from a systems-engineering standpoint: does the
shipped Bill of Materials and Configuration Management integrate
coherently with the platform's own existing Systems Engineering
Foundation, or does it introduce a parallel structure.

## What Product Configuration & BOM Management Now Exists

A user can, inside the real Engineering Workspace: see every Part/Sub-
Assembly/Component's own Quantity, Unit of Measure, Find Number, Item
Number, and Reference Designator, both in the Property Inspector and as
a compact prefix on its own tree node; set or change any of the five via
`SetBomLineCommand`; browse a sibling group ordered by Item Number the
moment every member has one; create a working Configuration, an Approved
Baseline, or a Released Release over any set of objects at their current
revisions; compare two configurations and see exactly what was added,
removed, or revised between them; and check a Baseline/Release's own
internal consistency (every member exists, at the revision it claims).

## Confirms Rather Than Redesigns

- **Reuses `ADR-0080`'s own additive-facet pattern a fourth time**
  (`IHasBomLine`) — no new Domain extension mechanism was invented.
- **Reuses `Configuration`/`Baseline`/`Release` exactly as `WP8.2C`
  shipped them** — zero new Configuration Management code exists; this
  Work Package's own entire Configuration Management scope is Workspace
  presentation and one new validation wiring over already-real classes.
- **Reuses `ValidationRuleSet.Register`** — the exact extension point
  its own `WP8.2B`-era XML documentation named for "a future discipline
  module," now used for the first time, by the second consecutive
  Mechanical Work Package, not a new validation mechanism.
- **Reuses `IReferenceIntegrityChecker.CheckBaselineMembersAsync`** —
  real since `WP8.2C`, reachable from the Workspace for the first time,
  never reimplemented.
- **Reuses the existing Product Structure tree as the BOM hierarchy** —
  no second, parallel "BOM view" was built; the same
  `MechanicalProductStructureNodeProvider` the Explorer already used
  gained BOM-aware titles and sorting, nothing more.

## What Remains Outside This Work Package's Own Scope

Product Variants remain placeholder architecture only, per the WP's own
explicit instruction — no real variant resolution exists. No
configuration management *workflow* (a guided, multi-step create-review-
approve-release process) exists — Baseline/Release creation is direct,
mirroring every other Kind's own `Create`. No cost roll-up, no
make/buy designation, no supplier-quantity reconciliation against
`PurchaseItem` (`WP8.2C`, already a real Kind, still not Workspace-
presented). All are candidates for a future Work Package, not gaps in
this one's own delivery.

## Verdict

**Sound.** Product Configuration & BOM Management integrates by reuse
throughout, exactly as `WP 9.0A` did — one new additive facet, zero new
mechanisms, two pre-existing-code defects found and fixed rather than
compounded. The additive extension model (`ADR-0075`, `ADR-0080`,
`ADR-0083`) has now been proven across four facets and two consecutive
Work Packages without a single frozen contract being reopened.

## Related Documents

`WP9.0B Implementation Report.md`; `ADR-0075`; `ADR-0080`; `ADR-0083`;
`WP9.0A Systems Engineering Review.md`; `WP8.2A Engineering Domain
Architecture.md`.
