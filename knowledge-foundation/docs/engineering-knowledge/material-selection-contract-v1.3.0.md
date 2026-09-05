# Engineering Knowledge Foundation v1.3.0

## Deterministic material-selection contract

The selection layer is now specified independently of TempestOS production implementation.

### Core rule

**Unknown is not pass.**

A candidate only satisfies a hard constraint when the knowledge base contains an applicable observation for the candidate's material state. Missing evidence is returned as `evidence_required`.

### Query structure

A query consists of:
- hard `constraints`;
- ordered soft `ranking` factors;
- optional `scope`;
- explicit `exclusions`;
- optional evidence policy.

### Result structure

Every result exposes:
- query ID/status;
- candidate material/state;
- eligibility;
- matched constraints;
- ranking factors;
- evidence state;
- warnings;
- evidence gaps and exclusions.

### Why this matters

This gives TempestOS a deterministic contract that can later be implemented in the domain/service layer while keeping engineering knowledge separate from selection algorithms.

The reference engine included in this release is **specification support, not production code**. It intentionally refuses to infer temperature or environment suitability from generic room-temperature data.
