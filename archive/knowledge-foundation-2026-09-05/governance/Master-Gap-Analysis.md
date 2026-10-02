# Master Gap Analysis — v0.74.0 Against the V1.0 Fundamentals Boundary

**Performed:** 2026-09-05, against the actual file tree, not against any
prior report. Every figure below is reproducible with
`python3 tools/validate.py` and the rollup in
`governance/generated/domain-coverage-matrix.json`.

**Depth scale:** the 15-element standard in
`governance/V1.0.0-Definition-of-Done.md` §7. "Best depth" is the single
strongest record in that domain — not the average, which is lower.

---

## 1. Classification Used

| Class | Meaning |
|---|---|
| **COMPLETE** | Topic present and at full depth |
| **ADEQUATE** | Topic present, depth sufficient for a fundamentals baseline |
| **INSUFFICIENT** | Topic named and structured, content below usable depth |
| **MISSING** | No meaningful representation |
| **SPECIALIST** | Deliberately beyond the fundamentals boundary |
| **FUTURE** | Belongs in the library eventually, not in v1.0.0 |

## 2. Result Summary

| Class | Domains |
|---|---|
| COMPLETE | **0** |
| ADEQUATE | **0** |
| INSUFFICIENT | **43** |
| MISSING | **1** (springs — 1 file, 44 words) |
| SPECIALIST / FUTURE | per §5 |

**No domain in this package reaches adequate depth.** The strongest
record anywhere scores 5 of 15; most domains peak at 2 or 3. This is a
uniform finding, not a patchy one, and it is consistent with the
baseline: the library is a well-organised topic index whose content has
not been written yet.

## 3. Domain Matrix

Ordered by content volume. Every row is INSUFFICIENT unless marked.

| Domain (§3 list) | Files | Words | Best depth | Class |
|---|---:|---:|---|---|
| Materials | 130 | 22,658 | 2/15 | INSUFFICIENT |
| Mechanical engineering | 56 | 6,980 | 5/15 | INSUFFICIENT |
| Fatigue | 49 | 4,251 | 5/15 | INSUFFICIENT |
| Manufacturing | 47 | 3,632 | 4/15 | INSUFFICIENT |
| Mechanics | 46 | 6,514 | 5/15 | INSUFFICIENT |
| Environmental engineering | 45 | 3,084 | 3/15 | INSUFFICIENT |
| Thermal engineering | 40 | 3,397 | 5/15 | INSUFFICIENT |
| Joints | 34 | 1,895 | 3/15 | INSUFFICIENT |
| Structures | 33 | 1,502 | 4/15 | INSUFFICIENT |
| Electrical fundamentals | 24 | 2,713 | 1/15 | INSUFFICIENT |
| Verification | 20 | 5,030 | 2/15 | INSUFFICIENT |
| Tribology / wear | 20 | 1,493 | 3/15 | INSUFFICIENT |
| Interfaces | 17 | 637 | 3/15 | INSUFFICIENT |
| Pressure systems | 12 | 732 | 3/15 | INSUFFICIENT |
| Fracture | 11 | 1,577 | 5/15 | INSUFFICIENT |
| Systems engineering | 11 | 334 | 2/15 | INSUFFICIENT |
| Tolerancing / GD&T | 8 | 302 | 2/15 | INSUFFICIENT |
| Electronics | 7 | 245 | 2/15 | INSUFFICIENT |
| Requirements | 7 | 218 | 2/15 | INSUFFICIENT |
| Sensors | 6 | 218 | 2/15 | INSUFFICIENT |
| Signal conditioning | 6 | 216 | 2/15 | INSUFFICIENT |
| Electromechanical | 6 | 210 | 2/15 | INSUFFICIENT |
| Safety | 4 | 165 | 3/15 | INSUFFICIENT |
| Reliability | 4 | 165 | 3/15 | INSUFFICIENT |
| Digital control | 4 | 142 | 2/15 | INSUFFICIENT |
| Instrumentation | 4 | 133 | 2/15 | INSUFFICIENT |
| Maintainability | 4 | 113 | 2/15 | INSUFFICIENT |
| Serviceability | 4 | 113 | 2/15 | INSUFFICIENT |
| Human factors | 4 | 113 | 2/15 | INSUFFICIENT |
| Quality | 4 | 112 | 3/15 | INSUFFICIENT |
| Production | 4 | 112 | 3/15 | INSUFFICIENT |
| Configuration management | 4 | 109 | 1/15 | INSUFFICIENT |
| Change control | 4 | 109 | 1/15 | INSUFFICIENT |
| Architecture | 4 | 116 | 1/15 | INSUFFICIENT |
| Shafts | 4 | 215 | 4/15 | INSUFFICIENT |
| Gears | 4 | 189 | 2/15 | INSUFFICIENT |
| Bearings | 4 | 171 | 3/15 | INSUFFICIENT |
| Validation | 3 | 362 | 0/15 | INSUFFICIENT |
| Control systems | 3 | 156 | 2/15 | INSUFFICIENT |
| Fluid engineering | 3 | 144 | 3/15 | INSUFFICIENT |
| Metrology | 3 | 105 | 2/15 | INSUFFICIENT |
| Dynamics | 3 | 104 | 5/15 | INSUFFICIENT |
| Vibration | 3 | 104 | 5/15 | INSUFFICIENT |
| **Springs** | **1** | **44** | 1/15 | **MISSING** |

For scale: a single domain at the required depth is roughly 2,000–4,000
words. The entire 44-domain library currently holds 14,380 words of
engineering knowledge content.

## 4. What This Means For v1.0.0

Under the Product Owner's decision of 2026-09-05, **this gap is not a
v1.0.0 blocker.** v1.0.0 releases the structure with the data classified
as provisional; content depth is explicitly excluded (Definition of Done
§4) and registered as `TD-K01` (AMBER, deferred).

What the gap analysis therefore delivers to v1.0.0 is not a repair
programme but three things:

1. **A measured starting line.** 14,380 words, median depth 1/15,
   re-derivable at any time. Progress in the Content Programme is
   measurable from record one.
2. **A confirmed topic architecture.** 56 topic directories across the
   fundamentals domains, sensibly named and grouped, with only one
   genuine structural omission (springs). The scaffolding is sound and
   is being kept.
3. **An honest label on every record**, enforced by the validator rather
   than by convention.

## 5. Deliberately Outside The Fundamentals Boundary

Recorded so that later work does not mistake exclusion for omission:
advanced FEA and CFD; advanced control theory; specialist materials
science; specialist electronics and RF; advanced structural dynamics;
advanced fracture mechanics; specialist reliability mathematics;
regulatory compliance and product certification; and domain-specific
professional practice beyond fundamentals.

Each belongs to a separately defined Advanced Engineering / Specialist
Knowledge Programme, not to v1.x of this library.

## 6. Recommended Sequence For The Content Programme

Not part of v1.0.0. Recorded here because the analysis produced it.

1. **Mechanics, materials behaviour, joints, fatigue** — cited by
   everything else; write these first or later domains will invent
   their own vocabulary.
2. **Springs** — the one true structural gap; small and quick.
3. **Bearings, gears, shafts, structures, pressure** — the component
   domains, each already scaffolded.
4. **Thermal, fluid, electrical, electronics, instrumentation, control,
   digital control** — the cross-domain chain.
5. **Systems, verification, safety, reliability, quality, production,
   configuration, human factors** — process domains, best written once
   the technical domains they act on exist.
6. **Worked examples and cross-domain cases last**, since a worked
   example that cannot cite real content is a narrative.
