# 50 thermal.bridging

Kind: `thermal.bridging` (thermal-bridge allowance).
Status: existing CalcRecordKind `thermal.bridging`

## Used by
- B4 U-value and room heat loss (built, `bs.room-heat-loss`) reads `y_tb` as the allowance on the area facing outside.

## Symbol mapping
- `y_tb[W/(m2*K)]` fills the B4 input `y_tb`, a quantity in W/(m^2*K). The importer writes `m2` as m^2.
- `construction` identifies the row. The importer stores it but B4 never reads it.
- The importer saves one record per row. Pick the row for your construction.

## Copy from
- Type the allowance from your own source, or from a bridging calculation for the construction.
- Cite the source, its edition and the table, or file the calculation as evidence.
- Set origin to `Derived` when you calculated the value.

## Columns
- `construction[text]`: the construction the allowance applies to, as your source names it.
- `y_tb[W/(m2*K)]`: thermal-bridge allowance on the area facing outside.

## Edition to record
Record the source edition and amendment, or the date of your calculation.

## Importer checks
- Reject a blank construction. Reject a duplicate construction.
- Reject a blank or negative y_tb. Zero is allowed.
- B4 refuses y_tb outside 0 to 1 W/(m2*K). Warn when y_tb is above 0.3.

## Maps to CalcDataRecord
Kind is `thermal.bridging`. The importer saves one CalcDataRecord per CSV row.
The `y_tb` column becomes one CalcDataValue with Symbol `y_tb`, a Number and the Unit `W/(m^2*K)`.
The `construction` text joins the record title. Columns the kind does not list are stored but never applied.
