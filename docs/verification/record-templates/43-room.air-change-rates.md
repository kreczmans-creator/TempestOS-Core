# 43 room.air-change-rates

Kind: `room.air-change-rates` (Air change rates).
Status: proposed — B4 is built but lists no CalcRecordKind for this; Track S decides whether to add one

## Used by
- B4 U-value and room heat loss (built) takes this value as a typed input. It lists no record kind for it, so the importer cannot apply this file yet.

## Copy from
- Type the values from CIBSE Guide A, the MCS design guidance, or BS EN 12831-1 and its National Annex.
- Cite the document, its edition, and the table number.

## Columns
- `room_type[text]`: room type, as the table prints it.
- `n_ach[1/h]`: air changes per hour.

## Edition to record
Record the document edition and amendment.

## Importer checks
- Reject a blank room_type or n_ach. Reject a negative n_ach.
- Reject a duplicate room_type.
- Warn when n_ach is above 10 per hour.

## Maps to CalcDataRecord
Kind is `room.air-change-rates`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `ach_table` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
