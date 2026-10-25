# 67 cylinder.buckling-safety

Kind: `cylinder.buckling-safety` (safety factor on the rod critical load).
Status: existing CalcRecordKind `cylinder.buckling-safety`

## Used by
- F1 cylinder circuit (built, `fp.cylinder`) divides the critical load by S_f.

## Symbol mapping
- `S_f` fill the F1 input of the same symbol.
- `basis` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.
- F1 default of 3.5 is a placeholder, not a value from a standard.

## Copy from
- Type S_f from your design reference or the cylinder maker.
- Cite the source and its edition.

## Columns
- `basis[text]`: design reference and duty the factor applies to.
- `S_f[-]`: safety factor on the critical buckling load.

## Edition to record
Record the source edition.

## Importer checks
- Reject a blank basis. Reject a duplicate basis.
- Reject a blank S_f. F1 refuses S_f outside 1 to 20.
- Warn when S_f is below 2.

## Maps to CalcDataRecord
Kind is `cylinder.buckling-safety`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `basis` text joins the record title. Columns the kind does not list are stored but never applied.
