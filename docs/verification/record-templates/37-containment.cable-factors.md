# 37 containment.cable-factors

Kind: `containment.cable-factors` (IET containment cable factors).
Status: proposed — no CalcRecordKind yet; Track S confirms symbols when the calculator is built

## Used by
- E8 containment (planned) sums cable factors to size conduit and trunking.

## Copy from
- Type the values from the IET On-Site Guide, the appendix on cable capacities of conduit and trunking.
- Cite the guide edition and table numbers. Confirm them against your copy.

## Columns
- `cable_type[text]`: cable type, as the table prints it.
- `csa[mm2]`: conductor cross-sectional area.
- `F_conduit_straight[-]`: cable factor for a straight conduit run. Blank if not given.
- `F_conduit_bends[-]`: cable factor for a conduit run with bends. Blank if not given.
- `F_trunking[-]`: cable factor for trunking. Blank if not given.

## Edition to record
Record the guide edition and the BS 7671 amendment it follows.

## Importer checks
- Reject any row with a non-positive number in a numeric column. Factor columns may be blank.
- Reject a duplicate cable_type and csa row.
- Within one cable_type, csa and every filled factor must rise.

## Maps to CalcDataRecord
Kind is `containment.cable-factors`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `cable_factor_table` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
