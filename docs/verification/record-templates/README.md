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
F1 cylinder circuit, F3 O-ring gland, M8 snap-fits, B8 pipe expansion, S9 plate bending, E8 containment and B9 insulation are now built.
Their kinds sit with the related group: F1 and F3 near the hydraulic data, M8 after the springs, B8, E8 and B9 at the end.
Templates 66 to 82 are new. They cover the kinds of those calculators that no earlier template held.
Templates 35, 36 and 37 are superseded or covered by a material, and carry no kind.
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
| 26 | 66 | `cylinder.mounting` | Design reference or cylinder maker | F1 | F1 built | A wrong K makes the rod buckling check unsafe. F1 defaults to the pinned case. |
| 27 | 67 | `cylinder.buckling-safety` | Design reference or cylinder maker | F1 | F1 built | The factor scales the allowed rod load. F1 default is a placeholder. |
| 28 | 17 | `cylinder.line-limits` | Hose or pipe manufacturer's sizing guide, or your design guide | F1 | F1 built | Velocity limits keep hydraulic lines from cavitation and heating. F1 ships placeholder defaults. |
| 29 | 68 | `cylinder.hose-sizes` | Hose or pipe supplier's catalogue | F1 | F1 built | F1 picks the smallest bore from the list. Optional. |
| 30 | 22 | `shaft.material` | Material standard for the shaft steel, or certificate | M4 | built | M4 runs today and its strength inputs drive the fatigue result. |
| 31 | 61 | `shaft.marin` | Fatigue reference in use (edition and table) | M4 | M4 built | The surface factor scales the endurance limit. M4 computes it when you leave it blank. |
| 32 | 23 | `shaft.limits` | Bearing and gear manufacturers' data | M4 | built | Excess slope shortens bearing life. M4 checks it against these limits. |
| 33 | 24 | `bearing.ratings` | Bearing manufacturer's catalogue | M5, M4 | M5 built; M4 built | A wrong rating changes bearing life by a cube. M5 and the drive-train chain depend on it. |
| 34 | 25 | `bearing.factors` | Bearing manufacturer's catalogue | M5 | built | M5 selects factors by hand today. A record removes a typing step. |
| 35 | 26 | `bearing.aiso` | Bearing manufacturer's life tool | M5 | built | It scales bearing life. M5 accepts a value of 1 until you supply this record. |
| 36 | 39 | `spring.wire-strength` | BS EN 10270 table or wire certificate; or a W3 material curve (see 62) | M7 | M7 built | Spring stress limits scale from R_m. A wrong value breaks a spring. M7 has no default. |
| 37 | 40 | `spring.permissible` | Design reference or BS EN 13906-1 permissible stress clauses | M7 | M7 built | It sets the pass limit for the spring check as fractions of R_m. |
| 38 | 53 | `spring.endurance` | Wire maker's data or your fatigue reference | M7 | M7 built | Without S_e the spring fatigue result stays blocked. |
| 39 | 52 | `spring.seating` | Design reference or spring standard | M7 | M7 built | The end coefficient sets the buckling limit of a compression spring. |
| 40 | 69 | `snapfit.friction` | Your reference, plastic maker or test | M8 | M8 built | Friction sets the assembly and separation forces. |
| 41 | 70 | `snapfit.annular-factor` | Your design reference | M8 | M8 built | It scales the hoop strain of an annular snap. A low value hides a broken ring. |
| 42 | 71 | `snapfit.annular-force-factor` | Your design reference | M8 | M8 built | It scales the annular snap force. Blank means 1. |
| 43 | 72 | `snapfit.secant-modulus` | Plastic maker's stress-strain curve | M8 | M8 built | Es sets every snap-fit force. A W3 curve (template 64) can replace it. |
| 44 | 73 | `plate.deflection-limit` | Specification or product standard | S9 | S9 built | It sets the deflection pass limit. S9 default is span/250. |
| 45 | 27 | `fluid.expansion` | Glycol supplier data sheet | B6 | built | An undersized vessel can lift the safety valve. B6 runs today. |
| 46 | 28 | `emitter.rating` | Emitter manufacturer's catalogue (BS EN 442 rating) | B6 | built | It sets emitter output at part load. The effect is smaller than a vessel size. |
| 47 | 29 | `psu.derating` | PSU manufacturer's data sheet | E6 | built | An overheated PSU fails. The risk is lower than the safety sets above. |
| 48 | 30 | `battery.factors` | Battery manufacturer's data sheet | E6 | built | A short battery fails the backup duty. E6 runs today. |
| 49 | 60 | `pcb.method` | IPC standard that publishes the curve fit | E7 | E7 built | An overheated trace can fail or burn. E7 ships legacy defaults that a record replaces. |
| 50 | 31 | `oring.criteria` | O-ring handbook or seal maker's gland guide | F3 | F3 built | Wrong squeeze, fill or stretch bands pass a leaking groove. F3 ships defaults. |
| 51 | 32 | `oring.ring` | SAE AS568, ISO 3601-1 or supplier catalogue | F3 | F3 built | F3 takes the ring size and tolerances from it. No size table ships. |
| 52 | 33 | `oring.extrusion` | Seal maker's extrusion chart, for example Parker ORD 5700 | F3 | F3 built | Extrusion destroys a seal under pressure. The chart is read as points. |
| 53 | 34 | `pipe.allowable-range` | Your piping code formula for Sa (ASME B31.3 Table A-1 and Table 302.3.5 as inputs) | B8 | B8 built | A wrong Sa passes an overstressed pipe. B8 has no default. |
| 54 | 35 | none (superseded) | ASME B31.3 Table 302.3.5 | B8 | B8 built; no record read | Sa already holds the factor f. Do not fill this file. |
| 55 | 36 | none (covered by the W3 material library) | ASME B31.3 Appendix C tables | B8 | B8 built; no record read | B8 takes E and alpha from a material (templates 62 and 63). Do not fill this file. |
| 56 | 74 | `pipe.section` | Pipe datasheet or pipe standard | B8 | B8 built | OD and wall set the section and the stress. No pipe table ships. |
| 57 | 75 | `pipe.screening-k1` | Your piping code | B8 | B8 built | Without K1 the screening check is not judged and the sheet does not pass. |
| 58 | 76 | `bellows.data` | Bellows manufacturer's datasheet | B8 | B8 built | The area and spring rate set the anchor force. Optional. |
| 59 | 37 | none (superseded) | IET On-Site Guide, containment capacity tables | E8 | E8 built; no record read | E8 sizes fill from cable areas and a space factor. Do not fill this file. |
| 60 | 38 | `containment.space-factor` | Your installation standard or company rule | E8 | E8 built | Overfilled containment overheats cables. E8 has no default and blocks until you enter f_max. |
| 61 | 79 | `cable.bend-radius` | Cable maker's data sheet | E8 | E8 built | A tight bend damages a cable. |
| 62 | 80 | `containment.friction` | Cable or lubricant maker, or pulling records | E8 | E8 built | Friction drives the pull tension through the capstan equation. |
| 63 | 81 | `cable.pulling-limit` | Cable maker's installation data | E8 | E8 built | Overpulling damages a cable. E8 ships no value. |
| 64 | 82 | `containment.weight-correction` | Your reference | E8 | E8 built | It scales the weight on the containment. Use 1 for full weight. |
| 65 | 41 | `climate.design-temperature` | CIBSE Guide A or MCS design data | B4 | B4 built | Heat loss scales with the design temperature difference. Heating plant size follows. |
| 66 | 42 | `room.internal-temperatures` | CIBSE Guide A or MCS design data | B4 | B4 built; no record kind | B4 types the room temperature by hand. The template waits for a kind. |
| 67 | 43 | `room.air-change-rates` | CIBSE Guide A, MCS data or BS EN 12831-1 NA | B4 | B4 built; no record kind | B4 types the air change rate by hand. The template waits for a kind. |
| 68 | 44 | `ground.u-values` | CIBSE Guide A or BS EN ISO 13370 | B4 | B4 built; no record kind | B4 takes ground floor U as an override. The template waits for a kind. |
| 69 | 48 | `surface.resistances` | BS EN ISO 6946 or your own source | B4 | B4 built; no record kind | B4 no longer lists a kind for it. It falls back to conventional values when blank. A wrong value shifts every U-value. |
| 70 | 49 | `material.conductivity` | Manufacturer declared value or your licensed reference | B4 | B4 built | Conductivity sets each layer resistance, so it sets the U-value. |
| 71 | 50 | `thermal.bridging` | Your own source or a bridging calculation | B4 | B4 built | It adds heat loss on the area facing outside. Zero is the default. |
| 72 | 45 | `climate.monthly` | CIBSE weather data; method in BS EN ISO 13788 | B7 | B7 built | Condensation risk depends on the month. B7 needs all 12 rows and ships no climate data. |
| 73 | 56 | `material.conductivity` (B7 layers) | Manufacturer's declared value, your licensed reference and material standard | B7 | B7 built | B7 reads thickness, conductivity and vapour resistance of every layer. Condensation risk follows. |
| 74 | 57 | `surface.resistance` | BS EN ISO 6946 and BS EN ISO 13788 | B7 | B7 built | Surface resistances set the inside surface temperature. B7 warns when Rsi_f is low. |
| 75 | 58 | `climate.humidity-class` | BS EN ISO 13788 humidity class method | B7 | B7 built | It sets the inside vapour load in moisture-excess mode. |
| 76 | 59 | `criteria.condensation` | Your own source for the materials used | B7 | B7 built | It sets the pass or fail limits on surface humidity and condensate. |
| 77 | 63 | `material.properties` (T tables) | Material standard, supplier data sheet or test certificate | S7, M7 | Library built | Properties against temperature. The library reads them when you ask for a temperature. |
| 78 | 64 | `material.properties` (Es curve) | Material standard, supplier data sheet or test certificate | none | Library built; no calculator reads it | Stored for a later nonlinear calculator. |
| 79 | 65 | `material.properties` (friction pairs) | Material standard, supplier data sheet or test report | none | Library built; no calculator reads it | Stored for a later calculator. S6 still uses template 51. |
| 80 | 77 | `surface.coefficient` | Standard or maker's guidance | B9 | B9 built | h_se sets the surface temperature and the heat loss. |
| 81 | 78 | `surface.emissivity` | Jacketing maker or your reference | B9 | B9 built | Emissivity sets the radiation part of h_se. |
| 82 | 46 | `criteria.insulation` | Project specification, BS 5422 or ISO 12241 | B9 | B9 built | Last in the order. A wrong limit wastes energy or leaves a hot surface. |

Each kind has `NN-<kind>.csv` (header only) and `NN-<kind>.md` (what to enter).
The file name keeps the name planned when the template was written. The Kind column shows the name in the code.
Where the two differ, the md says so and the Kind name map below lists both.
Kinds marked "existing CalcRecordKind" in the md have symbols in a built calculator.
Kinds marked "covered by the W3 material library" take their values from a material record. The material file is template 62.
At release 90d15f11, a material or a renamed kind covers templates 12 (S7 only), 20, 21, 39 and 40.
At release 2f53791c, renamed kinds cover templates 17, 31 to 34, 38 and 46. A material covers 36. Templates 35 and 37 are superseded.
The md of each lists the old planned name and the kind in the code.
Kinds marked proposed have no CalcRecordKind in a built calculator. At release 2f53791c, none of templates 17, 31 to 38 and 46 is still proposed. Track S confirms their symbols when a calculator needs them.

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
| `17-hydraulics.velocity-limits` | `cylinder.line-limits` | F1 | built. Planned name was `hydraulics.velocity-limits`. |
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
| `31-oring.gland` | `oring.criteria` | F3 | built. Planned name was `oring.gland`. |
| `32-oring.sizes` | `oring.ring` | F3 | built. Planned name was `oring.sizes`. |
| `33-oring.extrusion-limits` | `oring.extrusion` (table `ext`) | F3 | built. Planned name was `oring.extrusion-limits`. |
| `34-asme.b31-3.allowable-stress` | `pipe.allowable-range` | B8 | built. Planned name was `asme.b31-3.allowable-stress`. |
| `35-asme.b31-3.stress-range` | none (superseded) | B8 (built) | superseded. Sa already holds the factor f. |
| `36-asme.b31-3.thermal` | none (covered by `material.properties`) | B8 (built) | superseded. B8 takes E and alpha from a W3 material (templates 62 and 63). |
| `37-containment.cable-factors` | none (superseded) | E8 (built) | superseded. E8 uses areas and a space factor, not cable factors. |
| `38-containment.conduit-trunking` | `containment.space-factor` | E8 | built. Planned name was `containment.conduit-trunking`. |
| `39-en10270.spring-wire` | `spring.wire-strength` | M7 | built. Planned name was `en10270.spring-wire`. Also covered by the W3 material library: `Rm_A` and `Rm_m` on a material give R_m(d) (template 62). |
| `40-en13906.permissible-stress` | `spring.permissible` | M7 | built. Planned name was `en13906.permissible-stress`. |
| `41-climate.design-temperatures` | `climate.design-temperature` | B4 | built. Planned name was `climate.design-temperatures`. |
| `42-room.internal-temperatures` | `room.internal-temperatures` (proposed, no kind yet) | B4 (built, no record kind) | planned (no kind in the built calculator) |
| `43-room.air-change-rates` | `room.air-change-rates` (proposed, no kind yet) | B4 (built, no record kind) | planned (no kind in the built calculator) |
| `44-ground.u-values` | `ground.u-values` (proposed, no kind yet) | B4 (built, no record kind) | planned (no kind in the built calculator) |
| `45-climate.monthly` | `climate.monthly` | B7 | built |
| `46-insulation.bs5422` | `criteria.insulation` | B9 | built. Planned name was `insulation.bs5422`. |
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
| `64-material.secant-curve` | `material.properties` (`Es_curve`) | W3 library | built. M8 reads it as `Es_pts`. |
| `65-material.friction-pairs` | `material.properties` (`mu_pairs`) | W3 library | built. M8 can read mu from it (template 69). |
| `66-cylinder.mounting` | `cylinder.mounting` | F1 | built |
| `67-cylinder.buckling-safety` | `cylinder.buckling-safety` | F1 | built |
| `68-cylinder.hose-sizes` | `cylinder.hose-sizes` (table `hoses`` | F1 | built |
| `69-snapfit.friction` | `snapfit.friction` | M8 | built |
| `70-snapfit.annular-factor` | `snapfit.annular-factor` | M8 | built |
| `71-snapfit.annular-force-factor` | `snapfit.annular-force-factor` | M8 | built |
| `72-snapfit.secant-modulus` | `snapfit.secant-modulus` | M8 | built |
| `73-plate.deflection-limit` | `plate.deflection-limit` | S9 | built |
| `74-pipe.section` | `pipe.section` | B8 | built |
| `75-pipe.screening-k1` | `pipe.screening-k1` | B8 | built |
| `76-bellows.data` | `bellows.data` | B8 | built |
| `77-surface.coefficient` | `surface.coefficient` | B9 | built |
| `78-surface.emissivity` | `surface.emissivity` | B9 | built |
| `79-cable.bend-radius` | `cable.bend-radius` | E8 | built |
| `80-containment.friction` | `containment.friction` | E8 | built |
| `81-cable.pulling-limit` | `cable.pulling-limit` | E8 | built |
| `82-containment.weight-correction` | `containment.weight-correction` | E8 | built |

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
- F3 `oring.extrusion` also lists the single value `gap_allow`. Template 33 holds the table `ext` only.
- F1, M8, B8, S9, B9 and E8 read material properties through `material.properties`. Template 62 holds them.
- S9 reads gamma_M0 from `ec3.national-annex`. Template 14 holds it.

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
- Decide how the importer maps `Es_curve` of template 64 to the M8 table input `Es_pts`.
- Decide how the importer converts alpha in 1/K (template 62) to the B8 input unit um/(m*K).
- Keep licensed values out of every export, as CalcDataRecord requires.
