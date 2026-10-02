# Engineering Knowledge Foundation v1.2.0

## Material selection layer

The database now contains the first machine-consumable selection semantics.

A selection request should be represented as:

`constraints → eligible states → property/environment filters → exclusions → ranking → evidence report`

### Important distinction

A material can be:
- **eligible** for further consideration,
- **screening-ranked**, or
- **approved for design**.

These are deliberately different states.

The selector must not manufacture missing values. If a required property or environmental compatibility observation is absent, the result should say **unknown / evidence required**, not infer suitability.

### New data layers

- property taxonomy
- material/environment compatibility
- manufacturing compatibility
- selection fixtures

The next stage should connect these to actual sourced observations and implement deterministic filtering/ranking.
