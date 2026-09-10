using System.Security.Cryptography;
using System.Text;

namespace Tempest.Core.Invoicing.OAuth;

/// <summary>
/// PKCE (RFC 7636) code verifier/challenge generation for
/// <see cref="OAuthAuthoriser"/>'s own authorisation-code round trip —
/// <c>S256</c> only, the method every provider this platform speaks to
/// requires (`WP 19.1A` part 2).
/// </summary>
internal static class PkceGenerator
{
    /// <summary>Generates one code verifier (43 base64url characters, the RFC's own minimum-entropy shape) and its <c>S256</c> challenge.</summary>
    public static (string Verifier, string Challenge) Generate()
    {
        var verifier = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        return (verifier, challenge);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
