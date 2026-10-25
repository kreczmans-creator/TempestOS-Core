# 80 containment.friction

Kind: `containment.friction` (pulling friction coefficient).
Status: existing CalcRecordKind `containment.friction`

## Used by
- E8 containment (built, `elec.containment`) uses mu in the capstan equation for the pull.

## Symbol mapping
- `mu` fill the E8 input of the same symbol.
- `pair` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.

## Copy from
- Type mu from the cable or lubricant maker, or from your own pulling records.
- Cite the source and its date.

## Columns
- `pair[text]`: sheath and containment pair, with or without lubricant.
- `mu[-]`: coefficient of friction of the cable sheath on the containment.

## Edition to record
Record the source date.

## Importer checks
- Reject a blank pair. Reject a duplicate pair.
- Reject a negative mu. E8 refuses mu outside 0 to 2.
- Warn when mu exceeds 1.

## Maps to CalcDataRecord
Kind is `containment.friction`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `pair` text joins the record title. Columns the kind does not list are stored but never applied.
