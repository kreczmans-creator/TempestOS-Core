# ADR-0007: The Service Provider Owns All Module Construction

## Status

Accepted — WP 2.4 (Dependency Injection), 2026-07-22.

## Context

Before WP 2.4, `ModuleLifecycleManager` (WP 2.3) called `Activator.CreateInstance`
directly to construct a module the first time it was initialised. WP 2.4's stated
objective was explicit: "The runtime shall no longer construct module instances
directly using `Activator.CreateInstance()`. Instance creation becomes the
responsibility of a service provider," and fixed the architecture's responsibility
boundaries as: Discovery discovers, Registration registers, Lifecycle
orchestrates, Service Provider creates.

## Decision

`ModuleLifecycleManager`'s constructor now requires an `ITempestServiceProvider`.
Its private `ResolveInstance` method — the only place in the class that obtains a
module instance — calls `_serviceProvider.GetService(descriptor.ModuleType)`
instead of `Activator.CreateInstance(descriptor.ModuleType)`. This is the only
change WP 2.4 made to `ModuleLifecycleManager`'s behaviour; everything else
(ordering, state transitions, failure handling) is untouched.

`ReflectionFrameworkDiscoveryService`'s own, separate `Activator.CreateInstance`
call — used to transiently instantiate a type purely to read its `Id`/`Name`/
`Version` during discovery, before any container exists — was deliberately left
unchanged. See ADR-0008.

## Consequences

**Positive:**

- **One instantiation site for "real" module instances, full stop.** Anyone
  wanting to understand how a module actually comes into existence at runtime has
  exactly one method to read: `ModuleLifecycleManager.ResolveInstance`, which
  delegates immediately to the service provider.
- **Modules can now have real dependencies.** Before WP 2.4, a module's
  constructor could only ever be parameterless (that's all `Activator.CreateInstance(Type)`
  supports without a lot of extra plumbing). Now a module can declare
  `IModuleLifecycle`-implementing types with constructors depending on
  `ILogger`-style services, configuration objects, or other registered services —
  the entire point of introducing DI in the first place.
- **Construction failures are now first-class, descriptive exceptions** —
  `ServiceNotRegisteredException`, `CircularServiceDependencyException`,
  `AmbiguousConstructorException` — instead of whatever `Activator.CreateInstance`
  happens to throw (typically an unhelpful `MissingMethodException` for a missing
  parameterless constructor).

**Negative:**

- Every caller constructing a `ModuleLifecycleManager` must now also construct and
  wire up a `ServiceCollection`/`TempestServiceProvider`, register every
  discoverable module's concrete type into it (via `AddDiscoveredModules`), and
  keep that registration step in sync with discovery's output. This is more setup
  than the previous single-argument constructor required, and is not enforced by
  the type system — nothing stops a caller from constructing a
  `ModuleLifecycleManager` with a `TempestServiceProvider` that has no
  registrations at all, which would then fail at `Initialise` time instead of at
  construction time.
- **A subtle bug had to be found and fixed as a direct consequence of this change**
  (see the WP 2.4 completion report and the accompanying case study): resolving an
  instance was originally called inside the state-transition lock but outside the
  try/catch that marks a module `Failed`. Before WP 2.4, a bare
  `Activator.CreateInstance` call on a parameterless-constructor type essentially
  couldn't fail; after WP 2.4, missing dependencies, circular dependencies, and
  ambiguous constructors are all realistic failures at exactly that call site, and
  the existing code hadn't been written with that in mind.

## Future Considerations

The composition-root wiring (discovery → registration → `AddDiscoveredModules` →
`TempestServiceProvider` → `ModuleLifecycleManager`) is currently assembled by hand
in every caller (and in every test). If this wiring grows more elaborate — for
example, if non-module services also need registering before modules can resolve
their own dependencies — a small, explicit composition-root helper may be worth
introducing, so the correct order of operations is encoded once rather than
repeated at every call site.
