# Engineering Knowledge Foundation v1.0.0

## Provenance, designation mapping and validation

This release establishes the first operational trust model for the material database.

### Provenance
Sources are classified as primary standards sources or secondary engineering references. Standard metadata is verified separately from material property observations.

### Designation mappings
Mappings are explicit relationships with confidence and warnings. Legacy names remain searchable without becoming automatic equivalents. Alloy identity and material condition remain separate; for example, 6082-T6 and 6082-T651 are not merged.

### Validation
Initial rules cover units, condition context, temperature, provenance, equivalence, conflicting observations, polymer conditioning and metal product form.

## Traceability target

`material → condition → property observation → source → provenance → engineering selection`

All current numeric catalogue values remain `candidate_reference`.
