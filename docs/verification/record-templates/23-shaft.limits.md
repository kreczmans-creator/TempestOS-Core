# 23 shaft.limits

Kind: `shaft.limits` (Slope and deflection limits).
Status: existing CalcRecordKind

## Used by
- M4 shaft (built) checks the slope at the bearings and the deflection at the loads.

## Copy from
- Type the limits from the bearing manufacturer's catalogue and the gear manufacturer's data.
- Cite each document, its edition or date, and the table or page.

## Columns
- `component[text]`: bearing or gear type the limit applies to.
- `theta_max[rad]`: allowable slope at the bearing.
- `y_max[mm]`: allowable deflection at the load.

## Edition to record
Record the catalogue edition or year, the page or table number, and the catalogue download date.

## Importer checks
- Reject any row with a blank or non-positive number in a numeric column.
- Warn when theta_max is outside 1e-5 to 0.1 rad.
- Warn when y_max is outside 0.001 to 10 mm.
- Reject a row where the component is blank.

## Maps to CalcDataRecord
Kind is `shaft.limits`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `component` text joins the record title. Columns the kind does not list are stored but never applied.
