# 29 psu.derating

Kind: `psu.derating` (PSU derating).
Status: existing CalcRecordKind

## Used by
- E6 load budget (built) reads f_derate for the PSU rating.

## Copy from
- Type the factor from the PSU manufacturer's derating curve at the ambient and altitude.
- Cite the manufacturer, the data sheet edition and the figure number.

## Columns
- `psu[text]`: PSU model, as the data sheet prints it.
- `theta[degC]`: ambient temperature the factor applies to.
- `altitude[m]`: altitude the factor applies to. Leave blank if the data sheet gives none.
- `f_derate[-]`: derating factor.

## Edition to record
Record the catalogue edition or year, the page or table number, and the catalogue download date.

## Importer checks
- Reject a blank theta or a blank or non-positive f_derate. Altitude may be blank or zero.
- Reject an f_derate above 1. Warn when it is below 0.1.
- For one psu and altitude, f_derate must not rise as theta rises.
- Warn when theta is outside -20 to 70 degC.

## Maps to CalcDataRecord
Kind is `psu.derating`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `psu` text joins the record title. Columns the kind does not list are stored but never applied.
