# 65 material.friction-pairs

Kind: `material.properties` (W3 material library: friction coefficient by material pair).
Status: existing CalcRecordKind `material.properties`. The W3 material library owns this kind.

## Used by
- No built calculator maps an input to this property yet.
- Template 51 (`joint.friction`) still feeds S6. This property does not replace it.

## Symbol mapping
- Each material becomes one value with the Symbol `mu_pairs`. Its rows hold the pairs.
- Each row has the columns `pair` and `mu`.

## Copy from
- Type the coefficients from the material standard, the supplier's data sheet or your test report.
- Cite the source, its edition and the table. State the surface condition in the `pair` text.

## Columns
- `material[text]`: material name. It must match a material row in template 62.
- `pair[text]`: the second material and the surface condition, in plain words, such as dry or lubricated.
- `mu[-]`: friction coefficient for the pair.

## Edition to record
Record the source edition and the table. The provenance row covers every pair.

## Importer checks
- Reject a blank material, pair or mu. Reject a duplicate pair for one material.
- Reject a negative mu. Reject a mu above 5.
- Warn when mu is above 1.

## Maps to CalcDataRecord
Kind is `material.properties`. The importer adds one CalcDataValue with Symbol `mu_pairs` to the material.
That value has Rows with the columns `pair` and `mu`.
