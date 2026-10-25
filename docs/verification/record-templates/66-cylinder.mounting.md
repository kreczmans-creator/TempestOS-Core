# 66 cylinder.mounting

Kind: `cylinder.mounting` (effective-length factor of the mounting).
Status: existing CalcRecordKind `cylinder.mounting`

## Used by
- F1 cylinder circuit (built, `fp.cylinder`) multiplies the rod length by K for the buckling check.

## Symbol mapping
- `K_m` fill the F1 input of the same symbol.
- `mounting` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.
- F1 defaults to 1 (pinned both ends). That is not safe for a fixed-free mounting, where K is 2.

## Copy from
- Type K from your design reference or the cylinder maker for each mounting.
- Cite the reference, its edition and the table.

## Columns
- `mounting[text]`: how the cylinder body and rod end are held.
- `K_m[-]`: effective-length factor of the mounting.

## Edition to record
Record the reference edition.

## Importer checks
- Reject a blank mounting. Reject a duplicate mounting.
- Reject a blank K_m. F1 refuses K_m outside 0.5 to 2.5.

## Maps to CalcDataRecord
Kind is `cylinder.mounting`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `mounting` text joins the record title. Columns the kind does not list are stored but never applied.
