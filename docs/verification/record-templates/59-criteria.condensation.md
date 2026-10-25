# 59 criteria.condensation

Kind: `criteria.condensation` (critical surface humidity and condensate limit).
Status: existing CalcRecordKind `criteria.condensation`

## Used by
- B7 Glaser condensation (built, `bs.glaser`) reads RH_crit and Mc_limit.
- B7 ships with a default RH_crit. Mc_limit is optional. Blank means B7 reports the condensate and does not judge it.

## Symbol mapping
- Each header is the symbol of a B7 input. The importer fills the input with the same symbol.
- `basis` names the materials or the rule behind the limits. The importer stores it and B7 never reads it.
- The importer saves one record per row. Pick the row for the materials in your build-up.

## Copy from
- Type the limits from your own source for the materials used. This can be a standard, a manufacturer's guidance or a design brief.
- Cite the source, its edition and the table or clause.

## Columns
- `basis[text]`: materials or rule the limits apply to, in plain words.
- `RH_crit[%]`: critical relative humidity at the surface.
- `Mc_limit[g/m2]`: largest condensate allowed on any interface. Leave blank to report only.

## Edition to record
Record the source edition and amendment, or the guidance date.

## Importer checks
- Reject a blank basis. Reject a duplicate basis.
- Reject a blank or non-positive RH_crit. Reject an RH_crit above 100.
- B7 refuses RH_crit outside 30 to 100 percent. Warn outside 50 to 100 percent.
- B7 refuses Mc_limit outside 0.001 to 1e6 g/m2 when filled. Warn outside 1 to 5000 g/m2.

## Maps to CalcDataRecord
Kind is `criteria.condensation`. The importer saves one CalcDataRecord per CSV row.
Each numeric column becomes one CalcDataValue with its Symbol, Number and Unit.
The `basis` text joins the record title. Columns the kind does not list are stored but never applied.
