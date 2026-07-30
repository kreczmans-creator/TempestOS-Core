using Tempest.Core.Identity;

namespace Tempest.Core.Api;

/// <summary>Describes one registered REST route. Immutable.</summary>
/// <param name="Method">The HTTP method (e.g. <c>"GET"</c>, <c>"POST"</c>).</param>
/// <param name="Path">The route path (e.g. <c>"/api/v1/sample-report"</c>).</param>
/// <param name="CommandId">The registered <see cref="Commands.CommandDescriptor.Id"/> this route invokes.</param>
/// <param name="RequiredPermission">The permission a caller must hold before this route dispatches.</param>
public sealed record ApiRouteDescriptor(string Method, string Path, string CommandId, Permission RequiredPermission);
