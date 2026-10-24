# 32 oring.sizes

Kind: `oring.sizes` (O-ring standard sizes).
Status: proposed — no CalcRecordKind yet; Track S confirms symbols when the calculator is built

## Used by
- F3 O-ring gland (planned) picks a standard size that fits the gland.

## Copy from
- Type the sizes from SAE AS568, or from ISO 3601-1.
- Cite the standard, its edition, and the size table number.

## Columns
- `size[text]`: size code, as the standard prints it.
- `d_1[mm]`: inside diameter.
- `d_2[mm]`: cross-section diameter.
- `tol_d1[mm]`: tolerance on the inside diameter. Blank if not given.
- `tol_d2[mm]`: tolerance on the cross-section. Blank if not given.

## Edition to record
Record the standard edition and amendment. Note whether the sizes are inch or metric in notes.

## Importer checks
- Reject any row with a blank or non-positive number in a numeric column. Tolerances may be blank.
- Reject a duplicate size.
- Warn when d_1 is below d_2.

## Maps to CalcDataRecord
Kind is `oring.sizes`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `size_table` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
