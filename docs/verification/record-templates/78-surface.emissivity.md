# 78 surface.emissivity

Kind: `surface.emissivity` (surface emissivity).
Status: existing CalcRecordKind `surface.emissivity`

## Used by
- B9 insulation (built, `bs.insulation`) uses eps for the radiation part of h_se.

## Symbol mapping
- `eps` fill the B9 input of the same symbol.
- `finish` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.

## Copy from
- Type eps from the maker's data or your reference.
- Cite the source and its edition.

## Columns
- `finish[text]`: jacketing or finish, as the maker names it.
- `eps[-]`: total emissivity of the finish.

## Edition to record
Record the source edition.

## Importer checks
- Reject a blank finish. Reject a duplicate finish.
- Reject a blank eps. B9 refuses eps outside 0.01 to 1.
- Warn when eps is below 0.05.

## Maps to CalcDataRecord
Kind is `surface.emissivity`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `finish` text joins the record title. Columns the kind does not list are stored but never applied.
