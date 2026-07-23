# WP 4.2 — Plugin Manifest Architecture

## 1. Introduction

WP 4.2, like WP 2.7A before it, produced no production code. Its job was to
design the Plugin Manifest — the description of a module that exists
before that module is ever loaded — closing a gap `Runtime Host
Architecture.md` named during WP 2.7A but explicitly left out of that work
package's own scope. This retrospective covers one new architecture
document (`Plugin Manifest Architecture.md`) and two new Rejected Designs
entries (RD-0008, RD-0009).

## 2. Purpose

To answer, in writing, before implementation begins: what does a manifest
describe (and, as importantly, what does it deliberately not describe);
where does reading one sit relative to the existing, frozen module
pipeline; what shape should it take; how are its values validated and
versioned; and what must be decided — via ADR — before any of this becomes
code.

## 3. Background

By the time WP 4.2 began, WP 4.0 (Platform Contracts) and WP 4.1 (Module
SDK) had both shipped, each demonstrating the same discipline: build only
what current understanding supports, defer everything else with a named
owner. WP 4.2 continues that discipline in an architecture-only mode,
mirroring exactly how WP 2.7A preceded WP 2.7B for the Runtime Host itself.

## 4. The Problem

1. **Where does a manifest actually sit**, given `Runtime Host
   Architecture.md` already named "before Module Discovery" as the answer,
   without ever designing what "before" means in practice?
2. **What should the manifest contain**, and — following this release's own
   governing philosophy — what should it deliberately not contain yet?
3. **What shape should it take** — object, interface, attribute,
   code, JSON, generated, or a separately-discoverable artifact — evaluated
   explicitly rather than assumed?
4. **Does any of Discovery, Registration, or Lifecycle need to change?**
   (The answer needed to be no, and needed to be demonstrated, not merely
   asserted.)
5. **What must be decided, formally, before a single line of
   implementation is written?**

## 5. The Design

See `docs/architecture/Plugin Manifest Architecture.md` in full. In
summary: a manifest is a pre-discovery artifact, structurally analogous to
`ModuleDescriptor` but describing something not yet loaded, rather than
something already reflectable. It is a JSON file, read by a new,
Host-owned "Plugin Discovery" step preceding Module Discovery — not an
attribute, not generated code, not a change to any existing service.
Required fields (`Id`, `Name`, `Version`, `MinimumPlatformVersion`,
`AssemblyFileName`) were each justified individually; several
brief-suggested possibilities (Author, Description, an explicit entry-point
type, a version ceiling) were considered and explicitly excluded, each for
a stated reason, not by omission.

## 6. Alternatives Considered

Recorded in full, with reasoning, in `Plugin Manifest Architecture.md`'s
own "Architectural Questions — Evaluated" and "Alternative Designs
Considered" sections, and permanently indexed as RD-0008
(`IPluginManifestSource` abstraction) and RD-0009 (a maximum/"tested up
to" platform version) in the Rejected Designs Log. Both follow directly
from the same reasoning already applied throughout WP 4.0 and WP 4.1: no
current consumer, no real design experience to draw on yet, purely
additive if reversed later.

## 7. Why This Solution Was Chosen

Every non-obvious decision in this work package traces back to one
question: does the existing architecture already answer this, or is it
genuinely new ground? "Where does a manifest sit" was already answered by
`Runtime Host Architecture.md`; this work package's job was to make that
one sentence into a complete design. "What does the manifest need to
contain" was genuinely new ground, and was resolved the same way WP 4.0
resolved its own six-contract scope: challenge every candidate field
against a real, current consumer, and exclude what fails that test.

## 8. Architectural Principles

- **The Manifest describes; the Runtime decides** — the organising
  principle for every responsibility boundary in this design.
- **Reuse Before Invention** — `PluginManifest` reuses `ModuleDescriptor`'s
  own immutable-snapshot shape; `IPluginManifestDiscoveryService` reuses
  `IFrameworkDiscoveryService`'s own interface pattern.
- **Avoid Speculative Design** — the same discipline WP 4.0 and WP 4.1
  both applied, now demonstrated a third time and permanently indexed via
  RD-0008/RD-0009.
- **Fail Fast** — validation occurs at Plugin Discovery time, before any
  assembly is loaded, not discovered awkwardly later via a failed
  `Assembly.LoadFrom` call.

## 9. Benefits

- A complete, reviewable design exists before any code — the same benefit
  WP 2.7A produced for the Runtime Host, now produced for plugin loading.
- A genuine, previously-unnoticed architectural gap was found: TempestOS
  has no queryable "what version am I" at runtime at all. Finding this
  before implementation, rather than during it, is exactly what an
  architecture-first pass is for.
- Two ADRs are now named and scoped precisely (plugin failure
  classification; phase-table placement) rather than left to be decided
  ad hoc, under time pressure, mid-implementation.

## 10. Trade-offs

- This is documentation only — nothing here is enforced by a compiler,
  test, or running code yet, exactly as WP 2.7A's own retrospective noted
  about itself.
- Implementation is explicitly recommended *not* to proceed yet — a
  trade-off in itself: two ADRs and one cross-cutting fix (the platform
  version gap) must land first, adding a real, if small, delay before
  `WP 4.2`'s design becomes working code.

## 11. Common Mistakes

The mistake most worth naming here is one avoided, not one that happened:
proposing an explicit "entry point type" field for the manifest, which
would have quietly duplicated Module Discovery's own type-scanning logic
in a second place. Reusing Discovery's existing scan — applied to a
newly-loaded plugin assembly exactly as it already applies to any other —
avoided inventing a second, competing way to answer "which type is the
module," a mistake that would only have surfaced once two mechanisms
started disagreeing.

## 12. Future Evolution

- **Two ADRs**, named explicitly in `Plugin Manifest Architecture.md`, must
  be written before implementation: plugin failure classification, and
  `Host Lifecycle.md` phase-table placement.
- ~~The platform-version-at-runtime gap should be resolved as its own,
  small, Runtime-Foundation-level addition~~ — **done**: see WP 4.2A
  (*Runtime Platform Version Infrastructure*), completed immediately
  after this work package, resolving it exactly as recommended here —
  as its own, separate, focused work package, not folded into Plugin
  Manifest implementation.
- **A future implementation work package** (not yet numbered) builds
  `PluginManifest`, `IPluginManifestDiscoveryService`, and the
  corresponding updates to `Host Lifecycle.md`, `Runtime State Machine.md`,
  and `Failure Behaviour.md` — mirroring exactly how WP 2.7B implemented
  WP 2.7A's design.

## 13. Key Takeaways

1. An architecture-only work package's value is proportional to what it
   finds, not just what it proposes — the platform-version gap is this
   work package's most significant discovery, and it was found by asking
   "what does `MinimumPlatformVersion` actually get compared against,"
   not by looking for problems.
2. The Rejected Designs Log, introduced immediately before this work
   package began, proved its value the very first time it was used for a
   design-phase (rather than backfilled) rejection — RD-0008 and RD-0009
   exist because the log made "should this be a separate abstraction"
   feel like a real, indexed question to answer, not a passing thought to
   wave away in one sentence.
3. "The Manifest describes, the Runtime decides" is a small enough
   sentence to fit in a commit message, and precise enough to resolve
   every responsibility-boundary question this work package faced.

---

## Architectural Debt Assessment

**No new debt introduced.** This work package produced documentation only.
One genuine gap was *found*, not introduced: TempestOS's own running
version is not queryable at runtime (Versioning Strategy, in the main
architecture document). This is pre-existing, newly discovered — every
other debt item on record from the Runtime Foundation and WP 4.0/4.1
remains exactly as described.

## Observations

- **Files changed**: 1 new architecture document (`Plugin Manifest
  Architecture.md`); 2 new Rejected Designs entries (RD-0008, RD-0009);
  this retrospective. Zero production code files touched — none exist for
  this work package to touch.
- **ADRs required**: 2 (plugin failure classification; `Host Lifecycle.md`
  phase-table placement) — deliberately left open, not decided informally.
  **Both now resolved**: failure classification, ADR-0025 (WP 4.2B); phase-
  table placement, ADR-0026 (WP 4.2C, see that retrospective). No ADR
  remains outstanding.
- **Risks discovered**: the platform-version-at-runtime gap (resolved —
  WP 4.2A, see that retrospective); loading an untrusted/malformed
  assembly file (classification now settled — ADR-0025, category 6); no
  assembly-unloading support (consistent with, not worse than, ADR-0015).
- **Readiness assessment**: the design is complete and sound. Per the
  architecture document's own Recommendation, implementation was withheld
  until the two required ADRs were written. **Update, WP 4.2C**: all three
  original prerequisites are now resolved — the platform-version gap
  (WP 4.2A), plugin failure classification (WP 4.2B, ADR-0025), and phase-
  table placement (WP 4.2C, ADR-0026). No architectural blocker remains;
  Plugin Manifest implementation (`WP 4.2`) may now proceed.
