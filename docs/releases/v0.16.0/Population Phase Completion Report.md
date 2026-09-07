# Population Phase — Completion Report

| | |
|---|---|
| **Phase** | Post-foundation population |
| **Branch** | `claude/tempestos-a4-bearing-library-unobtf` |
| **Date** | 2026-09-07 |
| **Baseline** | `d15726d` — Foundation Skeleton Completion Certification |
| **Sources register** | [`Seed Data Sources Register.md`](../../governance/Data/Seed%20Data%20Sources%20Register.md) |

---

## A. Baseline

The certified foundation state was taken as given and not reopened: 40/40
original work packages structurally complete, P01–P07 complete or
foundation-complete, 5,397 tests passing in both configurations, governance
at 13 pass / 3 environmental warnings / 0 fail, Colour Review Board PASS
with zero RED and zero outstanding AMBER.

No foundation architecture was redesigned. One genuine disagreement between
a source and a validation rule was found and is recorded rather than
resolved (§K).

---

## B. Data populated

**79 records across sixteen libraries**, every one of them Draft, every one
of them naming its source.

### P01 — Engineering reference data

| Library | Records | Source | Provenance | Validation | Known limitations |
|---|---|---|---|---|---|
| Standards | 14 | ISO catalogue via a tertiary index; CEN designations as cited by the datasheets that use them | Named source and document on every row | Passes `StandardValidationService` | Six of fourteen have no official title — the ISO and CEN catalogues refused automated retrieval, and titles were left unrecorded rather than reconstructed. Publication status is `Unknown` for those six because nothing read confirmed the edition is current. |
| Materials | 6 | Aalco Metals (5), Siderticino (1) | Per-value: standards' limits marked `Standard`, stockholders' typical figures marked `EngineeringReference` | Passes `MaterialValidationService` | One property deliberately omitted (5083 density — the source prints an impossible 265 g/cm³). Electrical resistivity published by four sources but not modelled. One property set per grade, so size-banded minima are scoped by condition text rather than structurally. |
| Constants | 12 | NIST CODATA 2022 | `StructuredImport` from a public-domain machine-readable table | Passes `ConstantValidationService` | Only the constants whose dimensions this platform models. Exact values marked `Exact`; measured values carry their standard uncertainty. Relative uncertainties not recorded — CODATA publishes them in a separate table. |
| Fasteners | 7 | ISO 262 coarse-thread sizes via a tertiary index | Placeholder citations, flagged as such in their own notes | Passes `FastenerValidationService` | **Geometry only.** No property class, no material, no strength. `Mechanical.IsRecorded` answers `false` for all seven, so a consumer detects the absence rather than reading a default. |
| Bearings | 2 | RHD Bearings | Manufacturer's own product data | Passes `BearingValidationService` | One family only (deep groove ball). Approximate shoulder and recess diameters not recorded, because the model holds exact quantities with nowhere to carry an approximation qualifier. |
| Manufacturing processes | 4 | Proto Labs | `ManufacturerCatalogue` throughout, supplier named in `Variant` | Passes `ProcessValidationService` | Every figure is one supplier's commercial capability, not a limit of the process. Material compatibility recorded at family level because that is what the supplier states. |

### P02 — Engineering intelligence

| Library | Records | Composition |
|---|---|---|
| Engineering rules | 5 | Three restating a named supplier's published limit (`MFG-CAP-001..003`), two authored and owned by Tempest (`TDE-DES-001..002`) |

### P03 — Commercial intelligence

| Library | Records | Notes |
|---|---|---|
| Suppliers | 3 | Aalco, Proto Labs, RHD — the three the corpus was actually sourced from. All `Prospective`; all capabilities `Offered`, never `Verified` or `Proven`. |
| Process costs | 1 | The published mould tooling starting price, `Quoted`, dated, with a note that it must be re-quoted before commercial use. |
| Lead times | 2 | Machining and sheet metal, both `Estimated` rather than `Historical` — Tempest has placed no orders and observed no delivery. |

### P05 — Engineering assets

| Library | Records | Notes |
|---|---|---|
| Templates | 1 | Calculation record sheet, four sections, structured fields |
| Calculation packs | 1 | Bracket stress check, material input pinned to a real revision |
| Verification artefacts | 1 | Against a real registered requirement; standing `NotPerformed` |
| Design review packs | 1 | `NotConcluded`, no participants, two observations |
| Technical documents | 1 | Material selection note, pinned to two material revisions |

All five authored by Tempest, all five about the same bracket, and all five
deliberately unfinished — see §K.

### P06 — Knowledge and Academy

| Library | Records | Notes |
|---|---|---|
| Prompts | 2 | Held as text with declared inputs, outputs and failure modes. Nothing executes them. |
| Academy nodes | 1 | A lesson with three outcomes and three activities, linked to the example and challenge below |
| Challenges | 1 | Fictional scenario, real numbers — the two published 6082-T6 minima |
| Worked examples | 1 | Two of its three steps pin the material revision they read |
| Lessons (failures) | 0 | Deliberately empty — see §C |

---

## C. Data intentionally still empty

| Library | Why |
|---|---|
| `Tempest.Core.Components` (springs, gears, mechanical components) | No source pursued. The manufacturer-scoped pattern is already demonstrated by bearings. |
| `Tempest.Core.Knowledge.Lessons` (failure and lessons database) | Populating it means writing up a real company's failure or inventing one. Neither belongs in the library engineers would most expect to be true. |
| Every P04 Business OS library | Out of this phase's scope. Business operations data is the organisation's own and cannot be sourced externally. |
| Every P07 Business Governance library | Same reason: contracts, risks, IP assets, rate cards and financial scenarios are Tempest's own records, not published data. |
| Bearing families other than deep groove ball | Not reachable — the two large manufacturers serve catalogue data only to a scripted browser. |
| Fastener mechanical properties | ISO 898-1 paywalled, restatements unreachable. |

---

## D. Import mechanism

**One interface and one service, and that is the whole of it.**

- `IReferenceSeed<TDefinition>` — a named, versioned set of records. Reads
  nothing, parses nothing, reaches nothing outside the process.
- `ReferenceSeedService.ApplyAsync` — calls
  `IReferenceDataCatalog<T>.RegisterAsync` once per record, through the
  library's own ordinary catalogue.

There is no pipeline, no staging area, no schema, no parser, no second
persistence mechanism and no scraping framework. Everything that makes a
registration durable, indexed, revisable and supersedable is the
catalogue's existing behaviour, unchanged.

**Three properties the mechanism guarantees:**

1. **Additive, never destructive.** A record already present is skipped.
   A test proves a human correction made after seeding survives a re-seed.
2. **Idempotent.** A second run registers nothing;
   `ReferenceSeedOutcome.MadeNoChange` answers `true`.
3. **Seeding is not verifying.** Every record lands in `Draft`. The service
   has no way to write a reviewer, so a populated library cannot present
   itself as a verified one.

**Why the datasets are code rather than data files.** The definitions are
strongly dimensioned — a yield strength is a `Quantity<Pressure>`, not a
number and a string — so expressing them in the language the units live in
means the compiler checks every unit, every property name's expected
dimension and every enumeration member. A JSON seed would have needed a
schema, a parser and a unit resolver to reach the same place, and would
have failed at run time where this fails at build time.

**Host integration.** `ReferenceSeedService` is registered as an ordinary
singleton in the real `TempestHost`. The host does **not** seed itself at
start-up, and a test asserts that a freshly started host has an empty
material library: when a library gets populated is a governance decision,
not a side effect of booting.

---

## E. Cross-domain references now working

| Reference | Mechanism | Proven by |
|---|---|---|
| Material → Standard | `StandardReference.StandardId` | `EveryStandardCitation_ResolvesToARegisteredStandard` |
| Fastener → Standard | same | same |
| Bearing → Standard | same | same |
| Process → Standard | same | same |
| Rule → Standard | same | same |
| Calculation pack → Material **revision** | `CalculationInput.SourcePin` | `TheCalculationPackPinsTheMaterialRevisionItActuallyRead` |
| Calculation pack → Template **revision** | `TemplateUsage` | Built from the registered template, not a literal |
| Worked example → Material **revision** | `WorkedStep.SourcePin` | `TheWorkedExampleReadsTheSameMaterialRecordItsLessonTeachesFrom` |
| Technical document → Material revisions (×2) | Recorded pins | Seeded from resolved records |
| Verification artefact → Requirement | `VerifiedRequirement.RequirementId` | `TheVerificationArtefactNamesARequirementThatReallyExists` |
| Design review → Calculation pack, Verification artefact, Requirement | Reference lists | `TheDesignReviewGathersTheCalculationVerificationAndRequirementTogether` |
| Supplier → Material records | `SupplierCapability.MaterialRecordIds` | `TheCommercialRecordsPointAtRealProcessesAndMaterials` |
| Supplier → Process records | `SupplierCapability.ProcessRecordId` | same |
| Cost → Process | `CommercialApplicability.ProcessRecordId` | same |
| Lead time → Process | same | same |
| Process → Material family | `ProcessMaterialCompatibility.Family` | `AProcessNamesMaterialFamiliesThatTheMaterialLibraryActuallyHolds` |
| Academy lesson → Worked example, Challenge | Activity references | `TheWorkedExampleReadsTheSameMaterialRecordItsLessonTeachesFrom` |

**Two references behave in ways worth stating explicitly.**

A citation may carry a different edition from the standard it resolves to.
Two Aalco datasheets cite different editions of EN 573-3 (2009 and 2019),
and the bearing pages cite ISO 15:2011 where the index holds 2017. The
model keeps the disagreement: each citation carries the edition its own
document stated, and the EN 573-3 index record deliberately records no
edition of its own because its citers disagree.

Revising a material leaves a pin pointing at what the calculation actually
used. A test revises the pinned 6082-T6 record and then retrieves the
pinned revision, confirming it still holds the original values and does not
carry the correction. Without that, the reproducibility claim would be
decoration.

---

## F. P02 intelligence introduced

| Code | Kind | Statement |
|---|---|---|
| `MFG-CAP-001` | Supplier-derived, `Constraint` | A part over 559 mm in its largest dimension cannot be produced by Proto Labs' factory CNC milling |
| `MFG-CAP-002` | Supplier-derived, `Requirement` | An unmarked machined dimension is held only to ISO 2768-1-1989-f — ±0.127 mm for this supplier |
| `MFG-CAP-003` | Supplier-derived, `Constraint` | Sheet metal must be 0.61–6.35 mm; the three-day route caps at 3.175 mm |
| `TDE-DES-001` | Authored, `Requirement` | A material in a released design cites the standard its properties come from |
| `TDE-DES-002` | Authored, `Requirement` | A strength value is meaningless without the condition it holds under |

Each supplier-derived rule is scoped to its supplier in its applicability
conditions. "Machining cannot exceed 559 mm" is false; "this supplier's
factory envelope does not exceed 559 mm" is true, and a rule that quietly
generalised a commercial limit into a physical one is how a platform starts
giving confidently wrong advice.

Both authored rules state more than their machine conditions can test, and
both set `RequiresHumanReview` rather than pretending the check is
automatable.

**There is no third category of remembered engineering wisdom, because
there is no way to review one.**

---

## G. P05 assets introduced

One template, one calculation pack, one verification artefact, one design
review pack and one technical document — all authored by Tempest, all about
the same bracket so that the links between them are real links rather than
five unrelated examples.

All five are **deliberately unfinished**. The calculation pack records its
method and its pinned material basis but no result, because two of its
three inputs are not established. The verification artefact is
`NotPerformed`. The design review is `NotConcluded` with an empty
participant list. Filling any of that in with plausible values would have
turned a demonstration of structure into a fabricated engineering record,
which is the one thing an engineering platform must never contain.

The verification model refused an artefact naming an empty requirement
identity. Rather than mint a plausible `Guid`, a real requirement
(`REQ-BRACKET-001`) is created and the assets hang from it — which also
gives the next phase the head of its scenario.

---

## H. P06 knowledge introduced

Two prompts, one Academy lesson, one challenge and one worked example.

All four teach the same thing, because it is the thing the real datasheets
in this repository teach the moment you read them: **a strength without its
size band is not a property**. 6082-T6 has four different published proof
stresses. A student can be shown the record rather than told a story, and
the worked example turns on two of those real figures — 260 MPa and
200 MPa — pinned to the revision it read them from.

The prompts are text with declared inputs, outputs and failure modes. One
of the failure modes is drawn from this run's own experience: an extractor
that "helpfully" corrected the 5083 datasheet's impossible density would
have hidden the source's error.

Nothing executes a prompt, calls a model, or orchestrates an agent.

---

## I. Testing

**36 new tests**, in four files under `tests/Tempest.Core.Tests/Population/`.

| File | Covers |
|---|---|
| `SeedDatasetTests` | Registration, idempotency, non-destructiveness, validation, provenance, release refusal, unit and condition round-trip, the omitted density, exact vs measured uncertainty, absent fastener strength |
| `CrossDomainReferenceTests` | Every link in §E, plus edition divergence and revision stability |
| `PopulationHostRegistrationTests` | The seam in the real host, that the host does not self-seed, container retrieval, and survival across a host restart |
| `ScenarioReadinessTests` | That the eight-step scenario has data at every step — and, separately, that it cannot yet be run to a completed state |

### Full gate

| Configuration | Assembly | Passed | Failed | Skipped |
|---|---|---|---|---|
| Debug | `Tempest.Core.Tests` | 4,948 | 0 | 0 |
| Debug | `Tempest.Desktop.Tests` | 474 | 0 | 0 |
| Release | `Tempest.Core.Tests` | 4,948 | 0 | 0 |
| Release | `Tempest.Desktop.Tests` | 474 | 0 | 0 |

**5,422 tests, 0 failures, 0 skips, both configurations.** Baseline was
5,397; the population phase adds 25 net to the Core assembly's own count
against a baseline of 4,923.

Build warnings: 0 new. Build errors: 0.

---

## J. Governance

`scripts/governance-healthcheck.ps1` — **13 passed, 3 warned, 0 failed**,
identical to the certified baseline.

Two checks failed mid-phase and were fixed by updating the registers, not
by relaxing the checks:

| Register | Change |
|---|---|
| Interface Register | `IReferenceSeed<TDefinition>` added; 310 → 311. `ReferenceSeedService` has no interface of its own, deliberately — there is one way to seed and nothing to substitute. |
| Namespace Register | `Tempest.Core.ReferenceData.Seeding` (5 files) and `...Seeding.Datasets` (10 files) added; 93 → 95. No existing namespace changed. |

The three remaining warnings are the pre-existing environmental ones the
baseline already discloses (no git tags in this clone; two historical
release folders without a `WorkPackages.md`).

New governance document: **`docs/governance/Data/Seed Data Sources
Register.md`** — every source, what was taken, what may not be concluded,
licensing, and the deferred datasets with reasons.

---

## K. Deferred work

### K.1 Remaining population

- Springs, gears and mechanical components: no records.
- Fastener mechanical properties: needs ISO 898-1 or a readable
  restatement.
- Bearing families beyond deep groove ball.
- Standards titles for the six CEN entries: needs catalogue access.
- Failure and lessons database: needs a policy decision about whose
  failures may be written up, not a source.
- P04 and P07: organisational data, not acquirable externally.

### K.2 Integration

Not started, by instruction. No Desktop view, view-model, navigation entry
or menu item was added or changed. The only integration is the seam in
§D — one singleton, resolvable, proven to round-trip a record through the
container and across a host restart.

### K.3 End-to-end testing

Not started. The data is in place and joined up (§E), and
`ScenarioReadinessTests` proves both halves of that: every step of the
Requirement → Selection → Manufacturing → Calculation → Verification →
Review → Documentation → Commercial chain has records, **and** the chain
cannot yet be run to a completed state, because the calculation has no
result, the verification has no standing and the review has no decisions.
A later phase that mistook "linked" for "ready" would build a workflow over
records that deliberately hold nothing.

### K.4 Technical debt

**New, from this phase:**

| Item | Detail |
|---|---|
| Seed records cannot be consumed by P02 | Every seeded record is `Draft`; `DesignRuleService`, `EngineeringReviewService`, `TradeStudyService` and `ManufacturingDecisionService` all read released records only. **This is the architecture working as designed, not a defect** — but it means a person must review before the reasoning layer can use any of this. It is the single most consequential fact in this report. |
| `TEMPEST-MFG-005` vs turned wall thickness | The rule holds that turning produces no wall thickness; a supplier publishes a minimum wall thickness for turning. The data was moved to a constraint to fit the rule. A turned tube plainly has a wall, so the rule is arguably too strict. Left for the foundation to settle. |
| Tertiary citations awaiting replacement | Thirteen records (7 fasteners, 6 standards) cite an encyclopaedic index as a placeholder for the standard itself. |
| No electrical resistivity dimension | Published by four sources, recorded nowhere. Most acute for CW004A copper, where it is the property most users would want. |
| One property set per material grade | Size-banded and temper-banded minima are scoped by condition text rather than structurally. Adequate and honest, but a designer must read the condition. |

**Carried forward:** the six YELLOW items from the foundation
certification are untouched. None blocked population, and none was
remediated — including the `P07` free-text organisation item, which the
instruction explicitly named as a migration question rather than
permission to redesign a completed skeleton.

---

## L. Stop condition

The selected seed datasets are source-backed, validated against the
existing rules unweakened, persisted through the real catalogues,
revision-safe, provenance-safe, cross-referenceable and covered by tests;
both build configurations and the governance check pass. This phase stops
here.

The next deliberate phase is **TempestOS application integration and real
end-to-end engineering testing**, which will consume what this phase built.
Its first task is not integration. It is a person sitting down with the
sources named in §B and moving records out of Draft, because until that
happens the platform holds knowledge it is not permitted to act on.
