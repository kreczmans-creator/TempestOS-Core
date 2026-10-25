# Record templates

These templates let you type reference data into TempestOS records from your own licensed copies.
Every data file holds a header row and units only. No data rows ship here.
Track S, phase C-3, builds the CSV importer from these files.

## Priority order

Type the rows in table order, from the top. The file number is a fixed id. It is not a rank, so the numbers do not run in order.
The order follows calculator liability, then dependency.
Electrical safety (E4, E5) comes first. E4 is now built, so all its kinds sit at the top with E5.
Then come the Eurocode group (S4, S5 and S6 factors), then S7 and the W3 material library that feeds it.
S7 lateral-torsional buckling is now built. Its kinds rank high because a wrong factor can pass an unstable beam.
F2 accumulator is now built. Its pre-charge rules and size list follow S7, ahead of fluid data.
S6 base plate is now built. Its kinds sit with the Eurocode group.
B4 room heat loss is now built. Its record kinds sit with the B4 group near the end.
B7 Glaser is now built. Its kinds sit after B4, because condensation harms buildings more slowly than a failed beam.
M7 springs and E7 PCB trace are now built. Their kinds follow the M4 and M5 groups.
The W3 material library has one scalar template and three table templates. The table templates rank near the end, as the built calculators map scalar properties only.
M3 bolted joints has no record template. See Gaps. M4 shaft and its bearings follow S7 and F2, then the rest.
At equal liability, a record that a built calculator already needs ranks above one for a planned calculator.

| Order | File | Kind | Source | Calculators | Built or planned | Why this rank |
|---|---|---|---|---|---|---|
| 01 | 01 | `conductor.material` | IEC 60228, IEC 60949, BS 7671 Table 43.1 | E5, E3, E7 | E5 built; E3 built; E7 built | Smallest record. Three built calculators need it. |
| 02 | 02 | `device.characteristic` | Manufacturer datasheet | E5 | E5 built | E5 needs it for every fuse and breaker that is not a standard miniature breaker. |
| 03 | 47 | `device.ratings` | Manufacturer datasheet or product standard | E4 | E4 built | E4 cannot pick a device without it. The sheet blocks. |
| 04 | 03 | `bs7671.max-zs` | BS 7671 Tables 41.2-41.4 | E5 | E5 built | It can settle the unconfirmed E5 check on a 32 A type B breaker. |
| 05 | 04 | `cable.ampacity` | Your licensed ampacity table (BS 7671 Appendix 4, tables with A suffix) | E4, E3 | E4 built; E3 built | E4 sizes the conductor from it. A wrong value can undersize a cable. |
| 06 | 05 | `bs7671.voltage-drop` | BS 7671 Appendix 4, tables with B suffix | E3 | E3 built | E3 runs today and types the figure by hand. This record removes that step. |
| 07 | 06 | `cable.correction-factors` (C_a) | BS 7671 Appendix 4, ambient factor tables | E4 | E4 built | A wrong factor can overrate a cable. E4 multiplies the capacity by it. |
| 08 | 07 | `cable.correction-factors` (C_g) | BS 7671 Appendix 4, grouping factor tables | E4 | E4 built | Grouping is the factor most often missed. E4 only warns when C_g is 1 for a group. |
| 09 | 08 | `cable.correction-factors` (C_i) | BS 7671 Appendix 4 and regulation 523.7 | E4 | E4 built | Insulated walls cut cable capacity sharply. E4 needs the factor for those routes. |
| 10 | 09 | none (superseded) | BS 7671 Appendix 4, Cc paragraph | E4 | E4 built; no record read | E4 derives C_c in code. Do not fill this file. |
| 11 | 10 | `cable.ampacity` | BS EN 60204-1 Table 6 | E4 | E4 built | Machine wiring follows a different standard from BS 7671. It fills the same E4 table. |
| 12 | 11 | `cable.correction-factors` (C_a, C_g) | BS EN 60204-1 Annex D | E4 | E4 built | It completes the machine wiring set. E4 applies both factors as a product. |
| 13 | 12 | `steel.strengths` | EN 10025-2, tables for yield and tensile strength | S4, S6, S8 | S4 built; S6 built; S8 planned; S7 built via W3 | Three structural calculators read it. S7 takes f_y from a W3 material instead. Strength sets every utilisation. |
| 14 | 13 | `ec3-1-1.national-annex` | NA to BS EN 1993-1-1, clause 6.1(1) | S6, S8 | S6 built; S8 planned | S6 divides the plate resistance by gamma_M0. A wrong factor shifts every result. |
| 15 | 14 | `ec3.national-annex` | NA to BS EN 1993-1-8; EN 1993-1-8 Table 3.3; NA to BS EN 1993-1-1 for gamma_M1 | S4, S6, S7, S8 | S4 built; S6 built; S7 built; S8 planned | It governs bolt and weld resistance in three calculators. S6 reads gamma_M2. S7 reads gamma_M1. |
| 16 | 15 | `en1990.factors` | NA to BS EN 1990, Tables NA.A1.1, NA.A1.2(A)-(C) | S5, S8, S6 | S5 built; S8, S6 planned | Every load case in the structural chain passes through it. |
| 17 | 18 | `ec2.national-annex` | NA to BS EN 1992-1-1, Table 2.1N and clause 3.1.6 | S6 | S6 built | The base plate bears on concrete. These factors set the concrete design strength. |
| 18 | 19 | `concrete.strengths` | BS EN 1992-1-1 Table 3.1 | S6 | S6 built; no record kind | S6 types f_ck by hand. The template waits for a kind. |
| 19 | 51 | `joint.friction` | EN 1993-1-8 cl. 6.2.2 or the grout manufacturer | S6 | S6 built | It sets the shear carried by friction at the base plate. |
| 20 | 20 | `ec3.ltb-factors` | NA to BS EN 1993-1-1 cl. 6.3.2.3; EN 1993-1-1 Table 6.1 | S7 | S7 built | Lateral buckling can govern beam failure. S7 applies these factors to every LTB result. |
| 21 | 21 | `ec3.mcr-factors` | NCCI SN003, a published table or an LTB program | S7 | S7 built | C_1 sets the elastic critical moment. A wrong value shifts the LTB utilisation. S7 needs it for transverse loads. |
| 22 | 62 | `material.properties` | Material standard, supplier data sheet or test certificate | S7, M7 | S7 built; M7 built | The W3 material library feeds the stiffness and strength of two built calculators. It can replace 39 for the curve and 12 for S7. |
| 23 | 54 | `accumulator.rules` | Accumulator manufacturer's selection guide | F2 | F2 built | Wrong pre-charge limits can overstress the bladder or lose the stored volume. F2 runs today. |
| 24 | 55 | `accumulator.sizes` | Accumulator manufacturer's catalogue | F2 | F2 built | F2 picks the size from this list. A wrong size under-delivers the duty. |
| 25 | 16 | `fluid.properties` | Fluid supplier data or a property table | B5 | B5 built | B5 already runs on it. The HPU chain will need hydraulic fluid data later. |
| 26 | 17 | `hydraulics.velocity-limits` | Hose or pipe manufacturer's sizing guide | F1, B1 | F1 planned; B1 built (no kind); F2 built (no kind) | Velocity limits keep hydraulic lines from cavitation and heating. No built calculator lists a kind yet. |
| 27 | 22 | `shaft.material` | Material standard for the shaft steel, or certificate | M4 | built | M4 runs today and its strength inputs drive the fatigue result. |
| 28 | 61 | `shaft.marin` | Fatigue reference in use (edition and table) | M4 | M4 built | The surface factor scales the endurance limit. M4 computes it when you leave it blank. |
| 29 | 23 | `shaft.limits` | Bearing and gear manufacturers' data | M4 | built | Excess slope shortens bearing life. M4 checks it against these limits. |
| 30 | 24 | `bearing.ratings` | Bearing manufacturer's catalogue | M5, M4 | M5 built; M4 built | A wrong rating changes bearing life by a cube. M5 and the drive-train chain depend on it. |
| 31 | 25 | `bearing.factors` | Bearing manufacturer's catalogue | M5 | built | M5 selects factors by hand today. A record removes a typing step. |
| 32 | 26 | `bearing.aiso` | Bearing manufacturer's life tool | M5 | built | It scales bearing life. M5 accepts a value of 1 until you supply this record. |
| 33 | 39 | `spring.wire-strength` | BS EN 10270 table or wire certificate; or a W3 material curve (see 62) | M7 | M7 built | Spring stress limits scale from R_m. A wrong value breaks a spring. M7 has no default. |
| 34 | 40 | `spring.permissible` | Design reference or BS EN 13906-1 permissible stress clauses | M7 | M7 built | It sets the pass limit for the spring check as fractions of R_m. |
| 35 | 53 | `spring.endurance` | Wire maker's data or your fatigue reference | M7 | M7 built | Without S_e the spring fatigue result stays blocked. |
| 36 | 52 | `spring.seating` | Design reference or spring standard | M7 | M7 built | The end coefficient sets the buckling limit of a compression spring. |
| 37 | 27 | `fluid.expansion` | Glycol supplier data sheet | B6 | built | An undersized vessel can lift the safety valve. B6 runs today. |
| 38 | 28 | `emitter.rating` | Emitter manufacturer's catalogue (BS EN 442 rating) | B6 | built | It sets emitter output at part load. The effect is smaller than a vessel size. |
| 39 | 29 | `psu.derating` | PSU manufacturer's data sheet | E6 | built | An overheated PSU fails. The risk is lower than the safety sets above. |
| 40 | 30 | `battery.factors` | Battery manufacturer's data sheet | E6 | built | A short battery fails the backup duty. E6 runs today. |
| 41 | 60 | `pcb.method` | IPC standard that publishes the curve fit | E7 | E7 built | An overheated trace can fail or burn. E7 ships legacy defaults that a record replaces. |
| 42 | 31 | `oring.gland` | ISO 3601-2 or supplier gland tables | F3 | planned | A wrong groove leaks under pressure. |
| 43 | 32 | `oring.sizes` | SAE AS568 or ISO 3601-1 size tables | F3 | planned | F3 needs the size list to pick a standard ring. |
| 44 | 33 | `oring.extrusion-limits` | Parker O-Ring Handbook (ORD 5700), extrusion limit chart | F3 | planned | Extrusion destroys a seal under pressure. The chart is read as points. |
| 45 | 34 | `asme.b31-3.allowable-stress` | ASME B31.3 Table A-1 | B8 | planned | Pressure piping stress limits. A wrong value passes an overstressed pipe. |
| 46 | 35 | `asme.b31-3.stress-range` | ASME B31.3 Table 302.3.5 | B8 | planned | It cuts the allowable range for fatigue. Without it B8 cannot assess cycling lines. |
| 47 | 36 | `asme.b31-3.thermal` | ASME B31.3 Appendix C tables | B8 | planned | Pipe expansion length drives the loop size. B8 needs it with the stress limits. |
| 48 | 37 | `containment.cable-factors` | IET On-Site Guide, containment capacity tables | E8 | planned | Overfilled containment overheats cables. E8 needs both factor sets. |
| 49 | 38 | `containment.conduit-trunking` | IET On-Site Guide, containment capacity tables | E8 | planned | It pairs with the cable factors. E8 cannot size a route without both. |
| 50 | 41 | `climate.design-temperature` | CIBSE Guide A or MCS design data | B4 | B4 built | Heat loss scales with the design temperature difference. Heating plant size follows. |
| 51 | 42 | `room.internal-temperatures` | CIBSE Guide A or MCS design data | B4 | B4 built; no record kind | B4 types the room temperature by hand. The template waits for a kind. |
| 52 | 43 | `room.air-change-rates` | CIBSE Guide A, MCS data or BS EN 12831-1 NA | B4 | B4 built; no record kind | B4 types the air change rate by hand. The template waits for a kind. |
| 53 | 44 | `ground.u-values` | CIBSE Guide A or BS EN ISO 13370 | B4 | B4 built; no record kind | B4 takes ground floor U as an override. The template waits for a kind. |
| 54 | 48 | `surface.resistances` | BS EN ISO 6946 or your own source | B4 | B4 built; no record kind | B4 no longer lists a kind for it. It falls back to conventional values when blank. A wrong value shifts every U-value. |
| 55 | 49 | `material.conductivity` | Manufacturer declared value or your licensed reference | B4 | B4 built | Conductivity sets each layer resistance, so it sets the U-value. |
| 56 | 50 | `thermal.bridging` | Your own source or a bridging calculation | B4 | B4 built | It adds heat loss on the area facing outside. Zero is the default. |
| 57 | 45 | `climate.monthly` | CIBSE weather data; method in BS EN ISO 13788 | B7 | B7 built | Condensation risk depends on the month. B7 needs all 12 rows and ships no climate data. |
| 58 | 56 | `material.conductivity` (B7 layers) | Manufacturer's declared value, your licensed reference and material standard | B7 | B7 built | B7 reads thickness, conductivity and vapour resistance of every layer. Condensation risk follows. |
| 59 | 57 | `surface.resistance` | BS EN ISO 6946 and BS EN ISO 13788 | B7 | B7 built | Surface resistances set the inside surface temperature. B7 warns when Rsi_f is low. |
| 60 | 58 | `climate.humidity-class` | BS EN ISO 13788 humidity class method | B7 | B7 built | It sets the inside vapour load in moisture-excess mode. |
| 61 | 59 | `criteria.condensation` | Your own source for the materials used | B7 | B7 built | It sets the pass or fail limits on surface humidity and condensate. |
| 62 | 63 | `material.properties` (T tables) | Material standard, supplier data sheet or test certificate | S7, M7 | Library built | Properties against temperature. The library reads them when you ask for a temperature. |
| 63 | 64 | `material.properties` (Es curve) | Material standard, supplier data sheet or test certificate | none | Library built; no calculator reads it | Stored for a later nonlinear calculator. |
| 64 | 65 | `material.properties` (friction pairs) | Material standard, supplier data sheet or test report | none | Library built; no calculator reads it | Stored for a later calculator. S6 still uses template 51. |
| 65 | 46 | `insulation.bs5422` | BS 5422 thickness tables | B9 | planned | Last in the order. A wrong thickness wastes energy but rarely harms people. |

Each kind has `NN-<kind>.csv` (header only) and `NN-<kind>.md` (what to enter).
The file name keeps the name planned when the template was written. The Kind column shows the name in the code.
Where the two differ, the md says so and the Kind name map below lists both.
Kinds marked "existing CalcRecordKind" in the md have symbols in a built calculator.
Kinds marked "covered by the W3 material library" take their values from a material record. The material file is template 62.
At release 90d15f11, a material or a renamed kind covers templates 12 (S7 only), 20, 21, 39 and 40.
The md of each lists the old planned name and the kind in the code.
Kinds marked proposed have no CalcRecordKind in a built calculator. Track S confirms their symbols when a calculator needs them.

## Kind name map

The CalcRecordKind column is the exact string in the calculator's `RecordKinds` list.
Built means a built calculator lists the kind. Planned means the name is only a proposal.
Several files can fill one kind. Each file then makes its own records, told apart by title label.

| Template file | CalcRecordKind | Calculator | Built or planned |
|---|---|---|---|
| `01-conductor.material` | `conductor.material` | E5, E3 | built |
| `02-device.characteristic` | `device.characteristic` | E5 | built |
| `03-bs7671.max-zs` | `bs7671.max-zs` (proposed, no kind yet) | E5 (built, no record kind) | planned (no kind in the built calculator) |
| `04-bs7671.ampacity` | `cable.ampacity` | E4 | built. Planned name was `bs7671.ampacity`. |
| `05-bs7671.voltage-drop` | `bs7671.voltage-drop` (proposed, no kind yet) | E3 (built, no record kind) | planned (no kind in the built calculator) |
| `06-bs7671.factors-ambient` | `cable.correction-factors` (C_a) | E4 | built. Planned name was `bs7671.factors-ambient`. |
| `07-bs7671.factors-grouping` | `cable.correction-factors` (C_g) | E4 | built. Planned name was `bs7671.factors-grouping`. |
| `08-bs7671.factors-insulation` | `cable.correction-factors` (C_i) | E4 | built. Planned name was `bs7671.factors-insulation`. |
| `09-bs7671.factors-protection` | none (superseded) | E4 (built) | superseded. E4 derives C_c in code. |
| `10-en60204.ampacity` | `cable.ampacity` | E4 | built. Planned name was `en60204.ampacity`. |
| `11-en60204.factors` | `cable.correction-factors` (C_a, C_g) | E4 | built. Planned name was `en60204.factors`. |
| `12-steel.strengths` | `steel.strengths` | S4, S6 | built. S7 is covered by the W3 material library: f_y, E and G come from a material (template 62). |
| `13-ec3.partial-factors` | `ec3-1-1.national-annex` | S6 | built. Planned name was `ec3.partial-factors`. |
| `14-ec3.national-annex` | `ec3.national-annex` | S4, S6, S7 | built. S7 reads gamma_M1 from this kind. |
| `15-en1990.factors` | `en1990.factors` | S5 | built |
| `16-fluid.properties` | `fluid.properties` | B5 | built |
| `17-hydraulics.velocity-limits` | `hydraulics.velocity-limits` (proposed, no kind yet) | F1, F2, B1 | planned |
| `18-en1992.factors` | `ec2.national-annex` | S6 | built. Planned name was `en1992.factors`. |
| `19-concrete.strengths` | `concrete.strengths` (proposed, no kind yet) | S6 (built, no record kind) | planned (no kind in the built calculator) |
| `20-ec3.ltb-parameters` | `ec3.ltb-factors` | S7 | built. Planned name was `ec3.ltb-parameters`. |
| `21-ltb.moment-factors` | `ec3.mcr-factors` | S7 | built. Planned name was `ltb.moment-factors`. |
| `22-shaft.material` | `shaft.material` | M4 | built |
| `23-shaft.limits` | `shaft.limits` | M4 | built |
| `24-bearing.ratings` | `bearing.ratings` | M5, M4 | built |
| `25-bearing.factors` | `bearing.factors` | M5 | built |
| `26-bearing.aiso` | `bearing.aiso` | M5 | built |
| `27-fluid.expansion` | `fluid.expansion` | B6 | built |
| `28-emitter.rating` | `emitter.rating` | B6 | built |
| `29-psu.derating` | `psu.derating` | E6 | built |
| `30-battery.factors` | `battery.factors` | E6 | built |
| `31-oring.gland` | `oring.gland` (proposed, no kind yet) | F3 | planned |
| `32-oring.sizes` | `oring.sizes` (proposed, no kind yet) | F3 | planned |
| `33-oring.extrusion-limits` | `oring.extrusion-limits` (proposed, no kind yet) | F3 | planned |
| `34-asme.b31-3.allowable-stress` | `asme.b31-3.allowable-stress` (proposed, no kind yet) | B8 | planned |
| `35-asme.b31-3.stress-range` | `asme.b31-3.stress-range` (proposed, no kind yet) | B8 | planned |
| `36-asme.b31-3.thermal` | `asme.b31-3.thermal` (proposed, no kind yet) | B8 | planned |
| `37-containment.cable-factors` | `containment.cable-factors` (proposed, no kind yet) | E8 | planned |
| `38-containment.conduit-trunking` | `containment.conduit-trunking` (proposed, no kind yet) | E8 | planned |
| `39-en10270.spring-wire` | `spring.wire-strength` | M7 | built. Planned name was `en10270.spring-wire`. Also covered by the W3 material library: `Rm_A` and `Rm_m` on a material give R_m(d) (template 62). |
| `40-en13906.permissible-stress` | `spring.permissible` | M7 | built. Planned name was `en13906.permissible-stress`. |
| `41-climate.design-temperatures` | `climate.design-temperature` | B4 | built. Planned name was `climate.design-temperatures`. |
| `42-room.internal-temperatures` | `room.internal-temperatures` (proposed, no kind yet) | B4 (built, no record kind) | planned (no kind in the built calculator) |
| `43-room.air-change-rates` | `room.air-change-rates` (proposed, no kind yet) | B4 (built, no record kind) | planned (no kind in the built calculator) |
| `44-ground.u-values` | `ground.u-values` (proposed, no kind yet) | B4 (built, no record kind) | planned (no kind in the built calculator) |
| `45-climate.monthly` | `climate.monthly` | B7 | built |
| `46-insulation.bs5422` | `insulation.bs5422` (proposed, no kind yet) | B9 | planned |
| `47-device.ratings` | `device.ratings` | E4 | built |
| `48-surface.resistances` | `surface.resistances` (proposed, no kind in release 90d15f11) | B4 (built, no record kind) | planned (B4 no longer lists the kind) |
| `49-material.conductivity` | `material.conductivity` | B4 | built |
| `50-thermal.bridging` | `thermal.bridging` | B4 | built |
| `51-joint.friction` | `joint.friction` | S6 | built |
| `52-spring.seating` | `spring.seating` | M7 | built |
| `53-spring.endurance` | `spring.endurance` | M7 | built |
| `54-accumulator.rules` | `accumulator.rules` | F2 | built |
| `55-accumulator.sizes` | `accumulator.sizes` | F2 | built |
| `56-glaser.layers` | `material.conductivity` (B7 `layers` table) | B7 | built. Shares the kind with template 49. |
| `57-surface.resistance` | `surface.resistance` | B7 | built |
| `58-climate.humidity-class` | `climate.humidity-class` | B7 | built |
| `59-criteria.condensation` | `criteria.condensation` | B7 | built |
| `60-pcb.method` | `pcb.method` | E7 | built |
| `61-shaft.marin` | `shaft.marin` | M4 | built |
| `62-material.properties` | `material.properties` (scalar properties) | S7, M7 (W3 library) | built |
| `63-material.temperature-tables` | `material.properties` (`E(T)` and the other temperature tables) | W3 library | built |
| `64-material.secant-curve` | `material.properties` (`Es_curve`) | W3 library | built. No calculator reads it yet. |
| `65-material.friction-pairs` | `material.properties` (`mu_pairs`) | W3 library | built. No calculator reads it yet. |

## CSV conventions

- Use UTF-8, a comma separator and one header row. Quote a cell that holds a comma.
- Write each header as `symbol[unit]`. Use `[-]` for dimensionless columns and `[text]` for text columns.
- Write units in plain ASCII. A digit after a unit symbol is a power, so `mm2` means mm^2.
- Write `A2*s` for A^2*s and `J/(K*mm3)` for J/(K*mm^3). Use `*` for a product and `/` for a ratio.
- Keep the exact symbol names the calculators use, such as `I_t`, `C_a` and `k_temp`.
- One row per table row. When a table repeats one quantity under several conditions, write one row per cell.
- Name the condition, such as the installation method, in a text or dimensionless column of that row.
- A scalar factor set is one row with one column per symbol.
- A file with scalar columns and table columns takes the scalars on the first data row only.
- For a table kind, the header holds the column symbols of the calculator's table input and nothing else. One file is one table.
- For a scalar kind that offers a choice of rows, the importer saves one record per row. Extra identifying columns are stored and never applied.
- The importer writes a unit such as `mm2` as mm^2 and `m2*K/W` as m^2*K/W. Track S confirms this.
- Leave a cell blank when the source gives no value. Never type a placeholder such as n/a.
- Type numbers in plain decimal or e-notation. Write a negative number with a minus sign.
- A text cell must not start with `=`, `+`, `-` or `@`. The shared CSV reader refuses it.
- Limits: 500 rows per table, 200 values per record, 200 characters per provenance field.
- Importer checks in each md are sanity checks. They are not values from any standard.
- Plausibility ranges come from the calculators' input limits or from engineering judgement. Track S may tune them.

## Provenance file

`_provenance.csv` has one header row and no data rows. One row describes one record.
Add one row for each data file you fill. Each field maps to a CalcDataRecord field.

| Field | Meaning |
|---|---|
| `kind` | The CalcRecordKind string from the Kind name map, for example `cable.ampacity`. The title label ties the row to its data file. |
| `title` | A short name for the record. It appears on calculation sheets. |
| `source_title` | The title of the publication you typed from. |
| `edition` | The edition or year printed on your copy. |
| `amendment` | The amendment, corrigendum or addendum number. Leave blank if none. |
| `table_or_clause` | The table, figure or clause you copied from. |
| `licence_holder` | The person or company that holds the licence for your copy. |
| `entered_by` | The person who typed the data. |
| `licensed_source` | `true` or `false`. The default is `true`. Licensed values never leave through an export. |
| `origin` | `UserEntered`, `Derived` or `ManufacturerExample`. |
| `notes` | Anything the next reader needs, such as the reference temperature. |

Origin `UserEntered` means you typed it from your own copy.
Origin `Derived` means you calculated it from a formula. Origin `ManufacturerExample` means one attributed manufacturer example.
The importer joins `edition` and `amendment` into the record's Edition field.
Several records of one kind need a label. End the title with the label in brackets, such as `Title [4d2a]`.
Name the matching data file `NN-<kind>--<label>.csv`, where `<kind>` is the file name part, not the CalcRecordKind. Track S may replace this pairing with an explicit key.

## How to fill

1. Open the md for the rank you are typing. Read the Copy from section.
2. Open your licensed copy at the table or clause the md names. Confirm the table number.
3. Copy the data file if you need more than one table. Name the copy as the Provenance file section says.
4. Type the values into the CSV below its header. Do not change the header.
5. Check each column against its unit. Convert your source unit by hand if it differs.
6. Add one row to `_provenance.csv` for the file. Fill every field except amendment and notes when they do not apply.
7. Type from your own copy only. Leave licensed_source as `true` unless the owner of the source allows redistribution.
8. Re-read ten percent of the rows against the source. Fix any slip before you import.
9. When the importer exists, run it. Read each warning and fix the file, or add a note that explains the value.

## Gaps

- M3 bolted joints (built) has no CalcRecordKind. Its bolt strengths, endurance limits and friction values need a template.
- S8 lifting lugs (planned) needs dynamic and lifting factors. No template covers them yet.
- The HPU chain (planned) needs hydraulic fluid density and bulk modulus. `fluid.properties` has no symbols for them.
- S6 base plate types f_ck by hand. Template 19 waits for a record kind. The joint coefficient from clause 6.2.5 has no template.
- B4 types the internal temperature, air change rate and ground floor U by hand. Templates 42 to 44 wait for record kinds.
- S7 LTB types `C_m_y`, `C_m_z` and `C_m_LT` by hand. No record kind lists them, so no template holds them.
- S8 lifting lugs (planned) may need further factors. Template 14 now holds gamma_M1 for S7 only.
- B4 lists no kind for surface resistances at release 90d15f11. Template 48 waits for a kind.
- M4 `shaft.material` has no W3 mapping. Its strength inputs still come from template 22.

## Open points for Track S

- Confirm the symbols of every proposed kind when its calculator is built.
- Confirm how the importer reads the boolean `timber` column of template 56. This README proposes 1 and 0.
- Decide how the CSV importer for the W3 library reads template 63, where `v` takes its unit from `v_unit`. The library imports JSON today.
- Decide how B4 and B7 share the kind `material.conductivity`. Their `layers` tables have different columns (templates 49 and 56).
- Decide whether to rename the B7 kind `surface.resistance` and the B4 kind `surface.resistances` to one name.
- Applying a table record replaces the whole table input. Templates 48 and 49 hold only some columns of the B4 `elements` and `layers` tables. Decide how to merge them by row.
- Decide how the importer handles `cable.correction-factors`, which three files (06, 07, 08) and file 11 fill with different symbols.
- Decide how a data file pairs with its provenance row. This README proposes the title label.
- Confirm the table symbol names in each md, such as `I_t_table`.
- Confirm that the importer converts units, for example rho_20 from ohm*mm to ohm*mm2/m for E3.
- Keep licensed values out of every export, as CalcDataRecord requires.
