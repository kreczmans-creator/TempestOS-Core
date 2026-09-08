# Foundation Colour Review Board

**Subject:** The complete seven-programme foundation, reviewed as a system
**Date:** 2026-09-06
**Branch:** `claude/tempestos-a4-bearing-library-unobtf`

---

## 0. Scope and method

Nine dimensions over `P01`–`P07` together, with attention to the seams
between them rather than to each in isolation. Findings are drawn from
the source, the test suite, the registers and the health check — not from
the completion reports, which are themselves under review.

**Evidence gathered:** 769 `Tempest.Core` source files, 311 public
interfaces, 663 distinct diagnostic codes across 46 prefixes, 4,923 Core
and 474 Desktop tests.

---

## 1. RED — release blockers

**None.**

No finding in this review prevents the foundation being called complete.

---

## 2. AMBER — required in-scope improvements

Both were found, both were remediated in this pass, and both are
re-verified below.

### AMBER-1 — Academy prerequisite cycle detection under-delivered its own contract ✅ **Remediated**

| | |
|---|---|
| **Finding** | `AcademyValidationRules.PrerequisiteCycle` is documented as "the node is its own prerequisite, directly or through a chain". The implementation checked only the direct pair. |
| **Evidence** | `AcademyCatalog.cs`, `EvaluatePrerequisitesAsync`: a single `prerequisite.Definition.PrerequisiteReferences.Contains(definition.Reference)`. A → B → C → A passed validation cleanly. |
| **Programme / WP** | P06 / WP06.2 |
| **File** | `src/Tempest.Core/Knowledge/Academy/AcademyCatalog.cs` |
| **Why it matters** | A three-node cycle is a curriculum a learner can no more start than a two-node one, and the code claimed to catch it. Code that under-delivers its own documented contract is worse than code that promises less. |
| **Recommendation** | Walk the whole chain, bounded against the cycle it is looking for. |
| **Required before certification** | Yes — a stated contract must hold. |
| **Remediation** | `IAcademyCatalog.FindAllPrerequisitesAsync` walks the transitive chain with a visited set and a depth bound; validation uses it and distinguishes a direct pair from a longer chain in its message. Four tests added. |

### AMBER-2 — persistence guards covered only the types somebody remembered ✅ **Remediated**

| | |
|---|---|
| **Finding** | Each programme's JSON round-trip guard was a hand-maintained `TheoryData` list. A governed type added later is covered only if somebody adds it. |
| **Evidence** | `EngineeringAssetPersistenceTests.RoundTrippableAssets`, `KnowledgePersistenceTests.RoundTrippableKnowledge`, `BusinessOperationsPersistenceTests.RoundTrippableRecords` — all literal lists. |
| **Programme / WP** | All; the defect class has landed in P07 and P03 |
| **File** | `tests/Tempest.Core.Tests/**/*PersistenceTests.cs` |
| **Why it matters** | This exact defect has shipped twice. `P07`'s `Money` read back as `0.00 (unspecified)`; `P03`'s `CostFigure` threw the moment a catalogue read it. Both were caught by a test naming that one type — which is precisely the coverage a list gives, and precisely why the third instance would not be caught. |
| **Recommendation** | A guard that discovers its own subjects. |
| **Required before certification** | Yes — the risk is recurrence of a shipped defect, not a hypothetical. |
| **Remediation** | `PersistableDefinitionGuardTests` reflects over every `ReferenceDataCatalog<T>` in the assembly and checks its definition type. Covers **48** governed types today; a library added in a year is covered the day it compiles. Distinguishes `NotSupportedException` (no usable constructor — the defect) from `JsonException` (required member missing — expected), a distinction probed empirically before being relied on. A meta-test reproduces the `CostFigure` defect on a throwaway type, so the guard cannot silently stop meaning anything. |

---

## 3. YELLOW — technical improvement and debt

Recorded, not remediated. None blocks foundation completion.

### YELLOW-1 — Contacts and interactions have no library-wide validation sweep

**Evidence:** `ICrmValidationService` is a bespoke interface, not an
`IReferenceValidationService<T>`, so `ContactCatalog` and
`InteractionCatalog` have no `ValidateLibraryAsync`. Every other governed
library in the platform has one.
**Programme / WP:** P04 / WP04.1 · **File:**
`src/Tempest.Core/BusinessOperations/Crm/CrmCatalogs.cs`
**Assessment:** The design reason is sound — a contact is only sound
relative to its organisation and an interaction relative to its contacts,
so one service validating both is right. The cost is the missing sweep.
**Recommendation:** Add `ValidateLibraryAsync` to `ICrmValidationService`.
**Before certification:** No.

### YELLOW-2 — `P07` opportunities still carry organisations as free text

**Evidence:** `Opportunity.OrganisationName` and `ExternalOrganisationId`
are unchanged; `WP04.1` now supplies the record they could point at.
**Programme / WP:** P07 / WP07.6, P04 / WP04.1
**Assessment:** Deliberate. Modifying a completed programme is outside the
remediation scope, and the link is available and unused rather than
missing. Disclosed in the `P04` completion report.
**Recommendation:** Wire in the integration phase.
**Before certification:** No.

### YELLOW-3 — `BudgetPosition` totals outgoing money only

**Evidence:** `CashDirection.Incoming` exists on `BudgetLine` and
`FinancialEntry`; `BudgetPositionService` filters to `Outgoing`.
**Programme / WP:** P04 / WP04.3
**Assessment:** Modelling receivables properly needs invoicing, which is
the accounting `ADR-0142` declines to do. Reporting a half-built
receivables position would be worse than reporting none.
**Recommendation:** Leave until a decision is taken about invoicing.
**Before certification:** No.

### YELLOW-4 — `P02`'s trade-off framework has no weighting or sensitivity

**Evidence:** `TradeStudyJudgement` records a per-criterion outcome with a
mandatory reason; standing derives from eliminating judgements. No
weights, no sensitivity analysis. `P03`'s `SourcingComparisonService`
takes the opposite approach with explicit weights.
**Programme / WP:** P02 / WP02.5
**Assessment:** Both are defensible and the divergence is not accidental —
`P02` refuses to score engineering alternatives at all, `P03` scores
suppliers with the weights visible. Worth a deliberate decision rather
than leaving two idioms in the platform by accident.
**Recommendation:** Decide whether `P02` should gain weighting, or record
the divergence in an ADR.
**Before certification:** No.

### YELLOW-5 — Cross-library references are strings, not a resolved graph

**Evidence:** `P06` activities cite worked examples by reference string;
`P04` orders cite budgets by reference string. Some are validated
optionally, none is traversable.
**Programme / WP:** P04, P05, P06
**Assessment:** Correct for a foundation — a resolved graph would couple
libraries that must stay independently loadable. It will matter when the
UI needs to navigate.
**Before certification:** No.

### YELLOW-6 — `IProjectDependencyRegister` sits outside the Interface Register's scope

**Evidence:** It lives in `Tempest.App`; the register's declared scope is
`src/Tempest.Core/`.
**Programme / WP:** P04 / WP04.2
**Assessment:** Correct placement, honest gap. Disclosed in the completion
report and in the register's own total rather than silently omitted.
**Recommendation:** Consider widening the register's scope.
**Before certification:** No.

---

## 4. GREEN — observations

**G-1 · Architecture — one lifecycle, seven programmes.** Every governed
library in `P01`–`P07` derives from `ReferenceDataCatalog<TDefinition>`.
A supersession bug has one place to be fixed. Confirmed: 48 definition
types discovered by reflection, all on the shared base.

**G-2 · Architecture — no vocabulary collisions across 46 diagnostic
prefixes.** All 663 diagnostic codes, all document kinds and all library
names are unique. The repository's own
`EngineeringVocabularyConsistencyTests` caught the one attempt to
redeclare `"supersedes"` during `P05` and it was corrected.

**G-3 · Architecture — the refusals are consistent and enforced.**
`P02` reports and never decides; `P03` recommends and never procures;
`P05` records approvals and confers none; `P06` holds knowledge and never
executes; `P04` records that a person ordered and never orders. Each is
enforced by a reflection test over its namespace, not by convention.

**G-4 · Code quality — no TODO, FIXME, HACK or XXX** in any of the three
programmes delivered in this run.

**G-5 · Testing — the suite finds real defects.** Four in this session,
all found by tests rather than by reading: `CostFigure`'s missing
constructor, `VerificationArtefact`'s whitespace reason, the `"supersedes"`
vocabulary duplication, and the Academy cycle gap. A suite that only
confirms what the author intended finds none of those.

**G-6 · Security — no secrets, credentials or personal data.** Scanned
across every fixture in the three new programmes: no password, token, key
or real address. The one e-mail address is `nobody@example.invalid`, using
the RFC 2606 reserved TLD.

**G-7 · Persistence — every governed type survives the real path.** The
full create/persist/reload/revise/retrieve-historical/supersede cycle is
tested per library against the document-backed store, not an in-memory
shortcut.

**G-8 · Cross-platform — no platform assumptions introduced.** No path
separators, no drive letters, no `/tmp`, no `DateTime.Now` in any of the
three new programmes. Time is injected via `TimeProvider` throughout, so
no test changes meaning overnight.

**G-9 · UI / accessibility — nothing to review, correctly.** No UI surface
was created, per the explicit instruction. The existing Desktop
accessibility baseline (`WP16.5A`) is untouched and its 474 tests pass.

**G-10 · Documentation and governance — clean and honest.** 13 health
checks pass, 0 fail. Registers reconciled at every programme boundary,
with three drifts disclosed rather than silently corrected: the Interface
Register's stale review field, `IReferencePinResolver`'s misplacement in a
domain namespace, and the Academy count.

**G-11 · Release engineering — no release claim was touched.** No tag, no
version, no release note altered. `VERSION` remains `0.16.0` and matches a
planned release folder.

**G-12 · Data state — every library ships empty, and says so.** No
fabricated supplier, price, standard, lesson, failure or customer. Each
completion report states the framework/data/fixture/integration position
separately rather than as one claim.

---

## 5. Verdict

| Severity | Count | Status |
|---|---|---|
| 🔴 **RED** | **0** | — |
| 🟠 **AMBER** | **2** | Both remediated and re-verified |
| 🟡 **YELLOW** | **6** | Recorded; none blocks completion |
| 🟢 **GREEN** | **12** | — |

### **PASS**

The foundation is architecturally complete across all seven programmes.
Both AMBER findings were remediated within this pass and re-verified by
the full suite. No RED finding was raised. The six YELLOW items are
genuine improvements, none of which prevents the foundation being called
complete, and none of which is deferred population or deferred integration
misclassified as a defect.

---

## 6. Secondary review

Performed after remediation.

| Check | Result |
|---|---|
| AMBER-1 fix behaves as claimed | Three-node cycle now an error; long non-closing chain correctly not an error; chain resolution returns the transitive set; a cyclic walk terminates |
| AMBER-2 fix behaves as claimed | 48 types discovered; meta-test reproduces the original defect and the guard catches it; the expected-failure case is correctly not flagged |
| No regression introduced | 4,923 Core + 474 Desktop, 0 failed, 0 skipped, Debug and Release |
| Governance unaffected | 13 passed, 3 warned, 0 failed |
| No new warnings | 0 warnings, both configurations |

**Secondary review: PASS.** Both remediations do what they claim, and
neither introduced a regression.
