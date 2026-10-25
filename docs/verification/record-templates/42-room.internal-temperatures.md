# 42 room.internal-temperatures

Kind: `room.internal-temperatures` (Internal design temperatures).
Status: proposed — B4 is built but lists no CalcRecordKind for this; Track S decides whether to add one

## Used by
- B4 U-value and room heat loss (built) takes this value as a typed input. It lists no record kind for it, so the importer cannot apply this file yet.

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
