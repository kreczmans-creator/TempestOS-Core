using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tempest.Core.Api;

/// <summary>
/// Generates a minimal OpenAPI 3.0 document reflecting every currently
/// registered <see cref="ApiRouteDescriptor"/> — a machine-readable
/// description of the REST API's own route surface, satisfying this Work
/// Package's own "OpenAPI generation" and "expose stable contracts"
/// objectives without a third-party Swagger/OpenAPI NuGet dependency.
/// </summary>
/// <remarks>
/// Deliberately minimal: each route is described only by its method,
/// path, and a generic 200/400/401/403/404/500 response shape — matching
/// <see cref="ApiRequestHandler"/>'s own actual behaviour exactly, not a
/// richer schema this release has no concrete need for. Uses
/// <see cref="System.Text.Json"/>, already used elsewhere in this
/// codebase (<c>PluginManifestDiscoveryService</c>, <c>AuditRecorder</c>)
/// — no new dependency introduced.
/// </remarks>
public static class OpenApiDocumentGenerator
{
    /// <summary>
    /// Generates the OpenAPI document, as a JSON string, for
    /// <paramref name="routes"/> plus, optionally, every late-bound
    /// query/action route (<c>ADR-0114</c>) in
    /// <paramref name="queryRoutes"/>.
    /// </summary>
    /// <param name="routes">Every command route to describe.</param>
    /// <param name="queryRoutes">Every late-bound query/action route to describe, or <see langword="null"/> for none.</param>
    public static string Generate(IReadOnlyList<ApiRouteDescriptor> routes, IReadOnlyList<ApiQueryRouteDescriptor>? queryRoutes = null)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var paths = new JsonObject();

        foreach (var route in routes)
        {
            AddOperation(paths, route.Method, route.Path, $"Invokes command '{route.CommandId}'.", isJsonResponse: false);
        }

        foreach (var route in queryRoutes ?? [])
        {
            AddOperation(
                paths,
                route.Method,
                route.Path,
                route.Query is null ? "Binds the request body to a typed command and dispatches it." : "Serves a read-only JSON projection.",
                isJsonResponse: route.Query is not null);
        }

        var document = new JsonObject
        {
            ["openapi"] = "3.0.3",
            ["info"] = new JsonObject
            {
                ["title"] = "TempestOS REST API",
                ["version"] = "v1",
            },
            ["paths"] = paths,
        };

        return document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static void AddOperation(JsonObject paths, string method, string path, string summary, bool isJsonResponse)
    {
        var pathItem = paths.TryGetPropertyValue(path, out var existing) && existing is JsonObject existingObject
            ? existingObject
            : new JsonObject();

        pathItem[method.ToLowerInvariant()] = new JsonObject
        {
            ["summary"] = summary,
            ["responses"] = new JsonObject
            {
                ["200"] = new JsonObject { ["description"] = isJsonResponse ? "The query succeeded; the body is JSON." : "The command succeeded." },
                ["400"] = new JsonObject { ["description"] = "The command reported a foreseeable failure, or the request body could not be bound." },
                ["401"] = new JsonObject { ["description"] = "No identity was supplied." },
                ["403"] = new JsonObject { ["description"] = "The caller does not hold the required permission." },
                ["404"] = new JsonObject { ["description"] = "No route or command matches." },
                ["500"] = new JsonObject { ["description"] = "An unhandled error occurred." },
            },
        };

        paths[path] = pathItem;
    }
}
