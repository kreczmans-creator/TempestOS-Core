# 12 steel.strengths

Kind: `steel.strengths` (Steel plate strengths by thickness band).
Status: existing CalcRecordKind `steel.strengths`

## Used by
- S4 EN 1993-1-8 bolts and welds (built, `struct.connection-ec3`) lists f_y and f_u for this kind.
- S6 base plate (built, `struct.base-plate`) lists f_y only. It fills the plate yield strength input `f_y`.
- S8 lifting lugs (planned) and S7 LTB (planned) will read the same values.

## Symbol mapping
- `f_y[MPa]` fills the input `f_y` in S4 and S6. `f_u[MPa]` fills the input `f_u` in S4 only.
- S6 ignores `f_u`. The importer applies only the symbols the calculator lists.
- Pick the row for the plate grade and the plate thickness t_p.

## Copy from
- Type the values from your licensed copy of EN 10025-2, the tables for yield and tensile strength by thickness.
- You may instead type the nominal values from EN 1993-1-1, Table 3.1. Confirm the table numbers against your copy.

## Columns
- `grade[text]`: steel grade, for example the grade name as the standard prints it.
- `t_min[mm]`: lower bound of the thickness band.
- `t_max[mm]`: upper bound of the thickness band.
- `f_y[MPa]`: yield strength for the band.
- `f_u[MPa]`: tensile strength for the band.

## Edition to record
Record the EN 10025-2 edition and amendment, or the EN 1993-1-1 edition if you use its Table 3.1.

## Importer checks
- Reject a blank value. Reject a non-positive f_y or f_u. The lowest t_min may be zero.
- Reject a row where t_max does not exceed t_min. Reject overlapping bands within one grade.
- Within one grade, f_y must not rise as thickness rises.
- Reject a row where f_u does not exceed f_y.
- S6 refuses f_y outside 235 to 460 MPa. Warn when f_u is outside 340 to 720 MPa.

## Maps to CalcDataRecord
Kind is `steel.strengths`. The importer saves one CalcDataRecord per CSV row.
Each row gives Values `f_y` and `f_u` in MPa. The grade and band join the record title.
t_min and t_max are stored as extra Values and never applied.
