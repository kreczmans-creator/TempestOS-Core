# 47 device.ratings

Kind: `device.ratings` (preferred ratings of a protective device range).
Status: existing CalcRecordKind `device.ratings`

## Used by
- E4 conductor sizing (built, `elec.conductor-sizing`) reads this record into its `ratings` table input.
- E4 picks the smallest rating at or above the design current. It blocks the sheet when no rating qualifies.

## Symbol mapping
- The whole file becomes one value with Symbol `ratings`. Its rows fill the `ratings` table input.
- The header is the one column symbol of that table: `I_n` in A.

## Copy from
- Type the preferred ratings from the manufacturer datasheet for the device range.
- You may instead type them from the product standard for the device. Cite its title, edition and table.
- Enter one device range per file. Name the range in the title label, such as `Title [type C MCB]`.

## Columns
- `I_n[A]`: rated current of one preferred rating.

## Edition to record
Record the datasheet edition or date, or the product standard edition and amendment.

## Importer checks
- Reject a blank or non-positive I_n. Reject a duplicate I_n.
- Reject more than 60 rows. E4 takes 60 rows at most.
- E4 refuses I_n outside 0.1 to 10000 A.
- Warn when the rows are not in rising order.

## Maps to CalcDataRecord
Kind is `device.ratings`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `ratings` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
