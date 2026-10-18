# Technical Debt Register

**Opened:** 2026-09-05, at the v0.74.0 baseline. No prior register
existed in the package; the debt below was found by inspection, not
inherited from a previous list.

**Colour meaning** (per the programme's own definitions):
**RED** blocks v1.0.0 as defined · **AMBER** must be resolved before
v1.0.0 unless formally justified as outside scope · **YELLOW**
non-blocking, belongs to future work · **GREEN** resolved.

**Rule applied throughout:** an item is only AMBER-with-justification
where the justification is written down and dated. Nothing here was
recoloured to make the register look finished.

---

## Open Items

### TD-K01 — The knowledge layer has no content depth

| Field | Value |
|---|---|
| **Description** | 299 knowledge records average 46 words and score a median 1 of the 15 required depth elements. No record reaches 10. Units appear in ~3%, equations 3%, assumptions 2%; physical meaning, interpretation, common mistakes and adjacent-discipline links appear in none. |
| **Origin** | Accumulated across v0.70.0–v0.74.0, which added topic scaffolding at speed without content behind it. |
| **Affected domain** | All 44 fundamentals domains. |
| **Severity** | Critical to the library's purpose. |
| **V1.0 impact** | **Excluded from v1.0.0 scope** by Product Owner decision, 2026-09-05 (Definition of Done §1, §4). |
| **Colour** | **AMBER — justified deferral.** |
| **Justification** | v1.0.0 is defined as a structure, governance and provenance baseline, not a content release. It makes no claim of content completeness; the deficit is stated in the README, the Definition of Done, the gap analysis and every record's own maturity classification. Deferring a deficit that is disclosed in four places and machine-checkable is not hiding it. |
| **Disposition** | Content Programme, post-v1.0.0. Estimated 100,000–170,000 words. |
| **Verification** | `governance/generated/domain-coverage-matrix.json` re-derives the depth score at any time; the current median of 1/15 is the fixed measure progress is judged against. |
| **Status** | OPEN — deferred, disclosed. |

### TD-K02 — No source-bound numeric property data

| Field | Value |
|---|---|
| **Description** | 64 of 731 JSON files contain any numeric value. `data/steel-depth/` is a queue of `source_or_review_required` rows. 447 records self-declare placeholder; 0 are verified. |
| **Origin** | The catalogue was built as a qualitative screening layer; source binding was always a later step and was never taken. |
| **Affected domain** | The entire materials catalogue. |
| **Severity** | Critical for design use; irrelevant for a structural release. |
| **V1.0 impact** | **Excluded** (Definition of Done §4). |
| **Colour** | **AMBER — justified deferral.** |
| **Justification** | Closing it needs licensed standards and a datasheet ingestion pipeline, not authoring effort. Standards texts cannot lawfully be scraped or reproduced, so this is a procurement and pipeline decision, made deliberately rather than worked around. |
| **Disposition** | Data Ingestion Programme: supplier datasheets, CAMPUS, NIMS, NIST for machine ingestion; hand entry from purchased standards for specified minima. |
| **Verification** | Validator invariant 1 — no record may claim verification without naming a source. Currently 0 violations, because currently 0 records claim verification. |
| **Status** | OPEN — deferred, disclosed. |

### TD-K03 — 151 content records declare no maturity status

| Field | Value |
|---|---|
| **Description** | 151 of 684 content records carry no `status`, `evidence_status` or `design_use` field, so they assert nothing about their own reliability. Concentrated in `exotics/` (48) and `engineering-polymers/` (43). |
| **Origin** | Schema drift across releases; several tranches were written before the status convention existed. |
| **Severity** | Moderate — an unclassified record is indistinguishable from a reviewed one to a consumer. |
| **V1.0 impact** | Directly contradicts the release's central claim that every record's status is stated. |
| **Colour** | **AMBER — must resolve before v1.0.0.** |
| **Disposition** | Either classify each, or accept them collectively under the package-wide default declared in the README, and list the exception. |
| **Verification** | Validator warning `NO DECLARED STATUS FIELD`; target is zero, or a written accepted-exception list. |
| **Status** | OPEN. |

### TD-K04 — Version identity is inconsistent four ways

| Field | Value |
|---|---|
| **Description** | Archive root directory says `v0.3.0`; `MANIFEST.json` says `0.72.0` and is 59 files stale; `MANIFEST-v0.74.0.json` says `0.74.0`; status files say `0.74.0`. |
| **Origin** | Versioned manifests were added without retiring or regenerating the unsuffixed one. |
| **Severity** | High for a release — a consumer reading `MANIFEST.json` gets a two-release-old view. |
| **Colour** | **RED — blocks v1.0.0.** |
| **Disposition** | `VERSION` file becomes the single authority; `MANIFEST.json` regenerated at release and reconciled by the validator; historical manifests retained unaltered. |
| **Verification** | Validator manifest reconciliation: `missing=0, unlisted=0` against `MANIFEST.json`. |
| **Status** | OPEN at register opening — see Closed Items. |

### TD-K05 — Process record disproportionate to content

| Field | Value |
|---|---|
| **Description** | 156 documents (70 handoffs, ~20 work-package status notes, ~60 release notes) describe the production of 14,380 words of engineering content. `docs/` contains no engineering material. |
| **Severity** | Low. |
| **Colour** | **YELLOW.** |
| **Disposition** | Retained in full. These are the project's genuine audit trail and deleting them would destroy history to improve a ratio. Future releases should not add a handoff document per version. |
| **Status** | OPEN — accepted. |

### TD-K06 — Keyword-based debt detection gives false assurance

| Field | Value |
|---|---|
| **Description** | A full sweep for TODO, FIXME, TBD, HACK, temporary, provisional, revisit, workaround, deprecated, obsolete and orphaned returns **zero hits** across 1,000 files, while 447 records declare themselves placeholder. A keyword-driven process would certify this package as debt-free. |
| **Severity** | Moderate — it is a defect in the *method*, not the content. |
| **Colour** | **YELLOW.** |
| **Disposition** | Replaced, not supplemented: debt detection for this package is the maturity classifier in `tools/validate.py`, which reads declared status fields. The keyword sweep is retained only as a secondary check. |
| **Status** | OPEN — method changed, monitoring. |

### TD-K07 — Validator false-positive history

| Field | Value |
|---|---|
| **Description** | The first validator read free text and reported 107 records as "claiming verification" because policy prose contained the word *verified*; the second still misread `"must be verified"` as a verification claim. Both were defects in the checker, corrected before any content was touched. |
| **Severity** | Low, now closed — recorded because a checker that cries wolf is how real findings get ignored. |
| **Colour** | **GREEN.** |
| **Disposition** | Classification is field-driven, with imperative phrases (`must be verified`) explicitly routed to *review-required*. |
| **Status** | CLOSED. |

## Closed Items

| ID | Item | Resolution | Verified by |
|---|---|---|---|
| TD-K04 | Version identity | `VERSION` added as sole authority; `MANIFEST.json` regenerated to match the package exactly; historical manifests retained unaltered; historical documents left as written | Validator: `MANIFEST.json` reconciles with 0 missing, 0 unlisted |
| TD-K07 | Validator false positives | Field-driven classification with imperative-phrase handling | Validator run: 0 failures |

## Register Summary

| Colour | Count | Meaning |
|---|---:|---|
| **RED, open** | **0** | No v1.0.0 blocker outstanding |
| **AMBER, open** | **3** | `TD-K01`, `TD-K02` justified and deferred; `TD-K03` to resolve before release |
| **YELLOW, open** | 2 | `TD-K05`, `TD-K06` accepted |
| **GREEN, closed** | 2 | `TD-K04`, `TD-K07` |

**The two largest items in this register are AMBER, not GREEN.** The
library's content deficit and its lack of source-bound data are real,
they are the dominant facts about this package, and they are recorded as
such. v1.0.0 is releasable because it does not claim to have solved
them — not because they were closed.
