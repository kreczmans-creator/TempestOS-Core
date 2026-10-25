# 53 spring.endurance

Kind: `spring.endurance` (alternating endurance strength of the wire).
Status: existing CalcRecordKind `spring.endurance`

## Used by
- M7 springs (built, `mech.spring`) reads `S_e` for the Goodman fatigue check.
- M7 has no default for `S_e`. The fatigue result stays blocked until you enter it.

## Symbol mapping
- `S_e[MPa]` fills the M7 input `S_e`.
- `wire`, `stress` and `N_c` identify the row. The importer stores them and applies none of them.
- Set the M7 input `N_c` to the same life by hand. The record does not set it.
- The importer saves one record per row. Pick the row for your wire and your required life.

## Copy from
- Type the endurance strength from the wire maker's data or your fatigue reference.
- Use shear strength for compression and extension springs. Use bending strength for torsion springs.
- Cite the source, its edition and the table or chart number.

## Columns
- `wire[text]`: wire grade and surface condition, as the source names them.
- `stress[text]`: shear or bending.
- `N_c[-]`: number of cycles the strength belongs to.
- `S_e[MPa]`: alternating endurance strength at that life.

## Edition to record
Record the source edition and amendment, or the wire maker's data sheet date.

## Importer checks
- Reject a blank wire or stress. Reject a duplicate wire, stress and N_c row.
- Reject a blank or non-positive N_c or S_e.
- M7 refuses S_e outside 1 to 10000 MPa. Warn outside 50 to 2000 MPa.
- M7 refuses N_c outside 1 to 1e12. Warn outside 1e4 to 1e10.

## Maps to CalcDataRecord
Kind is `spring.endurance`. The importer saves one CalcDataRecord per CSV row.
The `S_e` column becomes one CalcDataValue with Symbol `S_e`, a Number and the Unit `MPa`.
The `wire`, `stress` and `N_c` text join the record title. Columns the kind does not list are stored but never applied.
