# ADR-0148: Evidence Is a Canonical Kind, Specialising the Lifecycle Vocabulary and Citing Governed Reference Data

## Status

Accepted — `WP 18.0A` (Evidence record), 2026-09-09.

## Context

`D-028` (2026-09-09) makes evidence the product: an engineer calculates
wherever they already do, and Tempest records the result — the files,
what it is about, the governed references it stood on, its key figures,
its check and its issue. `WP 18.0A` is the substrate half of that
decision: a canonical Kind following `Part`'s own shape
(`EngineeringObjectBase`, `IRehydratable<Evidence>`), inheriting
identity, revisions, attachments and audit from the base.

Two contracts already existed for the pieces evidence needed to compose:
`GovernedBracketCheckService.CheckAsync` refuses an unreleased material as
a result, not an exception; `ADR-0074` makes lifecycle status "one
canonical vocabulary, specialised per object family," and
`RequirementStatus`/`RequirementStatusTransitions` is its own precedent
for specialising with an independent enum and table rather than reusing
`LifecycleState` directly.

## Decision

**1. `Evidence : EngineeringObjectBase, IEvidenceRecord, IRehydratable<Evidence>`**
(`src/Tempest.Core/Evidence/`), state: `Classification` (`EvidenceClassification`:
Calculation, Drawing, Report, Test, Other), `SubjectId` (`Guid?`, a tag,
never validated — `D-028`'s single-parent-tree-as-tags rule), `AuthorIdentityId`
(stable across revisions — see Decision 4), `Citations`
(`EvidenceCitation`: a `ReferencePin`, the record's own id as its display
name, and a `SourceCitationSnapshot` left `null` until `WP 18.0B`'s own
structured citation lands), `DeclaredFigures` (`DeclaredFigure`: name,
Input/Result role, a `Quantity` — the runtime-dimension facade, `ADR-0147`
— as `"<value> <symbol>"` text, checked against a new
`EvidenceUnitCatalog` flattening every dimension this platform declares),
`Status` (`EvidenceStatus`, below), `Check` (`CheckRecord?`), `Issue`
(`IssueRecord?`). `Classification` and `Status` are declared `new` on the
concrete class: both hide a differently-typed base member
(`IHasMetadata.Classification: string?`, `IHasLifecycle.Status:
LifecycleState`) inherited regardless, which stays reachable by casting
to that facet directly.

**2. `EvidenceStatus` (Draft, Checked, Issued, Superseded) specialises
`ADR-0074`'s canonical vocabulary the way `RequirementStatus` does: its
own closed enum and its own `EvidenceStatusTransitions` permitted table**
(`Draft→Checked`, `Checked→Issued`, `Issued→{Draft, Superseded}`,
`Superseded→{}`), not a reuse of `LifecycleState`. By name and meaning:
`Draft`≈canonical `Draft`, `Checked`≈`InReview` (checked, not yet issued),
`Issued`≈`Released`, `Superseded`≈`Superseded` outright; `Approved`,
`Obsolete`, `Archived`, `Cancelled` are omitted, as `ADR-0074` permits.
`Issued→Draft` is `ReviseAsync`: a new revision begins, and the issued
content stays readable via the object's own existing revision history
(`IHasRevisions.GetRevisionHistoryAsync`) — no second, evidence-specific
versioning mechanism.

**3. `IEvidenceService`/`EvidenceService` is the one governed seam.**
Every act external permission depends on — a cited record's own
`ValidationState`, a status move, the independent-check rule — is decided
here, before `Evidence`'s own `internal` mutators (each a single
`MutateTypeStateAndPersistAsync` call, one transaction with an audit row)
ever run, and reported back as a refusal result
(`EvidenceCitationResult`/`EvidenceActionResult`, an `EvidenceRefusal`
enum), never an exception — exactly `GovernedBracketCheckService`'s own
precedent. `CiteAsync` dispatches a caller-given library name string
across the five governed catalogues it depends on directly (Materials,
Fasteners, Bearings, Standards, Constants — mirroring `ReferencePin`'s own
documented reason no single typed handle spans them) and refuses a
non-`Released` record, naming it.

**4. The independent-check rule** (`Evidence:IndependentCheck`, Product
Owner 2026-09-09: off by default — a one-person consultancy has one login
and enters the client's review by hand) is read from
`IConfigurationProvider`, and from `ISettingsProvider` too when supplied:
`EvidenceService` registers its own `SettingDefinition` so a future
Settings screen (`WP 18.2A`) can read and write it unseen elsewhere. On,
`RecordCheckAsync` refuses a principal checking evidence whose
`AuthorIdentityId` equals their own; off, any principal may check,
`CheckerIdentityId` stays `null`. `AuthorIdentityId` is set once, at
creation, and carried unchanged through `Revise`.

**5. The Workspace registration owns no Desktop view** — `WP 18.0A`'s own
scope is substrate; `WP 18.2A` builds the view. `EvidenceWorkspaceRegistration`
registers an explorer area (`"evidence"`; `EvidenceNodeProvider`: project
→ classification group → evidence, titles carrying status) and
`EvidencePropertyFacetProvider` (classification, subject **named**, not a
bare id, via `PropertyFacetKind.ObjectReference` — a departure from the
"Parent" facet's own store-the-id convention elsewhere, because this
Kind's own acceptance test requires it), plus command descriptors
(`evidence.create`/`cite`/`declare-figure`/`check`/`issue`/`revise`).
Rename and delete reuse Mechanical's own `RenameMechanicalObjectCommand`/
`DeleteMechanicalObjectCommand`, both already Kind-agnostic, rather than
two more near-identical copies. `evidence.revise` carries a confirmation:
it is a genuine status move, so it does not join `ADR-0098`'s macro-safe
set.

## Consequences

**Positive:** a citation can never point at reference data nobody has
verified, with the same refusal shape a governed calculation already
uses; the check independence rule is real and tested in both positions
without a second identity mechanism; a created record opens right up with
no Desktop view yet built, because the Object Editor already opens any
Kind generically from its facet provider.

**Negative:** `EvidenceCitation.SourceCitationSnapshot` is always `null`
until `WP 18.0B` lands a structured `SourceCitation` — disclosed, not
silently populated with a guess. `EvidenceService` depends on all five
reference-data catalogues directly, `GovernedBracketCheckService`'s own
already-accepted shape, repeated rather than generalised for one caller.

## Related Documents

`D-028`; `ADR-0074` (lifecycle specialisation); `ADR-0145` (one
transaction, one audit row); `ADR-0147` (`Quantity`);
`GovernedBracketCheckService` (refusal-as-result precedent);
`RequirementStatusTransitions` (independent-status-vocabulary precedent);
`docs/releases/v1.0.0/WorkPackages.md` (`WP 18.0A` row).
