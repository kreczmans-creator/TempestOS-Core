# ADR-0100: External Controller Integration Is an `IInputBindingProvider` Abstraction — No Vendor SDK, One Real Keyboard Provider, One Test-Only Stub Controller

## Status

Accepted — `v0.10.0` "User Experience & Desktop Application", `WP 10.6A` (Command Execution & Productivity Experience), 2026-08-10.

## Context

`WP 10.6A`'s own controlling instruction names an "External controller
integration abstraction (Stream Deck, macro keyboards, etc.)," explicit
that "every Tempest command can later be bound to... without changing
the Command Framework," and equally explicit, in its own Out-of-Scope
section, that no vendor SDK, Stream Deck plugin, or hardware integration
is to be built this Work Package. The design question this leaves open is
architectural, not implementation: what does the abstraction itself look
like, concretely enough to prove it works, without any real hardware
dependency to prove it against?

`IEventBus` (`ADR-0028`) already establishes this platform's own answer
to an adjacent question — "many independent producers, one shared
dispatch surface, one producer's own failure isolated from every other" —
for domain events. The same shape fits an input source raising "invoke
this command" requests: a keyboard, a Stream Deck, a MIDI controller are
all, from the router's own perspective, indistinguishable producers of
the identical signal (a Command Id string).

## Decision

**`IInputBindingProvider`** (`Tempest.Core.Input`) is the minimal
contract: a `SourceName` and one event, `CommandRequested(string
commandId)`. **`IExternalControllerProvider : IInputBindingProvider`**
adds only `IsConnected` — the one property distinguishing a physical,
external device from an always-present software source (keyboard,
mouse). **`IInputBindingRegistry`/`InputBindingRouter`** (a Platform
Service, DI-singleton alongside `ICommandRegistry`) is the one shared
dispatch surface: `Register`/`Unregister` a provider, and every
registered provider's own `CommandRequested` is routed to
`ICommandRegistry.InvokeAsync` — the identical entry point the Command
Palette already uses, so a bound input source reaches precisely the same
commands (including a Macro, `ADR-0099`) with zero additional dispatch
logic. A throwing provider or a failed invocation is caught and logged
inside the router itself, never propagated back into the raising
provider's own code — the identical subscriber-isolation discipline
`ADR-0028` already established for `IEventBus`, applied here so one
misbehaving controller cannot destabilise every other bound input
source.

**One real implementation**: `KeyboardCommandBindingProvider`
(`Tempest.Desktop.Input`) — a genuine, working `KeyGesture → Command Id`
map, `Bind`/`Unbind`, wired into `MainWindow`'s own `KeyDown` (after the
fixed `KeyboardShortcuts` bindings, which take priority for a shared
gesture). Proves the abstraction against a real, already-shipped input
source. Ships with zero default bindings this Work Package — no
remapping UI is built (disclosed, real future work); the mechanism
itself is real and tested.

**One test-only implementation**: `StubExternalControllerProvider`
(`Tempest.Core.Tests`) — `IsConnected = true`, a `SimulatePress(commandId)`
method standing in for a physical button press. Proves an
`IExternalControllerProvider`-shaped provider drives the identical router
a real Stream Deck/MIDI/game-controller integration would, with zero
Command Framework changes, without depending on any vendor SDK or
physical hardware this environment cannot access. Permanently a test
double — this ADR does not anticipate it becoming production code; a
real vendor integration is its own, later, out-of-scope Work Package,
implementing `IExternalControllerProvider` directly against a real SDK.

## Consequences

**Positive:**

- A future Stream Deck/MIDI/game-controller integration needs to
  implement exactly one small interface and call `Register` once — no
  Command Framework change, no Desktop `MainWindow` change beyond that
  one registration call.
- `KeyboardCommandBindingProvider` and any future hardware provider are
  provably interchangeable from the router's own perspective — proven
  today by `StubExternalControllerProvider` exercising the identical
  code path a real device would.
- Isolates a misbehaving input source at its own boundary, mirroring
  `IEventBus`'s own already-accepted, already-tested precedent —
  no new isolation mechanism invented.

**Negative:**

- No real external controller exists yet — by design, and by explicit
  Out-of-Scope instruction; this ADR documents an abstraction proven
  against one real (keyboard) and one simulated (stub) provider, not a
  shipped Stream Deck/MIDI integration.
- `KeyboardCommandBindingProvider` ships with no end-user configuration
  UI — real, disclosed future work, distinct from this ADR's own
  architectural scope.

## Alternatives Considered

**A single, hard-coded `enum InputSource { Keyboard, StreamDeck, Midi,
... }` with one router method per case.** Considered and rejected —
directly contradicts "without changing the Command Framework" for every
future binding target: adding a seventh input source would mean editing
the enum and the router itself, exactly the coupling the brief's own
abstraction requirement exists to avoid.

**Building a real Stream Deck plugin now, to prove the abstraction
against genuine hardware rather than a test double.** Rejected outright
by this Work Package's own explicit Out-of-Scope instruction (no vendor
SDK, no Stream Deck plugin, no hardware integration) — `StubExternal
ControllerProvider` is the disclosed, deliberate substitute, proving the
identical router logic without the disallowed dependency.

## Related Documents

`ADR-0028`; `ADR-0036`; `ADR-0070`; `ADR-0099`;
`src/Tempest.Core/Input/IInputBindingProvider.cs`;
`src/Tempest.Core/Input/IExternalControllerProvider.cs`;
`src/Tempest.Core/Input/IInputBindingRegistry.cs`;
`src/Tempest.Core/Input/InputBindingRouter.cs`;
`src/Tempest.Desktop/Input/KeyboardCommandBindingProvider.cs`;
`tests/Tempest.Core.Tests/Input/StubExternalControllerProvider.cs`;
`docs/releases/v0.10.0/WP10.6A Implementation Report.md`.
