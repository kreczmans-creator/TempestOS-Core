# 55 accumulator.sizes

Kind: `accumulator.sizes` (nominal gas volumes in the manufacturer's range).
Status: existing CalcRecordKind `accumulator.sizes`

## Used by
- F2 accumulator sizing (built, `fp.accumulator`) picks the smallest listed size that covers the required gas volume.
- The size list is optional. Without it F2 selects no size and reports the required gas volume V0.

## Symbol mapping
- The whole file becomes one value with Symbol `sizes`. Its rows fill the `sizes` table input.
- The header is the one column symbol of that table: `V` in L.
- Enter one range per file. Name the type and pressure class in the title label, such as `Title [bladder 350 bar]`.

## Copy from
- Type the nominal gas volumes from the manufacturer's catalogue for your type and pressure class.
- Cite the catalogue, its edition or date, and the page or table.

## Columns
- `V[L]`: nominal gas volume of one size.

## Edition to record
Record the catalogue edition or date, and the download date.

## Importer checks
- Reject a blank or non-positive V. Reject a duplicate V.
- Reject more than 60 rows. F2 takes 60 rows at most.
- F2 refuses V outside 0.01 to 5000 L.
- Warn when the rows are not in rising order.

## Maps to CalcDataRecord
Kind is `accumulator.sizes`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `sizes` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
