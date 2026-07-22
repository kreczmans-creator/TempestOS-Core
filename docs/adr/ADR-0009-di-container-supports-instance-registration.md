# ADR-0009: The DI Container Supports Registering Pre-Built Instances

## Status

Accepted — WP 2.5 (Configuration Framework), 2026-07-22.

## Context

WP 2.5's requirement #6 stated: "Register `IConfigurationProvider` in the DI
container. Configuration shall be available to every runtime service." But
`IConfigurationProvider`'s concrete implementation, `ConfigurationProvider`, has
an `internal` constructor requiring an already-merged dictionary of values — a
dictionary only `ConfigurationBuilder.Build()` can produce, by loading and
validating whatever `IConfigurationSource` instances a caller supplied at
runtime. There is no way for `TempestServiceProvider` to construct a
`ConfigurationProvider` itself via reflection: the value it needs doesn't come
from resolving other registered services recursively, it comes from an
entirely separate build process that has to run first, once, outside the
container.

As of WP 2.4, `IServiceCollection`/`TempestServiceProvider` supported exactly
one registration shape: map a service type to an implementation type the
container itself would construct via reflection. Nothing supported registering
an object that already existed.

## Decision

`IServiceCollection` gained a new method, `AddInstance(Type serviceType, object
instance)` (plus a generic `AddInstance<TService>` extension-method overload,
matching the existing `Singleton`/`Transient` convention). `ServiceDescriptor`
gained a new, optional `ExistingInstance` property (`object?`, defaulting to
`null` for every existing registration shape). `TempestServiceProvider`'s
constructor now iterates the supplied `IServiceCollection`'s descriptors and
pre-seeds its own singleton-instance cache with any descriptor's
`ExistingInstance` — meaning `GetService` returns it via the *existing*
singleton-cache lookup that already ran before any construction was attempted.
No change was made to `Resolve` or `Construct` at all.

## Consequences

**Positive:**

- **Zero changes to the container's actual resolution logic.** Because
  `TempestServiceProvider.Resolve` already checked its singleton cache before
  attempting construction (a pre-existing WP 2.4 behaviour, there to support
  ordinary singleton registrations), pre-seeding that same cache with an
  existing instance was sufficient on its own — `Resolve`/`Construct` needed no
  new branch, no new special case, nothing.
- **One registry, not two.** A future consumer resolving a service through
  `TempestServiceProvider` does not need to know or care whether it was
  registered via `Add` (reflection-constructed) or `AddInstance`
  (pre-supplied) — both go through the exact same `GetService` call, and both
  are visible in the exact same `Descriptors` collection.
- **Reusable beyond configuration.** Any future runtime-supplied value the
  container cannot construct via reflection — a value read from the OS
  environment before the container exists, a connection object handed in from
  outside the composition root — can use this same mechanism, not a
  configuration-specific workaround.
- **Backward compatible.** `ExistingInstance`'s optional, defaulted parameter
  means every existing call to `new ServiceDescriptor(serviceType,
  implementationType, lifetime)` across WP 2.1–2.4's code and tests continues to
  compile and behave identically, unchanged.

**Negative:**

- **A second registration concept to learn.** `IServiceCollection` now has two
  genuinely different ways to register something — `Add` (or its
  `Singleton`/`Transient` sugar) versus `AddInstance` — and a reader
  encountering `AddInstance` for the first time needs to understand *why* it
  exists (some things can't be constructed by the container) rather than
  assuming it's simply a shorthand for something `Add` could already do.
- **`AddInstance` is always, implicitly, a singleton.** There is no way to
  register a pre-built instance as anything else, since a "transient pre-built
  instance" isn't a coherent concept in the first place — but this is worth
  stating explicitly rather than leaving a reader to infer it, since every
  other registration method takes an explicit `ServiceLifetime` argument and
  `AddInstance` conspicuously doesn't.
- **No disposal tracking**, for the exact same reason already noted in the
  WP 2.4 retrospective for ordinary constructed singletons: if a pre-built
  instance implements `IDisposable`/`IAsyncDisposable`, nothing in
  `TempestServiceProvider` will ever call it. This gap is not made any better
  or worse by this ADR — it simply now applies to `AddInstance`-registered
  values too, in addition to constructed singletons.

## Future Considerations

If `TempestServiceProvider` ever gains its own disposal-tracking mechanism (see
the WP 2.4 retrospective's Future Evolution section), it should account for
instances registered via `AddInstance` in exactly the same way as constructed
singletons — there is no reason for the two registration paths to behave
differently with respect to disposal, and a future implementation should not
introduce that asymmetry by treating `ExistingInstance`-backed descriptors as a
special case to skip.
