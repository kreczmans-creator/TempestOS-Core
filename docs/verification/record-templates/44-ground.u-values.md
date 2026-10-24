# 44 ground.u-values

Kind: `ground.u-values` (Ground floor U-value inputs).
Status: proposed — no CalcRecordKind yet; Track S confirms symbols when the calculator is built

## Used by
- B4 U-value and room heat loss (planned) reads the ground floor U-value for the floor and soil.

## Copy from
- Type the values from CIBSE Guide A, the ground floor U-value tables, or from BS EN ISO 13370.
- Cite the document, its edition, and the table numbers.

## Columns
- `construction[text]`: floor construction type, as the table prints it.
- `lambda_g[W/(m*K)]`: soil thermal conductivity the table assumes.
- `B_prime[m]`: characteristic floor dimension, area over half the perimeter.
- `R_f[m2*K/W]`: floor insulation thermal resistance.
- `U_g[W/(m2*K)]`: ground floor U-value.

## Edition to record
Record the document edition and amendment. Note the soil type in notes.

## Importer checks
- Reject any row with a negative number in a numeric column. R_f may be zero.
- Reject a duplicate construction, lambda_g, B_prime and R_f row.
- Within one construction and R_f, U_g must fall as B_prime rises.

## Maps to CalcDataRecord
Kind is `ground.u-values`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `ground_u_table` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
