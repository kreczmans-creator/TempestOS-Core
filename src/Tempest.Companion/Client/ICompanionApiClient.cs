using Tempest.Companion.Contracts;

namespace Tempest.Companion.Client;

/// <summary>
/// The Companion's one gateway to the TempestOS platform — the client
/// side of the REST boundary (<c>ADR-0114</c>). Every method either
/// returns the deserialized wire DTO or throws
/// <see cref="CompanionApiException"/>; nothing else escapes this
/// boundary, so every caller's error handling is typed. An interface so
/// view/service tests substitute a fake without a listener — production
/// always uses <see cref="CompanionApiClient"/> against the real API
/// (`WP 14.0A`'s own "no permanent production mocks" requirement).
/// </summary>
public interface ICompanionApiClient
{
    /// <summary>Fetches the Cockpit summary.</summary>
    Task<CockpitSummaryDto> GetCockpitAsync(CancellationToken cancellationToken = default);

    /// <summary>Fetches the live Project list.</summary>
    Task<ProjectListDto> GetProjectsAsync(CancellationToken cancellationToken = default);

    /// <summary>Fetches the triage summary.</summary>
    Task<AttentionDto> GetAttentionAsync(CancellationToken cancellationToken = default);

    /// <summary>Fetches recent Workspace activity.</summary>
    Task<ActivityDto> GetActivityAsync(CancellationToken cancellationToken = default);

    /// <summary>Fetches recent platform notifications.</summary>
    Task<NotificationListDto> GetNotificationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the set-document-status quick action — dispatched
    /// server-side through the existing Command Framework. Returns the
    /// command's own outcome; throws <see cref="CompanionApiException"/>
    /// only for boundary failures (unreachable/unauthorized/etc.), never
    /// for a command that ran and reported failure.
    /// </summary>
    Task<CompanionActionOutcome> SetDocumentStatusAsync(SetObjectStatusRequest request, CancellationToken cancellationToken = default);
}

/// <summary>A quick action's own outcome — the wire form of the dispatched command's <c>CommandResult</c>.</summary>
/// <param name="Succeeded">Whether the command succeeded.</param>
/// <param name="Message">The command's own outcome message, or <see langword="null"/>.</param>
public sealed record CompanionActionOutcome(bool Succeeded, string? Message);
