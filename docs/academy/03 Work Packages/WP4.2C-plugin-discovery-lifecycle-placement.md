# WP 4.2C — ADR: Plugin Discovery Lifecycle Placement

## 1. Introduction

WP 4.2C, like WP 4.2 and WP 4.2B before it, produced no production code.
Its entire job was one Architecture Decision Record — ADR-0026, *Plugin
Discovery and Plugin Loading Lifecycle Placement* — resolving the second,
and last, of the two ADRs `Plugin Manifest Architecture.md` named as
required before Plugin Manifest (`WP 4.2`) implementation may begin.

## 2. Purpose

To decide, precisely, where Plugin Discovery and Plugin Loading sit within
`Host Lifecycle.md`'s existing thirteen-phase table — a question `Runtime
Host Architecture.md` had named since WP 2.7A ("before Module Discovery")
without ever designing what "before" actually requires of the rest of the
startup sequence, and without which `WP 4.2`'s implementation could not
begin.

## 3. Background

By the time WP 4.2C began, ADR-0025 (WP 4.2B) had already classified every
plugin-loading failure category, and WP 4.2A had already given the
platform a queryable version. Both were named, individually, as
prerequisites for exactly this reason: this ADR needed a settled failure
model and a settled version source to reason about correctly, rather than
deciding all three at once. `Host Lifecycle.md`'s phase table had been
treated as complete and frozen since WP 2.7A/B — this work package is the
first time it is deliberately reopened.

## 4. The Problem

1. **Is this a new phase, or a refinement of an existing one?** The brief
   required this be evaluated explicitly, not assumed from the "before
   Module Discovery" language already on record.
2. **What must already exist before Plugin Discovery can run** — and does
   any of it create a genuine ordering conflict with what WP 4.2A already
   built? (It does — see Section 5.)
3. **Can Plugin Discovery rely on Logging and Configuration?** On
   `IPlatformVersionProvider`? Each needed a direct, not assumed, answer.
4. **Must Module Discovery change at all** to see plugin-loaded assemblies,
   or does its own existing `AppDomain.CurrentDomain.GetAssemblies()`
   default already make that unnecessary?
5. **How is duplicate-plugin-identity resolution made actually
   deterministic**, given ADR-0025's own "first manifest encountered wins"
   language never specified what "first" means across environments?

## 5. The Design

See `docs/adr/ADR-0026-plugin-discovery-lifecycle-placement.md` in full.
In summary: two new phases, **3.1 Plugin Discovery** and **3.2 Plugin
Loading**, inserted between the existing Phase 3 (Logging Built) and Phase
4 (Module Discovery) — decimal sub-numbering, not a renumbering of the
existing thirteen. Both occur entirely within the existing `Starting`
state; no new `HostState`, no new transition.

The design surfaced one genuine ordering conflict: Plugin Discovery needs
`IPlatformVersionProvider` for its `MinimumPlatformVersion` compatibility
check (ADR-0025, category 4), but WP 4.2A had placed
`PlatformVersionProvider`'s construction inside Platform Services
Registered (Phase 6) — which comes *after* Module Discovery, while Plugin
Discovery must run *before* it. Resolved by separating two concerns that
had been conflated: `PlatformVersionProvider`'s **construction** moves
earlier, to immediately follow Logging Built; its **DI registration**
(`AddInstance`) stays exactly where WP 4.2A put it, since nothing needs to
resolve it through DI before Module Initialisation regardless.

Deterministic duplicate-identity resolution is closed by specifying that
Plugin Discovery sorts candidate folders ordinally by folder name before
any processing, rather than relying on raw filesystem enumeration order,
which is not guaranteed stable across operating systems or file systems.

Module Discovery itself requires zero code changes: its existing
`AppDomain.CurrentDomain.GetAssemblies()` default already sees any
assembly loaded into the process by any means, including one Plugin
Loading loads via `Assembly.LoadFrom` — the design only needed to *use*
this already-true fact, not create a new capability.

## 6. Alternatives Considered

Recorded in full, with reasoning, in ADR-0026's own "Alternatives
Considered" section, and permanently indexed as RD-0012 (a single combined
Plugin Discovery/Loading phase), RD-0013 (renumbering all thirteen
existing phases instead of decimal sub-numbering), and RD-0014 (Plugin
Discovery reading platform version metadata independently rather than
reusing the Host's single `PlatformVersionProvider` instance). All three
follow the same discipline already established across this release: name
the alternative, state why it does not fit, record it permanently.

## 7. Why This Solution Was Chosen

Every non-obvious call in ADR-0026 traces back to protecting something
already settled while resolving something genuinely new. Decimal
sub-numbering was chosen specifically to leave the existing thirteen
phases, and every cross-reference to them across five documents and prior
ADRs, entirely untouched — a disproportionate blast radius was the
deciding factor against renumbering (RD-0013). Two phases, not one, was
chosen to mirror Module Discovery/Module Registration's own existing
split between a side-effect-free step and a harder-to-reverse one — the
same distinction, not a new one invented for plugins. Moving only
`PlatformVersionProvider`'s construction, not its registration, was chosen
because the two are genuinely separable concerns, and only one of them
was actually in tension with Plugin Discovery's ordering requirement.

## 8. Architectural Principles

- **Deterministic Startup** — every phase resolves the same way on every
  run; the new sort-by-folder-name rule exists specifically to make
  duplicate-identity resolution deterministic in practice, not only in
  principle.
- **One Responsibility Per Phase** — Plugin Discovery validates without
  side effects; Plugin Loading commits to a harder-to-reverse action
  (loading an assembly). Kept apart deliberately, per RD-0012.
- **Minimal Phases** — decimal sub-numbering was chosen over renumbering
  specifically to avoid an unnecessary expansion of what every other
  document must track, per RD-0013.
- **Reuse Before Invention** — Module Discovery's existing `AppDomain`
  behaviour is reused, not extended; `PlatformVersionProvider`'s existing
  constructor is reused earlier, not duplicated, per RD-0014.

## 9. Benefits

- Resolves the last remaining architectural blocker before Plugin
  Manifest implementation — both required ADRs (ADR-0025, ADR-0026) are
  now decided, alongside the already-resolved platform-version
  prerequisite (WP 4.2A).
- No renumbering of the existing thirteen phases — every existing
  cross-reference in `Host Lifecycle.md`, `Runtime State Machine.md`,
  `Startup Sequence.md`, `Failure Behaviour.md`, prior ADRs, and every
  prior Academy retrospective that cites a phase by number remains
  correct, unchanged, and valid.
- No new `HostState`, no new transition — `Runtime State Machine.md`
  required only a short note, not a redesign.
- Module Discovery requires zero code changes — proven, not merely
  assumed, by tracing exactly which existing guarantee Plugin Loading
  depends on.
- A real precision gap in ADR-0025 (what "first" means for duplicate
  resolution) was found and closed as a direct consequence of designing
  the ordering carefully enough to need to state it explicitly.

## 10. Trade-offs

- This is documentation only — nothing here is enforced by a compiler,
  test, or running code yet, exactly as every architecture-only work
  package in this release has noted about itself.
- `PlatformVersionProvider`'s construction now happens in a different
  place than WP 4.2A's own retrospective described. A future implementer
  of `WP 4.2` must move that one line — a small, explicit, and now
  fully-documented adjustment, not a rediscovery.
- Decimal phase numbering (`3.1`, `3.2`) is a new numbering shape for
  `Host Lifecycle.md`'s table — a reader must understand this means
  "between 3 and 4," not "version 3.1 of phase 3." Judged clearer and far
  less invasive than the alternative (RD-0013).

## 11. Common Mistakes

The mistake most worth naming here is one avoided: treating
`PlatformVersionProvider`'s ordering conflict as a reason to redesign its
registration, when only its construction was actually in tension with
Plugin Discovery's requirements. Recognising that construction and
registration are separable concerns — and moving only the one that
needed to move — avoided a larger, unnecessary change to WP 4.2A's
already-shipped design.

## 12. Future Evolution

- **No ADR remains outstanding** before Plugin Manifest (`WP 4.2`)
  implementation. All three prerequisites named by `Plugin Manifest
  Architecture.md` — the platform-version gap (WP 4.2A), plugin failure
  classification (WP 4.2B, ADR-0025), and phase-table placement (WP 4.2C,
  ADR-0026) — are resolved.
- **Background Services (`WP 4.5`)**, per `Risks.md` R1, is expected to
  also need a new phase inserted before Module Initialisation. ADR-0026's
  Future Considerations explicitly recommend it follow this ADR's own
  precedent — decimal sub-numbering, no renumbering, explicit entry/exit
  criteria — rather than re-deriving the question of *how* to insert a
  phase from scratch.
- **A future implementation work package** (`WP 4.2` itself) builds
  `PluginManifest`, `IPluginManifestDiscoveryService`, the
  `PlatformVersionProvider` construction-site move, and the corresponding
  code changes to realise Phases 3.1 and 3.2 exactly as designed here.

## 13. Key Takeaways

1. Reopening a table previously treated as "complete and frozen" does not
   require undoing that freeze everywhere — decimal sub-numbering let this
   ADR insert new phases while leaving every prior phase, and every
   document that cites one by number, completely untouched.
2. A dependency-ordering conflict (Plugin Discovery needs a service
   normally constructed later) does not always require redesigning the
   later thing — separating "construction" from "registration" as two
   independently-movable concerns resolved it with a one-line change.
3. Designing an ordering guarantee carefully enough to state it precisely
   ("sorted ordinally by folder name") is itself a way of finding gaps in
   an earlier decision (ADR-0025's imprecise "first encountered") — a
   second architecture-only pass over the same territory is not
   redundant if it asks a more specific question than the first pass did.

---

## Architectural Debt Assessment

**No new debt introduced.** This work package produced one ADR and three
Rejected Designs entries; no code exists for it to affect. Every debt item
on record from the Runtime Foundation, WP 4.0/4.1, and WP 4.2/4.2A/4.2B
remains exactly as previously described.

## Observations

- **Files changed**: 1 new ADR (`ADR-0026-plugin-discovery-lifecycle-
  placement.md`); 3 new Rejected Designs entries (RD-0012, RD-0013,
  RD-0014); `Host Lifecycle.md`, `Startup Sequence.md`, `Runtime State
  Machine.md`, and `Failure Behaviour.md` all updated with the new phases
  and their failure behaviour; `Plugin Manifest Architecture.md` and
  `Platform Service Map.md` updated to reflect both prerequisite ADRs now
  resolved; the WP 4.2/4.2A/4.2B retrospectives updated to remove now-
  stale "phase-table placement outstanding" language; `Risks.md` and
  `CHANGELOG.md` updated at the release level; this retrospective. Zero
  production code files touched.
- **Incidental fix**: `Failure Behaviour.md`'s own "Logging Failure"
  section was found, during this work package's cross-reference
  validation, to still describe the WP 2.6 sink-isolation gap as unfixed,
  even though it was fixed in WP 2.7B (`Logger.Log()` wraps `_sink.Write`
  in a `try`/`catch`). Corrected as part of this work package, since
  leaving a known-stale passage in place while editing the same document
  for an unrelated reason would only make it easier to overlook next time.
- **Remaining blocker before WP 4.2 implementation**: none. This was the
  second and last of the two ADRs named as required; combined with the
  already-resolved platform-version prerequisite (WP 4.2A), all three
  prerequisites `Plugin Manifest Architecture.md` named are now resolved.
- **Readiness assessment**: ADR-0026 is complete, self-consistent, and
  cross-referenced everywhere the original design document and its
  dependent lifecycle documents name a phase by number. Plugin Manifest
  implementation (`WP 4.2`) has no architectural blocker remaining and may
  now proceed.
