# Group E — Engineering Assets: Completion Report

**Programme:** P05 — Engineering Assets
**Work Packages:** E1 (`WP05.1`), E2 (`WP05.2`), E3 (`WP05.3`), E4 (`WP05.4`), E5 (`WP05.5`)
**Date:** 2026-09-06
**Branch:** `claude/tempestos-a4-bearing-library-unobtf`

---

## 0. Programme status — the honest four facts

| | State |
|---|---|
| **Framework** | Complete. Five governed libraries, 11 services, all registered in the real host. |
| **Authoritative data** | **None.** Every library ships empty. |
| **Fictional test data** | Present, test-project only, marked throughout. |
| **Operational workflow** | **Not started.** `P05` structures artefacts; it runs no process. |

| Gate | Result |
|---|---|
| Build, Debug | 0 errors, 0 warnings |
| Tests, Debug | **4,612 / 4,612** Core, **474 / 474** Desktop, 0 failed, 0 skipped |
| Governance health check | **13 passed, 3 warned, 0 failed** of 16 |

The three warnings are pre-existing and environmental: no `v*` git tags
are reachable in this container (two checks), and two historical release
folders predate the `WorkPackages.md` convention.

`P05` added **215 tests** (4,397 → 4,612).

---

## 1. Numbering

| Package | Roadmap identifier | Subject |
|---|---|---|
| `E1` | `WP05.1` | Engineering templates |
| `E2` | `WP05.2` | Calculation packs |
| `E3` | `WP05.3` | Verification artefacts |
| `E4` | `WP05.4` | Design review packs |
| `E5` | `WP05.5` | Technical documentation system |

Five packages. No `E6`. No subdivision.

---

## 2. Shared architecture

29 source files under `src/Tempest.Core/EngineeringAssets/`: four in the
shared core, and two or three per package.

The shared core is deliberately small — evidence, governance facts,
applicability, standing — because the five asset kinds share these facts
and share no hierarchy. Composition, not inheritance (`ADR-0136`).

Two separations run through everything:

- **Ownership from authorship.** Ownership changes; authorship never
  does.
- **Review from approval.** A reviewer says the work is sound; an
  approver commits the organisation. The second is a `P07`
  `BusinessAuthorisation`, and nothing in `P05` constructs one.

`AssetStanding` is a second axis from `ReferenceValidationState`, so a
Released template whose effective period ended last year can be described
accurately as both.

---

## 3. Boundaries — what P05 references and never duplicates

`ADR-0137` records one decision applied at three boundaries, and a
reflection test enforces it.

| `P05` | Existing platform machinery | How they meet |
|---|---|---|
| `E2` calculation packs | `Tempest.Core.Calculations` | Names the definition, links executions by Id; never restates a result |
| `E3` verification artefacts | `Tempest.Core.Requirements`, `Tempest.Core.Verification` | References a requirement by `Guid`; links verification records by Id |
| `E5` technical documents | `Tempest.Core.EngineeringData` | Points at content by `DocumentId`; emits the platform's own `DocumentReference` rows |

Every cross-library collaborator in validation is **optional**, so an
asset is recordable and checkable before the thing it cites is
registered. Unresolvable references are reported, never prevented.

---

## 4. The five packages

### E1 / WP05.1 — Engineering templates

Structure, not content: sections, fields, what each expects, what is
required. Never holds an answer.

**The central promise:** using a template pins it. `TemplateUsage` carries
a `ReferencePin` naming the exact revision worked from, and
`ITemplateCatalog.PinAsync` takes that revision from the record rather
than the caller — so a pin naming a revision nobody read is not
constructible. Revising a template to revision 4 leaves work done from
revision 3 saying revision 3. 10 diagnostics, `TEMPEST-EAT-001`–`010`.

### E2 / WP05.2 — Calculation packs

What the calculation *was*. Every input pins where its value came from;
`IsReproducible` is strict enough to fail an unnamed solver at an unnamed
version. `FindCitingAsync` answers the impact question backwards: a
material property was revised, so which calculations relied on it? 15
diagnostics, `TEMPEST-EAC-001`–`015`.

### E3 / WP05.3 — Verification artefacts

Six standings because the domain has six answers. `Standing` is derived
from the result rather than settable; `IsDemonstrated` is true for
`Passed` alone; `Weakest` over nothing is `NotPerformed`. **A pass with
nothing locatable behind it is an error, not a warning.**
`VerificationTraceService` reports concerns and changes nothing. 13
diagnostics, `TEMPEST-EAV-001`–`013`.

### E4 / WP05.4 — Design review packs

Observation, recommendation, action, decision, outcome and approval as
six separate records. A `ReviewDecision` refuses construction without a
rationale and a named person. Proceeding over an unanswered `Critical`
observation is an **error**. 15 diagnostics, `TEMPEST-EAR-001`–`015`.

### E5 / WP05.5 — Technical documentation

The governance card, not the document. `IssueRevision` ("A", "B", "P1")
stays distinct from the record's `RevisionNumber` (1, 2, 3), because
conflating them makes it impossible to say which issue somebody holds.
Validation concentrates on the transition into issue, including
`PredecessorStillInForce` — the case that puts two live issues of one
drawing on the shop floor. 13 diagnostics, `TEMPEST-EAD-001`–`013`.

---

## 5. Persistence

§35's full cycle, run against the real document-backed store for every
library: create, persist, reload, compare, revise, retrieve the
historical revision, supersede, retrieve the superseded record.

Plus a **blanket JSON round-trip guard** over every `P05` type —
serialise, deserialise, compare the rendered form. This is the shape that
would have caught `P03`'s `CostFigure` defect: a type whose constructor
the serialiser cannot call now fails in the suite rather than the first
time a catalogue reads it back.

Nested structure, enums, nullable fields, lists, `ReferencePin`s, dates
and the whole governance graph are asserted individually, not just
"round-trips without throwing".

---

## 6. Defects found and fixed

**Two, both found by the tests rather than by reading.**

1. **`VerificationArtefact.Standing` accepted whitespace as a reason.**
   It tested `NotApplicableReason` for null, so a whitespace-only string
   counted as a reason and silently retired a requirement. Blank is not a
   reason: a whitespace value now leaves the artefact at `NotPerformed`.
   Found by the adversarial tests.

2. **`DocumentRelationship.Kinds.Supersedes` redeclared a canonically
   owned value.** `"supersedes"` is platform-wide and owned by
   `EngineeringDomain.GovernanceRelationshipKinds` (`ADR-0105`). Caught by
   the repository's own `EngineeringVocabularyConsistencyTests` on the
   full Desktop run. Removed from `E5` entirely — aliasing the constant
   does not help, because C# inlines it and reflection still sees a second
   declaration. `E5` now names the canonical constant in `Kinds.All` and
   declares only the values it owns.

The second is exactly the drift §32 warns about, and the repository's
existing guard caught it without my having to look for it.

---

## 7. Registers

| Register | Before | After | Change |
|---|---|---|---|
| ADR Register | 135 | 138 | `ADR-0136`–`ADR-0138` |
| Architecture Document Register | 43 | 44 | `Group E Engineering Assets.md` |
| Namespace Register | 75 | 81 | Six `P05` namespaces |
| Interface Register | 272 | 283 | Eleven `P05` interfaces |
| Governance Index | 135 ADRs stated | 138 | Corrected |
| Exception Register | 99 | 99 | Unchanged — `P05` declares no new exception type |

**Three ADRs, not five.** §40 says not to write an ADR because a class
exists. The five packages embody three architectural decisions: the
lifecycle and the template-revision promise; referencing rather than
duplicating three existing models; and keeping engineering judgement out
of Booleans.

**One drift corrected.** `IReferencePinResolver` and
`CatalogPinResolver<T>` are generic reference-data infrastructure and were
placed in `P03`'s commercial namespace at `Group D` — mine, last
programme. Moved to `Tempest.Core.ReferenceData` so `P03` and `P05` share
one mechanism rather than `P05` depending on a commercial namespace for a
pin lookup. `Tempest.Core.ReferenceData` moves 32 → 33 files, stated in
the register.

---

## 8. What P05 did not touch

No `WP16` work, no Desktop functionality, no Companion functionality, no
release tags, no release claims, no `P02`, `P03` or `P07` behaviour, no
UI. Changes outside `Tempest.Core.EngineeringAssets`:

- `IReferencePinResolver` / `CatalogPinResolver<T>` relocated to
  `ReferenceData` (§7).
- The `P05` registration block in `TempestHost`, added after `P03`'s.

---

## 9. Known gaps and deferred work

**No engineering assets.** §0. The templates and document structures an
organisation uses are its own intellectual property; shipping them makes
them a thing to be upgraded rather than edited.

**No operational workflow.** `P05` holds artefacts. Running a review,
chasing an action to closure, routing a document for approval — none of
that exists and none was in scope.

**No UI.** Deliberately, per §30.

**`E3` does not resolve requirements against the Requirements service.**
`VerifiedRequirement` carries the `Guid` and validation reports what it
can from the artefact alone; confirming the requirement exists would need
`IRequirementsService` as an optional collaborator, which is a small
addition left for integration.

**Pin resolution is optional and partial.** Validation checks only
libraries a resolver was supplied for. Pins into unknown libraries are
silently unchecked rather than reported, because reporting them would mean
warning about every library `P05` does not know about.

**`E5` holds no attachment mechanism of its own**, by design — content is
`EngineeringData`'s. A caller wanting bytes goes there.

---

## 10. Git

| Commit | Subject |
|---|---|
| `6b62bd9` | P05 shared core and E1 / WP05.1: engineering templates |
| `cf51891` | P05 E2 / WP05.2: calculation packs |
| `9548529` | P05 E3 / WP05.3: verification artefacts |
| `ba335be` | P05 E4 / WP05.4: design review packs |
| `97b39bb` | P05 E5 / WP05.5: technical documentation system |
| `6ea2b78` | P05: tests, host registration, and a whitespace-reason defect fixed |

Branch: `claude/tempestos-a4-bearing-library-unobtf`. No pull request
opened; none was asked for.
