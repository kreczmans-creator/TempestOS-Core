# 31 oring.gland

Kind: `oring.gland` (O-ring gland dimensions).
Status: proposed — no CalcRecordKind yet; Track S confirms symbols when the calculator is built

## Used by
- F3 O-ring gland (planned) reads groove depth, width and squeeze for each cross-section and application.

## Copy from
- Type the values from ISO 3601-2 (housing dimensions), or from the O-ring supplier's gland tables.
- Cite the standard or handbook, its edition, and the table number.

## Columns
- `application[text]`: static radial, static axial or dynamic, as the table names it.
- `d_2[mm]`: O-ring cross-section diameter.
- `groove_depth[mm]`: groove depth.
- `groove_width[mm]`: groove width.
- `groove_radius[mm]`: groove bottom radius. Blank if not given.
- `squeeze_min[%]`: minimum squeeze. Blank if not given.
- `squeeze_max[%]`: maximum squeeze. Blank if not given.

## Edition to record
Record the standard or handbook edition and amendment.

## Importer checks
- Reject a blank or non-positive d_2, groove_depth or groove_width.
- Reject a duplicate application and d_2 row.
- Reject a row where squeeze_max is below squeeze_min.
- Within one application, d_2 and groove_depth must rise together.

## Maps to CalcDataRecord
Kind is `oring.gland`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `gland_table` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
