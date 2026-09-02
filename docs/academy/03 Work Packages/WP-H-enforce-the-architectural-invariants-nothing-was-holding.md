# WP-H — Enforce the Architectural Invariants Nothing Was Holding

## 1. Introduction

`WP-H` (`9c25223`) audited the existing architectural-enforcement surface,
found most of it already sufficient, and added five test classes for the
invariants genuinely unheld. Its governing principle was that an
architectural invariant should be *easy to discover, difficult to
accidentally bypass, and tied directly to the decision it enforces* — which
meant, more than anything, not duplicating enforcement that already existed.

## 2. Purpose

To close the gap between invariants the project had *decided* and
invariants the project could *detect*, without creating another generic
test bucket.

## 3. Background

After `TD-77`, Stage 5, `WP-B1` and `WP-A1`, the enforcement surface was
already substantial. The audit that opened this work package listed what
was already held and left alone:

- canonical surface invocation — `IdOnlyInvocationGuardTests` plus
  `SurfaceCommandIntegrationTests`;
- the Id-only allow-list, its staleness check and the migrated surfaces;
- policy completeness — `SurfaceCommandPolicyCompletenessTests`;
- `AppliesToKinds` ↔ factories — `KindEligibilityInvariantTests`;
- binding declaration — `CommandDescriptorBindingTests`;
- samples direction — `SampleSeparationTests`, at csproj level;
- `F-07` implemented-and-working — the Requirements/Mechanical integration
  tests dispatch all three through the real `ICommandDispatcher`;
- `F-14` behaviour — all five `Duplicate` handlers covered.

## 4. The Problem

Five invariants were held by nothing, or by something that did not actually
enforce the rule:

**Dependency direction** was held by the compiler alone — which enforces
the graph the `csproj` files *declare*, not the rule. Adding an upward
reference makes more compile, not less.

**`AT-23`'s dormancy premise** was unchecked. The Id-only call in
`InputBindingRouter` was allow-listed as DORMANT on one premise — nothing
in production calls `Bind(gesture, commandId)` — and nothing verified it. A
keyboard shortcut on a discipline command would have thrown into a
fire-and-forget `async void`, been caught, logged, and looked like a dead
key.

**`AT-10`'s premise** was likewise unchecked: that no shipped assembly maps
an HTTP route onto a command.

**`AT-24`/`F-14`'s five `Copy`-delegation sites** were documented as a
bounded list with nothing keeping them bounded.

**`TD-115`'s three implemented-but-unreachable commands** could be swept
away as dead code or quietly given a construction path, with nothing
noticing either.

## 5. The Design

One class per decision, each documenting the decision it protects, the
failure it catches, and why a behavioural test cannot catch it.

**`DependencyDirectionTests`** — asserts `Desktop → App → Core` and no
Avalonia below the shell, from the declared graph *and* re-checked against
`Assembly.GetReferencedAssemblies()`, so a reference arriving transitively
is caught too. No dependency-analysis framework was introduced.

**`DormantKeyboardBindingTests`** — asserts no production code calls
`Bind(gesture, commandId)`, and that the shell still registers the provider,
so the extension point is genuinely wired *and* genuinely bound to nothing.

**`NoShippedAssembly_MapsAnHttpRouteOntoACommand`** (added to
`IdOnlyInvocationGuardTests`) — both `MapCommand` callers are in
`Tempest.Samples`, which the Desktop does not reference.

**`DuplicateCopyDelegationTests`** — set equality between the five
documented files and the files that actually call a `Copy` handler
directly, so proliferation *and* disappearance both fail. Requirements'
non-delegating `Duplicate` is pinned beside them so the five read as a
bounded list rather than as "every Duplicate".

**`FutureCapabilityCommandTests`** — the three commands, their handlers and
their `RegisterHandler` lines still exist, and no production code
constructs one.

## 6. Alternatives Considered

**Consolidate the invariants into one location.** Rejected on the governing
principle rather than on taste: `Tempest.Core.Tests` does not reference
`Tempest.Desktop`, so a single location is not reachable in any case; and
the existing tests sit beside the decisions they enforce, which is two of
the principle's three clauses. Discoverability — the third — is served by
the registers, which is where `WP-H`'s entries went.

**Introduce a dependency-analysis framework.** Rejected; the invariant is
expressible in two assertions over the declared graph and the loaded
assemblies.

**Add tests for the eight already-enforced invariants.** Rejected as
duplication. The audit's most useful output was the list of what *not* to
write.

## 7. Why This Solution Was Chosen

Because the audit's conclusion — that most candidate work would be
duplication — was allowed to stand. A work package chartered to add
enforcement that concludes "eight of thirteen are already enforced, here
are the five that are not" is doing its job, not underdelivering.

Each new class also earns its place against a specific, named failure. That
is what stops an invariant suite becoming a place where tests go to be
thorough.

## 8. Architectural Principles

`ADR-0023` (dependencies flow downward) is the decision
`DependencyDirectionTests` protects, and it later did real work: `WP-Z2`
relied on it to keep `Tempest.App` dispatcher-free while fixing a
UI-thread defect.

The stated principle — discoverable, hard to bypass, tied to its decision —
is itself the reason consolidation was refused. A test in a general bucket
satisfies the first clause at the cost of the third.

## 9. Benefits

Five invariants moved from decided-but-undetected to build-enforced. Two
allow-list premises (`AT-10`, `AT-23`) that had been *assumed* are now
*checked*. `AT-23`'s check became `WP-A2`'s trigger, firing at the change
rather than at a bug report — exactly as designed.

## 10. Trade-offs

Some of these are source-level tests and share that form's brittleness to
formatting. Each states so in its own remarks. `DuplicateCopyDelegationTests`
also fails if someone deliberately refactors the pattern away — which is
the prompt to retire `AT-24`, not a wall.

## 11. Common Mistakes

**Believing the compiler enforces the dependency rule.** It enforces the
declared graph. Adding an upward reference makes more compile.

**Allow-listing on an unverified premise.** Two entries rested on premises
nothing checked. Both are now checked, and `WP-A2` promptly showed one of
them (`AT-23`'s) had been masking a defect rather than recording a choice.

**Writing the tests before the audit.** The audit's finding that eight
invariants were already enforced is what kept this work package small.

## 12. Future Evolution

`WP-A2` acted on `DormantKeyboardBindingTests`' trigger, migrating the
router and amending `AT-23` so it now means only what it says. `WP-Z1` then
corrected that test's own documentation, which had gone stale in the same
change — the test kept guarding `AT-23` correctly while its prose described
a defect that no longer existed.

## 13. Key Takeaways

- Audit before adding enforcement. Most of what a broad invariant charter
  suggests is usually already covered.
- An allow-list entry is only as good as the premise underneath it; check
  the premise.
- A test tied to its decision is discoverable through the register that
  records the decision. It does not need to live in a bucket named after
  its category.
- Five invariants, five mutations, five killed — an Avalonia package in
  `Tempest.App`, a sixth `Copy` delegation, a production
  `CompareBaselinesCommand` construction, a production keyboard binding,
  and a `MapCommand` call site.

## Related Documents

- `ADR-0023` — dependencies flow downward
- `docs/governance/Engineering/Architectural Dependency Register.md`
- `docs/governance/Quality/Technical Debt Register.md` — `AT-10`, `AT-23`,
  `AT-24`, `TD-115`
- `WP-A2` retrospective — the trigger, fired
- Commit `9c25223`
