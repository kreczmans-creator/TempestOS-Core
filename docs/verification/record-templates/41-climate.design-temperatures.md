# 41 climate.design-temperatures

Kind: `climate.design-temperatures` (External design temperatures).
Status: proposed — no CalcRecordKind yet; Track S confirms symbols when the calculator is built

## Used by
- B4 U-value and room heat loss (planned) reads the external design temperature for the site.

## Copy from
- Type the values from CIBSE Guide A, or from the MCS design guidance table for your location.
- Cite the guide, its edition, and the table number.

## Columns
- `location[text]`: location, as the table prints it.
- `theta_e[degC]`: external design temperature.
- `theta_m[degC]`: annual mean external temperature. Blank if not given.

## Edition to record
Record the guide edition and amendment. Note the weather data period in notes.

## Importer checks
- Reject a blank theta_e. Reject a duplicate location.
- Warn when theta_e is outside -20 to 10 degC.
- Warn when theta_m is above 20 degC.

## Maps to CalcDataRecord
Kind is `climate.design-temperatures`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `design_temp_table` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
