# TempestOS Engineering Knowledge Foundation

**Package version: see `VERSION` (authoritative).**
Current: **0.75.0** — Structure, Governance and Provenance Baseline.

---

## Read This First — Design-Use Boundary

> **No value, rule, threshold, or worked case in this package is approved
> for design use.**
>
> The content is **screening-level and provisional**. Of 684 content
> records: 447 declare themselves placeholder, 151 declare no status, 84
> require review, 2 are source-bound, and **none is verified**.
>
> Any product engineering decision must be taken against the applicable
> controlled standard, a qualified supplier datasheet, or test evidence —
> **never against this library.**

**Default classification.** Any record that declares no status of its own
is `placeholder` by this package-wide default. A record is never more
mature than it can evidence, and silence is not a claim of quality.
The current exceptions are enumerated in
`governance/generated/undeclared-records.txt`.

## What This Package Is

A structured **topic architecture** for engineering fundamentals — 56
topic areas spanning mechanics, materials, manufacturing, metrology,
tolerancing, thermal, fluid, electrical, electronics, instrumentation,
control, systems engineering, verification, safety, reliability,
quality, production, configuration management and human factors — with a
provenance model, a maturity classification, governance registers, and
automated validation.

## What This Package Is Not

It is **not** an engineering reference yet. The knowledge layer holds
about **14,380 words** across 44+ disciplines, at a median of **1 of 15**
required depth elements. It names the right subjects; it does not yet
teach them. That gap is registered as `TD-K01` (AMBER, deferred) and is
the subject of the Content Programme, not of this release.

It contains **no standards text**. Standards are cited by number and
title only; their content is copyrighted and is not reproduced.

## Layout

| Path | Contents |
|---|---|
| `data/engineering-metals/` | The engineering knowledge layer, 56 topic areas |
| `data/engineering-polymers/`, `exotics/`, `aluminium/`, `magnesium/`, `tungsten/`, `steel-depth/` | Materials catalogue — qualitative screening records |
| `data/governance/`, `data/evidence/`, `data/standards/` | Schemas, policies, evidence queues, status history |
| `docs/` | Full release history: 70 handoff notes, work-package status, release notes. Historical record — retained unaltered |
| `governance/` | V1.0.0 Definition of Done, master gap analysis, technical debt register |
| `governance/generated/` | Machine-generated registers — never hand-edited |
| `baseline/` | The v0.74.0 inspection this programme started from |
| `tools/validate.py` | The validator that generates the registers |

## Validation

```
python3 tools/validate.py            # regenerate registers, print summary
python3 tools/validate.py --check    # exit 1 on any invariant failure
```

Invariants enforced:
1. No content record may claim verification without naming a source.
2. Every content record must declare a maturity status, or appear in the
   accepted-exception list.
3. `MANIFEST.json` must reconcile exactly against the package.

## Governance

- `governance/V1.0.0-Definition-of-Done.md` — what v1.0.0 means, what it
  excludes, and the gate it must pass.
- `governance/Master-Gap-Analysis.md` — every domain measured and
  classified.
- `governance/Technical-Debt-Register.md` — every known deficiency, with
  colour and disposition. Two AMBER items are open and justified; they
  are the dominant facts about this package and are not hidden.
- `baseline/BASELINE-v0.74.0.md` — the measured starting point.

## Version History Convention

`VERSION` is the single authority. `MANIFEST.json` is regenerated at each
release to match it. Versioned manifests (`MANIFEST-v0.73.0.json`,
`MANIFEST-v0.74.0.json`) and everything in `docs/` are **historical
records and are not updated** — they are accurate statements about the
releases that wrote them.
