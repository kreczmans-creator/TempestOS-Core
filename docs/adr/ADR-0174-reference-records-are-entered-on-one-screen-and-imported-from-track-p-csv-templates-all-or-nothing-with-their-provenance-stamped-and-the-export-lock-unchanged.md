# ADR-0174: Reference Records Are Entered on One Screen and Imported From Track P's CSV Templates, All or Nothing, With Their Provenance Stamped and the Export Lock Unchanged

## Status

Accepted (v0.26.0, Addendum C, Track S, C-3). Builds on `ADR-0165` (the worksheet engine and its reference records), `ADR-0167` (released reference records and the Calculations node), `ADR-0171` (untrusted files pass through one set of hardened primitives) and the licensed-source rule of Addendum A §A5.

## Context

A calculator takes its tabulated values (ampacities, correction factors, bearing ratings, national-annex factors, climate rows) from reference records. No standards-derived record ships (§A5), so every record is typed by the owner from a licensed copy. Until C-3 there were two ways to make one, and both were poor for a table of fifty rows:

- the worksheet form's **Save inputs as record** panel, which saves what is typed on one sheet, one kind at a time, and only for a saved draft;
- a material's JSON file (W3).

Track P (verification) wrote 82 header-only CSV templates with notes (`docs/verification/record-templates/`), a `_provenance.csv`, and a README that matches each template to a record kind and says what an importer must check. They left five questions to Track S: how a template pairs with its provenance row, how a unit is read, how a table kind and a scalar kind differ on import, how the materials templates (62 to 65) add up, and which templates have no kind. A record kind is also nowhere marked required: an owner opening Conductor sizing sees a blocked sheet and no pointer to what it is missing.

## Decision

### 1. One records screen, reached from the worksheet

**Engineering → Modules → Calculations → Reference records** (`CalcRecordsView`, with `CalcRecordsViewModel` holding all behaviour and no UI). The left pane lists every record kind a visible calculator declares, each with its count; a kind that a calculator cannot do without and that has no record is marked `!`. The right pane shows, for the kind picked: its records, then the entry of new ones. The worksheet form links to it two ways: a **Fill** link per missing record (below), and an **Open records screen** button in the form's Reference records panel. `EngineeringAreaView.OpenRecordsAsync(kind)` is the one navigation entry; the composer hands it to `CalcSheetCoordinator.OpenRecordsScreenAsync`.

The screen lists a record's title, revision, edition, table, origin, who entered it, when, whether it is licensed, and which symbols it holds. **It never shows the values of a licensed record, and it has no export of any record**: the entry grid and the file preview show what the person is typing or importing, and nothing afterwards.

### 2. The templates are a catalogue in code, tied to the calculators

`RecordTemplates` holds Track P's 82 templates: file number and name, the record kind it fills, the header row, one line of notes, and the checks the notes ask for. A contract test reads the template files and the README's kind map and fails if either drifts from the catalogue. Each template is one of:

| Disposition | Meaning | Count |
|---|---|---|
| `Kind` | a visible calculator declares the kind: importable | 71 |
| `Proposed` | the notes propose a kind no calculator declares (03, 05, 19, 42, 43, 44, 48) | 7 |
| `Superseded` | no calculator reads it (09, 35, 36, 37) | 4 |

A proposed or superseded template is refused with the notes' reason; when a calculator starts to declare a proposed kind, a test fails until its template is switched to `Kind` and given its checks. The five record kinds that no template fills (`motor.rating`, `cable.impedance`, `motor.start`, `starter.limits`, `transformer.data`) are pinned by the same test; the three `lifting.*` kinds belong to the S8 calculator hidden by the lifting flag (ADR-0173) and are not listed while it is off.

What a column becomes is **derived from the calculators' own input specifications**, not restated: `RecordKindSchema` is built from the catalogue and gives, for each kind, its scalar symbols and its tables (with their columns, units, hard and soft limits, row caps and choices). A template column that is a scalar symbol of the kind is a value; one that is a column of a table symbol is a cell of that table (the template then makes **one record for the file**); a text column that is neither is a label that joins the title; a number that is neither is stored and never applied. A template that makes a record per row (`06`, `12`, `24`) therefore needs no special case, and neither does one that mixes scalars read from the first row with a table (`02`, `15`). A kind that two calculators declare with different tables (`material.conductivity`, B4 and B7) takes the union of their columns, and a template that holds only some columns of a table says so.

### 3. A file is read through the hardened primitives, and nothing is guessed

`RecordImportService.PreviewAsync` reads bytes through `BoundedInputReader` (5 MB, strict UTF-8, control characters refused), `CsvReader` (RFC 4180, a malformed file is refused whole with its line) and `LocaleNumber` under a `NumberProfile` the person chooses on the screen (a point or a comma, never taken from the machine). Text that a spreadsheet would read as a formula is an error. No new parser was written.

### 4. Units are checked per column, in the header

A header is `symbol[unit]`. Track P's ASCII form (`mm2`, `A2*s`, `J/(K*mm3)`) is read as the engine writes it (`mm^2`). The unit is checked against the unit the calculator takes:

- **Match**: accepted.
- **Convertible** (the same dimension, another unit, `mA` for `A`): accepted with a warning; the values are **kept as typed** and judged in the calculator's unit. The calculators already convert on use, and a record that shows what the person typed is easier to check against the source than one rewritten on import.
- **Missing** (a dimensioned column with no unit), **Mismatch** (another dimension, a unit on a plain number or on text, text where a number is taken), **Unknown**: errors. A number without its unit is not read as SI.

### 5. Rows are judged by the calculator's limits first, then the template's rules

For every number, the calculators' **hard** limits are applied in the calculator's unit: a value outside what *every* calculator that reads the kind accepts is an error (a slip in a unit or a column is likely); one that only some refuse is a warning naming them; the soft range is a warning. The template's own rules (required cells, duplicates, orderings within a table, the warning ranges) come from the notes' "Importer checks". The monotonic rules follow the notes' wording: where a note says "must fall" the check is strict; where it says "must not rise" it is not. An error names the line and the column; a preview lists them with the planned records. The typed grid goes through the same pipeline (`GridToCsv`, then the importer) so a typed table and a file cannot be judged differently.

### 6. All or nothing, in one transaction

`CommitAsync` analyses again against the store as it is, and if anything is an error nothing is written. Otherwise `CalcInstanceStore.SaveRecordsAsync` writes every record in **one transaction**: a record that exists, a repeated revision or a failed commit leaves the store as it was. The existing save logic (`PrepareRecordAsync`, split out of `SaveRecordCoreAsync` so both share it) applies unchanged to each record: shape, material stamping, the licence carried from an earlier revision.

### 7. Provenance is required and stamped

Title, source title, edition, table or clause and licence holder are required; the amendment is blank only if the copy has none (Track P's rule) and is joined to the edition (`2018; A2:2022`), as the record keeps one edition field. Who entered the record is **the signed-in person**, not the file's `entered_by`. **`licensed_source` is on by default**, so the values never leave through an export. Each value carries its own `CalcValueSource`: source, edition, table, licence class, who, when, and the **SHA-256 of the imported file** as the evidence hash; the record's notes name the file, the hash and the template. A typed table carries no hash and says it was typed. A Track P `_provenance.csv` can be read to fill the fields (`ReadProvenanceFile`); an `Indicative` origin is refused (that flag is for the shipped seeds).

A title that exists already is refused unless the import is saved **as its next revision** (a record is never edited; calculations that used it go stale, as before). Once a record is licensed in any revision a revision cannot release it.

### 8. Materials

Template 62 makes one material per row. Templates 63 (a property against temperature), 64 (the secant curve) and 65 (friction pairs) add a table to a material that is already in the library, as **one new revision per material** for the whole file. Property units and ranges are the W3 library's own (`MaterialProperties`).

### 9. Which records a calculator is missing

`RecordImportService.RequirementsAsync(template)` lists, for each kind a calculator declares, whether a record holds a value the calculator takes (a record of `ec3.national-annex` with only the S6 factors does not satisfy S7), how much the calculator needs it, and the templates that fill it. The need cannot be read from the calculators (their record kinds carry no such flag, and adding one would change every template's hash), so `RecordRequirements` states it once: **Required** (12 entries: E4 ampacity and ratings, E8 space factor, S5 factors, B4 and B7 layers, B7 climate, B8 Sa and K1, M8 friction, M7 wire strength and endurance), **Conditional** (a fuse's curve, an annular snap), **Optional**, and **Recommended** for everything else. A test holds the table to the catalogue. The worksheet form shows the missing required records first, in words, each with a **Fill** link, then the rest smaller. The material library counts as present (it ships four generic seeds).

### 10. The export lock is untouched

No change was made to `ExportRecordAsync`, `WithholdLicensedAsync`, the sheet's withheld rows or the CSV and JSON re-import rules. The import adds no path that writes a record's values out: its only output is a template's header row (`BlankTemplateCsv`). A test holds the public surface of the importer free of any export method, and tests import a licensed record, apply it, and show it is refused for export and withheld from a calculation's export.

### 11. Changes made after the C-3 review

- **A table a calculator selects from is refused when it runs the wrong way.** Template 04 and 10's rule that I_t rises with CSA (and, new, falls as the AWG number rises) is an **error**, not a warning: E4 picks the smallest cross-section whose I_t carries the current, so 270 A at 4 mm² followed by 34 A at 6 mm² would silently choose a wrong conductor. Every other ordering rule on a table a calculator selects or interpolates on (02, 06, 07, 08, 12, 29, 33, and 11's C_g) was already an error. The ones that stay warnings do not feed a lookup: 11's C_a (the note allows a rise below the reference ambient and E4 takes C_a as one number) and 39's Rm against d (one record per row, and M7 reads one Rm). Template 39's direction was the wrong way round against its note ("warn when Rm rises as d rises"); it now follows the note. A broken ordering is reported on **both rows** of the pair and names the two values (`I_t must rise as CSA rises: CSA 4 has I_t 270, but CSA 6 has I_t 34`).
- **A text cell is one line.** A line break, a tab or any other control character in a text cell, a label, a material name, the provenance typed on the screen or a field of a provenance file is an error naming the column and the line (`grout: the text on line 3 holds a line break or other control character. Put it on one line.`). A formula behind a line break in a quoted cell is not at the start of the cell, so the formula check alone missed it. `ExportRecordAsync`'s `Csv()` also quotes any value that holds a carriage return or a line feed, and keeps the leading-apostrophe guard for a formula.
- **Template 62 as the next revision carries the material forward.** The values the file does not set (a blank cell, a column left out, the E(T) tables and curves added by 63 to 65) are carried forward from the latest revision, as 63 to 65 already carry the rest, with their own sources. The preview says so on the row: `'Steel' is saved as its next revision: E(T), G, nu, rho are carried forward from revision 2 because the file does not set them.` A blank cell therefore cannot clear a property; a revision that must drop one is entered on the material's own form.
- **A per-row record is titled by its text labels and its row number, never by a number from the table.** The title is printed in the citation on every sheet, so a stored value from a licensed table (`theta_a 20 degC`) no longer joins it: `Ambient [air, PVC, row 2]`. The row number is added when the row also holds stored numbers (or has no label) in a file of more than one row. A stored-but-unapplied number is judged by no calculator, so each carries a **hard range** in the template (an error): `theta_a` in 06 and 11 -100 to 500 degC, `n_circuits` 1 to 1000, `length_in_insulation` 0 to 100 000 mm, `t_min` and `t_max` in 12 0 to 1000 mm, `t_mean_h` and `t_mean_c` in 16 -100 to 1000 degC, `d_min` and `d_max` in 22 0 to 5000 mm and in 40 0 to 1000 mm, `Fa_C0` in 25 0 to 100, `concentration` 0 to 100 %, `t_fill` and `t_max` in 27 -100 to 500 degC, `theta` in 29 and 30 -100 to 500 degC, `altitude` -500 to 20 000 m, `d` in 39 0 to 1000 mm, `N_c` in 53 1 to 1e15. A contract test lists the stored-but-unapplied columns from the importer's own column roles and fails if one has no hard range.

## Consequences

- A table of fifty rows is typed once on a grid, or imported from the owner's spreadsheet, and checked before anything is saved. The sheets' "a tabulated value not taken from a reference record" warning clears the way it already did.
- 71 of the 82 templates import. The other 11 are not defects of the importer: seven wait for a calculator to declare their kind and four are Track P's own "do not fill" files. Track S decides which proposed kinds to add (B4's room temperatures, air-change rates and ground U-values; E5's maximum Zs; E3's voltage drop; S6's concrete strengths; B4's surface resistances).
- Five declared kinds have no template and are entered from a worksheet (**Save inputs as record**): the E-series motor kinds (E1 and E9).
- Template 49 holds only the `name` and `lambda` columns of B4's layers table and 56 holds B7's; applying either replaces the whole table, as Track P's README foresaw. The importer warns when a template leaves a required table column out.
- A value that is too large for one calculator and fine for another (`rho_20` in two units across E5, E7 and E9) is a warning that names the calculator, not an error.
- The preview shows what the person typed or imported, including a licensed record's numbers, on their own screen before they are saved. That is not an export; it is the review the Track P README asks for ("re-read ten percent of the rows"). After the save the screen shows symbols and row counts only.

## Alternatives considered

- **Parse Track P's CSV files at run time** to learn each template's header. Rejected: the application would depend on the `docs/` folder; the catalogue is checked against it by a test instead.
- **A hand-written spec per template for units and ranges.** Rejected: it would repeat what the calculators already declare and drift from it. Only what the notes alone know is written down.
- **Convert every value to the calculator's unit on import.** Rejected: it rewrites the owner's numbers, and the calculators convert anyway.
- **Mark required kinds on `CalcRecordKind`.** Rejected: it changes every template's content hash and version; the table in the Library is reviewed by a test.
- **A save per record.** Rejected: a file with one bad row would leave a half-entered table that a calculator would then apply.

## Appendix: the 82 templates against the record kinds in code

Generated from `RecordTemplates` and the calculators' `RecordKinds` at the time of writing; the contract tests hold it. "Declared by" names the calculators that list the kind.

| Template | Record kind | Declared by | Import |
|---|---|---|---|
| 01 `conductor.material` | `conductor.material` | E5, E7, E9 | imports |
| 02 `device.characteristic` | `device.characteristic` | E5 | imports |
| 03 `bs7671.max-zs` | `bs7671.max-zs` (proposed) | — | proposed kind: refused until a calculator declares it |
| 04 `bs7671.ampacity` | `cable.ampacity` | E4 | imports |
| 05 `bs7671.voltage-drop` | `bs7671.voltage-drop` (proposed) | — | proposed kind: refused until a calculator declares it |
| 06 `bs7671.factors-ambient` | `cable.correction-factors` | E4 | imports |
| 07 `bs7671.factors-grouping` | `cable.correction-factors` | E4 | imports |
| 08 `bs7671.factors-insulation` | `cable.correction-factors` | E4 | imports |
| 09 `bs7671.factors-protection` | — | — | superseded: not imported |
| 10 `en60204.ampacity` | `cable.ampacity` | E4 | imports |
| 11 `en60204.factors` | `cable.correction-factors` | E4 | imports |
| 12 `steel.strengths` | `steel.strengths` | S4, S6 | imports |
| 13 `ec3.partial-factors` | `ec3-1-1.national-annex` | S6 | imports |
| 14 `ec3.national-annex` | `ec3.national-annex` | S4, S6, S7, S9 | imports |
| 15 `en1990.factors` | `en1990.factors` | S5 | imports |
| 16 `fluid.properties` | `fluid.properties` | B5 | imports |
| 17 `hydraulics.velocity-limits` | `cylinder.line-limits` | F1 | imports |
| 18 `en1992.factors` | `ec2.national-annex` | S6 | imports |
| 19 `concrete.strengths` | `concrete.strengths` (proposed) | — | proposed kind: refused until a calculator declares it |
| 20 `ec3.ltb-parameters` | `ec3.ltb-factors` | S7 | imports |
| 21 `ltb.moment-factors` | `ec3.mcr-factors` | S7 | imports |
| 22 `shaft.material` | `shaft.material` | M4 | imports |
| 23 `shaft.limits` | `shaft.limits` | M4 | imports |
| 24 `bearing.ratings` | `bearing.ratings` | M5 | imports |
| 25 `bearing.factors` | `bearing.factors` | M5 | imports |
| 26 `bearing.aiso` | `bearing.aiso` | M5 | imports |
| 27 `fluid.expansion` | `fluid.expansion` | B6 | imports |
| 28 `emitter.rating` | `emitter.rating` | B6 | imports |
| 29 `psu.derating` | `psu.derating` | E6 | imports |
| 30 `battery.factors` | `battery.factors` | E6 | imports |
| 31 `oring.gland` | `oring.criteria` | F3 | imports |
| 32 `oring.sizes` | `oring.ring` | F3 | imports |
| 33 `oring.extrusion-limits` | `oring.extrusion` | F3 | imports |
| 34 `asme.b31-3.allowable-stress` | `pipe.allowable-range` | B8 | imports |
| 35 `asme.b31-3.stress-range` | — | — | superseded: not imported |
| 36 `asme.b31-3.thermal` | — | — | superseded: not imported |
| 37 `containment.cable-factors` | — | — | superseded: not imported |
| 38 `containment.conduit-trunking` | `containment.space-factor` | E8 | imports |
| 39 `en10270.spring-wire` | `spring.wire-strength` | M7 | imports |
| 40 `en13906.permissible-stress` | `spring.permissible` | M7 | imports |
| 41 `climate.design-temperatures` | `climate.design-temperature` | B4 | imports |
| 42 `room.internal-temperatures` | `room.internal-temperatures` (proposed) | — | proposed kind: refused until a calculator declares it |
| 43 `room.air-change-rates` | `room.air-change-rates` (proposed) | — | proposed kind: refused until a calculator declares it |
| 44 `ground.u-values` | `ground.u-values` (proposed) | — | proposed kind: refused until a calculator declares it |
| 45 `climate.monthly` | `climate.monthly` | B7 | imports |
| 46 `insulation.bs5422` | `criteria.insulation` | B9 | imports |
| 47 `device.ratings` | `device.ratings` | E4 | imports |
| 48 `surface.resistances` | `surface.resistances` (proposed) | — | proposed kind: refused until a calculator declares it |
| 49 `material.conductivity` | `material.conductivity` | B4, B7 | imports |
| 50 `thermal.bridging` | `thermal.bridging` | B4 | imports |
| 51 `joint.friction` | `joint.friction` | S6 | imports |
| 52 `spring.seating` | `spring.seating` | M7 | imports |
| 53 `spring.endurance` | `spring.endurance` | M7 | imports |
| 54 `accumulator.rules` | `accumulator.rules` | F2 | imports |
| 55 `accumulator.sizes` | `accumulator.sizes` | F2 | imports |
| 56 `glaser.layers` | `material.conductivity` | B4, B7 | imports |
| 57 `surface.resistance` | `surface.resistance` | B7 | imports |
| 58 `climate.humidity-class` | `climate.humidity-class` | B7 | imports |
| 59 `criteria.condensation` | `criteria.condensation` | B7 | imports |
| 60 `pcb.method` | `pcb.method` | E7 | imports |
| 61 `shaft.marin` | `shaft.marin` | M4 | imports |
| 62 `material.properties` | `material.properties` | B8, B9, F1, F3, M7, M8, S7, S8, S9 | imports |
| 63 `material.temperature-tables` | `material.properties` | B8, B9, F1, F3, M7, M8, S7, S8, S9 | imports |
| 64 `material.secant-curve` | `material.properties` | B8, B9, F1, F3, M7, M8, S7, S8, S9 | imports |
| 65 `material.friction-pairs` | `material.properties` | B8, B9, F1, F3, M7, M8, S7, S8, S9 | imports |
| 66 `cylinder.mounting` | `cylinder.mounting` | F1 | imports |
| 67 `cylinder.buckling-safety` | `cylinder.buckling-safety` | F1 | imports |
| 68 `cylinder.hose-sizes` | `cylinder.hose-sizes` | F1 | imports |
| 69 `snapfit.friction` | `snapfit.friction` | M8 | imports |
| 70 `snapfit.annular-factor` | `snapfit.annular-factor` | M8 | imports |
| 71 `snapfit.annular-force-factor` | `snapfit.annular-force-factor` | M8 | imports |
| 72 `snapfit.secant-modulus` | `snapfit.secant-modulus` | M8 | imports |
| 73 `plate.deflection-limit` | `plate.deflection-limit` | S9 | imports |
| 74 `pipe.section` | `pipe.section` | B8 | imports |
| 75 `pipe.screening-k1` | `pipe.screening-k1` | B8 | imports |
| 76 `bellows.data` | `bellows.data` | B8 | imports |
| 77 `surface.coefficient` | `surface.coefficient` | B9 | imports |
| 78 `surface.emissivity` | `surface.emissivity` | B9 | imports |
| 79 `cable.bend-radius` | `cable.bend-radius` | E8 | imports |
| 80 `containment.friction` | `containment.friction` | E8 | imports |
| 81 `cable.pulling-limit` | `cable.pulling-limit` | E8 | imports |
| 82 `containment.weight-correction` | `containment.weight-correction` | E8 | imports |

Record kinds that a visible calculator declares and no template fills: `motor.rating` (E1), `cable.impedance`, `motor.start`, `starter.limits` and `transformer.data` (E9). The three `lifting.*` kinds (S8) are declared by a calculator the lifting flag hides and have no template either.
