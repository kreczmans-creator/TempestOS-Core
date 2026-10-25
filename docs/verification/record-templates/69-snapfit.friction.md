# 69 snapfit.friction

Kind: `snapfit.friction` (coefficient of friction of the joint pair).
Status: existing CalcRecordKind `snapfit.friction`

## Used by
- M8 snap-fits (built, `mech.snap-fit`) uses mu for the assembly and separation forces.

## Symbol mapping
- `mu` fill the M8 input of the same symbol.
- `pair` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.
- A W3 material can hold friction pairs (template 65). Read mu there and enter it here.

## Copy from
- Type mu from your reference, the plastic maker or a test.
- Cite the source, its edition and the table.

## Columns
- `pair[text]`: material and surface pair, with the state (static or kinetic).
- `mu[-]`: coefficient of friction.

## Edition to record
Record the source edition or test report number.

## Importer checks
- Reject a blank pair. Reject a duplicate pair.
- Reject a negative mu. M8 refuses mu outside 0 to 2.
- Warn when mu is outside 0.05 to 1.

## Maps to CalcDataRecord
Kind is `snapfit.friction`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `pair` text joins the record title. Columns the kind does not list are stored but never applied.
