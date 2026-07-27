# Navigation Framework Architecture

**Status: designed — WP 5.0A (ADR-0031, ADR-0032). Not yet implemented.**

## Objective

Design the Navigation Framework: the mechanism by which built-in platform
pages, future engineering modules, and future plugins each contribute a
navigable destination to one coherent structure, without the Runtime Host
ever needing to change, and without any rendering concern leaking into
`Tempest.Core`. This is `WP 5.0A` — architecture only; `WP 5.0B` implements
what this document decides.

## Repository Investigation

**`Tempest.App` today.** A single top-level statements file
(`Program.cs`), pre-module-pipeline: a `while (true)` loop printing a
hand-written numbered menu (`1 - Create Project`, `2 - List Projects`,
`0 - Exit`) and reading `Console.ReadLine()` directly. It does not
construct or run `TempestHost`, does not reference the module pipeline,
and has no concept of "a page," "a view," or "navigation" of any kind —
confirmed by direct inspection, not assumed. This is a disclosed,
pre-existing gap (`docs/governance/Quality/Technical Debt Register.md`),
not something this Work Package fixes; it does mean **no existing
navigation concept exists anywhere in the repository to reuse or
conflict with** — this design starts from a genuinely empty slate.

**No `INavigationProvider`, `NavigationService`, or any navigation type
exists in `src/`** — verified directly (`grep -rl Navigation src/`
returns nothing). `RD-0002` deliberately left `INavigationProvider`
undefined at `WP 4.0`, naming this Work Package (there, still numbered
`WP 4.6A`; see "A Note on Renumbering," below) as its own revisit
trigger.

**What already exists that this design must reuse, not duplicate:**

- **`IEventBus`/`EventBus`** (`Tempest.Core.Events`, `WP 4.4D`) —
  imperative subscribe/publish, sequential dispatch, unconditional
  per-subscriber failure isolation (ADR-0028). A cross-module
  notification mechanism already exists; Navigation does not need its
  own.
- **`ADR-0022`** (already Accepted, `v0.4.0` planning) — Navigation and
  Command Framework are orthogonal platform services; neither depends on
  the other; application logic wires intent to execution. This ADR
  already sketches `NavigationService.Navigate(...)` as a legitimate
  shape and is **binding, not open for revisiting** in this Work
  Package.
- **`ADR-0023`** — the four-layer platform model (Modules → Platform APIs
  → Platform Services → Runtime Host, downward-only dependencies).
  Whatever Navigation turns out to be, it is classified against this
  model, not exempted from it.
- **`ADR-0024`** — Platform Contracts are packaged by capability, one
  namespace per capability, contract and implementation together. Applies
  directly to wherever Navigation's own contracts land.
- **`ADR-0017`** — Discovery, Registration, and Lifecycle are Host-owned,
  never DI-public, because they carry orchestration authority over the
  module pipeline. This is the test every candidate ownership model below
  is measured against.
- **The reflection-discovery pattern** (Module → Plugin → Hosted Service)
  and the **imperative-registration pattern** (Event Bus subscription) —
  two already-proven, structurally different answers to "how does a
  platform capability learn what exists." Which one Navigation needs is
  a real design question, answered below, not assumed by analogy to
  either.

**Platform Services Register / Platform Service Map** both already carry
a "Navigation" row marked not-yet-designed — this document, plus
ADR-0031/ADR-0032, is what moves it to "designed."

### A Note on Renumbering

`docs/releases/v0.4.0/Architecture.md`, `Risks.md`, and `WorkPackages.md`
all refer to this Work Package as `WP 4.6A`, written while it was still
scoped inside the `v0.4.0` release plan. `v0.4.0` shipped as "Platform
Foundation" with Navigation rescoped out entirely (see `docs/releases/
v0.4.0/ReleasePlan.md`'s "Scope" section). This Work Package now begins
the `v0.5.0` "Developer Experience" release, renumbered `WP 5.0A`. Every
architectural finding `Architecture.md` recorded under the old number —
most importantly, ADR-0022's resolution and the single remaining open
question ("does Navigation belong in `Tempest.Core` at all?") — carries
forward unchanged; only the Work Package label changed. See `docs/
releases/v0.5.0/WorkPackages.md` for the renumbered plan.

## Architecture

### The Open Question, Resolved: Navigation Belongs in `Tempest.Core`

`Architecture.md` named this release's one genuinely open question:
*does Navigation belong in `Tempest.Core` at all, given everything built
so far is UI-agnostic and `Tempest.App` is a console loop?* This Work
Package's own brief already states the governing answer plainly:
**"Navigation should be a platform capability. Rendering remains an
application responsibility."** ADR-0031 records this formally; the
reasoning is not asserted, it is derived from precedent already
established:

- `ICommand`, `IEvent`, `IEventHandler<T>` (`WP 4.0`) all live in
  `Tempest.Core`, and none of them carries a shred of UI concern — they
  are pure data/dispatch contracts. A **catalogue of navigable
  destinations** (what pages exist, their titles, their hierarchy) is
  exactly the same *kind* of thing: metadata a platform service can hold
  and hand out, with zero opinion about how any of it is drawn.
- `ModuleDescriptor` and `PluginManifest` already establish the pattern
  of "the platform holds a registry of things it did not create the
  *meaning* of" — Discovery does not know what a module *does*;
  Registration does not know what a plugin's assembly *contains* beyond
  its manifest. A `NavigationItem` registry is the same shape again:
  the platform holds `Id`/`Title`/`Order`/hierarchy; it has no idea what
  actually gets drawn when a user picks one.
- **The boundary is not "Navigation vs. no Navigation in `Tempest.Core`"
  — it is "the navigation *model* in `Tempest.Core`, the navigation
  *rendering* in `Tempest.App`."** This is the same split already
  applied to every other platform service: `TempestHost` orchestrates
  modules without knowing their business logic; the Event Bus dispatches
  events without knowing what a handler does with one. Navigation
  dispatches "go to X" without knowing what "X" looks like on screen.

See ADR-0031 for the full decision record.

### Ownership: DI-Public, Not Host-Owned

Applying ADR-0017's own test — *does this component carry orchestration
authority over the module pipeline (register, initialise, start, stop,
dispose)?* — Navigation clearly does not. A `NavigationItem` registry has
exactly the same non-authority the Event Bus already has: a module that
resolves it cannot register other modules, cannot start or stop
anything, cannot retrigger Discovery. It can only add itself to a list
and ask "please go to X."

**Navigation is therefore a DI-public platform service, registered as an
ordinary container-constructed singleton** — the identical shape ADR-0020
already established for the Event Bus, applied a second time. See
ADR-0032.

### Dependency Direction

```
Module / Plugin-loaded Module
        │  (constructor-injects)
        ▼
INavigationProvider  ──(constructor-injects)──▶  IEventBus
        │
        ▼
   NavigationItem (data, no behaviour)
```

- A module or plugin-loaded module depends downward on
  `INavigationProvider`, exactly as it may depend on `IEventBus`,
  `ILogger`, or any other DI-public service (ADR-0023: dependencies flow
  downward only).
- `NavigationService` (the concrete implementation) depends downward on
  `IEventBus` to publish its own navigation-requested notification — a
  **platform-service-to-platform-service** dependency, not a violation
  of ADR-0023's layering: `LoggerFactory` already depends on
  `IConfigurationProvider` today, precedent that a platform service may
  depend on another platform service, provided the dependency is
  one-directional and introduces no cycle. `IEventBus` has, and needs,
  zero dependency back on Navigation.
- **`NavigationService` never depends on `ICommand` or any command
  dispatcher, and no command type ever depends on `INavigationProvider`
  as an assumed, hard-wired coupling** — ADR-0022 remains fully intact;
  application logic (a command's handler, or any other caller) is what
  wires the two together, explicitly, exactly as ADR-0022 already
  illustrates.
- `Tempest.App` (or any future UI shell) depends downward on both
  `INavigationProvider` (to enumerate items and render a menu) and
  `IEventBus` (to subscribe to the navigation-requested notification and
  perform the actual view swap) — it does not receive a callback or
  delegate *from* `Tempest.Core`; it reaches down to ask, the same
  direction every other consumer already reaches down to `ILogger` or
  `IConfigurationProvider`.

### Registration Model: Imperative, Not Declarative

**Decision: imperative registration**, mirroring the Event Bus's own,
already-settled shape (ADR-0028) rather than Discovery's reflection-based
one. A module or plugin-loaded module constructor-injects
`INavigationProvider` and calls `Register(NavigationItem)` from its own
`InitialiseAsync` (or `StartAsync`) — the identical pattern
`ClockLifecycleObserverModule` already uses to call
`IEventBus.Subscribe<T>`.

**Why imperative, not declarative (an attribute read by reflection,
mirroring `ModuleMetadataAttribute`):** `ModuleMetadataAttribute` exists
specifically to let Discovery read a module's identity *without
instantiating it* (ADR-0027) — a real constraint that mattered because
Discovery runs before the DI container exists. Navigation registration
happens *after* Dependency Injection Built, *during* Module
Initialisation — the module is already being constructed and driven
through its own lifecycle at that point; there is no
instantiation-avoidance problem for a declarative mechanism to solve.
Introducing one anyway would duplicate `ModuleMetadataAttribute`'s
reflection-reading machinery for a case that does not need it — see
`RD-0030`.

**Plugins contribute navigation identically to modules, with zero new
mechanism.** A plugin's own module, once Plugin Loading has made its
assembly visible to Module Discovery (the existing, unmodified
guarantee — see `Plugin Manifest Architecture.md`), is an ordinary
discovered module from this point forward. It registers navigation items
exactly as `ClockModule` would — no plugin-specific navigation API, no
plugin-specific discovery pass, exactly mirroring how Plugin Loading
already requires zero code change to Module Discovery itself.

### Discovery Model: None Required

No new discovery service, and no extension to
`ReflectionFrameworkFrameworkDiscoveryService`, is introduced. Navigation
items are not independently-loadable units the platform must go find —
they are contributed by modules the platform has *already* discovered,
through ordinary constructor injection and an ordinary method call. See
`RD-0032` for the Host-owned-discovery alternative considered and
rejected.

### Rendering Boundary

**`Tempest.Core.Navigation` contains no rendering type of any kind** — no
`View`, no `Page`, no `Component`, no delegate or callback reference to
anything UI-shaped. `NavigationItem` is pure, immutable data:

| Field | Type | Purpose |
|---|---|---|
| `Id` | `string` | Unique identifier, caller-assigned |
| `Title` | `string` | Display label |
| `Icon` | `string?` | An optional, symbolic icon key — never a rendered image, a font glyph, or a UI framework resource; the application resolves what, if anything, that key means |
| `Order` | `int` | Explicit ordering within its `Group`/parent, ties broken ascending ordinal by `Id` — the same deterministic-tie-break convention Discovery and every other reflection-based catalogue already uses |
| `Group` | `string?` | An optional grouping label (a menu section); `null` means ungrouped |
| `ParentId` | `string?` | An optional reference to another registered item's `Id`, establishing hierarchy; `null` means top-level |
| `IsVisible` | `Func<bool>?` | An optional predicate, evaluated by the caller at query time; `null` means always visible |

**How a render actually happens, entirely inside `Tempest.App`:**
`Tempest.App` (or a future UI shell) maintains its *own*, private mapping
from `NavigationItem.Id` to whatever it knows how to render — a Console
menu case, a WPF `UserControl`, a web route, anything at all.
`Tempest.Core.Navigation` never sees, holds, or needs to know that
mapping exists. When `NavigationService.Navigate(id)` is called, it
validates the id is registered and publishes a `NavigationRequestedEvent`
(an ordinary `IEvent`, `Tempest.Core.Navigation`) via `IEventBus` —
`Tempest.App` subscribes to that event and performs the actual swap using
its own private mapping. This is the same shape `ClockModule`/
`ClockLifecycleObserverModule` already prove end-to-end: a publisher
that knows nothing about its subscribers, a subscriber that supplies all
the meaning.

**`NavigationService` does not track "current location."** Which item is
presently on screen is rendering state, owned entirely by `Tempest.App`
— exactly as "which console menu case is currently displayed" was never
the Event Bus's concern either. A future UI shell is free to track this
however suits it (a field, a stack for back-navigation, a router's own
state) without `Tempest.Core` needing to change.

### Public Surface — As Designed

```csharp
namespace Tempest.Core.Navigation;

public sealed class NavigationItem
{
    public NavigationItem(
        string id,
        string title,
        int order = 0,
        string? icon = null,
        string? group = null,
        string? parentId = null,
        Func<bool>? isVisible = null);

    public string Id { get; }
    public string Title { get; }
    public int Order { get; }
    public string? Icon { get; }
    public string? Group { get; }
    public string? ParentId { get; }
    public Func<bool>? IsVisible { get; }
}

public interface INavigationProvider
{
    void Register(NavigationItem item);
    void Unregister(string id);
    IReadOnlyList<NavigationItem> Items { get; }
    Task Navigate(string id, CancellationToken cancellationToken = default);
}

public sealed class NavigationRequestedEvent : IEvent
{
    public NavigationRequestedEvent(NavigationItem item);
    public NavigationItem Item { get; }
}

public class NavigationException : Exception { /* base, mirrors PluginException/ModuleRegistrationException's own shape */ }
public sealed class DuplicateNavigationItemException : NavigationException { }
public sealed class NavigationItemNotFoundException : NavigationException { }
```

Exact signatures may be refined during `WP 5.0B` implementation (per
Engineering Governance §8's Architecture tier); the **shape, ownership,
and dependency direction above are the approved design** and are not
open for silent revision — any implementation-time deviation stops and
reports, per this Work Package's own governing instruction.

**`Items` ordering.** Returned pre-sorted: ascending by `Group` (nulls
first), then ascending by `Order`, then ascending ordinal by `Id` —
deterministic, matching the Deterministic Systems principle applied
identically everywhere else in this platform. `Items` returns *every*
registered item regardless of `IsVisible` — filtering by visibility is
the caller's own decision, not something `NavigationService` decides on
the caller's behalf (the same reasoning `EventBus.Items` — if it existed
— would apply: a registry reports what is registered; interpreting it is
the reader's job).

**`Register` duplicate handling.** Throws `DuplicateNavigationItemException`
if `Id` is already registered — mirroring `RuntimeModuleManager`'s own
duplicate-ID guard. Because registration happens *inside* a module's own
`InitialiseAsync`/`StartAsync`, this exception is caught and isolated by
`ModuleLifecycleManager`'s existing, unmodified per-module isolation
(ADR-0013) — **no new Host-level failure policy is needed**; the
existing one already covers it completely.

**`Unregister` of an unknown id.** A no-op — mirroring
`EventBus.Unsubscribe`'s own "unsubscribe of a never-subscribed handler
is a no-op" precedent exactly.

**`Navigate` of an unknown id.** Throws `NavigationItemNotFoundException`
— this is application logic's own error to handle (a command handler
navigating to a stale or mistyped id), not a Host-level concern, since
`NavigationService` is never part of Host orchestration.

## Lifecycle

**No new Host Lifecycle phase.** `NavigationService` is registered as an
ordinary singleton during the *existing* Platform Services Registered
phase (Phase 6) — `services.Singleton<INavigationProvider, NavigationService>()`,
the identical registration shape `services.Singleton<IEventBus, EventBus>()`
already uses. It is constructed by the container the first time
something resolves it, exactly like `EventBus`, not directly by
`TempestHost` — see the Ownership Matrix update below.

**No change to `Runtime State Machine.md`, `Host Lifecycle.md`'s phase
table, or `Failure Behaviour.md`'s Host-fatal/isolated boundary.** This
is the direct, intended consequence of choosing "DI-public platform
service" over "Host-owned collaborator" — the same simplification the
Event Bus already demonstrated, now proven true a second time for a
structurally different capability.

## Testing Strategy

Following this project's own established, "prefer real implementations
over mocks" convention (`docs/academy/06 Engineering Standards/
02-testing-strategy.md`):

- **`NavigationService` tested directly**, no test seam needed — it has
  no reflection-based discovery to isolate from ambient state, exactly
  like `EventBus`'s own test suite.
- **Registration**: duplicate `Id` throws; deterministic ordering
  (`Group`/`Order`/`Id`) proven with out-of-order registration input,
  mirroring Plugin Discovery's own ordering-proof pattern; `Unregister`
  of an unknown id is a no-op.
- **`Navigate`**: publishes exactly one `NavigationRequestedEvent` via a
  real `IEventBus`, observed by a real subscriber (not a mock) —
  mirroring `ClockModuleEventIntegrationTests`'s own end-to-end
  approach; navigating to an unknown id throws
  `NavigationItemNotFoundException` and publishes nothing.
- **Constructor injection**: a real, discovered module (a new fixture
  module, following `ClockModule`'s own precedent) constructor-injects
  `INavigationProvider` and registers a real item during
  `InitialiseAsync`, proven through the real, unmodified `TempestHost` —
  the same end-to-end proof pattern every DI-public service before it
  has used.
- **Isolation**: a module whose `InitialiseAsync` throws
  `DuplicateNavigationItemException` is isolated by the existing,
  unmodified `ModuleLifecycleManager` — proven by reusing exactly the
  isolation test shape already established for module initialisation
  failures, not a new Navigation-specific isolation test.
- **Plugin parity**: a plugin-loaded module registers a navigation item
  through the identical path a normally-discovered module uses — mirrors
  `PluginAssemblyLoaderTests.LoadPlugins_LoadedAssembly_IsVisibleToUnchangedModuleDiscovery`'s
  own "prove the existing mechanism needs no change" methodology.

## Required for v0.5 vs. Deferred Beyond v0.5

**Required for v0.5 (this design; implemented in `WP 5.0B`):**

- `NavigationItem` (hierarchy via `ParentId`, ordering via `Order`,
  grouping via `Group`, an optional icon key, an optional visibility
  predicate).
- `INavigationProvider`/`NavigationService`: imperative `Register`/
  `Unregister`, an ordered `Items` snapshot, `Navigate` publishing
  `NavigationRequestedEvent` via `IEventBus`.
- DI-public registration, no Host Lifecycle change.
- Module and plugin-loaded-module contribution, both through ordinary
  constructor injection — no special-casing for either.

**Explicitly deferred beyond v0.5 (named here so they are not silently
forgotten, not because any of them is currently planned):**

- **A first-class permission/role model.** No authentication or
  authorization concept exists anywhere in this platform yet; inventing
  one now, even narrowly for Navigation, would be exactly the
  speculative-design-ahead-of-need pattern ADR-0015's Future
  Considerations already warned against and `RD-0002` already applied
  once to this same release. `IsVisible`'s generic predicate is the
  mechanism a future permission system would plug into, once one exists
  — Navigation itself remains permanently ignorant of what a permission
  is.
- **Current-location / active-item tracking, breadcrumbs, and
  back-navigation history.** All rendering-shell state, owned by
  `Tempest.App` (or a future UI shell), not `Tempest.Core.Navigation`.
- **Declarative/attribute-based navigation contribution.** Deferred, not
  rejected outright as impossible — revisit only if a real, demonstrated
  need for it emerges (see `RD-0030`'s own revisit trigger).
- **Async/confirmable navigation** (for example, "confirm before
  discarding unsaved changes"). No current consumer has this need;
  inventing the shape now would be a guess.
- **Deep-linking, URL-style addressing, or a routing DSL.** No web or
  URL-addressable surface exists yet anywhere in this platform.

## Future Extensibility

- **A Command Framework consumer** (`WP 5.1`, formerly `WP 4.7`) can
  call `NavigationService.Navigate(...)` directly from a command's
  application logic, exactly as ADR-0022 already illustrates — no change
  to either service is anticipated.
- **Diagnostics** (`WP 5.2`, formerly `WP 4.8`) could register its own
  health/status page as an ordinary `NavigationItem`, proving the
  contribution model against a second real, non-synthetic consumer
  beyond whatever `WP 5.0B`'s own sample page uses.
- **A future permission system**, whenever one is designed, plugs into
  `IsVisible` without `NavigationService` itself changing — the seam is
  already in place, deliberately, per the deferral above.

## Related Documents

`ADR-0022` (Navigation/Command Framework orthogonality — binding,
unrevisited); `ADR-0031` (Navigation belongs in `Tempest.Core`); `ADR-0032`
(DI-public ownership, imperative registration); `Rejected Designs.md`
(`RD-0030`–`RD-0033`); `docs/architecture/Event Bus Architecture.md` (the
closest structural precedent); `docs/architecture/Platform Service Map.md`;
`docs/architecture/Ownership Matrix.md`; `docs/releases/v0.4.0/
Architecture.md` (the open question this document resolves);
`docs/releases/v0.5.0/WorkPackages.md`.
