namespace Tempest.Core.Api;

/// <summary>
/// The outcome of handling one REST request — a status code and an
/// optional, plain-text body. Deliberately minimal, mirroring
/// <see cref="Commands.CommandResult"/>'s own "just enough to report the
/// outcome" shape.
/// </summary>
/// <param name="StatusCode">The HTTP status code to return.</param>
/// <param name="Body">The response body, or <see langword="null"/> for an empty body.</param>
public sealed record ApiResponse(int StatusCode, string? Body);
