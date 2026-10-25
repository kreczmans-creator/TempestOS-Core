# 36 asme.b31-3.thermal

Kind: none. No CalcRecordKind reads these columns.
Status: superseded. B8 takes E and alpha from the W3 material library, not from its own kind. Do not fill this file.
Planned name: `asme.b31-3.thermal`. The file and its header stay so the numbering holds.

## Used by
- B8 pipe expansion (built, `bs.pipe-expansion`) takes `E` and `alpha` from a material. Use templates 62 and 63.
- The sheet wants the mean alpha between the install and the operating temperature. E is the modulus at the install temperature.

## Symbol mapping
- Enter the mean alpha and E on a material in template 62. Use template 63 for the temperature tables.

## Copy from
- Nothing. Skip this file.

## Columns
- `material[text]`: material group, as the table prints it.
- `t_metal[degC]`: metal temperature.
- `e_expansion[mm/m]`: total linear thermal expansion from the reference temperature.
- `E_mod[GPa]`: modulus of elasticity at that temperature.

## Edition to record
Not applicable.

## Importer checks
- None. Track S may delete this template.

## Maps to CalcDataRecord
No CalcDataRecord.
