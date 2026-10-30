# 63 material.temperature-tables

Kind: `material.properties` (W3 material library: a property against temperature).
Status: existing CalcRecordKind `material.properties`. The W3 material library owns this kind.

## Used by
- S7 LTB (built) and M7 springs (built) read a material at a stated temperature when you ask for one.
- The library interpolates between the rows. It reads a single value when no temperature is asked for.

## Symbol mapping
- Each property and material pair becomes one value with the Symbol `<property>(T)`, such as `E(T)`.
- `property` is one of E, G, nu, rho, alpha, lambda, f_y or f_u. The library refuses any other property.
- Each row becomes one table row with the columns `T` and `v`.
- The same material row in template 62 may hold a single value of the same property.

## Copy from
- Type the values from the material standard, the supplier's data sheet or your test certificate.
- Cite the source, its edition and the table or page.

## Columns
- `material[text]`: material name. It must match a material row in template 62.
- `property[text]`: the property symbol, without the `(T)` suffix.
- `T[degC]`: temperature of the row. Use degC, K or degF, and change the header unit to match.
- `v[-]`: the property value as a bare number. The unit sits in `v_unit`.
- `v_unit[text]`: unit of v, such as GPa or MPa. Leave blank for nu.

## Edition to record
Record the source edition and the table or page. The provenance row covers every value of the material.

## Importer checks
- Reject a blank material, property, T or v. Reject a property outside the list above.
- Reject fewer than two rows for one material and property.
- Reject temperatures that do not rise strictly from row to row. Reject a temperature at or below absolute zero.
- Reject a v_unit that does not measure the property. Reject a missing v_unit on a dimensional property.
- Apply the same range limits as template 62 to v.

## Maps to CalcDataRecord
Kind is `material.properties`. The importer adds one CalcDataValue to the material for each material and property pair.
That value has Symbol `<property>(T)` and Rows with the columns `T` and `v`. Each cell keeps its unit.
