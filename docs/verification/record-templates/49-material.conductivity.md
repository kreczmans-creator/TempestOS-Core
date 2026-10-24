# 49 material.conductivity

Kind: `material.conductivity` (thermal conductivity of construction layers).
Status: existing CalcRecordKind `material.conductivity`

## Used by
- B4 U-value and room heat loss (built, `bs.room-heat-loss`) reads the `lambda` column of its `layers` table.

## Symbol mapping
- The whole file becomes one value with Symbol `layers`. Its rows fill the `layers` table input.
- `lambda[W/(m*K)]` is the column the kind describes. `name` is the optional layer name column of that table.
- The table also needs `element` and `d` on every row. This template has no such columns.
- Applying the record replaces the table rows as it stands. Track S must merge by row or the user must retype the other columns.

## Copy from
- Type the declared value from the manufacturer's data sheet for each layer material.
- You may instead type the value from your own licensed reference. Cite its title, edition and table.
- Name the material in `name` as your drawing names the layer.

## Columns
- `name[text]`: layer material name.
- `lambda[W/(m*K)]`: thermal conductivity of the material.

## Edition to record
Record the data sheet date or the reference edition and amendment.

## Importer checks
- Reject a blank name. Reject a duplicate name.
- Reject a blank or non-positive lambda.
- B4 refuses lambda outside 0.01 to 5 W/(m*K).
- Reject more than 200 rows. B4 takes 200 layer rows at most.

## Maps to CalcDataRecord
Kind is `material.conductivity`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `layers` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
