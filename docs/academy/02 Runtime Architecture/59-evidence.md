# Evidence: the Record of a Calculation Done Elsewhere

**Release:** `v0.18.0` · **Work Package(s):** `WP 18.0A` ·
**Debt:** `TD-174`, `TD-175` (dissolved into `WP 18.0A`/`WP 18.2A`) ·
**Decision:** `ADR-0148`, `D-028` ·
**Code:** `src/Tempest.Core/Evidence/`, `src/Tempest.Workspace/Workspace/Evidence/`

**In plain terms.** An engineer's real work usually happens in a
spreadsheet or on a hand-calculation pad, not inside Tempest — and that
is going to keep being true. What Tempest now does is keep the proof of
that work as one record nobody can quietly change: the files calculated,
a plain description of what the calculation is about, exactly which
reference figures it relied on (a material's strength, a standard's
value) and at which approved version, the key numbers it produced, who
checked it, and when it was formally sent to the client. Once sent, a
record cannot be edited in place — more work opens a new, separate
version, and the one already sent stays exactly as it was. That single
idea, Tempest as the trustworthy record of work done elsewhere rather
than a place that tries to do the work itself, is what this release is
built around.

## The question that cancelled a calculation engine

`v0.18.0` was originally "Calculation as Document": an expression
grammar, a cell-grid editor, a run-by-run diff, built inside Tempest.
After the second Windows smoke test of `v0.17.0`, the Product Owner asked
the question that reframed the remaining programme:

> Is this the place for doing calculations? Or should they be done in
> separate software (an Excel workbook for example) and the evidence then
> loaded into Tempest as a record of that?

`D-028` is the answer: **evidence is the product; calculation is where
the engineer already does it.** About a third of the remaining
programme — 34 developer-days — was a calculation tool competing with
Excel, Mathcad and SMath on the engineer's own desk, holding the
highest-risk Work Package left in the plan: a frozen expression grammar,
locked down before its own parser had been written against it. Every
capability a consultancy needs from evidence — verified files, governed
data pinned to a released revision with an unreleased one refused, a
checker who cannot also be the author, an audit row for every act —
already existed in the substrate `v0.17.0` had built, needing no new
engine. Cancelling the grammar redirected 37 days at something the
platform already did well; the surfaces already shipped were not
removed (*"...pivot and strip out later than have to build it
later"*), and `WP 18.3A`, which would have retired them, was withdrawn
the same day.

## What a piece of evidence actually holds

`Evidence` is a canonical **Kind** — the platform's term for a named
category of engineering object, like Part or Requirement, each with its
own rules — following `Part`'s own shape exactly: `Evidence :
EngineeringObjectBase, IEvidenceRecord, IRehydratable<Evidence>`,
inheriting identity, revisions, attachments and audit from the base
rather than reimplementing any of them (see
`34-engineering-object-rehydration.md`). `IEvidenceRecord`, not the
briefed `IEvidence`: the first commit, `83649ec`, found that name already
taken by an unrelated, pre-existing Digital Thread type, and disclosed
the collision rather than working around it silently.

Its own state is small and deliberate:

- **`Classification`** — a closed choice of `Calculation`, `Drawing`,
  `Report`, `Test` or `Other`, not an open text field.
- **`SubjectId`** — the Part, Assembly, Requirement or Deliverable the
  evidence is about, a bare id never validated as a structural link:
  `D-028`'s **single-parent-tree-as-tags rule** — the object tree stays
  one strict hierarchy, and anything needing a second relationship is
  just a tag pointing at a name.
- **`AuthorIdentityId`** — set once, carried unchanged through every
  revision, so `Revise` cannot change whom the independence rule below
  holds responsible for the original work.
- **`Citations`** — governed records the work relied on, each an
  `EvidenceCitation` wrapping a `ReferencePin` (library, record, revision,
  minted from the record actually read) and a `SourceCitationSnapshot` of
  where the record's figure came from.
- **`DeclaredFigures`** — named, typed results and inputs, each a
  `Quantity` (`ADR-0147`, `52-units-as-a-runtime-dimension-vector.md`) as
  `"<value> <unit symbol>"` text, checked against `EvidenceUnitCatalog`.
- **`Status`**, **`Check`** and **`Issue`** — covered next.

## A lifecycle of its own, not a borrowed one

The obvious choice of status was the platform's existing eight-state
canonical vocabulary, `LifecycleState`. `ADR-0148` chose otherwise, for
the reason `41-project-tasks-and-delivery-workflow.md` gives for tasks:
the canonical table's rules are correct for what they protect, and wrong
for this shape of work. `ADR-0074` lets each object family **specialise**
the vocabulary with its own closed enum and table, and `RequirementStatus`
is the existing precedent. `EvidenceStatus` is the second — `Draft`,
`Checked`, `Issued`, `Superseded` — mapping by name and meaning onto
canonical `Draft`, `InReview`, `Released` and `Superseded`; `Approved`,
`Obsolete`, `Archived` and `Cancelled` are simply omitted.

```csharp
[EvidenceStatus.Draft] = new HashSet<EvidenceStatus> { EvidenceStatus.Checked },
[EvidenceStatus.Checked] = new HashSet<EvidenceStatus> { EvidenceStatus.Issued },
[EvidenceStatus.Issued] = new HashSet<EvidenceStatus> { EvidenceStatus.Draft, EvidenceStatus.Superseded },
[EvidenceStatus.Superseded] = new HashSet<EvidenceStatus>(),
```

`Issued → Draft` looks like the one move an engineering platform should
never allow. It is not: it is `ReviseAsync`, and it never touches the
issued content. A new revision begins; the issued one stays exactly as
sent, readable forever through the object's ordinary revision history. A
test walks all sixteen `(from, to)` pairs this table can produce against
a hand-audited expected set, so a widening edit fails immediately.

`Check` and `Issue` are not a fourth verification model: `CheckRecord`
and `IssueRecord` are plain values carried on the Kind, written in the
same transaction as the status move that follows, mirroring how the
platform's reference-review service already writes a reviewer's identity
alongside its own state change. `ADR-0148` itself was added to the ADR
Register the same day by `b33aa3a`; what checking and issuing look like
on screen is `WP 18.2B`, see `64-independent-check-and-the-issue-sheet.md`.

## The one seam that decides, and never throws

`Evidence`'s own methods are deliberately dumb: each is one
`MutateTypeStateAndPersistAsync` call — one transaction, one audit row
(`ADR-0145`, see `54-one-transaction-per-engineering-change.md`) — with
no judgement about whether the change is *allowed*. That judgement sits
in `IEvidenceService`/`EvidenceService`, the seam every act with an
external precondition passes through first, following
`GovernedBracketCheckService`'s own precedent: a refusal is "a
first-class answer... not an error condition," never a thrown exception.
`EvidenceService` reports every governed refusal the same way, as an
`EvidenceRefusal` on a result — an unverified citation or a check out of
turn belongs in a status bar, not a crash.

`CiteAsync` names its library by a plain string — the five governed
catalogues (Materials, Fasteners, Bearings, Standards, Constants) are
each generic over their own definition type, so no single typed handle
spans all five — and dispatches across them directly, refusing an
unreleased record by name: `$"Record '{recordId}' in '{library}' is
{lookup.Value.ValidationState}, not Released..."`

The same seam carries the **independence rule**: `Evidence:IndependentCheck`
(Product Owner: *"a one-person consultancy has one login and enters the
client's review by hand"*) is read from `IConfigurationProvider` and,
when supplied, `ISettingsProvider` too — `EvidenceService` registers its
own `SettingDefinition` so a future Settings screen can read and write it
unseen elsewhere. Off, any principal may record a check and
`CheckerIdentityId` stays `null`. On, `RecordCheckAsync` refuses a
principal checking their own evidence, or nobody signed in at all; both
positions are proved by test.

`EvidenceCitation.SourceCitationSnapshot` was meant to carry a snapshot of
where a cited record's figure came from, but `WP 18.0A` was written
before `WP 18.0B`'s own structured `SourceCitation` existed (see
`60-source-citations-and-supersession.md`), so it shipped `null`,
disclosed rather than guessed at. Once both merged, `57e1dd2` closed the
gap in four lines: `CiteAsync` now copies the released record's own
source citation onto the evidence citation it records, so an issue sheet
can print where a figure came from without a second lookup — two Work
Packages, built independently against a gap each side had named
honestly, meeting in one obviously-correct commit.

## A Workspace registration with nothing to look at yet

`EvidenceWorkspaceRegistration` (`6dd8df7`) wires the discipline into a
running Workspace: an explorer area, `"evidence"`, populated by
`EvidenceNodeProvider` (project → classification group → evidence, each
title carrying its status), a property facet provider, and six
`evidence.*` commands — `create`, `cite`, `declare-figure`, `check`,
`issue`, `revise`. `WP 18.0A`'s own scope was substrate, not screen (see
`63-the-evidence-workspace.md`), yet a created record still opens right
up, because the Object Editor already opens any Kind generically from its
facet provider. `evidence.rename` and `evidence.delete` get their own
ids but no new command classes: `RenameMechanicalObjectCommand`/
`DeleteMechanicalObjectCommand` already act on any
`IRenamable`/`IDeletable` object regardless of Kind, so Evidence's own
descriptors dispatch straight to Mechanical's existing handlers — though
reuse was not free. `10de195` found that `evidence.delete`, left as an
ordinary binding, would have deleted the object correctly and left the
shell still pointing at it — the stale-selection defect `TD-58` had
already closed for every other discipline by routing every delete
through `IWorkspaceManager.DeleteObjectAsync` instead. One disclosed line
closed the gap, outside `WP 18.0A`'s own file list but the right call:
the alternative was a delete that looked correct and was not.

## What was deliberately not built

`ADR-0148` names a cost of its own design still unpaid: `EvidenceService`
depends on all five reference-data catalogues directly, by constructor,
repeating the shape `GovernedBracketCheckService` had already accepted
for one caller rather than generalising a dispatch mechanism for what is
still only two. More fundamentally, `D-028` and `ADR-0148` hold one line
throughout: **TempestOS is not an ERP and not a PLM system.** There is no
part-occurrence model — a Part cited by three drawings is three tags at
the same id, not three tracked occurrences — no where-used index across
assemblies, no change control on a citation or a figure beyond the
record's own status moves, and no workflow engine behind `Check` or
`Issue`: both are values written in a transaction, not steps in a
configurable process. Every one is a capability a real PLM system would
have, left out on purpose, because `SubjectId` as an unvalidated tag is
what keeps the object graph a simple tree, not a web of relationships
nobody asked the product to become.

## What to take away

- **When an existing rule is correct for what it protects, specialise it
  rather than weaken it.** `EvidenceStatus` gets this Kind exactly the
  states it needs without loosening `LifecycleState`'s guarantee for
  every other object.
- **A refusal that names the exact record and reason belongs in the
  result, not a stack trace.** `EvidenceService` never throws for an
  ordinary engineering-governance finding.
- **Naming a gap a Work Package cannot yet close is what lets the next
  one close it cleanly.** `SourceCitationSnapshot` shipped `null`,
  disclosed, and was filled in four lines once the record that could
  fill it existed.
