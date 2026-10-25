# 21 ltb.moment-factors

Kind: `ec3.mcr-factors` (C1, C2 and k_c for the moment diagram).
Status: existing CalcRecordKind `ec3.mcr-factors`
Planned name: `ltb.moment-factors`. The built calculator names the kind `ec3.mcr-factors`. The file name stays.

## Used by
- S7 lateral-torsional buckling and N+M (built, `struct.ltb-interaction`) lists C_1, C_2 and k_c for this kind.
- S7 derives C_1 from the end-moment ratio when you leave C_1 blank. A record replaces that derivation.

## Symbol mapping
- `C_1[-]`, `C_2[-]` and `k_c[-]` fill the S7 inputs of the same symbol.
- `case` names the loading and end conditions. The importer stores it and S7 never reads it.
- The importer saves one record per row. Pick the row for your member.
- S7 also needs `C_m_y` whenever you enter C_1. The kind does not list it, so type it by hand.

## Copy from
- Type the values from the source you use: NCCI SN003, a published table, or the output of an LTB program.
- Cite the document, its edition and the table number or program version.

## Columns
- `case[text]`: loading and end conditions, in plain words. Include the end-fixity and the load position.
- `C_1[-]`: moment gradient factor C1.
- `C_2[-]`: load-position factor C2. Leave blank if the source gives none.
- `k_c[-]`: correction factor for the f modification in 6.3.2.3(2).

## Edition to record
Record the document number and issue, or the program name and version. Add later corrections in notes.

## Importer checks
- Reject a blank case. Reject a duplicate case.
- Reject a blank or non-positive C_1. S7 refuses C_1 outside 1 to 4.
- S7 refuses C_2 outside 0 to 1.5 and k_c outside 0.5 to 1.
- Warn when C_1 is below 1.

## Maps to CalcDataRecord
Kind is `ec3.mcr-factors`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `case` text joins the record title. Columns the kind does not list are stored but never applied.
