# WP 9.2A — Engineering Calculations Workspace — Lessons Learned

## Purpose

Records what went well, what was harder than expected, and what a
future Work Package facing a similar situation should know going in.

## What Went Well

**The Kind-keyed Workspace extension model generalised a third time,
including to content with no Domain identity at all.** `ADR-0067`'s own
registration pattern already proved itself against a facet-composed
architecture (Mechanical) and an immutable-snapshot service architecture
(Requirements). Calculation Templates are neither — they are plain,
in-memory, module-registered objects with no `Guid`, no `IEngineeringDocument`,
no revision history. `CalculationsNodeProvider`/`CalculationsWorkspaceViewFactory`/
`CalculationsPropertyFacetProvider` still slotted them in cleanly, as a
third, synthetic `"CalculationTemplate"` Kind alongside the two real
Domain Kinds, using nothing more than a registry-local `Guid` for
addressing. Strong, now three-times-proven evidence the abstraction
boundary (`string Kind` + `Guid` object identity, nothing else assumed)
was drawn in the right place.

**The Calculation Framework's own type-erasure precedent (`ADR-0056`)
extended cleanly one layer up.** `CalculationEngine` already solved "how
do you dispatch by a runtime string Id when every definition has a
different compile-time `TInput`/`TResult`" for itself, internally.
`CalculationTemplateRegistry` needed to solve the identical problem one
layer higher, for the Workspace's own single Execute/Recalculate command
— and the same answer (a type-keyed map of boxed, generic adapters)
applied directly, with no new technique invented.

**Zero Domain-layer changes.** Every prior real-discipline Work Package
(`WP 9.0A`/`WP 9.0B`/`WP 9.1A`) added at least one new Domain-layer
member — new facets, new service methods, new enum values. This one
added none: `Calculation`/`CalculationSet`/`ICalculationEngine` were
already sufficient, once the Workspace layer knew how to read and
dispatch them generically. The strongest possible confirmation that this
Work Package's own controlling instruction ("integrate, don't redesign")
was achievable as written, not merely aspirational.

## What Was Harder Than Expected

**Deciding what "Calculation Template" even means, with no existing
Workspace precedent for content that has no `Guid`.** Every prior
Kind-keyed provider (Mechanical, Requirements) roots its own tree in
real `IEngineeringObject`s or their own service-layer equivalent.
Templates are neither — `ICalculationEngine` has no "list every
registered definition" method (and adding one would be exactly the kind
of Domain-contract change this Work Package's own instruction forbids).
Resolved by keeping the Template catalogue entirely at the Workspace
layer (`CalculationTemplateRegistry`, populated by
`CalculationsWorkspaceRegistration` reading each definition's own
`Metadata` directly, mirroring how `ICommandRegistry`'s own descriptors
are a Workspace/App-layer catalogue over the Command Framework, never a
Domain-layer one).

**"Calculation Approval State" and "Safety Factors" both name concepts
with no dedicated Domain contract.** Both required a deliberate choice
between three options: invent a new contract (forbidden by this Work
Package's own controlling instruction), silently omit the scope item, or
represent the concept honestly through an existing, open, generic shape.
The third was chosen for both — `IHasLifecycle.Status` for Approval
State, a named `CalculationIntermediateResult` for Safety Factor — and
disclosed explicitly rather than left implicit, in both the Implementation
Report and `TD-30`. Worth remembering: "no contract redesign" sometimes
means the honest answer to a named scope item is "here is how the
existing framework's own general-purpose shape already expresses this,"
not "here is the new type that expresses this precisely."

**Picking representative calculation numbers that stay internally
consistent.** The Beam Bending Stress calculation's own first-drafted
sample input (a large aircraft-scale load over a short length) produced
a bending stress roughly four times the allowable — accidentally turning
a calculation intended to demonstrate "awaiting review" into a second,
unintended "Failed" demonstration, muddying the Cockpit KPI story the
representative data was built to tell clearly. Caught by directly
computing the formula by hand while reviewing the Cockpit KPI test
assertions, not by a failing test (the calculation is a genuinely valid
Conditional outcome either way — nothing was incorrect, only
narratively confusing). Corrected to a smaller, more realistic load.
Worth remembering: representative data that is meant to demonstrate one
specific KPI value cleanly should have its own arithmetic checked by
hand, not only trusted to "look plausible."

## Process Observations

Unlike `WP 9.0B`'s `ReviseAsync` finding and `WP 9.1A`'s permission-gated-read
finding, this Work Package's own implementation surfaced no genuine,
pre-existing defect in already-real code — every disclosed gap
(`TD-29`, `TD-30`) is a pre-existing absence (a capability that was never
built), not a bug in a capability that was. Consistent with this Work
Package's own unusually narrow footprint: touching zero Domain-layer
files leaves zero surface for a Domain-layer regression to hide in.

## Recommendation for Future Work Packages

When a scope item names a concept the Domain layer has no dedicated
contract for, resist inventing one under the pressure of "the
instruction lists it as a first-class item" — first check whether the
existing framework's own already-general shapes (an open string
category, a named intermediate result, a status enum) already express it
honestly. If they do, use them and disclose the mapping explicitly, the
same way `RequirementsKpiCards`' "Released→Satisfied" mapping and this
Work Package's own "Safety Factor→named intermediate result"/"Approval
State→LifecycleState" mappings were both disclosed rather than left for
a future reader to discover by surprise.

## Related Documents

`WP9.2A Implementation Report.md`; `WP9.2A Technical Debt Assessment.md`
(`TD-29`, `TD-30`); `ADR-0086`; `ADR-0087`; `WP9.0B Lessons Learned.md`;
`WP9.1A Lessons Learned.md`.
