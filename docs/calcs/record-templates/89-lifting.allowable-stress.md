# 89 lifting.allowable-stress

**In plain terms.** An allowable-stress lifting design divides the yield and ultimate strengths by set divisors and allows a multiple of the allowable stress in bearing on the pin. This file holds one approach of the code per row.

Kind: `lifting.allowable-stress` (Allowable-stress divisors and bearing multiple).
Status: existing CalcRecordKind; Track S template (DC-10, 2026-10-27). Track P had no file for this kind.

## Used by
- S8 lifting lugs and spreader beams (built, `struct.lifting`, hidden until the lifting flag is on) reads n_y, n_u and k_b.
- The kind is listed only while the lifting flag is on; the importer refuses the file until then.

## Copy from
- Type the values from your licensed copy of the lifting device design code, the clauses on allowable stresses and pin bearing.
- Cite the code, its edition and the clause.

## Columns
- `approach[text]`: the approach or clause set the divisors belong to, as the code names it. Joins the record title.
- `n_y[-]`: divisor on the yield strength in the allowable stress.
- `n_u[-]`: divisor on the ultimate tensile strength in the allowable stress.
- `k_b[-]`: bearing multiple of the allowable stress on the projected pin area.

## Edition to record
Record the code edition and amendment.

## Importer checks
- Reject a blank approach, n_y, n_u or k_b. Reject a non-positive number, or a divisor below 1.
- Reject a duplicate approach.
- Warn when a value is outside the usual range of the S8 input it fills.

## Maps to CalcDataRecord
Kind is `lifting.allowable-stress`. The importer saves one CalcDataRecord per CSV row. Each numeric column becomes one CalcDataValue with its Symbol and Number. The `approach` text joins the record title.
