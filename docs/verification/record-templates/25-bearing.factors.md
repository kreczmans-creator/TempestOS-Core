# 25 bearing.factors

Kind: `bearing.factors` (Bearing X, Y, e factors).
Status: existing CalcRecordKind

## Used by
- M5 bearing life (built) reads e_lim, X, Y, X_0 and Y_0.

## Copy from
- Type the factors from the manufacturer's catalogue for the bearing type and the F_a/C_0 ratio.
- Cite the manufacturer, the catalogue edition and the table number.

## Columns
- `bearing_type[text]`: bearing type, as the catalogue prints it.
- `Fa_C0[-]`: ratio of axial load to static rating the row applies to.
- `e_lim[-]`: load ratio limit e.
- `X[-]`: radial factor.
- `Y[-]`: axial factor.
- `X_0[-]`: static radial factor.
- `Y_0[-]`: static axial factor.

## Edition to record
Record the catalogue edition or year, the page or table number, and the catalogue download date.

## Importer checks
- Reject a blank or negative number in a numeric column.
- Warn when e_lim, X or X_0 is above 5, or Y or Y_0 is above 10.
- Within one bearing type, Fa_C0 must rise down the rows.
- Reject a duplicate bearing_type and Fa_C0 row.

## Maps to CalcDataRecord
Kind is `bearing.factors`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `bearing_type` text joins the record title. Columns the kind does not list are stored but never applied.
