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
    [property: JsonPropertyName("token_type")] string? TokenType)
{
    /// <summary>
    /// Redacted deliberately (`WP 21.5F` Offensive Security Audit, OSA-04):
    /// no call site logs this record today, but a compiler-generated
    /// <c>record ToString()</c> would print both tokens verbatim if one
    /// ever did — <c>$"{tokenResponse}"</c> or a structured
    /// <c>logger.LogX("{Response}", tokenResponse)</c> call reads as
    /// entirely ordinary code and would not visibly be a mistake. Reduced
    /// to nothing about either token's own value.
    /// </summary>
    public override string ToString() =>
        $"OAuthTokenResponse {{ AccessToken = <redacted>, RefreshToken = {(RefreshToken is null ? "<none>" : "<redacted>")}, ExpiresIn = {ExpiresIn}, TokenType = {TokenType} }}";
}
