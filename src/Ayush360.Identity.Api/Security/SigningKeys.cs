using System.Security.Cryptography;
using System.Text;
using Ayush360.Platform.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ayush360.Identity.Api.Security;

/// <summary>
/// The Identity API's RSA keys for RS256 access tokens.
///   - SigningCredentials: the PRIVATE key, used only by TokenService to sign tokens. Never leaves this API.
///   - ValidationKeys:     public keys (active + previous) this API uses to check its own tokens.
///   - PublicJwks:         the same public keys as JSON, served at /.well-known/jwks.json for the other APIs.
/// Each key's id ("kid") is its RFC 7638 thumbprint, so the same key always gets the same id.
/// </summary>
public sealed class SigningKeys
{
    private const int MinimumKeySize = 2048;

    public SigningCredentials SigningCredentials { get; }
    public IReadOnlyList<SecurityKey> ValidationKeys { get; }
    public object PublicJwks { get; }

    public SigningKeys(IOptions<JwtOptions> options, IHostEnvironment environment, ILogger<SigningKeys> logger)
    {
        var jwt = options.Value;

        // The RSA object must live as long as the app: it signs every token.
        var rsa = RSA.Create();
        rsa.ImportFromPem(NormalizePem(ReadActivePrivateKey(jwt, environment, logger)));
        if (rsa.KeySize < MinimumKeySize)
        {
            throw new InvalidOperationException($"Jwt:SigningKeyPem must be at least {MinimumKeySize} bits.");
        }

        var activePublic = rsa.ExportParameters(includePrivateParameters: false);
        var activeKid = Thumbprint(activePublic);
        SigningCredentials = new SigningCredentials(
            new RsaSecurityKey(rsa) { KeyId = activeKid },
            SecurityAlgorithms.RsaSha256);

        var publicKeys = new List<(string Kid, RSAParameters Parameters)> { (activeKid, activePublic) };
        foreach (var pem in jwt.PreviousPublicKeysPem.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            using var previous = RSA.Create();
            previous.ImportFromPem(NormalizePem(pem));
            var parameters = previous.ExportParameters(includePrivateParameters: false);
            publicKeys.Add((Thumbprint(parameters), parameters));
        }

        ValidationKeys = publicKeys
            .Select(k => (SecurityKey)new RsaSecurityKey(k.Parameters) { KeyId = k.Kid })
            .ToArray();

        PublicJwks = new
        {
            keys = publicKeys.Select(k => new
            {
                kty = "RSA",
                use = "sig",
                alg = SecurityAlgorithms.RsaSha256,
                kid = k.Kid,
                n = Base64UrlEncoder.Encode(k.Parameters.Modulus),
                e = Base64UrlEncoder.Encode(k.Parameters.Exponent),
            }).ToArray(),
        };

        logger.LogInformation(
            "Token signing key {KeyId} loaded ({PreviousKeyCount} previous keys still accepted)",
            activeKid, publicKeys.Count - 1);
    }

    private static string ReadActivePrivateKey(JwtOptions jwt, IHostEnvironment environment, ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(jwt.SigningKeyPem))
        {
            if (jwt.SigningKeyPem.Contains("PUBLIC KEY", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Jwt:SigningKeyPem holds a PUBLIC key. The Identity API needs the PRIVATE key (BEGIN PRIVATE KEY).");
            }

            return jwt.SigningKeyPem;
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Jwt:SigningKeyPem is missing. On Azure store the RSA private key (PKCS#8 PEM, 2048+ bits) in Key Vault " +
                "as secret 'Jwt--SigningKeyPem'. Create one with: " +
                "openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out signing-key.pem");
        }

        // Development: one key per developer PC, outside the repository, created on first run.
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Ayush360",
            "identity-dev-signing-key.pem");
        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var generated = RSA.Create(MinimumKeySize);
            File.WriteAllText(path, generated.ExportPkcs8PrivateKeyPem());
            logger.LogWarning("Created a development token signing key at {KeyPath}", path);
        }

        return File.ReadAllText(path);
    }

    /// <summary>Key Vault / environment variables sometimes deliver the PEM with literal "\n" instead of line breaks.</summary>
    private static string NormalizePem(string pem) => pem.Replace("\\n", "\n", StringComparison.Ordinal).Trim();

    /// <summary>RFC 7638 JWK thumbprint: SHA-256 of {"e","kty","n"} in this exact order, base64url.</summary>
    private static string Thumbprint(RSAParameters parameters)
    {
        var canonical =
            $"{{\"e\":\"{Base64UrlEncoder.Encode(parameters.Exponent)}\",\"kty\":\"RSA\",\"n\":\"{Base64UrlEncoder.Encode(parameters.Modulus)}\"}}";
        return Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
