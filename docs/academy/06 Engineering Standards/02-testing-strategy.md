# Engineering Standard: Testing Strategy

## Purpose

This standard describes the testing conventions consistently applied across
every TempestOS work package, from WP 2.1's 53 tests through WP 4.4E's 313
(all passing, zero warnings on every build, verified from a clean,
fully-committed tree before any work package is reported done), so future
work packages extend the existing approach rather than introducing an
inconsistent one alongside it. The specific count is not the point — see
`CHANGELOG.md`/`WorkPackages.md` for the current, authoritative total — the
point is that the same disciplined approach below has now been applied,
without deviation, across ten times as many tests as existed when this
document was first written.

## The Internal Test Seam Pattern

Where a public API's contract is deliberately broad or ambient (discovery's
"scans loaded assemblies"; a manager's "operates over whatever is currently
registered"), and testing it end-to-end would depend on incidental process
state outside the test's control, TempestOS exposes an `internal` overload
carrying the same core algorithm, tested directly, with `InternalsVisibleTo`
granting the test assembly access.

- `ReflectionFrameworkDiscoveryService.DiscoverModules(IEnumerable<Type>)` —
  `internal`, lets discovery's filtering/validation/dedup/ordering logic be
  tested against an explicit, controlled type list, independent of whatever
  happens to be loaded into the test process.
- `ModuleLifecycleManager.InitialiseModuleAsync`/`StartModuleAsync`/etc. —
  `internal`, lets individual transitions (including deliberately invalid ones)
  be tested directly, without needing to contrive a specific batch-operation
  sequence to indirectly trigger them.

This pattern exists specifically because "just test the real, public thing
end-to-end" produces flaky or actively self-defeating tests the moment the
system under test depends on ambient state (see WP 2.1's retrospective,
"Common Mistakes," for the specific failure mode this was designed to avoid:
deliberately-broken fixtures needed by *other* tests interfering with a
"happy path" test that scans the whole shared test assembly).

## Fixture Design

Test fixtures implementing runtime interfaces (`IModule`, `IModuleLifecycle`)
are kept minimal, `internal`, and clearly separated from production code —
`ModuleFixtures.cs`, `LifecycleTestFixtures.cs`,
`DependencyInjectionTestFixtures.cs` are named and organised specifically so a
reader never mistakes a test fixture for a real module.

Where a fixture needs to record cross-instance information for order-of-execution
assertions (WP 2.3's `RecordingLifecycleModuleAlpha`/`Beta`/`Gamma`, each
instantiated independently via reflection with no constructor injection
available), a shared, explicitly-`Reset()`-able static log is used deliberately,
documented as a pragmatic test-only mechanism, not a production pattern.

## Test Category Coverage

Every work package's brief specified an explicit list of required test
categories (e.g., WP 2.3: "Initialisation order, Startup order, Shutdown order,
State transitions, Invalid transitions, Cancellation, Exception handling,
Failed modules, Disposal order"). The standard is: every category in the brief
gets at least one test whose name and assertions make clear which category it
satisfies — a reviewer should be able to match every required category to a
specific test without ambiguity.

## Prefer the Real Implementation Over a Mock

**Established from WP 2.1 onward and made explicit, as a named brief
requirement, from WP 4.2 onward: use the real, production implementation
under test wherever practical; reserve a test double for the one thing a
real implementation genuinely cannot let a test observe or control.**

- WP 4.2 (Plugin Manifest) built `DynamicPluginAssemblyBuilder`
  (`System.Reflection.Emit.PersistedAssemblyBuilder`) to construct a
  genuinely valid, loadable compiled assembly at test time, rather than a
  test double standing in for "an assembly" — proving `PluginAssemblyLoader`
  against a real `Assembly.LoadFrom` call, and proving Module Discovery's
  own, completely unmodified `AppDomain.CurrentDomain.GetAssemblies()` scan
  genuinely sees a plugin-loaded assembly, not merely a simulated one.
- WP 4.4D (Event Bus) and WP 4.4E (Sample Module Event Integration) both
  test against the real `EventBus` implementation throughout — subscription
  ordering, snapshot semantics, re-entrant publishing, and failure isolation
  are all proven by constructing a real `EventBus` and real event/handler
  types, never a mocked `IEventBus`.
- The one recurring, accepted exception is a **level-recording `ILogger`**
  (`RecordingLogger`, `RecordingLevelLogger`), used specifically to observe
  *what was logged, and at what severity* — a real `ILogger` implementation
  cannot make its own internal log calls assertable any other way, and
  substituting one here does not stand in for any production behaviour
  under test; it exists purely to make an otherwise-unobservable side
  effect (a log line) checkable.

This is a stronger standard than "prefer integration tests" in the generic
sense — it is a specific, repeatable rule: before reaching for a test
double, ask whether the real type can be constructed and used directly in
the test at acceptable cost. If it can, use it. If not, name specifically
*why not* (as `RecordingLogger`'s own doc comment does), rather than
defaulting to a mock as a matter of habit.

## Regression Tests for Discovered Bugs

When a bug is found during implementation (not before it, as a hypothetical —
see WP 2.4's lock/try-catch bug, described in that work package's retrospective
and Common Mistakes section), a regression test is added specifically
reproducing the scenario that exposed it, named to describe the scenario, not
just "test the fix" — for example,
`InitialiseModuleAsync_MarksModuleFailed_WhenServiceProviderResolutionFails`.

## Build and Test Discipline

Every work package's completion is validated by a full solution build (zero
warnings, zero errors) and a full test run (all tests passing), performed from a
clean, fully-committed working tree — not merely "it worked when I ran it
mid-edit." This is what "Both must pass successfully" in each brief's Validation
section means in practice, and is checked explicitly before any work package is
reported complete.
