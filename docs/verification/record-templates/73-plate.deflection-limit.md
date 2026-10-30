# 73 plate.deflection-limit

Kind: `plate.deflection-limit` (plate deflection limit).
Status: existing CalcRecordKind `plate.deflection-limit`

## Used by
- S9 plate bending (built, `struct.plate-bending`) checks the deflection against span divided by this ratio.

## Symbol mapping
- `defl_ratio` fill the S9 input of the same symbol.
- `application` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.
- S9 default is 250.

## Copy from
- Type the ratio from the specification or the product standard in use.
- Cite the document, its edition and the clause.

## Columns
- `application[text]`: plate use and limit source.
- `defl_ratio[-]`: span-to-deflection ratio, such as 250 for span/250.

## Edition to record
Record the document edition.

## Importer checks
- Reject a blank application. Reject a duplicate application.
- Reject a blank defl_ratio. S9 refuses defl_ratio outside 10 to 10000.
- Warn when defl_ratio is outside 50 to 2000.

## Maps to CalcDataRecord
Kind is `plate.deflection-limit`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `application` text joins the record title. Columns the kind does not list are stored but never applied.
