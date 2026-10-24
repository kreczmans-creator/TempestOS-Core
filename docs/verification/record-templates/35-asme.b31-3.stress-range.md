# 35 asme.b31-3.stress-range

Kind: `asme.b31-3.stress-range` (ASME B31.3 stress range reduction factor).
Status: proposed — no CalcRecordKind yet; Track S confirms symbols when the calculator is built

## Used by
- B8 pipe expansion (planned) reads f to reduce the allowable stress range for cyclic service.

## Copy from
- Type the values from ASME B31.3, Table 302.3.5.
- Cite the edition and the table number.

## Columns
- `N_min[-]`: lower bound of the equivalent full-range cycles.
- `N_max[-]`: upper bound of the equivalent full-range cycles.
- `f_stress[-]`: stress range reduction factor.

## Edition to record
Record the ASME B31.3 edition year and addenda.

## Importer checks
- Reject any row with a blank or non-positive number in a numeric column.
- Reject a row where N_max does not exceed N_min.
- Reject overlapping cycle bands.
- f_stress must not rise as N rises. Reject an f_stress above 1.

## Maps to CalcDataRecord
Kind is `asme.b31-3.stress-range`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `f_stress_table` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
