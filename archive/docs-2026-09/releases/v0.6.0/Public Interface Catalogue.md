# TempestOS v0.6.0 — Public Interface Catalogue

## Status

**Proposed interface signatures only. No implementation.** Every type
below is a design artifact for review, mirroring the shape of an actual
`Tempest.Core` public contract (matching this codebase's own XML-doc and
naming conventions exactly) without being compiled, tested, or committed
as source. Each interface is justified against an existing platform
convention it deliberately mirrors, rather than inventing a new shape —
see the note under each service. No interface below is final; each is
subject to its own owning Work Package's dedicated architecture phase,
where one is warranted (see `WorkPackages.md`).

## How These Signatures Were Derived

Every signature below follows a rule already established somewhere in
this platform, rather than introducing a new pattern:

- **Imperative registration, never open-generic/keyed DI resolution**
  (`RD-0040`) — `IReportingService`/`INotificationDispatcher` register
  their per-type collaborators (`IReportRenderer`, `INotificationHandler<T>`)
  imperatively, mirroring `ICommandDispatcher.RegisterHandler<TCommand>`.
- **A marker interface for the "fact" type, a generic handler interface
  for the consumer side** — `INotification`/`INotificationHandler<T>`
  mirrors `IEvent`/`IEventHandler<T>` exactly.
- **A result type that lets a caller know whether an operation
  succeeded without relying solely on exceptions for the common case** —
  `AuditRecordResult`, `LicenseValidationResult` mirror `CommandResult`.
- **`Try`-style, non-throwing lookups paired with a throwing primary
  method where "not found" is exceptional** — mirrors
  `ICommandRegistry`'s own `InvokeAsync` (throws
  `CommandNotFoundException`) alongside `RegisterDescriptor`.

## `Tempest.Core.Persistence` *(proposed — established as part of `WP 6.4`)*

```csharp
namespace Tempest.Core.Persistence;

/// <summary>
/// A minimal, internal, platform-owned durable store. Not a general-
/// purpose database abstraction — scoped narrowly to what platform
/// services (Settings, Audit) need to remember between process runs.
/// </summary>
public interface IPersistenceStore
{
    /// <summary>
    /// Reads the value stored under <paramref name="key"/> within
    /// <paramref name="collection"/>, or <see langword="null"/> if none
    /// exists.
    /// </summary>
    /// <param name="collection">A logical grouping, owned by exactly one calling service.</param>
    /// <param name="key">The item's key, unique within <paramref name="collection"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/> or <paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="PersistenceException">The underlying store could not be read.</exception>
    Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes <paramref name="value"/> under <paramref name="key"/>
    /// within <paramref name="collection"/>, creating or overwriting as
    /// needed.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="collection"/>, <paramref name="key"/>, or <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="PersistenceException">The underlying store could not be written.</exception>
    Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the item stored under <paramref name="key"/> within
    /// <paramref name="collection"/>, if any. Never throws for a
    /// missing key — deletion is idempotent.
    /// </summary>
    Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enumerates every key currently stored within
    /// <paramref name="collection"/>. Never <see langword="null"/>;
    /// empty if the collection has no entries.
    /// </summary>
    Task<IReadOnlyList<string>> ListKeysAsync(string collection, CancellationToken cancellationToken = default);
}

/// <summary>Base type for every exception this namespace raises.</summary>
public abstract class PersistenceException : Exception { /* ... */ }

/// <summary>The underlying store could not be read or written.</summary>
public sealed class PersistenceStoreUnavailableException : PersistenceException { /* ... */ }
```

## `Tempest.Core.Reporting` *(proposed — `WP 6.0`)*

```csharp
namespace Tempest.Core.Reporting;

/// <summary>
/// Marks a concrete report definition — identity and the shape of the
/// data it produces. Carries no rendering logic of its own, mirroring
/// how <c>ICommand</c> carries no handling logic.
/// </summary>
public interface IReportDefinition
{
    /// <summary>A stable, unique identifier for this report definition.</summary>
    string Id { get; }

    /// <summary>A human-readable display name.</summary>
    string Name { get; }
}

/// <summary>
/// Renders one <typeparamref name="TDefinition"/> into a
/// <see cref="ReportResult"/>. Exactly one renderer is registered per
/// definition type — mirroring <c>ICommandHandler&lt;TCommand&gt;</c>'s
/// own one-handler-per-command-type rule.
/// </summary>
public interface IReportRenderer<TDefinition> where TDefinition : IReportDefinition
{
    Task<ReportResult> RenderAsync(TDefinition definition, ReportRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Registers report definitions and their renderers, and dispatches a
/// render request by definition Id. Registration is imperative — this
/// service is never resolved via open-generic or keyed DI (<c>RD-0040</c>).
/// </summary>
public interface IReportingService
{
    /// <exception cref="DuplicateReportDefinitionException">A definition is already registered under <see cref="IReportDefinition.Id"/>.</exception>
    void RegisterDefinition<TDefinition>(TDefinition definition, IReportRenderer<TDefinition> renderer) where TDefinition : IReportDefinition;

    /// <exception cref="ReportDefinitionNotFoundException">No definition is registered under <paramref name="definitionId"/>.</exception>
    Task<ReportResult> GenerateAsync(string definitionId, ReportRequest request, CancellationToken cancellationToken = default);

    /// <summary>Every registered definition's Id and Name. Never <see langword="null"/>.</summary>
    IReadOnlyList<IReportDefinition> RegisteredDefinitions { get; }
}

/// <summary>Parameters for a single report generation request. Immutable.</summary>
public sealed record ReportRequest(IReadOnlyDictionary<string, string> Parameters);

/// <summary>The rendered output of a report generation request. Immutable.</summary>
public sealed record ReportResult(string ContentType, byte[] Content);

public abstract class ReportingException : Exception { /* ... */ }
public sealed class DuplicateReportDefinitionException : ReportingException { /* ... */ }
public sealed class ReportDefinitionNotFoundException : ReportingException { /* ... */ }
```

## `Tempest.Core.Identity` *(proposed — `WP 6.1`)*

```csharp
namespace Tempest.Core.Identity;

/// <summary>Identifies a single actor — a user or a system principal.</summary>
public interface IIdentity
{
    /// <summary>A stable, unique identifier for this identity.</summary>
    string Id { get; }

    /// <summary>A human-readable display name.</summary>
    string DisplayName { get; }
}

/// <summary>
/// The acting party for a given operation — an <see cref="IIdentity"/>
/// plus the set of permissions currently granted to it.
/// </summary>
public interface IPrincipal
{
    IIdentity Identity { get; }
    IReadOnlyList<Permission> Permissions { get; }
}

/// <summary>
/// Resolves the <see cref="IPrincipal"/> performing the current
/// operation. DI-public, consumed exactly like <c>ILogger</c> or
/// <c>IEventBus</c> by any service or module needing to know who is
/// acting.
/// </summary>
public interface ICurrentPrincipalAccessor
{
    /// <summary>
    /// The current principal, or <see langword="null"/> if no
    /// authenticated principal is available for the current operation.
    /// </summary>
    IPrincipal? Current { get; }
}

/// <summary>
/// Answers whether a given principal holds a given permission.
/// </summary>
public interface IPermissionEvaluator
{
    bool HasPermission(IPrincipal principal, Permission permission);

    /// <exception cref="PermissionDeniedException"><paramref name="principal"/> does not hold <paramref name="permission"/>.</exception>
    void RequirePermission(IPrincipal principal, Permission permission);
}

/// <summary>A single, named, granular permission. Immutable.</summary>
public sealed record Permission(string Key);

public abstract class IdentityException : Exception { /* ... */ }
public sealed class PermissionDeniedException : IdentityException { /* ... */ }
```

## `Tempest.Core.Notifications` *(proposed — `WP 6.2`)*

```csharp
namespace Tempest.Core.Notifications;

/// <summary>
/// Marks a concrete notification type — a user- or system-facing
/// notice, typically derived from (or raised alongside) an
/// <c>IEvent</c>. Mirrors <c>IEvent</c>'s own marker-only shape.
/// </summary>
public interface INotification
{
    /// <summary>When this notification was raised.</summary>
    DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// Handles one <typeparamref name="TNotification"/>. Subscribed
/// imperatively at runtime — never resolved generically through the
/// container (<c>RD-0040</c>) — mirroring <c>IEventHandler&lt;T&gt;</c>.
/// </summary>
public interface INotificationHandler<TNotification> where TNotification : INotification
{
    Task HandleAsync(TNotification notification, CancellationToken cancellationToken = default);
}

/// <summary>
/// Dispatches a notification to every subscribed handler. Failure
/// isolation mirrors <c>IEventBus</c>'s own unconditional per-subscriber
/// isolation (<c>ADR-0028</c>).
/// </summary>
public interface INotificationDispatcher
{
    void Subscribe<TNotification>(INotificationHandler<TNotification> handler) where TNotification : INotification;
    void Unsubscribe<TNotification>(INotificationHandler<TNotification> handler) where TNotification : INotification;
    Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification;
}

public abstract class NotificationException : Exception { /* ... */ }
```

## `Tempest.Core.Api` *(proposed — `WP 6.3`)*

```csharp
namespace Tempest.Core.Api;

/// <summary>
/// Registers a route so it becomes reachable over the REST API's
/// hosted HTTP surface. Route handling itself dispatches through the
/// existing, unmodified <c>ICommandRegistry.InvokeAsync</c> — this
/// interface only describes route-to-command mapping, never a second,
/// competing invocation mechanism.
/// </summary>
public interface IApiEndpointRegistry
{
    /// <exception cref="DuplicateApiRouteException">A route is already registered for <paramref name="method"/> + <paramref name="path"/>.</exception>
    void MapCommand(string method, string path, string commandId, Permission requiredPermission);

    /// <summary>Every currently registered route. Never <see langword="null"/>.</summary>
    IReadOnlyList<ApiRouteDescriptor> Routes { get; }
}

/// <summary>Describes one registered REST route. Immutable.</summary>
public sealed record ApiRouteDescriptor(string Method, string Path, string CommandId, Permission RequiredPermission);

public abstract class ApiException : Exception { /* ... */ }
public sealed class DuplicateApiRouteException : ApiException { /* ... */ }
```

*The hosted-service scaffold type itself (implementing `IHostedService`,
per `ADR-0047`) is deliberately not drafted here — its shape depends on
the ASP.NET Core/Kestrel adoption decision (`ADR-0049`), which this
review recommends but does not itself ratify.*

## `Tempest.Core.Settings` *(proposed — `WP 6.4`)*

```csharp
namespace Tempest.Core.Settings;

/// <summary>Describes one setting — identity, default value, and type. Immutable.</summary>
public interface ISettingDefinition
{
    string Key { get; }
    string DisplayName { get; }
    string DefaultValue { get; }
}

/// <summary>
/// Reads and writes runtime-mutable setting values — explicitly
/// distinct from <c>IConfigurationProvider</c>, which is read-only and
/// loaded once at startup (Case Study 05).
/// </summary>
public interface ISettingsProvider
{
    /// <exception cref="DuplicateSettingDefinitionException">A definition is already registered under <see cref="ISettingDefinition.Key"/>.</exception>
    void RegisterDefinition(ISettingDefinition definition);

    /// <exception cref="SettingNotFoundException">No definition is registered under <paramref name="key"/>.</exception>
    Task<string> GetValueAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the value for <paramref name="key"/> and publishes an
    /// <see cref="ISettingsChangedEvent"/> through the existing Event
    /// Bus.
    /// </summary>
    /// <exception cref="SettingNotFoundException">No definition is registered under <paramref name="key"/>.</exception>
    Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default);
}

/// <summary>
/// Published through the existing <c>IEventBus</c> whenever a setting
/// value changes — reuses the Event Bus contract rather than inventing
/// a new notification path.
/// </summary>
public interface ISettingsChangedEvent : IEvent
{
    string Key { get; }
    string OldValue { get; }
    string NewValue { get; }
}

public abstract class SettingsException : Exception { /* ... */ }
public sealed class DuplicateSettingDefinitionException : SettingsException { /* ... */ }
public sealed class SettingNotFoundException : SettingsException { /* ... */ }
```

## `Tempest.Core.Audit` *(proposed — `WP 6.5`)*

```csharp
namespace Tempest.Core.Audit;

/// <summary>A single, durable, immutable record of an action taken by a principal.</summary>
public interface IAuditRecord
{
    string ActorId { get; }
    string Action { get; }
    DateTimeOffset OccurredAt { get; }
    IReadOnlyDictionary<string, string> Detail { get; }
}

/// <summary>Records an audit entry. Never throws for the caller's own action failing — an audit record may describe a failed action.</summary>
public interface IAuditRecorder
{
    Task RecordAsync(string action, IReadOnlyDictionary<string, string>? detail = null, CancellationToken cancellationToken = default);
}

/// <summary>Queries previously recorded audit entries. Read-only.</summary>
public interface IAuditQuery
{
    Task<IReadOnlyList<IAuditRecord>> QueryAsync(AuditQueryCriteria criteria, CancellationToken cancellationToken = default);
}

/// <summary>Filter criteria for an audit query. Immutable; every property optional.</summary>
public sealed record AuditQueryCriteria(string? ActorId = null, string? Action = null, DateTimeOffset? From = null, DateTimeOffset? To = null);

public abstract class AuditException : Exception { /* ... */ }
```

## `Tempest.Core.Licensing` *(proposed — `WP 6.6`)*

```csharp
namespace Tempest.Core.Licensing;

/// <summary>A single, validated license. Immutable.</summary>
public interface ILicense
{
    string LicenseeName { get; }
    DateTimeOffset? ExpiresAt { get; }
    IReadOnlyList<string> EnabledCapabilities { get; }
}

/// <summary>
/// Validates a license at Host startup, before the DI container exists —
/// mirroring Configuration's own pre-container construction. Invalid
/// results are Host-fatal (<c>ADR-0013</c>), not isolated.
/// </summary>
public interface ILicenseValidator
{
    LicenseValidationResult Validate();
}

/// <summary>The outcome of license validation. Mirrors <c>CommandResult</c>'s own success/failure shape.</summary>
public sealed record LicenseValidationResult(bool IsValid, ILicense? License, string? FailureReason);

/// <summary>
/// The read-only, post-validation view of the current license — DI-
/// public, registered via <c>AddInstance</c> once validation succeeds.
/// </summary>
public interface ILicenseProvider
{
    ILicense CurrentLicense { get; }
    bool HasCapability(string capability);
}

public abstract class LicensingException : Exception { /* ... */ }
public sealed class LicenseValidationException : LicensingException { /* ... */ }
```

## `Tempest.Core.ExportImport` *(proposed — `WP 6.7`)*

```csharp
namespace Tempest.Core.ExportImport;

/// <summary>
/// Marks a service's data as exportable through a versioned, round-
/// trip-safe contract — explicitly distinct from
/// <c>IPersistenceStore</c>, which is internal, platform-owned state.
/// </summary>
public interface IExportable
{
    /// <summary>The schema version this exporter writes. Read on import to detect an incompatible or older artifact.</summary>
    int SchemaVersion { get; }

    Task ExportAsync(Stream destination, CancellationToken cancellationToken = default);
}

/// <summary>Coordinates exporting one or more <see cref="IExportable"/> sources into a single portable artifact.</summary>
public interface IExportService
{
    Task ExportAsync(Stream destination, IReadOnlyList<IExportable> sources, CancellationToken cancellationToken = default);
}

/// <summary>Reads a previously exported artifact back into the owning service(s).</summary>
public interface IImportService
{
    /// <exception cref="IncompatibleExportSchemaException">The artifact's schema version is not supported by the current platform version.</exception>
    Task ImportAsync(Stream source, CancellationToken cancellationToken = default);
}

public abstract class ExportImportException : Exception { /* ... */ }
public sealed class IncompatibleExportSchemaException : ExportImportException { /* ... */ }
```

## Related Documents

`Release Architecture.md`; `Platform Services Overview.md`; `Platform
Service Dependency Diagram.md`; `Service Lifecycle.md`; `Required
ADRs.md`; `RD-0040` (open-generic/keyed registration rejected);
`ADR-0028` (Event Bus dispatch/failure model, mirrored by
Notifications); `ADR-0037`/`ADR-0038` (Command Framework registration/
dispatch failure model, mirrored by Reporting).
