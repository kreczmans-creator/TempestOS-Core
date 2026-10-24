# 16 fluid.properties

Kind: `fluid.properties` (Fluid specific heats).
Status: existing CalcRecordKind

## Used by
- B5 heat exchanger (built) reads cp_h and cp_c.
- F2 accumulator (planned) and the HPU chain (planned) use hydraulic fluid data.

## Copy from
- Type the values from the fluid supplier's data sheet or a property table you hold a licence for.
- Cite the supplier or table, the edition or date, and the table number or page.

## Columns
- `fluid_h[text]`: hot-side fluid name.
- `t_mean_h[degC]`: hot-side mean temperature the value applies to.
- `cp_h[J/(kg*K)]`: hot-side specific heat capacity.
- `fluid_c[text]`: cold-side fluid name.
- `t_mean_c[degC]`: cold-side mean temperature the value applies to.
- `cp_c[J/(kg*K)]`: cold-side specific heat capacity.

## Edition to record
Record the data sheet edition or date. F2 also needs density and bulk modulus. The kind has no symbols for them yet.

## Importer checks
- Reject a blank or non-positive cp_h or cp_c. Reject a blank mean temperature.
- Warn when cp_h or cp_c is outside 500 to 10000 J/(kg*K).
- Warn when a mean temperature is outside -40 to 200 degC.
- Reject a row where a fluid name is blank.

## Maps to CalcDataRecord
Kind is `fluid.properties`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `fluid_h` text joins the record title. Columns the kind does not list are stored but never applied.
