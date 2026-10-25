# 35 asme.b31-3.stress-range

Kind: none. No CalcRecordKind reads these columns.
Status: superseded. B8 reads the allowable stress range Sa as one value, so the factor f is already inside it. Do not fill this file.
Planned name: `asme.b31-3.stress-range`. The file and its header stay so the numbering holds.

## Used by
- B8 pipe expansion (built, `bs.pipe-expansion`) lists the kinds `pipe.allowable-range`, `pipe.section`, `pipe.screening-k1`, `bellows.data` and `material.properties`. None holds f.

## Symbol mapping
- Apply f when you work out Sa. Enter Sa in template 34.

## Copy from
- Nothing. Skip this file.

## Columns
- `N_min[-]`: lower bound of the equivalent full-range cycles.
- `N_max[-]`: upper bound of the equivalent full-range cycles.
- `f_stress[-]`: stress range reduction factor.

## Edition to record
Not applicable.

## Importer checks
- None. Track S may delete this template.

## Maps to CalcDataRecord
No CalcDataRecord.
