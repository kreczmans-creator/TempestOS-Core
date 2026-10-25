# 51 joint.friction

Kind: `joint.friction` (friction coefficient between base plate and grout).
Status: existing CalcRecordKind `joint.friction`

## Used by
- S6 base plate (built, `struct.base-plate`) reads `C_fd` for the shear resistance by friction.

## Symbol mapping
- `C_fd[-]` fills the S6 input `C_fd`, a number.
- `grout` identifies the row. The importer stores it but S6 never reads it.
- The importer saves one record per row. Pick the row for the grout you use.

## Copy from
- Type the value for your grout from EN 1993-1-8, clause 6.2.2, or from the grout manufacturer.
- Cite the source, its edition and the clause or data sheet.

## Columns
- `grout[text]`: grout type or product, as your source names it.
- `C_fd[-]`: friction coefficient between plate and grout.

## Edition to record
Record the standard edition and amendment, or the data sheet date.

## Importer checks
- Reject a blank grout. Reject a duplicate grout.
- Reject a blank or negative C_fd. Zero is allowed.
- S6 refuses C_fd outside 0 to 0.45.

## Maps to CalcDataRecord
Kind is `joint.friction`. The importer saves one CalcDataRecord per CSV row.
The `C_fd` column becomes one CalcDataValue with Symbol `C_fd`, a Number and the Unit `-`.
The `grout` text joins the record title. Columns the kind does not list are stored but never applied.
