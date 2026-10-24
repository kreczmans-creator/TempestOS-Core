# Record templates

These templates let you type reference data into TempestOS records from your own licensed copies.
Every data file holds a header row and units only. No data rows ship here.
Track S, phase C-3, builds the CSV importer from these files.

## Priority order

Type rank 01 first. The ranking follows calculator liability, then dependency.
Electrical safety (E4, E5) comes first. Then come S8 lifting, F2 accumulator, S6 base plate and S7 LTB.
M3 bolted joints has no record template. See Gaps. M4 shaft and its bearings follow, then the rest.
At equal liability, a record that a built calculator already needs ranks above one for a planned calculator.

| Rank | Kind | Source | Calculators | Built or planned | Why this rank |
|---|---|---|---|---|---|
| 01 | `conductor.material` | IEC 60228, IEC 60949, BS 7671 Table 43.1 | E5, E3, E4 | E5 built; E3 built; E4 planned | Smallest record. Three calculators need it. Two of them already run. |
| 02 | `device.characteristic` | Manufacturer datasheet | E5, E4 | E5 built; E4 planned | E5 needs it for every fuse and breaker that is not a standard miniature breaker. |
| 03 | `bs7671.max-zs` | BS 7671 Tables 41.2-41.4 | E5, E4 | E5 built; E4 planned | It can settle the unconfirmed E5 check on a 32 A type B breaker. |
| 04 | `bs7671.ampacity` | BS 7671 Appendix 4, tables with A suffix | E4, E3 | E4 planned; E3 built | E4 sizes the conductor from it. A wrong value can undersize a cable. |
| 05 | `bs7671.voltage-drop` | BS 7671 Appendix 4, tables with B suffix | E3, E4 | E3 built; E4 planned | E3 runs today and types the figure by hand. This record removes that step. |
| 06 | `bs7671.factors-ambient` | BS 7671 Appendix 4, ambient factor tables | E4, E3 | E4 planned; E3 built | A wrong factor can overrate a cable. E4 needs it before it can size. |
| 07 | `bs7671.factors-grouping` | BS 7671 Appendix 4, grouping factor tables | E4 | planned | Grouping is the factor most often missed. E4 cannot size a bunched circuit without it. |
| 08 | `bs7671.factors-insulation` | BS 7671 Appendix 4 and regulation 523.7 | E4 | planned | Insulated walls cut cable capacity sharply. E4 needs the factor for those routes. |
| 09 | `bs7671.factors-protection` | BS 7671 Appendix 4, Cc paragraph | E4 | planned | One number, but E4 must apply it where the old fuse type is present. |
| 10 | `en60204.ampacity` | BS EN 60204-1 Table 6 | E4 | planned | Machine wiring follows a different standard from BS 7671. E4 needs both sets. |
| 11 | `en60204.factors` | BS EN 60204-1 Annex D | E4 | planned | It completes the machine wiring set. E4 cannot derate a panel cable without it. |
| 12 | `steel.strengths` | EN 10025-2, tables for yield and tensile strength | S4, S8, S6, S7 | S4 built; S8, S6, S7 planned | Four structural calculators read it. Strength sets every utilisation. |
| 13 | `ec3.partial-factors` | NA to BS EN 1993-1-1, clause 6.1(1) | S3, S8, S6, S7 | S3 built; S8, S6, S7 planned | Every steel resistance divides by these factors. A wrong factor shifts every result. |
| 14 | `ec3.national-annex` | NA to BS EN 1993-1-8; EN 1993-1-8 Table 3.3 | S4, S8, S6 | S4 built; S8, S6 planned | It governs bolt and weld resistance in three calculators. |
| 15 | `en1990.factors` | NA to BS EN 1990, Tables NA.A1.1, NA.A1.2(A)-(C) | S5, S8, S6, S7 | S5 built; S8, S6, S7 planned | Every load case in the structural chain passes through it. |
| 16 | `fluid.properties` | Fluid supplier data or a property table | B5, F2 | B5 built; F2 planned | F2 sits high in the order and needs fluid data. B5 already runs on it. |
| 17 | `hydraulics.velocity-limits` | Hose or pipe manufacturer's sizing guide | F1, F2, B1 | F1, F2 planned; B1 built | Velocity limits keep hydraulic lines from cavitation and heating. |
| 18 | `en1992.factors` | NA to BS EN 1992-1-1, Table 2.1N and clause 3.1.6 | S6 | planned | The base plate bears on concrete. These factors set the grout and concrete strength. |
| 19 | `concrete.strengths` | BS EN 1992-1-1 Table 3.1 | S6 | planned | The base plate check needs the concrete class. One row per class keeps it simple. |
| 20 | `ec3.ltb-parameters` | NA to BS EN 1993-1-1 cl. 6.3.2.3; EN 1993-1-1 Table 6.3 | S7 | planned | Lateral buckling can govern beam failure. S7 cannot run without these. |
| 21 | `ltb.moment-factors` | NCCI SN003b-EN-EU tables for C1, C2 | S7 | planned | S7 needs the moment shape factors. EN 1993-1-1 does not print them. |
| 22 | `shaft.material` | Material standard for the shaft steel, or certificate | M4 | built | M4 runs today and its strength inputs drive the fatigue result. |
| 23 | `shaft.limits` | Bearing and gear manufacturers' data | M4 | built | Excess slope shortens bearing life. M4 checks it against these limits. |
| 24 | `bearing.ratings` | Bearing manufacturer's catalogue | M5, M4 | M5 built; M4 built | A wrong rating changes bearing life by a cube. M5 and the drive-train chain depend on it. |
| 25 | `bearing.factors` | Bearing manufacturer's catalogue | M5 | built | M5 selects factors by hand today. A record removes a typing step. |
| 26 | `bearing.aiso` | Bearing manufacturer's life tool | M5 | built | It scales bearing life. M5 accepts a value of 1 until you supply this record. |
| 27 | `fluid.expansion` | Glycol supplier data sheet | B6 | built | An undersized vessel can lift the safety valve. B6 runs today. |
| 28 | `emitter.rating` | Emitter manufacturer's catalogue (BS EN 442 rating) | B6 | built | It sets emitter output at part load. The effect is smaller than a vessel size. |
| 29 | `psu.derating` | PSU manufacturer's data sheet | E6 | built | An overheated PSU fails. The risk is lower than the safety sets above. |
| 30 | `battery.factors` | Battery manufacturer's data sheet | E6 | built | A short battery fails the backup duty. E6 runs today. |
| 31 | `oring.gland` | ISO 3601-2 or supplier gland tables | F3 | planned | A wrong groove leaks under pressure. |
| 32 | `oring.sizes` | SAE AS568 or ISO 3601-1 size tables | F3 | planned | F3 needs the size list to pick a standard ring. |
| 33 | `oring.extrusion-limits` | Parker O-Ring Handbook (ORD 5700), extrusion limit chart | F3 | planned | Extrusion destroys a seal under pressure. The chart is read as points. |
| 34 | `asme.b31-3.allowable-stress` | ASME B31.3 Table A-1 | B8 | planned | Pressure piping stress limits. A wrong value passes an overstressed pipe. |
| 35 | `asme.b31-3.stress-range` | ASME B31.3 Table 302.3.5 | B8 | planned | It cuts the allowable range for fatigue. Without it B8 cannot assess cycling lines. |
| 36 | `asme.b31-3.thermal` | ASME B31.3 Appendix C tables | B8 | planned | Pipe expansion length drives the loop size. B8 needs it with the stress limits. |
| 37 | `containment.cable-factors` | IET On-Site Guide, containment capacity tables | E8 | planned | Overfilled containment overheats cables. E8 needs both factor sets. |
| 38 | `containment.conduit-trunking` | IET On-Site Guide, containment capacity tables | E8 | planned | It pairs with the cable factors. E8 cannot size a route without both. |
| 39 | `en10270.spring-wire` | BS EN 10270-1 tensile strength table | M7 | planned | Spring stress limits scale from R_m. A wrong value breaks a spring. |
| 40 | `en13906.permissible-stress` | BS EN 13906-1 permissible stress tables | M7 | planned | It sets the pass limit for the spring check. |
| 41 | `climate.design-temperatures` | CIBSE Guide A or MCS design data | B4 | planned | Heat loss scales with the design temperature difference. Heating plant size follows. |
| 42 | `room.internal-temperatures` | CIBSE Guide A or MCS design data | B4 | planned | It sets the room temperature the heat loss is based on. |
| 43 | `room.air-change-rates` | CIBSE Guide A, MCS data or BS EN 12831-1 NA | B4 | planned | Ventilation loss is a large share of room heat loss. |
| 44 | `ground.u-values` | CIBSE Guide A or BS EN ISO 13370 | B4 | planned | Floor loss is hard to compute by hand. The tables give a safe shortcut. |
| 45 | `climate.monthly` | CIBSE weather data; method in BS EN ISO 13788 | B7 | planned | Condensation risk depends on the month. B7 needs all 12. |
| 46 | `insulation.bs5422` | BS 5422 thickness tables | B9 | planned | Last in the order. A wrong thickness wastes energy but rarely harms people. |

Each kind has `NN-<kind>.csv` (header only) and `NN-<kind>.md` (what to enter).
Kinds marked in the md as existing CalcRecordKind already have symbols in the calculators.
Kinds marked proposed have no CalcRecordKind yet. Track S confirms their symbols when the calculator is built.

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
| `kind` | The kind name, for example `bs7671.ampacity`. It must match a data file. |
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
Name the matching data file `NN-<kind>--<label>.csv`. Track S may replace this pairing with an explicit key.

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
- F2 accumulator (planned) needs accumulator manufacturer limits. No template covers them yet.
- S8 lifting lugs (planned) needs dynamic and lifting factors. No template covers them yet.
- B7 Glaser (planned) needs the ISO 13788 humidity classes. No template covers them yet.
- F2 and the HPU chain need hydraulic fluid density and bulk modulus. `fluid.properties` has no symbols for them.
- S6 base plate may need the joint coefficient from EN 1993-1-8, clause 6.2.5. No template covers it yet.

## Open points for Track S

- Confirm the symbols of every proposed kind when its calculator is built.
- Decide how a data file pairs with its provenance row. This README proposes the title label.
- Confirm the table symbol names in each md, such as `I_t_table`.
- Confirm that the importer converts units, for example rho_20 from ohm*mm to ohm*mm2/m for E3.
- Keep licensed values out of every export, as CalcDataRecord requires.
