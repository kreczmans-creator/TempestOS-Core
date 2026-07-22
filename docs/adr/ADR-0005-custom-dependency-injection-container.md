# ADR-0005: Build a Custom, Minimal Dependency Injection Container

## Status

Accepted — WP 2.4 (Dependency Injection), 2026-07-22.

## Context

WP 2.4 needed constructor injection, singleton/transient lifetimes, and
descriptive resolution failures. The overwhelmingly obvious industry choice for
this in a .NET project is `Microsoft.Extensions.DependencyInjection` — mature,
free, extremely widely used, and would have taken a fraction of the time to wire
in compared to building one from scratch.

WP 2.4's brief explicitly forbade it, along with Autofac and any other
third-party container, and explicitly instructed: "Implement the TempestOS
container."

## Decision

TempestOS has its own dependency injection container: `IServiceCollection` /
`ServiceCollection` for registration, `ITempestServiceProvider` /
`TempestServiceProvider` for resolution, in a new `Tempest.Core.DependencyInjection`
namespace, with zero external package dependencies.

## Consequences

**Positive:**

- **No third-party surface area in the dependency tree.** TempestOS's build,
  licensing, and security posture aren't coupled to a package outside the
  project's own control. For a project self-describing as heading toward
  "professional commercial software," this is a deliberate, defensible stance —
  not every project needs to make this trade, but this one has chosen to.
- **The container's behaviour is exactly as complex as TempestOS needs and no
  more.** No scoped lifetime (TempestOS has no request/unit-of-work concept to
  scope to), no open-generic registration support, no keyed services, no
  `IServiceProviderFactory` extensibility model. Every one of
  `Microsoft.Extensions.DependencyInjection`'s more advanced features that
  TempestOS doesn't need is simply absent, rather than present-but-unused
  complexity.
- **Full control over failure messages.** Requirement #7 of WP 2.4 (construction
  chain, requested service, and missing dependency all identified in every
  resolution failure) was achievable exactly to specification, since the whole
  exception hierarchy is TempestOS's own — see ADR-0007 for the resolution
  mechanics this enables.

**Negative:**

- **Reinventing a wheel the wider .NET ecosystem has already spent years hardening.**
  `Microsoft.Extensions.DependencyInjection` has handled edge cases (open generics,
  `IEnumerable<T>` multi-registration, disposal tracking of singleton instances —
  see the WP 2.4 completion report's Observations) that TempestOS's container does
  not yet handle, simply because nobody has needed them yet. Every future need
  along those lines is now TempestOS's own engineering cost to design and build,
  not a free upgrade from taking a NuGet package update.
- **No community, no Stack Overflow answers, no existing familiarity for engineers
  joining the project.** A new engineer who already knows
  `Microsoft.Extensions.DependencyInjection` has to learn TempestOS's container
  specifically; none of their existing DI knowledge transfers directly (though the
  concepts — singleton/transient, constructor injection — are the same).

## Future Considerations

If TempestOS's dependency graph grows to need capabilities the current container
doesn't have (disposal tracking of singletons is the most likely first gap — see
the WP 2.4 completion report), those should be added deliberately, one at a time,
each justified by an actual need rather than spec'd in speculatively. If the
container's scope ever grows large enough that maintaining it costs more than the
independence from a third-party package is worth, that trade-off should be
revisited explicitly — not silently reversed.
