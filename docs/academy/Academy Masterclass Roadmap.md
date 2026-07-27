# Academy Masterclass Roadmap

## Purpose

The Academy's existing documents are each scoped narrowly and deliberately —
a work package retrospective, a principle, a pattern, a case study — by
design (Engineering Governance §6). A **Masterclass** is a different kind of
document: long-form, synthesising material that is currently scattered
across many of those narrower documents into one complete, guided narrative
on a single large subject, suitable for someone who wants to genuinely
*master* that subject in TempestOS's own terms, not just look up one fact
about it.

This roadmap identifies which subjects currently justify a Masterclass,
ranks them by educational value, and states what each would need to draw
on. **None of these are written yet.** Writing one is a deliberate,
separate, future decision — this document exists so that decision can be
made with a clear view of the candidates and the cost of each, not
invented from scratch when the need arises.

## How to Use This Document

Before starting a Masterclass, re-read this entry's own "What It Would Draw
On" list and confirm it is still accurate — new work packages since this
roadmap was written may have added or changed relevant material. A
Masterclass that goes stale the moment it's written is worse than no
Masterclass at all (the same principle that governs the Platform Service
Map and the Engineering Glossary — Governance §6). Update this roadmap,
not just the Masterclass itself, whenever a Masterclass is written, retired,
or its scope changes.

---

## Priority 1 — Highest Educational Value

### 1. Building a Modular Runtime Platform, End to End

**Why this ranks first.** This is the single subject every other topic in
the Academy is, in some sense, already in service of — and no document
currently walks a reader through the *whole* thing, start to finish, as one
continuous story. A reader who completes this Masterclass should be able to
explain TempestOS's entire architecture, from "why does a module need a
public parameterless constructor" all the way to "why is the Event Bus
DI-public but Discovery is not," as one coherent design, not a collection
of independently-justified pieces.

**What it would draw on.** The Module Pipeline; The Startup Sequence;
Working with the TempestOS Host; Platform Layering; every WP 2.x and WP 4.x
retrospective, read as chapters of one story rather than independent
documents; all four Failure Isolation cases, read together.

**Estimated scope.** Very large — this is the "textbook," not a chapter.
Realistically the last Masterclass to actually write, not the first,
precisely because it depends on every other subject below already being
well-understood and stable.

### 2. Designing a Runtime Host

**Why this ranks highly.** `TempestHost` is the single most architecturally
dense component in the platform — six independent platform services,
orchestrated, with its own state machine, its own failure model, and its
own shutdown discipline. "Working with the TempestOS Host" (the new
Academy concept guide) is the *first-read* version of this; a Masterclass
would be the *deep* version — including the genuine tensions found during
implementation (the brief-vs-architecture composition-root disagreement;
the diagram-vs-transition-table shutdown ambiguity) as worked examples of
how to resolve an ambiguity in an already-frozen design without reopening
it casually.

**What it would draw on.** Runtime Host Architecture.md, Host Lifecycle.md,
Runtime State Machine.md, Shutdown Sequence.md, Failure Behaviour.md,
Ownership Matrix.md, the WP 2.7/2.7B retrospectives, ADR-0011 through
ADR-0019, and Background Services Architecture.md, the WP 4.5 retrospectives
(architecture and implementation), and ADR-0021/ADR-0029/ADR-0030, which
extend the Host's own phase table a second time and are a directly
relevant worked example of inserting new phases into an already-frozen
design without reopening it.

**Estimated scope.** Large, but self-contained — unlike Priority 1, this
does not depend on every other subject being finished first.

### 3. Event-Driven Systems in TempestOS

**Why this ranks highly.** The Event Bus is the platform's first genuinely
new cross-module communication mechanism, and it embodies a real, subtle
design space (dispatch ordering, re-entrancy, failure isolation,
subscription lifetime) that a working engineer will encounter again in any
future event-driven system they build, inside or outside TempestOS. Unlike
some other candidates, the *practical* guide already exists (Building an
Event-Driven Module) — what's missing is the *design-space* treatment: why
snapshot-based dispatch makes re-entrancy safe without a queue, why
subscription is imperative rather than DI-auto-discovered, and how each of
these connects to general event-driven-architecture theory a reader may
already know from outside TempestOS.

**What it would draw on.** ADR-0020, ADR-0028, Event Bus Architecture.md,
the WP 4.4/4.4D/4.4E retrospectives, Building an Event-Driven Module,
RD-0019 through RD-0022.

**Estimated scope.** Medium — the practical and architectural material
already exists in full; a Masterclass here is primarily a synthesis and
deepening exercise, not new research.

---

## Priority 2 — Strong Educational Value, Narrower Audience

### 4. Plugin Architecture and Dynamic Loading

**Why this ranks here.** Genuinely valuable, and the concept guide (Plugin
Architecture) already gives a strong first read — but the audience for a
*deep* treatment (assembly loading, `AssemblyLoadContext` boundaries,
manifest-before-load design) is narrower than the Priority 1 candidates:
primarily engineers building or extending the plugin system itself, not
every engineer touching the platform.

**What it would draw on.** Plugin Manifest Architecture.md, ADR-0025,
ADR-0026, the WP 4.2 family of retrospectives, Plugin Architecture (concept
guide), RD-0008 through RD-0014.

**Estimated scope.** Medium.

### 5. Dependency Injection: From Container to Constructor-Injected Modules

**Why this ranks here.** TempestOS's DI story spans two genuinely separate
eras — WP 2.4's original container, and WP 4.4A/4.4B's later, additive
extension letting a *discovered* module participate in it — connected by a
real, non-obvious mechanical constraint (Discovery's own metadata probe).
A Masterclass here would be valuable specifically for anyone designing a
*third* DI-adjacent capability, but the existing Engineering Principle
document plus the two work package retrospectives already cover the
material reasonably completely for most other readers.

**What it would draw on.** The Dependency Injection principle document,
WP 2.4's retrospective, Module Dependency Injection Architecture.md,
WP 4.4A/4.4B's retrospectives, ADR-0005 through ADR-0009, ADR-0027.

**Estimated scope.** Medium.

---

## Priority 3 — Valuable, but Better Served by Existing Material for Now

### 6. Engineering Governance as a Discipline

**Why this ranks lower.** Engineering Governance.md is already,
effectively, close to Masterclass-length and Masterclass-depth on its own
terms — it is a complete, standalone constitution, not a summary pointing
elsewhere. A separate Masterclass would mostly restate it. **Revisit this
only if** a future need emerges to teach the *process* to an audience
broader than TempestOS's own contributors (a general "how to run an
architecture-first engineering programme" piece, reusable beyond this
specific project) — that would be a genuinely different document, not a
restatement.

### 7. Platform Evolution: Reading a Codebase's Own History

**Why this ranks lower, but is worth naming.** A retrospective-of-
retrospectives — how TempestOS's own engineering discipline itself evolved
(the Rejected Designs Log's introduction partway through; the Academy's own
growth from four documents to the current, much larger set) — is a
genuinely interesting subject, but it teaches process archaeology more than
it teaches transferable engineering skill, and depends on the codebase's
history being substantially "finished" to tell well. **Revisit this after
v0.4.0 ships**, when the full arc has a natural ending point.

---

## What This Roadmap Deliberately Does Not Include

Every subject explicitly listed in the brief that prompted this roadmap
was considered; two are intentionally absent from the numbered list above
because a Masterclass would not currently add value over what already
exists:

- **A standalone "Dependency Injection" Masterclass covering only the
  general principle** — the Engineering Principles document already
  covers this at the right depth for a general audience; see Priority 2,
  item 5, for the TempestOS-specific extension that *would* justify one.
- **A standalone "Engineering Governance" Masterclass restating the
  existing document** — see Priority 3, item 6.

## Maintenance

Update this roadmap whenever: a new subject accumulates enough scattered
material across several work packages to become a genuine candidate; an
existing candidate's "What It Would Draw On" list changes because the
underlying material was rewritten or consolidated; or a Masterclass from
this list is actually written, in which case move it to a "Written"
section (to be added) rather than deleting the entry.
