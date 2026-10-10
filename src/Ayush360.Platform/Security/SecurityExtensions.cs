using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Ayush360.Platform.Security;

public static class SecurityExtensions
{
    /// <summary>
    /// Validates Ayush360 access tokens (issued by Identity) on every API.
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
                    IssuerSigningKey = jwt.GetSigningKey(),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = PlatformClaims.Name,
                    RoleClaimType = PlatformClaims.Role,
                };
            });

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
}
