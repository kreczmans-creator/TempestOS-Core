# 22 shaft.material

Kind: `shaft.material` (Shaft material).
Status: existing CalcRecordKind

## Used by
- M4 shaft (built) reads S_ut, S_y and E.
- The drive-train chain E1 to M4 to M5 to M2 (built) passes through M4.

## Copy from
- Type the values from the material standard for the shaft steel, for example the table for the bar diameter band.
- Or type them from the mill certificate. Cite the standard, its edition and the table number.

## Columns
- `material[text]`: material and condition, as the standard prints them.
- `d_min[mm]`: lower bound of the diameter or thickness band.
- `d_max[mm]`: upper bound of the diameter or thickness band.
- `S_ut[MPa]`: ultimate tensile strength.
- `S_y[MPa]`: yield strength.
- `E[GPa]`: Young's modulus.

## Edition to record
Record the standard edition and amendment, or the certificate number and date.

## Importer checks
- Reject a blank value. Reject a non-positive S_ut, S_y or E. The lowest d_min may be zero.
- Reject a row where S_y is not below S_ut.
- Warn when S_ut is outside 300 to 2000 MPa or S_y is outside 150 to 1900 MPa.
- Warn when E is outside 50 to 250 GPa.
- Reject a row where d_max does not exceed d_min.

## Maps to CalcDataRecord
Kind is `shaft.material`. The importer saves one CalcDataRecord per CSV row.
Each row gives Values `S_ut`, `S_y` and `E`. The material and band join the record title.
d_min and d_max are stored as extra Values and never applied.
