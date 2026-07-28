# Failure Isolation Across TempestOS

## 1. Introduction

TempestOS has, by v0.4.0, made the same fundamental decision four separate
times, in four separate contexts, and reached the same answer every time —
without ever mechanically copying the previous decision. `WP 5.1A`
(Command Framework Architecture) later ran a fifth case through the
identical test and, for the first time, reached a genuinely *different*
answer — see Case 5, below. This document collects all five decisions
together, side by side, because reading them in isolation (as five
separate ADRs, in five separate work packages) hides something worth
seeing clearly: a single, recurring engineering question, answered
honestly each time, by actually re-deriving the reasoning rather than
assuming the previous answer must transfer — and, at least once,
concluding that it should not.

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

**Case 5 — Command Dispatch (ADR-0038, WP 5.1A).** The first case where
the answer is neither "isolated" nor "no new case needed" (compare
Navigation, below) but a third, deliberately different outcome:
**propagate, do not isolate.** A command handler's own exception is
*not* caught by `ICommandDispatcher`/`ICommandRegistry` — it propagates
directly to whatever called `DispatchAsync`/`InvokeAsync`. The reasoning
is the same three-question test as every prior case, honestly re-asked:
is a command handler's failure isolated or fatal? Neither, in the sense
Cases 1–4 mean it — it is the *caller's own concern*, because a command
"has an expected result" (the property that already distinguishes it
from an event, per the Engineering Glossary): the caller genuinely needs
to know whether the command it asked for succeeded, in order to react.
Isolating a command failure the way an event subscriber's is isolated
would make "expected result" a fiction — the caller would never learn
the command failed at all.

## 6. Alternatives Considered

**One single, uniform isolation rule applied identically to all five
cases**, skipping the case-by-case analysis. Rejected, implicitly, every
time: Case 2's opt-in exists precisely *because* a background service is
shaped differently from a module in a way that matters (it runs
unsupervised, indefinitely); Cases 3 and 4 each explicitly re-examined
whether that same opt-in should transfer, and both times found it should
not, for reasons specific to when and how the failure can occur. Case 5
went further still — not merely declining an opt-in, but rejecting
isolation itself as the default. A single rule "applied by analogy"
would have missed all three of these genuine distinctions.

**Escalating every module/plugin/subscriber failure to Host-fatal**, on the
theory that "if it's broken, the platform shouldn't hide it." Considered
and rejected as far back as ADR-0013 itself — a platform-service failure
and an individual module's failure are different categories of event, and
collapsing the distinction removes the platform's ability to tell "a
plugin misbehaved" from "the ground itself gave way" (see `FOUNDATION.md`'s
own fourth non-negotiable principle).

## 7. Why This Solution Was Chosen

Every one of the five cases was decided by asking the same question freshly
— *does this specific kind of failing thing have the properties that would
justify a critical/non-critical distinction, or isolation at all?* —
rather than by pattern-matching against the most recent prior decision.
Case 2 answered yes, for a stated, specific reason (self-supervision).
Cases 3 and 4 each answered no, for their own stated, specific reason,
arrived at independently even though both cases could easily have been
assumed to "obviously" mirror Case 2. Case 5 asked the same question and
found that isolation itself was the wrong default: a command's own
"expected result" property means its caller must observe failure, not
have it absorbed on their behalf.

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
- Five independent decisions reaching a consistent, principled pattern —
  rather than five unrelated, ad hoc rules — is itself evidence that
  ADR-0013's original reasoning was sound enough to bear repeated, honest
  re-examination rather than needing to be taken on faith each time, and
  robust enough that a genuinely different answer (Case 5) could emerge
  from the same test without the test itself needing to change.

## 10. Trade-offs

- Five separate ADRs (0013, 0021, 0025, 0028, 0038) each carry their own
  version of this reasoning — a reader wanting the complete picture has,
  until this document, needed to read all five independently and notice
  the pattern themselves.
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

Navigation (`WP 5.0A`) was run through this same test first, with a
genuinely different outcome from Cases 1–4 above: it needs **no new
failure model at all**. A module's own navigation-registration failure
(a duplicate `Id`) happens *inside* that module's own `InitialiseAsync`,
already fully governed by Case 1's own isolation — Navigation introduces
no new *kind* of failure, only a new call site for a kind this document
already covered, so no new case was needed for it. Command dispatch
(`WP 5.1A`) was run through the identical test next, and this time the
outcome *was* a genuinely new case (Case 5, above) — a command handler's
failure is neither isolated nor treated as Host-fatal; it propagates to
the caller, because "an expected result" requires the caller to observe
it. Together, Navigation and Commands are themselves a useful data
point: not every new platform capability needs its own entry in this
list, but assuming *in advance* which outcome a new capability will
reach — "no new case" or "a genuinely new rule" — would have been
guessing in both directions. Any future "thing that runs and can fail" —
a future Requirements/Project Engine capability, a future Diagnostics
background probe — should still be run through the same test before
assuming any outcome: is it isolated by default; does a genuine, stated
precondition justify a critical opt-in; is there a residual, always-fatal
category for a defect in the containment mechanism itself; does an
existing case already answer all three questions without needing a new
one (Navigation's own outcome); or does the failure need to propagate to
an observing caller instead of being isolated at all (Case 5's own,
newest outcome).

## 13. Key Takeaways

1. TempestOS has one recurring failure-isolation question, asked honestly
   five separate times, not five unrelated rules that happen to rhyme —
   and the fifth asking (Case 5) is proof the question is still being
   asked honestly, not merely repeated: it produced a genuinely different
   answer.
2. An opt-in escalation mechanism (Case 2's critical flag) is justified by
   a specific property (self-supervision) — not by "this concept feels
   similar to one that already has an opt-in."
3. The strongest evidence an architectural principle is sound is that it
   survives being re-examined honestly, more than once, by people willing
   to conclude "no, this case is different" rather than applying the
   previous answer by default.
