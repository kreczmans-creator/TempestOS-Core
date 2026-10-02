# Engineering Knowledge Foundation v1.4.0

## Core metal population

This pass materially expands the catalogue with **16 steels and 12 aluminium alloy states**, covering common structural steels, carbon/low-alloy steels, stainless steels, precipitation-hardening stainless, tool steels, wrought aluminium and aerospace-oriented aluminium states.

Each record now carries:
- representative screening mechanical/physical properties;
- explicit reference temperature;
- standard references to resolve;
- evidence state;
- data-quality gaps;
- separation between catalogue identity and observation-level property data.

### Trust boundary

The numerical values in this release are **candidate reference values for screening only**. They are not asserted as guaranteed standard minima, certified material allowables, or supplier certificate values.

Before TempestOS can use an observation for authoritative design, it needs an exact source, applicable product form, material condition, section/thickness where relevant, test method and provenance.

### Immediate benefit

The selector now has enough breadth to exercise realistic material-selection queries against a non-trivial steel/aluminium population while preserving the evidence gaps that prevent premature design use.
