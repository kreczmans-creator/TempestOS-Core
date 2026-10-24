# 48 surface.resistances

Kind: `surface.resistances` (surface resistances and air-gap resistance).
Status: existing CalcRecordKind `surface.resistances`

## Used by
- B4 U-value and room heat loss (built, `bs.room-heat-loss`) reads the `Rsi`, `Rse` and `Rg` columns of its `elements` table.
- B4 uses conventional surface resistances for any element that leaves Rsi or Rse blank.

## Symbol mapping
- The whole file becomes one value with Symbol `elements`. Its rows fill the `elements` table input.
- Each header is a column symbol of that table, in m^2*K/W. The importer writes `m2*K/W` as m^2*K/W.
- The kind lists only these three columns. The table also needs `name` and `area` on every row.
- Applying the record replaces the table rows as it stands. Track S must merge by row or the user must retype the other columns.
- Enter one flow direction or one gap case per file. Name it in the title label, such as `Title [upward flow]`.

## Copy from
- Type the values from BS EN ISO 6946 for the flow direction, or from your own source.
- Cite the standard, its edition, and the table.
- Rg is the air-gap resistance. Leave it blank when the element has no air gap.

## Columns
- `Rsi[m2*K/W]`: internal surface resistance.
- `Rse[m2*K/W]`: external surface resistance.
- `Rg[m2*K/W]`: air-gap resistance.

## Edition to record
Record the edition and amendment printed on your copy.

## Importer checks
- Reject a negative number. Leave a cell blank when the source gives no value.
- B4 refuses Rsi or Rse outside 0 to 1 m2*K/W and Rg outside 0 to 2 m2*K/W.
- Warn when Rsi or Rse is outside 0.04 to 0.25, or Rg is outside 0 to 0.6.
- Reject more than 60 rows. B4 takes 60 elements at most.

## Maps to CalcDataRecord
Kind is `surface.resistances`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `elements` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
