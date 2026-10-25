# 58 climate.humidity-class

Kind: `climate.humidity-class` (internal humidity class by moisture excess).
Status: existing CalcRecordKind `climate.humidity-class`

## Used by
- B7 Glaser condensation (built, `bs.glaser`) reads these inputs when humidity comes from the moisture excess.
- The default humidity mode is constant relative humidity. That mode ignores this record.

## Symbol mapping
- Each header is the symbol of a B7 input. The importer fills the input with the same symbol.
- `class` names the humidity class. The importer stores it and B7 never reads it.
- The importer saves one record per row. Pick the row for your building class.
- B7 holds the moisture excess constant below theta_a and constant above theta_b.

## Copy from
- Type the moisture excess at two outside temperatures from your copy of the humidity-class method in BS EN ISO 13788.
- Type the safety factor dp_f from your design reference. Check its wording against your copy.
- Cite the standard, its edition and the table or figure number.

## Columns
- `class[text]`: humidity class, as your source names it.
- `dv_a[g/m3]`: moisture excess at the cold point.
- `theta_a[degC]`: outside temperature of the cold point.
- `dv_b[g/m3]`: moisture excess at the warm point.
- `theta_b[degC]`: outside temperature of the warm point.
- `dp_f[-]`: safety factor on the vapour pressure difference.

## Edition to record
Record the edition and amendment of the standard printed on your copy.

## Importer checks
- Reject a blank class. Reject a duplicate class.
- Reject a blank dv_a, theta_a, dv_b, theta_b or dp_f. Reject a negative dv_a or dv_b.
- Reject a row where theta_b does not exceed theta_a.
- B7 refuses dv outside 0 to 50 g/m3, theta outside -60 to 60 degC, and dp_f outside 1 to 2.
- Warn when dv is above 20, or dp_f is above 1.5. Warn when dv_b exceeds dv_a.

## Maps to CalcDataRecord
Kind is `climate.humidity-class`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `class` text joins the record title. Columns the kind does not list are stored but never applied.
