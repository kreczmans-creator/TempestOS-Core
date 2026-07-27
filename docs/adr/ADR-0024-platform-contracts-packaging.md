# ADR-0024: Platform Contracts Are Packaged by Capability, Not a Shared Contracts Namespace

## Status

Accepted — v0.4.0, WP 4.0 (Platform Contracts), 2026-07-23.

## Context

`WP 4.0` introduces five genuinely new interfaces — `IHostedService`,
`ICriticalBackgroundService`, `ICommand`, `IEvent`, `IEventHandler<T>` —
and re-affirms two that already exist (`IModule`, `IModuleLifecycle`).
Two packaging questions needed a decision before any code was written.

**Where do the five new interfaces live?** Two options were considered:
a single new `Tempest.Core.Contracts` namespace holding all of them
together, grouped by "these are contracts" rather than by what they are
for; or distributing each across its own capability-specific namespace,
matching how every existing part of the platform is already organised
(`Tempest.Core.Configuration`, `Tempest.Core.Logging`,
`Tempest.Core.Modules`, `Tempest.Core.DependencyInjection`,
`Tempest.Core.Runtime` — one namespace per capability, holding both its
contracts and its implementations together).

**Does `IHostedService` need a different name to avoid confusion with
`Microsoft.Extensions.Hosting.IHostedService`?** That type is well-known
elsewhere in the .NET ecosystem. TempestOS depends on no such package
(ADR-0005), so there is no compiler-level collision risk — unlike
`ITempestServiceProvider`, which was named specifically to avoid an
*actual* ambiguous-reference error against `System.IServiceProvider`
(`System` being an implicit global using every file already has). This is
a purely human-familiarity concern, not a build-breaking one.

## Decision

**Contracts are packaged by capability, not gathered into a shared
`Contracts` namespace.** Each new interface lives in a namespace matching
the capability it belongs to, mirroring every existing part of the
platform:

- `IHostedService`, `ICriticalBackgroundService` → `Tempest.Core.BackgroundServices`
  (a new namespace — deliberately not `Tempest.Core.Hosting`, which
  ADR-0016 already reserved for environment/deployment adapters; reusing
  it here would recreate the exact naming confusion ADR-0016 exists to
  prevent).
- `ICommand` → `Tempest.Core.Commands` (new namespace).
- `IEvent`, `IEventHandler<T>` → `Tempest.Core.Events` (new namespace).
- `IModule`, `IModuleLifecycle` remain exactly where they are —
  `Tempest.Core.Modules` — unmoved. "Re-affirmed, not redefined" means no
  file relocation, no signature change; `WP 4.0`'s only claim on them is
  documenting them as part of the same platform-contracts family.

Each new namespace is where its owning work package (`WP 4.4` Event Bus,
`WP 4.5` Background Services, `WP 4.7` Command Framework) will add its
concrete implementation — the contract and its implementation share a
namespace, exactly as `Tempest.Core.Logging` already holds both `ILogger`
and `Logger` together.

**`IHostedService` keeps its name.** No rename. The XML documentation on
the interface states plainly that it is unrelated to
`Microsoft.Extensions.Hosting.IHostedService` and that TempestOS has no
dependency on that package — the same clarifying-note treatment ADR-0016
gave `Tempest.Core.Runtime` vs. `Tempest.Core.Hosting`, applied here
because the underlying risk (a name a reader might assume they already
know) is the same in kind, even though the mechanism (human expectation,
not compiler ambiguity) differs from `ITempestServiceProvider`'s case.

## Consequences

**Positive:**

- No new packaging pattern is introduced — a contributor who already
  understands "one namespace per capability" from the existing six
  services immediately understands where to find, and where to add to,
  every one of these five.
- `Tempest.Core.Hosting`'s meaning (ADR-0016: environment/deployment
  adapters) stays exactly as narrow and unconfused as it was — background
  services get their own, differently-named home.
- No rename churn for `IHostedService` before it has a single consumer;
  the naming concern is addressed with a documentation note costing
  nothing to maintain, rather than a name change costing nothing to
  justify.

**Negative:**

- Five new namespaces now exist for capabilities with no implementation
  yet — each is, for now, a single interface file. This is a deliberate,
  accepted shape: the namespace exists because the *contract* is stable
  enough to define (per `WP 4.0`'s own governing philosophy), not because
  there is yet much to put in it.
- A future contributor still needs to actively check XML documentation to
  learn `IHostedService` is unrelated to the ASP.NET Core concept of the
  same name — a documentation-only mitigation carries residual risk that a
  rename would not.

## Future Considerations

If `IHostedService`'s naming genuinely causes recurring confusion once
`WP 4.5` implements it and it has real usage, revisit the rename option
then, with actual evidence of confusion rather than a hypothetical
concern — do not treat this ADR's decision as permanent if that evidence
appears.
