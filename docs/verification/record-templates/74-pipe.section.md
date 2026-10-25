# 74 pipe.section

Kind: `pipe.section` (pipe outside diameter and wall).
Status: existing CalcRecordKind `pipe.section`

## Used by
- B8 pipe expansion (built, `bs.pipe-expansion`) reads the outside diameter and wall for the section properties.

## Symbol mapping
- `OD`, `t_wall` fill the B8 inputs of the same symbol.
- `pipe` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.
- B8 ships no pipe table. A link to an upstream calculation can fill the same symbols.

## Copy from
- Type OD and wall from the manufacturer's datasheet or your copy of the pipe standard.
- Cite the datasheet or standard, its edition and the table.

## Columns
- `pipe[text]`: pipe designation, as the datasheet or standard prints it.
- `OD[mm]`: outside diameter.
- `t_wall[mm]`: wall thickness, less any allowance you apply.

## Edition to record
Record the edition and the tolerance class.

## Importer checks
- Reject a blank pipe. Reject a duplicate pipe.
- Reject a blank or non-positive OD or t_wall. Reject t_wall at or above half of OD.
- B8 refuses OD outside 1 to 10000 mm and t_wall outside 0.1 to 1000 mm.
- Warn when OD is outside 10 to 1500 mm or t_wall outside 0.5 to 60 mm.

## Maps to CalcDataRecord
Kind is `pipe.section`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `pipe` text joins the record title. Columns the kind does not list are stored but never applied.
