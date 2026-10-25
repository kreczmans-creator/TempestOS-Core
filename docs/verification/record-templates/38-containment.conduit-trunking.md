# 38 containment.conduit-trunking

Kind: `containment.space-factor` (fill limit of conduit, trunking or tray).
Status: existing CalcRecordKind `containment.space-factor`
Planned name: `containment.conduit-trunking`. The built calculator names the kind `containment.space-factor`. The file name stays.

## Used by
- E8 containment (built, `elec.containment`) compares the cable fill with this limit.

## Symbol mapping
- `f_max` fill the E8 input of the same symbol.
- `arrangement` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.
- E8 ships no space factor and no default. The sheet blocks until you enter `f_max`.
- Write 0.45 for 45 %.

## Copy from
- Type the fill limit from your copy of the installation standard or your company rule.
- Cite the standard, its edition and the clause or table.

## Columns
- `arrangement[text]`: containment type and cable arrangement the limit applies to.
- `f_max[-]`: largest permitted fill as a fraction of the containment area, between 0 and 1.

## Edition to record
Record the standard edition and amendment.

## Importer checks
- Reject a blank arrangement. Reject a duplicate arrangement row.
- Reject a blank f_max. E8 refuses f_max outside 0.01 to 1.
- Warn when f_max exceeds 0.6.

## Maps to CalcDataRecord
Kind is `containment.space-factor`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `arrangement` text joins the record title. Columns the kind does not list are stored but never applied.
