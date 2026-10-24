# 42 room.internal-temperatures

Kind: `room.internal-temperatures` (Internal design temperatures).
Status: proposed — no CalcRecordKind yet; Track S confirms symbols when the calculator is built

## Used by
- B4 U-value and room heat loss (planned) reads the internal design temperature for each room type.

## Copy from
- Type the values from CIBSE Guide A, or from the MCS design guidance table.
- Cite the guide, its edition, and the table number.

## Columns
- `room_type[text]`: room type, as the table prints it.
- `theta_i[degC]`: internal design temperature.

## Edition to record
Record the guide edition and amendment.

## Importer checks
- Reject a blank room_type or theta_i. Reject a duplicate room_type.
- Warn when theta_i is outside 10 to 28 degC.

## Maps to CalcDataRecord
Kind is `room.internal-temperatures`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `room_temp_table` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
