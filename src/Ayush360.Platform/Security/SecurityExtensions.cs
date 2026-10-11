using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Ayush360.Platform.Security;

public static class SecurityExtensions
{
    /// <summary>
    /// Validates Ayush360 access tokens (RS256, issued by the Identity API) on every API.
    /// - Module APIs (HMS, ...): set Jwt:JwksUrl; public keys are downloaded from Identity and cached.
    ///   When Identity rotates its key, a token with an unknown key id triggers a re-download automatically.
    /// - Identity API: leaves Jwt:JwksUrl empty and plugs in its own keys (see Identity's Program.cs).
    /// </summary>
    public static IServiceCollection AddAyushAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(JwtOptions.SectionName);
        services.AddOptions<JwtOptions>().Bind(section);
        var jwt = section.Get<JwtOptions>() ?? new JwtOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep claim names exactly as issued ("sub", "perm", ...).
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    // Only RS256 is accepted: blocks "alg: none" and HMAC tokens signed with the public key.
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = PlatformClaims.Name,
                    RoleClaimType = PlatformClaims.Role,
                };

                if (!string.IsNullOrWhiteSpace(jwt.JwksUrl))
                {
                    options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                        jwt.JwksUrl,
                        new JwksRetriever(jwt.Issuer),
                        new HttpDocumentRetriever { RequireHttps = jwt.RequireHttpsMetadata })
                    {
                        AutomaticRefreshInterval = TimeSpan.FromHours(1),
                        // Retry quickly if Identity wasn't up yet when this API started.
                        RefreshInterval = TimeSpan.FromSeconds(30),
                    };
                }
            });

        // Fail at startup (not on the first request) when an API has no way to check token signatures.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Validate(
                options => options.ConfigurationManager is not null
                           || options.TokenValidationParameters.IssuerSigningKeys?.Any() == true,
                "No token signing keys configured. Module APIs must set Jwt:JwksUrl " +
                "(development: http://localhost:5077/.well-known/jwks.json).")
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Secure by default: every endpoint needs a valid token unless it is marked [AllowAnonymous].
    /// [RequirePermission] / [RequireModule] add permission and licence checks on top.
    /// </summary>
    public static IServiceCollection AddAyushAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, PlatformPolicyProvider>();
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }

    /// <summary>Downloads Identity's jwks.json and hands its public keys to the JWT validator.</summary>
    private sealed class JwksRetriever(string issuer) : IConfigurationRetriever<OpenIdConnectConfiguration>
    {
        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
            string address, IDocumentRetriever retriever, CancellationToken cancel)
        {
            var json = await retriever.GetDocumentAsync(address, cancel);
            var keySet = new JsonWebKeySet(json);

            var configuration = new OpenIdConnectConfiguration { Issuer = issuer, JwksUri = address };
            foreach (var key in keySet.GetSigningKeys())
            {
                configuration.SigningKeys.Add(key);
            }

            return configuration;
        }
    }
}
