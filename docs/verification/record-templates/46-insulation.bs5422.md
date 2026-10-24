# 46 insulation.bs5422

Kind: `insulation.bs5422` (BS 5422 insulation thickness).
Status: proposed — no CalcRecordKind yet; Track S confirms symbols when the calculator is built

## Used by
- B9 insulation (planned) reads the tabulated thickness for heat conservation, condensation control or personnel protection.

## Copy from
- Type the values from BS 5422, the thickness tables for your purpose.
- Cite the edition and the table number. Enter one table per file.

## Columns
- `purpose[text]`: purpose, as the table names it.
- `lambda_ref[W/(m*K)]`: reference thermal conductivity the table assumes.
- `t_fluid[degC]`: fluid temperature.
- `dn[mm]`: pipe outside diameter or nominal size, as the table prints it.
- `thickness[mm]`: tabulated insulation thickness.

## Edition to record
Record the BS 5422 edition and amendment.

## Importer checks
- Reject a blank value. Reject a non-positive lambda_ref, dn or thickness. The fluid temperature may be zero or negative.
- Reject a duplicate purpose, lambda_ref, t_fluid and dn row.
- Within one purpose and t_fluid, dn must rise and thickness must not fall.
- Warn when thickness is above 300 mm.

## Maps to CalcDataRecord
Kind is `insulation.bs5422`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `thickness_table` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
