# WP 9.3A — Verification Management Workspace

> This file satisfies `WP 9.3A`'s own two named Academy deliverables —
> "Academy Concept Guide" and "Academy Implementation Retrospective" — as
> two clearly headed parts within one file, mirroring
> `WP9.4A-engineering-documents-workspace.md`'s own identical, disclosed
> documentation-structure decision, preserving this folder's own
> established one-file-per-Work-Package convention.

# Part I — Concept Guide

## 1. Introduction

`WP 9.3A` is `v0.9.0`'s own sixth Work Package by completion order, and
the fifth real Engineering discipline wired into the Engineering
Workspace, after Mechanical (`WP 9.0A`/`WP 9.0B`), Requirements
(`WP 9.1A`), Calculations (`WP 9.2A`), and Documents (`WP 9.4A`). It
carries the earlier number `9.3A` despite completing after `WP 9.4A` —
`WP 9.2A` closed with "no `WP 9.3A` begins until the Product Owner gives
further instruction," and it was `WP 9.4A` itself that named Verification
as the natural next Work Package (`FCR-0055`), closing the gap its own
predecessor left open.

## 2. Purpose

To give the already-real Verification Framework
(`IVerificationService`/`VerificationService`, `WP 7.1E`) and its
Engineering Domain counterpart (`VerificationActivity`, `WP 8.2C`) a
complete Workspace presence — a browsable Explorer tree categorised by
verification method; a Property Inspector showing real facets including
Subject, Method, Latest Outcome, Latest Criteria/Evidence, and Digital
Thread links; nine commands covering the full Verification Management
lifecycle (including recording a real, evidentiary Pass/Fail/Conditional
result); real Engineering Cockpit KPIs; and four representative,
real Verification Activities demonstrating cross-discipline traceability
to Requirements, Calculations, Mechanical Product Structure, Materials,
Risks, Decisions, and Documents — using nothing the Domain layer or the
Verification Framework did not already, or could not additively,
provide.

## 3. Background

By the time this Work Package began, `VerificationActivity` (`WP 8.2C`)
existed as a compiled, tested Domain object, and `IVerificationService`
(`WP 7.1E`) existed as a compiled, tested Framework — but the two had
never been introduced to each other, and neither had any Workspace
presence. One live Verification record already existed in the platform
(`RequirementsWorkspaceSampleModule`'s own direct `RecordAsync` call
against a Requirement, `WP 9.1A`) — proof the Framework itself worked,
but with no Verification Activity, Explorer area, or Cockpit KPI
anywhere touching it.

## 4. The Problem

Two distinct problems, echoing `WP 9.2A`'s own shape in structure but
not in answer:

**Presentation and wiring.** Surfacing `VerificationActivity` through
the Workspace, by now a familiar pattern.

**Deciding whether a connecting mechanism, mirroring `WP 9.2A`'s own
`CalculationTemplateRegistry`, was needed at all.** The instinctive
assumption, given `WP 9.2A`'s own precedent (a Domain object paired with
a separate execution Framework), was "yes, build the Verification
equivalent." Reading `IVerificationService`'s own actual shape first
(one method, `RecordAsync`, no generic `TInput`/`TResult` per Template)
revealed the assumption was wrong — there was no generic-dispatch
problem to solve.

## 5. The Design

`ADR-0089` answers the second problem by *not* building an adapter:
`RecordVerificationResultCommand` calls `IVerificationService.RecordAsync`
directly, realising "Execute," "Record Result," and "Attach Evidence"
together in one command, since the Framework itself has exactly one
action to dispatch to.

The first problem is solved exactly as every prior real-discipline Work
Package already proved it should be:
`VerificationActivityNodeProvider`/`VerificationActivityWorkspaceViewFactory`/
`VerificationActivityPropertyFacetProvider` mirror their Documents
counterparts' own shape closely, categorised by `Method` via
`VerificationMethodCategory` — the identical categorisation-over-one-real-Kind
pattern `WP 9.4A`'s own `DocumentCategory` already established, reused
directly rather than reinvented.

"Verification Plan" vs. "Verification Activity" (`ADR-0090`) is answered
the same way `ADR-0088` answered Document classification: an existing,
already-general field (`LifecycleState`, not `Classification` this time)
already expresses the distinction honestly.

A genuine platform characteristic, not part of the original design, was
discovered while building `VerificationRecordReader`: `VerificationService
.RecordAsync` links its own subject to the new record through the raw
document store only, never through `EngineeringDomainContext
.RelationshipRepository` — unlike `CalculationTemplateRegistry.ExecuteAsync`,
which explicitly makes that second, `RelationshipRepository`-populating
call itself. `VerificationRecordReader` was corrected to read the raw
store directly instead, once this was understood (`TD-32`).

## 6. Alternatives Considered

**A `VerificationMethodRegistry`, mirroring `CalculationTemplateRegistry`'s
own shape exactly, "just in case."** Considered and rejected; building
a registry to solve a generic-dispatch problem that does not exist would
be structure without justification — see `ADR-0089`'s own Alternatives
Considered section.

**Having `RecordVerificationResultCommandHandler` call `activity.LinkAsync
(record.Id, "verifiedBy")` itself, after `RecordAsync`, to populate
`RelationshipRepository` directly.** Considered and rejected; this would
create a genuine duplicate raw-store reference (the identical
source/target/kind recorded twice), a new defect this Work Package's own
implementation would have introduced to "fix" a data-visibility gap that
a correct read-side change resolves without any additional write at all.

**A dedicated `VerificationPlan` Domain Kind.** Considered and rejected
for the identical reason `ADR-0088` already established for Document
classification — see `ADR-0090`'s own Alternatives Considered section.

## 7. Why This Solution Was Chosen

Every alternative either built unneeded structure (a registry with no
underlying dispatch problem), introduced a new defect while attempting
to fix an existing gap (the duplicate-link approach), or reopened a
frozen Domain contract for a distinction an existing field already
expresses (a new Plan Kind). The chosen design costs nothing extra
Domain- or Framework-side, reuses `WP 9.4A`'s own categorisation pattern
directly, and resolves its own one genuine platform-characteristic
finding at the read side, where the actual, already-correct data already
lives.

## 8. Architectural Principles

**Not every Domain-object-plus-separate-Framework pairing needs a
connecting adapter — check the Framework's own actual dispatch shape
before assuming one is needed by analogy to a prior Work Package.**
`WP 9.2A` needed `CalculationTemplateRegistry` because
`ICalculationEngine.ExecuteAsync` is generic per Template;
`IVerificationService.RecordAsync` is not generic at all, so nothing
equivalent was needed here.

**A structurally similar precedent's own shape transferring does not
guarantee its own specific data-access call transfers with it.**
`CalculationRecordReader`'s and `VerificationRecordReader`'s own
*shapes* are near-identical; their own correct *data source* differs,
because the two Frameworks populate different underlying stores for
what looks, from the outside, like the identical kind of link.

**When a data-visibility gap is found, prefer reading the existing,
already-correct data from the right place over writing new data to make
it visible somewhere else.** The chosen fix for `TD-32` introduces zero
new writes and zero risk of duplication; the rejected alternative would
have introduced both.

## 9. Files Added

15 new files under `src/Tempest.App/Workspace/Verification/`; 2 new
files under `src/Samples/Tempest.Samples/`; 3 new test files. See
`WP9.3A Implementation Report.md` for the complete list including edited
files.

## 10. Trade-offs

`VerificationRecordReader`'s own Activity→Record link is read from the
raw document store, never `RelationshipRepository` — any future code
querying `RelationshipRepository` directly for this specific link will
see none, a disclosed, narrow trap (`TD-32`, `FCR-0057`). "Verification
Approval State" is a status reading, not a governed sign-off record
(`TD-30`, `FCR-0058`). Witness information has no dedicated field
(`FCR-0059`). All three accepted, disclosed, not silently absorbed.

## 11. Common Mistakes

Assuming a Workspace-layer reader pattern (`CalculationRecordReader`)
transfers to a structurally similar-looking Framework
(`IVerificationService`) without verifying the new Framework's own
actual relationship-writing mechanism first. Caught immediately by nine
failing tests, not by review — corrected before any test assertion was
adjusted to match the wrong behaviour, and before any commit.

## 12. Future Evolution

`VerificationService.RecordAsync` additionally linking through
`IHasRelationships` when its own subject is a real Domain object
(`FCR-0057`), a governed Approval/Review workflow extending `FCR-0052`
(`FCR-0058`), and a dedicated `Witness` field on `VerificationEvidenceEntry`
(`FCR-0059`) are all named, deliberate non-scope for this Work Package.

## 13. Key Takeaways

The Kind-keyed Workspace extension model (`ADR-0067`) has now been
proven across five genuinely different situations without a single
frozen Workspace contract being reopened, and without a single
Domain-layer or Verification-Framework file being edited, a second
consecutive time (after `WP 9.2A`). Equally important: this Work Package
demonstrates that a precedent's own *shape* generalising does not mean
every one of its own *specific mechanisms* generalises unmodified — the
discipline of verifying against a real, seeded integration test suite,
established since `WP 9.0B`, caught the one place that assumption broke,
before it ever reached a commit.

# Part II — Implementation Retrospective

## What Was Planned vs. What Was Built

The plan called for a Documents-pattern Workspace layer over
`VerificationActivity`, a possible connecting adapter to
`IVerificationService` (mirroring `WP 9.2A`'s own `CalculationTemplateRegistry`,
pending confirmation it was actually needed), and representative
Verification Activities demonstrating Digital Thread integration across
every named node the controlling instruction lists. What was built
matched that plan exactly, with the adapter question resolved to "not
needed" after reading `IVerificationService`'s own real shape — the
second consecutive real-discipline Work Package (after `WP 9.2A` itself,
in the opposite direction) to have its own initial structural assumption
tested directly against the real Framework before being built. The one
correction made during implementation was the `VerificationRecordReader`
data-source fix (`TD-32`), found by nine failing tests and corrected
before any commit.

## Verification Rigour

50 new tests, 1972/1972 passing, across four full clean rebuild-and-test
runs (two Debug, two Release, via `src/TempestOS.slnx`), plus per-project
Release builds of `Tempest.App`/`Tempest.Samples`. Unlike `WP 9.2A`'s and
`WP 9.4A`'s own verification, which each surfaced no genuine defect in
already-real code, this Work Package's own testing found one genuine,
disclosed platform characteristic (`TD-32`) — caught by nine failing
tests, not by inspection, and fully corrected at the read side before
any commit.

## Governance Discipline

Two new ADRs (`ADR-0089`, `ADR-0090`) record the two genuine new
architectural decisions this Work Package made, both confined entirely
to the Workspace layer. One new Technical Debt item (`TD-32`) and three
new Future Capability candidates (`FCR-0057`–`FCR-0059`) disclose every
known limitation directly, none silently absorbed. The controlling
instruction's own disclosed inconsistency (the "Await... `WP 9.4A`"
closing line, referring to an already-complete Work Package) is
recorded plainly in the Implementation Report and `PROJECT_STATUS.md`,
neither silently corrected nor treated as a request to redo `WP 9.4A`.

## Retrospective Verdict

The Kind-keyed Workspace extension model proved itself a fifth time,
this time by correctly recognising when a connecting mechanism is *not*
needed, resolved by reading the real Framework's own shape rather than
assuming a prior Work Package's own precedent applied unmodified. The
one genuine platform characteristic this Work Package's own
implementation surfaced was found by a failing test, root-caused
precisely by comparing two Frameworks' own actual source, and resolved
at the read side without touching either the Domain layer or the
unmodifiable Verification Framework — reinforcing, for a second
consecutive Work Package, that representative data exercised by a real
integration test suite earns its keep as a verification technique.

## Related Documents

`WP9.3A Implementation Report.md`; `WP9.3A Lessons Learned.md`;
`ADR-0089`; `ADR-0090`; `WP9.2A-engineering-calculations-workspace.md`;
`WP9.4A-engineering-documents-workspace.md`.
