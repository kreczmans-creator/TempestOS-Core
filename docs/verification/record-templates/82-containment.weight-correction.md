# 82 containment.weight-correction

Kind: `containment.weight-correction` (cable weight-correction factor).
Status: existing CalcRecordKind `containment.weight-correction`

## Used by
- E8 containment (built, `elec.containment`) multiplies the cable weight on the containment by w_c.

## Symbol mapping
- `w_c` fill the E8 input of the same symbol.
- `arrangement` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.
- Use 1 for full weight on the containment.

## Copy from
- Type w_c from your reference for the arrangement.
- Cite the source and its edition.

## Columns
- `arrangement[text]`: cable arrangement, such as cradled or triangular.
- `w_c[-]`: factor on the cable weight that bears on the containment.

## Edition to record
Record the source edition.

## Importer checks
- Reject a blank arrangement. Reject a duplicate arrangement.
- Reject a blank w_c. E8 refuses w_c outside 1 to 10.
- Warn when w_c exceeds 3.

## Maps to CalcDataRecord
Kind is `containment.weight-correction`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `arrangement` text joins the record title. Columns the kind does not list are stored but never applied.
