# Shell & Composition Framework Architecture

**Status: designed — WP 5.0C (ADR-0033, ADR-0034, ADR-0035). Implemented
— WP 5.0D, exactly as designed, with zero deviation from the shape
below.**

## Objective

Design how `Tempest.App` consumes the platform: the application shell that
becomes `Tempest.App`'s own composition root, presents the platform's
capabilities to a user, and consumes Navigation, the Event Bus, and (in
future) Commands and Diagnostics — without ever becoming a second place
platform behaviour is decided. This is `WP 5.0C` — architecture only;
implementation is a later Work Package (`WP 5.0D`).

## Repository Investigation

**`Tempest.App` today — confirmed, not assumed.** `src/Tempest.App/Program.cs`
is a single top-level-statements file: it constructs `BootstrapService`,
`HostingService`, and `ProjectService` directly (all pre-module-pipeline,
`Tempest.Core.Bootstrap`/`Tempest.Core.Hosting`/`Tempest.Core.Projects`),
then runs a hand-written `while (true)` console loop reading
`Console.ReadLine()` and switching on a numbered menu (`1 - Create
Project`, `2 - List Projects`, `0 - Exit`). **It does not construct or run
`TempestHost`/`TempestHostBuilder` at all.** This was already found and
disclosed during `WP 5.0A`'s own Repository Investigation
(`Navigation Framework Architecture.md`) and remains true, unchanged,
today — re-verified directly (`grep -rl "TempestHost" src/Tempest.App/`
finds nothing). This is the central fact this Work Package designs around:
**there is currently no composition root anywhere in this repository that
actually assembles a *running* `ITempestHost` and presents it to a user.**
`ITempestHostBuilder`/`TempestHost` are exercised today only by test code.

**`ITempestHost`'s own public surface — confirmed by direct inspection**
(`src/Tempest.Core/Runtime/ITempestHost.cs`): `State` (a `HostState`),
`RunAsync(CancellationToken)` (blocks until `Stopped`), `StopAsync()`
(requests a controlled shutdown), and `IAsyncDisposable.DisposeAsync()`.
**No member exposes the Host's own internal `ITempestServiceProvider`.**
`TempestHost.cs` builds a `ServiceCollection`/`TempestServiceProvider`
entirely inside `ExecuteStartupPhasesAsync`, holds it in a local variable,
and never assigns it to any field or exposes it through any public member
— confirmed by direct inspection of every `public`/`private` member on the
type. **Nothing outside the module pipeline can currently resolve
`INavigationProvider` or `IEventBus` from a running Host.** This is the
second central fact this Work Package designs around, and the reason a
Shell cannot simply "go get" Navigation the way a module already can.

**The Navigation Framework (`WP 5.0A`/`WP 5.0B`, implemented) already
names the Shell's role, without building it.** `Navigation Framework
Architecture.md`'s own "Rendering Boundary" section states plainly:
"`Tempest.App` (or a future UI shell) maintains its *own*, private mapping
from `NavigationItem.Id` to whatever it knows how to render...
`Tempest.Core.Navigation` never sees, holds, or needs to know that mapping
exists." **This Work Package is where that private mapping — named but
not designed by `WP 5.0A` — finally gets a design.** `INavigationProvider`
already offers exactly what a shell needs: `Items` (an ordered,
already-filtered-by-nothing snapshot of every registered
`NavigationItem`), and `Navigate(id)`, which publishes a
`NavigationRequestedEvent` through the real, unmodified `IEventBus` for
any subscriber to observe.

**The Event Bus (`IEventBus`, implemented) is already a DI-public,
constructor-injectable platform service** (ADR-0020) — any component that
can resolve it may `Subscribe`/`Unsubscribe`/`PublishAsync`, with no
special casing for who the caller is. The only open question is *how* the
Shell obtains a reference to it at all, addressed above.

**Hosted Services (`IHostedService`/`IHostedServiceManager`, implemented
`WP 4.5`) are Host-owned and started between Module Initialisation and
`Running`** (`IHostedService.StartAsync`'s own contract: "Invoked once,
between Module Initialisation and Runtime Running"). **This bounded-
completion contract is the direct reason a blocking, interactive shell
cannot itself be implemented as a hosted service** — see "The Shell Is
Not a Module or a Hosted Service," below, and `RD-0035`.

**The Module SDK (`ModuleBase`/`ModuleLifecycleBase`) exists to reduce
boilerplate for modules that participate in `InitialiseAsync`/
`StartAsync`/`StopAsync`/`DisposeAsync`, each expected to complete
promptly** so `ModuleLifecycleManager`'s batch-per-phase orchestration can
proceed to the next module and, eventually, to `Running`. **This is the
same reason a blocking, interactive shell cannot itself be implemented as
a module** — see `RD-0034`.

**Sample Modules (`ClockModule`, `ClockLifecycleObserverModule`,
`NavigationSampleModule` and its two companions) already prove, end to
end, that a real module or plugin-loaded module can constructor-inject a
DI-public platform service and use it from an ordinary lifecycle method.**
The Shell's own consumption of `INavigationProvider`/`IEventBus` follows
the identical shape these modules already established — the only genuine
difference is *how* the Shell obtains that constructor-injection-style
access from *outside* the module pipeline, since it is not itself a
module.

**No duplication found.** Nothing under `Tempest.Core` renders anything;
nothing under `Tempest.App` currently consumes the platform. The boundary
this Work Package draws does not overlap or re-decide anything an
existing platform service already owns.

## Architecture

### The Shell Is the Composition Root `Tempest.App` Has Never Had

`ADR-0009` already named this destination without building it: "the
composition root — whatever code assembles a running TempestOS instance,
today exercised directly by test setup, eventually a dedicated startup
sequence... eventually `Program.cs`." **The Shell *is* that eventual
composition root.** `Tempest.App`'s entry point becomes: construct a
`TempestHostBuilder`, `Build()` it, hand the resulting `ITempestHost` to
the Shell, and let the Shell run it, consume it, and present it to a
user. This is not a new architectural layer competing with the Runtime
Host — it is `Tempest.App` finally doing the one job every other
`Tempest.App`-shaped executable in this project's own precedent (the test
harness) has already been doing for every Work Package since `WP 2.7B`.

### The Shell Is Not a Module or a Hosted Service

Both were seriously considered and rejected — see `RD-0034`/`RD-0035` for
the full reasoning. In summary: a module's `InitialiseAsync`/`StartAsync`
and a hosted service's `StartAsync` are each expected to *complete*, so
`ModuleLifecycleManager`/`HostedServiceManager` can proceed to the next
participant and, eventually, so the Host can reach `Running`. A shell that
blocks on `Console.ReadLine()` inside either lifecycle method would hang
Host startup forever — the opposite of what either mechanism exists to
guarantee. The Shell is layered *above* the Host, not driven *by* it,
exactly as `ADR-0033` records.

### Ownership: The Shell Owns Presentation; the Runtime Host Owns Orchestration

The Runtime Host remains exactly what `Runtime Host Architecture.md`
already states it is: UI-agnostic, owning orchestration, startup,
shutdown, cancellation, and disposal ordering — nothing here changes any
of that. The Shell owns everything about *presenting* the platform to a
user: what regions exist, what renders in each, how user input becomes a
`Navigate(id)` call (or, in future, a dispatched command), and how a
published `NavigationRequestedEvent` becomes something drawn on screen.
Neither owns the other's job. See `ADR-0033`.

### Dependency Direction

```
Program.cs (entry point)
        │  constructs
        ▼
   TempestHostBuilder ──build()──▶ ITempestHost
        │                                │
        │  constructs, owns              │  RunAsync (background task)
        ▼                                ▼
      Shell  ◀──── resolves, once Built ──── ITempestHost.Services
        │            (INavigationProvider, IEventBus)
        │  subscribes to
        ▼
NavigationRequestedEvent (via IEventBus)
        │
        ▼
   Shell's own private Id → rendering mapping (Content Region)
```

- `Program.cs` constructs the Shell directly — the Shell is not itself
  resolved from any container; it is hand-constructed code, exactly like
  `TempestHostBuilder` and `Program.cs` already are today (`ADR-0009`'s
  own composition-root category).
- The Shell depends downward on `ITempestHost` (to run and observe it)
  and, once available, on `INavigationProvider`/`IEventBus` (to present
  Navigation and react to it) — never the reverse. Neither
  `Tempest.Core.Runtime` nor `Tempest.Core.Navigation` gains any reference
  to `Tempest.App`, the Shell, or any rendering type. `ADR-0023`'s
  downward-only layering is unaffected; the Shell simply becomes the
  layer *above* Modules that ADR-0023's own four-layer model already
  reserves room for (Modules → Platform APIs → Platform Services →
  Runtime Host is the platform; the Shell sits above all four, consuming
  the Runtime Host's own public surface, exactly as a human operator or a
  test harness already does).
- `ITempestHost` gains exactly one new, additive member —
  `Services` (an `ITempestServiceProvider?`, `null` until Dependency
  Injection Built, non-`null` from then on) — the mechanical enabler of
  the dependency edge above. See `ADR-0034`.

### Composition Model

**Application lifetime and Shell lifetime coincide; no second state
machine is introduced.** "Application lifetime" is the OS process's own
lifetime — `Program.Main` start to return. "Shell lifetime" is the period
during which the Shell is actively presenting the platform to a user.
For `v0.5`, these are the same interval: the Shell is constructed at the
start of `Program.Main` and stops presenting only when the process is
about to exit. Introducing a distinct Shell state enum, paralleling
`HostState`, would be speculative — nothing today needs the Shell to
outlive, or be a strict subset of, the process's own lifetime. The
Shell's own three coarse states (constructing the Host, running/
presenting, shutting down) are observable directly from `ITempestHost`'s
own `State` plus whether the Shell's own presentation loop has returned —
no new enum is required to express them.

**The Shell's run sequence, resolved concretely (implementation detail of
*how*, not *whether*, is left to `WP 5.0D`):**

1. Construct `ITempestHostBuilder`, add configuration sources, `Build()`.
2. Start `host.RunAsync(...)` as a background task — **not** awaited
   synchronously, since the Shell's own presentation loop and the Host's
   own run must proceed concurrently.
3. Once `host.Services` is non-`null` (guaranteed no later than
   `HostState.Running`, and in practice available as soon as the
   Dependency Injection Built phase completes — see *Host Lifecycle.md*'s
   phase table, unchanged by this design), resolve `INavigationProvider`
   and `IEventBus` through it.
4. Subscribe to `NavigationRequestedEvent`; render the Navigation Region
   from `INavigationProvider.Items`.
5. Enter the Shell's own presentation loop (in today's console form: read
   input, translate a selection into `Navigate(id)`); react to each
   observed `NavigationRequestedEvent` by rendering the corresponding page
   into the Content Region.
6. On an exit request, call `host.StopAsync()`, await the background
   `RunAsync` task, then `DisposeAsync()` the host.

**Workspace, Navigation Region, Content Region, Status Bar, Dialogs,
Notifications — the Shell's own regions, all owned by `Tempest.App`,
none known to `Tempest.Core`:**

| Region | Required for v0.5 | Populated by |
|---|---|---|
| **Workspace** | Yes | The Shell's own top-level presentation surface — the console screen as a whole, for `v0.5`. Named "Workspace" here as a UI-composition term; unrelated to, and not to be confused with, `IConfigurationProvider`'s existing `WorkspaceRoot` (a filesystem path, `Tempest.Core.Configuration`) — the two share a word, not a concept, and this document names the collision explicitly so it is never rediscovered as a surprise. |
| **Navigation Region** | Yes | `INavigationProvider.Items`, rendered as a menu; already fully backed by an implemented, tested platform service. |
| **Content Region** | Yes | Whatever page the Shell's own, private `Id`-to-rendering mapping resolves for the most recently observed `NavigationRequestedEvent`. See "Page/View Construction," below. |
| **Status Bar** | Reserved, not populated | The Shell's composition model names this region now so a future consumer (Diagnostics, `WP 5.2`) has somewhere defined to render into — but nothing populates it in `v0.5`, since no diagnostics data exists yet to show. |
| **Dialogs** | Deferred | No current page needs more than an inline console prompt (already sufficient for anything `v0.5`'s built-in pages require). A first-class modal/dialog abstraction is not designed now — see Deferred, below. |
| **Notifications** | Deferred | No current background event needs to surface an out-of-band notice to the user. Deferred — see below. |

**Multiple workspaces: rejected, not deferred.** A console shell has
exactly one input stream and one output stream; there is no realistic
`v0.5` need for more than one concurrent workspace, and no plausible
near-term consumer. See `RD-0037`. A future, fundamentally different
shell technology (a GUI, a web front end) would revisit this on its own
terms, as its own architecture — not as an extension of this one.

### Page/View Construction

**The Shell owns a closed, hand-registered mapping from `NavigationItem.Id`
to a rendering action, covering exactly the built-in pages `Tempest.App`
itself ships with.** An item with no matching registration renders a
generic, honest placeholder ("no view registered for this item") rather
than failing — the same "disclose the gap rather than crash or silently
drop it" instinct this project applies everywhere else. See `ADR-0035`.

**Dependency injection participates at exactly one boundary: the Shell's
own resolution of platform services, not page construction.** The Shell
resolves `INavigationProvider`/`IEventBus` once, through
`ITempestHost.Services`, and passes whatever a specific page's own
rendering closure needs directly — page construction itself is ordinary
object construction, not a second pass through the platform's DI
container. Routing page construction through the same container modules
use was considered and rejected: see `RD-0036` for the full reasoning,
including why a module or plugin contributing its *own* page is
explicitly deferred, not solved, by this design.

### Platform Integration

**Navigation.** The Shell resolves `INavigationProvider` once (via
`ITempestHost.Services`), enumerates `Items` for the Navigation Region,
subscribes to `NavigationRequestedEvent` for Content Region updates, and
calls `Navigate(id)` in response to user input. The Shell depends
downward on `INavigationProvider`; `Tempest.Core.Navigation` gains no
reference to the Shell, `Tempest.App`, or any rendering concept — the
boundary `ADR-0031` already drew is unaffected, now with its first real
consumer designed.

**Event Bus.** The Shell subscribes to `NavigationRequestedEvent` exactly
as `ClockLifecycleObserverModule` already subscribes to
`ClockModuleLifecycleEvent` — the identical publisher-knows-nothing-
about-subscribers shape, now proven against a non-module consumer for the
first time.

**Hosted Services.** The Shell has **no integration with Hosted Services
in `v0.5`.** `IHostedServiceManager`/`IHostedServiceDiscoveryService`
remain Host-owned and unregistered in the container (ADR-0017) — exposing
`ITempestHost.Services` does not, and structurally cannot, make either
resolvable, since neither is ever added to the `ServiceCollection` in the
first place. A future Status Bar could surface hosted-service state once
a read-only diagnostics projection exists (`WP 5.2`) — not designed now.

**Commands (future, `WP 5.1`).** Not yet implemented; nothing to
integrate with today. Once a dispatcher exists, the Shell becomes exactly
the "application logic" `ADR-0022` already describes: a menu selection
may dispatch a command whose own handler calls
`NavigationService.Navigate(...)`, exactly as `ADR-0022`'s own
`OpenModuleCommand → NavigationService.Navigate(...)` shape illustrates.
Neither Navigation nor Commands depends on the other; the Shell wires
them, as application logic always has under this ADR.

**Diagnostics (future, `WP 5.2`).** Not yet implemented. The Status Bar
region is reserved for it; no further design is made now.

**Plugins.** No plugin-specific integration is needed. A plugin-loaded
module's contributed `NavigationItem` appears in `INavigationProvider.Items`
identically to one contributed by an ordinarily-discovered module — already
proven end to end by `WP 5.0B`'s own plugin-compatibility test. The Shell
renders it exactly like any other item, using its own placeholder for any
`Id` it has no built-in page for. Plugin-contributed *rendering* is
explicitly deferred — see `ADR-0035` and `RD-0036`.

## Application Lifecycle

No new Host Lifecycle phase, and no change to `Runtime State Machine.md`,
is introduced by this design. The Shell observes `HostState` exactly as
any external caller already can (`ITempestHost.State` is already public);
it does not add a state, a phase, or a transition to the Host's own
lifecycle. The Shell's own three coarse stages — construct-and-start,
present-while-running, request-shutdown-and-dispose — are described in
"Composition Model," above, and require no new enum.

## Implementation Note: Forcing `Tempest.Samples` to Load (`WP 5.0D`)

A genuine, non-obvious finding from implementation, disclosed rather than
silently worked around: `NavigationSampleModule.NavigationItemId` and
`SecondaryNavigationSampleModule.NavigationItemId` are compile-time
`const` fields — the C# compiler inlines their literal values directly
into `Tempest.App`'s own IL at compile time. Referencing them alone,
while sufficient to key the Shell's own page mapping, does **not** force
the CLR to load `Tempest.Samples.dll` into the process at runtime,
because no reference to the *type itself* is ever emitted. Without an
explicit `typeof(NavigationSampleModule).Assembly` access forcing the
load before `RunAsync` starts the Host's own Module Discovery phase,
Discovery's `AppDomain.CurrentDomain.GetAssemblies()` scan finds **zero**
`Tempest.Samples` modules — confirmed directly during implementation, by
running the real application before this fix and observing "Framework
discovery completed. 0 module(s) found." The fix is one explicit line in
`TempestShell`'s own constructor, documented in place; this does not
change any design decision this document or `ADR-0033`–`ADR-0035` make,
only a genuinely non-obvious implementation-time correctness detail worth
recording so a future contributor does not rediscover it the hard way.

## Testing Strategy

**Realised in full by `WP 5.0D`** — every scenario below is proven by a
real, passing test against the real `TempestShell`, `TempestHost`, and
`INavigationProvider`/`IEventBus`; a `StringWriter`/`StringReader` stands
in for the console (a real implementation of both contracts, observing
output exactly as a console would — not a mock). Following this
project's own established "prefer real implementations over mocks"
convention (`docs/academy/06 Engineering Standards/02-testing-strategy.md`):

- **`ITempestHost.Services` availability**: `null` before Dependency
  Injection Built, non-`null` from then through `Disposed` — proven
  directly against the real `TempestHost`, mirroring how every other
  `HostState` transition is already proven in `TempestHostTests.cs`.
- **Shell resolves real platform services**: constructing a Shell against
  a real, running `TempestHost` and resolving `INavigationProvider`/
  `IEventBus` through `Services` — proving the same object instances the
  module pipeline itself uses, not a private copy.
- **Navigation Region reflects `Items`**: registering real
  `NavigationItem`s (via a real module, mirroring `NavigationSampleModule`)
  and proving the Shell's own rendering enumerates them in the same
  deterministic order `INavigationProvider.Items` already guarantees.
- **Content Region reacts to `NavigationRequestedEvent`**: calling
  `Navigate(id)` and proving the Shell's own subscriber renders the
  expected page — or the generic placeholder, for an `Id` with no
  registered page.
- **Shell shutdown drains cleanly**: requesting shutdown through the
  Shell and proving `host.StopAsync()`/`DisposeAsync()` both complete,
  mirroring the existing `TempestHostTests.cs` shutdown proofs.

## Required for v0.5 vs. Deferred Beyond v0.5

**Required for v0.5 (this design; implemented in `WP 5.0D`):**

- The Shell as `Tempest.App`'s own composition root, replacing the
  bootstrap-era console loop as the entry point's own job — **done**:
  `Program.cs` now builds a `TempestHostBuilder`, constructs
  `TempestShell`, and calls `RunAsync()`. The bootstrap-era
  `ProjectService`/`BootstrapService`/`HostingService` code itself remains
  untouched and unmigrated, exactly as scoped — `Program.cs` simply no
  longer calls it.
- `ITempestHost.Services`, additive and read-only — **done**.
- Workspace, Navigation Region, and Content Region, populated — **done**,
  proven against the real `NavigationSampleModule`/
  `SecondaryNavigationSampleModule`.
- A Status Bar region, reserved but unpopulated — **done**.
- The Shell's own closed, hand-registered page mapping, with an honest
  placeholder for unregistered items — **done**: `PlaceholderPage`
  serves both the two built-in pages and the generic unknown-item case.

**Explicitly deferred beyond v0.5 (named here so they are not silently
forgotten, not because any of them is currently planned):**

- **Module- or plugin-contributed page rendering.** No mechanism exists
  for a module or plugin to contribute its own view for a `NavigationItem`
  it registers; deferred until a real consumer needs it. See `ADR-0035`,
  `RD-0036`.
- **Dialogs and Notifications as first-class Shell services.** No current
  page needs more than an inline console prompt; no current background
  event needs to surface an out-of-band notice. Deferred, not rejected —
  revisit once a real consumer exists.
- **Themes.** Meaningful once a richer UI framework exists; not
  applicable to a console-only shell.
- **Multiple workspaces.** Rejected outright for this shell — see
  `RD-0037` — not merely deferred.
- **A first-class Shell state machine.** Application lifetime and Shell
  lifetime coincide for `v0.5`; revisit only if a genuine need for the
  Shell to outlive, or be a strict subset of, the process's own lifetime
  ever arises.

## Future Extensibility

- **`WP 5.1` (Command Framework)** slots into the Shell's own input-
  handling exactly as `ADR-0022` already illustrates — no change to the
  Shell's own composition model is anticipated, only a new source of
  "user did something" alongside direct `Navigate(id)` calls.
- **`WP 5.2` (Diagnostics)** populates the already-reserved Status Bar
  region and may register its own `NavigationItem` for a health/status
  page, proving the Shell's own placeholder-then-real-page path against a
  second, non-synthetic consumer.
- **A future, different shell** (a GUI, a web front end) would consume
  the identical `ITempestHost.Services`/`INavigationProvider`/`IEventBus`
  surface this document designs — proving, as `ADR-0031` already
  anticipated for Navigation specifically, that the platform/application
  boundary drawn here is real, not merely declared.

## Related Documents

`ADR-0009` (Composition Root owns externally-created services — the
destination this Work Package fulfils); `ADR-0017` (Discovery/
Registration/Lifecycle remain Host-owned — unaffected, and the direct
precedent `ADR-0034` reasons from); `ADR-0020` (Event Bus is DI-public);
`ADR-0022` (Navigation/Command orthogonality — binding, unrevisited);
`ADR-0023` (four-layer platform model); `ADR-0031`/`ADR-0032` (Navigation
Framework); `ADR-0033`/`ADR-0034`/`ADR-0035` (this Work Package);
`Rejected Designs.md` (`RD-0034`–`RD-0037`); `Navigation Framework
Architecture.md`; `Runtime Host Architecture.md`; `Ownership Matrix.md`;
`docs/releases/v0.5.0/WorkPackages.md`.
