using Tempest.Core.Api;
using Tempest.Core.Identity;
using Tempest.Core.Modules;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that demonstrates the REST
/// API: it maps one HTTP route to an already-registered command,
/// containing no business logic of its own whatsoever — the purest
/// possible proof of "the REST API shall remain a thin transport layer...
/// no business logic shall exist inside controllers/endpoints."
/// </summary>
/// <remarks>
/// <para>
/// The living reference module `WP 6.3` validates the REST API against —
/// mirrors <see cref="ReportingSampleModule"/>'s own role for Reporting.
/// Carries <see cref="ModuleMetadataAttribute"/> so Discovery can read its
/// identity without instantiating it (ADR-0027), freeing its constructor
/// to request <see cref="IApiEndpointRegistry"/> — the one DI-public
/// platform service this module needs — via ordinary constructor
/// injection.
/// </para>
/// <para>
/// <b>Deliberately depends on <see cref="ReportingSampleModule"/> having
/// also registered its own command</b> — a disclosed departure from
/// every prior sample module's own "independently usable" convention
/// (<see cref="AuditSampleModule"/>, <see cref="NotificationSampleModule"/>,
/// and so on, each usable alone). This is deliberate, not an oversight:
/// the REST API's own domain purpose is to expose *already-registered*
/// platform capability over HTTP, never to define its own — mapping the
/// route to <see cref="ReportingSampleModule.GenerateSampleReportCommandId"/>
/// (a command that already exercises Identity, Settings, Audit, and
/// Notifications together, see that module's own remarks) is the purest
/// possible demonstration that the REST layer itself contains zero
/// business logic: this entire module is one <see cref="IApiEndpointRegistry.MapCommand"/>
/// call. Both modules must be present together for
/// <see cref="GenerateReportRoutePath"/> to actually work — see this
/// Work Package's own Platform Integration Demonstration.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.api", "API Sample", "1.0.0")]
public sealed class ApiSampleModule : ModuleLifecycleBase
{
    /// <summary>The HTTP method <see cref="GenerateReportRoutePath"/> is mapped under.</summary>
    public const string GenerateReportRouteMethod = "POST";

    /// <summary>The route path mapped to <see cref="ReportingSampleModule.GenerateSampleReportCommandId"/>.</summary>
    public const string GenerateReportRoutePath = "/api/v1/sample-report";

    private readonly IApiEndpointRegistry _endpointRegistry;

    /// <summary>
    /// Initialises a new instance of the <see cref="ApiSampleModule"/> class.
    /// </summary>
    /// <param name="endpointRegistry">
    /// The REST API service this module maps its route through, resolved
    /// via ordinary constructor injection.
    /// </param>
    public ApiSampleModule(IApiEndpointRegistry endpointRegistry)
        : base("tempest.samples.api", "API Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(endpointRegistry);

        _endpointRegistry = endpointRegistry;
    }

    /// <summary>
    /// Gets a value indicating whether <see cref="InitialiseAsync"/> has
    /// mapped this module's own route.
    /// </summary>
    public bool HasRegistered { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// Maps <see cref="GenerateReportRouteMethod"/> +
    /// <see cref="GenerateReportRoutePath"/> to
    /// <see cref="ReportingSampleModule.GenerateSampleReportCommandId"/>,
    /// requiring <see cref="ReportingSampleModule.GenerateReportPermissionKey"/>
    /// — no other logic of any kind.
    /// </remarks>
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _endpointRegistry.MapCommand(
            GenerateReportRouteMethod,
            GenerateReportRoutePath,
            ReportingSampleModule.GenerateSampleReportCommandId,
            new Permission(ReportingSampleModule.GenerateReportPermissionKey));

        HasRegistered = true;

        return Task.CompletedTask;
    }
}
