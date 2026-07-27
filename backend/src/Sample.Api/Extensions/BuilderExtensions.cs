namespace Sample.Api.Extensions;

using System.Net.Mime;
using System.Security.Claims;
using System.Text.Json;
using global::Mediator;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

public static class BuilderExtensions
{
    public static IServiceCollection AddDispatcher(this IServiceCollection services)
    {
        services.AddMediator((MediatorOptions options) =>
        {
            options.Namespace = "Sample";
            options.ServiceLifetime = ServiceLifetime.Singleton;
            options.GenerateTypesAsInternal = true;
            options.Assemblies = [typeof(Application.AssemblyInfo).Assembly];
            options.PipelineBehaviors = [typeof(Infrastructure.Behaviors.ValidationBehavior<,>)];
            options.StreamPipelineBehaviors = [];

            options.Telemetry.EnableMetrics = true;
            options.Telemetry.MeterName = "Sample.Mediator";
            options.Telemetry.EnableTracing = true;
            options.Telemetry.ActivitySourceName = "Sample.Mediator";
        });

        return services;
    }

    public static IServiceCollection AddKeycloakAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("Keycloak");
        var authority = section["Authority"] ?? throw new InvalidOperationException("Keycloak:Authority is not configured.");
        var audience = section["Audience"] ?? throw new InvalidOperationException("Keycloak:Audience is not configured.");
        var requireHttps = section.GetValue("RequireHttpsMetadata", true);

        services.AddHttpClient("keycloak-token", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseProxy = false
        });

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = requireHttps;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authority,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    NameClaimType = "preferred_username",
                    RoleClaimType = "role"
                };
            });

        // Keycloak nests roles under realm_access.roles and resource_access.<client>.roles.
        // Instead of flattening them at token validation time (which couples us to the
        // internal token type), evaluate the JSON directly in the authorization policy.
        services.AddAuthorization(options =>
        {
            options.AddPolicy("admin", policy => policy.RequireAssertion(context =>
            {
                var principal = context.User;
                if (principal is null)
                {
                    return false;
                }

                // Look for a registered role in either the realm_access or resource_access scopes.
                foreach (var claim in principal.FindAll("realm_access"))
                {
                    if (ClaimHasRole(claim.Value, "admin"))
                    {
                        return true;
                    }
                }

                foreach (var claim in principal.FindAll("resource_access"))
                {
                    if (ClaimHasRole(claim.Value, "admin"))
                    {
                        return true;
                    }
                }

                // Fallback: a flattened "role" claim (e.g. when caller already mapped it).
                return principal.IsInRole("admin");
            }));
        });

        return services;
    }

    private static bool ClaimHasRole(string claimValue, string roleName)
    {
        if (string.IsNullOrWhiteSpace(claimValue))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(claimValue);
            return RolesInElement(document.RootElement, roleName);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool RolesInElement(JsonElement element, string roleName)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("roles", out var roles)
            && roles.ValueKind == JsonValueKind.Array)
        {
            foreach (var r in roles.EnumerateArray())
            {
                if (r.ValueKind == JsonValueKind.String
                    && string.Equals(r.GetString(), roleName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}