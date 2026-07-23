# ADR-0023: Platform Layering — Dependencies Flow Downward Only

## Status

Accepted — v0.4.0 release planning, 2026-07-23. Applies platform-wide, not
only to this release — see `docs/releases/FOUNDATION.md`, which this ADR is
cross-referenced from.

## Context

v0.4.0 introduces several new platform capabilities at once (Event Bus,
Background Services, Navigation, Command Framework) alongside a formal
contracts phase (`WP 4.0`). Rather than deciding, ad hoc, per work package,
how each new piece is allowed to depend on the others and on what already
exists, this release names one explicit, checkable rule.

This is substantially a **formalisation of boundaries the Runtime
Foundation already established independently**, not a new constraint
invented for this release:

- ADR-0013 already separates platform-service failure from module failure.
- ADR-0017 already keeps Discovery, Registration, and Lifecycle Host-owned
  and out of modules' reach.
- ADR-0020 already forbids one module depending on another directly.
- `Runtime Host Architecture.md` already states the Host owns no business
  logic, as an explicit non-responsibility.

Each of these was decided independently, for its own reason, at a
different point in this project's history. This ADR is what happens when
they are read together: they are all instances of one general rule that
had never been named as such.

## Decision

Four layers. Dependencies flow downward only:

```
Modules
   ↓
Platform APIs      (contracts — IModule, IEventBus, ICommand, IHostedService,
                     INavigationProvider, etc. — defined by WP 4.0 and each
                     capability's own work package, never speculatively)
   ↓
Platform Services  (concrete implementations — Configuration, Logging,
                     Event Bus, Background Services, and so on)
   ↓
Runtime Host       (constructs and orchestrates Platform Services;
                     drives Modules through Lifecycle)
```

Explicitly forbidden, in every case, without exception:

- **Service → Module.** No Platform Service may depend on, reference, or
  hold a reference to any specific module or module type.
- **Module → Module.** No module may depend on, reference, or hold a
  reference to another module directly — ADR-0020's existing prohibition,
  restated here as one instance of this general rule, not a separate one.
- **Runtime → Feature.** The Runtime Host may never depend on, reference,
  or contain business/domain-specific logic — already stated informally as
  a non-responsibility in `Runtime Host Architecture.md`; restated here as
  a formal layering rule, not merely an intention.

## Consequences

**Positive:**

- Gives every future work package one simple, checkable question for
  review: *does this dependency point downward?* — rather than re-deriving
  the reasoning behind ADR-0013, ADR-0017, and ADR-0020 from scratch each
  time a new capability is proposed.
- Names, for the first time as a general concept, the distinction between
  a **Platform API** (a contract — `WP 4.0`'s own deliverable) and a
  **Platform Service** (a concrete, constructed implementation) — a
  distinction the Runtime Foundation's own documents already used
  informally (Configuration the service vs. `IConfigurationProvider` the
  contract) but never named as a general, four-layer architecture.
- Consolidates, rather than adds to, existing constraints — this ADR is
  presented as a unification of what ADR-0013, ADR-0017, and ADR-0020
  already required, not a new rule layered on top of possibly-conflicting
  ones.

**Negative:**

- A fourth named layer ("Platform APIs," distinct from "Platform Services")
  is new vocabulary a future contributor must learn, alongside the
  existing Host/Platform Service/Module vocabulary the Engineering
  Glossary already maintains.
- Enforcement today is a review discipline — a reviewer checks the
  direction of a proposed dependency — not a compiler-enforced or
  architecture-tested constraint. A future work package could add an
  automated dependency-direction check if violations become a recurring
  review finding; none exists yet, and this ADR does not mandate one.

## Future Considerations

If this layering rule is violated even once and only caught in review
rather than earlier, consider whether an automated architecture-conformance
test (checking assembly or namespace dependency direction) is warranted.
This ADR decides the rule; it does not decide how the rule is enforced
beyond review discipline.
