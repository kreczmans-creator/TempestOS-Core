# Engineering Knowledge Foundation v0.5.0

## Population pass

The material knowledge layer now includes a first separate thermal/electrical observation layer for representative materials.

### Property groups now represented

**Mechanical:** density, Young's modulus, Poisson's ratio, yield strength, UTS, elongation, plus vocabulary for shear/bulk modulus, compression, hardness, fatigue and fracture toughness.

**Thermal:** coefficient of thermal expansion, thermal conductivity, specific heat, melting range, glass transition, heat-deflection temperature and service temperature.

**Electrical:** resistivity, conductivity, dielectric constant and dielectric strength.

**Selection:** qualitative cost tier, availability and general uses.

## Data governance

All newly added observations are `candidate_reference`. They are not calculation-authoritative.

The repository must preserve the distinction between:
- a source-published observation;
- a normalised/unit-converted observation;
- a selected engineering input.

No averaging of conflicting sources is permitted without an explicit engineering rationale.

## Standards governance

The standards catalogue stores metadata and navigation references, not copyrighted standards text. ISO's current catalogue confirms, among others, ISO 1101:2017, ISO 6892-1:2019, ISO 404:2013 (with Amendment 1:2022), ISO 683-1:2016, ISO 683-2:2016, ISO 6361-1:2011 and ISO 10012:2026 as published/current references. ISO 898-1:2013 has a replacement work item under development.

## Next pass

Build the canonical source-backed material records with:
`material + condition + property + value + unit + temperature + product form + test method + source + provenance + verification state`.
Then broaden the catalogue across the remembered core families: steels, aluminium, tungsten, magnesium, exotics, engineering polymers and general polymers.
