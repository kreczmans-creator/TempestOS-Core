# 90 lifting.limits

**In plain terms.** A lug plate with a large hole for its thickness can dish, and a lifting device is proof loaded at a multiple of its rated load. This file holds one basis per row: the code, or the owner's own procedure.

Kind: `lifting.limits` (Dishing limit and proof-load factor).
Status: existing CalcRecordKind; Track S template (DC-10, 2026-10-27). Track P had no file for this kind.

## Used by
- S8 lifting lugs and spreader beams (built, `struct.lifting`, hidden until the lifting flag is on) reads dish_max and k_proof.
- The kind is listed only while the lifting flag is on; the importer refuses the file until then.

## Copy from
- Type the limit from your licensed copy of the lifting device design code, or from the owner's written procedure.
- Cite the code or the procedure, its edition and the clause.

## Columns
- `basis[text]`: the code or procedure the limit comes from, as it is titled. Joins the record title.
- `dish_max[-]`: dishing-ratio limit on the hole diameter over the plate thickness; blank when the basis gives none.
- `k_proof[-]`: proof-load factor on the rated load; blank when the basis gives none.

## Edition to record
Record the code or procedure edition.

## Importer checks
- Reject a blank basis. Reject a row with neither dish_max nor k_proof.
- Reject a non-positive number.
- Reject a duplicate basis.
- Warn when a value is outside the usual range of the S8 input it fills.

## Maps to CalcDataRecord
Kind is `lifting.limits`. The importer saves one CalcDataRecord per CSV row. Each filled numeric column becomes one CalcDataValue with its Symbol and Number. The `basis` text joins the record title.
