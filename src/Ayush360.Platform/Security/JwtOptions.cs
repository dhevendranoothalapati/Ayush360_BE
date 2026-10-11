namespace Ayush360.Platform.Security;

/// <summary>
/// "Jwt" configuration section.
///
/// Access tokens are signed with RS256 (RSA, asymmetric):
///   - The Identity API holds the PRIVATE key and is the only service that can create tokens.
///   - Every other API (HMS, later Billing, Pharmacy, ...) only has the PUBLIC key, which it downloads from
///     Identity's /.well-known/jwks.json. A module API can check a token but can never mint one.
///
/// Issuer / Audience / lifetimes live in appsettings.json.
/// SigningKeyPem is a secret: Key Vault on Azure; generated automatically on your PC in Development. Never commit it.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 30;

    /// <summary>
    /// Longest a sign-in can last (one shift). While the user works, the app quietly renews the short access token,
    /// but never past this many hours after the password was typed; then they must sign in again.
    /// Renewing does NOT extend it.
    /// </summary>
    public int SessionHours { get; set; } = 12;

    // ---- APIs that only VALIDATE tokens (HMS and future module APIs) ----

    /// <summary>
    /// Where to download Identity's public keys, e.g. http://localhost:5077/.well-known/jwks.json in development,
    /// or the Identity API's internal https address on Azure (env var Jwt__JwksUrl).
    /// Leave empty in the Identity API itself: it uses its own keys directly.
    /// </summary>
    public string JwksUrl { get; set; } = string.Empty;

    /// <summary>Must be true outside development so keys are only ever fetched over https.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    // ---- Identity API only ----

    /// <summary>
    /// The active RSA private key (PKCS#8 PEM, "-----BEGIN PRIVATE KEY-----", 2048 bits or more).
    /// Azure: Key Vault secret "Jwt--SigningKeyPem". Development: leave empty and a key is generated on first run.
    /// </summary>
    public string SigningKeyPem { get; set; } = string.Empty;

    /// <summary>
    /// Key rotation: PUBLIC keys of previous signing keys ("-----BEGIN PUBLIC KEY-----"). They are still published in
    /// jwks.json so tokens signed before the rotation keep working until they expire. Remove after AccessTokenMinutes.
    /// </summary>
    public string[] PreviousPublicKeysPem { get; set; } = [];
}
