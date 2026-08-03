# WP 7.0A — Lessons Learned

## Status

Complete.

## 1. TempestOS's product ambition existed only as fragments before this Work Package

Before `WP 7.0A`, the fact that TempestOS is meant to eventually serve
real engineering disciplines (Mechanical, Structural, Electrical,
Building Services/HVAC, Materials, Manufacturing, Systems Engineering,
Project Management, Quality) lived in exactly three places: `Threat
Model.md`'s own assumption 1 (a single sentence, written for a security
audit, not a product document), one dormant code file's field names
(`ProjectModel.cs`, unreferenced since before this project's Claude-
developed history began), and `ADR-0013`'s own passing mention of a
"Requirements Engine" and "Project Engine." No single document connected
these three signals into one coherent vision. Writing `VISION.md` was
the first time they were read together — a genuine finding in its own
right, not merely a writing exercise.

## 2. A future-capability register is only as honest as what it refuses to invent

The single hardest editorial decision in this Work Package was what
*not* to write. `Future Capability Register.md` could easily have
listed a plausible-sounding capability for each of the six empty
Engineering Discipline categories — a "Mechanical Analysis Module," a
"Structural Load Calculator" — and no reader unfamiliar with the
underlying source documents would have questioned it. None of that
would have been sourced from anything real. The register's own
Coverage Note disclosing these six empty categories, rather than
padding them, is this Work Package's own most direct application of
this project's standing "disclose Unknown, never invent" discipline
(`Governance Philosophy.md`), applied for the first time to *product*
content rather than only architectural or governance content.

## 3. Merging duplicate future-capability signals required real judgment, not mechanical concatenation

`TD-09`/`TD-10`/`TD-11` and `Security Roadmap.md` items 1, 2, and 10 all
describe the same underlying capability (a plugin/registration trust
boundary) from three different documents, written for three different
purposes (a Technical Debt Register entry, a security roadmap item, and
a Command Framework architecture finding). Recognising these as one
`FCR` entry rather than three or five required reading each source
document's own reasoning, not just pattern-matching on shared keywords
— a mechanical merge based on keyword overlap alone would have missed
that `TD-12` (Persistence query capability) is a genuinely distinct
capability from all three, despite also touching "the enforcement
mechanism exists but isn't applied yet" language superficially similar
to the trust-boundary cluster.

## 4. This project's own established governance discipline transferred directly to a new kind of register

Every convention this session's prior Work Packages established —
permanent, never-reused identifiers; a Coverage Status disclosing
partial coverage explicitly; a Cross-Reference Check verifying every
citation against its source; Verified/Inferred/Unknown marking on
uncertain claims (`FCR-0026`, marked Inferred rather than Verified,
since no Work Package confirmed defence-sector operation as an actual
current objective) — applied to `Future Capability Register.md` and
`Capability Categories.md` without needing any adaptation. This is
itself evidence the governance model this project built for tracking
*what exists* generalises cleanly to tracking *what might exist next*,
not a coincidence.

## 5. A vision document needs the same evidentiary discipline as an ADR, or it becomes marketing

The temptation, writing `VISION.md`, was to write aspirationally — bold
claims about market position, competitive advantage, total addressable
engineering-services market. None of that appears in the final
document. Every claim is either cited to an existing document or stated
explicitly as an ambition, not a fact. This is a deliberate,
non-obvious choice: a vision document that reads like the rest of this
project's own engineering documentation is more useful to a future
contributor than one that reads like a pitch deck, because it can
actually be checked against evidence the same way an ADR can.

## Recommendations for `v0.7.0` and Beyond

- **Identify real Engineering Discipline capabilities via a dedicated
  exercise, not further documentation mining.** The six empty categories
  in `Capability Categories.md` will not be honestly populated by
  reading this repository's own existing text again — they require
  engaging a real engineering-domain stakeholder or a concrete customer
  scenario.
- **Revisit `Future Capability Register.md`'s Coverage Status at every
  future release boundary**, mirroring every other governance register
  in this suite, so this new register does not become the next one to
  go stale for several Work Packages before a closing review catches it
  — precisely the pattern `FCR-0005` itself exists to prevent
  elsewhere.
- **Classify `FCR-0027` (Requirements Engine) and `FCR-0028` (Project
  Engine) under `ADR-0013` explicitly**, the first time either is
  seriously proposed for design — not implicitly, by whichever Work
  Package happens to touch either first.

## Related Documents

`VISION.md`; `docs/governance/Future Capability Register.md`;
`docs/governance/Capability Categories.md`; `docs/governance/Product
Roadmap.md`; `docs/governance/Governance Philosophy.md`.
