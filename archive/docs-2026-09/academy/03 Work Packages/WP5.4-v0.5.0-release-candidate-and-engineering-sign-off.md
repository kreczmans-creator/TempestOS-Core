# WP 5.4 — v0.5.0 Release Candidate & Engineering Sign-Off

## What This Document Is

Unlike every other retrospective in this folder, `WP 5.4` did not design
or implement a platform capability — it verified, closed, and signed off
an entire release. This document is shaped to answer the five specific
questions that kind of Work Package actually raises (what was achieved,
architectural lessons, implementation lessons, repository maturity,
recommendations for the next release), rather than forced into the
13-section template every feature Work Package's retrospective uses —
that template's own "Alternatives Considered"/"Trade-offs" sections
don't meaningfully apply to a whole-release verification pass. This is a
deliberate, disclosed departure, not an oversight.

## 1. Introduction

`v0.5.0` ("Developer Experience") is the second release TempestOS has
shipped. Where `v0.4.0` proved the platform could be *extended* —
plugins, an event bus, background services — `v0.5.0` proves it can be
*used*: a person can navigate it, invoke logic in it, and see what it is
doing. `WP 5.4` is this release's own closing Work Package: not a
feature, but the formal verification that everything claimed across
`WP 5.0A` through `WP 5.3` is actually true, checked directly rather than
assumed from nine separate retrospectives' own say-so.

## 2. What Was Achieved

Nine Work Packages, four new platform services, one comprehensive
security audit, zero regressions:

- **Navigation** (`WP 5.0A`/`WP 5.0B`) — a DI-public, UI-agnostic
  registry of navigable destinations, the platform's first real,
  human-facing concept.
- **The Shell & Composition Framework** (`WP 5.0C`/`WP 5.0D`) —
  `Tempest.App` runs the real platform for the first time in this
  project's history, closing a gap that had existed, disclosed but
  unaddressed, since `WP 2.7B`.
- **The Platform Security Baseline** (`WP 5.0S`) — the platform's first
  comprehensive security audit, not part of the original plan, folded in
  once the platform reached a size that warranted one. No Critical or
  High severity finding.
- **The Command Framework** (`WP 5.1A`/`WP 5.1B`) — a typed dispatcher
  and an Id-keyed registry, reaching the identical "DI-public Platform
  Service" shape the Event Bus and Navigation had each independently
  reached before it.
- **Diagnostics** (`WP 5.2`) — a read-only projection over the Host's
  own lifecycle state, closing two named debt items and introducing a
  genuinely novel Ownership Matrix combination (DI-public *and*
  Composition-Root-constructed) in the process.
- **Developer Experience tooling** (`WP 5.3`) — a `dotnet new` module
  template and a four-Work-Package-old Discovery pitfall finally
  enforced, not merely documented.

355 → 552 tests (+197). 30 → 39 ADRs. 29 → 45 Rejected Designs entries.
63 → 77 Academy articles (see Repository Maturity, below, for a
genuine miscount in this figure found and corrected by this Work
Package). Zero breaking changes to any Platform Foundation contract.

## 3. Architectural Lessons

**The "is this a DI-public Platform Service" question keeps getting
asked, and keeps getting answered the same way, for a good reason.** The
Event Bus (`v0.4.0`), Navigation, the Command Framework, and Diagnostics
each reached this design independently, at four different Work Packages,
without ever citing each other's decision as a shortcut. This is not
coincidence — it is evidence that `ADR-0009`'s Composition Root pattern
and the "constructor-injected, no orchestration authority" shape it
implies are genuinely the right answer for this class of problem, not a
convention followed out of habit. Diagnostics then extended the pattern
into new territory: the first platform service that is *both*
Composition-Root-constructed *and* DI-public, a combination the Ownership
Matrix had no prior row for — because unlike the three before it,
Diagnostics' own dependencies (`IModuleLifecycleManager`/
`IHostedServiceManager`) are Host-owned and not yet constructed at
registration time. The `Func<T>` lazy-accessor pattern this forced is
this release's single most reusable architectural contribution — any
future Platform Service facing the identical timing constraint now has a
worked example to follow, not a problem to re-solve from scratch.

**A four-layer platform model absorbs a fourth and fifth new capability
without strain, which is itself the evidence it was designed correctly
the first time.** `ADR-0023` was written during `v0.4.0` planning,
before Navigation, the Shell, the Command Framework, or Diagnostics
existed even as ideas — none of them required so much as a footnote
added to that model to fit.

## 4. Implementation Lessons

**Two genuine implementation findings this release, both resolved
without a container redesign.** `CommandHandlerTable` (two independent
singleton registrations against one concrete type do not share an
instance) and the `Func<T>` accessor pattern (two dependencies do not
exist yet at registration time) are both, in retrospect, the same class
of lesson: a design that is correct at the architecture level can still
surface a real, previously-unconsidered constraint the moment it meets
this specific container's actual construction rules — and the right
response, both times, was a small, additive collaborator, not a
revisit of `ADR-0005`'s own custom-container decision.

**A brief's own premise is not always true, and checking costs less than
building the wrong thing.** `WP 5.2`'s own brief described an "Event
Framework Implementation" against an architecture document that did not
exist. Investigating this before writing code — the same discipline
`WP 4.4C` established (`D-009`) — found the real, current `WP 5.2` was
Diagnostics Improvements entirely. This is the second time in this
project's history a Work Package brief's own premise was found false
before implementation began, not after; both times, the cost of checking
was a few minutes of repository investigation, and the cost of not
checking would have been an entire Work Package's worth of wasted,
wrongly-targeted implementation.

**A documented pitfall is not the same as an enforced one.**
`Building a Module.md` warned, in prose, since `WP 4.1`, that a module
without `[ModuleMetadata]` needs a parameterless constructor — but the
actual failure mode (a raw `MissingMethodException`) went unfixed for
four Work Packages until `WP 5.3` specifically went looking for exactly
this kind of gap. Documentation that describes a constraint is not a
substitute for code that enforces it clearly; a Developer-Experience-
focused Work Package is precisely the right place to notice the
difference.

## 5. Repository Maturity

**The governance suite held under sustained load, but not without
friction — a friction worth naming honestly.** Every Work Package this
release produced its required ADRs, Rejected Designs entries, and
Academy retrospectives — zero governance gaps of that kind were found.
But a second, quieter pattern also held, consistently: **every Work
Package from `WP 5.1B` onward found real, previously-unnoticed drift
during its own repository review, left behind by an earlier Work
Package, not by itself.** `WP 5.1B` found stale Engineering/Delivery
registers and a missing `WP 5.0S` entry in `WorkPackages.md`. `WP 5.2`
found a stale Command Framework status marker. `WP 5.3` found `RD-0042`–
`RD-0044` missing from their own source log, a stale Engineering
Governance §11, and a `Governance Register.md` Compliance Matrix four
Work Packages out of date. `WP 5.4` — this Work Package — found a
genuine arithmetic error in the Exception Register (a stated total that
had *never* matched its own Entries table, since `WP 5.1B` first
introduced it), a `ReleasePlan.md` frozen at `WP 5.0C`'s own moment in
time, three risks (`R5`, `R7`, `R9`) that had been sitting "retired for
this release's shipped scope; residual carries forward" for so long the
residual itself had quietly been resolved without anyone updating the
risk row, and a `Contributor Learning Path.md` — the document a *new
contributor* reads first — still pointing at the wrong release's own
Work Package plan.

None of this is a criticism of any one Work Package. It is evidence that
**a governance suite this size cannot be kept current by any single Work
Package's own repository review alone** — each review is necessarily
scoped to what that Work Package touched, and a document nobody's own
scope touches drifts invisibly until something forces a wider sweep. This
release's own experience suggests that a repository-wide sweep — closer
in shape to what `WP 5.4` just performed — has genuine, recurring value
at a cadence tighter than "once per release," not because the discipline
failed, but because it is structurally impossible for a narrowly-scoped
review to catch drift outside its own scope.

**The governance suite itself remains internally sound, with two
genuine exceptions, both arithmetic, both now corrected.** Every count
this Work Package independently re-derived from the repository directly
— ADRs (39), Rejected Designs (45), Decision Register entries (20),
production modules (7), public interfaces (31) — matched what the
relevant register already claimed. Two did not: the Exception Register's
own stated total (30) had never matched its own Entries table (31, true
since `WP 5.1B` first introduced the mismatch); and the Academy
Register's own "03 Work Packages" count had silently undercounted its
own table by one for at least two consecutive Work Packages ("33" then
"34," while the table itself already held 34 then 35 rows), with the
overall academy-wide total (76) inheriting the same undercount rather
than being independently re-derived from the file system each time. Both
errors share the same shape: a register's own summary line was updated
by incrementing the *previous* summary line, not by re-counting the
table beneath it — a small, easy mistake that compounds silently for
exactly as long as nobody re-derives the number from source. The suite
is trustworthy; it simply needs a periodic, dedicated pass wider than any
one Work Package's own scope, re-deriving counts from the file system
directly rather than incrementing the previous register's own stated
number, to stay that way.

## 6. Recommendations for v0.6.0

1. **Consider a lighter, more frequent "governance sweep" checkpoint**
   — not a full Work Package, but a standing item in the Definition of
   Done for every third or fourth Work Package, re-deriving key counts
   directly from the repository (ADRs, RD entries, exception types,
   namespace counts) rather than trusting the previous register's own
   arithmetic. This release's own experience shows this catches real
   errors a normal repository review, scoped to one Work Package's own
   changes, structurally cannot.
2. **Resolve `TD-09`/`TD-10`/`TD-11` together, as `Security Roadmap.md`
   item 1 already recommends**, before any Work Package proposes real
   third-party plugin support — an isolation boundary and an ownership/
   priority model for Navigation and Command Ids, designed as one
   Architecture Work Package, not two.
3. **Wire the Shell's own input handling to the Command Framework** — the
   one named, deferred connection between two capabilities this release
   built independently but never actually joined.
4. **Revisit `TD-01`'s own legacy `LoggingService` code** with a concrete
   disposition (migrate for real, or delete outright) rather than
   re-scoping it forward a third time — the code has now had zero live
   callers across two entire releases.
5. **Whatever `v0.6.0` builds, treat this release's own architectural
   reuse pattern (DI-public, Composition-Root-constructed where needed)
   as the default starting hypothesis** for any new platform service,
   not a decision to re-derive from first principles each time.

## Key Takeaways

1. A release-closing Work Package's own value is in *re-deriving* claims
   directly from the repository, not in re-reading what nine other
   retrospectives already said about themselves — the one genuine
   arithmetic error this Work Package found was invisible to every
   register's own internal review precisely because no one had
   re-counted against the actual source files since `WP 5.1B`.
2. Architectural reuse compounds: four independent "should this be
   DI-public" decisions reaching the same answer is stronger evidence of
   a correct pattern than any single ADR's own reasoning could be alone.
3. Documentation that describes a constraint and code that enforces it
   are two different deliverables — a release is not fully "developer
   experience" complete until both exist for the same constraint.
4. Governance discipline at the level of "did this Work Package do its
   own job" and governance discipline at the level of "does the whole
   suite still agree with itself" are different questions, and this
   release's own history shows the second one needs its own, periodic,
   wider-scoped check — not because anyone failed, but because no
   narrowly-scoped review can structurally catch what falls outside it.

## Related Documents

`docs/releases/v0.5.0/CHANGELOG.md`; `docs/releases/v0.5.0/Release
Notes.md`; `docs/releases/v0.5.0.md`; `docs/releases/v0.5.0/
ReleaseChecklist.md`; every `WP 5.0A`–`WP 5.3` retrospective;
`docs/governance/Quality/Technical Debt Register.md`;
`docs/security/Security Roadmap.md`; `docs/releases/v0.4.0/Risks.md`.
