# Parallel Programme A — Engineering Reference Data

**Part of** [Parallel Work Programme A–G](Parallel%20Work%20Programme%20A–G.md).
**Position in recommended order:** 1st (A → G → C → E → F → B; D concurrent).
**Status of every sub-package below:** Defined, not started (2026-09-05).
**Claude Code required for this programme:** No.

## Programme Purpose

The reference data every other parallel programme cites. Materials,
standards, fasteners, bearings, mechanical components, constants and
manufacturing processes — produced once, in a clean tabular form, so
that Programmes B, C, E and F all reason against the same vocabulary
instead of each inventing one.

**Programme-level acceptance:** every dataset below exists as a
delimited file (CSV or equivalent) with a stated header row, a stated
unit for every dimensional column, and a cited source for every row.
An uncited row is not accepted, however plausible.

**Standing rule for this programme:** where a value is genuinely not
known, the cell reads `Unknown` — never an invented number. A materials
table with fifty honest rows is worth more than two hundred with
plausible fillers, because the fifty can be used in a calculation.

---

## A.1 — Materials Database

**1. Purpose.** Give every downstream calculation, selection decision
and quotation a single, sourced set of engineering material properties.

**2. Scope.** *In:* the material families actually used in the target
work — structural and stainless steels, aluminium alloys, common
engineering plastics and elastomers, and the copper alloys used for
bearings and bushes. Mechanical, thermal and physical properties;
standard designations; typical forms. *Out:* full alloy-by-heat-treat
matrices, exotic aerospace alloys, composites, and any property that
requires test data this business does not hold.

**3. Required inputs.** Material standards (EN, ASTM, ISO) or supplier
datasheets for each entry; a decided unit convention (SI, with the unit
recorded per column); the target material families list.

**4. Data / content fields.** `MaterialID`; `Name`; `Designation`;
`Standard`; `Family`; `Condition/Temper`; `Density (kg/m³)`; `Young's
Modulus (GPa)`; `Poisson's Ratio`; `Yield Strength (MPa)`; `Ultimate
Tensile Strength (MPa)`; `Elongation (%)`; `Hardness`; `Thermal
Conductivity (W/m·K)`; `Coefficient of Thermal Expansion (µm/m·K)`;
`Specific Heat (J/kg·K)`; `Max Service Temperature (°C)`;
`Corrosion Resistance (qualitative)`; `Machinability (qualitative)`;
`Weldability (qualitative)`; `Typical Forms`; `Relative Cost Index`;
`Typical Applications`; `Source`; `SourceDate`; `Confidence`
(Verified / Inferred / Unknown); `Notes`.

**5. Outputs / artefacts.** `Materials Database.csv`; a short
`Materials Database — Field Definitions.md` stating every column's
meaning, unit and allowed values; a source list.

**6. Acceptance criteria.** Every row has a designation, a standard, a
source and a confidence mark. Every dimensional column has one declared
unit used consistently. No blank cells — `Unknown` is written
explicitly. The file opens cleanly and round-trips through a spreadsheet
without column drift.

**7. Dependencies.** None. This is the programme's own root; `A.2`
(Standards Library) will later supply the standard citations, but `A.1`
does not wait for it.

**8. Recommended next action.** Fix the column list above, then populate
the twenty materials the business actually uses most — depth before
breadth.

**9. Claude Code required?** **No.** This is data entry against
published sources; a spreadsheet is the right tool.

**10. TempestOS integration.** **Yes.** TempestOS already ships a
Materials framework (`FCR-0031`, `v0.7.0`). A later numbered technical
Work Package can import this file as seed data. That importer is not in
scope here, and no schema is designed against this file yet — the file
is written to be *correct*, not to match a schema.

---

## A.2 — Standards Library

**1. Purpose.** Record which engineering standards apply to this
business's work, what each one governs, and which edition is held —
so a drawing or specification cites a standard that has actually been
read.

**2. Scope.** *In:* the standards genuinely used — dimensional and
tolerancing, materials, fasteners, welding, surface finish, drawing
practice, safety and machinery directives where applicable. *Out:*
reproducing any standard's own text (licensed content), and standards
the business neither holds nor applies.

**3. Required inputs.** The list of standards held or subscribed to;
the domains the business works in; access to each standard's own scope
statement (title and abstract are freely citable).

**4. Data / content fields.** `StandardID`; `Body` (ISO/EN/BS/ASTM/…);
`Number`; `Edition/Year`; `Title`; `Domain`; `Applies To`; `Supersedes`;
`Superseded By`; `Status` (Current / Withdrawn / Unknown); `Held?`
(Yes / No / Subscription); `Where Held`; `Typical Use In Our Work`;
`Related Standards`; `Notes`.

**5. Outputs / artefacts.** `Standards Library.csv`; a one-page
`Standards — How We Cite Them.md` fixing the citation format used on
drawings and in specifications.

**6. Acceptance criteria.** Every row states an edition and a currency
status. No standard's own copyrighted text is reproduced. Every
"Applies To" entry maps to real work the business does.

**7. Dependencies.** None to start. Feeds `A.1`, `A.3`, `E.1` and `E.3`.

**8. Recommended next action.** List the standards already cited on
existing drawings and quotes — that list is the honest starting scope.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes, later** — as a reference register
behind document and verification metadata. Low urgency; useful as a
lookup long before it is a product feature.

---

## A.3 — Fastener Library

**1. Purpose.** Make fastener selection and BOM entry a lookup rather
than a recalculation, with the strength and torque data attached.

**2. Scope.** *In:* metric bolts, screws, nuts and washers in the
property classes actually used; head styles; thread pitches (coarse and
fine); strength grades; preload and torque guidance; standard lengths.
*Out:* imperial series unless a real project demands it, and any
application-specific torque value that depends on a joint this library
cannot see.

**3. Required inputs.** ISO/EN fastener standards; property class data;
a decided friction-coefficient assumption for any tabulated torque, and
that assumption must be stated on the table itself.

**4. Data / content fields.** `FastenerID`; `Type`; `Standard`;
`Thread Designation`; `Nominal Diameter (mm)`; `Pitch (mm)`;
`Length (mm)`; `Head Style`; `Drive`; `Property Class`; `Material`;
`Coating/Finish`; `Tensile Stress Area (mm²)`; `Proof Load (kN)`;
`Recommended Preload (kN)`; `Tightening Torque (N·m)`;
`Assumed Friction Coefficient`; `Clearance Hole (mm)`;
`Tapping Drill (mm)`; `Across Flats (mm)`; `Standard Availability`;
`Source`; `Confidence`; `Notes`.

**5. Outputs / artefacts.** `Fastener Library.csv`; `Fastener Torque
Assumptions.md` (one page, stating the friction and joint assumptions
behind every tabulated torque); a preferred-fastener shortlist.

**6. Acceptance criteria.** Every torque figure carries its assumption
explicitly. Stress areas and proof loads trace to a cited standard. A
preferred subset is marked, so designs converge on stock items.

**7. Dependencies.** `A.1` (fastener materials), `A.2` (standards).
Neither blocks a start.

**8. Recommended next action.** Populate M3–M16, classes 8.8/10.9/A2-70,
coarse thread only. That covers the large majority of real use.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes** — as a standard-parts catalogue
behind BOM and product-structure entry. A later technical Work Package's
job.

---

## A.4 — Bearing Library

**1. Purpose.** Support bearing selection and life calculation without
returning to a manufacturer's catalogue for every enquiry.

**2. Scope.** *In:* deep groove ball, angular contact, cylindrical and
tapered roller, thrust and plain bearings in the size ranges used;
dynamic and static load ratings; speed limits; dimensions; fits and
tolerances guidance. *Out:* manufacturer-proprietary internal geometry,
and any lubrication recommendation not traceable to a published source.

**3. Required inputs.** Manufacturer catalogues (publicly published
ratings); ISO bearing designation conventions; the shaft-size ranges the
business actually designs around.

**4. Data / content fields.** `BearingID`; `Designation`; `Type`;
`Bore d (mm)`; `Outer Diameter D (mm)`; `Width B (mm)`;
`Dynamic Load Rating C (kN)`; `Static Load Rating C₀ (kN)`;
`Fatigue Load Limit (kN)`; `Limiting Speed (rpm)`;
`Reference Speed (rpm)`; `Mass (kg)`; `Seal/Shield`; `Cage Type`;
`Recommended Shaft Fit`; `Recommended Housing Fit`;
`Lubrication`; `Manufacturer`; `Source`; `Confidence`; `Notes`.

**5. Outputs / artefacts.** `Bearing Library.csv`; `Bearing Selection
Notes.md` covering the L10 life relation and the fit-selection basis, in
both cases citing the source rather than restating a catalogue.

**6. Acceptance criteria.** Every rating is attributed to a named
manufacturer and catalogue. Dimensions are complete enough for a layout
without a further lookup. Load ratings are never averaged across
manufacturers.

**7. Dependencies.** `A.6` (constants, for life calculation) — helpful,
not blocking.

**8. Recommended next action.** Populate the 6000/6200/6300 deep-groove
series first; it covers most general machine design.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes** — as catalogue data behind
component selection and calculation inputs.

---

## A.5 — Springs, Gears & Mechanical Components

**1. Purpose.** Extend the standard-component reference beyond fasteners
and bearings to the other parts that recur in machine design.

**2. Scope.** *In:* compression, extension and torsion springs; spur and
helical gears, and the basic gear geometry relations; belts, chains and
pulleys; seals and O-rings; linear guides, ball screws, shafts, keys and
couplings. *Out:* full gear rating calculations to ISO 6336 (that is
`B.3`/`E.2` territory), and any bespoke component.

**3. Required inputs.** Standard component catalogues; O-ring and key
standard dimension tables; the component families actually used.

**4. Data / content fields.** Common: `ComponentID`; `Category`;
`Designation`; `Standard`; `Material`; `Key Dimensions`;
`Rating/Capacity`; `Manufacturer`; `Source`; `Confidence`; `Notes`.
Category-specific: springs — `Wire Diameter`, `Mean Coil Diameter`,
`Free Length`, `Rate (N/mm)`, `Max Deflection`, `Solid Length`; gears —
`Module`, `Teeth`, `Pressure Angle`, `Face Width`, `Pitch Diameter`,
`Helix Angle`, `Quality Grade`; seals — `Section`, `Inside Diameter`,
`Groove Dimensions`, `Compound`, `Temperature Range`; keys —
`Width`, `Height`, `Length`, `Shaft Diameter Range`, `Tolerance`.

**5. Outputs / artefacts.** One CSV per category, plus a
`Mechanical Components — Index.md` naming each file and its scope.

**6. Acceptance criteria.** Each category file is internally consistent
in units and complete for the sizes the business uses. Geometry
relations (module, pitch diameter) are stated once, in the index, not
repeated per row.

**7. Dependencies.** `A.1`, `A.2`. Neither blocks a start.

**8. Recommended next action.** Start with O-rings and keys — small,
finite, standardised tables that can be finished in a sitting and used
immediately.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes** — same route as `A.3`/`A.4`:
standard-parts catalogues behind BOM and selection.

---

## A.6 — Engineering Constants & Fundamentals

**1. Purpose.** Fix one authoritative set of constants, unit
conversions and standard formulae, so no two calculations in this
business silently use different values.

**2. Scope.** *In:* physical constants; SI and imperial conversion
factors; standard gravity; common section properties (area, second
moment of area, section modulus) for standard profiles; beam deflection
and stress cases; stress/strain, thermal and fluid relations; safety
factor conventions. *Out:* derivations, and any relation the business
does not actually apply.

**3. Required inputs.** Standard engineering references; the unit
convention decision already made in `A.1`.

**4. Data / content fields.** `ConstantID`; `Name`; `Symbol`; `Value`;
`Unit`; `Uncertainty`; `Category`; `Source`. For formulae: `FormulaID`;
`Name`; `Expression`; `Variables and Units`; `Assumptions`;
`Valid Range`; `Source`; `Worked Example Reference`.

**5. Outputs / artefacts.** `Engineering Constants.csv`;
`Unit Conversions.csv`; `Standard Formulae.md`; `Section
Properties.csv`.

**6. Acceptance criteria.** Every formula states its assumptions and
valid range — a formula without stated assumptions is not accepted.
Every constant carries a unit. Conversion factors are exact where the
definition is exact, and marked as rounded where not.

**7. Dependencies.** None. This is the second root of the programme,
alongside `A.1`.

**8. Recommended next action.** Write the unit convention and the safety
factor convention first, on one page. Those two decisions govern every
later calculation pack in `E.2`.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes** — TempestOS already ships a Units
& Quantities framework (`FCR-0030`) and a Calculation framework
(`FCR-0032`); these constants and formulae are natural seed content for
both. A later technical Work Package's job.

---

## A.7 — Manufacturing Process Library

**1. Purpose.** Describe every manufacturing process the business
specifies or buys, with its real capabilities and limits — the
engineering half of what `C.2` later prices.

**2. Scope.** *In:* machining (turning, milling, drilling, grinding),
turning-centre and multi-axis work, sheet metal (laser, punch, bend,
weld), fabrication and welding, casting, moulding, additive, and surface
treatments. Achievable tolerances, surface finishes, size envelopes,
typical batch sizes and design-for-manufacture constraints. *Out:*
costs and lead times — those are `C.2` and `C.3` deliberately, so that
engineering capability and commercial reality stay separable.

**3. Required inputs.** Process capability references; real supplier
capability statements where held; the processes actually used in
delivered work.

**4. Data / content fields.** `ProcessID`; `Process`; `Family`;
`Sub-process`; `Typical Materials`; `Achievable Tolerance (mm)`;
`Best-case Tolerance (mm)`; `Surface Finish Ra (µm)`;
`Min/Max Size Envelope`; `Min Wall/Feature Size`;
`Typical Batch Size Range`; `Tooling Required`; `Setup Complexity`;
`Design Constraints`; `Common Defects`; `Post-processing Needed`;
`Quality Checks`; `Source`; `Confidence`; `Notes`.

**5. Outputs / artefacts.** `Manufacturing Process Library.csv`;
`Design for Manufacture — Process Notes.md` (one page per process
family, constraints only).

**6. Acceptance criteria.** Every tolerance and finish figure is
attributed either to a cited reference or to a named supplier's own
stated capability — never to general impression. Processes the business
does not use are absent, not padded in.

**7. Dependencies.** `A.1` (materials). Feeds `B.2`, `C.2`, `C.3`.

**8. Recommended next action.** Document the three processes most used
in the last year, from real jobs, before adding any process from a
textbook.

**9. Claude Code required?** **No.**

**10. TempestOS integration.** **Yes** — TempestOS ships a Manufacturing
Workspace (`WP 9.5A`, `v0.9.0`); this library is candidate reference
content behind it. Import is a later numbered Work Package.
