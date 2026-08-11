# ADR-0101: `Tempest.App`/`WorkspaceShell` Is TempestOS's Internal Engineering Harness, Not a Shipped Product

## Status

Accepted — `v0.11.0` "Release Engineering & Architecture Governance",
`WP 11.3B` (Presentation Strategy Implementation), 2026-08-11.
References `ADR-0068` and `ADR-0092` (neither modified by this ADR).

## Context

`WP11.0A Platform Architecture Review.md` (finding `A-2`) found two
independently maintained presentation stacks — `Tempest.App` and
`Tempest.Desktop` — with no documented product decision about whether
the console remained a shipped surface. `WP11.3A Presentation Strategy
Review.md` investigated directly and found the question was narrower
than originally framed, with six specific, evidenced findings:

1. **`TempestShell` has been unreachable from any running entry point
   since `ADR-0068`** (`WP 8.1A`, `v0.8.0`) — three releases before this
   ADR. Zero executable references anywhere in the repository outside
   its own now-retired file and test suite.
2. **`README.md` and `docs/academy/Contributor Learning Path.md`**
   described an architecture roughly six releases stale, omitting
   `Tempest.Desktop` entirely and naming the console as the only way to
   run TempestOS.
3. **A claim that `WP 10.0B` formally decided "`Tempest.Desktop` is
   primary, the console is retained for diagnostics/testing"** existed
   only as a source-code comment (`Tempest.Desktop/Program.cs`) — a
   direct search of all six `WP 10.0B` deliverable documents and the
   rest of the repository found this exact framing nowhere else.
   Unverified, not previously ratified anywhere a future contributor
   would find it.
4. **`.github/workflows/ci.yml` and `.github/workflows/release.yml`**
   (this project's own `WP 11.1A`/`WP 11.1B`) packaged `Tempest.App` and
   `Tempest.Desktop` as symmetric, co-equal release artifacts —
   contradicting the informal Desktop-primary positioning.
5. **No dedicated Work Package has advanced `WorkspaceShell`'s own
   console rendering since `WP 10.0B`** — every subsequent touch to
   `src/Tempest.App/Workspace/` extended the *shared* Workspace domain
   layer (which `Tempest.Desktop` equally consumes), never the console
   presentation itself, while `Tempest.Desktop` received sixteen
   dedicated Work Packages (`WP 10.0A`–`WP 10.9A`).
6. **`WorkspaceShell` retains genuine, demonstrated value as a
   verification harness** — no Avalonia startup cost, directly
   scriptable and redirectable, already relied on by `docs/academy/
   02 Runtime Architecture/31-commercial-user-experience-and-application-completion.md`'s
   own real `dotnet run` process-launch audit technique.

`WP11.3A`'s own trade study scored four options; "Differentiated
Disposition" — retire the provably dead `TempestShell`, formally ratify
the still-live `WorkspaceShell` as an internal tool rather than leaving
its status implicit — scored highest (21/25) against "retire both"
(11/25), "do nothing" (8/25), and "promote the console to a co-equal
product" (6/25). This ADR is that ratification.

`TempestShell`, `IPage`, and `PlaceholderPage` (and their own dedicated
test suites) were retired in the same Work Package that produced this
ADR — see `WP11.3B Presentation Strategy Implementation.md` for the
removal itself. This ADR's own scope is the remaining, live question:
what `Tempest.App`/`WorkspaceShell` *is*, now that it is no longer a
candidate primary presentation surface.

## Decision

**`Tempest.Desktop` is TempestOS's sole shipped, primary product
surface. `Tempest.App`, presented through `WorkspaceShell`, is
TempestOS's Internal Engineering Harness — a first-party diagnostic and
verification tool, not a second shipped application.**

Concretely:

1. **`Tempest.App`/`WorkspaceShell` continues to exist, unmodified in
   behaviour, and continues to be built and tested on every Build Gate
   and Test Gate run.** This decision changes its *classification*, not
   its code — `ADR-0092`'s own finding that the shared Workspace
   contracts are rendering-agnostic is unaffected, and nothing about
   this ADR requires or invites removing `WorkspaceShell` itself
   (`WP11.3A` finding F6: real, demonstrated harness value, no evidence
   of harm from keeping it).
2. **The shared Workspace domain layer** (`WorkspaceManager`, the six
   Engineering Disciplines' commands/node providers/factories,
   `EngineeringWorkspaceComposer`) **is unaffected by this decision
   entirely** — it is platform infrastructure `Tempest.Desktop` depends
   on directly, not part of what this ADR reclassifies.
3. **Release engineering treats the two surfaces asymmetrically**,
   correcting `WP11.3A` finding F4: `Tempest.Desktop`'s build output is
   TempestOS's shipped release artifact; `Tempest.App`'s build output,
   if published at all, is clearly labelled as an internal harness
   build, never presented as a second, co-equal product deliverable.
4. **Documentation describes both surfaces accurately** — correcting
   `WP11.3A` finding F2: `README.md` and `Contributor Learning Path.md`
   name `Tempest.Desktop` as how to run TempestOS, and `Tempest.App` as
   the internal harness it now formally is.

## Consequences

**Positive:**

- Closes `WP11.0A` finding `A-2` with a real, citable decision — the
  next contributor who asks "is the console shipped?" finds this ADR
  instead of re-deriving the answer from source the way `WP11.3A` had
  to.
- Preserves `WorkspaceShell`'s own demonstrated value (`F6`) rather than
  discarding a working asset on no evidence of harm — the same
  "act only on demonstrated need" discipline this project applies
  everywhere else, applied here in the direction of *not* removing
  something that works.
- Requires no change to any Workspace contract, Platform Service, or
  Engineering Domain type — `WP11.3A` traced every dependency this
  decision touches and found all of it confined to the presentation
  layer.

**Negative:**

- `Tempest.App` now carries two distinct identities in one project (a
  shared Workspace domain library `Tempest.Desktop` depends on, and a
  harness executable) — a future contributor unfamiliar with this ADR
  could still find that bundling surprising. Splitting them into
  separate projects would be a genuine architectural change and is
  explicitly not proposed here (`WP11.3A`'s own "minimum disruption"
  brief; `WP 11.3B`'s own "no architectural redesign" constraint).
- `WorkspaceShell`'s own dedicated test suite continues to run on every
  CI cycle for a surface most users will never touch — an accepted,
  known cost, not a new one; `WP11.3A` weighed and rejected trimming it
  absent a demonstrated problem.

## Alternatives Considered

See `WP11.3A Presentation Strategy Review.md`'s own Options Analysis and
Engineering Trade Study in full. In summary:

**Retire both `TempestShell` and `WorkspaceShell`** — rejected.
`WorkspaceShell` has real, demonstrated use (`F6`); removing it on no
evidence of harm inverts this project's own "act only on demonstrated
need" discipline.

**Leave the classification undecided (do nothing)** — rejected. This is
the status quo `WP11.0A` already found unsatisfactory, and `WP11.3A`
independently confirmed costs real, compounding documentation and
release-packaging accuracy (`F2`, `F4`) the longer it persists.

**Promote the console to a fully co-equal, actively developed second
product** — rejected. Contradicts `WP11.3A` finding F5's own evidence
of actual Work Package allocation, and would double the ongoing
engineering cost of every future Engineering Discipline addition with
no demonstrated business need.

## Related Documents

`ADR-0068` (unchanged — the decision that first made `TempestShell`
unreachable, cited not amended); `ADR-0092` (unchanged — the decision
that made `Tempest.Desktop` the graphical presentation for the
Engineering Workspace, cited not amended); `ADR-0062` (unchanged,
reaffirmed by `ADR-0092`); `ADR-0094` (Avalonia selection, unaffected);
`docs/releases/v0.11.0/WP11.0A Platform Architecture Review.md`
(finding `A-2`); `docs/releases/v0.11.0/WP11.3A Presentation Strategy
Review.md` (the complete evidence base and trade study this ADR
ratifies); `docs/releases/v0.11.0/WP11.3B Presentation Strategy
Implementation.md`; `src/Tempest.App/Workspace/WorkspaceShell.cs`;
`src/Tempest.Desktop/Program.cs`.
