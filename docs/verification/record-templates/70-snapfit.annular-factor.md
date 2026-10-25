# 70 snapfit.annular-factor

Kind: `snapfit.annular-factor` (annular snap strain factor).
Status: existing CalcRecordKind `snapfit.annular-factor`

## Used by
- M8 snap-fits (built, `mech.snap-fit`) multiplies the nominal hoop strain by K_ann.

## Symbol mapping
- `K_ann` fill the M8 input of the same symbol.
- `shape` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.

## Copy from
- Type K_ann from your design reference for the ring shape.
- Cite the source and its edition.

## Columns
- `shape[text]`: ring shape the factor applies to.
- `K_ann[-]`: factor on the nominal hoop strain. Use 1 for a plain free ring.

## Edition to record
Record the source edition.

## Importer checks
- Reject a blank shape. Reject a duplicate shape.
- Reject a blank K_ann. M8 refuses K_ann outside 0.1 to 10.
- Warn when K_ann is outside 0.5 to 3.

## Maps to CalcDataRecord
Kind is `snapfit.annular-factor`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `shape` text joins the record title. Columns the kind does not list are stored but never applied.
