# 24 bearing.ratings

Kind: `bearing.ratings` (Bearing load ratings).
Status: existing CalcRecordKind

## Used by
- M5 bearing life (built) reads C and C_0.
- The drive-train chain E1 to M4 to M5 to M2 (built) takes its bearing loads from M4.

## Copy from
- Type the ratings from the manufacturer's catalogue or product page for the exact designation.
- Cite the manufacturer, the catalogue edition and the product table number.

## Columns
- `designation[text]`: bearing designation, as the catalogue prints it.
- `C[kN]`: basic dynamic load rating.
- `C_0[kN]`: basic static load rating.

## Edition to record
Record the catalogue edition or year, the page or table number, and the catalogue download date.

## Importer checks
- Reject any row with a blank or non-positive number in a numeric column.
- Reject a duplicate designation.
- Warn when C or C_0 is outside 0.1 to 10000 kN.
- Warn when the designation is blank.

## Maps to CalcDataRecord
Kind is `bearing.ratings`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `designation` text joins the record title. Columns the kind does not list are stored but never applied.
