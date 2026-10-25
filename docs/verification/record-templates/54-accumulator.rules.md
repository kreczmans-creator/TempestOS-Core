# 54 accumulator.rules

Kind: `accumulator.rules` (pre-charge limits for the accumulator type and make).
Status: existing CalcRecordKind `accumulator.rules`

## Used by
- F2 accumulator sizing (built, `fp.accumulator`) applies both ratios to absolute pressures.
- F2 ships with default ratios. A record replaces them and cites the manufacturer.

## Symbol mapping
- `r_0[-]` and `r_p[-]` fill the F2 inputs of the same symbol.
- `type` and `basis` identify the row. The importer stores them and F2 never reads them.
- The importer saves one record per row. Pick the row for your accumulator type and make.
- F2 reads both ratios as absolute-pressure ratios. Convert gauge ratios by hand, or record the basis and check.

## Copy from
- Type the limits from the manufacturer's selection guide for your accumulator type: bladder, diaphragm or piston.
- r_0 is the largest p0/p1, with p0 at the maximum operating temperature.
- r_p is the largest p2/p0, with p0 at the minimum operating temperature.
- Cite the guide, its edition or date, and the page or table.

## Columns
- `type[text]`: accumulator type and make, as the guide names them.
- `basis[text]`: gauge or absolute. State the pressure basis the manufacturer's ratio uses.
- `r_0[-]`: largest pre-charge pressure as a fraction of the lowest working pressure p1.
- `r_p[-]`: largest ratio of the highest working pressure p2 to the pre-charge pressure p0.

## Edition to record
Record the guide edition or date. Put the pressure basis in notes as well.

## Importer checks
- Reject a blank type or basis. Reject a duplicate type and basis row.
- Reject a blank or non-positive r_0 or r_p.
- F2 refuses r_0 outside 0.1 to 1 and r_p outside 1 to 20.
- Warn when basis is gauge. The same numbers read as absolute give a different ratio.

## Maps to CalcDataRecord
Kind is `accumulator.rules`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `type` and `basis` text join the record title. Columns the kind does not list are stored but never applied.
