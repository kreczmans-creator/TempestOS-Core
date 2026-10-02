# Material Identity and Property Model

## Core separation

TempestOS should distinguish:

`Material → MaterialCondition → MaterialPropertyObservation`

A **Material** represents stable identity/designation. A **MaterialCondition** captures context such as temper, heat treatment, product form, thickness, orientation, processing and environmental conditioning. A **MaterialPropertyObservation** records a source-backed engineering value.

### Property observation

Each observation should preserve:

- property definition
- value and unit
- temperature where relevant
- material condition
- product form/thickness where relevant
- orientation where relevant
- test method where relevant
- source
- provenance
- verification/authority state

This prevents a single material record from mixing incompatible values.

## Data maturity

`unresolved → candidate_reference → source_verified → calculation_authoritative`

Promotion requires evidence. The existing seed values remain reference data until their sources and applicability are verified.

## Selection metadata

Materials may also carry qualitative:

- cost tier: very_cheap / cheap / medium / medium_expensive / expensive / very_expensive
- availability: very_high / high / medium / low
- general uses
- manufacturing suitability
- environmental suitability

These are screening metadata, not design approval or live commercial quotations.

## Important engineering behaviour

Temperature dependence, moisture conditioning, heat treatment, product form and direction must be represented where they materially affect a property. This is particularly important for polymers and anisotropic/processed materials.
