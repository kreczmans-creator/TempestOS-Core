# 33 oring.extrusion-limits

Kind: `oring.extrusion` (permitted extrusion gap against hardness and pressure).
Status: existing CalcRecordKind `oring.extrusion`
Planned name: `oring.extrusion-limits`. The built calculator names the kind `oring.extrusion`. The file name stays.
One file holds one compound. Name the compound in the title label.

## Used by
- F3 O-ring gland (built, `fp.oring-gland`) interpolates the permitted gap against hardness and pressure.

## Symbol mapping
- The whole file becomes one value with Symbol `ext`. Its rows fill the `ext` table input.
- The header holds the column symbols of that table and nothing else.
- The single value `gap_allow` of the same kind has no template. Type it on the sheet if you have one value.

## Copy from
- Type points read from the seal maker's extrusion chart for the compound, for example in the Parker O-Ring Handbook.
- Cite the handbook edition and the figure number.
- Read the chart for the case without a back-up ring. Type a separate file for a fitted back-up ring and name it in the title label.

## Columns
- `H[-]`: seal hardness in Shore A that the curve applies to.
- `p[bar]`: pressure point on the curve.
- `gap[mm]`: largest permitted diametral gap at that pressure.

## Edition to record
Record the handbook edition and the temperature the chart assumes.

## Importer checks
- Reject a blank or negative number. Reject a duplicate H and p row.
- Within one hardness, p must rise and gap must not rise.
- F3 refuses H outside 20 to 100, p outside 0 to 2000 bar and gap outside 0 to 5 mm.
- F3 takes 100 rows at most.
- Warn when H is outside 40 to 95.

## Maps to CalcDataRecord
Kind is `oring.extrusion`. The importer saves one CalcDataRecord for the file.
One CalcDataValue with Symbol `ext` holds Rows. Each CalcRow has one cell per column, keyed by column symbol.
