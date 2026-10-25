# 75 pipe.screening-k1

Kind: `pipe.screening-k1` (screening limit K1).
Status: existing CalcRecordKind `pipe.screening-k1`

## Used by
- B8 pipe expansion (built, `bs.pipe-expansion`) compares OD*Y/(L-U)^2 with K1 in the screening check.

## Symbol mapping
- `K_scr` fill the B8 input of the same symbol.
- `code` identifies the row. The importer stores it and the calculator never reads it.
- The importer saves one record per row. Pick the row that matches your case.
- Without K1 the sheet shows the screening ratio and does not judge it. The sheet does not pass.
- Convert in^2/ft^2 to mm^2/m^2 by hand. Multiply by 6944.4.

## Copy from
- Type K1 from your piping code and its formula.
- Cite the code, its edition and the clause.

## Columns
- `code[text]`: piping code, edition and clause that give K1.
- `K_scr[mm2/m2]`: screening limit K1, in mm^2/m^2.

## Edition to record
Record the code edition and addenda.

## Importer checks
- Reject a blank code. Reject a duplicate code.
- Reject a blank or non-positive K_scr. B8 refuses K_scr outside 1e-6 to 1e9.
- Warn when K_scr is outside 1 to 1000.

## Maps to CalcDataRecord
Kind is `pipe.screening-k1`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `code` text joins the record title. Columns the kind does not list are stored but never applied.
