# 52 spring.seating

Kind: `spring.seating` (end-support coefficient for spring buckling).
Status: existing CalcRecordKind `spring.seating`

## Used by
- M7 springs (built, `mech.spring`) reads `nu_seat` for the buckling check of a compression spring.

## Symbol mapping
- `nu_seat[-]` fills the M7 input `nu_seat`.
- `support` names the end condition. The importer stores it and M7 never reads it.
- The importer saves one record per row. Pick the row for the way your spring ends are held.

## Copy from
- Type the coefficient for each end condition from your design reference or the spring standard.
- Cite the reference, its edition and the table or figure number.

## Columns
- `support[text]`: how the ends are held, in plain words. Say which ends are fixed, pinned or free.
- `nu_seat[-]`: end-support coefficient for that condition.

## Edition to record
Record the reference edition and amendment.

## Importer checks
- Reject a blank support. Reject a duplicate support.
- Reject a blank or non-positive nu_seat.
- M7 refuses nu_seat outside 0.25 to 5. Warn outside 0.5 to 2.

## Maps to CalcDataRecord
Kind is `spring.seating`. The importer saves one CalcDataRecord per CSV row.
The `nu_seat` column becomes one CalcDataValue with Symbol `nu_seat`, a Number and the Unit `-`.
The `support` text joins the record title. Columns the kind does not list are stored but never applied.
