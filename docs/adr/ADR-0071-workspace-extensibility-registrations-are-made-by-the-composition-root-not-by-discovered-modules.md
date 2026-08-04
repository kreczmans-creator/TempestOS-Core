# ADR-0071: Workspace Extensibility Registrations Are Made by the Composition Root, Not by Discovered Modules

## Status

Accepted — `v0.8.0` "Engineering Workspace", `WP 8.1B` (Navigation &
Project Explorer), 2026-08-04. Corrects a genuine, disclosed
implementation-phase finding against `ADR-0067`'s own stated assumption.

## Context

`ADR-0067` decided Workspace extensibility is Kind-keyed registration
(`IWorkspaceManager.RegisterView`/`RegisterExplorerArea`), and its own
Decision section states: "a future Engineering Discipline Module's own
composition code calls both registration methods once, exactly as it
already calls `INavigationProvider.Register`/`ICommandRegistry.
RegisterDescriptor` today." Building the first real registrations
against this mechanism (`WP 8.1B`'s own living reference content,
`Tempest.App.Workspace.Samples`) found this assumption does not hold.

`INavigationProvider`/`ICommandRegistry` are Platform Services, resolved
through `ITempestHost.Services` — any module running inside the Host can
reach them via ordinary constructor injection, exactly as
`NavigationSampleModule`/`CommandSampleModule` already demonstrate.
`IWorkspaceManager`, by contrast, is explicitly **not** a Platform
Service (`ADR-0062`) — it is a composition-root component, constructed
directly by `Tempest.App`'s own entry point, never resolved through
`ITempestHost.Services`. A module has no path to reach it: it cannot be
injected (it is not registered in the DI container), and a module
running inside the Host has no ambient reference to the
`WorkspaceManager` instance wrapping that Host from the outside. The
Host/Workspace boundary that makes `ADR-0062` correct (the Workspace
wraps a Host, not the reverse) is the same boundary that makes
`ADR-0067`'s own worked example impossible.

## Decision

**`IWorkspaceManager.RegisterView`/`RegisterExplorerArea` calls are made
by `Tempest.App`'s own composition root (`Program.cs`), immediately
after constructing `WorkspaceManager` — never by a discovered
`IModule`.** A module may still register a `NavigationItem`
(`INavigationProvider`) to name the *area* a future registration will
populate (exactly as `WorkspaceExplorerSampleModule` does here), but the
Workspace-specific registration calls themselves belong to the
composition root, which already holds the one `WorkspaceManager`
instance a module can never reach. `WP 8.1B`'s own living reference
content follows this shape exactly: `WorkspaceExplorerSampleModule`
(a real, discovered `IModule`) registers only its own `NavigationItem`;
`Program.cs` separately calls `manager.RegisterExplorerArea`/
`RegisterView` with the actual sample provider/factory instances.

This does not change `ADR-0067`'s own core decision (Kind-keyed
registration, two registries, one per extension point) — only *who*
calls the registration methods, and *where* in the platform's own
composition that call belongs.

## Consequences

**Positive:**

- Corrects a genuine gap between `ADR-0067`'s own stated worked example
  and the real Host/Workspace boundary `ADR-0062` already established —
  found and fixed before any real Engineering Discipline Module needed
  to discover it independently.
- Keeps the Host/Workspace separation (`ADR-0062`) uniform: nothing
  Workspace-specific ever needs to be reachable from inside a
  Host-discovered module, matching `WorkspaceManager`'s own existing
  "never DI-registered" design exactly.
- A future real registration (Requirements, most naturally, per
  `WP8.0B Workspace Contracts.md`'s own Recommendations) now has an
  unambiguous, proven shape to follow — `Program.cs` grows one or two
  more registration calls, not a redesigned extension mechanism.

**Negative:**

- `Program.cs` grows a small, disclosed coupling to whichever discipline
  modules the platform ships sample or production content for — a cost
  already accepted implicitly for `WP 8.1A`'s own forced
  `Tempest.Samples` assembly load, extended here to explicit
  registration calls.
- A module's own `NavigationItem` registration and its own
  Workspace-content registration are now two separate, uncoordinated
  steps (one inside the Host, one in the composition root) that must
  agree on the same area Id string — a small, disclosed ergonomic cost,
  mirroring `ADR-0067`'s own already-accepted "two registration calls,
  not one" trade-off.

## Alternatives Considered

**Giving a module a reference to `IWorkspaceManager` via a new,
Workspace-aware module base class or DI registration** — considered and
rejected. This would require registering `WorkspaceManager` (or an
abstraction over it) into the Host's own DI container, directly
contradicting `ADR-0062`'s own decision that the Workspace is not a
Platform Service and never resolved through `ITempestHost.Services` —
reopening a settled architectural boundary for a smaller ergonomic
convenience.

**Leaving `ADR-0067`'s own worked example uncorrected, treating
`Program.cs` registration as an unstated implementation detail** —
considered and rejected. `ADR-0067` is an Accepted, authoritative
record; leaving a stated assumption inside it that this Work Package
directly disproved would mislead a future reader relying on that ADR's
own Decision section, the same reasoning `WP 8.1A` already applied to
its own two disclosed findings against `WP8.0B Lifecycle
Definitions.md`.

## Related Documents

`ADR-0062`; `ADR-0067`; `WP8.1B Implementation Report.md`;
`src/Tempest.App/Program.cs`; `src/Tempest.App/Workspace/Samples/`;
`src/Samples/Tempest.Samples/WorkspaceExplorerSampleModule.cs`.
