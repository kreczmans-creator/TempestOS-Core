# Seed Data Review Set

| | |
|---|---|
| **Purpose** | To put in front of a human reviewer exactly what would have to be checked, and against what, before any seeded record may be released and relied on. |
| **Produced** | 2026-09-07 (Integration phase) |
| **Basis** | The 79 seeded records at commit `d3b9904`, plus the standards-designation correction made during this phase. |
| **Companion** | [Seed Data Sources Register](Seed%20Data%20Sources%20Register.md) — the sources themselves, licensing, and the deferred datasets. |
| **Validation evidence** | Every row's validation result below was read from the libraries' own validation services, not asserted. |

---

## 1. What this document is, and what it is not

This is **not** a review. Nobody has checked any of these records against
its source.

It is the list a reviewer would work through, with everything they would
need in front of them: what the record says, where it came from, what its
own library's validation service says about it, what it is wanted for, and
what is known to be doubtful about it.

**No record here has been released, and no release action is proposed.**
The repository provides no authorised release mechanism — reaching
`Released` requires writing a reviewer's name, a verification date and a
`VerifiedAgainstSource` status into a record's provenance, and nothing
binds those fields to the person actually doing it. Until a reviewer
performs that act themselves, or such a mechanism exists, every record
below stays `Draft`. That is recorded as a finding, not worked around.

---

## 2. Records the engineering scenario needs

The bracket scenario (see the integration report) consumes these records.
They are the priority for review, because until they are released the
scenario cannot run through P02's reasoning services at all.

| Record | Source | Provenance state | Validation | Intended use | Outstanding concerns |
|---|---|---|---|---|---|
| `mat-6082-t6` | Aalco datasheet "Aluminium Alloy - Commercial Alloy - 6082 - T6 Extrusions" | Named source and document; `AutomatedExtraction`; **not verified** | Clean — no errors, no warnings | The bracket's candidate material; pinned by the calculation pack and the worked example | Datasheet carries no revision or date, so a later edition cannot be detected. Values recorded are the 20–150 mm rod and bar band only; three other size bands and three other product forms carry different minima. |
| `mat-s355j2` | Siderticino "S355J2 (S355) technical specifications" | Named source and document; `AutomatedExtraction`; **not verified** | Clean | The strength-led alternative in the material comparison | A distributor's restatement of EN 10025-2:2019, not the standard. Recorded values are the t ≤ 16 mm band; four thicker bands carry lower yields. |
| `mat-1-4301` | Aalco "Stainless Steel - Austenitic - 1.4301 (304) Bar and Section" | Named; **not verified** | Clean | Corrosion-resistant candidate in selection | No quantified corrosion data recorded, because the source states none. Electrical resistivity published but not modelled. |
| `mat-1-4404` | Aalco "Stainless Steel - Austenitic - 1.4404 (316L) Bar and Section" | Named; **not verified** | Clean | Corrosion-resistant candidate in selection | As above. |
| `mat-5083-o-h111` | Aalco "Aluminium Alloy - Commercial Alloy - 5083 - '0' - H111 Sheet and Plate" | Named; **not verified** | Clean | Second aluminium candidate; the honesty case study | **Density deliberately absent** — see §4.1. Elongation recorded only for a band this record does not cover, so it is absent too. |
| `mat-cw004a` | Aalco "Copper and Copper Alloys - Copper (Pure) - CW004A Sheet, Plate and Bar" | Named; **not verified** | Clean | Conductivity-led candidate; demonstrates temper-spanning ranges | Published strengths span the full temper range (proof stress 50–340 MPa). The recorded figures are the annealed end and must not be used without specifying a temper. |
| `prc-cnc-milling` | Proto Labs CNC machining service page | Named; **not verified** | Clean | The manufacturing route for a machined bracket | Supplier capability, not a process limit. Envelope figures are the extremes of a three-axis box, so a part inside them is not thereby guaranteed to fit. |
| `prc-cnc-turning` | Proto Labs CNC machining service page | Named; **not verified** | Clean | Contrast route in the manufacturing decision | Published minimum wall thickness could not be recorded as a capability — see §4.2. |
| `prc-sheet-metal-fabrication` | Proto Labs sheet metal page | Named; **not verified** | Clean | Contrast route | Source states no tolerance, no maximum part size and no hole limits. Their absence is silence, not freedom. |
| `prc-injection-moulding` | Proto Labs injection moulding page | Named; **not verified** | Clean | Volume-decision contrast | Carries a dated price; see §5. |
| `std-en-755-2` | Citation in the Aalco 6082 datasheet | Named; **not verified** | 2 warnings: `TEMPEST-STD-001` no title, `TEMPEST-STD-005` publisher status unknown | The standard the 6082 minima are stated against | No official title obtained — the CEN catalogue refused automated retrieval. Publication status unknown, so nothing confirms 2008 is current. |
| `std-en-10025-2` | Citation in the Siderticino datasheet | Named; **not verified** | 2 warnings: `TEMPEST-STD-001`, `TEMPEST-STD-005` | The standard the S355J2 minima are stated against | As above. |
| `std-iso-2768-1` | Citation on the Proto Labs machining page | Named; **not verified** | 2 warnings: `TEMPEST-STD-001`, `TEMPEST-STD-005` | The general tolerance class unmarked machined dimensions fall to | As above. |
| `rule-mfg-milling-envelope` | Proto Labs capability | Named; **not verified** | Clean | Bounds the manufacturing decision | Restates one supplier's commercial limit. Must not be read as a limit of CNC milling. |
| `rule-mfg-general-tolerance` | Proto Labs capability | Named; **not verified** | Clean | Governs unmarked dimensions on the bracket drawing | The ±0.127 mm figure is this supplier's; the principle is general. |
| `rule-tde-material-standard` | Authored by Tempest Design Engineering | Authored; **not reviewed** | Clean | Requires the bracket's material to cite its standard | Authored, so it carries Tempest's authority and nobody else's. Its machine condition is only a presence check; the rule sets `RequiresHumanReview` for that reason. |
| `rule-tde-strength-condition` | Authored by Tempest Design Engineering | Authored; **not reviewed** | Clean | Requires the bracket's strength figures to carry their conditions | As above. |

---

## 3. Release candidacy

Records are grouped by how much work a reviewer faces, not by how much the
scenario wants them.

### 3.1 Strongest candidates — a reviewer can check these against a primary source today

| Records | Why |
|---|---|
| All 12 constants (`const-*`) | NIST CODATA 2022 is primary, authoritative, public domain, machine-readable and still online at a stable URL. A reviewer can diff the stored values against the published table in minutes. Exactness and uncertainty are both recorded as the source states them. |

### 3.2 Reviewable, with a caveat the reviewer must accept

| Records | Why | The caveat |
|---|---|---|
| The 6 materials | The datasheets are online and readable, and the values are unambiguous | Each is a **secondary** source restating an EN standard. A reviewer verifies "this is what Aalco published", not "this is what EN 755-2 requires". Whether that is sufficient is a governance decision, not a data question. |
| The 4 processes | Same — the supplier's pages are online and the figures are explicit | The reviewer is verifying a **commercial capability statement** that will change without notice. A verification date matters more here than anywhere else in the corpus. |
| The 2 bearings | The manufacturer's specification pages are online | Manufacturer-specific load ratings. Verifying them says nothing about any other manufacturer's 6205. |
| The 3 supplier records, 1 cost, 2 lead times | Attributable to pages that were read | The cost and lead times are time-sensitive; see §5. |

### 3.3 Not yet release candidates

| Records | Why not |
|---|---|
| The 7 fasteners | Cite a tertiary index as a placeholder for ISO 262. A reviewer cannot verify them against the standard without buying it, and verifying them against the encyclopaedia entry would only confirm the transcription, not the fact. |
| The 6 untitled standards | Cannot be cited bibliographically at all (`TEMPEST-STD-001`), and their publication status is unknown (`TEMPEST-STD-005`). Releasing a citation index whose entries cannot be cited would be self-defeating. |
| The 8 titled standards | Better, but still sourced from a tertiary index rather than the ISO catalogue. |
| The 2 authored rules | Authored content needs a reviewer other than its author. Tempest wrote them; Tempest must have a second person read them. |
| All 5 P05 assets and all 5 P06 knowledge records | Deliberately unfinished — a calculation with no result, a verification that verified nothing, a review nobody attended. There is nothing to verify yet. |

---

## 4. The three known data issues, carried forward explicitly

### 4.1 The Aalco 5083 density — keep it omitted

The source datasheet prints **`Density 265 g/cm³`**, confirmed by two
independent reads. That is roughly twelve times the density of osmium and
is evidently a misplaced decimal point for 2.65 g/cm³.

**The property is absent from `mat-5083-o-h111` and must stay absent.**
Recording 265 would publish a false value; recording 2.65 would publish a
value no source states. The record's own notes carry the full explanation,
and `SeedDatasetTests.TheAluminiumRecordWithABadPublishedDensity_HasNoDensityAtAll`
fails if anybody fills the gap in.

**For the reviewer:** this is not a transcription error to fix. It is a
question to put to Aalco, and until they answer it the honest state of
TempestOS's knowledge of 5083's density is *unknown*.

### 4.2 `TEMPEST-MFG-005` — turning and wall thickness

**The contradiction.** Proto Labs publishes a minimum wall thickness of
0.51 mm for factory CNC turning. `TEMPEST-MFG-005` holds that a wall
thickness capability may not be recorded against a turning process,
because turning does not produce a wall.

**What the population phase did.** Moved the figure into a
`ProcessConstraint` on `prc-cnc-turning`, so the fact survives and the rule
is not weakened. The constraint text states the disagreement in full.

**Why it is still open.** A turned tube plainly has a wall, and a designer
turning a thin-walled bush needs that number where a capability query will
find it — which is exactly where the rule forbids putting it. So one of
these is wrong:

- the rule's premise, that turning produces no wall thickness; or
- the model's assumption that "wall thickness" means only a moulded or
  formed wall.

**This is an engineering question, not a defect.** It is recorded here as
a decision requiring human authority. The integration phase surfaces it
through the validation mechanism (see the integration report's refusal
tests) rather than resolving it. **Do not weaken the rule to make a
scenario pass, and do not overwrite the supplier's figure to make the rule
pass.**

### 4.3 The empty failure and lessons library

`Tempest.Core.Knowledge.Lessons` holds nothing, deliberately: populating it
means writing up a real company's failure or inventing one.

Where an end-to-end scenario needs failure or lessons content, it must use
**explicitly fictional test content, labelled as such**, and that content
must live under `tests/` rather than in the shipped corpus.

---

## 5. Time-sensitive records

| Record | What expires | Observed |
|---|---|---|
| `cost-protolabs-mould-tooling` | A published starting price of USD 1,495 | 2026-09-07 |
| `lead-protolabs-cnc-machining` | Advertised turnaround, 1–4 working days | 2026-09-07 |
| `lead-protolabs-sheet-metal` | Advertised turnaround, 1–5 working days | 2026-09-07 |

Each carries its observation date in `CommercialSource.ObservedOn`. A
reviewer releasing any of these is asserting the figure was true on that
date — never that it is true now. Any commercial use requires a fresh
quotation.

---

## 6. What a reviewer would have to do

For each record they intend to release:

1. Open the source named in its provenance.
2. Compare every recorded value, unit and condition against it.
3. Confirm the extraction method recorded is what actually happened.
4. Revise the record's provenance with their own principal id, the date,
   and `VerifiedAgainstSource`.
5. Transition it `Draft → Checked → Validated → Released`.

Steps 4 and 5 are the governed human action this phase does not perform and
must not simulate.

**Known gap in step 4:** `ReferenceProvenance.ReviewerPrincipalId` is a
caller-supplied string. Nothing binds it to the principal actually signed
in, so a reviewer's name can be written by anybody with write access. The
underlying document revision does record the real principal, so the
forgery would be detectable — but the provenance itself is not
self-attesting. Raised as an integration-phase finding.
