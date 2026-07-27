# WP 5.0D — Shell & Composition Framework Implementation

## 1. Introduction

WP 5.0D implements `TempestShell` (`Tempest.App.Shell`) and
`ITempestHost.Services` exactly as `ADR-0033`, `ADR-0034`, `ADR-0035`, and
`Shell & Composition Framework Architecture.md` designed them — with zero
deviation from the approved shape. `Tempest.App`'s entry point
(`Program.cs`) now constructs and runs a real `ITempestHost` for the
first time in this project's history, replacing the bootstrap-era
console loop `WP 5.0A`'s own Repository Investigation first found, and
`WP 5.0C`'s own investigation re-confirmed, unchanged.

## 2. Purpose

To realise `ADR-0033`–`ADR-0035`'s decisions precisely, prove them
against the real `TempestHost`, `NavigationSampleModule`, and
`SecondaryNavigationSampleModule`, and demonstrate — not merely argue —
that a minimum viable Shell can present real Navigation and Content
regions without the Runtime Host requiring any change beyond one
additive, read-only property.

## 3. Background

`WP 5.0C`'s own architecture phase answered every open structural,
mechanical, and ownership question in writing before any code was
written: the Shell is a composition root, not a module or hosted
service; `ITempestHost` gains a `Services` property; page construction
is the Shell's own, DI-independent business. With the design settled,
this Work Package's own brief was explicit about scope: implement a
*minimum viable* Shell — Navigation rendering, Content rendering, page
selection, placeholder pages — and nothing else. No colours, themes,
ANSI styling, dialogs, notifications, Command Framework, Diagnostics,
settings, or project-system functionality.

## 4. The Problem

1. **Implement exactly the shape `ADR-0033`–`ADR-0035` specify** —
   `ITempestHost.Services`, additive and read-only; `TempestShell` as a
   composition root, not a module or hosted service; a closed,
   hand-registered page mapping, independent of the DI container.
2. **Wire `Tempest.App`'s entry point to actually run the platform** —
   `Program.cs` must build a `TempestHostBuilder`, construct the Shell,
   and run it, for the first time in this project's history.
3. **Use the existing sample modules, not hard-coded navigation items** —
   the Navigation Region must enumerate whatever `INavigationProvider.Items`
   actually returns; the Shell's own page mapping may reference
   well-known `Id`s, but must not duplicate what modules exist.
4. **Prove it end to end, preferring real implementations over mocks** —
   a real `TempestHost`, real sample modules, and a real `IEventBus`,
   with only the console's own `TextWriter`/`TextReader` supplied by
   tests (a `StringWriter`/`StringReader`, itself a real implementation
   of both contracts, not a mock).

## 5. The Design

See `src/Tempest.Core/Runtime/ITempestHost.cs`/`TempestHost.cs` and
`src/Tempest.App/Shell/` in full — implemented without deviation from
`ADR-0033`–`ADR-0035`'s own code skeleton. `ITempestHost` gains
`Services` (`ITempestServiceProvider?`), set once `TempestHost` finishes
building its container (immediately after the existing "Dependency
Injection Built" log line), guarded by the same `_gate` lock `State`
already uses. `TempestShell` implements `IEventHandler<NavigationRequestedEvent>`
and `IAsyncDisposable`: its constructor accepts an `ITempestHost` (in
`HostState.Created`), a `TextWriter`, and a `TextReader`, and registers
two built-in `PlaceholderPage`s keyed by
`NavigationSampleModule.NavigationItemId`/
`SecondaryNavigationSampleModule.NavigationItemId`. `RunAsync` starts the
Host's own `RunAsync` as a background task, waits for `Services` to
become non-`null`, resolves `INavigationProvider`/`IEventBus`, subscribes
itself, renders the Navigation Region and a reserved Status Bar line,
then runs an input loop translating numbered selections into
`Navigate(id)` calls until `0` is entered, at which point it calls
`StopAsync()` on the Host and awaits its background run task.
`Program.cs` itself is now four lines: build the host, construct the
Shell, `await using`, `RunAsync()`.

## 6. Alternatives Considered

None — this Work Package implements already-decided ADRs exactly, per
its own explicit brief. No new architectural alternative was evaluated
here; see `ADR-0033`–`ADR-0035` and their own retrospective (`WP 5.0C`)
for the alternatives the design phase weighed and rejected (`RD-0034`–
`RD-0037`).

## 7. Why This Solution Was Chosen

Not applicable in the usual sense — the solution was chosen by
`ADR-0033`–`ADR-0035`. This Work Package's own judgment calls were
narrow: using a numbered-selection input scheme (mirroring the
bootstrap-era console loop's own convention) rather than free-text
`Id` entry, since a number is shorter to type and matches what the
Navigation Region already renders; and giving `PlaceholderPage` a single,
reusable shape for both the Shell's own built-in pages and the generic
unknown-item case, since neither needs anything beyond a title and a
message.

## 8. Architectural Principles

- **Reuse Before Invention** — `Services` reuses `ITempestServiceProvider`
  unchanged; the Shell's own subscription to `NavigationRequestedEvent`
  reuses the exact shape `ClockLifecycleObserverModule` already proved
  for `ClockModuleLifecycleEvent`.
- **Minimal Host Complexity** — confirmed, not merely claimed:
  `TempestHost.cs` gained one field, one property, and three lines inside
  `ExecuteStartupPhasesAsync`; `RuntimeModuleManager`,
  `ModuleLifecycleManager`, `ReflectionFrameworkDiscoveryService`, and
  `TempestServiceProvider` are unchanged.
- **Platform Layering** (`ADR-0023`) — `Tempest.App` now depends downward
  on `Tempest.Core` and `Tempest.Samples`; neither gains any reference
  upward to `Tempest.App` or the Shell.
- **Fail Fast** — `TempestShell`'s constructor validates every argument
  immediately; `PlaceholderPage` validates its own title/message the same
  way `NavigationItem` already does.

## 9. Benefits

- **`Tempest.App` runs the real platform for the first time in this
  project's history** — confirmed by direct execution, not merely by
  test: `dotnet run` against the built application shows real Module
  Discovery finding all five `Tempest.Samples` modules, real navigation
  rendering, and a graceful shutdown.
- **Every scenario `ADR-0033`–`ADR-0035` named is now proven, not merely
  designed** — 46 new tests exercise `ITempestHost.Services`,
  `TempestShell`, and `PlaceholderPage` directly, including a full
  interactive session driven by a real `StringReader` end to end.
- **The duplicate-navigation-ID failure mode designed at `WP 5.0B` is now
  proven all the way through the Shell** — running the real application
  with `DuplicateNavigationSampleModule` present shows it isolated
  (`Failed during Initialise`) while the Shell continues presenting the
  two successfully-registered items normally, the Host reaching
  `Running`, not `Faulted`.
- **`ADR-0017`'s own boundary survives contact with a real, external
  consumer** — `Services` resolves `IEventBus`/`INavigationProvider`
  correctly while `IFrameworkDiscoveryService`/`IRuntimeModuleManager`/
  `IModuleLifecycleManager` all still throw `ServiceNotRegisteredException`,
  proven directly, not merely asserted.

## 10. Trade-offs

- No automatic unregistration of the Shell's own `IEventBus` subscription
  on an ungraceful exit — `StopAsync` unsubscribes explicitly; a process
  killed outright skips it, exactly the same accepted gap `ADR-0028`
  already discloses for any Event Bus subscriber.
- The bootstrap-era `BootstrapService`/`HostingService`/`ProjectService`
  code remains in the repository, entirely unreferenced by `Program.cs`
  now — untouched and unmigrated, exactly as `WP 5.0C` scoped it, not a
  gap discovered here.
- Module- or plugin-contributed page rendering still has no answer — a
  real, disclosed limitation (`ADR-0035`, `RD-0036`), unchanged by this
  Work Package.

## 11. Common Mistakes

The mistake most worth naming here is one found and fixed, not merely
avoided: assuming that referencing `NavigationSampleModule.NavigationItemId`
in the Shell's own constructor would force `Tempest.Samples` to load into
the process before Module Discovery ran. It does not — `NavigationItemId`
is a compile-time `const`, inlined directly into `Tempest.App`'s own IL;
running the real application before the fix showed Discovery finding
"0 module(s)" despite `Tempest.Samples` being referenced in the `.csproj`.
An explicit `typeof(NavigationSampleModule).Assembly` access, added to the
constructor with a comment explaining why, closed the gap. See
`docs/academy/02 Runtime Architecture/10-shell-and-application-composition.md`'s
own "Common Architectural Mistakes" section for the general lesson.

## 12. Future Evolution

- **`WP 5.1` (Command Framework)**, once implemented, becomes a new
  source of Shell input handling — a dispatched command's own handler
  calling `NavigationService.Navigate(...)`, exactly as `ADR-0022`
  already illustrates — with no change anticipated to `TempestShell`'s
  own composition.
- **`WP 5.2` (Diagnostics)** populates the Shell's already-reserved
  Status Bar region and may register its own `NavigationItem`, proving
  the placeholder-then-real-page path against a second, non-synthetic
  consumer.
- **Module- or plugin-contributed page rendering** remains a named,
  deferred extension point (`ADR-0035`, `RD-0036`) — unchanged by this
  implementation.

## 13. Key Takeaways

1. Implementing an already-fully-designed set of ADRs closely is a
   narrow, low-risk exercise precisely because the hard questions were
   already answered — this Work Package's only real judgment calls were
   the input scheme and the shared placeholder-page shape, not new design
   decisions.
2. Running the real application, not just the test suite, found a real
   bug (`const` fields not forcing assembly load) that no unit test
   happened to exercise, since every test already referenced
   `Tempest.Samples` types directly elsewhere in the same test assembly
   — a genuine argument for "actually run it" over "the tests pass,"
   consistent with this project's own UI/frontend validation discipline.
3. A minimum viable implementation that resists scope creep (no colours,
   no themes, no dialogs) is still a complete, real proof of every
   architectural claim it was built to demonstrate — smallness and
   rigor are not in tension here.

---

## Architectural Debt Assessment

**No new debt introduced.** The trade-offs named above (no automatic
unsubscription on an ungraceful exit; unmigrated bootstrap-era code;
module/plugin-contributed rendering still unsolved) are each `ADR-0028`'s,
`WP 5.0C`'s, or `ADR-0035`'s own already-disclosed, accepted scope
boundaries, not new debt discovered here. Every other debt item on
record from the Foundation phase and `WP 5.0A`–`WP 5.0C` remains exactly
as previously described.

## Observations

- **Files added**: `src/Tempest.App/Shell/IPage.cs`; `PlaceholderPage.cs`;
  `TempestShell.cs`; `tests/Tempest.Core.Tests/Shell/TempestShellTests.cs`;
  `PlaceholderPageTests.cs`; this retrospective.
- **Files modified**: `src/Tempest.Core/Runtime/ITempestHost.cs` (the
  `Services` property); `TempestHost.cs` (one field, one property
  implementation, three lines inside `ExecuteStartupPhasesAsync`);
  `src/Tempest.App/Program.cs` (rewritten as the real entry point — the
  bootstrap-era `BootstrapService`/`HostingService`/`ProjectService`
  source files themselves are untouched); `src/Tempest.App/Tempest.App.csproj`
  and `tests/Tempest.Core.Tests/Tempest.Core.Tests.csproj` (new project
  references); `tests/Tempest.Core.Tests/Runtime/TempestHostTests.cs`
  (8 new `Services` tests).
- **Tests added**: 46 — `ITempestHost.Services` availability, resolution,
  singleton identity, persistence through Stop/Dispose, and the
  Discovery/Registration/Lifecycle non-exposure proof (8); `TempestShell`
  construction and argument validation (4); Host startup and service
  resolution (2); title/Navigation Region/Status Bar rendering (4);
  multiple built-in pages and page-to-page distinctness (3); unknown-page
  placeholder (1); navigation selection including invalid input (6);
  event handling through the real Event Bus (1); a full interactive
  session driven by a real `StringReader` (2); graceful shutdown and
  disposal (2); repeated startup/shutdown across fresh instances (1);
  Shell composition from a real `TempestHostBuilder` (1); duplicate-
  navigation-module isolation observed through the Shell (1);
  `PlaceholderPage` construction, validation, and rendering (9, in its own
  file).
- **Test results**: 446 of 446 passing (400 pre-existing + 46 new), 0
  failures, stable across repeated runs.
- **Build results**: 0 warnings, 0 errors.
- **Manual verification**: the built application was run directly
  (`dotnet run`) with piped input, twice — once before the `const`-field
  fix (confirming the bug: "0 module(s) found"), once after (confirming
  5 modules discovered, Home/Settings pages rendering correctly, the
  duplicate module isolated without faulting the Host, and a clean exit).
- **Platform changes outside `ITempestHost.Services` and
  `Tempest.App/Shell/`**: none. `RuntimeModuleManager`,
  `ModuleLifecycleManager`, `ReflectionFrameworkDiscoveryService`,
  `TempestServiceProvider`, and every existing `Tempest.Core.Navigation`/
  `Tempest.Core.Events` type are unchanged.
- **Readiness assessment**: `WP 5.0D` is complete. `ADR-0033`–`ADR-0035`
  are fully realised and proven against the real Host, real sample
  modules, and a real interactive session. `Tempest.App` is, for the
  first time, a genuine consumer of the platform it ships with —
  `WP 5.1`'s Command Framework may now proceed as its own, separate Work
  Package against a fully validated, real Shell.
