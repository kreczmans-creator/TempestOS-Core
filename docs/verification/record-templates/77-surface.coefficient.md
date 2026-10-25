# 77 surface.coefficient

Kind: `surface.coefficient` (outer surface coefficient).
Status: existing CalcRecordKind `surface.coefficient`

## Used by
- B9 insulation (built, `bs.insulation`) uses h_se for the outer surface film.

## Symbol mapping
- `h_se` fill the B9 input of the same symbol.
- `surface` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.
- B9 can compute h_se from its correlations. A record replaces the entered value.

## Copy from
- Type h_se from your copy of the standard or the maker's guidance for the surface.
- Cite the source, its edition and the table.

## Columns
- `surface[text]`: surface condition, such as still air or moving air, and the jacketing.
- `h_se[W/(m2*K)]`: outer surface heat transfer coefficient.

## Edition to record
Record the source edition.

## Importer checks
- Reject a blank surface. Reject a duplicate surface.
- Reject a blank or non-positive h_se. B9 refuses h_se outside 0.5 to 200.
- Warn when h_se is outside 3 to 40.

## Maps to CalcDataRecord
Kind is `surface.coefficient`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `surface` text joins the record title. Columns the kind does not list are stored but never applied.
