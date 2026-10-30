# 57 surface.resistance

Kind: `surface.resistance` (surface resistances for the condensation check).
Status: existing CalcRecordKind `surface.resistance`
This kind is singular and scalar. Template 48 holds the B4 table kind `surface.resistances`.

## Used by
- B7 Glaser condensation (built, `bs.glaser`) reads Rsi, Rse and Rsi_f.
- B7 ships with defaults for all three. It warns when Rsi_f falls below its conventional value.

## Symbol mapping
- Each header is the symbol of a B7 input. The importer fills the input with the same symbol.
- `case` names the element type or flow direction. The importer stores it and B7 never reads it.
- The importer saves one record per row. Pick the row for your element.

## Copy from
- Type Rsi and Rse from your copy of the surface-resistance standard, for example BS EN ISO 6946.
- Type Rsi_f from your copy of BS EN ISO 13788 for the element type.
- Cite each standard, its edition and the table or clause.

## Columns
- `case[text]`: element type and heat-flow direction, in plain words.
- `Rsi[m2*K/W]`: internal surface resistance for the temperature profile.
- `Rse[m2*K/W]`: external surface resistance.
- `Rsi_f[m2*K/W]`: internal surface resistance for the surface-condensation check.

## Edition to record
Record the edition and amendment of each standard printed on your copy.

## Importer checks
- Reject a blank case. Reject a duplicate case.
- Reject a blank or negative number in a numeric column.
- B7 refuses Rsi, Rse and Rsi_f outside 0.01 to 1 m2*K/W.
- Warn when Rsi or Rse is outside 0.04 to 0.25, or Rsi_f is outside 0.04 to 0.5.

## Maps to CalcDataRecord
Kind is `surface.resistance`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `case` text joins the record title. Columns the kind does not list are stored but never applied.
