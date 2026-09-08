# WP 9.4A — Engineering Documents Workspace

> This file satisfies `WP 9.4A`'s own two named Academy deliverables —
> "Academy Concept Guide" and "Academy Implementation Retrospective" — as
> two clearly headed parts within one file, mirroring
> `WP9.2A-engineering-calculations-workspace.md`'s own identical,
> disclosed documentation-structure decision, preserving this folder's
> own established one-file-per-Work-Package convention.

# Part I — Concept Guide

## 1. Introduction

`WP 9.4A` is `v0.9.0`'s own fifth Work Package, and the fourth real
Engineering discipline wired into the Engineering Workspace, after
Mechanical (`WP 9.0A`/`WP 9.0B`), Requirements (`WP 9.1A`), and
Calculations (`WP 9.2A`). It also begins under a disclosed numbering
gap — the release's own record shows `WP 9.2A` closing with "no `WP 9.3A`
begins... until the Product Owner gives further instruction," and the
Product Owner's own instruction commissioning this Work Package names
`9.4A` directly. Where `WP 9.2A` proved the Kind-keyed Workspace
extension model against synthetic, non-Domain content, `WP 9.4A` proves
it against a different kind of gap entirely: a controlling instruction
naming eight Document types where the Domain layer provides only three
real Kinds.

## 2. Purpose

To give the already-real Documentation & Design Domain family
(`Document`/`Drawing`/`CadModel`, `WP 8.2C`) a complete Workspace
presence — a browsable Explorer tree categorised across every named
Document type; a Property Inspector showing real facets including
Drawing Number, Classification, Attachments, and Digital Thread links;
nine commands covering the full Document Management lifecycle
(including one genuinely new capability, file attachment); real
Engineering Cockpit KPIs; and nine representative, real engineering
documents demonstrating cross-discipline traceability to Requirements,
Calculations, Mechanical Product Structure, Risks, and Decisions — using
nothing the Domain layer did not already, or could not additively,
provide.

## 3. Background

By the time this Work Package began, `Document`/`Drawing`/`CadModel`
(`WP 8.2C`) existed as compiled, tested Domain objects — one of them
(`Drawing`) even already instantiated once, by the base
`EngineeringDomainSampleModule`'s own sixteen-object representative
graph — but with no Workspace presence at all: no Explorer area, no
Property Inspector facets, no commands, no Cockpit KPIs. Unlike
Calculations (`WP 9.2A`), which connected two already-real but
previously-unconnected frameworks, Documents needed no such connecting
mechanism — `Document` already carries every facet
(`IHasAttachments`/`IHasMetadata`/`IHasLifecycle`) this Work Package's
own scope needs. The genuine problem was different: the controlling
instruction names eight Document types (Drawings, Specifications,
Reports, Procedures, Standards, Datasheets, External References,
Attachments), and the Domain layer gives concrete classes to only three
of them.

## 4. The Problem

Two distinct problems:

**Six named types, three real Kinds.** Specification/Report/Procedure/
Standard/Datasheet/External Reference have no dedicated Domain Kind
anywhere in the platform. Building five new concrete classes would
satisfy the letter of the naming but violate this Work Package's own
explicit "no contract redesign" instruction — the identical shape of
tension `WP 9.2A`'s own "Failed"/"Out-of-date" KPI names and `WP 9.1A`'s
own "Released→Satisfied" mapping already resolved for status
vocabulary, here facing an entire object taxonomy instead of a handful
of names.

**A Digital Thread scope item (Documents ↔ Verification) with no live
Verification object anywhere to link to.** The disclosed `WP 9.3A`
numbering gap means the Verification Workspace this project's own
sequence expected to precede Documents was never built — so no live
Verification Domain object exists anywhere in this platform, a genuine
gap this Work Package's own scope runs directly into, not one it
introduces.

## 5. The Design

`ADR-0088` solves the first problem directly: `IHasMetadata.Classification`
— an existing, free-text facet every `Document` already carries via
`EngineeringObjectBase`'s own unconditional facet implementation — is
used to distinguish Specification/Report/Procedure/Standard/Datasheet/
External Reference, with `DocumentObjectFactoryRegistry` declaring six
named `Workspace`-layer string constants for them. `DocumentsNodeProvider`'s
own `DocumentCategory.Of` maps every live Document onto one of nine
Explorer categories — `Drawing`/`CadModel` by their own real Kind
directly, everything else by `Classification` — giving every named type
its own first-class Explorer presence without a single new Domain type.

The second problem is solved by disclosure, not invention: rather than
fabricate a Verification Domain object solely to populate a
demonstration link (which this Work Package's own governance discipline
explicitly forbids — "disclose all inconsistencies... do not silently
modify historical records"), the representative Test Report instead
`references` the one real Requirement with an actually-recorded
Verification — the closest genuine, live anchor this platform has —
and the gap itself is named directly in the Implementation Report,
Technical Debt Assessment, and Future Capability Assessment
(`FCR-0055`, recommending a Verification Workspace as the natural next
Work Package).

## 6. Alternatives Considered

**Five new concrete Domain classes (`Specification`, `Report`,
`Procedure`, `Standard`, `ExternalReferenceDocument`)** — considered and
rejected outright as exactly the "contract redesign" this Work Package's
own controlling instruction forbids; see `ADR-0088`'s own Alternatives
Considered section.

**A new `DocumentType` enum in `Tempest.Core.EngineeringDomain`** —
considered and rejected for the identical reason; any new Domain-layer
type, even a small enum, is still a Domain contract change this Work
Package's own scope forbids.

**Fabricating a placeholder Verification object to populate the
Documents↔Verification Digital Thread demonstration** — considered and
rejected; would misrepresent the platform's own actual current state,
directly contradicting this Work Package's own explicit governance
instruction to disclose inconsistencies rather than paper over them.

## 7. Why This Solution Was Chosen

Every alternative for the first problem would have reopened a Domain
contract `WP 8.2A`–`WP 8.2C` explicitly closed, for a display/categorisation
need the existing `Classification` facet already serves. The chosen
design costs nothing extra Domain-side, and generalises the Kind-keyed
Workspace extension model one further way — proving it now handles
"many named types over one real Kind," in addition to the "one synthetic
Kind with no Domain identity" shape `WP 9.2A` already proved. For the
second problem, the honest disclosure was chosen over a fabricated
demonstration because a Work Package whose own controlling instruction
explicitly requires disclosing inconsistencies cannot itself introduce a
new one to avoid an awkward gap.

## 8. Architectural Principles

**A free-text, unvalidated facet can honestly express an entire
taxonomy, not only a handful of status names.** `IHasMetadata.Classification`
was designed as a general-purpose facet; this Work Package is the first
to use it to carry an entire six-member taxonomy, proving the same
open-string precedent `RelationshipCategory` already established
(`ADR-0076`) generalises to Domain object categorisation, not only to
relationship metadata.

**Disclosure of a gap is itself a form of correctness.** Where the
platform's own governance discipline requires disclosing
inconsistencies, the "complete" answer to a scope item is sometimes "here
is exactly what exists, and here is exactly what does not, named
plainly" — not a fabricated demonstration that would make the gap
invisible to a future reader.

## 9. Files Added

15 new files under `src/Tempest.App/Workspace/Documents/`; 2 new files
under `src/Samples/Tempest.Samples/`; 3 new test files. See `WP9.4A
Implementation Report.md` for the complete list including edited files.

## 10. Trade-offs

`Classification` is free text, never validated at write time — a
misspelled value degrades honestly to `"Uncategorized"`, never crashes
or silently drops the object (`ADR-0088`). No file/URL storage service
exists anywhere in this platform — Attachments and External Reference
Content are metadata/placeholder only, never actual file content
(`TD-31`, `FCR-0054`). Documents↔Verification traceability is
structurally proven, not populated against a real, live Verification
object (`TD-30`'s own extended consequence, `FCR-0055`). All three
accepted, disclosed, not silently absorbed.

## 11. Common Mistakes

Assuming an integration test's own expected object count reflects only
the current Work Package's own new sample data — the Documents Cockpit
KPI test initially expected nine live Documents (this Work Package's own
count) and failed against the real, composed graph, which correctly also
counts the base `EngineeringDomainSampleModule`'s own pre-existing
"Sample Drawing" (ten total). Caught immediately by the failing
integration test itself; corrected before merge. Worth remembering: an
integration test assembling multiple sample modules must always reason
about the *full* composed graph, never only the newest module's own
contribution in isolation.

## 12. Future Evolution

A real file/URL attachment storage Platform Service (`FCR-0054`), a
Verification Workspace — this Work Package's own explicit recommendation
for what should follow it (`FCR-0055`) — and a dedicated Governance &
Risk Workspace giving `Risk`/`Decision`/`Issue`/`Hazard`/`Assumption`
their own first-class Explorer/Cockpit presence (`FCR-0056`) are all
named, deliberate non-scope for this Work Package.

## 13. Key Takeaways

The Kind-keyed Workspace extension model (`ADR-0067`) has now been
proven across four genuinely different situations — a facet-composed
Domain architecture, an immutable-snapshot service architecture,
synthetic non-Domain content bridging two previously-unconnected
frameworks, and (this Work Package) a controlling instruction naming
more sub-types than the Domain layer has dedicated Kinds for — without a
single frozen Workspace contract being reopened, and without a single
Domain-layer file being edited, a fourth time. Equally important: this
Work Package demonstrates that honest disclosure of a genuine platform
gap (no live Verification object to demonstrate against) is a more
correct engineering outcome than a fabricated demonstration that would
have hidden it.

# Part II — Implementation Retrospective

## What Was Planned vs. What Was Built

The plan called for a Calculations-pattern Workspace layer over
`Document`/`Drawing`/`CadModel`, a `Classification`-based realisation of
the six named types with no dedicated Kind, and representative
documents demonstrating Digital Thread integration across every named
node the controlling instruction lists. What was built matched that plan
exactly, with zero Domain-layer changes required — the second
consecutive real-discipline Work Package (after `WP 9.2A`) to need none.
The one addition made during implementation beyond the initial plan was
the `AttachDocumentCommand`/`ADR-0088`-adjacent decision to expand
representative data from six named documents to nine, covering every
type the controlling instruction's own broader Scope section lists,
disclosed directly rather than left implicit.

## Verification Rigour

57 new tests, 1922/1922 passing, across four full clean rebuild-and-test
runs (two Debug, two Release, via `src/TempestOS.slnx`), plus per-project
Release builds of `Tempest.App`/`Tempest.Samples`. Like `WP 9.2A`'s own
verification, this Work Package's own testing surfaced no genuine defect
in already-real code — the one test failure encountered during
development (the Cockpit KPI count assuming nine Documents instead of
the real, composed graph's own ten) was a test-expectation error, caught
and corrected before any commit, not a defect in shipped code.

## Governance Discipline

One new ADR (`ADR-0088`) records the one genuine new architectural
decision this Work Package made, confined entirely to the Workspace
layer. One new Technical Debt item (`TD-31`) and three new Future
Capability candidates (`FCR-0054`–`FCR-0056`) disclose every known
limitation directly, none silently absorbed. The `WP 9.3A` numbering gap
is disclosed plainly in the Implementation Report and `PROJECT_STATUS.md`,
neither silently renumbered nor silently filled in.

## Retrospective Verdict

The Kind-keyed Workspace extension model proved itself a fourth time,
this time against the novel problem of a controlling instruction naming
more sub-types than the Domain layer provides dedicated Kinds for —
resolved by recognising an existing, general facet already expresses the
taxonomy honestly, without reopening a single frozen contract or editing
a single Domain-layer file. Building nine real, representative documents
— not placeholder text — surfaced one genuine test-expectation error (an
incomplete accounting of the full composed sample graph) a purely
mechanical "does it compile" verification would never have caught,
reinforcing `WP 9.0B`'s, `WP 9.1A`'s, and `WP 9.2A`'s own shared lesson
that representative data earns its keep as a verification technique, not
only as a presentation nicety.

## Related Documents

`WP9.4A Implementation Report.md`; `WP9.4A Lessons Learned.md`;
`ADR-0088`; `WP9.0A-mechanical-product-structure.md`;
`WP9.1A-requirements-management-workspace.md`;
`WP9.2A-engineering-calculations-workspace.md`.
