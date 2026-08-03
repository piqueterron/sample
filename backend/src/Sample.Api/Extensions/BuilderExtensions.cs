namespace Sample.Api.Extensions;

using System.Net.Mime;
using System.Security.Claims;
using System.Text.Json;
using FluentValidation;
using global::Mediator;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

public static class BuilderExtensions
{
    /// <summary>
    /// Configures CORS for the SPA clients that call the API from a different origin
    /// (e.g. the React backoffice on http://localhost:5173 calling http://localhost:5157).
    /// Origins are read from the "Cors:AllowedOrigins" configuration section (semicolon-
    /// separated) and fall back to the well-known dev origins.
    /// </summary>
    public static IServiceCollection AddSpaCors(this IServiceCollection services, IConfiguration configuration)
    {
        var configured = configuration["Cors:AllowedOrigins"];
        var origins = (string.IsNullOrWhiteSpace(configured)
                ? "http://localhost:5173;http://localhost:5174;http://localhost:4173;http://localhost:3000"
                : configured)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        services.AddCors(options =>
        {
            options.AddPolicy("spa", policy => policy
                .WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
        });

        return services;
    }

    /// <summary>
    /// Registers the global exception handler so unhandled exceptions are mapped
    /// to RFC 7807 <c>ProblemDetails</c> responses. Domain invariants become
    /// <c>409 Conflict</c>; everything else becomes <c>500 Internal Server Error</c>.
    /// Endpoints that declare <c>ProducesProblem(...)</c> in their OpenAPI metadata
    /// now have a runtime handler that actually produces that shape.
    /// </summary>
    public static IServiceCollection AddGlobalExceptionHandler(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        return services;
    }

    public static IServiceCollection AddDispatcher(this IServiceCollection services)
    {
        services.AddMediator((MediatorOptions options) =>
        {
            options.Namespace = "Sample";
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.GenerateTypesAsInternal = true;
            options.Assemblies = [typeof(Application.AssemblyInfo).Assembly];
            options.PipelineBehaviors = [typeof(Application.Behaviors.ValidationBehavior<,>)];
            options.StreamPipelineBehaviors = [];

            options.Telemetry.EnableMetrics = true;
            options.Telemetry.MeterName = "Sample.Mediator";
            options.Telemetry.EnableTracing = true;
            options.Telemetry.ActivitySourceName = "Sample.Mediator";
        });

        return services;
    }

    /// <summary>
    /// Registers the application-layer abstractions against their
    /// <c>Sample.Infrastructure</c> implementations (e.g.
    /// <c>IUserRepository</c> -> <c>UserRepository</c>,
    /// <c>ITaskRepository</c> -> <c>TaskRepository</c>). Uses Scrutor assembly
    /// scanning so new repository implementations in the Infrastructure
    /// assembly are picked up automatically. Also registers FluentValidation
    /// validators discovered in the Application assembly so the
    /// <c>ValidationBehavior</c> pipeline behavior can resolve them.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var infrastructureAssembly = typeof(Infrastructure.Persistence.SampleDbContext).Assembly;
        var applicationAssembly = typeof(Application.AssemblyInfo).Assembly;

        // Scan the Infrastructure assembly for any concrete class that
        // directly implements an interface from Sample.Application.Abstractions.
        // Since each repository implements exactly one such interface the
        // AsMatchingInterface registration wires them up automatically.
        services.Scan(scan => scan
            .FromAssemblies(infrastructureAssembly)
            .AddClasses(classes => classes.AssignableTo(typeof(Sample.Application.Abstractions.IUserRepository)))
            .AsMatchingInterface()
            .WithScopedLifetime());

        services.Scan(scan => scan
            .FromAssemblies(infrastructureAssembly)
            .AddClasses(classes => classes.AssignableTo(typeof(Sample.Application.Abstractions.ITaskRepository)))
            .AsMatchingInterface()
            .WithScopedLifetime());

        // FluentValidation: every AbstractValidator<T> in Application is
        // registered as a validator. The ValidationBehavior pipeline resolves
        // IEnumerable<IValidator<TMessage>> from DI.
        services.AddValidatorsFromAssembly(applicationAssembly);

        return services;
    }

    public static IServiceCollection AddKeycloakAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection("Keycloak");
        var authority = section["Authority"] ?? throw new InvalidOperationException("Keycloak:Authority is not configured.");
        var audience = section["Audience"] ?? throw new InvalidOperationException("Keycloak:Audience is not configured.");
        var requireHttps = section.GetValue("RequireHttpsMetadata", true);

        // In a Docker dev stack the OIDC discovery document is fetched in-container
        // (http://keycloak:8080/...) but tokens are issued with the browser-facing
        // issuer (http://localhost:8080/...) because Keycloak stamps `iss` from the
        // Host header of the requesting browser. Both issuers must be accepted, so
        // `Keycloak:ValidIssuers` is an optional semicolon-separated list of every
        // issuer that may appear in a real token; `authority` is always accepted
        // (it is the issuer the discovery document reports from the API's viewpoint).
        var extraIssuers = (section["ValidIssuers"] ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var validIssuers = new HashSet<string>(StringComparer.Ordinal) { authority.TrimEnd('/') };
        foreach (var issuer in extraIssuers)
        {
            validIssuers.Add(issuer.TrimEnd('/'));
        }

        services.AddHttpClient("keycloak-token", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
        })
        // Use framework defaults for the primary handler. Previous code disabled
        // auto-redirect (`AllowAutoRedirect = false`) and disabled the system proxy
        // (`UseProxy = false`); the latter breaks any environment behind a corporate
        // forward proxy. Leaving all defaults keeps the handler behavior aligned
        // with `HttpClientFactory` expectations.
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler());

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // `Authority` is used only to locate the OIDC discovery document
                // (from which the signing keys / jwks_uri are read). It does NOT
                // restrict which `iss` claims are accepted - that is governed by
                // `TokenValidationParameters.ValidIssuers` below.
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = requireHttps;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuers = validIssuers,
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