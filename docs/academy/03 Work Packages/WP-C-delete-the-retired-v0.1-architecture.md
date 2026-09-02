# WP-C — Delete the Retired v0.1 Architecture

## 1. Introduction

`WP-C` deleted the pre-`TempestHost` architecture that `Tempest.Core` had
carried, unreferenced, since the runtime it replaced was built. Nine types
went in two commits — `dfa6ee1` for the eight the approved scope named, and
`7e28f74` for the ninth the deletion itself orphaned. It closed `TD-01`,
open since `WP 2.6`, and was the first work package of the remediation
programme.

## 2. Purpose

To take the branch `TD-01`'s own recorded trigger had always offered and
nobody had taken: *"the legacy bootstrap code is either genuinely revived
or deliberately deleted."* `WP 5.2` had declined to migrate it (`D-020`)
without ruling on deletion, so the row stayed open on a decision that was
never made rather than on work that was never done.

## 3. Background

The deleted set was one coherent architecture, not eight unrelated
leftovers. `BootstrapService` was the sole consumer of `HostingService`,
`ConfigurationService` and `LoggingService`, and was itself called by
nothing. `ProjectService` constructed its own `JsonProjectRepository` in
its constructor — no DI, no interface resolution — and had been superseded
by `Tempest.App.Projects`.

Two facts made deletion safe to *establish* rather than assume: none of the
eight carried XML documentation, unlike the rest of `Tempest.Core`; and the
only surviving mentions anywhere in the codebase were two comments naming
the code as retired.

## 4. The Problem

Retired code that still compiles is not inert. It is the first thing a
reader encounters when they open `Tempest.Core/Bootstrap/` looking for how
the platform starts, and the answer it gives is wrong. `README.md` still
described the `src/` tree in its terms — which `WP-REVIEW` later found and
corrected, because a reviewer would have gone looking for directories that
no longer existed.

The subtler problem is that a dormant architecture accumulates
justification. Each release it survives is weak evidence that it might be
needed, and `TD-01` had survived several.

## 5. The Design

Deletion, plus exactly the corrections deletion forced.

One production line changed beyond the removals: `JsonExportFormat`'s
`<see cref>` pointed at `JsonProjectRepository` purely to cite a JSON
convention, and a dangling `cref` fails the build under
`TreatWarningsAsErrors`. The `cref` was removed and the sentence kept its
meaning.

Two registers stated the deleted namespaces as current and were corrected
as a direct consequence rather than as separate work: the Namespace
Register retired four rows following `Tempest.App.Shell`'s own established
convention, and the Platform Services Register's Project Engine row now
cites the live App-layer capability instead of deleted code.

The second commit, `7e28f74`, removed `ApplicationConfiguration` — the
settings record `ConfigurationService` produced and
`HostingService`/`BootstrapService` consumed. It became referenced by
nothing but itself once the first three went: **a ninth orphan created by
the deletion rather than found by the audit**, which is why it was reported
for a ruling rather than removed on the spot. The same commit brought
`AT-20` current: it claimed a macro step must be a `CommandDescriptor` with
`CreateDefault` set and that only `Tempest.Samples` commands qualified.
`TD-77` Stage 5 had changed that and the row was never updated. It is now
marked Retired, with the original text struck through rather than erased.

## 6. Alternatives Considered

**Revive it.** `TD-01`'s other branch. Rejected on the evidence: nothing
called it, nothing documented it, and `Tempest.App` already provided the
project capability `ProjectService` had offered.

**Leave it and close `TD-01` as accepted.** Rejected because the row's
trigger names two outcomes and "accepted permanently" is neither.

**Delete `ApplicationConfiguration` in the same commit.** Rejected at the
time: it was not in the approved scope, and a deletion that grows itself
mid-flight is how scope stops meaning anything. Reported instead, ruled on,
and taken as `WP-C`'s completion.

**Delete the other dormant code the audit surfaced.** Explicitly not done —
`Models/ProjectModel` is still referenced by live App and Desktop code; the
`Core/EngineeringDomain/Contracts` declared vocabulary, `Core/Api`, the
`InputBindingRouter` extension point and `TD-115`'s three
implemented-but-unreachable commands are all dormant for reasons of their
own and were out of scope.

## 7. Why This Solution Was Chosen

Because the deletion was provable. Every claim behind it — no callers, no
documentation, superseded by a named replacement — is checkable against the
repository rather than argued from taste. That is the difference between
removing dead code and removing code somebody might have wanted.

Splitting the ninth type into a second commit preserved that property. The
first commit deleted what the audit had proved unreferenced *before* the
work started; the second deleted what the first had made unreferenced. Both
are honest, and conflating them would have hidden that the audit's count of
eight was correct only until the work began.

## 8. Architectural Principles

`ADR-0016` (`Tempest.Core.Runtime` is distinct from `Tempest.Core.Hosting`)
is the decision this closes out: the distinction it drew becomes moot once
one side of it no longer exists, and the Namespace Register now records
`Tempest.Core.Hosting` and `Tempest.Core.Bootstrap` as retired rather than
as a live contrast.

More broadly: a register is a claim about the repository, and a claim that
has stopped being true is a defect in the register, not a stale note. Both
corrected registers were changed in the same commit as the code that
falsified them.

## 9. Benefits

`Tempest.Core` no longer ships a second, wrong answer to "how does this
platform start". `TD-01` is discharged on its own stated terms after
several releases open. The registers that described the deleted namespaces
are accurate again. And 250 lines of unreferenced production code stopped
appearing in every search, every audit and every reader's mental model.

## 10. Trade-offs

The deletion is irreversible in practice — the code is recoverable from
history, but nothing will bring back the context that would make reviving
it sensible. That is the accepted cost of taking a trigger's deletion
branch, and `TD-01` had been open long enough that the alternative was
indefinite deferral.

`ApplicationConfiguration` needed a second commit and a second ruling.
Slower than deleting nine types at once, and correct: the ninth was not in
scope when the work was approved.

## 11. Common Mistakes

**Assuming "unreferenced" from a search.** The claim needed three
independent supports — no production callers, no test callers, and a named
live replacement — because a reflective or configuration-driven caller
would satisfy none of the first two.

**Treating a dangling `cref` as cosmetic.** Under
`TreatWarningsAsErrors=true` it fails the build. Documentation references
are code.

**Letting a deletion grow.** The moment `ApplicationConfiguration` was
found orphaned, the tempting move was to remove it silently as part of "the
same cleanup". It was reported instead.

## 12. Future Evolution

The dormant code `WP-C` deliberately left is tracked rather than forgotten:
`TD-110` records the deletion and its scope; `TD-115` covers the three
implemented-but-unreachable commands, which `WP-H` later pinned with
`FutureCapabilityCommandTests` so they can be neither swept away as dead
code nor quietly given a construction path. `Core/Api` remains live but
unreachable over HTTP, which `WP-A2` decided rather than deferred.

`Models/ProjectModel` remains referenced by live code and is not a
deletion candidate today.

## 13. Key Takeaways

- A debt row with a two-branch trigger stays open until somebody takes a
  branch. Declining to migrate is not the same as deciding not to.
- Prove "unreferenced" three ways before deleting, and record which three.
- A deletion can create orphans that the pre-work audit could not have
  found. Report them; do not absorb them.
- Registers that describe deleted code are corrected in the deleting
  commit, not in a later tidy-up.

## Related Documents

- `docs/governance/Quality/Technical Debt Register.md` — `TD-01`, `TD-110`,
  `TD-115`, `AT-20`
- `docs/governance/Engineering/Namespace Register.md` — the four retired rows
- `docs/governance/Engineering/Platform Services Register.md` — Project Engine
- `ADR-0016` — `Tempest.Core.Runtime` versus `Tempest.Core.Hosting`
- Commits `dfa6ee1`, `7e28f74`
