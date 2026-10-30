# Record templates (Track S, DC-10)

**In plain terms.** Track P wrote 82 header-only CSV templates (`docs/verification/record-templates`, read-only) for the reference records the calculators take from the user's own licensed copies. Eight record kinds the calculators declare had no template there. These eight files fill that gap: one header row each, units in the header, no data rows, and a note that says what to copy from, which checks the importer makes and how the file becomes records. They follow Track P's conventions and are held to the importer's catalogue (`RecordTemplates`, entries 83 to 90) by the same contract tests.

Nothing numeric from a licensed standard is typed here or in the importer: a template carries field names, units and the source to cite, never a value.

The three lifting templates (88 to 90) fill kinds the lifting calculator declares, which the lifting flag hides (ADR-0173); the importer lists and accepts them only while the flag is on.

## Kind name map

The CalcRecordKind column is the exact string in the calculator's `RecordKinds` list.

| Template file | CalcRecordKind | Calculator | Built or planned |
|---|---|---|---|
| `83-motor.rating` | `motor.rating` | E1 | built |
| `84-cable.impedance` | `cable.impedance` | E9 | built |
| `85-motor.start` | `motor.start` | E9 | built |
| `86-starter.limits` | `starter.limits` | E9 | built |
| `87-transformer.data` | `transformer.data` | E9 | built |
| `88-lifting.design-factors` | `lifting.design-factors` | S8 | built |
| `89-lifting.allowable-stress` | `lifting.allowable-stress` | S8 | built |
| `90-lifting.limits` | `lifting.limits` | S8 | built |

Track P's templates 03, 05, 19, 42, 43 and 44 (proposed kinds in Track P's README) are built under DC-10 as well: E5 declares `bs7671.max-zs` (the table input `Z_s_max_table`), E3 declares `bs7671.voltage-drop` (the table input `mVAm_table`), S6 declares `concrete.strengths` (`f_ck`), and B4 declares `room.internal-temperatures` (`T_int`, from the column `theta_i`), `room.air-change-rates` (`n`, from the column `n_ach`) and `ground.u-values` (the new input `U_g`). Template 48 (`surface.resistances`) stays proposed: its cells belong to the B4 elements table, which is the room's geometry and not record data (Black B-A-10).

## CSV conventions

As Track P's README: UTF-8, a comma separator, one header row `symbol[unit]` with `[-]` for a dimensionless column and `[text]` for text, ASCII units with a digit as a power (`kg*m2` is kg·m²), one row per record, blank cells where the source gives no value, and no text cell that starts with `=`, `+`, `-` or `@`. The importer judges every number against the limits of the calculator that reads it, then against the checks in each note.

## Provenance

The records screen asks for the title, the source title, the edition and amendment, the table or clause and the licence holder, and stamps the signed-in person, the time and the file's SHA-256 on every value; a licensed source (the default) never leaves through an export. A Track P `_provenance.csv` row can fill those fields.
