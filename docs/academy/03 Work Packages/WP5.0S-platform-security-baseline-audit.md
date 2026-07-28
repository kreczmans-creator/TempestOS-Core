# WP 5.0S — Platform Security Baseline Audit

## 1. Introduction

WP 5.0S is TempestOS's first comprehensive security audit — every
production file across `Tempest.Core`, `Tempest.App`, and
`Tempest.Samples`, reviewed with a security lens for the first time.
Unlike every other Work Package in this project's history, it built no
new feature and changed no approved architecture. Its output is four new
standing documents under `docs/security/`, two governance-register
updates, one small, isolated code fix, and this retrospective — which,
unusually for this series, is not only a record of what happened but a
short course in *why* security engineering works the way it does, for a
reader who has never done this before.

## 2. Purpose

To establish the **v0.5.0 Security Baseline**: a single, honest,
comprehensive statement of TempestOS's current security posture, against
which every future Work Package can be compared. Not a penetration test
against a finished product — TempestOS is nowhere near finished — but a
disciplined first look at a platform that is still small enough to
examine completely, before it grows too large to.

## 3. Background

Every prior Work Package built something and then, at most, reasoned
about that one thing's own failure modes (ADR-0021's failure
classification, ADR-0025's plugin failure categories, and so on). None
had asked the different question this Work Package asks: taken as a
whole, across every layer built so far, what could go wrong, for whom,
and why would it matter? That question requires a **threat model** — a
description of assets, actors, and trust boundaries — before it can be
answered, because "is this secure?" is meaningless without first
answering "secure against what, and for whom?"

## 4. The Problem

### What a threat model actually is

A threat model is not a checklist of vulnerabilities. It is three simple
questions, answered in order, *before* looking at any code:

1. **What are we protecting?** (Assets — data, capability, availability.)
2. **Who, or what, are we protecting it from?** (Actors — and, critically,
   what each actor is currently trusted to do.)
3. **Where does trust actually change hands?** (Trust boundaries — the
   places in the system where code stops trusting its caller and starts
   validating instead.)

`Threat Model.md` answers all three for TempestOS as it is today, and as
its own stated future ambitions (multi-user, authentication, third-party
plugins, cloud sync) will eventually require it to be. Only once that
model exists does "is this secure?" become answerable — and the honest
answer, for most of this codebase, turned out to be "yes, because there
is currently very little to attack" — followed by "but here is exactly
where that stops being true."

### Why "no vulnerabilities found" and "real findings exist" are not in tension

A single-user, offline, unauthenticated desktop application has a
narrow attack surface almost by definition — there is no network caller,
no other user, and (so far) no secret worth stealing. This audit found no
Critical or High severity vulnerability, and that finding is genuine, not
a whitewash: it follows directly from what TempestOS does not yet do.
What this audit *did* find is a set of architectural decisions that are
entirely reasonable today, and will stop being reasonable the moment one
of `Threat Model.md`'s own "eventually" assumptions goes live. That
distinction — a decision being *correct for today* while also being
*future security debt* — is the single most important concept this Work
Package had to hold in mind throughout.

## 5. The Design (Methodology)

The audit worked in three passes, deliberately in this order:

1. **Read before judging.** Every previously-unreviewed source file
   (Configuration, Logging, Plugins, Versioning, bootstrap-era code, the
   remaining Dependency Injection extensions) was read in full, alongside
   a second pass over the subsystems most central to trust-boundary
   questions (`TempestServiceProvider`, `ReflectionFrameworkDiscoveryService`,
   `EventBus`, `HostedServiceManager`, `TempestHost`, `NavigationService`,
   `RuntimeModuleManager`). Reading code with "what could this do that its
   author didn't intend?" in mind is a genuinely different exercise from
   reading it to understand what it *does* do — the same file can be read
   twice, for two different questions, and yield two different sets of
   observations.
2. **Search for known-dangerous patterns.** Targeted searches across
   `src/` for `Assembly.Load`/`Activator.CreateInstance` (dynamic code
   loading), file/path construction (traversal risk), JSON
   deserialization (untrusted-input risk), and hard-coded
   secrets/credentials. This pass is fast and mechanical, and exists to
   catch the kind of mistake that reading alone can miss simply through
   volume — 119 files is a lot to hold in working memory at once.
3. **Classify every candidate finding against the threat model, not in
   the abstract.** A path-traversal bug is not automatically "Critical"
   just because path traversal is a famous vulnerability class — its
   actual severity here depends on what an attacker gains by exploiting
   it, which in turn depends on who the plausible attacker is today
   (nobody — every plugin author is currently the project's own team)
   versus tomorrow (a genuinely untrusted third party, once assumption 7
   in `Threat Model.md` arrives).

## 6. Alternatives Considered

**Fix every finding immediately, regardless of scope.** Rejected. This
Work Package's own brief was explicit: "do not redesign the platform...
only implement fixes where they are clearly isolated, non-breaking, and
do not require architectural redesign." Two findings (the plugin trust
boundary, the navigation ownership gap) would require a genuine
architectural decision — a new isolation model, a new capability concept
— to fix correctly. Fixing them hastily, inside an audit Work Package
that was not scoped or reviewed as an architecture Work Package, would
have produced exactly the kind of un-reviewed, undocumented architectural
change this project's own governance discipline exists to prevent.

**Treat every finding as equally urgent.** Rejected. Severity has to
track real-world exploitability, not just pattern-matching against a
vulnerability's famous name. Calling the plugin manifest path-traversal
gap "Critical" because "path traversal" sounds alarming, when the actor
who could exploit it (today) is the project's own team and the impact
(today) is nothing a fully-trusted actor couldn't already do some other
way, would have been dishonest severity inflation — and would have
buried the two findings that actually matter most (the plugin trust
boundary, and the absence of any secrets-handling convention) under a
false sense of alarm.

**Skip writing a Threat Model and Security Principles document, and only
produce the findings review.** Rejected. A findings list with no
Threat Model behind it invites exactly the severity-inflation problem
above — every reviewer, in every future Work Package, would have to
re-derive "who is the attacker, and why does this matter" from scratch,
inconsistently, each time. Writing the model once, as its own standing
document, is what makes the baseline durable rather than a one-off
snapshot.

## 7. Why This Solution Was Chosen

Four documents, not one, because they answer four different, durable
questions that should not be conflated: what are we protecting and from
whom (`Threat Model.md`), what standing rules govern how we build
(`Security Principles.md`), what did we actually find, this time
(`Platform Security Review v0.5.0.md`), and what should change, and when
(`Security Roadmap.md`). One isolated fix, not six, because five of the
six findings genuinely require a decision this Work Package was not
authorised, or positioned, to make — and pretending otherwise would have
been a bigger governance failure than leaving them open and disclosed.

## 8. Architectural Principles

This section teaches the general security concepts this audit applied,
each grounded in a real, specific TempestOS example — not as abstract
theory, but as decisions this codebase has actually made, for better or
worse.

### Least privilege

*The idea:* a component should be able to do only what it needs to do,
and nothing more — not because it is assumed malicious, but because
limiting what is *possible* limits what a bug, or a future attacker, can
turn into. TempestOS's clearest example: `IRuntimeModuleManager`,
`IModuleLifecycleManager`, and `IFrameworkDiscoveryService` are never
registered into the dependency injection container (ADR-0017). A module
cannot ask the container for "the thing that controls me" because that
thing was never put somewhere a module could ask for it. This is least
privilege applied structurally, not by convention or code review — the
capability simply does not exist to be misused.

### Trust boundaries are about *where trust changes*, not about layers

*The idea:* a trust boundary is not the same thing as an architectural
layer. Two components can sit in different architectural layers
(`Tempest.Core` vs. `Tempest.App`) while trusting each other completely,
and two components can sit in the *same* layer while one should not
trust the other at all. TempestOS's four-layer model (ADR-0023) is a
**layering** boundary — it organises *what depends on what* — and this
audit's single most important finding (`SEC-01`) is that TempestOS has
exactly **one** real internal **trust** boundary (the DI-container
exclusion above) and nowhere else. Once a plugin's assembly loads, it
sits inside the same trust domain as every first-party module — the
layering diagram would show it in a different box, but the trust diagram
would not.

### Isolating failure is not the same as isolating trust

*The idea:* a system can be very good at making sure one component's
*crash* doesn't take down the whole system (reliability), while doing
nothing at all to stop one component from *misusing* another
(security) — these are genuinely different properties, easy to conflate
because both use the word "isolation." TempestOS's plugin failure
classification (ADR-0025) is excellent at the first property: a
malformed manifest, a missing assembly, an incompatible version — none
of these crash the Host. It says nothing at all about the second
property: a plugin whose assembly loads *successfully* is fully trusted
from that point on. This audit is the first document to say that
distinction out loud for this codebase.

### Validate at the boundary, not after

*The idea:* untrusted data should be checked the moment it crosses from
"outside" to "inside" a system — not passed further in and checked
"eventually." This Work Package's own fix (`PL-1`) is a small, direct
example: a plugin manifest's `AssemblyFileName` field is untrusted the
instant it is read from a JSON file on disk, and the containment check
now happens at exactly that point — in `PluginManifestDiscoveryService.
ParseAndValidate`, before the resulting path is used for anything —
rather than, say, inside `PluginAssemblyLoader` where the damage of a bad
path would already be closer to done.

### Severity is relative to a threat model, not absolute

*The idea:* the same code pattern (a path-traversal-shaped bug, an
unauthenticated write, a missing lock) can be Critical in one system and
Informational in another, depending entirely on who can reach it and
what they gain. This audit repeatedly had to resist rating a finding by
how a vulnerability class is *usually* rated in the industry, and
instead rate it by what it actually means for *this* system, *today* —
which is why `PL-1` is Medium rather than Critical, and why several
findings here are Informational rather than omitted entirely: they are
real, worth recording, and currently low-consequence, all at once.

## 9. Benefits

- A durable, written answer to "is TempestOS secure?" that does not need
  to be re-derived from scratch by every future reviewer.
- A concrete, closed gap (`PL-1`) in the plugin manifest pipeline, found
  before any real third-party plugin existed to exploit it.
- Two previously-implicit pieces of debt (the plugin trust boundary, the
  navigation ownership gap) now explicit, named, and tracked in the same
  governance register as every other debt item in the project — no
  longer only "obvious once you think about it," but written down.
- A Security Roadmap that tells a future Work Package exactly when each
  deferred item becomes due, rather than leaving "eventually" to mean
  "whenever someone happens to notice."

## 10. Trade-offs

- This audit is a point-in-time snapshot, not continuous monitoring — it
  is only as current as `2026-07-28`, and a future Work Package that adds
  a new subsystem without checking it against `Security Principles.md`
  could reintroduce a pattern this audit specifically flagged.
- Several findings were deliberately left unfixed, on the reasoning that
  fixing them now would be premature or architecturally out of scope.
  This is the correct trade-off per this Work Package's own brief, but it
  does mean the baseline includes known, disclosed, open items rather
  than a clean slate — a security baseline audit's job is to be honest
  about that, not to hide it behind a clean-looking report.
- Writing four standing documents, rather than one findings list, is
  more governance overhead to maintain going forward — each will need
  its own "Last Reviewed" discipline, the same as every other governance
  register.

## 11. Common Mistakes

Mistakes this audit specifically avoided, written as guidance for future
security work in this codebase:

- **Confusing "no vulnerability found" with "nothing to say."** A clean
  area still gets an explicit "Reviewed. No security vulnerabilities or
  architectural security concerns identified." statement in `Platform
  Security Review v0.5.0.md` — silence would be indistinguishable from
  "not reviewed at all."
- **Rating a finding by its vulnerability class's typical reputation
  instead of its actual exploitability here.** See Architectural
  Principles, above — this is the single easiest mistake to make in any
  security review, and the one this audit corrected for most explicitly.
- **Building the fix for a problem that doesn't exist yet.** No
  secrets-redaction system was built (`SEC-02`) because no secret exists
  in the codebase to redact — building one now would be speculative
  complexity with nothing real to justify it, and would itself become a
  piece of unused, untested machinery that could hide a real bug later.
- **Treating a dead-code finding as a live one.** `FS-1`/`FS-2`
  (the bootstrap-era project-data model) are Informational, not Medium
  or High, specifically because the code they describe is unreachable
  from any entry point in the running application today — a real finding
  about a real risk, but not an active exploit path.
- **Fixing an architectural finding by editing code instead of writing
  an ADR.** `SEC-01` and `NAV-1` are recorded as debt requiring a future,
  dedicated Architecture Work Package — not patched around inside an
  audit Work Package that was never reviewed as an architecture change.

## 12. Future Evolution

`Security Roadmap.md` is this Work Package's own answer to "what
happens next," sequenced against `Threat Model.md`'s own assumptions
rather than against a calendar date. The next Work Package that proposes
third-party plugin support, multi-user operation, authentication, or
cloud synchronisation should read that roadmap's matching item *before*
starting design — each names the specific decision that item requires
and the specific finding in `Platform Security Review v0.5.0.md` that
motivates it.

## 13. Key Takeaways

- A threat model answers "secure against what, for whom" before any
  finding can be meaningfully rated — severity is relative, not absolute.
- Failure isolation and trust isolation are different properties; a
  platform can have excellent reliability isolation and no trust
  isolation at all, and TempestOS currently does.
- "Correct for today, and future debt at the same time" is not a
  contradiction — it is the normal, honest shape of security findings in
  a system that is still early in its own life.
- Fixing what's isolated and disclosing what isn't is more valuable, and
  more honest, than fixing everything hastily or fixing nothing at all.
- A security baseline is only useful if it is written down once, checked
  against by name in every future Work Package's Definition of Done, and
  updated — not re-derived from scratch — as the platform grows.

## Architectural Debt Assessment

Two new debt items were disclosed by this Work Package, both already
recorded in `Technical Debt Register.md`:

- **TD-09** — no isolation boundary between a loaded plugin and a
  first-party module (`SEC-01`). Open; trigger is real third-party plugin
  support; requires a future, dedicated Architecture Work Package.
- **TD-10** — `NavigationService.Unregister` performs no ownership check
  (`NAV-1`). Open; trigger is paired with TD-09's own resolution.

No existing debt item's status changed. `PL-1` (the plugin manifest path
containment fix) was closed within this Work Package and is not carried
forward as debt.

## Observations

This audit is, itself, an instance of a pattern worth naming: a project
that has maintained rigorous governance discipline (ADRs, a Decision
Register, a Technical Debt Register, an Academy) for its architecture had
never yet applied that same discipline specifically to security — not
because security was neglected, but because no Work Package had yet
asked the question "taken as a whole, what could go wrong?" Establishing
that question's answer once, early, while the codebase is still small
enough to read in full, is precisely what makes a *continuously
maintained* security baseline possible, as distinct from the more common
industry pattern of a single penetration test performed just before
release, against a system too large by then to fully understand.

## Related Documents

`docs/security/Threat Model.md`; `docs/security/Security Principles.md`;
`docs/security/Platform Security Review v0.5.0.md`;
`docs/security/Security Roadmap.md`; `Technical Debt Register.md`;
`Decision Register.md`; ADR-0017, ADR-0021, ADR-0023, ADR-0025,
ADR-0026, ADR-0028, ADR-0029, ADR-0032, ADR-0034.
