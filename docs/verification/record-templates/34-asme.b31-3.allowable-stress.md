# 34 asme.b31-3.allowable-stress

Kind: `asme.b31-3.allowable-stress` (ASME B31.3 allowable stresses).
Status: proposed — no CalcRecordKind yet; Track S confirms symbols when the calculator is built

## Used by
- B8 pipe expansion (planned) reads S_c and S_h for the allowable displacement stress range.

## Copy from
- Type the values from ASME B31.3, Table A-1, for the material and temperature.
- Use the SI table if your edition has one. Cite the material specification and grade.

## Columns
- `material[text]`: material specification and grade, as the table prints it.
- `t_cold[degC]`: minimum metal temperature the S_c value applies to.
- `S_c[MPa]`: basic allowable stress at the minimum metal temperature.
- `t_hot[degC]`: maximum metal temperature the S_h value applies to.
- `S_h[MPa]`: basic allowable stress at the maximum metal temperature.

## Edition to record
Record the ASME B31.3 edition year and addenda. Note the units of the table you used.

## Importer checks
- Reject a blank value. Reject a non-positive S_c or S_h.
- Reject a row where t_hot does not exceed t_cold.
- Warn when S_h exceeds S_c.
- Warn when S_c or S_h is outside 10 to 400 MPa.
- Reject a duplicate material, t_cold and t_hot row.

## Maps to CalcDataRecord
Kind is `asme.b31-3.allowable-stress`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `material` text joins the record title. Columns the kind does not list are stored but never applied.
