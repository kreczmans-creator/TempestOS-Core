# 56 glaser.layers

Kind: `material.conductivity` (layer conductivity and vapour resistance for B7).
Status: existing CalcRecordKind `material.conductivity`
Planned name: none. B7 lists the same kind as B4 with a wider `layers` table, so it gets its own file.

## Used by
- B7 Glaser condensation (built, `bs.glaser`) reads the `layers` table: thickness, conductivity and vapour resistance.
- Template 49 holds the narrower B4 version of the same table.

## Symbol mapping
- The whole file becomes one value with Symbol `layers`. Its rows fill the `layers` table input.
- Each header is a column symbol of that table. Enter the layers from the inside outwards.
- Applying the record replaces the table rows as it stands.
- B7 reads `element` to pick the rows of one wall. Leave it blank when the file holds one wall.

## Copy from
- Type thickness d from your drawing or build-up. Type lambda from the manufacturer's declared value or your licensed reference.
- Type mu from the manufacturer's data or your copy of the material standard.
- Cite each source, its edition and the table. Name the layer in `name` as your drawing does.

## Columns
- `element[text]`: wall or element the layer belongs to. Optional.
- `name[text]`: layer name, inside first. Optional.
- `d[mm]`: layer thickness.
- `lambda[W/(m*K)]`: thermal conductivity of the layer material.
- `mu[-]`: vapour resistance factor of the layer material.
- `timber[-]`: type 1 for a timber or breathable layer and 0 for any other layer. Leave blank if unsure.

## Edition to record
Record the data sheet date or the reference edition and amendment.

## Importer checks
- Reject a blank, zero or negative d, lambda or mu.
- B7 refuses d outside 0.01 to 2000 mm, lambda outside 0.005 to 10 W/(m*K), and mu below 1.
- Warn when d is outside 1 to 500 mm, lambda outside 0.01 to 5, or mu above 1e5.
- Reject any timber value other than 0 or 1.
- Reject more than 30 rows. B7 takes 30 layers at most.

## Maps to CalcDataRecord
Kind is `material.conductivity`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `layers` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
