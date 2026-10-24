# 45 climate.monthly

Kind: `climate.monthly` (Monthly external climate).
Status: proposed — no CalcRecordKind yet; Track S confirms symbols when the calculator is built

## Used by
- B7 Glaser condensation (planned) reads monthly external temperature and humidity.

## Copy from
- Type the values from the climate data you hold a licence for, for example CIBSE weather data.
- The method is BS EN ISO 13788. Cite the data set, its edition, and the table number.

## Columns
- `month[-]`: month number, 1 to 12.
- `theta_e[degC]`: monthly mean external temperature.
- `phi_e[%]`: monthly mean external relative humidity.

## Edition to record
Record the data set name, its edition and the location. Note the ISO 13788 edition you follow.

## Importer checks
- Reject a blank month. Reject a month outside 1 to 12 or a duplicate month.
- Reject a phi_e outside 0 to 100 percent.
- Warn when theta_e is outside -30 to 40 degC.
- Reject a file without exactly 12 rows.

## Maps to CalcDataRecord
Kind is `climate.monthly`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `climate_table` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
