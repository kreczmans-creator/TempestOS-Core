# WP 9.0A — Mechanical Product Structure

> This file satisfies `WP 9.0A`'s own two named Academy deliverables —
> "Academy Concept Guide" and "Academy Implementation Retrospective" — as
> two clearly headed parts within one file, preserving this folder's own
> established one-file-per-Work-Package convention rather than inventing
> a new two-file split with no prior precedent. Disclosed explicitly, not
> silently substituted; see `WP9.0A Implementation Report.md`.

# Part I — Concept Guide

## 1. Introduction

`WP 9.0A` is `v0.9.0` ("Mechanical Foundation")'s own first Work
Package — the first to wire a real Engineering Discipline into the real
Engineering Workspace. Every prior Workspace Work Package (`WP8.0A`–
`WP8.1C`) built the shell, the navigation, the Cockpit, and the
extension points against fixed, fictional sample content; every prior
Engineering Domain Work Package (`WP8.2A`–`WP8.2C`) built real,
compiled, tested canonical objects with nothing yet presenting them.
This Work Package connects the two for the first time.

## 2. Purpose

To prove that the Kind-keyed Workspace extension model (`ADR-0067`) and
the composed-facet Domain model (`ADR-0075`) both generalise to real
use — not merely to the sample content each was originally proven
against — and to give a user something genuinely new: the ability to
browse, inspect, and structurally edit a real mechanical product
hierarchy inside the running platform.

## 3. Background

By the time this Work Package began, `Project`/`Assembly`/`SubAssembly`/
`Part`/`Component`/`Configuration` already existed as real, tested
`WP8.2C` concrete classes — a fact the Canonical Object Catalogue's own
"Conceptual" marking for all six had grown stale about. Separately, the
Workspace's own `IProjectExplorerNodeProvider`/`IWorkspaceViewFactory`/
`IWorkspaceCommand`/`IPropertyInspector` all existed, each proven only
against `Workspace/Samples`' own fixed, fictional tree. Two halves of the
same eventual capability, neither yet touching the other.

## 4. The Problem

Two distinct problems, not one:

**Presentation.** Real Domain data needed to reach the Explorer, the
object view, and the Property Inspector — a matter of implementing the
already-defined extension points against real reads, genuinely
mechanical work, not a design problem.

**Mutation.** Rename, Delete, and Move do not exist anywhere in the
frozen Domain. `IHasBusinessIdentifier.DisplayName` has no setter;
`IEngineeringDocumentStore` has no delete or unlink; nothing before this
Work Package ever needed to reparent an object after construction. This
was the real design problem: how to add genuinely new capability to a
platform whose own explicit governing rule for this Work Package was "no
contract redesign."

## 5. The Design

**Presentation** reuses the Kind-keyed provider model exactly:
`MechanicalProductStructureNodeProvider`/`MechanicalWorkspaceViewFactory`
implement the two existing provider interfaces; a third,
`IPropertyFacetProvider`, is added following the identical shape (see
§8, Architectural Principles).

**Mutation** is three new, small, individually composable facet
interfaces — `IRenamable`, `IHasParent`, `IDeletable` — added to
`Tempest.Core.EngineeringDomain` and composed only into the five Kinds
that need them. `EngineeringObjectBase` implements all three
unconditionally, exactly as it already implements every other facet.
`MoveAsync` records a *new* `"groupedUnder"` relationship link to the new
parent rather than removing the old one — the append-only Relationship
framework becomes a full move history, while a new, separate `ParentId`
field is the one live value anything renders from. `DeleteAsync` is
soft-delete only, and refuses to delete an object with live children.

Six new Workspace commands (`IWorkspaceCommand`/`ICommand`) — Create,
Rename, Delete, Move, Copy, Duplicate — give the Command Framework its
first real, non-sample implementations, closing `WP8.1B`'s own disclosed
"no concrete `IWorkspaceCommand` is implemented" gap.

## 6. Alternatives Considered

**Workspace-layer-only mutation, no Domain change** — explored and found
unbuildable: Rename has no backing field to change; Delete has no
Domain concept to represent it, and misusing `LifecycleState` would
apply a structural fact to a shared, platform-wide vocabulary meant for
a different purpose; Move has no live pointer to update. See `ADR-0080`.

**A `Deleted` `LifecycleState` member** — rejected; `LifecycleState` is
frozen and shared by every canonical Kind, most of which (a Requirement,
a Risk) will never have a Product-Structure-style parent/child
relationship for a "has live children" guard to apply to.

**A single, unified `IStructuralObject` interface** rather than three
small facets — rejected; would reintroduce the large, kitchen-sink
interface shape `ADR-0075` already rejected in favour of small,
independently composable ones.

## 7. Why This Solution Was Chosen

Every alternative either reopened a genuinely frozen contract, or
represented one concept as another it does not actually mean. The
chosen design extends nothing by force: `ADR-0075`'s own composition
model already anticipated exactly this shape of extension, and every one
of the ~30 already-shipped Kinds that does not need Rename/Move/Delete
is completely unaffected by their existence.

## 8. Architectural Principles

**Composition over reopening.** A capability gap in a frozen contract is
filled by a new, small, independently composable facet — never by
adding a member to an existing interface's own already-frozen shape.

**Kind-keyed extensibility generalises.** The same
registration-dictionary-plus-`TryAdd` shape that already served two
provider categories (`ADR-0067`) served a third (`ADR-0082`) with no new
design, only a new interface following the same shape.

**Append-only history, always.** Every mutation this Work Package adds —
Rename (implicitly, since `DisplayName` itself is never itself
versioned, unlike Revisions), Move, Delete — either preserves the
platform's own existing append-only guarantee directly, or (Delete)
deliberately never removes anything.

## 9. Files Added

`src/Tempest.Core/EngineeringDomain/Contracts/StructuralMutation.cs`;
twelve files under `src/Tempest.App/Workspace/Mechanical/`;
`src/Tempest.App/Workspace/IPropertyFacetProvider.cs`;
`src/Samples/Tempest.Samples/MechanicalProductStructureSampleModule.cs`;
`src/Samples/Tempest.Samples/MechanicalWorkspaceExplorerModule.cs`. See
`WP9.0A Implementation Report.md` for the complete list including edited
files.

## 10. Trade-offs

`IAssembly.ChildIds`/`ISubAssembly.ParentAssemblyId` (frozen,
construction-time snapshots) are now honestly stale the moment an object
moves — accepted, disclosed, `ADR-0081`, rather than reopening either
interface. All six Mechanical commands omit `createDefault`, trading
Command Palette "invoke by bare Id" convenience for not pretending a
meaningless parameterless default exists.

## 11. Common Mistakes

Assuming a Domain capability gap always means a contract redesign is
needed — usually, as here, a new, narrowly-scoped facet composed only
where needed is enough, and is far cheaper to review and reason about.
Assuming "the tree needs a breadcrumb, therefore new code is needed" —
`WP8.1B`'s own `ProjectExplorer.CurrentPath`/`WorkspaceShell.BuildBreadcrumb`
already provide this generically; check for an existing generic
mechanism before building a Kind-specific one.

## 12. Future Evolution

A second Engineering Discipline Module reusing the same three provider
categories and the same three structural-mutation facets would confirm
this Work Package's own patterns generalise rather than being an
accidental one-off (`FCR-0042`/`FCR-0043`). Multi-selection and
drag-and-drop remain genuinely blocked on, respectively, a frozen
single-selection contract and a terminal-only presentation choice
(`FCR-0039`/`FCR-0040`).

## 13. Key Takeaways

A "frozen" contract is not immovable — it is a contract nothing may
*reopen* without a recorded reason; additive, disclosed, ADR-backed
extension is how this platform has always grown its own Domain, and
this Work Package is simply the first to need three new facets at once
rather than one. Reuse is the default; a new mechanism is the last
resort, not the first idea.

# Part II — Implementation Retrospective

## What Was Planned vs. What Was Built

The plan called for exactly this shape — additive Domain facets, three
Kind-keyed providers, six Workspace commands, representative data
demonstrating every new capability at least once. What was actually
built matched the plan closely; the one adjustment made during
implementation was disclosed directly rather than silently absorbed:
`Duplicate` could not be exercised inside the sample data's own seeding
(`Tempest.Samples` cannot reference `Tempest.App`, where `Duplicate`'s
own command lives) — covered instead by dedicated Workspace command
tests, a layering-driven correction, not a scope reduction.

## Verification Rigour

64 new tests, 1695/1695 passing, across four full clean rebuild-and-test
runs (two Debug, two Release) — matching this project's own established
release-readiness bar (`WP8.9.0`). One genuine, pre-existing platform
finding (`TD-26`) was discovered during manual console verification and
confirmed, not assumed, pre-existing via a disposable second checkout at
the unmodified `v0.8.0` tag before being disclosed rather than silently
patched.

## Governance Discipline

Three new ADRs (`ADR-0080`–`ADR-0082`) record every deviation from a
frozen contract at the moment it was made, not retrospectively. One
historical-document correction (the Canonical Object Catalogue's own
stale "Conceptual" marking) was disclosed rather than silently edited in
place, per this project's own "never silently modify historical
records" convention.

## Retrospective Verdict

The additive-facet extension model proved itself capable of a genuinely
new capability (structural mutation) without a single frozen contract
being reopened — the strongest practical evidence yet that `ADR-0075`'s
own original composition decision was the right one, two full Work
Packages after it was made.

## Related Documents

`WP9.0A Implementation Report.md`; `WP9.0A Lessons Learned.md`; `ADR-0075`;
`ADR-0080`; `ADR-0081`; `ADR-0082`; `WP8.2C-engineering-domain-implementation.md`.
