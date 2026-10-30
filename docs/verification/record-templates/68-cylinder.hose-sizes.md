# 68 cylinder.hose-sizes

Kind: `cylinder.hose-sizes` (hose and pipe inside diameters).
Status: existing CalcRecordKind `cylinder.hose-sizes`

## Used by
- F1 cylinder circuit (built, `fp.cylinder`) picks the smallest listed bore that keeps each line under its velocity limit.

## Symbol mapping
- The whole file becomes one value with Symbol `hoses`. Its rows fill the `hoses` table input.
- The header holds the column symbols of that table and nothing else.
- Enter one range per file. Name the range in the title label.

## Copy from
- Type the inside diameters from the supplier's catalogue for the range you use.
- Cite the catalogue, its edition or date, and the page.

## Columns
- `ID[mm]`: inside diameter of one hose or pipe size.

## Edition to record
Record the catalogue edition or date and the download date.

## Importer checks
- Reject a blank or non-positive ID. Reject a duplicate ID.
- F1 refuses ID outside 3 to 200 mm. It takes 60 rows at most.
- Warn when the rows are not in rising order.

## Maps to CalcDataRecord
Kind is `cylinder.hose-sizes`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `hoses` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
