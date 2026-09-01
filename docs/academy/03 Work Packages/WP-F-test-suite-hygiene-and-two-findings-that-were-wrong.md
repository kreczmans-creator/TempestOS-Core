# WP-F — Test-Suite Hygiene, and Two of the Four Findings Were Wrong

## 1. Introduction

`WP-F` (`3a9b777`) addressed `TD-114`'s four test-suite findings. Two were
real as described, one had the wrong premise, and one had the wrong number
*and* the wrong label. All four are recorded as they actually are, which is
the more useful half of this work package's output.

## 2. Purpose

To reduce test-suite brittleness where brittleness was real, and to correct
the findings where the audit had described something that was not there.

## 3. Background

`TD-114` was a grouping row covering four audit findings: exact-count
brittleness (`F-11`), repeated host boots (`F-12`), a test-only public API
(`F-15`), and weak assertions (`F-18`).

## 4. The Problem

**`F-11` — exact-count brittleness (real).** Six per-discipline command
counts, plus assertions counting to 74, spread across four files. Adding
one production command broke roughly a dozen assertions in three files,
none of which had anything to do with the new command.

**`F-12` — repeated host boot (wrong premise).**
`SurfaceCommandIntegrationTests` boots 9 times out of 236 across the
Desktop suite — about 4%. The heavy files are `DigitalThreadGraphTests`
(24) and the cockpit tests (16). What actually costs the three minutes is
that the whole collection is serialised, and **that is mandatory**: the
headless Avalonia dispatcher is process-wide.

**`F-15` — test-only public API (real).**
`ServiceProviderExtensions.GetService<T>()`: a public API on a
plugin-hosting assembly with zero production callers and 32 test call
sites, while production resolved through `GetService(typeof(T))` in 40
places.

**`F-18` — 84 weak assertions (wrong number, wrong label).** The real
figure is 269 of 2,820 test methods carrying a single weak-shaped
assertion — and nearly all are legitimate contract tests whose one
assertion *is* the contract. Only 8 assert something that cannot fail by
construction, and 3 of those turned out to be real lifecycle contracts a
scan cannot see.

## 5. The Design

**`F-11`.** The six per-discipline counts became set assertions over
declared Ids: adding a command fails one test, *naming* the command. The
source-declaration test compares source Ids to registry Ids as sets rather
than counting to 74; the macro-eligibility test's two counts were
restatements of the set equality directly above them and were removed; the
identity test asserts uniqueness rather than restating 74 twice.

**One canonical reconciliation is retained and completed:**
`74 = 18 unavailable + 56 bindable`, with all three terms asserted rather
than two and a comment. That arithmetic is the protection — it is what
stops a nineteenth unavailable command hiding inside the bindable set.
Numeric assertions that carry meaning (HTTP status codes, viewport
geometry, byte sizes, page counts) were left alone.

**`F-12`.** No work done, premise corrected in the register. A shared-host
fixture was **rejected rather than attempted**: 5 of those 9 tests mutate
the domain, and `TD-37`/`WP 10.1B` root-caused the rule that every
independent test gets a fresh persistence root. The ~3s available from the
two provably read-only classes was declined as not worth the churn.

**`F-15`.** Deleted, and the 32 test call sites migrated to the form
production already used — rather than migrating 40 production casts to the
convenience.

**`F-18`.** One deletion and three strengthenings:

- deleted: `UnitConverterTests.Constructor_RequiresNoArguments` —
  `Assert.NotNull(new UnitConverter())`.
- strengthened: `MultiPanelLayout…` — its name promised the Explorer,
  Document Area and Inspector were present; nothing checked any of them.
- strengthened: the Cockpit refresh test — its own comment claimed the
  second `Refresh` "must not duplicate state" and nothing asserted it.
- strengthened: the Explorer filter test — its name promised filtering
  reduces the tree; it never filtered and never counted.

`WorkspaceManagerTests.CanRevise_FactoryRegisteredForKind_IsTrue`, named in
the audit as a tautology, is a real registration-to-query contract and was
kept. The other 265 were not touched.

Also: the Desktop `EngineeringCockpitTests` became `CockpitViewHonestyTests`
— it shared a name with the Core class covering a different subject, which
compiled fine and confused only readers. And six Desktop test helpers that
had been copied between classes now have one implementation in
`DesktopTestHelpers`; the copying had already cost something, since
`FindButton` was duplicated in a form that searched the whole Ribbon
instead of the command's own tab and could match the wrong button.

## 6. Alternatives Considered

**Rewrite all 269 weak-shaped tests.** Rejected: nearly all are legitimate.
A mechanical scan cannot distinguish a contract test with one assertion
from a vacuous one.

**Migrate production to `GetService<T>()`.** Rejected — production was out
of scope, and the extension was the outlier, not the 40 call sites.

**Introduce a shared `WorkspaceHost` fixture.** Rejected on isolation
grounds established by `TD-37`.

**Replace exact counts with different exact counts.** Rejected; the point
was set membership, not a better number.

## 7. Why This Solution Was Chosen

Because correcting a finding is worth as much as closing one. Two of four
findings did not describe the repository, and recording that accurately
stops the next audit rediscovering the same wrong premise — and stops work
being done against it.

The retained `74 = 18 + 56` reconciliation is the counterexample that keeps
the rest honest: not every count is brittleness, and the one that carries
meaning was completed rather than removed.

## 8. Architectural Principles

`TD-37`/`WP 10.1B`'s isolation rule — every independent test gets a fresh
persistence root — was treated as binding, not as an obstacle to
optimisation.

More generally: a test's name is a claim. Three of the four `F-18` changes
were cases where the name promised something the body never checked.

## 9. Benefits

Adding a production command now fails one test, naming it. A public API
with no production callers is gone from a plugin-hosting assembly. Four
tests now check what their names claim. Two register findings are corrected
rather than propagated. And the Test Register was re-derived in full from
disk rather than patched: it had listed 15 of 37 Core directories with
v0.5-era counts, recorded `Shell/` as retired at 0 when it holds 4 files,
and did not cover `Tempest.Desktop.Tests` at all.

## 10. Trade-offs

265 weak-shaped tests were left untouched. That is deliberate — they are
contract tests — but it means a future mechanical scan will flag them
again, which is why the finding's correction is recorded in `TD-114` rather
than only in this article.

The test count fell by one (3,069 → 3,068). A deletion that removes a test
asserting nothing is a gain, and stating the direction plainly is better
than quietly holding the number.

## 11. Common Mistakes

**Trusting a mechanical scan's label.** "Weak" described 8 of 269, and 3 of
those 8 were real contracts.

**Optimising a cost you have not measured.** `F-12`'s three minutes come
from mandatory serialisation, not from the 9 boots it named.

**Copying a test helper.** `FindButton`'s duplicate had drifted into a form
that could match the wrong button — the copying had already cost something
before anyone consolidated it.

## 12. Future Evolution

**Discovered, not fixed:** `FindButtonById` exists in three further Desktop
test files under a different name, already tab-scoped and correct — outside
the audited migration set.

The Test Register re-derivation this work package performed was itself
later found to contain one error: `WP-Z1` established that the Desktop
attribute figure written here (358) was wrong, the true figure at this
commit being 353, because attribute names were counted wherever they
appeared rather than only on executable lines. Corrected and disclosed
there.

## 13. Key Takeaways

- Re-derive an audit's numbers before acting on them. Here, two of four
  findings did not survive contact with the repository.
- Not every exact count is brittleness. Keep the one that carries an
  invariant, and complete it.
- A test whose name promises more than its body checks is worse than no
  test, because it reads as covered.
- Four mutations, four killed: a new discipline command, a Cockpit refresh
  that no longer clears, a filter that ignores its query, and an
  unregistered Inspector panel.

## Related Documents

- `docs/governance/Quality/Technical Debt Register.md` — `TD-114`, `TD-37`
- `docs/governance/Quality/Test Register.md` — re-derived here, corrected by `WP-Z1`
- `WP-Z1` retrospective — the Desktop attribute-count correction
- Commit `3a9b777`
