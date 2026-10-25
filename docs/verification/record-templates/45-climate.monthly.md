# 45 climate.monthly

Kind: `climate.monthly` (monthly outside climate).
Status: existing CalcRecordKind `climate.monthly`

## Used by
- B7 Glaser condensation (built, `bs.glaser`) reads the twelve monthly outside conditions.
- No climate data ships with the calculator. You supply all twelve rows.

## Symbol mapping
- The whole file becomes one value with Symbol `climate`. Its rows fill the `climate` table input.
- Each header is a column symbol of that table. B7 needs all twelve rows.
- Enter the rows in the order the year runs, January to December.

## Copy from
- Type the values from the climate data you hold a licence for, for example CIBSE weather data.
- The method is BS EN ISO 13788. Cite the data set, its edition, the location and the table number.

## Columns
- `month[text]`: month name or label. Optional for the calculator. Keep it to check the order.
- `theta_e[degC]`: monthly mean outside temperature.
- `RH_e[%]`: monthly mean outside relative humidity.
- `days[d]`: days in the month. Leave blank to use calendar days.

## Edition to record
Record the data set name, its edition and the location. Note the ISO 13788 edition you follow.

## Importer checks
- Reject a file without exactly 12 rows.
- Reject a blank theta_e or RH_e. Reject an RH_e outside 0 to 100 percent.
- B7 refuses theta_e outside -60 to 60 degC. Warn outside -30 to 40 degC.
- Warn when RH_e is below 20 percent.
- Reject a days value outside 1 to 366 when filled. Warn outside 28 to 31.
- Reject a duplicate month label.

## Maps to CalcDataRecord
Kind is `climate.monthly`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `climate` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
