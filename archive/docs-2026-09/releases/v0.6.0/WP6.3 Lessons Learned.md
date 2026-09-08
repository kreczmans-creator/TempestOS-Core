# WP 6.3 — REST API — Lessons Learned

## 1. When a Prior ADR Names an Explicit Revisit Trigger, Test the Deferred Alternative — Don't Just Re-Argue It

`ADR-0044`'s own `WP 6.1` reasoning tentatively favoured
`AsyncLocal<T>` before deciding against it, explicitly naming `WP 6.3`
as the point to revisit that choice once genuine request concurrency
existed. It would have been easy to arrive at this Work Package and
simply re-read that reasoning, conclude "the original argument still
sounds right," and move on. Instead, the deferred alternative was
actually built and run against the full existing suite — and it
regressed 17 tests, a concrete, measurable fact no amount of re-reading
the original prose would have surfaced. **Lesson: an explicit revisit
trigger is an invitation to gather new evidence, not just to reconsider
old reasoning with the benefit of hindsight.**

## 2. The Safest Fix for Shared Mutable State Under Concurrency Is Often Not Touching It At All

The obvious-seeming fix for "ambient state isn't safe under
concurrency" is to make the ambient state itself concurrency-safe
(locks, `AsyncLocal<T>`, etc.). This Work Package's own actual solution
was different: `ApiRequestHandler` never establishes the ambient
principal in the first place, resolving a purely local `IPrincipal`
value instead and passing it explicitly wherever it's needed. No
locking strategy, no per-flow isolation mechanism, no new concurrency
primitive — just avoiding the shared state entirely for the one new
concurrent caller. **Lesson: before reaching for a concurrency-safety
mechanism, ask whether the new caller actually needs to touch the
shared state at all, or whether an existing, non-mutating alternative
(here, `IIdentityService.GetPrincipal`) already provides everything
required.**

## 3. Confining a New External Dependency Requires an Explicit, Verified Boundary, Not Just a Design Intention

`ADR-0049`'s own decision to adopt ASP.NET Core "confined to
`RestApiHostedService`" could easily have been just a stated intention
that quietly slipped — `WebApplication`'s own internal DI container is
right there, resolvable via `HttpContext.RequestServices`, and nothing
stops a future contributor from reaching for it out of convenience.
This Work Package verified the boundary directly (grepping for any
`Tempest.Core` type resolved through it — none found) rather than
trusting the stated intention alone. **Lesson: a stated architectural
boundary is a claim; verifying it against the actual compiled code is
what makes it a fact.**

## 4. A Command Exposed Over REST Inherits Whatever That Command Already Does — Including Its Own Assumptions

`ReportingSampleModule.GenerateSampleReportCommandId` was written in
`WP 6.0` with no REST API in mind at all — it happened to already
integrate Identity, Settings, Audit, and Notifications. Exposing it
over HTTP required zero changes to it, which is exactly the payoff of
`ADR-0048`'s own "dispatch through the existing Command Framework"
decision. But it also meant this Work Package inherited that command's
own audit-attribution assumption (ambient-principal-based) without
having designed for it — the mismatch between "REST doesn't establish
ambient state" and "this command's own downstream call assumes it
might be set" is precisely `TD-15`. **Lesson: exposing an existing
command over a new transport is not risk-free just because no code
changes — the new transport's own behavioural assumptions can still
collide with the exposed command's own, even when neither one's code
was touched.**

## 5. A Governance Register Can Go Unnoticed-Stale Even When the Very Thing It Tracks Has Already Happened

`Hosted Services Register.md`'s own "zero production hosted services
exist" text survived, unnoticed, through the entirety of `WP 6.2`,
which shipped this codebase's first one. This is not a subtle
undercount like a total-count arithmetic drift — it is a register whose
own central claim became flatly false the moment `WP 6.2` merged, and
nothing caught it until this Work Package's own repository review.
**Lesson: a register's own "Coverage Status: Partial, reason: nothing
exists yet" framing needs to be re-checked against the *current* file
system every time a Work Package in its own subject area ships, not
just when that register's own Review Frequency happens to be
triggered by something else.**

## Related Documents

`WP6.3 Implementation Report.md`; `WP6.3 Engineering Review Report.md`;
`WP6.3 Platform Integration Demonstration.md`; `WP6.3 Platform Impact
Assessment.md`; `WP6.3 Technical Debt Assessment.md`; `WP6.3 Future
Capability Recommendations.md`; `docs/academy/03 Work
Packages/WP6.3-rest-api-implementation.md`; `ADR-0049`; `ADR-0052`.
