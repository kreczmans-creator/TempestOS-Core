using System.Text.Json.Serialization;

namespace Tempest.Core.Invoicing.OAuth;

/// <summary>
/// The RFC 6749 §5.1 token response shape both providers answer an
/// authorisation-code exchange or a refresh with — the wire shape only;
/// <see cref="OAuthAuthoriser"/> alone decides what to do with it.
/// </summary>
internal sealed record OAuthTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("token_type")] string? TokenType);
