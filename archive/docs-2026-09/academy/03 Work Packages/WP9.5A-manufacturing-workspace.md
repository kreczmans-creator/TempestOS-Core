# WP 9.5A — Manufacturing Workspace

> This file satisfies `WP 9.5A`'s own two named Academy deliverables —
> "Academy Concept Guide" and "Academy Retrospective" — as two clearly
> headed parts within one file, mirroring
> `WP9.3A-verification-management-workspace.md`'s own identical,
> disclosed documentation-structure decision, preserving this folder's
> own established one-file-per-Work-Package convention.

# Part I — Concept Guide

## 1. Introduction

`WP 9.5A` is `v0.9.0`'s own seventh Work Package by completion order,
and the sixth real Engineering discipline wired into the Engineering
Workspace, after Mechanical (`WP 9.0A`/`WP 9.0B`), Requirements
(`WP 9.1A`), Calculations (`WP 9.2A`), Documents (`WP 9.4A`), and
Verification (`WP 9.3A`). Its own closing instruction signals it is
intended as the last Engineering Discipline Work Package before release
wrap-up — skipping `WP 9.6A` through `WP 9.8A` entirely and moving
straight to `WP 9.9.0` Release Preparation.

## 2. Purpose

To give the already-real Manufacturing-family Domain classes
(`ManufacturingOperation`/`WorkInstruction`/`Inspection`, `WP 8.2C`) a
complete Workspace presence — a browsable Explorer tree categorised by
Routings/Operations/Supplier Operations/Work Instructions/Inspections; a
Property Inspector showing real facets including Part, Classification,
Status, BOM line, and Digital Thread links; eight commands covering the
full Manufacturing Management lifecycle; real Engineering Cockpit KPIs;
and one representative Routing sequence demonstrating cross-discipline
traceability to Requirements, Mechanical Product Structure, Calculations,
Verification, and Documents — using nothing the Domain layer did not
already provide, and reusing two other disciplines' own existing
Workspace-layer types outright where their own shape already fit.

## 3. Background

By the time this Work Package began, `ManufacturingOperation`,
`WorkInstruction`, and `Inspection` (`WP 8.2C`) existed as compiled,
tested Domain objects, confirmed by direct repository-wide search to
have been instantiated by no sample module or test anywhere — the
identical clean starting point every prior real discipline began from.
`IHasBomLine` (`WP 9.0B`) and `Mechanical.SetBomLineCommand` already
existed and already claimed, in their own documentation, to be
Kind-agnostic; `Verification.RecordVerificationResultCommand` (`WP 9.3A`)
already existed with the identical claim. Neither claim had ever been
exercised against a Manufacturing object before this Work Package.

## 4. The Problem

Three distinct problems, echoing every prior real discipline's own
shape in structure but not fully in answer:

**Presentation and wiring.** Surfacing `ManufacturingOperation`,
`WorkInstruction`, and `Inspection` through the Workspace — familiar by
now, except that two of the three Kinds are themselves subtypes of
`Document`/`VerificationActivity`, already-Workspace-integrated base
types from other disciplines.

**Sequencing.** This Work Package's own scope names "Routings" as a
distinct concept from "Operations" — a Routing is, conceptually, an
ordered list of steps. No such container, or ordering mechanism, exists
in the Domain layer beyond the generic `IHasParent`/`IHasBomLine`
facets every object already carries.

**Deciding whether to build new facet/view providers for
`WorkInstruction`/`Inspection`, or reuse Documents'/Verification's own.**
The instinctive assumption, given every prior Work Package's own
precedent (build one facet provider per Kind, in your own namespace),
was "build Manufacturing-specific versions." Reading
`DocumentsPropertyFacetProvider`'s/`VerificationActivityPropertyFacetProvider`'s
own actual constructor shape first (both already accept an arbitrary
`kind: string`, never hardcoded to their own native Kind) revealed the
assumption was unnecessary.

## 5. The Design

The sequencing problem is answered by `ADR-0091`: a Routing is a plain
`ManufacturingOperation` with `Classification = "Routing"`, used purely
as a structural container; its own real `IHasParent` children
(`Classification = "Operation"`) are its own steps, ordered via the
existing `IHasBomLine.ItemNumber` field — the identical field, and the
identical convention, `MechanicalProductStructureNodeProvider.OrderForBom`
already established for Mechanical BOM lines.

The presentation problem is solved exactly as every prior real
discipline already proved it should be for `ManufacturingOperation`
itself (`ManufacturingNodeProvider`/`ManufacturingWorkspaceViewFactory`/
`ManufacturingOperationPropertyFacetProvider`, mirroring their Documents
counterparts closely), but answered differently for `WorkInstruction`/
`Inspection`: `ManufacturingWorkspaceRegistration` registers
`DocumentsPropertyFacetProvider`/`DocumentsWorkspaceView(Factory)` and
`VerificationActivityPropertyFacetProvider`/
`VerificationActivityWorkspaceView(Factory)` directly, constructed with
`kind: "WorkInstruction"`/`kind: "Inspection"` — zero new facet/view
code for either.

The third problem — whether `SetBomLineCommand`/
`RecordVerificationResultCommand` genuinely work against a Manufacturing
target — is answered empirically: both are dispatched, unmodified,
against live Manufacturing objects in dedicated integration tests, not
merely assumed compatible because their own prior documentation said so.

## 6. Alternatives Considered

**A dedicated `Routing` Domain Kind, or a new sequencing field distinct
from `IHasBomLine.ItemNumber`.** Considered and rejected; both would be
exactly the "contract redesign"/"duplicate framework" this Work
Package's own controlling instruction forbids — see `ADR-0091`'s own
Alternatives Considered section.

**Building `ManufacturingWorkInstructionPropertyFacetProvider`/
`ManufacturingInspectionPropertyFacetProvider` as new, Manufacturing-owned
types, mirroring `ManufacturingOperationPropertyFacetProvider`'s own
shape.** Considered and rejected once `DocumentsPropertyFacetProvider`'s/
`VerificationActivityPropertyFacetProvider`'s own genericity over `Kind`
was confirmed by direct read — building duplicates would produce two
files of code that behave identically to code that already exists and
already works, verified by dedicated tests against the reused
construction.

**Reusing Manufacturing's own commands from Documents/Verification, to
match the read-side reuse above.** Considered and rejected — see the
Implementation Report's own "Commands remain this Work Package's own"
section; Command Palette category clarity and each existing factory's
own inability to construct a `"WorkInstruction"`/`"Inspection"` without
Manufacturing-specific required fields both argue against it.

## 7. Why This Solution Was Chosen

Every alternative either reopened a frozen Domain contract for a
distinction an existing field already expresses (a new Routing Kind or
sequencing field), or duplicated already-generic, already-working code
for no functional gain (new facet/view providers). The chosen design
costs nothing extra Domain-side, proves two other disciplines' own
"Kind-agnostic" claims empirically rather than by assertion, and
establishes a new, disclosed cross-Work-Package reuse pattern future
disciplines can now follow with precedent rather than reinventing it.

## 8. Architectural Principles

**A Kind-keyed provider that is already generic over its own `Kind`
parameter can be reused for an entirely different discipline's own
Kind, if that Kind happens to be a subtype of the provider's own native
base type.** Neither `DocumentsPropertyFacetProvider` nor
`VerificationActivityPropertyFacetProvider` was designed with
Manufacturing in mind — their own genericity was simply already there,
confirmed by direct read, not a coincidence engineered for this Work
Package.

**A documented "already Kind-agnostic" claim about an existing command
is worth verifying empirically before relying on it for a new
discipline, not merely trusting the prior Work Package's own prose.**
`SetBomLineCommand`'s and `RecordVerificationResultCommand`'s own
documentation both already asserted Kind-agnosticism; this Work Package
is the first to actually dispatch either against a foreign Kind and
assert on the result.

**An established "constructor-inject the N prior sample modules"
pattern must be rechecked against the new module's own literal ordinal
Id, not assumed to continue transferring because it held for several
consecutive Work Packages.** Caught during implementation planning, not
by a failing test — see Lessons Learned.

## 9. Files Added

14 new files under `src/Tempest.App/Workspace/Manufacturing/`; 2 new
files under `src/Samples/Tempest.Samples/`; 3 new test files. See
`WP9.5A Implementation Report.md` for the complete list including edited
files.

## 10. Trade-offs

`Classification` remains free text, unvalidated at write time — a
Routing with no real children, or a Supplier Operation with no
`"manufacturedBy"` link, is a real, live, but structurally incomplete
object the platform cannot detect or prevent (`ADR-0091`, mirrors
`ADR-0088`'s own identical, already-accepted trade-off). Manufacturing
Approval State is a status reading, not a governed sign-off record
(`TD-30`, unchanged, now a fourth consumer). `EngineeringCockpit
.FormatCoverage`'s own zero-denominator text remains Requirements-specific
and unfixed at the shared source, worked around locally instead
(`TD-33`, `FCR-0061`). All accepted, disclosed, not silently absorbed.

## 11. Common Mistakes

Assuming the initial implementation plan's own "extend the cross-sample-module
dependency list by one more" continuation (adding
`EngineeringVerificationWorkspaceSampleModule`, mirroring `WP 9.3A`'s
own five-module precedent) would transfer without rechecking the new
module's own literal Id against it. Caught during planning by direct
Id-string comparison, not by a failing test — corrected before any
sample-module code was written.

## 12. Future Evolution

A genuine `Routing`/`SupplierOperation` Domain Kind with structured
fields (`FCR-0060`), parameterising `EngineeringCockpit.FormatCoverage`'s
own empty-state message (`FCR-0061`), and extending
`VerificationService.RecordAsync`'s own `IHasRelationships` linking to
cover Inspection subjects (`FCR-0062`, extending `WP 9.3A`'s own
`FCR-0057`) are all named, deliberate non-scope for this Work Package.

## 13. Key Takeaways

The Kind-keyed Workspace extension model (`ADR-0067`) has now been
proven across six genuinely different Engineering disciplines without a
single frozen Workspace contract being reopened, and without a single
Domain-layer file being edited a fourth consecutive time (after
`WP 9.2A`, `WP 9.4A`, `WP 9.3A`). Equally important: this Work Package
demonstrates that the model's own reuse is not limited to "one provider
per discipline" — a provider already generic over its own Kind
parameter can serve a second, unrelated discipline outright, and an
existing command already generic over its own facet cast can serve a
third Domain Kind its own author never anticipated, both proven
empirically rather than assumed.

# Part II — Implementation Retrospective

## What Was Planned vs. What Was Built

The plan called for a Documents-pattern Workspace layer over
`ManufacturingOperation`, a disclosed decision on how to represent
Routings/Supplier Operations without a new Domain Kind, and
representative Manufacturing data demonstrating Digital Thread
integration across every named node the controlling instruction lists.
What was built matched that plan exactly, with one refinement made
during implementation planning itself, before any code was written: the
initial plan's own fifth cross-sample-module dependency
(`EngineeringVerificationWorkspaceSampleModule`) was dropped once its
own ordinal Id was found to sort after this Work Package's own sample
module — a genuine correction to the plan, not merely an unneeded
addition left in.

## Verification Rigour

54 new tests, 2026/2026 passing, across four full clean rebuild-and-test
runs (two Debug, two Release, via `src/TempestOS.slnx`), plus per-project
Release builds of `Tempest.App`/`Tempest.Samples`. Unlike `WP 9.3A`'s own
verification, which found its one genuine finding (`TD-32`) only after
nine tests failed, this Work Package's own one new finding (`TD-33`) was
found by direct code inspection during design, before any test was
written against it — a data point in its own right about where in the
process a given class of finding tends to surface.

## Governance Discipline

One new ADR (`ADR-0091`) records the one genuine new architectural
decision this Work Package made, confined entirely to the Workspace
layer. One new Technical Debt item (`TD-33`) and three new Future
Capability candidates (`FCR-0060`–`FCR-0062`) disclose every known
limitation directly, none silently absorbed. The controlling
instruction's own `WP 9.6A`–`WP 9.8A` skip is recorded plainly in the
Implementation Report and `PROJECT_STATUS.md` as a plain observation,
not an inconsistency requiring correction.

## Retrospective Verdict

The Kind-keyed Workspace extension model proved itself a sixth time,
this time by demonstrating genuine cross-Work-Package reuse on the read
side for the first time in this project's history — two other
disciplines' own already-shipped facet/view providers served a third
discipline's own Kinds outright, with zero new code, verified by
dedicated tests rather than assumed compatible. Two other disciplines'
own commands, documented as Kind-agnostic since their own original Work
Packages, were proven so empirically for the first time. The one
genuine new finding this Work Package's own implementation surfaced was
caught by design-time inspection, resolved locally, and disclosed as
debt rather than either silently absorbed or used to justify touching
already-shipped Cockpit code outside this Work Package's own scope.

## Related Documents

`WP9.5A Implementation Report.md`; `WP9.5A Lessons Learned.md`;
`ADR-0091`; `WP9.3A-verification-management-workspace.md`;
`WP9.4A-engineering-documents-workspace.md`.
