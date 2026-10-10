using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Ayush360.Platform.Security;

/// <summary>
/// "Jwt" configuration section. Issuer/Audience live in appsettings.json;
/// SigningKey is a secret (dotnet user-secrets locally, Key Vault on Azure). Never commit it.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 30;

    /// <summary>How long a refresh token keeps the user signed in without typing the password again.</summary>
    public int RefreshTokenDays { get; set; } = 7;

    public SymmetricSecurityKey GetSigningKey()
    {
        if (string.IsNullOrWhiteSpace(SigningKey) || SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is missing or shorter than 32 characters. " +
                "Set the same key for every API with: dotnet user-secrets set \"Jwt:SigningKey\" <key>");
        }

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
    }
}
