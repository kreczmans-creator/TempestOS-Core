using System.Text.Json;
using Tempest.App.Workspace.Documents;
using Tempest.Companion.Contracts;
using Tempest.Core.Api;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Identity;
using Tempest.Core.Runtime;

namespace Tempest.App.Composition;

/// <summary>
/// Registers the TempestOS Companion API (`WP 14.0A`, <c>ADR-0113</c>) on
/// the REST API's late-bound query-and-action surface (<c>ADR-0114</c>) —
/// read-only JSON projections of the Engineering Cockpit and Engineering
/// Domain, plus one body-binding action dispatched through the existing
/// Command Framework. Called from
/// <see cref="EngineeringWorkspaceComposer.RegisterEngineeringDisciplines"/>,
/// so every presentation layer that composes the Engineering Workspace
/// (console <c>WorkspaceShell</c>, <c>Tempest.Desktop</c>) serves the
/// identical Companion API without either knowing it exists — the same
/// composition-root-owns-registration rule <c>ADR-0071</c> already
/// establishes for Workspace extensibility.
/// </summary>
/// <remarks>
/// <para>
/// Every query requires <see cref="CompanionPermissions.Read"/>; the
/// action requires <see cref="CompanionPermissions.Act"/> — enforced by
/// the platform's existing <see cref="IPermissionEvaluator"/> against the
/// existing configuration-driven identity model (<c>ADR-0043</c>,
/// <c>ADR-0052</c>). An unconfigured deployment therefore serves
/// <c>403</c> for every Companion route — fail-closed by construction,
/// with transport exposure unchanged (loopback-only, <c>TD-13</c>/
/// <c>TD-14</c>).
/// </para>
/// <para>
/// Mutations never bypass the Command Framework: the one action below
/// binds its body to the existing <see cref="SetDocumentStatusCommand"/>
/// and dispatches it through <see cref="ICommandDispatcher"/> — the
/// identical handler the desktop Ribbon's own status transitions already
/// run (<c>ADR-0063</c>).
/// </para>
/// </remarks>
public static class CompanionApiRegistration
{
    /// <summary>
    /// Registers every Companion query and action route. Must be called
    /// only after the Workspace has started (<c>WorkspaceManager.Current</c>
    /// non-null) — the identical precondition
    /// <see cref="EngineeringWorkspaceComposer.RegisterEngineeringDisciplines"/>
    /// already carries.
    /// </summary>
    /// <param name="manager">The started Workspace's own manager.</param>
    /// <param name="host">The running Host.</param>
    /// <exception cref="InvalidOperationException">The Workspace has not started, or the Host's own services are not resolvable.</exception>
    public static void Register(Tempest.App.Workspace.WorkspaceManager manager, ITempestHost host)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(host);

        var services = host.Services ?? throw new InvalidOperationException("The Host must be running (ITempestHost.Services resolvable) before the Companion API can be registered.");

        if (manager.Current is not Tempest.App.Workspace.Workspace workspace)
            throw new InvalidOperationException("The Workspace must have started (WorkspaceManager.Current non-null) before the Companion API can be registered.");

        var queryRegistry = (IApiQueryRegistry)services.GetService(typeof(IApiQueryRegistry));
        var domainContext = (EngineeringDomainContext)services.GetService(typeof(EngineeringDomainContext));
        var commandDispatcher = (ICommandDispatcher)services.GetService(typeof(ICommandDispatcher));
        var eventBus = (IEventBus)services.GetService(typeof(IEventBus));

        var queryService = new CompanionQueryService(workspace, domainContext);

        var notificationBuffer = new CompanionNotificationBuffer();
        eventBus.Subscribe(notificationBuffer);

        var read = new Permission(CompanionPermissions.Read);
        var act = new Permission(CompanionPermissions.Act);

        queryRegistry.MapQuery(CompanionApiRoutes.Cockpit, read,
            _ => Task.FromResult(JsonSerializer.Serialize(queryService.BuildCockpitSummary(), CompanionJson.Options)));

        queryRegistry.MapQuery(CompanionApiRoutes.Projects, read,
            async cancellationToken => JsonSerializer.Serialize(await queryService.BuildProjectListAsync(cancellationToken).ConfigureAwait(false), CompanionJson.Options));

        queryRegistry.MapQuery(CompanionApiRoutes.Attention, read,
            async cancellationToken => JsonSerializer.Serialize(await queryService.BuildAttentionAsync(cancellationToken).ConfigureAwait(false), CompanionJson.Options));

        queryRegistry.MapQuery(CompanionApiRoutes.Activity, read,
            _ => Task.FromResult(JsonSerializer.Serialize(queryService.BuildActivity(), CompanionJson.Options)));

        queryRegistry.MapQuery(CompanionApiRoutes.Notifications, read,
            _ => Task.FromResult(JsonSerializer.Serialize(notificationBuffer.BuildSnapshot(), CompanionJson.Options)));

        queryRegistry.MapAction(CompanionApiRoutes.SetDocumentStatus, act,
            (requestBody, cancellationToken) => commandDispatcher.DispatchAsync(BindSetDocumentStatus(requestBody), cancellationToken));
    }

    /// <summary>
    /// Binds the set-document-status request body to the existing
    /// <see cref="SetDocumentStatusCommand"/> — validation failures throw
    /// <see cref="ApiRequestBindingException"/>, which the API pipeline
    /// maps to <c>400 Bad Request</c>, never <c>500</c>.
    /// </summary>
    internal static SetDocumentStatusCommand BindSetDocumentStatus(string? requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
            throw new ApiRequestBindingException("a JSON request body { targetObjectId, targetKind, status } is required.");

        var request = JsonSerializer.Deserialize<SetObjectStatusRequest>(requestBody, CompanionJson.Options)
            ?? throw new ApiRequestBindingException("the request body deserialized to nothing.");

        if (request.TargetObjectId == Guid.Empty)
            throw new ApiRequestBindingException("targetObjectId must be a non-empty GUID.");

        if (!CompanionQueryService.DocumentKinds.Contains(request.TargetKind, StringComparer.Ordinal))
            throw new ApiRequestBindingException($"targetKind must be one of: {string.Join(", ", CompanionQueryService.DocumentKinds)}.");

        if (!Enum.TryParse<LifecycleState>(request.Status, ignoreCase: true, out var status))
            throw new ApiRequestBindingException($"status '{request.Status}' is not a LifecycleState.");

        return new SetDocumentStatusCommand(request.TargetObjectId, request.TargetKind, status);
    }
}
