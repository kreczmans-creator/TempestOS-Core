# Seed Data Sources Register

| | |
|---|---|
| **Purpose** | Every external document the TempestOS shipped reference libraries were transcribed from, what was taken from it, and what may and may not be concluded from it. |
| **Scope** | The seed datasets under `src/Tempest.Core/ReferenceData/Seeding/Datasets/`: Standards, Materials, Constants, Fasteners, Bearings (the five libraries seeded at start-up), plus the Process and engineering-asset datasets recorded in the first acquisition. |
| **Acquisitions** | **First:** 2026-09-07 (population phase). **Day-one:** 2026-10-01 (PO decision 2026-10-01). Every source in an acquisition was retrieved on that day; each record's provenance notes carry the retrieval date and the exact address. |
| **Last reviewed** | 2026-10-01 (day-one acquisition, branch `wt/reference-seeds`). |
| **Supersedes** | `archive/docs-2026-09/governance/Data/Seed Data Sources Register.md` (frozen copy of the first-acquisition register; its §2 content is carried forward below unchanged in substance). |
| **Machine-readable counterpart** | `SeedSources` (`SeedSources.cs`, `SeedSources.DayOne.cs`). The records carry those provenance objects; this document lets a person read the same facts without reading code. |

---

## 1. The two things to know before using any of this data

**1. It is released, and it is secondary.** Under the Product Owner's
decision of 2026-10-01 ("need materials database ... fully populate ... Seed
as much information into those databases as possible from recognised
internet sources to ensure these are fully usable from day 1, otherwise it's
a hard block against v1.0.0"), every record in the five start-up libraries is
**released at seed**: the host's `ReferenceSeedService` carries
`ReferenceSeedReleasePolicy`, which verifies and releases each newly
registered record through `ReferenceReviewService` — the same path, permission
checks and audit rows a person's review uses — as the named, non-human
principal **`tempest.reference-seed`**. Every such record's provenance reads:

> Seeded from *&lt;source organisation — document&gt;*; released at seed for
> day-one use (PO decision 2026-10-01); verify against the primary standard
> before issue. The check this verification records is against the cited
> secondary source document as transcribed at acquisition, not against the
> primary standard itself.

`VerificationStatus` is `VerifiedAgainstSource` because release requires it;
the source it was verified against is the secondary document named, and the
words above say so on every record. **Before issuing work that relies on a
value, check it against the primary standard or the mill/manufacturer
certificate** and, if it differs, supersede the record with a person-verified
one.

**2. Nothing a person did is ever overwritten.** Seeding is additive. A
record a person registered, revised, checked or released is never touched;
a shipped record whose secondary key (designation, part number) is already
held by someone else's record is skipped (`KeyConflict`). The one refresh the
seeder performs is of a shipped record still exactly as an earlier seed left
it (Draft, revision 1, never verified, same source organisation): it is
brought up to the current dataset and released. At start-up, a library that
holds any record of a person's own is left entirely alone; a library holding
only shipped records is topped up.

---

## 2. Coverage at the day-one acquisition

| Library | Before (r1) | After (r2) | Released at seed |
|---|---|---|---|
| Standards (citation index) | 14 | 29 | yes |
| Materials | 6 | 77 | yes |
| Constants (CODATA 2022) | 12 | 12 | yes |
| Fasteners | 7 (geometry only) | 131 | yes |
| Bearings | 2 | 39 | yes |

Materials by group: structural steels 10 (S235JR, S275JR, S355JR/J0/J2/K2,
S460N/M/Q, S355J2H); engineering steels 9 (C45, 42CrMo4 +QT, 34CrNiMo6 +QT,
51CrV4 +QT, 11SMn30 +C, AISI 4130/4140/4340/8620); stainless 13 (1.4301,
1.4307, 1.4401, 1.4404, 1.4462, 1.4305, 1.4016, 1.4021, 1.4542 P930, 431,
410, 440C, 321); aluminium 11 (6082-T6, 6082-T651, 6061-T6, 6063-T6,
5083-O/H111, 5754-H22, 1050A-H14, 2014A-T6511, 7075-T6, 2024-T351,
7050-T7451); copper 6 (CW004A, CW614N, CW451K, C63000, C95400, C10100);
cast iron 3 (EN-GJL-250, EN-GJS-400-15, EN-GJS-500-7); titanium 2 (Grade 2,
Grade 5); nickel 3 (INCONEL 600, 625, 718); magnesium 5 (AZ31B-H24, AZ61A-F,
AZ80A-T6, AZ91D-F, WE43A-T6); thermoplastics 15 (POM-C, PA 6, PA 66, PA 66
GF30, PEEK, PC, PEI, PPS, ABS, PTFE, PE-UHMW, PE-HD, PP, PVC-U, cast PMMA).

Fasteners: the 7 geometry-only records, plus ISO 898-1 classes 4.6, 5.6, 8.8,
10.9, 12.9 at M3, M4, M5, M6, M8, M10, M12, M14, M16, M18, M20, M22, M24, M27,
M30, M33, M36 (85), plus ISO 3506-1 A2-70, A4-70, A4-80 at M3–M24 (39).

Bearings: RHD deep groove ball bearings 6000–6012, 6200–6212, 6300–6312 (39).

**Calculator readiness.** Every material carries density, Young's modulus,
yield/0.2% proof strength, ultimate tensile strength and thermal expansion
coefficient — the properties the calculation modules and the bracket check
read — except the documented gaps in §5. 10 materials also carry a fatigue
strength the fatigue module can read (S355J2, 6061-T6, 2014A-T6511, 7075-T6,
2024-T351, C63000, C10100 and the three cast irons). Every ISO 898-1 fastener carries the
property class, stress area and proof strength the bolted-joint module reads;
every bearing carries the dynamic and static radial ratings the L10 module
reads. `DayOneReferenceLibraryTests` pins all of this, plus that a beam
calculation runs on the seeded S355J2 with nobody releasing it by hand.

---

## 3. Sources — first acquisition (2026-09-07)

Unchanged in substance from the archived register.

| Source | Documents | Used by | Standing and limitation |
|---|---|---|---|
| NIST (CODATA 2022, `allascii.txt`) | One machine-readable table | `ConstantSeed` (12) | Primary, authoritative, public domain. `StructuredImport`. |
| Aalco Metals Limited | 1.4301, 1.4404, 6082-T6, 5083-O/H111, CW004A datasheets | `MaterialSeed` (5) | Secondary restatement of EN limits. **Source defect:** the 5083-O/H111 sheet prints a density of 265 g/cm³ (see §6). |
| Siderticino SA | S355J2 datasheet | `MaterialSeed` (1) | Restates EN 10025-2:2019. |
| RHD Bearings | 6205, 6305 specifications | `BearingSeed` (2) | Manufacturer's own ratings; not interchangeable with another maker's. |
| Proto Labs, Inc. | CNC, sheet metal, moulding capability pages | `ProcessSeed`, frozen `RuleSeed`/`CommercialSeed` | Commercial capability statement, not a process specification. |
| Wikimedia Foundation (tertiary) | "ISO metric screw thread"; "List of ISO standards 1–1999" | `FastenerSeed` geometry (7), `StandardSeed` titles (6) | Placeholders for the standards themselves. |
| Tempest Design Engineering (authored) | — | `EngineeringAssetSeed`, frozen `RuleSeed`/`KnowledgeSeed` | Authored, not sourced; **not released at seed** (`ReleaseAtSeed` is false for these datasets). |

---

## 4. Sources — day-one acquisition (2026-10-01)

Every value below was read on 2026-10-01 from the document at the address
given in the record's own provenance. "Secondary" means the document restates
a standard's limits; "manufacturer" means the publisher's own product data.

| # | Source organisation | Documents (exact addresses in each record) | Used for | Standing |
|---|---|---|---|---|
| 4.1 | **SteelNumber** (steelnumber.com, European steel and alloy grades database) | Grade pages: S275JR (name_id 3), S355JR (8), S355J0 (2), S355K2 (11), S460N (22), S460M (30), S460Q (39), S355J2H (649), C45 (152), 42CrMo4 (335), 11SMn30 (155), EN-GJL-250 (1505), EN-GJS-400-15 (1520), EN-GJS-500-7 (1522) | Mechanical limits by thickness/ruling section for EN 10025-2/-3/-4/-6, EN 10210-1, EN 10277-2/-3, EN 10083-3, EN 1561, EN 1563 grades | Secondary. Cites EN 10025-2:**2004** (not 2019) for the structural grades; recorded as cited. Its KV row tabulates 27 J at −20/0/+20 °C for the JR/J0/J2 family without separating them. |
| 4.2 | **Siderticino SA** | S235JR datasheet; S355 (S355J2) datasheet; 11SMnPb30/37 datasheet | S235JR mechanical and physical; the EN 1993-1-1/-1-2 design values (E 210 GPa, G 81 GPa, α 12×10⁻⁶/K, ρ 7.85 g/cm³, λ 53.3 W/m·K, c 440 J/kg·K) used as supplementary physical values for every EN 10025/10210 grade; S355 base-metal fatigue limit "about 160–180 MPa"; 11SMn30 density and modulus (from the leaded sister grade) | Secondary; physical values are design-code values, cited as such. |
| 4.3 | **Ovako AB** — Steel Navigator material data sheets | 42CrMo4, C45, 34CrNiMo6, 25CrMo4, 20NiCrMo2-2, 51CrV4 PDFs (with their "Last revised" stamps) | Physical properties block (E 210 GPa, ν 0.3, G 80 GPa, ρ 7800 kg/m³, mean CTE 20–300 °C 12 µm/m·K, λ 40–45, c 460–480) for C45, 42CrMo4, 51CrV4; CTE for AISI 4130 (via 25CrMo4, which Ovako lists as similar to 4130) and AISI 8620 (via 20NiCrMo2-2) | Manufacturer. The same block appears on every Ovako low-alloy sheet. |
| 4.4 | **Swiss Steel Group (Deutsche Edelstahlwerke)** | Firmodur 6582 34CrNiMo6 1.6582 technical data sheet (19/12/2018) | 34CrNiMo6 +QT limits per DIN EN 10083-3 and physical properties | Manufacturer. |
| 4.5 | **Saarstahl AG** | 51CrV4 (50CrV4) 1.8159 material specification sheet | 51CrV4 +QT limits | Manufacturer. |
| 4.6 | **Aalco Metals Limited** (day-one sheets) | 1.4307, 1.4401, 1.4462, 1.4305, 1.4016, 1.4021, 1.4542 bar; 6061-T6, 6063-T6 extrusions; 6082-T6/T651 plate; 5754-H22, 1050A-H14 sheet; 2014A-T6511 extrusion; 5083-H32 sheet (density only); CW614N rod; CW451K sheet and bar | Stainless, aluminium and copper EN grades | Secondary restatement of EN 10088-3, EN 755-2, EN 485-2, EN 12164, EN 1652. **Source defects:** see §6 (1.4006 modulus). |
| 4.7 | **AZoM (AZO Materials)** | Articles 6742 (4130), 6769 (4140), 6772 (4340), 6754 (8620), 1023 (431), 970 (410), 1024 (440), 967 (321), 9413 (Ti Grade 2), 9299 (Ti Grade 5), 8621 (AZ31B-H24), 8635 (AZ61A-F), 8657 (AZ80A-T6), 8670 (AZ91D-F), 8545 (WE43A-T6) | AISI alloy steels, martensitic/stabilised stainless, titanium, magnesium | Secondary (handbook and producer data restated; the stainless articles are the Atlas Steels grade datasheets). The 4130 and 4140 tables do not state a heat-treatment condition; the records say so. |
| 4.8 | **Kaiser Aluminum** | Rod & Bar Alloy 7075, 2024, 6061, 2014 technical data; Sheet Coil & Plate Alloy 7050 | 7075-T6, 2024-T351, 7050-T7451 records; R.R. Moore fatigue endurance limits (5×10⁸ cycles) supplementing 6061-T6 and 2014A-T6511 | Manufacturer; every value labelled typical. |
| 4.9 | **Copper Development Association Inc.** | Alloy pages C63000, C95400, C10100, C51000 | C63000, C95400, C10100 records; CTE supplementing CW451K (Aalco lists UNS C51000 as corresponding) | Industry association data, US customary units, recorded in those units. |
| 4.10 | **CASTFAST GmbH** (foundry) | "Material properties of cast iron" sheets for GJL and GJS | Proof strength (GJL), elongation, modulus, Poisson, density, CTE, conductivity, heat capacity, tension–compression fatigue and compressive strength for the three cast irons | Secondary restatement of EN 1561/EN 1563 and their informative annexes. Prints its rotating-bending fatigue rows in kN/mm² (evidently N/mm²); those rows are not used. |
| 4.11 | **Ensinger GmbH** | Stock-shape product pages: TECAFORM AH natural, TECAMID 6 / 66 natural, TECAMID 66 GF30 black, TECAPEEK natural, TECANAT natural, TECAPEI natural, TECATRON SX natural, TECARAN ABS grey, TECAFLON PTFE natural; en-gb pages for TECAFINE PE 1000, PE 300 and PP natural | 12 thermoplastics | Manufacturer; typical values on test specimens. |
| 4.12 | **Röchling Industrial** | Technical Data Sheet Trovidur PVC-U black (release 20/09/2023) | PVC-U | Manufacturer guideline values (DIN EN 15860). |
| 4.13 | **Röhm GmbH (PLEXIGLAS)** | Technical information 222-6, PLEXIGLAS GS UV transmitting | Cast PMMA | Manufacturer; values stated as minima (≥). |
| 4.14 | **The Chemours Company** | Teflon PTFE Properties Handbook (read, not used for a value) | Confirms no tensile modulus or yield is published for PTFE — only a flexural modulus range, which is not substituted | Manufacturer. |
| 4.15 | **Special Metals Corporation** | Technical bulletins INCONEL alloy 600, 625, 718 | Three nickel alloys | Manufacturer; tensile ranges are "composites ... not suitable for specification purposes" (600, 625); 718 records the Table 6 oil-tool minima. |
| 4.16 | **Würth Industrie Service** | DINO technical handbook chapter 1 (steel fasteners, extract of DIN EN ISO 898-1 Tab. 2 and test-force tables) and chapter 2 (stainless, extract of DIN EN ISO 3506-1 Tab. 16) | All 124 property-class fastener records: Rm, ReL/Rp0.2, Sp, Fp, A, HV, As,nom, coarse pitch | Secondary extract of the ISO tables. Fastenal's "Mechanical Properties of Metric Fasteners" (Rev 3-6-09) was read for cross-checking and is **not** cited: it predates ISO 898-1:2013 and omits class 5.6. |
| 4.17 | **RHD Bearings** (day-one pages) | `rhdbearings.com/specs/{6000,6200,6300}-series/{designation}/` for 6000–6012, 6200–6212, 6300–6312 | 37 new bearing records | Manufacturer's own ratings and speed limits. |
| 4.18 | **CEN / ISO / ASTM** (citation index) | 15 new index entries (EN 10025-3, -4, -6; EN 10210-1; EN 1993-1-1; EN 10083-3; EN 10277-2, -3; EN 1561; EN 1563; EN 12164; ISO 898-1; ISO 3506-1; ASTM A276; ASTM A564) | Bibliographic only | Designation and the edition the citing source named; no title (no publisher catalogue was read). |

**Sources tried and not usable on 2026-10-01:** ASM/MatWeb (`asm.matweb.com`,
`matweb.com`) refused automated access (connection reset / HTTP 403); SIMONA
technical handbook (HTTP 403); SKF and Schaeffler catalogues (scripted-browser
only); BGH 1.4057 sheet (404). Dörrenberg's 1.4057 sheet was read but gives
no modulus, so grade 431 is taken from AZoM.

---

## 5. Documented gaps — values deliberately not recorded

Each gap is stated in the record's own notes, and
`DayOneReferenceLibraryTests.EveryMaterial_CarriesEveryCalculatorConsumedProperty_ExceptItsDocumentedGaps`
pins the list, so a gap cannot be filled or opened silently.

| Record | Missing | Why |
|---|---|---|
| `mat-11smn30-c` | Thermal expansion | No source read for 11SMn30 publishes one. |
| `mat-abs` | Thermal expansion | The Ensinger ABS page publishes none. |
| `mat-ptfe` | Young's modulus, yield strength | Not published (PTFE creeps rather than yielding); Chemours gives only a flexural modulus range, which is a different quantity. |
| `mat-pmma-cast` | Yield strength | Cast PMMA fails brittle; none published. |
| `mat-pe-uhmw`, `mat-pe-hd`, `mat-pp`, `mat-pvc-u` | Ultimate tensile strength | Not published (no break in the test, or yield only). Ultimate strength is read only by the fillet-weld module, which does not apply to these materials. |
| `mat-aisi-8620-annealed` | Elongation | Not published by the article. |
| Stainless fasteners (39) | Proof strength, proof load | ISO 3506-1 specifies 0.2% proof stress and tensile strength for screws, not a proof stress; the bolted-joint module asks for it by hand. |
| Electrical resistivity (many records) | — | Published by most metal sources; the platform models no resistivity dimension. |

---

## 6. Source defects found and how they were handled

| Source | Defect | Handling |
|---|---|---|
| Aalco 5083-O/H111 datasheet (first acquisition) | Density printed as 265 g/cm³ | Not used. The day-one record now carries 2.65 g/cm³ from **Aalco's own 5083-H32 sheet** for the same alloy (density does not depend on temper), cited on the value. |
| Aalco 1.4006 (410) datasheet | Modulus of elasticity printed as 300 GPa | Datasheet not used; grade 410 seeded from AZoM 970 (E 200 GPa) instead. |
| AZoM 9299 (Ti Grade 5) | Title gives UNS R56200 | Recorded as printed; the record asks for the UNS number to be confirmed against the ASTM product specification before issue. |
| RHD 6301 page | Mass 0.051 kg, lighter than the smaller 6300 (0.053 kg) | Recorded as published, flagged in the record's notes. |
| CASTFAST GJS sheet | Rotating-bending fatigue rows in kN/mm² | Rows not used; the tension–compression row (N/mm²) is. |

---

## 7. Knowledge-foundation archive — what was used, what was corrected

The PO directed that the 5 September `knowledge-foundation/` work (recovered
as branch `archive/knowledge-foundation`, commit `d3660dd`) be the primary
material list. Its values are mostly `placeholder`, `undeclared` or
`candidate_reference` screening figures, so **no archive value was copied**:
every seeded value traces to a §4 source, and where the archive differed the
source was used.

**Coverage: 60 of the 89 distinct materials the archive's canonical discovery
index and catalogue files name are seeded** (counting the archive's duplicate
identities — e.g. `MAT-SS-304`/`MAT-STEEL-304`,
`MAT-PEEK`/`MAT-POLY-PEEK`, `C101`/`C110`/`C11000` — once). Seeded: S235JR,
S275JR, S355JR, S355J2, C45 (EN8 by mapping), 42CrMo4 (EN19 compared),
34CrNiMo6 (EN24 by mapping), AISI 4130/4140/4340/8620, 51CrV4; 304, 316L,
321, 410, 420, 430, 440C, 17-4 PH; 1050A, 2014, 2024-T351, 5083-H111,
5754-H22, 6061-T6, 6063-T6, 6082-T6, 6082-T651, 7050-T7451, 7075-T6/T651;
AZ31B, AZ61A, AZ80A, AZ91D, WE43; C101/C11000 (as CW004A), C10100, C63000,
C95400; INCONEL 600, 625, 718; Ti Grade 2, Grade 5; ABS, HDPE, PA6,
PA66-GF30, PC, PEEK, PEI, PMMA, POM, PP, PPS, PVC-U.

**Not yet seeded (29):** AISI 1008, 1018, 52100, SAE 9255, P355NH, PV270,
Hardox 450, Maraging 250, tool steels D2, H13, M2, O1, 13-8Mo; 310S, 15-5 PH;
2024-T3, 5083-H116; Hastelloy C-276 and X, Monel 400 and K-500, Nimonic 80A,
Haynes 25 (L-605); commercially pure Mo, Ta and W (the archive's
`MAT-W-COMMERCIAL` and `MAT-W-W1`), lanthanated W, 90W heavy alloy. Reasons: tool and bearing steels are specified by hardness after heat
treatment and the sources read give no yield or tensile strength; 1018 and
15-5 PH sources gave no condition or no physical data; the remaining
superalloy and refractory sources were not reached in this acquisition. The
archive's `engineering-polymers/families` files additionally name LCP, PA12,
PBT, PPA, POM-H and filled PEEK/PEI/PC/PBT/PPA/PPS variants, which are not
seeded.

**Archive values corrected by the sources** (examples): S235JR yield 250 MPa
in the archive vs 235 MPa (EN 10025-2, t ≤ 16 mm); 6061-T6 yield 276 MPa
(typical) vs 240 MPa (EN 755-2 minimum, Aalco); 6082-T6 yield 250 vs 260 MPa
(20–150 mm band); 316L yield 170 vs 200 MPa (EN 10088-3, Aalco); Ti-6Al-4V
yield 830 vs 828 MPa minimum; C45 yield 370 (archive) vs 305 MPa (+N, EN).

**Designation mappings carried over** from
`data/designation-mappings/material-designation-mappings-v1.1.0.yaml`, into
the records' notes with the archive's own confidence: MAP-42CRMO4-4140,
MAP-EN8-C45, MAP-EN24-4340, MAP-6082-T6-T651, MAP-304-UNS, MAP-316L-UNS.

---

## 8. The three content categories

| Category | How a record declares it | Released at seed? |
|---|---|---|
| **Source-backed** | `SourceOrganisation` names an external body; values carry `Standard`, `ManufacturerCatalogue` or `EngineeringReference` origin | Yes, for the five start-up libraries |
| **Authored** | `SourceOrganisation = "Tempest Design Engineering"`; notes begin `AUTHORED` | No |
| **Fictional / test** | Lives under `tests/` | No |

---

## 9. Remaining work (tracked as the v1.0.0 blocker in `docs/releases/v1.0.0/WorkPackages.md`)

1. A person checks the most-used records (S355J2, S275JR, 6082-T6, 1.4301,
   1.4404, 8.8 fasteners M8–M24) against the primary standards and supersedes
   them with person-verified revisions.
2. The 29 archive materials in §7 not yet seeded, and CW453K (CuSn8, named by
   the PO; only CW451K was published by the stockholder read).
3. Bearing families other than deep groove ball (angular contact, taper
   roller, cylindrical roller) — still unreachable from the large
   manufacturers.
4. An electrical resistivity dimension, so the values every metal source
   publishes can be recorded.
