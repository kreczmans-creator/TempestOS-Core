# 26 bearing.aiso

Kind: `bearing.aiso` (Bearing a_ISO).
Status: existing CalcRecordKind

## Used by
- M5 bearing life (built) reads a_iso and kappa.

## Copy from
- Type the values from the manufacturer's life tool for the lubricant, contamination and kappa.
- Cite the manufacturer, the tool name and version, and the date of the run.

## Columns
- `designation[text]`: bearing designation the run applies to.
- `contamination[text]`: contamination level, as the tool names it.
- `kappa[-]`: viscosity ratio.
- `a_iso[-]`: life modification factor a_ISO.

## Edition to record
Record the tool version and run date. Add the load case in notes.

## Importer checks
- Reject any row with a blank or non-positive number in a numeric column.
- Warn when a_iso is outside 0.1 to 50.
- Warn when kappa is outside 0.1 to 4.
- Reject a row where the designation is blank.

## Maps to CalcDataRecord
Kind is `bearing.aiso`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `designation` text joins the record title. Columns the kind does not list are stored but never applied.
