# v0.74.0 Baseline Inspection Report

**Taken:** 2026-09-05, before any file in the package was modified.
**Method:** direct enumeration and parsing of the extracted archive. No
claim below is taken from a prior report, handoff note or status file;
where a prior document's claim is quoted, it is quoted in order to be
tested against the files themselves.

Machine-readable companion: `BASELINE-v0.74.0.json` (per-file path, size
and SHA-256 for all 1,000 files, plus every census below).

---

## 1. Package Identity

| Fact | Value |
|---|---|
| Archive | `TempestOSEngineeringKnowledgeFoundationv0.74.0.zip` |
| Archive root directory | `TempestOS-Engineering-Knowledge-Foundation-v0.3.0` |
| `MANIFEST.json` declares | `0.72.0` |
| `MANIFEST-v0.73.0.json` declares | `0.73.0` |
| `MANIFEST-v0.74.0.json` declares | `0.74.0` |
| Latest library status file | `library-status-v0.74.0.json` |

**Finding B-01 (version identity).** Four different version identities
are present in one package. The archive root directory says `v0.3.0`;
the unsuffixed `MANIFEST.json` — the file a consumer would read first —
says `0.72.0`, two releases stale. Only the version-suffixed manifest
carries `0.74.0`. Nothing in the package resolves which is
authoritative.

## 2. Physical Inventory

| Measure | Value |
|---|---|
| Files | 1,000 |
| Directories | 229 |
| Total size | 1,802,589 bytes (1.72 MB) |
| `.json` | 734 |
| `.md` | 156 |
| `.yaml` | 92 |
| `.csv` | 17 |
| `.py` | 1 |
| Byte-identical duplicate files | **0** |

Top-level split: `data/` 841 files, `docs/` 156 files, 3 manifests.

**Manifest validation (performed, not assumed):** `MANIFEST-v0.74.0.json`
lists 999 entries. Every listed file exists on disk. One file on disk is
absent from it — `MANIFEST-v0.74.0.json` itself, though it does list the
other two manifests. Minor, but it means no manifest in the package is a
complete description of the package.

## 3. What The Package Actually Contains

`docs/` (156 files) is almost entirely process history: 70
`continue-handoff-vX.md`, ~20 `wp-status-vX.md`, and one release note per
version. It contains **no engineering content**.

`data/` (841 files) divides into two very different bodies of material:

| Body | Files | Character |
|---|---|---|
| Materials catalogue — `engineering-polymers` (229), `exotics` (81), `aluminium` (26), `magnesium` (23), `tungsten` (19), `steel-depth` (10), plus family dirs | ~390 | Qualitative screening records about material families |
| Engineering knowledge layer — `engineering-metals/` across 56 topic directories | 308 | The fundamentals content: mechanics, dynamics, bearings, gears, GD&T, controls, safety, systems, human factors, production quality |
| Governance and registers — `governance/` (72), `evidence/` (19), `standards/` (7), provenance, validation | ~90 | Status files, schemas, policies, evidence queues |

## 4. The Measured Depth Of The Knowledge Layer

Every one of the 299 parseable knowledge records in
`data/engineering-metals/` was parsed and scored against the fifteen
depth elements the programme's own §11 standard requires (definition,
physical meaning, governing principles, key variables, units,
assumptions, equations, interpretation, design implications, failure
implications, verification implications, common mistakes, limitations,
worked example, adjacent disciplines).

| Measure | Result |
|---|---|
| Knowledge records | 299 |
| **Median words per record** | **46** |
| Longest record | 121 words |
| **Total words, entire knowledge layer** | **14,380** |
| **Median depth elements present, of 15** | **1** |
| Best record in the package | 5 of 15 |
| **Records meeting 10 or more of 15** | **0** |

Element presence across all 299 records:

| Element | Present in | % |
|---|---|---|
| Worked example / workflow | 88 | 29% |
| Verification / checks | 77 | 25% |
| Definition or purpose | 53 | 17% |
| Limitations / boundary | 25 | 8% |
| Governing principles | 16 | 5% |
| Key variables | 14 | 4% |
| **Equations** | **10** | **3%** |
| Failure implications | 8 | 2% |
| Design implications | 6 | 2% |
| **Assumptions** | **6** | **2%** |
| **Units anywhere in the record** | **~3%** | — |
| Physical meaning, interpretation, common mistakes, adjacent disciplines | **0** | **0%** |

A representative record, complete and unedited —
`dynamics-vibration/single-degree-freedom-v0.70.0.json`:

```json
{
  "version": "0.70.0",
  "status": "design-framework",
  "purpose": "Establish first-order vibration response.",
  "relationships": ["ωn = sqrt(k/m)", "fn = ωn/(2π)"],
  "checks": ["forcing frequency", "resonance proximity", "damping",
             "response amplitude", "fatigue"],
  "boundary": "Real structures require validated modal properties where
               distributed mass/stiffness or nonlinearities matter."
}
```

Entire topic areas are this thin in total: `bearings` is 3 files and
1,156 bytes; `gears` 3 files and 1,164 bytes; `digital-control` 4 files
and 1,621 bytes; `human-factors-maintainability` 4 files and 1,421
bytes.

**Finding B-02 (the master finding).** The knowledge layer is a
**topic index expressed as keyword lists**, not engineering fundamentals
content. It names the right subjects — the topic coverage is genuinely
broad and sensibly organised — but it does not teach any of them. No
record in the package meets the depth standard the programme sets, and
the median record meets one element of fifteen.

## 5. The Package's Own Assessment Of Itself

This is not an external judgement imposed on the material. The package
says the same thing about itself, in its own metadata:

| Self-declared value | Count |
|---|---|
| `"design_use": "screening_only"` | 201 |
| `"design_use": "not approved"` | 143 |
| `"evidence_status": "screening-level qualitative record"` | 97 |
| `"evidence_status": "foundation reference"` | 66 |
| `"evidence_status": "unverified"` | 52 |
| `"status": "open"` | 79 |
| `"status": "framework_only"` | 10 |

`data/governance/library-status-v0.74.0.json`, the most recent status
record in the package, states in full: *"Principal mechanical and
cross-domain fundamentals are now represented; another gap-analysis pass
is required before declaring the fundamentals layer complete."*

`data/steel-depth/` — ten files — is a queue of property rows whose
status is `source_or_review_required`: the library records which values
it needs, and holds almost none of them.

**Finding B-03 (provenance).** Across 731 JSON files, 64 contain any
numeric value at all. The materials catalogue is qualitative throughout.
There is no populated property dataset behind it.

## 6. Technical Debt — Keyword Sweep

Run across all 1,000 files:

| Term | Files |
|---|---|
| TODO, FIXME, TBD, HACK, temporary, provisional, revisit, workaround, deprecated, obsolete, orphaned | **0 each** |
| placeholder | 2 |
| draft | 3 |
| incomplete | 4 |
| unknown | 7 |
| future | 5 |
| unresolved | 2 |
| stale | 1 |

**Finding B-04.** The keyword sweep is nearly clean, and it is
misleading. The package carries no marker debt because the deficiency is
not marked — it is the content itself. 201 records declaring
`screening_only` and 143 declaring `not approved` are the real debt
register, and they are self-declarations of incompleteness written in
the normal schema rather than flagged as debt. A keyword-driven debt
process would report this package as clean.

## 7. Baseline Conclusion

Verified, and stated plainly:

1. The **topic architecture is sound** — 56 topic directories covering
   mechanics through human factors, sensibly named and sensibly grouped.
   That is real work and it is worth keeping.
2. The **content behind those topics does not exist yet** at any usable
   depth. 14,380 words spanning 44+ engineering disciplines is a
   contents page, not a foundation.
3. The **materials catalogue is qualitative screening material** with no
   populated property data and no source binding.
4. The **process record is disproportionate** to the content: 70 handoff
   documents and 74 versioned status files describing the production of
   14,380 words of engineering material.

Nothing here is fabricated and nothing is hidden — the package is honest
about its own status in every metadata field. The gap is between what
the version history implies has been achieved and what the files
contain.

**This baseline modifies nothing.** It is the fixed reference point every
later gate in this programme is measured against.
