# Integration Phase — Completion Report

| | |
|---|---|
| **Phase** | Population → Integration + real engineering validation |
| **Branch** | `claude/tempestos-a4-bearing-library-unobtf` |
| **Date** | 2026-09-07 |
| **Companion documents** | [Seed Data Review Set](../../governance/Data/Seed%20Data%20Review%20Set.md) · [Seed Data Sources Register](../../governance/Data/Seed%20Data%20Sources%20Register.md) · [Population Phase Completion Report](./Population%20Phase%20Completion%20Report.md) |

---

## 1. Baseline

Verified at the actual `HEAD` before any work began, not assumed:

```
$ git rev-parse HEAD
d3b9904f42bccacc37487820cbcad8d7f53b4172
$ git status --short
(no output)
```

Debug build re-run at that commit: **0 warnings, 0 errors**. The stated
baseline of 4,948 Core + 474 Desktop and governance 13/3/0 was confirmed
before proceeding.

---

## 2. Scenario

**A mounting bracket, machined from bar, carrying a static axial load.**

Chosen from the data that exists rather than the data being chosen to fit
it. The population phase already seeded `REQ-BRACKET-001`, a calculation
pack, a verification artefact, a design review and a technical document all
about this bracket, and the six seeded materials genuinely disagree on the
two axes a bracket turns on. A shaft scenario would have needed fatigue
data nobody has; a bearing-supported assembly would have needed more than
one bearing family.

The two selection criteria are **explicitly labelled assumptions** and are
marked `ReferenceValueOrigin.DerivedByTempestOS` in the requirement set
itself, because `REQ-BRACKET-001` states that a positive margin is required
and sets no load. Nothing was invented and presented as a requirement.

| Criterion | Value | Standing |
|---|---|---|
| Minimum specified proof stress | 200 MPa | ASSUMPTION — stands in for a design stress no requirement states |
| Maximum density | 3.0 g/cm³ | ASSUMPTION — expresses "this is carried by hand" |

---

## 3. Data consumed

Real seeded records, read through their governed catalogues. Nothing was
copied into a parallel test model.

| Library | Records used |
|---|---|
| Materials | All six: `mat-s355j2`, `mat-1-4301`, `mat-1-4404`, `mat-6082-t6`, `mat-5083-o-h111`, `mat-cw004a` |
| Manufacturing | All four Proto Labs capability records |
| Engineering rules | `MFG-CAP-001/002`, `TDE-DES-001/002` |
| Standards | The citation index, resolved from materials, processes and rules |
| Engineering assets | Template, calculation pack, verification artefact, design review, technical document |
| Commercial | Three suppliers, one cost, two lead times |
| Knowledge | Worked example, challenge, Academy lesson |
| Requirements | `REQ-BRACKET-001`, a real registered requirement |

### The engineering result

Offered all six grades against the two criteria, **6082-T6 is the only
survivor**, on its real published numbers — 260 MPa minimum proof stress
for 20–150 mm bar, 2.70 g/cm³.

| Grade | Standing | Why |
|---|---|---|
| 6082-T6 | **Constraints satisfied** | 260 MPa ≥ 200 MPa, 2.70 ≤ 3.0 g/cm³ |
| S355J2 | Eliminated | Strong enough at 355 MPa; 7.85 g/cm³ fails the mass constraint |
| 1.4301, 1.4404 | Eliminated | 8.0 g/cm³ |
| 5083-O/H111 | **Unresolved**, not eliminated | Its density is *absent* — see §6 |
| CW004A | Not offered | Copper is outside the acceptable families |

This is a real engineering conclusion drawn from real published data, and
the elimination reasons are the right ones: the result is a selection, not
a filter that happened to leave one thing standing.

---

## 4. Cross-domain references

| From | To | Proven by |
|---|---|---|
| Material / fastener / bearing / process / rule | Standards index | `EveryStandardCitation_ResolvesToARegisteredStandard` |
| Calculation pack input | Material **revision** | `TheCalculationPackPinsTheMaterialRevisionItActuallyRead` |
| Calculation pack | Template **revision** | `AnEngineeringResultTracesBackToItsSourceDocument` |
| Worked example step | Material revision | `TheWorkedExampleTeachesFromTheSameRecordTheScenarioSelected` |
| Verification artefact | Registered requirement | `TheVerificationArtefactNamesARequirementThatReallyExists` |
| Design review | Calculation pack + verification + requirement | `TheAssetChainReferencesTheSameRecordsSelectionChose` |
| Supplier | Material and process records | `TheCommercialRecordsPointAtRealProcessesAndMaterials` |
| Cost / lead time | Process record | same |
| Selection result | Material revision (`ReferencePin`) | `TheSurvivingCandidateCarriesThePinThatMakesTheChoiceReproducible` |

**A citation may legitimately disagree with its index.** Two Aalco
datasheets cite different editions of EN 573-3 (2009 and 2019); the bearing
pages cite ISO 15:2011 where the index holds 2017. Each citation keeps the
edition its own document stated, and the EN 573-3 index record deliberately
records no edition because its citers disagree.

---

## 5. Engineering workflow

| Step | Outcome |
|---|---|
| Requirement | ✅ `REQ-BRACKET-001` is a real registered requirement; the verification artefact names its id |
| Engineering reference data | ✅ Read through the governed catalogues, with identity, revision, provenance and lifecycle preserved |
| Material selection | ✅ Ran through `MaterialSelectionService`; one survivor on real numbers |
| Manufacturing | ✅ Ran through `ManufacturingDecisionService.ScreenCatalogueAsync`; milling confirmed viable |
| Engineering calculation | ⚠️ The pack carries method, assumptions and a pinned material basis, and **no result** — two of its three inputs are not established. Traced, not computed. |
| Verification evidence | ⚠️ `NotPerformed`, correctly: there is no calculation result to verify |
| Design review | ⚠️ `NotConcluded`, no participants — the review has not been held |
| Technical documentation | ✅ Present, pinned to two material revisions |
| Commercial consideration | ✅ Cost and lead time resolve to the chosen route; both remain information, not decisions |

The three ⚠️ steps are the population phase's deliberate incompleteness, not
integration failures. They are what an honest engineering record looks like
before the engineering has been done.

---

## 6. Refusal behaviour

Twelve refusal tests. **No safeguard was weakened to make anything pass.**

| Attempt | Result |
|---|---|
| Use Draft reference data where Released is required | Zero candidates. Lifting the requirement returns all six, proving it was the gate and not an empty library |
| Evaluate Draft rules | Five rules registered, none evaluated |
| Read a property the record does not carry | `Unresolved` — never defaulted, never failed |
| Compare a density against a pressure | Does not pass, though 2.70 is numerically ≤ 3.0 |
| Operate on a non-existent record | `ReferenceRecordNotFoundException` |
| Edit a Released record | `ReleasedReferenceImmutableException` |
| Demote a Released record to Draft | `InvalidReferenceStateTransitionException` |
| Register a second record with a live designation | `DuplicateReferenceKeyException` |
| Use a superseded record | Drops out of selection; still retrievable by explicit revision |
| Promote a supplier capability to a decision | Everything stays `Prospective` / `Offered` |
| Screen an unsuitable process | **`Unresolved`, not `Eliminated`** — see below |
| Forge a review on the shipped corpus | The shipped corpus is asserted untouched by the test-only mechanism |

### The most interesting refusal

Injection moulding, screened for an aluminium bracket, comes back
**Unresolved rather than Eliminated**.

That is correct and more careful than eliminating it. The process records
carry only `Suitable` compatibility entries, because the supplier publishes
what it offers and says nothing about what it refuses. Absence from that
list is not evidence of incompatibility, and the service declines to
manufacture the inference — a system that eliminated on silence would guess
wrong the first time a supplier forgot to list something.

A counterpart test proves the logic is sound and the gap is in the data:
given an explicit `NotSuitable` entry, the same service eliminates the same
process.

### The `TEMPEST-MFG-005` contradiction — surfaced, not resolved

`TheTurningWallThicknessContradictionIsVisible_NotResolved` asserts that
the turning record still carries no wall-thickness capability, that the
supplier's 0.51 mm figure survives as a constraint naming the rule it
conflicts with, and that the library still validates clean. The
disagreement remains findable by a reviewer, which is the point: an
engineering disagreement that becomes invisible has been resolved by
accident.

---

## 7. Persistence

Run through the **real host and the real file-backed store**, across host
restarts — not against an in-memory catalogue a test built for itself.

**create → persist → reload → inspect**, all confirmed after a restart:
identifier; quantity (260, not 260 000 000); unit symbol (`MPa`, unconverted);
the conditions that make the number mean anything (`20 mm to 150 mm`);
value origin; provenance organisation and verification status; lifecycle
state; the resolved `StandardId` on a citation; nested objects (assumptions,
governing equations); and the `ReferencePin` inside a calculation input.

**revise → persist → reload historical revision → inspect:** three separate
host sessions. 6082-T6's proof stress was corrected from 260 MPa to
240 MPa in session two. In session three the current record reads 240 MPa,
the calculation's pinned revision still reads **260 MPa**, and the trace
reports both — `HasMovedOnSincePinned` is `true`.

An engineer can reproduce what TempestOS knew when the calculation was
performed.

---

## 8. Traceability

`EngineeringTraceRegister` answers "where did this engineering result come
from?" by resolving each pin **at the revision the artefact pinned**, never
the current one.

For `TDE-CPK-001` the chain resolves end to end:

```
calculation TDE-CPK-001
  └─ input IN-MATERIAL
       └─ Materials/mat-6082-t6 r1
            └─ Aalco Metals Limited
               "Aalco technical datasheet — Aluminium Alloy - Commercial
                Alloy - 6082 - T6 Extrusions"
               "Mechanical properties, rod and bar 20 mm to 150 mm band"
```

The register reports four things a reviewer needs and one they might not
want: dangling references, references whose record has moved on,
**inputs that rest on nothing but the pack's own assertion** (two, in this
pack), and whether the whole calculation rests on verified data (it does
not, because nothing is verified). A broken pin is reported as a dangling
reference with a reason, never by silently falling back to today's values.

---

## 9. UI / application integration

Deliberately small. **No view was added and no navigation was changed.**

Two read models in a new `Tempest.App.Engineering` namespace, following the
`ProjectTaskRegister` / `ProjectMilestoneRegister` pattern exactly — no
state, no caching, no persistence — constructed in `WorkspaceHost` beside
the project registers:

- **`ReferenceLibraryRegister`** — what reference data is held and whether
  it may be relied on. It computes `UnusableReason` once rather than
  leaving each surface to compose it, and distinguishes *empty* from
  *populated but unusable*, which is the difference the reference lifecycle
  exists to protect.
- **`EngineeringTraceRegister`** — §8.

Five Avalonia tests exercise these through the desktop application's own
`WorkspaceHost`: a freshly started application, finding populated data and
being told why it is not usable, following a calculation back to its source
document, and seeing the old revision after the data is corrected.

---

## 10. Defects discovered

Three real defects, all found by integration and all fixed. Two were
invisible to the population phase because nothing there rendered a
designation or resolved a broken pin.

| # | Defect | Root cause | Fix | Regression test |
|---|---|---|---|---|
| 1 | Every indexed standard rendered as **`EN EN 10025-2:2019`** | The seed wrote the body prefix into `Designation`, and `FullDesignation` prefixes `Body.Code` again. The model's contract is explicit, so the data was wrong | All 14 designations reduced to the bare number | `NoStandardRepeatsItsOwnBodyCodeInItsDesignation` |
| 2 | A pin naming a non-existent revision **threw out of the trace register** | The catch covered `ReferenceDataException` only; the catalogue raises `ArgumentOutOfRangeException` for a revision outside the range that exists | Catch widened to both | `ADanglingReferenceIsReportedRatherThanFallingBackToTheCurrentRevision` |
| 3 | A test named for process screening **never ran the screening service** — it compared compatibility lists | Written against the data rather than the service | Rewritten to run `ScreenCatalogueAsync`; it immediately exposed the `Unresolved`-not-`Eliminated` behaviour in §6 | `AnUnsuitableManufacturingProcessDoesNotSurviveScreening` plus a new counterpart test |

A traceability surface that crashes on the broken pin it exists to find is
worse than useless, so defect 2 mattered more than its size suggests.

---

## 11. Colour Review Board

Nine perspectives, against the integrated result.

### 🔴 RED — 0

No release blocker was found.

### 🟠 AMBER — 3 (2 remediated, 1 deferred with reason)

**AMBER-1 — A test asserted what it did not exercise. ✅ Remediated.**
*Testing / IV&V.* `AnUnsuitableManufacturingProcessDoesNotSurviveScreening`
compared material-compatibility lists and never called the screening
service, so it would have passed with the service entirely broken. Rewritten
to run `ScreenCatalogueAsync` against the real released records. Doing so
immediately surfaced genuine behaviour the list comparison had hidden
(§6), and a counterpart test was added to prove the logic is sound where
the data supports it. Re-verified: 12/12 refusal tests pass.

**AMBER-2 — The traceability register crashed on a broken pin. ✅ Remediated.**
*Architecture / code quality.* Defect 2 in §10. The register existed
specifically to report broken references and instead propagated an
exception into whatever surface asked. Catch widened to the two exception
types the catalogue actually raises, with the reasoning recorded at the
catch site. Re-verified by a test that deliberately breaks two pins in
different ways.

**AMBER-3 — `ReviewerPrincipalId` is caller-supplied and unattested. ⏸️ Deferred, out of scope.**
*Security.* Reaching `Released` requires writing a reviewer's name, a date
and `VerifiedAgainstSource` into a record's provenance. Nothing binds those
fields to the principal actually signed in, so a reviewer's name can be
written by anybody with write access. The underlying document revision does
record the real principal, so a forgery is *detectable* — but the
provenance is not self-attesting.

*Why it is not remediated here.* The fix is a governed review action that
takes the reviewer from `ICurrentPrincipalAccessor`. That is new capability,
and §2.4 of this phase's own instruction reserved the approval/release
action as a human action precisely because no authorised mechanism exists.
Building one would have answered a question this phase was told to leave
open. **Recommended as the first task of the next phase**, ahead of any
further UI work, because until it exists no release can be trusted.

Pre-existing, not introduced here. Recorded in the Seed Data Review Set §6.

### 🟡 YELLOW — 7

| # | Perspective | Finding |
|---|---|---|
| Y-1 | Testing / data | Process compatibility records carry only `Suitable` entries, so screening can confirm a viable process but cannot eliminate an unsuitable one — it returns `Unresolved`. Correct behaviour over incomplete data; the gap is that no record has a `NotSuitable` entry. |
| Y-2 | Architecture / data honesty | The desktop application registers **two fictional sample materials** into the real Materials Library at start-up (`SAMPLE-MAT-001`, `sample.fictional-test-alloy`). They are honestly labelled in provenance — `SourceOrganisation` "TempestOS sample module", `SourceDocument` "Fictional test fixture — not a real material standard" — and neither can reach `Released`, so no reasoning service can consume them. But they sit in the same list an engineer browses, and the two sample modules disagree on `SourceClassification` ("TestFixture" vs "metal"), so classification is not a reliable discriminator. Provenance is. Now asserted by a test. |
| Y-3 | Architecture | `EngineeringTraceRegister` resolves pins into Materials and Templates only. A pin into any other library is reported unresolved with a reason rather than silently dropped, but adding a library is a deliberate edit to this class. |
| Y-4 | Architecture | `ReferenceLibraryRegister` names its six catalogues explicitly. A seventh P01 library would not appear until somebody edits it — deliberate (reflection would mean nobody ever decided what an engineer sees), but it is a maintenance obligation. |
| Y-5 | Code quality | `TracedReference` is a twelve-parameter record encoding a resolved/unresolved union with eight nullable fields. It works and is documented, but a discriminated shape would express it better. |
| Y-6 | Documentation / governance | `IReferenceLibraryRegister` and `IEngineeringTraceRegister` live in `Tempest.App`, outside the Interface Register's declared `src/Tempest.Core/` scope — the same disclosed boundary as `IProjectDependencyRegister`, now affecting three interfaces. |
| Y-7 | UI / accessibility | Nothing surfaces `UnusableReason`, the trace, or the fictional-vs-sourced distinction to a user yet. The protection these provide is real but currently only reachable from tests. |

### 🟢 GREEN — 12

| Perspective | Observation |
|---|---|
| Architecture | The integration used the existing read-model pattern exactly; no competing architecture, no orchestration engine, no second calculation engine |
| Architecture | No second persistence mechanism; everything writes and reads through the governed catalogues |
| Code quality | 0 build warnings in both configurations; no `TODO`, `FIXME` or `HACK` in new code |
| Code quality | `ConfigureAwait(false)` on every await in the new application code (15/15) |
| Testing / IV&V | 32 new tests; the full suite is 5,454 with **0 skipped** — nothing was disabled to reach green |
| Testing / IV&V | Refusal coverage is broader than the happy path, which is the right ratio for an engineering platform |
| Security | No secrets, credentials or personal data in new code |
| Security | The test-only review mechanism is quarantined in one internal class with a reviewer id that cannot be mistaken for a person, and a test asserts the shipped corpus is untouched by it |
| Persistence / durability | Units, conditions, provenance, pins, lifecycle state and nested objects all survive a real host restart; historical revisions remain readable after correction |
| Cross-platform | No `DateTime.Now`, no hard-coded path separators, no platform assumptions in new code; Desktop tests run headless |
| Documentation / governance | Registers reconciled in the same pass that broke them; the phase transition is recorded in `PROJECT_STATUS.md` |
| Release engineering | Governance health returned to the certified baseline of 13 pass / 3 pre-existing warnings / 0 fail |

**Verdict: PASS** — 0 RED; 2 of 3 AMBER remediated and re-verified within
the phase, the third out of scope by instruction and recorded with a named
next step.

---

## 12. Remediation

| Finding | Root cause | Remediation | Regression test | Re-verified |
|---|---|---|---|---|
| AMBER-1 | Test written against data, not the service | Rewritten to call `ScreenCatalogueAsync`; counterpart test added for the explicit-incompatibility case | `AnUnsuitableManufacturingProcessDoesNotSurviveScreening`, `AnExplicitlyUnsuitableMaterialEntryDoesEliminateTheProcess` | ✅ 12/12 refusal tests |
| AMBER-2 | Catch too narrow for the exception the catalogue raises | Widened to `ReferenceDataException or ArgumentOutOfRangeException`, reasoning recorded at the catch | `ADanglingReferenceIsReportedRatherThanFallingBackToTheCurrentRevision` | ✅ 5/5 traceability tests |
| Defect 1 (§10) | Seed duplicated the body prefix | 14 designations corrected | `NoStandardRepeatsItsOwnBodyCodeInItsDesignation` | ✅ 26/26 population tests |
| AMBER-3 | No authorised review mechanism exists | **Deferred** — out of scope by §2.4; recommended as the next phase's first task | — | — |

Full suite re-run after all remediation: **Debug and Release, 4,975 Core +
479 Desktop, 0 failed, 0 skipped.**

---

## 13. Outstanding work

### 13.1 Remaining population
Unchanged from the population phase: springs/gears/components, fastener
mechanical properties, bearing families beyond deep groove ball, six
standards titles, the failure and lessons library, and all of P04 and P07.
**New:** no process record carries a `NotSuitable` material entry (Y-1).

### 13.2 Remaining integration
- A governed review action (AMBER-3) — the blocker on everything downstream.
- Traceability for verification artefacts and design reviews; only
  calculation packs are traced today.
- Pin resolution beyond Materials and Templates (Y-3).

### 13.3 Remaining UI
Everything. No view, no navigation entry, no menu item was added. A
Reference Data surface and a Traceability surface are the two the read
models were shaped for (Y-7).

### 13.4 Remaining end-to-end scenarios
The bracket chain is exercised but not *completed*: the calculation has no
result, the verification no standing, the review no decisions. Completing
it needs a design load, which needs a requirement somebody writes. That is
the natural next scenario, and it needs no new architecture.

### 13.5 Existing YELLOW technical debt
The six YELLOW items from the foundation certification are untouched and
none blocked this phase. The population phase's five debt items stand. This
phase adds seven (§11).

---

## 14. Recommendation

**TempestOS is ready for the next round of real engineering scenarios, with
one condition.**

What this phase establishes is that the architecture holds up under real
data. Records are consumed through governed services; cross-domain
references resolve; a calculation can name the exact revision of the exact
record it stood on, and still resolve it after that record has been
corrected and the process restarted three times; and the system refuses,
correctly and in twelve distinct ways, to do things it does not have the
grounds to do. The three defects found were real, small, and are fixed.

The condition is AMBER-3. Every seeded record is `Draft`, and the only path
to `Released` runs through a provenance field anybody can write. Until a
governed review action exists, releasing data means trusting a name typed
into a string — and every downstream capability, including every scenario
richer than this one, depends on released data.

So: **next phase begins with the review mechanism, then a human review of
the twelve constants** (the strongest release candidates — primary,
public-domain, still online), **then the first scenario that computes an
actual number.** UI work should follow that, not precede it: a surface over
data nobody may rely on shows an engineer a library they must not use.
