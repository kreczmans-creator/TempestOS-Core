# Seed Data Sources Register

| | |
|---|---|
| **Purpose** | Every external document the TempestOS seed corpus was transcribed from, what was taken from it, and what may and may not be concluded from it. |
| **Scope** | The population phase's seed datasets, under `src/Tempest.Core/ReferenceData/Seeding/Datasets/`. |
| **Acquisition date** | 2026-09-07. Every source below was retrieved on that one day. |
| **Last reviewed** | 2026-09-07 (Population phase). |
| **Machine-readable counterpart** | `SeedSources` in `src/Tempest.Core/ReferenceData/Seeding/SeedSources.cs`. That type, not this document, is what the records themselves carry; this document exists so a person can read the same facts without reading code. |

---

## 1. The one thing to know before using any of this data

**Nothing in the seed corpus has been verified by a person.**

Every seeded record carries a named source organisation and a named source
document, and every one of them sits in
`ReferenceValidationState.Draft` with
`ReferenceVerificationStatus.NotVerified`, no reviewer and no verification
date. That is not an oversight waiting to be tidied. It is the true state,
and the platform enforces its consequences: a record cannot reach
`Released` without a named reviewer who checked it against its own source
on a recorded date, and no seeding path can supply one.

The practical effect is that **P02's reasoning services cannot yet consume
this data**. `DesignRuleService`, `EngineeringReviewService`,
`TradeStudyService` and `ManufacturingDecisionService` all read released
records only. The population phase has given the platform knowledge; it has
not, and could not, give it authority. A person must review.

---

## 2. Sources

### 2.1 National Institute of Standards and Technology (NIST)

| | |
|---|---|
| **Document** | CODATA Internationally Recommended Values of the Fundamental Physical Constants — Complete Listing (`allascii.txt`) |
| **Revision** | 2022 CODATA adjustment |
| **Retrieved from** | `https://physics.nist.gov/cuu/Constants/Table/allascii.txt` |
| **Extraction method** | `StructuredImport` — a machine-readable table the publisher itself distributes |
| **Used by** | `ConstantSeed` (12 records) |
| **Licensing** | A work of the United States government, published without licensing restriction. |
| **Strength** | The strongest source in the corpus: primary, authoritative, public domain, and it states its own uncertainties. |
| **Limitation** | Only 12 of several hundred entries were taken — the ones whose dimensions this platform's unit system models honestly. Molar quantities, magnetic moments and per-tesla quantities were left out rather than stored dimensionlessly. |

### 2.2 Aalco Metals Limited

| | |
|---|---|
| **Documents** | Four technical datasheets: 1.4301 (304) bar and section; 1.4404 (316L) bar and section; 6082-T6 extrusions; CW004A copper sheet, plate and bar |
| **Revision** | None stated on any of them, so none is recorded |
| **Retrieved from** | `https://www.aalco.co.uk/datasheets/` |
| **Extraction method** | `AutomatedExtraction` — read out of an HTML document by tooling, and inherently in need of checking |
| **Used by** | `MaterialSeed` (5 of 6 records) |
| **Licensing** | Factual property values only. No datasheet text, layout or document was copied into the repository. |
| **Strength** | A real stockholder's real published datasheets, citing the EN standards the limits come from. |
| **Limitation** | Secondary: Aalco restates limits set by standards it does not publish. The records cite the EN standard as each value's own origin and name Aalco as the document actually read, so a reviewer can go to the standard itself. |
| **Known source defect** | The 5083 datasheet prints a density of 265 g/cm³, which is physically impossible. Confirmed by two independent reads, so it is the source's error and not a transcription fault. The property is omitted from `mat-5083-o-h111` rather than corrected to the value the source evidently meant. |

### 2.3 Siderticino SA

| | |
|---|---|
| **Document** | S355J2 (S355) technical specifications — Non-alloy structural steels |
| **Retrieved from** | `https://siderticino.it/en/steel-datasheets/` |
| **Extraction method** | `AutomatedExtraction` |
| **Used by** | `MaterialSeed` (`mat-s355j2`) |
| **Strength** | Gives the thickness-banded EN 10025-2:2019 limits in full, including the Charpy requirement that is the whole reason to specify J2. |
| **Limitation** | A distributor's restatement of EN 10025-2, not the standard. |

### 2.4 RHD Bearings

| | |
|---|---|
| **Documents** | Product specifications for the 6205 and 6305 deep groove ball bearings |
| **Retrieved from** | `https://rhdbearings.com/specs/` |
| **Extraction method** | `AutomatedExtraction` |
| **Used by** | `BearingSeed` (2 records), `CommercialSeed` (1 supplier) |
| **Strength** | A manufacturer publishing its own product data as static, readable pages. |
| **Limitation** | Load ratings and speed limits are this manufacturer's own and are **not** interchangeable with another manufacturer's figures for the same ISO 15 boundary dimensions. The bearing record names the manufacturer for exactly this reason. |
| **Why not SKF or Schaeffler** | Both serve their catalogue data only to a scripted browser. Neither was reachable, and a distributor's republication was preferred against and rejected in favour of a manufacturer that publishes directly. |

### 2.5 Proto Labs, Inc.

| | |
|---|---|
| **Documents** | Three service capability pages: CNC machining; sheet metal fabrication; injection moulding |
| **Retrieved from** | `https://www.protolabs.com/services/` |
| **Extraction method** | `AutomatedExtraction` |
| **Used by** | `ProcessSeed` (4 records), `RuleSeed` (3 records), `CommercialSeed` (1 supplier, 1 cost, 2 lead times) |
| **Strength** | Quantified, current and attributable: envelopes, tolerances, thickness ranges, lead times and a tooling price. |
| **Limitation** | **A commercial capability statement, not a process specification.** These figures describe what one supplier offers and will change when its equipment or policy changes. Nothing here generalises to a statement about CNC milling, sheet metal work or moulding as processes, and every record that carries these figures says so on its face. |
| **Time sensitivity** | The USD 1,495 mould tooling starting price is the only price in the corpus. It carries its observation date and must be re-quoted before any commercial use. |

### 2.6 Wikimedia Foundation (tertiary — placeholder citations)

| | |
|---|---|
| **Documents** | "ISO metric screw thread"; "List of ISO standards 1–1999" |
| **Extraction method** | `AutomatedExtraction` |
| **Used by** | `FastenerSeed` (7 records), `StandardSeed` (6 titled records) |
| **Why it was used** | The ISO and CEN catalogues both refused automated retrieval (HTTP 403), and no manufacturer restatement of the needed tables was reachable in readable form. |
| **Standing** | **Tertiary. These records are placeholders for citations of the standards themselves**, and their provenance notes say so in those words, so that replacing them is findable work rather than a discovery somebody makes in three years. |

### 2.7 Tempest Design Engineering (authored, not sourced)

| | |
|---|---|
| **Used by** | `RuleSeed` (2 records), `EngineeringAssetSeed` (5 records), `KnowledgeSeed` (5 records) |
| **Standing** | Authored content carries Tempest's own authority and nobody else's. It is recorded with `ExtractionMethod.ManualTranscription` and `SourceOrganisation = "Tempest Design Engineering"`, and its notes begin `AUTHORED, not sourced`. |
| **Authoring is not reviewing** | None of it has been checked by a second person, and its verification status says so. |

---

## 3. The three content categories, and how to tell them apart

| Category | How a record declares it | Where it appears |
|---|---|---|
| **Source-backed** | `SourceOrganisation` names an external body; values carry `ReferenceValueOrigin.Standard`, `.ManufacturerCatalogue` or `.EngineeringReference` | All of P01, the supplier-derived rules, the commercial records |
| **Authored** | `SourceOrganisation = "Tempest Design Engineering"`; notes begin `AUTHORED` | The two `TDE-DES-*` rules, all five P05 assets, all five P06 knowledge records |
| **Fictional / test** | Lives under `tests/`, or — for the one narrative scenario in the corpus — says so in its own notes | Test fixtures; the `CHL-MAT-001` challenge scenario, whose framing is fictional while the numbers it turns on are the real published minima |

No record is ambiguous between two of these, and
`SeedDatasetTests.EverySeededRecord_NamesASourceAndClaimsNoVerification`
asserts the source side of it for every record in every library.

---

## 4. Deferred datasets, and why

| Library | State | Why |
|---|---|---|
| **Springs, gears and mechanical components** (`Tempest.Core.Components`) | Empty | No source was pursued. Component data is manufacturer-specific and the corpus already demonstrates the manufacturer-scoped pattern through bearings. |
| **Fastener mechanical properties** | Absent from otherwise-populated records | ISO 898-1 is paywalled; the two fastener manufacturers whose technical libraries restate it serve the tables only inside PDFs that were not retrievable. Writing "8.8" and a proof stress from memory would have produced a library that looks complete and traces to nothing. |
| **Bearing families other than deep groove ball** | Empty | Not reachable — see §2.4. Rolling-element selection turns on the choice *between* families, so the bearing dataset cannot yet support that decision. |
| **Failure and lessons database** (`Tempest.Core.Knowledge.Lessons`) | Empty, deliberately | Populating it means either writing up a real company's failure — somebody else's story, and often somebody's litigation — or inventing one, which puts fiction into the library engineers would most expect to be true. |
| **Standards titles for the CEN entries** | Six of fourteen standards have no title | The ISO and CEN catalogues refused automated retrieval. Left unrecorded rather than reconstructed. |
| **Electrical resistivity** | Published by four sources, recorded nowhere | This platform models no electrical resistivity dimension. For CW004A copper that is the property most users would want, and it is recorded as a real gap in that record's own notes rather than worked around. |

---

## 5. One disagreement between the data and the model

The Proto Labs turning page publishes a minimum wall thickness of 0.51 mm.
`TEMPEST-MFG-005` rejects it, holding that a turning process does not
produce a wall thickness.

The data was moved to a `ProcessConstraint` to fit the rule, rather than
the rule relaxed to fit the data — population does not get to weaken
validation. But the disagreement is real: a turned tube plainly has a wall,
and the rule is arguably too strict. It is recorded here and in the record
itself for the foundation to settle deliberately, not resolved as a side
effect of a population run.
