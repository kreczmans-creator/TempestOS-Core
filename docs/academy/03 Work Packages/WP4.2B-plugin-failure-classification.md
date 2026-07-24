# WP 4.2B — ADR: Plugin Failure Classification

## 1. Introduction

WP 4.2B, like WP 4.2 before it, produced no production code. Its entire
job was one Architecture Decision Record — ADR-0025, *Plugin Failure
Classification* — resolving the first of the two ADRs `Plugin Manifest
Architecture.md` named as required before Plugin Manifest implementation
may begin.

## 2. Purpose

To classify, exhaustively and deterministically, how TempestOS responds to
every named category of plugin-loading failure — not just state the
headline principle ("fail one plugin, not the platform") but decide,
category by category, whether each is fatal or isolated, what severity it
logs at, and what happens to startup, the failing plugin, and every other
plugin as a result.

## 3. Background

`Plugin Manifest Architecture.md`'s own reasoning already leaned toward
isolating plugin failures, mirroring ADR-0013's module-failure half rather
than its platform-service half — but a lean is not a decision, and this
work package's own brief named eleven specific failure categories a
complete classification needed to cover, several of which (duplicate
identity, dependency load failure, reflection/type load failure) the
original design document had not addressed individually at all.

## 4. The Problem

1. **Does every one of the eleven named categories actually belong to this
   ADR's scope**, or do some of them already fall under existing,
   unchanged pipeline behaviour (Module Discovery, Registration,
   Lifecycle) this ADR must not re-decide?
2. **Should any category be treated differently from the others**, or does
   "fail one plugin, not the platform" hold uniformly?
3. **Should plugins get an opt-in-to-critical mechanism**, mirroring
   `ICriticalBackgroundService` (ADR-0021), given how closely a plugin
   resembles a background service on the surface?
4. **What, if anything, in the existing, already-shipped codebase already
   handles one of these categories**, such that this ADR only needs to
   document it rather than newly decide it?

## 5. The Design

See `docs/adr/ADR-0025-plugin-failure-classification.md` in full. In
summary: a scope boundary is drawn first — this ADR governs only failures
within the *new* Plugin Discovery/Loading steps, not failures occurring
after a plugin's module is handed off to the existing, unchanged pipeline
(categories 9 and 10, explicitly marked out of scope and unchanged). Of
the remaining nine categories, eight are isolated (varying only in logging
severity, from Information for an expected version incompatibility to
Error for a corrupt assembly file) and one — a genuine defect in the
Host's own plugin-loading orchestration, not attributable to any specific
plugin — is Host-fatal, mirroring Module Initialisation's own identical
carve-out exactly.

## 6. Alternatives Considered

Recorded in full, with reasoning, in ADR-0025's own "Alternatives
Considered" section, and permanently indexed as RD-0010 (Host-fatal
plugin failures, mirroring Module Discovery's own duplicate-ID handling)
and RD-0011 (a per-plugin `IsCritical` manifest opt-in, mirroring
ADR-0021). Both follow the same discipline already established across
this release: name the alternative, state why it does not fit this
specific case, and record it permanently rather than leaving the
reasoning implicit in a single ADR's own prose.

## 7. Why This Solution Was Chosen

Every non-obvious call in ADR-0025 traces back to one question: does
ADR-0013's existing two-category model already answer this, or is this
genuinely new ground? For every category except the Host-level-defect
carve-out, the existing "module failures are isolated" half of ADR-0013
already answered it — a plugin's entire purpose is to *become* a module,
so its failure to do so is reasoned about the same way. The one
genuinely new element — a full, named table covering categories ADR-0013
never had to enumerate this specifically (duplicate identity, incompatible
version, missing assembly, and so on) — exists because this brief asked
for exhaustive coverage, not because the underlying principle needed to
change.

## 8. Architectural Principles

- **Fail one plugin, not the platform** — the organising principle for
  every row in ADR-0025's table but one.
- **Minimal special cases** — eleven named categories collapse to three
  outcomes (not-a-failure, isolated, Host-fatal); only one category
  deviates from "isolated."
- **Reuse Before Invention** — the Host-fatal carve-out reuses Module
  Initialisation's own existing "Host-level defect" language verbatim,
  rather than inventing new terminology for the same concept.
- **Avoid Speculative Design** — the per-plugin critical-opt-in question
  was resolved by recognising *why* ADR-0021's own mechanism works
  (a live component's self-assessment) and that no equivalent exists at
  the point these failures occur, not by assuming the pattern should
  simply transfer.

## 9. Benefits

- Plugin Manifest implementation now has a complete, table-form failure
  model to build directly against — no category left to improvise at
  implementation time.
- Category 8 (reflection/type load failure) was found to already be
  handled by existing, unchanged code
  (`ReflectionFrameworkDiscoveryService.GetLoadableTypes`'s existing
  `ReflectionTypeLoadException` handling) — a concrete instance of this
  release's own repeated pattern: reviewing existing code carefully
  sometimes reveals less new work is needed than a brief's own category
  list might suggest.
- Two genuine alternatives were named, reasoned about, and permanently
  recorded (RD-0010, RD-0011) rather than only implicitly rejected inside
  a single document's prose.

## 10. Trade-offs

- This is documentation only — nothing here is enforced by a compiler,
  test, or running code yet, exactly as every architecture-only work
  package in this release has noted about itself.
- No mechanism yet exists for surfacing "which plugins failed and why" to
  anything beyond the log — named explicitly in ADR-0025's own Future
  Considerations as a real, present gap for a future diagnostics
  capability (`WP 4.8`) or the Plugin Manifest implementation itself to
  close, not solved here.

## 11. Common Mistakes

The mistake most worth naming here is one avoided: assuming
`ICriticalBackgroundService`'s pattern should transfer to plugins simply
because the two concepts look similar on the surface (both are
"optional, pluggable capabilities that might fail"). Examining *why*
ADR-0021's mechanism works — a live, running component capable of making
its own self-assessment — and confirming that precondition does not hold
for a manifest read before any plugin code has ever executed, is what
correctly ruled the pattern out, rather than a more superficial "these two
things seem similar, so use the same mechanism" instinct.

## 12. Future Evolution

- ~~**The one remaining ADR** — `Host Lifecycle.md` phase-table placement —
  is now the sole remaining blocker before Plugin Manifest implementation
  may begin.~~ **Resolved, WP 4.2C — ADR-0026.** No ADR remains outstanding
  before Plugin Manifest implementation.
- **A future diagnostics capability** (`WP 4.8`) should be able to read
  "which plugins failed, and why" from whatever structure Plugin Manifest
  implementation ends up building — ADR-0025 requires this be possible
  without itself being revisited, but does not design the structure.
- **The candidate exception hierarchy** (`PluginException` and its six
  named subtypes) is now fully named in `Plugin Manifest Architecture.md`,
  ready for `WP 4.2` to implement without further design.

## 13. Key Takeaways

1. A brief that lists eleven failure categories does not necessarily
   require eleven different behaviours — the value of working through
   each one individually is confirming *sameness*, not just finding
   differences.
2. Scope boundaries matter as much as the decision itself — explicitly
   naming which categories belong to the *existing*, unchanged pipeline
   (and are therefore out of this ADR's scope) prevented this document
   from accidentally re-deciding something ADR-0013 already settled.
3. The right question when a new component resembles an existing
   pattern is never "does this look similar" but "does the *reason* the
   existing pattern works also hold here" — it did not, for
   `ICriticalBackgroundService`, and recognising that avoided a real
   design mistake.

---

## Architectural Debt Assessment

**No new debt introduced.** This work package produced one ADR and two
Rejected Designs entries; no code exists for it to affect. Every debt item
on record from the Runtime Foundation, WP 4.0/4.1, and WP 4.2/4.2A remains
exactly as previously described.

## Observations

- **Files changed**: 1 new ADR (`ADR-0025-plugin-failure-classification.md`);
  2 new Rejected Designs entries (RD-0010, RD-0011); `Plugin Manifest
  Architecture.md` updated throughout (Alternative Designs Considered,
  Risks, ADRs Required, Recommendation, Validation Strategy, Candidate
  Public API); the WP 4.2 retrospective updated; Platform Service Map
  updated; this retrospective. Zero production code files touched.
- **Remaining blocker before WP 4.2 implementation**: one ADR —
  `Host Lifecycle.md` phase-table placement for the new Plugin Discovery
  and Plugin Loading steps. **Resolved, WP 4.2C — ADR-0026** (see that
  retrospective). No ADR remains outstanding.
- **Readiness assessment**: ADR-0025 is complete, self-consistent, and
  cross-referenced everywhere the original design document named it as
  required. Plugin Manifest implementation was correctly blocked on
  exactly one remaining decision at the time of this work package; that
  decision (ADR-0026, WP 4.2C) was subsequently resolved, and **Plugin
  Manifest implementation is now complete (WP 4.2)** — every isolated/
  Host-fatal category this ADR classifies is verified by a real,
  dedicated test in `PluginManifestDiscoveryServiceTests`/
  `PluginAssemblyLoaderTests`. See the WP 4.2 implementation retrospective.
