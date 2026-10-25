# 37 containment.cable-factors

Kind: none. No CalcRecordKind reads these columns.
Status: superseded. E8 sizes the fill from cable areas and a space factor, not from cable factors. Do not fill this file.
Planned name: `containment.cable-factors`. The file and its header stay so the numbering holds.

## Used by
- E8 containment (built, `elec.containment`) sums the cable cross-section areas and compares the fill with `f_max`. It sums no cable factors.

## Symbol mapping
- E8 has no cable factor input. Enter the fill limit in template 38.

## Copy from
- Nothing. Skip this file.

## Columns
- `cable_type[text]`: cable type, as the table prints it.
- `csa[mm2]`: conductor cross-sectional area.
- `F_conduit_straight[-]`: cable factor for a straight conduit run.
- `F_conduit_bends[-]`: cable factor for a conduit run with bends.
- `F_trunking[-]`: cable factor for trunking.

## Edition to record
Not applicable.

## Importer checks
- None. Track S may delete this template.

## Maps to CalcDataRecord
No CalcDataRecord.
