# WP 9.0A — Mechanical Product Structure — Lessons Learned

## Purpose

Records what went well, what was harder than expected, and what a
future Work Package facing a similar situation should know going in.

## What Went Well

**The domain data already existed.** `WP8.2C` had already given
`Project`/`Assembly`/`SubAssembly`/`Part`/`Component`/`Configuration`
real, tested concrete classes — this Work Package never had to write or
test a single new canonical object class, only extend what already
existed. The Canonical Object Catalogue's own stale "Conceptual" marking
for these six (corrected here, not silently) was the only sign this
had already happened; worth checking a Kind's own `Implementation/`
folder directly, not just the catalogue document, before assuming a gap.

**The Kind-keyed extension model generalised cleanly to a third
concern.** `IPropertyFacetProvider` (`ADR-0082`) needed no new pattern —
copying `IProjectExplorerNodeProvider`'s own exact shape, then reusing
`WorkspaceManager`'s own existing `TryAdd`/`DuplicateWorkspaceRegistrationException`
implementation pattern verbatim, took a fraction of the effort a novel
design would have.

**Existing tests caught real regressions immediately.** Adding two new
sample modules bumped a hardcoded module-discovery count from 22 to 24;
`EngineeringCockpit`'s own placeholder-text assertions broke the moment
real data replaced them. Both were exactly the kind of fast, precise
feedback a 1600+-test suite exists to give.

## What Was Harder Than Expected

**Composition-root timing.** Every prior Workspace registration
(`RegisterView`/`RegisterExplorerArea`) was designed against fixed
sample content needing nothing from a running Host. This Work Package's
own registrations need `EngineeringDomainContext`/`ICommandDispatcher`/
`ICommandRegistry`, all three only resolvable after the Host starts —
which `Program.cs`'s own original, single `shell.RunAsync()` call did
not expose a seam for. Splitting it into `StartAsync()` / register /
`RunInputLoopAsync()` / `StopAsync()` was the fix, and turned out to
already be a tested pattern (`WorkspaceManagerTests.RegisterView_AfterStartAsync_IsStillHonoured`)
— worth checking existing tests for a pattern before assuming a genuinely
new one is needed.

**Diagnosing `Areas: 0`.** What first looked like a bug in this Work
Package's own new registration code turned out, after reproducing it
against a disposable `git worktree` of the unmodified `v0.8.0` tag, to
be a pre-existing Runtime Host characteristic no prior test had ever
exercised (`TD-26`) — no prior Workspace test had ever combined
`WorkspaceManager` with a real, data-seeding module. Confirming
"pre-existing, not mine" took real, deliberate verification (a second
checkout, a second build, a second run), not assumption — worth doing
whenever a suspicious finding could plausibly be either.

**Choosing where "delete" lives.** The instinct to add a `Deleted`
`LifecycleState` member was strong (it already has `Cancelled`/
`Archived`) and wrong — `LifecycleState` is shared platform-wide, and a
structural fact is not a lifecycle stage. Landing on a separate
`IDeletable` facet took deliberately working through `ADR-0074`'s own
existing reasoning first.

## Process Observations

Reading `WP8.1B`/`WP8.1C`'s own disclosed limitations
("no `IWorkspaceCommand` is implemented," "every facet is Id/Kind only")
directly named exactly what this Work Package needed to build — a
disclosed limitation in this project's own documentation reliably means
"here is the next Work Package's own starting point," not "here is a
permanent design boundary."

## Recommendation for Future Work Packages

Before assuming a Domain gap needs a contract redesign, check whether an
*additive* facet — composed only where actually needed, implemented
once in `EngineeringObjectBase`, disclosed via its own ADR — already
covers it; `ADR-0075`'s own composition model was designed to make this
the default answer, not the exception.

## Related Documents

`WP9.0A Implementation Report.md`; `WP9.0A Technical Debt Assessment.md`
(`TD-26`); `ADR-0080`; `ADR-0081`; `ADR-0082`.
