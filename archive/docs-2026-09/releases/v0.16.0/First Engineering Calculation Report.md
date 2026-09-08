# First Real Engineering Calculation — Completion Report

| | |
|---|---|
| **Phase** | Governed release → calculation → verification → traceability |
| **Branch** | `claude/tempestos-a4-bearing-library-unobtf` |
| **Date** | 2026-09-07 |
| **Companions** | [Integration Phase Report](./Integration%20Phase%20Report.md) · [Seed Data Review Set](../../governance/Data/Seed%20Data%20Review%20Set.md) · [ADR-0143](../../adr/ADR-0143-a-calculation-carries-the-reference-revision-it-stood-on-and-the-engine-can-read-it-back.md) |

---

## 1. Baseline

Verified at the actual `HEAD` before any work began:

```
$ git rev-parse HEAD
32717104de9be1f8036b5951d477884e8b56b5e2
$ git status --short
(no output)
```

Debug build re-run at that commit: **0 warnings, 0 errors**. The stated
baseline of 4,975 Core + 479 Desktop and governance 13/3/0 was confirmed
before proceeding.

---

## 2. Calculation

**Bracket section check** — `calc.bracket-section-check`.

A first-order direct-stress and mass check on one bracket section. It is
the calculation an engineer does on the back of a drawing: divide the load
by the area, compare against the material's allowable, weigh the part.

**It was not invented for this phase.** The bracket's seeded calculation
pack `TDE-CPK-001` already declares `sigma = F / A` and
`margin = (Rp0.2 / sigma) - 1`, and the seeded worked example
`WEX-MAT-001` already teaches from those equations against the 6082-T6
record. Implementing anything else would have left the platform's
documentation describing one calculation and its code performing another.

**Why mass is included.** The bracket is carried, so it has two acceptance
criteria and always did — the integration phase's material selection
screened on a strength floor and a density ceiling together. Mass is
arithmetic over the same inputs, not a second calculation family, and it is
what makes the missing 5083 density bite in the calculation exactly as it
bit in selection.

### What was reused rather than built

The framework already existed and was not replaced: `ICalculationDefinition`,
`ICalculationEngine`, `CalculationRecord`, `CalculationContext`, and five
worked definitions already dimensioned on `Quantity<TDimension>`. This phase
added **one definition** to that extension point.

Three gaps were real and were closed (ADR-0143): a calculation could not
say which *revision* of a material it used; the engine was write-only; and
nothing stood between the catalogue and the pure calculation.

---

## 3. Engineering basis

### Equations

```
sigma  = F / A                        direct stress on the minimum section
margin = (allowable / sigma) - 1      margin of safety
mass   = density x area x length      prismatic mass estimate
```

Acceptance: `margin >= 0` **and** `mass <= limit`.

### Assumptions — all five recorded on the calculation and persisted with every record

| Assumption | Justification |
|---|---|
| Loading is static and purely axial across the minimum section | The closed-form method assumes it; a bending or fatigue case needs a different check |
| The area given is the minimum area resisting the load, net of holes | The calculation cannot see geometry; it divides by the area it is given |
| The delivered material meets the specified minimum for the size band supplied | Published proof stresses are specification minima for a stated form and size band, not measured properties of a bar |
| The member is a prism of constant section over its stated length | Mass is density × area × length; a tapered part weighs less |
| Acceptance allows one part in a billion of relative slack | So a section exactly on its limit is not failed by unit-conversion rounding — see §10, Defect 1 |

### Declared limits

The calculation states on its own face that it does not consider bending,
buckling, stress concentration, bearing at the fixing holes, or fatigue.
It is a hand calculation and says so rather than leaving a reader to assume
otherwise.

### Standards

**No standard is cited by the calculation, deliberately.** The material
properties it consumes cite EN 755-2 through their own records, and that
citation is traceable (§7). The closed-form method itself is textbook
mechanics, not a standard's method, and inventing a citation to make the
calculation look better sourced would have been worse than citing nothing.

---

## 4. Data

| Record | Revision at use | Property used | Value | Provenance |
|---|---|---|---|---|
| `mat-6082-t6` | pinned at execution | `YieldStrength` | 260 MPa | Aalco datasheet — 6082-T6 Extrusions, rod and bar 20–150 mm band |
| `mat-6082-t6` | same revision | `Density` | 2 700 kg/m³ (2.70 g/cm³) | same datasheet, physical properties table |

Released through the governed mechanism by a signed-in principal before the
calculation would run. The result carries the `ReferencePin` — library,
record and revision — so the exact revision is persisted with the numbers.

**Nothing else was populated.** No library gained a record for this phase.

---

## 5. Numerical verification

Every expected value below was derived **by hand, from first principles**,
and is written out in the test files so a reviewer can repeat it without a
calculator and without reading the implementation.

```
A = 60 mm2 = 6.0e-5 m2      L = 150 mm = 0.15 m
6082-T6: allowable 260 MPa, density 2700 kg/m3
```

| Case | Derivation | Expected | Result | Verdict |
|---|---|---|---|---|
| **Nominal**, F = 12 kN | 12 000 / 6.0e-5 = 2.0e8 Pa | **200 MPa** | 200 MPa | ✅ |
| | 260/200 − 1 = 1.3 − 1 | **0.30** | 0.30 | ✅ |
| | 2700 × 6.0e-5 × 0.15 | **0.0243 kg** | 0.0243 kg | ✅ |
| | 0.0243 ≤ 0.050 and margin ≥ 0 | **MeetsCriteria** | MeetsCriteria | ✅ |
| **Boundary**, F = 15.6 kN | 2.6e8 × 6.0e-5 = 15 600 N | **margin 0.00** | ~0 (−1.1e-16) | ✅ |
| **Stress failure**, F = 18 kN | 18 000 / 6.0e-5 = 3.0e8 Pa | **300 MPa** | 300 MPa | ✅ |
| | 260/300 − 1 | **−0.133333…** | −0.133333… | ✅ |
| **Mass failure**, limit 20 g | 24.3 g > 20 g, stress still passes | **DoesNotMeetCriteria** | DoesNotMeetCriteria | ✅ |
| **S355J2**, same geometry | 355/200 − 1 = 1.775 − 1 | **0.775** | 0.775 | ✅ |
| | 7850 × 9.0e-6 | **0.07065 kg** | 0.07065 kg | ✅ |
| | passes on strength, fails on mass | **DoesNotMeetCriteria** | DoesNotMeetCriteria | ✅ |

The S355J2 case reaches numerically the same conclusion the integration
phase's material selection reached by screening: strong enough by a wide
margin, and nearly three times too heavy.

### Independent verification, as an artefact

`BracketEngineeringRecordService.RecordVerificationAsync` takes the
independently-computed figures **as parameters** and compares them. It
cannot re-run the production formula — that is the difference between
verifying and asserting — and a test proves it records `Failed` when the
independent checker disagrees (using the 200 MPa figure from the wrong size
band, as a real checker might).

The verification artefact `TDE-VER-001` moved from `NotPerformed` to
`Passed`, performed by a principal different from the reviewer who released
the material.

### Unit correctness

- The same physical problem stated in mm/N and in m/kN gives identical
  results, asserted to 1e-12.
- Every input is a `Quantity<TDimension>`; a dimensionally invalid input
  **does not compile**, which is why no test asserts it throws.
- The result keeps its dimensions: applied stress is a `Quantity<Pressure>`
  in MPa, mass a `Quantity<Mass>` in kg. The margin is dimensionless
  because a ratio of two stresses genuinely is.

### Determinism

No clock, no randomness, no catalogue lookup, no shared state inside
`Calculate`. Three separate executions of the same input produce equal
results, asserted by record equality.

---

## 6. Persistence

Run through the **real host and the real file-backed store**, across host
restarts.

**create → calculate → persist → restart → reload → inspect.** After a
restart the reloaded record retains: the result and its units (200 MPa, not
2.0e8, and `MPa` not `Pa`); the margin and mass; the outcome and both
criterion flags; the material record id **and its pinned revision**; all
five assumptions; the intermediate working; the referenced material ids;
the executing principal; the execution timestamp; the document revision
number; and the validation outcome.

**revise → persist → reload historical revision → inspect.** Three host
sessions. In session two the material was corrected from 260 MPa to
240 MPa — by superseding it, because a released record cannot be edited,
which is the lifecycle working as intended. In session three the reloaded
calculation still pins revision N, still reads 260 MPa and margin 0.30, and
that revision is still retrievable from the catalogue so the calculation
can be reproduced from first principles rather than merely re-read from its
own record.

---

## 7. Traceability

```
RESULT  margin 0.30, MeetsCriteria
  └─ calculation record  <guid>, calc.bracket-section-check, revision 1
       └─ input IN-MATERIAL  260 MPa
            └─ ReferencePin  Materials / mat-6082-t6 @ rN
                 └─ material revision rN
                      └─ YieldStrength 260 MPa
                           └─ Aalco Metals Limited
                              "Aalco technical datasheet — Aluminium Alloy -
                               Commercial Alloy - 6082 - T6 Extrusions"
                              "Mechanical properties, rod and bar 20 mm to
                               150 mm band"
```

Closed from either end: the calculation pack's `IN-MATERIAL` input carries
the same pin the result does, and `ExecutionRecordIds` points at the engine
record. No bespoke traceability mechanism was built — the pin, the
catalogue's revision history and the P05 pack's own fields already existed.

---

## 8. Application

Through `WorkspaceHost`, alongside the existing read models. **No view was
added and no navigation was changed.**

An engineer can, and three Avalonia tests demonstrate:

1. see which reference data is held and that it may not yet be used;
2. be refused when they try to calculate on Draft data;
3. review and release a record **as themselves** — the reviewer comes from
   the session principal;
4. see the same library view change to usable;
5. supply the load and geometry and execute;
6. read the result, the margin and the acceptance state;
7. inspect the five assumptions and the provenance of the data used;
8. retrieve the calculation again, typed, with its pin intact.

A third test proves that with nobody signed in the application refuses to
record a review and leaves the record untouched.

---

## 9. Refusal behaviour

| Attempt | Result |
|---|---|
| Calculate on a **Draft** material | `MaterialNotReleased`, with the state named |
| Calculate on a **superseded** material | `MaterialNotReleased`, naming Superseded |
| Calculate on a material that **does not exist** | `MaterialNotFound` |
| Calculate where a **required property is missing** (5083 density) | `RequiredPropertyMissing` — see below |
| Calculate where a property has the **wrong dimension** | `PropertyDimensionWrong`, naming both dimensions |
| Zero or negative load, area, length, allowable, density or mass limit | `CalculationInputInvalidException`, ten cases |
| Read a stored record as the **wrong result type** | `CalculationException` — see §10, Defect 2 |
| Read an **unknown** record id | `null`, which is the honest answer |
| Review with **nobody signed in** | `ReferenceReviewException`, record untouched |
| Release **without verification** | `ReferenceReviewException` |
| Verify a record that is **already verified** | `ReferenceReviewException` naming the existing reviewer |
| Write a **refused** check into a pack or verification artefact | `ArgumentException`; the seeded artefact stays `NotPerformed` |

### The 5083 case

Its source publishes a density of 265 g/cm³ — impossible. The population
phase omitted the property rather than substituting 2.65. This is where
that decision earns its keep: the calculation **refuses** rather than
producing a mass nobody can stand behind, and the refusal says so in those
words. A test asserts the property is still absent.

### `TEMPEST-MFG-005`

Untouched. The selected calculation does not involve turning or wall
thickness, so the contradiction was neither encountered nor resolved.

---

## 10. Defects discovered

Three, all found by testing this phase's own work, all fixed.

| # | Defect | Root cause | Fix | Regression test |
|---|---|---|---|---|
| 1 | **A section exactly at its allowable was reported as failing** | 60 mm² converts to `5.9999999999999995e-05` m², so 15.6 kN lands one ulp above 260 MPa and a bare `applied <= allowable` fails the bracket by ~1e-14 % | Acceptance allows `AcceptanceRelativeTolerance = 1e-9` — seven orders above double rounding, seven below the precision any material property is known to. Named, public, justified on the constant, and declared as an assumption that travels with every record | `AtExactlyTheAllowableStress…`, `TheAcceptanceToleranceReachesRoundingErrorAndNothingElse` |
| 2 | **Reading a record as the wrong result type silently succeeded** | `System.Text.Json` ignores unrecognised members and defaults the rest, so a bracket check read as a bolt shear result came back well-formed and full of zeroes | Records carry the result type they were written from; a mismatch is refused. Nullable, so records written before the field existed still read | `AMalformedPersistedCalculationIsReported_NotReturnedAsMissing` |
| 3 | **The verification artefact hard-coded `"TDE-CPK-001"`**, and the pack's density input was **back-computed** by dividing the mass out | Both written in haste; the second meant the result stated one of the two material properties it used and not the other | Pack reference is a caller parameter; `BracketSectionCheckResult` now carries `Density` alongside `AllowableStress` | Asserted in `TheCompleteChain…` |

A plausible-looking zero is the worst answer an engineering tool can give,
so Defect 2 mattered more than its size suggests.

---

## 11. Colour Review Board

### 🔴 RED — 0

### 🟠 AMBER — 2, both remediated within the phase

**AMBER-1 — The result did not state all the data it used, and a
cross-reference was hard-coded. ✅ Remediated.**
*Code quality / correctness.* Defect 3 in §10. `BracketSectionCheckResult`
carried `AllowableStress` but not `Density`, so the pack recorder
reconstructed the density by dividing the mass back out — exact, obtuse,
and wrong the moment the formula changes. Separately,
`RecordVerificationAsync` wrote `"TDE-CPK-001"` into every artefact it
touched, which would put a false cross-reference on any other pack's
verification. Both fixed; full suite re-run.

**AMBER-2 — A stored calculation could be read as the wrong type. ✅ Remediated.**
*Persistence / durability.* Defect 2 in §10. Re-verified by a test that
reads a bracket record as a bolt shear result and requires it to throw.

### 🟡 YELLOW — 6

| # | Perspective | Finding |
|---|---|---|
| Y-1 | Security / governance | `ReferenceReviewService` does not enforce separation of duties: the same principal may verify and release, and may verify a record they created. Deliberate — the platform cannot know an organisation's policy, and a one-engineer firm would be locked out of its own data. The acts are separate methods so a policy can be layered on later, and every act names its principal so a reviewer's independence is auditable after the fact. |
| Y-2 | Persistence | Calculation records written before `ResultTypeName` existed cannot be type-checked on read. They still load; they are simply unverifiable. No migration was written because no such record exists outside a developer's own store. |
| Y-3 | Architecture | `GovernedBracketCheckService` registers its calculation definition in its constructor and swallows `DuplicateCalculationException`. A constructor side effect is a smell; the alternative was a coupling discovered at run time by whoever forgot to register. Revisit when the container gains factory registration. |
| Y-4 | Architecture | One governed service per calculation, with property names written into it. Correct for one calculation and does not generalise — the shape should be extracted from the second, not guessed at now. |
| Y-5 | UI / accessibility | Still no view. Everything demonstrated here is reachable only from tests and from the host's own properties, so the protections are real but invisible to a user. |
| Y-6 | Documentation / governance | `GovernedBracketCheckService`, `BracketEngineeringRecordService` and `ReferenceReviewService` are concrete classes with no interfaces, so they do not appear in the Interface Register. Intentional — there is one implementation of each and nothing to substitute — but it means the register understates the phase's public surface. |

### 🟢 GREEN — 12

| Perspective | Observation |
|---|---|
| Architecture | One calculation definition added to an existing extension point; no `CalculationBase`, no orchestration layer, no second persistence, no second reporting framework |
| Architecture | The pure/`Calculate` contract was respected — no I/O, no lookup, no clock inside the calculation |
| Code quality | 0 build warnings in both configurations; no `TODO`, `FIXME`, `HACK`, or `DateTime.Now` in new code |
| Code quality | `ConfigureAwait(false)` on every await in new service code (12/12) |
| Testing / IV&V | Expected values derived by hand and written out in the tests; verification takes the independent figures as parameters and can fail |
| Testing / IV&V | 46 new tests; full suite 5,500 with **0 skipped** — nothing disabled to reach green |
| Security | The reviewer is taken from the signed-in principal and cannot be supplied; with nobody signed in the act is refused rather than attributed to "unknown" |
| Security | No secrets, credentials or personal data in new code |
| Persistence / durability | Units, conditions, pins, assumptions, author and lifecycle all survive restart; historical revisions remain readable after supersession |
| Cross-platform | No platform assumptions; Desktop tests run headless |
| Documentation / governance | ADR-0143 records the decision *and* what was deliberately not built; four registers reconciled in the same pass that broke them |
| Release engineering | Governance returned to the certified baseline of 13 pass / 3 pre-existing warnings / 0 fail |

**Verdict: PASS** — 0 RED; both AMBER remediated and re-verified within the
phase.

---

## 12. Test gate

| Configuration | Assembly | Passed | Failed | Skipped |
|---|---|---|---|---|
| Debug | `Tempest.Core.Tests` | 5,018 | 0 | 0 |
| Debug | `Tempest.Desktop.Tests` | 482 | 0 | 0 |
| Release | `Tempest.Core.Tests` | 5,018 | 0 | 0 |
| Release | `Tempest.Desktop.Tests` | 482 | 0 | 0 |

**5,500 tests, 0 failed, 0 skipped, 0 warnings, both configurations.**
Baseline was 5,454; this phase adds 46.

Governance: **13 pass / 3 warn / 0 fail.** The three warnings are the
pre-existing environmental ones the baseline already documents — no git
tags in this clone (twice) and two historical release folders without a
`WorkPackages.md`. None is attributable to this work.

---

## 13. The demonstration, in one line

> A material published by Aalco, reviewed and released by a signed-in
> engineer, supplied a 260 MPa proof stress at a pinned revision; a bracket
> of 60 mm² carrying 12 kN was found to have a margin of **0.30** and a mass
> of **24.3 g**, meeting both criteria; an independent hand calculation
> agreed and the verification artefact records **Passed**; the calculation
> persists, survives restart, still reads 260 MPa after the material was
> corrected to 240 MPa, and traces back to the datasheet table it came from.

---

## 14. Remaining work

### 14.1 Additional calculation modules
None started, deliberately. The next calculation should be chosen after
reviewing what this one exposed — in particular Y-4: the governed-resolution
shape should be extracted from a second real example, not designed now.

### 14.2 Additional population
Unchanged. Springs/gears/components, fastener mechanical properties, bearing
families beyond deep groove ball, six standards titles, the failure and
lessons library, all of P04 and P07, and no process record carrying a
`NotSuitable` material entry.

### 14.3 UI refinement
Everything. A Reference Data surface, a Traceability surface and a
calculation input/result surface are the three the services were shaped
for. This is now the largest single gap.

### 14.4 Broader end-to-end scenarios
The bracket chain is complete from requirement to verified, traceable,
persisted result. It does not yet flow into the design review's decisions
or the technical document's content, and no second component has been taken
through it.

### 14.5 Existing technical debt
The six foundation YELLOWs, the population phase's five, and the
integration phase's seven all stand. The integration phase's AMBER-3 —
`ReviewerPrincipalId` forgeable — **is closed by this phase**. This phase
adds the six in §11.

---

## 15. Recommendation

**The vertical slice works, and it is the right slice.**

TempestOS can now take a material published by a named source, have a
person release it under their own identity, perform a real numerical
calculation on it, produce a result that meets or fails stated criteria,
have that result independently verified, persist it, survive a restart, and
trace it back to the table in the datasheet it came from — while refusing,
in twelve distinct ways, to do any of that on data that cannot support it.

Two things should happen before a second calculation family:

1. **A user interface.** Every protection built over the last three phases
   is currently invisible. An engineer cannot see the library, the
   refusals, the trace or the result without writing code, and a
   correctness guarantee nobody can observe is not yet a product feature.
2. **A real human review of the twelve NIST constants.** The review
   mechanism now exists and is honest. Using it on the corpus's strongest
   release candidates would be the first time TempestOS held data a person
   had actually stood behind.

The second calculation should wait for both, and should then be chosen to
stress what this one did not: a standard's method rather than textbook
mechanics, and a case where the acceptance criterion comes from a cited
document rather than from a stated assumption.
