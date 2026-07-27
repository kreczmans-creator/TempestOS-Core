# Failure Isolation Across TempestOS

## 1. Introduction

TempestOS has, by v0.4.0, made the same fundamental decision four separate
times, in four separate contexts, and reached the same answer every time —
without ever mechanically copying the previous decision. This document
collects those four decisions together, side by side, because reading them
in isolation (as four separate ADRs, in four separate work packages) hides
something worth seeing clearly: a single, recurring engineering question,
answered consistently, by actually re-deriving the reasoning each time
rather than assuming the previous answer must transfer.

## 2. Purpose

To state, once, the question every one of TempestOS's failure-isolation
decisions actually answers — *when something fails, what is the smallest
"blast radius" of the failure, and why* — and to show a reader all four
worked examples together, so the *pattern* is visible, not just four
individually-reasonable rules that happen to look similar.

## 3. Background

A system that runs more than one independent thing at a time — modules,
plugins, event subscribers, background services — has to decide, for each
kind of thing, what happens when one of them fails. Get this wrong in one
direction (isolate everything, unconditionally) and a genuine defect in the
platform itself gets silently absorbed as if it were routine. Get it wrong
in the other direction (treat everything as fatal) and one broken module,
one malformed plugin, or one buggy event handler takes the whole platform
down with it. TempestOS's answer, in every one of the four cases below, is:
draw the boundary at the smallest unit that can be *reasoned about*
independently, and treat a failure at that unit as fully contained — unless
the failure is actually evidence that the containment mechanism *itself* is
broken, which is a different, and always Host-fatal, category.

## 4. The Problem

For each new category of "thing that runs and can fail" TempestOS has
added, the same three questions have recurred:

1. Is a failure here isolated (contained to the one thing that failed) or
   fatal (the whole platform stops)?
2. Is there a legitimate reason for *this specific instance* of the failing
   thing to escalate its own failure to fatal — an opt-in?
3. What residual category of failure, if any, remains fatal even when the
   general rule is "isolated"?

## 5. The Design

**Case 1 — Modules (ADR-0013, WP 2.3/2.7A).** A platform-service failure
(Configuration, Logging, Discovery, Registration, Dependency Injection) is
Host-fatal — there is no coherent "partial platform." A module's own
lifecycle failure is isolated — logged, marked `Failed`, the batch
continues, the Host still reaches `Running`. This is the foundational
instance of the pattern; every later case either extends it directly or is
measured against it.

**Case 2 — Background Services (ADR-0021, implemented `WP 4.5`).** Isolated
by default, exactly like a module — but with a genuine, deliberate opt-in: a
service may declare itself `ICriticalBackgroundService`, escalating its own
failure to Host-fatal, for both `StartAsync` and `StopAsync` symmetrically.
The opt-in exists here specifically because a background service is a live,
running, self-supervising component capable of a real self-assessment ("my
own failure means the platform itself is no longer trustworthy") — a
precondition the next two cases do *not* share.

**Case 3 — Plugins (ADR-0025, WP 4.2B/4.2).** Isolated for every one of
eleven named failure categories (malformed manifest, incompatible version,
missing assembly, and so on) — with exactly one Host-fatal exception: a
genuine defect in Plugin Discovery/Loading's *own* orchestration, not
attributable to any specific plugin. A per-plugin critical opt-in
(mirroring Case 2) was considered and explicitly rejected (RD-0011): a
manifest is read before any plugin code has ever executed, so the
"self-assessing, live component" precondition that justifies Case 2's
opt-in simply does not hold here.

**Case 4 — Event Subscribers (ADR-0028, WP 4.4D).** Isolated
*unconditionally* — no opt-in at all, stricter even than Case 3. A
subscriber's own exception is caught, logged at `Error`, and never rethrown
to the publisher or the Host, full stop. This is deliberately the simplest
of the four rules, and the reasoning for omitting an opt-in echoes Case 3's
own (RD-0022): a synchronously-invoked event handler, called by something
that already exists and is already running, does not have the "live,
self-assessing component" quality that justifies Case 2's mechanism either.

## 6. Alternatives Considered

**One single, uniform isolation rule applied identically to all four
cases**, skipping the case-by-case analysis. Rejected, implicitly, every
time: Case 2's opt-in exists precisely *because* a background service is
shaped differently from a module in a way that matters (it runs
unsupervised, indefinitely); Cases 3 and 4 each explicitly re-examined
whether that same opt-in should transfer, and both times found it should
not, for reasons specific to when and how the failure can occur. A single
rule "applied by analogy" would have missed both of these genuine
distinctions.

**Escalating every module/plugin/subscriber failure to Host-fatal**, on the
theory that "if it's broken, the platform shouldn't hide it." Considered
and rejected as far back as ADR-0013 itself — a platform-service failure
and an individual module's failure are different categories of event, and
collapsing the distinction removes the platform's ability to tell "a
plugin misbehaved" from "the ground itself gave way" (see `FOUNDATION.md`'s
own fourth non-negotiable principle).

## 7. Why This Solution Was Chosen

Every one of the four cases was decided by asking the same question freshly
— *does this specific kind of failing thing have the properties that would
justify a critical/non-critical distinction?* — rather than by pattern-
matching against the most recent prior decision. Case 2 answered yes, for a
stated, specific reason (self-supervision). Cases 3 and 4 each answered no,
for their own stated, specific reason, arrived at independently even though
both cases could easily have been assumed to "obviously" mirror Case 2.

## 8. Architectural Principles

- **Fail Fast, applied at the right granularity** — a failure is reported
  immediately and loudly, but *at the boundary of the thing that actually
  failed*, not one level up.
- **The platform-service/module failure boundary** (ADR-0013) — the
  ancestor every later case either extends or is deliberately measured
  against and found different.
- **Avoid Speculative Design** — an opt-in mechanism is added only where a
  real, stated precondition justifies it (Case 2), never introduced by
  default "in case it's needed later" (Cases 3 and 4 both explicitly
  declined it).

## 9. Benefits

- A reader who understands Case 1 (modules) already has most of what they
  need to predict, correctly, how a new failure category *should* be
  designed — and, just as importantly, has a concrete pattern (Cases 3/4)
  for recognising when the obvious-looking analogy (an opt-in "because
  background services have one") does not actually transfer.
- Four independent decisions reaching a consistent, principled pattern —
  rather than four unrelated, ad hoc rules — is itself evidence that
  ADR-0013's original reasoning was sound enough to bear repeated, honest
  re-examination rather than needing to be taken on faith each time.

## 10. Trade-offs

- Four separate ADRs (0013, 0021, 0025, 0028) each carry their own version
  of this reasoning — a reader wanting the complete picture has, until this
  document, needed to read all four independently and notice the pattern
  themselves.
- The asymmetry itself (Case 2 gets an opt-in; Cases 3 and 4 do not) is a
  genuine, ongoing cognitive cost for a new reader, exactly as ADR-0004's
  permissive-disposal asymmetry is (Case Study 03) — mitigated by
  documenting *why* the asymmetry is deliberate, not eliminated by
  pretending it doesn't exist.

## 11. Common Mistakes

The mistake this document exists to help a future engineer avoid: assuming
a new "thing that runs and can fail" should get whichever isolation rule
the most recently designed category received, without checking whether the
justifying precondition (self-supervision, in Case 2's instance) actually
holds for the new case. Twice already (Cases 3 and 4), a superficially
similar opt-in was seriously considered and correctly rejected because the
underlying precondition didn't hold — this is the recurring lesson, not a
one-off finding specific to plugins or to events.

## 12. Future Evolution

Any future "thing that runs and can fail" — a Command Framework handler, a
Navigation transition, a future Requirements/Project Engine capability —
should be run through this same test before its own failure model is
decided: is it isolated by default; does a genuine, stated precondition
justify a critical opt-in; is there a residual, always-fatal category for a
defect in the containment mechanism itself. All four existing cases answer
these three questions explicitly, in writing, before implementation began —
the next one should too.

## 13. Key Takeaways

1. TempestOS has one recurring failure-isolation question, asked honestly
   four separate times, not four unrelated rules that happen to rhyme.
2. An opt-in escalation mechanism (Case 2's critical flag) is justified by
   a specific property (self-supervision) — not by "this concept feels
   similar to one that already has an opt-in."
3. The strongest evidence an architectural principle is sound is that it
   survives being re-examined honestly, more than once, by people willing
   to conclude "no, this case is different" rather than applying the
   previous answer by default.
