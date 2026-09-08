# WP 9.2A — Engineering Calculations Workspace

> This file satisfies `WP 9.2A`'s own two named Academy deliverables —
> "Academy Concept Guide" and "Academy Implementation Retrospective" — as
> two clearly headed parts within one file, mirroring
> `WP9.1A-requirements-management-workspace.md`'s own identical, disclosed
> documentation-structure decision, preserving this folder's own
> established one-file-per-Work-Package convention.

# Part I — Concept Guide

## 1. Introduction

`WP 9.2A` is `v0.9.0`'s own fourth Work Package, and the third real
Engineering discipline wired into the Engineering Workspace, after
Mechanical (`WP 9.0A`/`WP 9.0B`) and Requirements (`WP 9.1A`). Where
those two Work Packages proved the Kind-keyed Workspace extension model
against two genuinely different Domain architectures (facet-composed,
and immutable-snapshot service-oriented), `WP 9.2A` proves it a third
way: against a discipline whose Domain object (`Calculation`) and whose
execution engine (`ICalculationEngine`) are two separate, already-real,
already-tested frameworks (`WP 8.2C` and `WP 7.1D` respectively, built a
full release apart) that had simply never been introduced to each other,
or to the Workspace, before.

## 2. Purpose

To give the already-real Calculation Framework a complete Workspace
presence — a browsable Explorer tree showing every registered
Calculation Template, every Calculation Set, and every Calculation; a
Property Inspector showing real facets including Latest Result, Safety
Factor, and Digital Thread links; ten commands covering the full
Calculation Management lifecycle; real Engineering Cockpit KPIs; and
five representative, real engineering calculations demonstrating
cross-discipline traceability — using nothing the Domain layer did not
already, or could not additively, provide.

## 3. Background

By the time this Work Package began, `Tempest.Core.Calculations`
(`WP 7.1D`) was a complete, tested, but Workspace-invisible framework:
register a definition, execute it, get back a durable, evidentiary
`CalculationRecord<TResult>` — all correct, all unreachable from the
Engineering Workspace a user actually opens, and demonstrated only by
one trivial sample calculation (`DoubleLengthCalculationDefinition`)
that deliberately proved dispatch, not engineering. `Calculation`/
`CalculationSet` (`WP 8.2C`) existed as Domain objects, but — unlike the
five already-Implemented canonical Kinds `WP 8.2C` explicitly gave no
competing concrete class — had never been constructed by anything, nor
connected to the execution framework that shared their own name.

## 4. The Problem

Three distinct problems, the first two echoing all three prior real-discipline
Work Packages' own shape, the third genuinely new to this one:

**Presentation and wiring.** Surfacing `Calculation`/`CalculationSet`
through the Workspace, by now a familiar pattern.

**Two frameworks, never previously connected, needing to work together
through the Workspace.** `ICalculationEngine` executes by a `string`
Id with generic `TInput`/`TResult`; `Calculation` is a Domain object
with a `Guid` identity and no execution capability of its own. Nothing
in either framework's own design linked the two — this Work Package had
to invent the linking mechanism, in the one layer both frameworks are
already visible to (`Tempest.App`), without changing either framework.

**A single Workspace command must dispatch an open-ended, growing set of
differently-typed calculations.** Unlike Mechanical's eight or
Requirements' three fixed Kinds, "Calculation Template" is not a closed
set at all — new ones are meant to be added by future modules, each with
its own, different `TInput`/`TResult`. A Workspace command's own shape is
necessarily fixed at compile time; the set of things it must dispatch to
is not.

## 5. The Design

`CalculationTemplateRegistry` (`ADR-0086`) solves the third problem
directly: a small, `Tempest.App`-only, JSON-marshalling type-erasure
adapter, one instance per registered Template, all reachable through one
non-generic `Dictionary<string, ICalculationTemplateAdapter>`. This
simultaneously solves the second problem — the adapter's own
`ExecuteAsync` is the one place that calls `ICalculationEngine.ExecuteAsync<TInput,TResult>`
*and* then links the resulting record back to a real `Calculation`
Domain object via `LinkAsync(record.Id, "calculatedBy")` — the two
frameworks meeting, for the first time, entirely inside a new, additive
Workspace-layer type, neither framework itself aware the other exists.

The first problem is solved exactly as `WP 9.0A`/`WP 9.1A` already
proved it should be: `CalculationsNodeProvider`/
`CalculationsWorkspaceViewFactory`/`CalculationsPropertyFacetProvider`
mirror their Mechanical/Requirements counterparts' own shape closely,
extended by one further, synthetic Kind (`"CalculationTemplate"`) for
content with no Domain identity at all — proof the Kind-keyed extension
model generalises even that far.

"Calculation Approval State" and "Safety Factors" — two scope items with
no dedicated Domain contract — are represented through the Calculation
Framework's own already-general shapes (`IHasLifecycle.Status`, a named
`CalculationIntermediateResult`) rather than through invented new types
(`ADR-0087`).

## 6. Alternatives Considered

**One hand-written Workspace command per Calculation Template** —
considered and rejected; would not scale past this Work Package's own
five representative calculations, and would require touching
`Tempest.App` every time a future module adds a sixth. Rejected in
favour of the one generic adapter registry.

**A Domain-layer "list every registered Template" method on
`ICalculationEngine`** — considered and rejected; `WP8.2B Dependency
Rules.md` §8 proposes no registry contract, and adding one to serve only
the Workspace's own display need would be exactly the kind of Domain
contract change this Work Package's own controlling instruction
forbids. The Workspace-layer registry already knows every Template it
itself registers — no Domain-layer enumeration was needed.

**A new, dedicated `ISafetyFactor`/`IApprovalRecord` Domain contract** —
considered and rejected for both concepts; the Calculation Framework's
own already-general shapes express both honestly, and inventing new
types would be "contract redesign," not integration.

## 7. Why This Solution Was Chosen

Every alternative either would not scale, would reopen a Domain contract
explicitly out of scope, or would invent new Domain state to serve one
Work Package's own display need alone. The chosen design — one
Workspace-layer, JSON-marshalling adapter registry, connecting two
already-real frameworks without changing either — costs nothing extra
Domain-side, scales to any future Template without touching this Work
Package's own code again, and proves the Kind-keyed Workspace extension
model generalises to synthetic, non-`IEngineeringObject` content, not
only to real Domain objects.

## 8. Architectural Principles

**Two independently-correct frameworks do not need to know about each
other to be connected — a third, thin layer can do it.** Neither
`ICalculationEngine` nor `Calculation` changed; `CalculationTemplateRegistry`
is the entire connection, and it lives where both are already visible.

**Type erasure, once solved well, tends to need solving again one layer
up.** `CalculationEngine`'s own boxed-`object` dispatch (`ADR-0056`)
and `CalculationTemplateRegistry`'s own JSON-marshalled dispatch are the
same technique, applied twice, at two different layers, for the same
underlying reason: a caller with only a runtime string Id cannot carry a
compile-time generic type argument.

**An honest mapping onto an existing general shape beats a precise new
type built under time pressure.** Safety Factor and Approval State both
had "invent a new type" available as an option; both were represented
through existing, general, already-battle-tested shapes instead, with
the mapping disclosed rather than hidden.

## 9. Files Added

18 new files under `src/Tempest.App/Workspace/Calculations/`; 3 new
files under `src/Samples/Tempest.Samples/`; 3 new test files. See
`WP9.2A Implementation Report.md` for the complete list including edited
files.

## 10. Trade-offs

Recalculate requires fresh input every time — the Framework's own
stored `CalculationRecord` never retained the input that produced it
(`TD-29`, `FCR-0053`). Evidence composition (`ITraceable.GetEvidenceAsync`)
remains structurally empty for every Calculation, worked around by
reading relationships directly (`TD-30`, `FCR-0051`). Approval State is
a status reading, not a governed sign-off record (`TD-30`, `FCR-0052`).
All three accepted, disclosed, not silently absorbed.

## 11. Common Mistakes

Trusting representative engineering numbers to "look plausible" without
computing the governing formula by hand — the Beam Bending Stress
calculation's own first-drafted load/length combination produced a
stress roughly four times the allowable, accidentally turning a
calculation meant to demonstrate "awaiting review" into an unintended
second "Failed" demonstration. Caught by hand-computing the formula
while reviewing the Cockpit KPI test assertions, corrected before any
test was written against the wrong expectation.

## 12. Future Evolution

Concrete `ICalculationResult`/`IVerificationResult` implementations
(`FCR-0051`), a governed Approval/Review workflow (`FCR-0052`), and
Recalculate resuming from a stored input (`FCR-0053`) are all named,
deliberate non-scope for this Work Package.

## 13. Key Takeaways

The Kind-keyed Workspace extension model (`ADR-0067`) has now been
proven across three genuinely different situations — a facet-composed
Domain architecture, an immutable-snapshot service architecture, and
(this Work Package) synthetic, non-Domain content bridging two
previously-unconnected frameworks — without a single frozen Workspace
contract being reopened, and without a single Domain-layer file being
edited. The strongest evidence yet that "integrate, don't redesign" is
achievable exactly as written, not merely aspirational.

# Part II — Implementation Retrospective

## What Was Planned vs. What Was Built

The plan called for a Mechanical-pattern Workspace layer over
`Calculation`/`CalculationSet`, a new type-erasure adapter connecting it
to `ICalculationEngine`, and five representative engineering
calculations demonstrating Digital Thread integration. What was built
matched that plan exactly, with zero Domain-layer changes required —
the one respect in which this Work Package's own implementation needed
less structural improvisation than either `WP 9.0A`/`WP 9.0B` (which
each added real Domain facets) or `WP 9.1A` (which added seven Domain
service methods and fixed two genuine defects). The one correction made
during implementation was narrative, not architectural: the Beam Bending
Stress representative calculation's own sample input was recomputed by
hand and adjusted before being exercised by any test, so the
representative data would tell the intended Cockpit KPI story clearly.

## Verification Rigour

57 new tests, 1865/1865 passing, across four full clean rebuild-and-test
runs (two Debug, two Release, via `src/TempestOS.slnx`), plus per-project
Release builds of `Tempest.App`/`Tempest.Samples`. Unlike `WP 9.0B`'s
`ReviseAsync` finding and `WP 9.1A`'s permission-gated-read finding, this
Work Package's own verification surfaced no genuine defect in already-real
code — consistent with its own unusually narrow footprint (zero
Domain-layer files touched).

## Governance Discipline

Two new ADRs (`ADR-0086`, `ADR-0087`) record the two genuine new
architectural decisions this Work Package made, both confined entirely
to the Workspace layer. Two new Technical Debt items (`TD-29`, `TD-30`)
and three new Future Capability candidates (`FCR-0051`–`FCR-0053`)
disclose every known limitation directly, none silently absorbed.

## Retrospective Verdict

The Kind-keyed Workspace extension model proved itself a third time,
this time against synthetic content with no Domain identity at all, and
against the novel problem of connecting two already-real, previously-
unconnected frameworks — without reopening a single frozen contract, and
without editing a single Domain-layer file. Building real, representative
engineering calculations — not placeholder arithmetic — surfaced one
genuine narrative-correctness issue (an accidentally-overstressed sample
beam) a purely mechanical "does it compile and pass" verification would
never have caught, reinforcing `WP 9.0B`'s and `WP 9.1A`'s own shared
lesson that representative data earns its keep as a verification
technique, not only as a presentation nicety.

## Related Documents

`WP9.2A Implementation Report.md`; `WP9.2A Lessons Learned.md`;
`ADR-0086`; `ADR-0087`; `WP9.0A-mechanical-product-structure.md`;
`WP9.1A-requirements-management-workspace.md`.
