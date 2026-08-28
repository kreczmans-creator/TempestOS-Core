namespace Tempest.Companion.Contracts;

/// <summary>
/// Every route path the TempestOS Companion API serves (`WP 14.0A`,
/// <c>ADR-0114</c>) — declared once, here, and consumed verbatim by both
/// the server-side registration (<c>Tempest.App</c>'s
/// <c>CompanionApiRegistration</c>) and the Companion client
/// (<c>Tempest.Companion</c>'s <c>CompanionApiClient</c>), so a path can
/// never drift between the two sides of the HTTP boundary.
/// </summary>
public static class CompanionApiRoutes
{
    /// <summary>The Companion API's own route prefix, under the REST API's existing <c>/api/v1</c> version root.</summary>
    public const string Base = "/api/v1/companion";

    /// <summary>GET — the complete Cockpit summary (<see cref="CockpitSummaryDto"/>): the mobile expression of the Engineering Cockpit (`ADR-0069`).</summary>
    public const string Cockpit = Base + "/cockpit";

    /// <summary>GET — every live Project (<see cref="ProjectListDto"/>).</summary>
    public const string Projects = Base + "/projects";

    /// <summary>GET — everything currently requiring attention (<see cref="AttentionDto"/>): attention items, blocked items, open decisions, reviews awaiting a decision.</summary>
    public const string Attention = Base + "/attention";

    /// <summary>GET — recent meaningful Workspace activity (<see cref="ActivityDto"/>).</summary>
    public const string Activity = Base + "/activity";

    /// <summary>GET — recent platform notifications (<see cref="NotificationListDto"/>), derived from the Event Bus per <c>ADR-0046</c>.</summary>
    public const string Notifications = Base + "/notifications";

    /// <summary>POST — transitions one Document-family object's lifecycle status (<see cref="SetObjectStatusRequest"/>), dispatched through the existing <c>SetDocumentStatusCommand</c>.</summary>
    public const string SetDocumentStatus = Base + "/actions/set-document-status";
}

/// <summary>
/// The permission keys the Companion API enforces per route — flat,
/// exact-match keys evaluated by the platform's existing
/// <c>IPermissionEvaluator</c> (<c>ADR-0044</c>), configured through the
/// existing <c>Identity:Roles:*</c>/<c>Identity:Principals:*</c>
/// configuration model (<c>ADR-0043</c>). The Companion introduces no
/// parallel identity system.
/// </summary>
public static class CompanionPermissions
{
    /// <summary>Required by every read-only Companion query route.</summary>
    public const string Read = "companion.read";

    /// <summary>Required by every Companion action route — deliberately distinct from <see cref="Read"/>, so an awareness-only principal can be configured with no ability to act.</summary>
    public const string Act = "companion.act";
}
