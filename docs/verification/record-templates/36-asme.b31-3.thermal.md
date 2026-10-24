# 36 asme.b31-3.thermal

Kind: `asme.b31-3.thermal` (ASME B31.3 thermal expansion and modulus).
Status: proposed — no CalcRecordKind yet; Track S confirms symbols when the calculator is built

## Used by
- B8 pipe expansion (planned) reads the expansion and the modulus for the pipe material.

## Copy from
- Type the values from the ASME B31.3 Appendix C tables for thermal expansion and modulus of elasticity.
- Confirm the table numbers against your copy.

## Columns
- `material[text]`: material group, as the table prints it.
- `t_metal[degC]`: metal temperature.
- `e_expansion[mm/m]`: total linear thermal expansion from the reference temperature.
- `E_mod[GPa]`: modulus of elasticity at that temperature.

## Edition to record
Record the ASME B31.3 edition year and addenda. Note the reference temperature in notes.

## Importer checks
- Reject a blank value. Reject a non-positive E_mod. The temperature may be zero or negative, and e_expansion may be zero.
- Within one material, t_metal must rise and e_expansion must rise.
- Reject a duplicate material and t_metal row.
- Warn when E_mod is outside 50 to 250 GPa.

## Maps to CalcDataRecord
Kind is `asme.b31-3.thermal`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `thermal_table` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
