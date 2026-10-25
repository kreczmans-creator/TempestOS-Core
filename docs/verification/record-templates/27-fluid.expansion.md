# 27 fluid.expansion

Kind: `fluid.expansion` (Heat-transfer fluid expansion).
Status: existing CalcRecordKind

## Used by
- B6 sealed system (built) reads e_fluid to size the expansion vessel.

## Copy from
- Type the value from the glycol supplier's data between the fill and maximum temperatures.
- Cite the supplier, the data sheet edition and the table number.

## Columns
- `fluid[text]`: fluid name.
- `concentration[%]`: glycol concentration by volume.
- `t_fill[degC]`: fill temperature the value starts from.
- `t_max[degC]`: maximum system temperature the value ends at.
- `e_fluid[-]`: volume expansion coefficient between t_fill and t_max.

## Edition to record
Record the catalogue edition or year, the page or table number, and the catalogue download date.

## Importer checks
- Reject a blank e_fluid, t_fill or t_max. Reject a non-positive e_fluid.
- Reject a row where t_max does not exceed t_fill.
- Warn when e_fluid is outside 0.001 to 0.2.
- Warn when concentration is outside 0 to 60 percent.

## Maps to CalcDataRecord
Kind is `fluid.expansion`. The importer saves one CalcDataRecord per CSV row.
e_fluid becomes the applied Value. t_fill and t_max are stored as extra Values and never applied.
The fluid and concentration join the record title.
