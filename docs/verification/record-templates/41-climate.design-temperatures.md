# 41 climate.design-temperatures

Kind: `climate.design-temperature` (external design temperature, singular).
Status: existing CalcRecordKind `climate.design-temperature`
Planned name: `climate.design-temperatures`. The built calculator names the kind `climate.design-temperature`. The file name stays.

## Used by
- B4 U-value and room heat loss (built, `bs.room-heat-loss`) reads `theta_e` as the external design temperature.

## Symbol mapping
- `theta_e[degC]` fills the B4 input `theta_e`, a quantity in degC.
- `location` identifies the row. The importer stores it but B4 never reads it.
- The importer saves one record per row. Pick the row for your site.
- The planned column `theta_m` is gone. B4 does not read it. Template 45 covers monthly data for B7.

## Copy from
- Type the values from CIBSE Guide A, or from the MCS design guidance table for your location.
- Cite the guide, its edition, and the table number.

## Columns
- `location[text]`: location, as the table prints it.
- `theta_e[degC]`: external design temperature.

## Edition to record
Record the guide edition and amendment. Note the weather data period in notes.

## Importer checks
- Reject a blank theta_e. Reject a duplicate location.
- B4 refuses theta_e outside -15 to 5 degC.

## Maps to CalcDataRecord
Kind is `climate.design-temperature`. The importer saves one CalcDataRecord per CSV row.
The `theta_e` column becomes one CalcDataValue with Symbol `theta_e`, a Number and the Unit `degC`.
The `location` text joins the record title. Columns the kind does not list are stored but never applied.
