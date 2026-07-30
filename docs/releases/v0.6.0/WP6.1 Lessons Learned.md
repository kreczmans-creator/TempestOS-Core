# WP 6.1 — Permissions & Identity — Lessons Learned

## 1. A Tentative Architectural Suggestion Is Not a Ratified Decision

`Platform Service Contracts.md`'s own Thread Safety Expectations said
`CurrentPrincipalAccessor` "likely requires an `AsyncLocal<T>`-backed
implementation... a specific design point `WP 6.1`'s own architecture
phase must resolve explicitly." Reading this carefully — not skimming
past the hedge word "likely" — mattered: implementing `AsyncLocal<T>`
first and discovering the problem afterward would have cost a rewrite
plus a shaken sample module. Verifying the suggestion with a small,
direct prototype test before committing to it caught the problem before
any dependent code was written against it. **Lesson: when an
architecture document hedges its own recommendation, treat that hedge
as an instruction to verify, not a formality to skip.**

## 2. "Positioned to Resolve" and "Resolved" Are Different Claims, and the Difference Matters

The Contract Review's own Technical Debt Assessment already drew this
distinction in advance ("Positioned for resolution via `WP 6.1`/
`ADR-0044` — not resolved by this document"). Implementation confirmed
why the distinction was worth drawing early: building the enforcement
mechanism (`IPermissionEvaluator`) is genuinely different work from
retrofitting three other, already-shipped services to call it, and
conflating the two in a retrospective or status update would have been
a real, avoidable overclaim. **Lesson: when an architecture-phase
document pre-emptively hedges a claim, honor that hedge literally during
implementation rather than letting an implementation's own momentum
round it up to "done."**

## 3. Deferred Design Questions Should Be Named, Not Rediscovered

The original `Public Interface Catalogue.md` explicitly listed
`IIdentityService`'s own principal-population mechanism as undefined —
"the mechanism a future... login flow... must still define." Because
this was named explicitly rather than silently absent, implementing it
was a planned task with a known shape to fill, not a surprise discovered
mid-implementation. **Lesson: an architecture document that names its
own gaps explicitly is more useful to its own implementation phase than
one that looks complete but silently omits a hard question** — the gap
itself is a deliverable.

## 4. Fail-Closed Defaults Simplify More Than They Cost

Choosing "unrecognized identity resolves to zero permissions" over
"unrecognized identity throws" removed an entire category of
exception-handling boilerplate every future caller would otherwise need,
while remaining strictly safer than the alternative (a fail-open
default). The only case that still throws (`RoleNotFoundException`) is
narrowly scoped to an actual configuration defect, not an ordinary,
expected condition. **Lesson: when choosing between "throw for an
absent grant" and "return an inert, harmless default," the harmless
default is very often the better API, provided the one case that is a
genuine defect (not merely "nobody granted anything yet") still throws
loudly.**

## 5. A Living Sample Module Forces Honesty About Defaults

Building `IdentitySampleModule` to demonstrate both the fail-closed
default and the granted path — rather than only the "happy path" — is
what surfaced, concretely, that the ambient `CurrentPrincipalAccessor`
design was required: a sample module establishing a principal during
`InitialiseAsync` and a test later dispatching a command against it are
exactly the "one call chain establishes, a separate one later reads"
scenario `AsyncLocal<T>` cannot support. **Lesson: a real, working
consumer that exercises both the success and failure paths finds design
problems abstract review does not.**

## 6. Re-Deriving Governance Counts Directly Continues to Pay Off

Following `WP 5.4`'s own standing-practice recommendation, this Work
Package re-derived the Namespace Register's file count directly rather
than incrementing the previously-stated figure — and found a genuine,
year-old-in-project-time gap (`WP 5.3`'s own template file, never
counted). **Lesson: this is now the fourth or fifth consecutive Work
Package to find real drift this way; the practice is earning its keep
and should keep being applied, not treated as a one-off `WP 5.4`
artifact.**

## Related Documents

`WP6.1 Implementation Report.md`; `WP6.1 Engineering Review Report.md`;
`WP6.1 Technical Debt Assessment.md`; `WP6.1 Future Capability
Recommendations.md`; `docs/academy/03 Work Packages/
WP6.1-permissions-and-identity-implementation.md`; `ADR-0043`;
`ADR-0044`.
