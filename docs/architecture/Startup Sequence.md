# Startup Sequence

**Status: architecture only. No production code exists yet.**

## Relationship to *The Startup Sequence* (Academy)

`docs/academy/02 Runtime Architecture/02-the-startup-sequence.md` already
documents the Configuration → Logging → DI portion of this sequence in
narrative form, with its own reasoning (why "freeze" is conceptual, why
logging slots in where it does). This document is that sequence's complete,
diagrammatic superset — it starts from the same steps, then continues through
Discovery, Registration, Module Initialisation, Running, and every failure
path, which the Academy document deliberately left collapsed into a single
"Runtime starts" step. The two documents must agree with each other; this one
extends the Academy one, and does not contradict it — see ADR-0011 for the one
place a literal reading of an illustrative phase order needed correcting.

## Sequence Diagram

```mermaid
sequenceDiagram
    participant Host
    participant ConfigBuilder as ConfigurationBuilder
    participant Config as IConfigurationProvider
    participant LogFactory as LoggerFactory
    participant Discovery as IFrameworkDiscoveryService
    participant Registry as RuntimeModuleManager
    participant Services as ServiceCollection
    participant Provider as TempestServiceProvider
    participant Lifecycle as ModuleLifecycleManager

    Note over Host: Host Created
    Host->>ConfigBuilder: new ConfigurationBuilder(logger?)
    Host->>ConfigBuilder: AddSource(...) x N
    Host->>ConfigBuilder: Build()
    alt Configuration valid
        ConfigBuilder-->>Host: IConfigurationProvider
        Note over Host: Configuration Built
    else Configuration invalid
        ConfigBuilder--xHost: ConfigurationException
        Note over Host: -> Faulted (see Runtime State Machine)
    end

    Host->>LogFactory: new LoggerFactory(config, sink)
    alt MinimumLevel missing or valid
        LogFactory-->>Host: ILoggerFactory
        Host->>LogFactory: CreateLogger("Runtime")
        LogFactory-->>Host: default ILogger
        Note over Host: Logging Built
    else MinimumLevel invalid
        LogFactory--xHost: ConfigurationException
        Note over Host: -> Faulted
    end

    Host->>Discovery: new ReflectionFrameworkDiscoveryService(logger)
    Host->>Discovery: DiscoverModules()
    alt Discovery succeeds
        Discovery-->>Host: ModuleDescriptor[] (ascending by Id)
        Note over Host: Module Discovery
    else Discovery fails
        Discovery--xHost: ModuleDiscoveryException / DuplicateModuleIdException
        Note over Host: -> Faulted
    end

    Host->>Registry: new RuntimeModuleManager(logger)
    loop for each descriptor
        Host->>Registry: Register(descriptor)
        alt Registration succeeds
            Registry-->>Host: RuntimeModule
        else Registration fails
            Registry--xHost: DuplicateModuleRegistrationException
            Note over Host: -> Faulted
        end
    end
    Note over Host: Module Registration

    Host->>Services: new ServiceCollection(logger)
    Host->>Services: AddInstance(config), AddInstance(sink/factory/logger)
    Host->>Services: AddDiscoveredModules(registry.GetAll())
    Note over Host: Platform Services Registered

    Host->>Provider: new TempestServiceProvider(services, logger)
    Provider-->>Host: ITempestServiceProvider
    Note over Host: Dependency Injection Built

    Host->>Lifecycle: new ModuleLifecycleManager(registry, provider, logger)
    Host->>Lifecycle: InitialiseAllAsync(startupToken)
    Host->>Lifecycle: StartAllAsync(startupToken)
    Note over Lifecycle: Individual module failures are isolated (WP 2.3) -<br/>marked Failed, logged, batch continues. Does not fault the Host.
    alt startupToken cancelled mid-phase
        Lifecycle--xHost: OperationCanceledException
        Note over Host: -> attempt teardown of what exists -> Stopped
    else Initialisation/Start phases complete (regardless of individual module outcomes)
        Lifecycle-->>Host: (returns normally)
        Note over Host: Module Initialisation complete
        Note over Host: -> Running
    end
```

## Notes on the Diagram

- Every `--x` arrow (an exception crossing back to the Host) is Host-fatal per
  ADR-0013, **except** individual module failures inside
  `InitialiseAllAsync`/`StartAllAsync`, which `ModuleLifecycleManager` already
  isolates internally and which never reach the Host as an exception at all —
  the Host only ever observes those via `IModuleLifecycleManager.GetState`/`Modules`
  afterward, not as a thrown exception during this sequence.
- The startup `CancellationToken` (ADR-0014) is threaded through every
  `async` call in this diagram, not only the lifecycle calls shown — omitted
  from the other steps above for readability, since none of Configuration,
  Logging, Discovery, or Registration currently perform genuinely
  cancellable work.
- `AddDiscoveredModules` and the `AddInstance` calls for configuration/logging
  all happen against the *same* `ServiceCollection`, before it is ever handed
  to `TempestServiceProvider`'s constructor — see ADR-0011 for why this
  ordering is load-bearing, not incidental.

## Failure Paths Summary

| Phase | Exception(s) | Outcome |
|---|---|---|
| Configuration Built | `ConfigurationException` | Host-fatal → `Faulted` |
| Logging Built | `ConfigurationException` | Host-fatal → `Faulted` |
| Module Discovery | `ModuleDiscoveryException`, `DuplicateModuleIdException` | Host-fatal → `Faulted` |
| Module Registration | `DuplicateModuleRegistrationException` | Host-fatal → `Faulted` |
| Platform Services Registered | `ArgumentException` (malformed registration) | Host-fatal → `Faulted` |
| Dependency Injection Built | (not expected to throw under normal conditions) | — |
| Module Initialisation — individual module | (isolated internally by `ModuleLifecycleManager`) | Module `Failed`; Host continues to `Running` |
| Module Initialisation — startup cancelled | `OperationCanceledException` | Host attempts teardown → `Stopped` (not `Faulted`) |

See *Failure Behaviour.md* for the full failure model, including shutdown-time
and logging-time failures this diagram does not cover.
