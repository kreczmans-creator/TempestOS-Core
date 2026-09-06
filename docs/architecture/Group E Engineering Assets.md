# Group E — Engineering Assets

**Programme:** P05 — Engineering Assets
**Namespace:** `Tempest.Core.EngineeringAssets`
**Governing ADRs:** `ADR-0136`, `ADR-0137`, `ADR-0138`
**Status:** Architecturally complete, `Group E`. Every library ships
**empty** — see §10.

---

## 1. Purpose

`P01` established what is true about materials, standards and processes.
`P02` established what follows from them. `P03` and `P07` established what
things cost and what the organisation is committed to. `P05` establishes
the **reusable artefacts engineering work produces**, so that structure
is authored once rather than recreated on every job.

Five questions, and a sixth about every answer:

| | Question | Package |
|---|---|---|
| 1 | What structure should this kind of work follow? | `E1` / `WP05.1` |
| 2 | What did this calculation use, and could somebody do it again? | `E2` / `WP05.2` |
| 3 | Has this requirement actually been shown to be met? | `E3` / `WP05.3` |
| 4 | What did the review look at, find, agree and decide? | `E4` / `WP05.4` |
| 5 | Which issue of which document is in force? | `E5` / `WP05.5` |
| 6 | *Who says so, and would we still say it today?* | all |

---

## 2. What P05 is not

- **Not a calculation engine.** `Tempest.Core.Calculations` executes
  calculations. `E2` packages them (`ADR-0137`).
- **Not a requirements tool.** `Tempest.Core.Requirements` owns
  requirements. `E3` holds evidence about them.
- **Not a document-management system.** `Tempest.Core.EngineeringData`
  owns document content, revisions and links. `E5` holds the governance
  card.
- **Not a workflow engine.** `E4` structures what a review produced.
  Running the review, chasing the actions and obtaining approval belong
  to later operational integration.
- **Not an authority.** Nothing in `P05` approves, authorises, signs off
  or certifies anything (`ADR-0138`).

---

## 3. The shared core

Four files every package uses.

### `EngineeringEvidence`

Kind, description, and where to find it — a document Id, a
`ReferencePin`, or an external reference. `IsLocatable` is the property
that matters: evidence nobody can go and check is a recollection.
`IsIndependent` separates a test report or certificate from an internal
record or somebody's judgement.

A separate vocabulary from `P07`'s `BusinessEvidenceKind`. The two records
have the same shape and different meanings: an insurance certificate is
not engineering evidence and a test report is not a business record.

### `AssetGovernanceFacts`

Ownership, authorship, reviews, approval, classification, evidence —
composed into each asset type, not inherited (`ADR-0136`).

Two separations carried throughout: **ownership from authorship**
(ownership changes, authorship never does) and **review from approval**
(a reviewer says the work is sound; an approver commits the
organisation).

### `AssetApplicability` and `AssetEnquiry`

Discipline, project, subject kind, effectivity, conditions — every
dimension optional, and an unstated dimension means **unrestricted**.
This is the opposite of `P03`'s convention and `ADR-0136` records why.

### `AssetStanding` and `AssetGovernanceValidation`

Seven standings as a second axis from the record lifecycle, plus the
governance checks every library shares under `TEMPEST-EAG-001`–`012`.

---

## 4. Governance and storage

| Library | Document kind |
|---|---|
| `EngineeringTemplates` | `EngineeringTemplate` |
| `EngineeringCalculationPacks` | `EngineeringCalculationPack` |
| `EngineeringVerificationArtefacts` | `EngineeringVerificationArtefact` |
| `EngineeringDesignReviews` | `EngineeringDesignReviewPack` |
| `EngineeringTechnicalDocuments` | `EngineeringTechnicalDocumentRecord` |

Each on `ReferenceDataCatalog<TDefinition>` with the full lifecycle, and
each with a validation service reporting under its own prefix:
`TEMPEST-EAT` (templates), `-EAC` (calculation packs), `-EAV`
(verification), `-EAR` (reviews), `-EAD` (documents), over the shared
`-EAG`.

---

## 5. E1 / WP05.1 — Engineering templates

A template is **structure, not content**: sections, fields, what each
field expects, what is required. It never holds an answer.

**Using a template pins it.** `TemplateUsage` records a `ReferencePin`
naming the exact revision worked from, and `ITemplateCatalog.PinAsync`
takes that revision from the record rather than the caller, so a pin
naming a revision nobody read is not constructible. Revising a template
to revision 4 leaves work done from revision 3 saying revision 3.

`FindApplicableAsync` returns **released** templates only, most specific
first. A draft template has not been checked by anybody and must not
silently shape engineering work.

---

## 6. E2 / WP05.2 — Calculation packs

What the calculation *was*: inputs and where each value came from, the
method, the assumptions, the outputs and what they must satisfy, the
limitations, and who stands behind it.

Every `CalculationInput` pins the record revision its value came from, so
"what did this calculation use when it was performed?" keeps its answer.
`FindCitingAsync` asks it backwards: a material property was revised, so
which calculations relied on the old one?

`IsReproducible` is deliberately strict — inputs, outputs, a stated
method, every input sourced, and a named tool at a named version where
software was used. An unnamed solver at an unnamed version fails it.

---

## 7. E3 / WP05.3 — Verification artefacts

Four things kept apart (`ADR-0137`, `ADR-0138`): the **requirement**
(referenced, never copied), the **activity**, the **evidence**, and any
**decision** taken on the strength of it.

Six standings, because the domain has six answers. `Standing` is derived
from the result rather than settable; `IsDemonstrated` is true for
`Passed` alone; `Weakest` over nothing is `NotPerformed`. A recorded pass
with nothing locatable behind it is a validation **error**.

`VerificationTraceService` answers "is this requirement verified, and how
do we know?" and reports concerns — including the common one: verified,
on the asserting party's own material, against a requirement revision
nobody pinned.

---

## 8. E4 / WP05.4 — Design review packs

Six things kept apart: observation, recommendation, action, decision,
outcome, approval (`ADR-0138`).

A `ReviewDecision` refuses construction without a rationale and a named
person. An action with no owner is reported. `ReviewOutcome` is the
reviewers' judgement and `Approval` is the organisation's commitment, and
a pack may have the first without the second.

Concluding `Proceed` while a `Critical` observation has no action and no
decision against it is a validation **error**.

`FindOutstandingActionsAsync` surfaces every unclosed action across every
review, most overdue first — actions live inside their own packs, which is
where they belong and the last place anybody looks.

---

## 9. E5 / WP05.5 — Technical documentation

The governance card for a document, not the document. Content, revision
history and links stay in `EngineeringData`;
`ToDocumentReferences()` expresses `E5` relationships as the platform's
own `DocumentReference` rows.

`IssueRevision` ("A", "B", "P1") is deliberately distinct from the
record's own `RevisionNumber` (1, 2, 3). `DocumentStatus` is a second axis
from the record lifecycle, so a released validated record of a draft
drawing stays expressible.

Validation concentrates on the transition into issue, where a
documentation system either holds or fails — including
`PredecessorStillInForce`, which catches the case that puts two live
issues of one drawing on the shop floor.

---

## 10. What ships

**Every library ships empty.** `P05` is structure, governance and
traceability; it contains no template, no calculation, no verification
and no document.

This is deliberate. The templates and document structures an organisation
uses are its own intellectual property and change with its practice;
shipping them in a platform release makes them a thing to be upgraded
rather than edited.

Test fixtures are fictional throughout and marked as such: project
"FIX-PROJ", documents "FIX-DWG-…", requirement "FIX-REQ-001". They live
only in the test project, backed by in-memory stores that die with the
test, and are registered nowhere at run time.

---

## 11. Dependencies

`P05` depends on:

- **`P01`'s shared reference-data layer** for the lifecycle, `ReferencePin`
  and `IReferencePinResolver`.
- **`P07`** for `EffectivePeriod`, `BusinessAuthorisation` and
  `ConfidentialityClassification`.
- **`Tempest.Core.Requirements`**, **`Tempest.Core.Calculations`**,
  **`Tempest.Core.Verification`** and **`Tempest.Core.EngineeringData`**
  — all referenced by identity, none duplicated (`ADR-0137`).

Every cross-library collaborator in validation is **optional**, so an
asset is recordable and checkable before the thing it cites is
registered.

`P05` does not depend on `P02`, `P03` or `P06`.
