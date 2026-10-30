# 30 battery.factors

Kind: `battery.factors` (Battery factors).
Status: existing CalcRecordKind

## Used by
- E6 load budget (built) reads DoD, eta_b, k_temp, k_age, k_p and H_rate for battery sizing.

## Copy from
- Type the values from the battery manufacturer's data for the chemistry and the temperature.
- Cite the manufacturer, the data sheet edition and the table number.

## Columns
- `battery[text]`: battery model and chemistry.
- `theta[degC]`: temperature the k_temp value applies to.
- `DoD[-]`: allowed depth of discharge.
- `eta_b[-]`: discharge efficiency.
- `k_temp[-]`: temperature capacity factor.
- `k_age[-]`: end-of-life capacity factor.
- `k_p[-]`: Peukert exponent. Leave blank if not used.
- `H_rate[h]`: hour rate of the rated capacity. Blank if k_p is blank.

## Edition to record
Record the catalogue edition or year, the page or table number, and the catalogue download date.

## Importer checks
- Reject a blank or non-positive DoD, eta_b, k_temp or k_age.
- Warn outside these ranges: DoD 0.1 to 1, eta_b 0.5 to 1, k_temp 0.3 to 1.2, k_age 0.5 to 1.
- Warn when k_p is outside 1 to 1.5 or H_rate is outside 1 to 100 h.
- Reject a row that fills one of k_p and H_rate but not the other.

## Maps to CalcDataRecord
Kind is `battery.factors`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `battery` text joins the record title. Columns the kind does not list are stored but never applied.
