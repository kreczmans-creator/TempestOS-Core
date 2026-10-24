# 33 oring.extrusion-limits

Kind: `oring.extrusion-limits` (O-ring extrusion limits).
Status: proposed — no CalcRecordKind yet; Track S confirms symbols when the calculator is built

## Used by
- F3 O-ring gland (planned) checks the diametral clearance against the pressure and the seal hardness.

## Copy from
- Type the limits from the O-ring supplier's handbook, for example the Parker O-Ring Handbook, ORD 5700.
- Cite the handbook edition and the figure or table number.

## Columns
- `hardness[ShoreA]`: seal hardness the curve applies to.
- `backup_ring[text]`: none or fitted, as the handbook names it.
- `p[bar]`: pressure point on the curve.
- `clearance_max[mm]`: maximum diametral clearance at that pressure.

## Edition to record
Record the handbook edition and date. Note the temperature the curve assumes in notes.

## Importer checks
- Reject any row with a blank or non-positive number in a numeric column.
- Reject a duplicate hardness, backup_ring and p row.
- Within one hardness and backup_ring, p must rise and clearance_max must not rise.
- Warn when hardness is outside 60 to 95 ShoreA.

## Maps to CalcDataRecord
Kind is `oring.extrusion-limits`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `extrusion_table` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
