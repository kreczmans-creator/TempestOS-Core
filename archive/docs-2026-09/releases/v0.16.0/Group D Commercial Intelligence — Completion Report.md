# Group D — Commercial Intelligence: Completion Report

**Programme:** P03 — Commercial Intelligence
**Work Packages:** D1 (`WP03.1`), D2 (`WP03.2`), D3 (`WP03.3`), D4 (`WP03.4`), D5 (`WP03.5`)
**Date:** 2026-09-06
**Branch:** `claude/tempestos-a4-bearing-library-unobtf`

---

## 0. Programme status

**Framework complete. Commercial data empty. Operational workflow not
started.**

Those are three separate facts and are reported separately throughout.

| Gate | Result |
|---|---|
| Build, Debug | 0 errors, 0 warnings |
| Build, Release | 0 errors, 0 warnings |
| Tests, Debug | **4,400 / 4,400** Core, **474 / 474** Desktop, 0 failed, 0 skipped |
| Tests, Release | **4,400 / 4,400** Core, **474 / 474** Desktop, 0 failed, 0 skipped |
| Governance health check | **13 passed, 3 warned, 0 failed** of 16 |
| Working tree | Clean |

The three warnings are pre-existing and disclosed: no `v*` git tags are
reachable in this container (two checks), and two historical release
folders predate the `WorkPackages.md` convention.

`P03` added **277 tests** (4,123 → 4,400).

---

## 1. Numbering

The commissioning instruction fixed the numbering and forbade renaming,
adding or subdividing. As delivered:

| Package | Roadmap identifier | Subject |
|---|---|---|
| `D1` | `WP03.1` | Supplier database |
| `D2` | `WP03.2` | Process & cost library |
| `D3` | `WP03.3` | Lead-time intelligence |
| `D4` | `WP03.4` | Quote / estimate structure |
| `D5` | `WP03.5` | Procurement decision support |

Five packages. No `D6`. No subdivision. Programme `P03`, group `D`
throughout.

---

## 2. What shipped

29 source files under `src/Tempest.Core/CommercialIntelligence/`, four
test files, five ADRs, one architecture document.

| Namespace | Files | Contents |
|---|---|---|
| `CommercialIntelligence` | 6 | The shared core |
| `.Suppliers` | 5 | `D1` |
| `.Costs` | 3 | `D2` |
| `.LeadTimes` | 3 | `D3` |
| `.Estimating` | 6 | `D4` |
| `.Procurement` | 6 | `D5` |

Eight governed libraries, each on `ReferenceDataCatalog<TDefinition>`,
each with its own document kind, index collections and validation
service. 20 new public interfaces, all registered in `TempestHost`.

---

## 3. Reuse rather than restatement

The instruction was explicit that `P03` must not introduce a second money
representation. It does not.

| Reused from | What |
|---|---|
| `P07` | `Money`, `CurrencyCode`, `CurrencyMismatchException`, `EffectivePeriod`, `BusinessEvidence`, `BusinessAuthorisation` |
| `P01` | Process and material record identities, cited by Id; the `ReferenceDataCatalog<T>` lifecycle; `ReferencePin` |
| `P02` | Nothing — and nothing in `P02` reads `P03` |

`CostFigure` **wraps** `Money` rather than replacing it, adding only a
certainty and, for a range, both ends. `LeadTimeDuration` is the one place
`P03` declines to reuse an existing type, and `ADR-0133` records why.

Two units were added to `DurationUnits` — `Day` and `Week` — documented as
calendar units. That is the only change `P03` made to `Group A` code.

---

## 4. The five packages

### D1 / WP03.1 — Supplier database

`SupplierIdentity` is separate from `SupplierRecord`, so who a business
*is* is separable from what it *does*.

**Identity is never merged.** `SupplierIdentityService` compares and
reports; it has no merge operation. Only a registration-number match is
conclusive; everything else is a recorded possible duplicate for a person
to resolve. Normalisation is public and deterministic.

**Nothing qualifies a supplier.** `CapabilityAssurance` runs `NotAssessed
→ Offered → Verified → Proven`, with `Declined` and `Disproven`; every
value names who established what. 21 diagnostics, `TEMPEST-CID-001`–`021`.

### D2 / WP03.2 — Process & cost library

`ProcessCostRecord` says what a process costs *from a supplier, at a
quantity, on a date*, linked to `A7` by `ProcessRecordId`. `A7` says what
a process is; `D2` says what it costs. Neither duplicates the other.

`CostBasis` decides whether `TotalFor(quantity)` can scale a figure;
unscalable bases return `null` rather than a plausible wrong number. 13
diagnostics, `TEMPEST-CIP-001`–`013`.

### D3 / WP03.3 — Lead-time intelligence

A working day is not a duration (`ADR-0133`). `ToElapsed()` returns `null`
for working days; comparison across the boundary throws. Every consumer
handles incomparability explicitly rather than assuming a shift pattern.

`FindApplicableAsync` returns records strongest claim first — committed
over quoted over historical over estimated — and returns the whole set,
because the weaker figures are frequently the more realistic ones. 14
diagnostics, `TEMPEST-CIL-001`–`014`.

### D4 / WP03.4 — Quote / estimate structure

Four types, four libraries, four document kinds (`ADR-0134`):
`CostEstimate`, `SupplierQuote`, `CustomerQuotation`, `RealisedOutcome`.

**Reproducibility.** Every `EstimateLine` pins the library, record and
revision it was derived from. `EstimatingService.ReproduceAsync` re-reads
those pins and reports where the libraries have moved; it never alters the
estimate. `FindCitingAsync` asks the same question backwards.

**Unknowns propagate.** One unpriced line gives an estimate no total,
rather than a total that is certainly too small.

**Authority.** An issued quotation that names nobody who issued it is a
validation **error**. 30 diagnostics, `TEMPEST-CIQ-001`–`030`.

### D5 / WP03.5 — Procurement decision support

`SourcingRequirement` states the criteria before the candidates are
assessed and is governed in its own right, so the weights cannot be
reshaped around the answer somebody wanted.

`SourcingComparisonService` is pure and deterministic — no I/O, no clock,
no randomness, tie-broken on ordinal code rather than input order.

- **Absent information is never scored as zero.** It reduces the
  candidate's established weight and becomes an outstanding question.
- **Excluded candidates stay in the record**, with the failed criterion
  and its reason attached.
- `RequiresHumanDecision` is unconditionally `true`, and
  `AlternativeChosen` makes disagreeing a first-class outcome.
- A recorded decision naming nobody who took it is a validation **error**.

25 diagnostics, `TEMPEST-CIS-001`–`025`.

---

## 5. What P03 will not do

Enforced by a reflection test over every type in the namespace, which
fails on any public member named `Award`, `PlaceOrder`, `RaiseOrder`,
`IssuePurchaseOrder`, `ApproveSupplier`, `QualifySupplier`,
`CommitExpenditure`, `CommitSpend`, `AcceptQuote`, `AcceptQuotation`,
`SignContract` or `Procure`.

| The organisation must | `P03` does |
|---|---|
| Award the work | Rank the candidates and say what it could not establish |
| Approve a supplier | Record what somebody established, and who |
| Accept a quote | Record what was offered, how firm, until when |
| Issue a quotation | Refuse to call it issued without naming who issued it |
| Commit expenditure | Total an estimate and name the unpriced lines |
| Convert a currency | Throw, naming both currencies |
| Convert working days to elapsed time | Return `null` |

---

## 6. Defect found and fixed

**`CostFigure` could not be deserialised.** Its constructor is private —
correctly, so every figure is built through a factory that says what kind
of figure it is — and `System.Text.Json` therefore had no constructor to
call. Every persisted cost record, and every estimate containing one,
threw `NotSupportedException` the moment a catalogue read it back.

Found by the round-trip tests the commissioning instruction asked for,
citing the money-persistence defect `P07` had already hit. Fixed with
`[JsonConstructor]` on the private constructor and guarded by a test.

This is the same class of defect as `P07`'s, in a different type, and it
is the reason those tests were specified. Two related types were checked
in the same pass and are sound: `Money` (already annotated during `P07`)
and `LeadTimeDuration` (a positional record struct, so no annotation is
needed).

---

## 7. Tests

277 tests across four files, all fixtures fictional and clearly marked.

| File | Covers |
|---|---|
| `CommercialFixtures.cs` | Shared fictional construction |
| `SupplierAndCostTests.cs` | `D1`, `D2`, `D3`, shared core |
| `EstimatingTests.cs` | `D4`, including three money round-trips |
| `ProcurementTests.cs` | `D5`, plus the structural guards |

**Two tests were rewritten rather than the code they exercised**, and
both times the code was right:

1. A pin was expected to carry revision 1. It carries the revision the
   record actually stood at when read — which the walk to Released has
   already advanced. The pin was correct; the expectation was not.
2. Two separately built estimates were expected to compare equal. Record
   equality compares list references, so they never do. Rewritten to
   compare content, which is what determinism actually means here.

One threshold was left alone and the test adjusted to it: a ten-point
lead reading as `Clear` rather than `Marginal` is defensible, so the test
now uses a genuinely narrow six-point lead.

---

## 8. Fixtures are not data

Every library ships **empty**. Real commercial intelligence — who the
organisation buys from, what it pays, how long suppliers take — is
business data belonging to the organisation, not content shippable in a
platform. Fabricating it would produce records indistinguishable in shape
from real ones and wrong in every particular.

Test fixtures are fictional throughout: "Notional Machining Ltd",
"Fictional Castings Ltd", "Imaginary Finishing Ltd", "Fictional Client
Ltd", registration number `00000000`, reference `FIX-Q-1`. They live only
in the test project, backed by in-memory stores that die with the test,
and are registered nowhere at run time. No credential, secret, personal
datum, bank detail or real financial figure appears in any source or test
file.

---

## 9. Registers

| Register | Before | After | Change |
|---|---|---|---|
| ADR Register | 130 | 135 | `ADR-0131`–`ADR-0135` |
| Architecture Document Register | 42 | 43 | `Group D Commercial Intelligence.md` |
| Namespace Register | 69 | 75 | Six `P03` namespaces |
| Interface Register | 252 | 272 | Twenty `P03` interfaces |
| Governance Index | 130 ADRs stated | 135 | Corrected |
| Exception Register | 99 | 99 | Unchanged — `P03` declares no new exception type |

**One drift disclosed rather than carried forward.** The Interface
Register's **Last Reviewed** field still read `WP 16.3B` (2026-09-04),
although both `Group B` and `Group C` had added rows and revised the
Total line beneath the table. The row data itself was sound — the
health check confirmed those rows present — so only the narrative was
stale. Corrected in place, with the drift stated in the field itself.

---

## 10. What P03 did not touch

No `WP16` work, no Desktop functionality, no Companion functionality, no
release tags, no release claims, no `P01`, `P02` or `P07` behaviour. The
only change outside `Tempest.Core.CommercialIntelligence` is:

- Two calendar units added to `DurationUnits` (`Day`, `Week`), purely
  additive.
- `[JsonConstructor]` on `CostFigure`'s private constructor — a `P03`
  type.
- `SupplierCatalog.LibraryName` promoted to a `const` so a
  `ReferencePin` can name it — a `P03` type.
- The `P03` registration block in `TempestHost`, added after `P07`'s and
  depending on nothing above it.

---

## 11. Known gaps and honest limitations

**No commercial data.** §8.

**No operational workflow.** `P03` holds records and reasons over them.
Raising an enquiry, chasing a quotation, following up a supplier — none
of that exists, and none was in scope.

**Pin resolution is optional.** `CostEstimateValidationService` takes
`IReferencePinResolver`s and checks only the libraries it was given one
for. Unresolvable pins into unknown libraries are silently not checked
rather than reported, because reporting them would mean warning about
every library `P03` does not know about.

**`ReproduceAsync` re-reads cost pins only.** Lead-time, material and
supplier pins are recorded and returned by `AllPins`, but the reproduction
check walks the cost library. Extending it is straightforward and was
not in scope.

**Margin is computed, never set.** `P03` has no view on what margin is
appropriate; `MarginOver` reports what a quotation implies against an
estimate, using the estimate's highest figure so a ranged estimate yields
the safe margin rather than the flattering one.

---

## 12. Git

| Commit | Subject |
|---|---|
| `a0b371f` | P03 shared core: commercial intelligence foundations |
| `6e6d69f` | P03 D1 / WP03.1: supplier database |
| `ee13992` | P03 D2 / WP03.2: process and cost library |
| `0ab5cbb` | P03 D3 / WP03.3: lead-time intelligence |
| `cbd823e` | P03 D4 / WP03.4: quote and estimate structure |
| `3aae570` | P03 D5 / WP03.5: procurement decision support |
| `4fcb839` | P03: tests, host registration, and a persistence defect fixed |

Branch: `claude/tempestos-a4-bearing-library-unobtf`. No pull request was
opened; none was asked for.
