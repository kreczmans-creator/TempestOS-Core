# 71 snapfit.annular-force-factor

Kind: `snapfit.annular-force-factor` (annular snap force factor).
Status: existing CalcRecordKind `snapfit.annular-force-factor`

## Used by
- M8 snap-fits (built, `mech.snap-fit`) multiplies the hoop force of the free ring by K_F.

## Symbol mapping
- `K_F` fill the M8 input of the same symbol.
- `arrangement` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.
- A blank record means 1 on the sheet.

## Copy from
- Type K_F from your design reference.
- Cite the source and its edition.

## Columns
- `arrangement[text]`: support arrangement the factor applies to, such as a tube behind the ring.
- `K_F[-]`: factor on the hoop force of the free ring.

## Edition to record
Record the source edition.

## Importer checks
- Reject a blank arrangement. Reject a duplicate arrangement.
- Reject a blank K_F. M8 refuses K_F outside 0.01 to 20.
- Warn when K_F is outside 0.5 to 5.

## Maps to CalcDataRecord
Kind is `snapfit.annular-force-factor`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `arrangement` text joins the record title. Columns the kind does not list are stored but never applied.
