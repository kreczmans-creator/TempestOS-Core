# Building a Module

## What This Document Is

A practical guide for writing a TempestOS module, using the Module SDK
(`ModuleBase`, `ModuleLifecycleBase` — `Tempest.Core.Modules`, WP 4.1). If
you want to understand *why* the module pipeline is shaped the way it is,
read *The Module Pipeline* first; this document assumes that shape and
shows you how to build against it with the least ceremony possible.

## The Two Base Classes

**`ModuleBase`** — for a module with no lifecycle behaviour at all (it
just needs to exist and be discoverable):

```csharp
public sealed class ReportingModule : ModuleBase
{
    public ReportingModule()
        : base("tempest.reporting", "Reporting Module", "1.0.0")
    {
    }
}
```

That's the entire module. `Id`, `Name`, and `Version` are handled by the
base class; you never write the three property getters yourself.

**`ModuleLifecycleBase`** — for a module that does something during
initialisation, startup, shutdown, or disposal. Every lifecycle method is
`virtual` and defaults to a no-op — override only the ones you need:

```csharp
public sealed class TelemetryModule : ModuleLifecycleBase
{
    public TelemetryModule()
        : base("tempest.telemetry", "Telemetry Module", "1.0.0")
    {
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // begin collecting telemetry
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        // flush and stop
        return Task.CompletedTask;
    }

    // InitialiseAsync and DisposeAsync are not overridden — they inherit
    // the base class's no-op default. You do not write empty overrides
    // for phases you don't use.
}
```

## Why These Two Classes Exist

Before the SDK, every module repeated the same three property getters for
`Id`/`Name`/`Version`, and a module using `IModuleLifecycle` directly had
to write an explicit override for *every* lifecycle method, even the ones
it didn't care about — three trivial `=> Task.CompletedTask;` lines were
common. `ModuleBase` and `ModuleLifecycleBase` remove exactly that
repetition, and nothing else. There is no reflection, no attribute-driven
metadata, no source generator, and no hidden behaviour anywhere in either
class — reading the two source files is reading the entire mechanism.

## One Constraint You Still Need to Know About

Your module still needs its own **public parameterless constructor** —
that has not changed, and the SDK does not remove this requirement.
Discovery finds your module by scanning loaded assemblies and
instantiating each candidate type with zero arguments, purely to read its
metadata, before discarding that instance. This means:

- You cannot give your module a constructor that takes a service
  dependency (`ILogger`, `IConfigurationProvider`, or anything else) as
  its *only* constructor, because Discovery would fail to construct it.
- Whatever your module needs to do, it needs to do without a
  constructor-injected dependency, at least for now. If your module
  genuinely needs a platform service, that is a real, current limitation
  of the module pipeline — not something `ModuleBase`/`ModuleLifecycleBase`
  can work around, since the constraint comes from Discovery and
  `TempestServiceProvider`'s own construction rules, not from the SDK.

This is documented here because it is exactly the kind of thing a module
author discovers the hard way if it isn't written down.

**Update, WP 4.4B — implemented.** ADR-0027's design is now real:
`Tempest.Core.Modules.ModuleMetadataAttribute`, an optional, class-level
attribute, lets Discovery read your module's `Id`/`Name`/`Version` without
instantiating it at all. A module carrying it may declare any constructor
`TempestServiceProvider` can resolve — including one requiring a
DI-public platform service, such as `ILogger`:

```csharp
[ModuleMetadata("tempest.telemetry", "Telemetry Module", "1.0.0")]
public sealed class TelemetryModule : ModuleLifecycleBase
{
    private readonly ILogger _logger;

    public TelemetryModule(ILogger logger)
        : base("tempest.telemetry", "Telemetry Module", "1.0.0")
    {
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.Information("Telemetry module started.");
        return Task.CompletedTask;
    }
}
```

The attribute's values and the base constructor's literal values must
agree — Discovery reads the attribute alone and never cross-checks it
against the eventually-constructed instance, so keeping them in sync is
your own responsibility.

**Nothing above changes for a module without the attribute**: every
example earlier on this page — and every module already in this
codebase, including the sample module (`ClockModule`) — keeps exactly the
parameterless-constructor requirement described here, unchanged, forever.
The attribute is purely opt-in: add it only when your module genuinely
needs a constructor-injected service. See `Module Dependency Injection
Architecture.md` and ADR-0027 for the full design and reasoning.

## When Not to Use the SDK

If your module's identity or lifecycle needs something genuinely different
from what `ModuleBase`/`ModuleLifecycleBase` provide, implement `IModule`/
`IModuleLifecycle` directly — the SDK is a convenience, not a requirement.
Nothing in the runtime treats an SDK-built module any differently from a
hand-written one; Discovery, Registration, and Lifecycle cannot tell the
difference, and neither should you need to.

## A Real Example

`Tempest.Samples.ClockModule` (`src/Samples/Tempest.Samples/ClockModule.cs`,
`WP 4.3`) is a real, compiled, production module written exactly as this
guide describes — a public, zero-argument constructor, `ModuleLifecycleBase`,
and no constructor dependency of any kind. If this guide and the real code
ever disagree, trust the code and raise it — a passing test suite
(`ClockModuleTests`/`ClockModuleDiscoveryTests`/`ClockModulePipelineTests`)
is this guide's own, ongoing proof of accuracy, not merely an aspiration.

## Related Documents

*The Module Pipeline* (this folder) · WP 4.0 retrospective (*Platform
Contracts*) · WP 4.1 retrospective (*Module SDK*) · WP 4.3 retrospectives
(*Sample Module Architecture*, *Sample Module Implementation*) · WP 4.4A
retrospective (*Dependency Injection for Discovered Modules*) ·
`docs/architecture/Platform Service Map.md`'s Module SDK entry ·
`docs/architecture/Sample Module Architecture.md` ·
`docs/architecture/Module Dependency Injection Architecture.md` ·
ADR-0027.
