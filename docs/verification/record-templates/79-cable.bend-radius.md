# 79 cable.bend-radius

Kind: `cable.bend-radius` (minimum bend radius multiple).
Status: existing CalcRecordKind `cable.bend-radius`

## Used by
- E8 containment (built, `elec.containment`) compares each bend radius with n times the cable diameter.

## Symbol mapping
- `n_bend` fill the E8 input of the same symbol.
- `cable` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.
- Use the largest multiple among the cables in the bundle.

## Copy from
- Type n from the cable maker's data sheet.
- Cite the data sheet, its date and the page.

## Columns
- `cable[text]`: cable type, as the data sheet names it.
- `n_bend[-]`: minimum bend radius as a multiple of the overall cable diameter.

## Edition to record
Record the data sheet date.

## Importer checks
- Reject a blank cable. Reject a duplicate cable.
- Reject a blank n_bend. E8 refuses n_bend outside 1 to 100.
- Warn when n_bend is above 30.

## Maps to CalcDataRecord
Kind is `cable.bend-radius`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `cable` text joins the record title. Columns the kind does not list are stored but never applied.
