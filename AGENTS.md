# AGENTS.md

This file gives AI coding agents the context needed to work effectively on this
repository. It complements `README.md` (human-focused) with agent-focused
technical instructions. Spec follows <https://agents.md/>.

## Project Overview

**sample** is a backend reference implementation in **.NET 10** built with a
strict DDD layering and a vertical-slices feature layout. The API is an
ASP.NET Core **Minimal API** secured with **Keycloak** (OIDC / JWT Bearer),
persisting to **PostgreSQL** through **Entity Framework Core** (Npgsql). It is
fully instrumented with **OpenTelemetry** and ships with a local observability
stack (Alloy -> Tempo / Loki / Prometheus -> Grafana).

### Key technologies

- **Language / Framework**: C# 13 / .NET 10 (`net10.0`)
- **API host**: ASP.NET Core Minimal APIs + OpenAPI (Microsoft.OpenApi)
- **API docs UI**: Scalar.AspNetCore (`/scalar/v1`)
- **Mediation**: Mediator (source-generator, `Sample` namespace)
- **Validation pipeline**: `ValidationBehavior<,>` registered as Mediator pipeline
- **Persistence**: EF Core 10 + Npgsql.EntityFrameworkCore.PostgreSQL
- **AuthN**: Keycloak 26.2 (OIDC) + `JwtBearer`
- **AuthZ**: Policy-based, `RequireAssertion` over `realm_access`/`resource_access` claims
- **Observability**: OpenTelemetry 1.17 (Traces + Metrics, OTLP HTTP) -> Grafana LGTM
- **DI assembly scanning**: Scrutor
- **Containerization**: Docker (multi-stage Dockerfile), docker-compose dev stack
- **Solution file**: `Sample.slnx` (XML-based, .NET 10 feature)

### Solution layout

```text
Sample.slnx                       # XML solution (root)
Directory.Build.props             # net10.0, ImplicitUsings, Nullable, analyzers
Directory.Packages.props          # Central Package Management (CPM)
backend/
  src/
    Sample.Api/                   # Composition root, Minimal API endpoints, DI
      Endpoints/                  # vertical slices: Auth/, Users/
        IEndpoint.cs              # marker interface for endpoint registration
      Extensions/                 # BuilderExtensions, EndpointExtensions, ObservabilityExtensions
      Program.cs                  # top-level host (1 file, no Startup)
      Dockerfile                   # multi-stage, mcr.microsoft.com/dotnet/{sdk,aspnet}:10.0
      appsettings*.json           # config; sensitive values via UserSecrets / env
    Sample.Application/           # use-cases (Features/<Feature>/<Query|Command>/)
      Features/Users/GetUsers/UserQueryHandler.cs  # IRequest + IRequestHandler pair
      AssemblyInfo.cs
    Sample.Infrastructure/        # cross-cutting: EF Core, Mediator pipeline behaviors
      Behaviors/ValidationBehavior.cs
    Sample.Domain/                # pure domain model (no references)
  tests/
    Sample.Api.IntegrationTests/   # xUnit + Testcontainers (Keycloak) + WebApplicationFactory
      Endpoints/                   # PublicEndpointsTests, UsersEndpointAuthorizationTests
      Infrastructure/              # KeycloakFixture, SampleApiFactory, IntegrationTestBase
      keycloak/realm-export.json   # minimal realm imported into the test Keycloak
backoffice/                      # React 19 + Vite + TypeScript SPA (Authorization Code + PKCE)
  src/                            # main.tsx, App.tsx, oidc.ts (UserManager), api.ts
  public/                         # favicon.svg
  Dockerfile                      # multi-stage: node build -> nginx serve (port 80->5173)
  package.json, vite.config.ts    # Vite dev server 5173, /api proxy -> API
  .env                            # VITE_OIDC_* (public PKCE client, no secret)
.docker/                          # local dev observability + Keycloak stack
  docker-compose.yml              # base services
  docker-compose.override.yml     # dev overrides: ports, env, Keycloak config
  alloy/ grafana/ keycloak/ loki/ prometheus/ tempo/
.editorconfig                     # spaces, project-wide formatting
```

Layering dependency rule (enforced by project references):

```text
Api  ->  Application  ->  Infrastructure  ->  Domain
         (Features)      (Behaviors)        (Entities/ValueObjects)
```

`Sample.Application` references `Sample.Infrastructure` (which holds the
Mediator pipeline behavior and EF Core wiring) and `Sample.Infrastructure`
references `Sample.Domain`. `Sample.Domain` has **no** package references.
Do not invert this direction.

## Setup Commands

### Prerequisites

- **.NET SDK 10** (`dotnet --version` must report a `10.x` build).
  Verified on `10.0.400-preview.0.26322.102`. Produces an informational
  `NETSDK1057` message (using a preview release) - this is expected, not an error.
- **Docker** + Docker Compose (for the local observability + Keycloak stack).
- No Node.js, Python, or other runtimes are required for the backend.

### Restore & build

```bash
dotnet restore Sample.slnx
dotnet build Sample.slnx -c Debug          # verified: 0 errors, 0 warnings
dotnet build Sample.slnx -c Release
```

### Local dependencies (Keycloak + Postgres + Grafana LGTM)

From the repo root:

```bash
docker compose -f .docker/docker-compose.yml -f .docker/docker-compose.override.yml up -d
```

Services exposed on the host:

- **Keycloak**: <http://localhost:8080> - credentials `admin / admin`
- **Postgres**: `localhost:5432` (db `keycloak`) - credentials `keycloak / keycloak`
- **Grafana**: <http://localhost:3000> - credentials `admin / admin`
- **Alloy UI**: <http://localhost:12345>
- **Tempo API**: <http://localhost:3200>
- **Loki API**: <http://localhost:3100>
- **Prometheus**: <http://localhost:9090>

### Run the API

Host (uses `appsettings.Development.json`, OTLP at `http://localhost:4318`):

```bash
dotnet run --project backend/src/Sample.Api/Sample.Api.csproj
```

Container (override maps host `5157` -> container `8080`):

```bash
docker compose -f .docker/docker-compose.yml -f .docker/docker-compose.override.yml up sample.api
```

### User Secrets

The `Sample.Api` project has `UserSecretsId = 325c578c-ca6d-464f-a459-6fb7558a19d0`.
Sensitive values (e.g. `Keycloak:ClientSecret` for confidential clients) must
live in User Secrets or env vars, never in committed `appsettings*.json`.

## Development Workflow

### Endpoint convention (vertical slices)

Every HTTP feature is a `sealed class` implementing `IEndpoint`
(`backend/src/Sample.Api/Endpoints/IEndpoint.cs`):

```csharp
public sealed class MyFeatureEndpoint : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/my-feature").WithTags("MyFeature");
        group.MapGet("/", HandleAsync)
             .WithSummary("...")
             .Produces(StatusCodes.Status200OK);
    }
}
```

- Endpoints are auto-discovered via Scrutor (`EndpointExtensions.AddEndpoints`
  scans `typeof(IEndpoint).Assembly`), so a new endpoint file needs **no**
  manual registration.
- Group the endpoints by tag with `WithTags`.
- Always declare `WithSummary` / `WithDescription` + `Produces` / `ProducesProblem`
  so the OpenAPI document (consumed by Scalar at `/scalar/v1`) stays complete.
- Protected groups use `RequireAuthorization("<policy-name>")` (e.g. `"admin"`).

### Application features

Each use case lives in `Sample.Application/Features/<Feature>/<Query|Command>/`
and is a `record` request + handler pair. Send through `IMediator`:

```csharp
await mediator.Send(new UserQuery(), cancellationToken);
```

`Mediator` source generator emits the dispatch types into the `Sample`
namespace. Do not change `options.Namespace = "Sample"` or the generated types
will not match handler lookups.

### Validation behavior

`Sample.Infrastructure.Behaviors.ValidationBehavior<TMessage,TResponse>` is
wired into the Mediator pipeline. The current body is a `//TODO` placeholder -
implement real validation here (FluentValidation or custom) when adding
validation. Do not bypass the pipeline; route all validation through this
behavior to keep cross-cutting concerns in one place.

### Keycloak auth nuances (important)

This repo has a non-trivial Keycloak + JwtBearer setup. Capture the verified
patterns before touching auth code:

1. **`aud` claim**: the Keycloak client `portal-api` must have an
   `oidc-audience-mapper` protocol mapper, otherwise tokens fail validation
   with `WWW-Authenticate: Bearer error="invalid_token", error_description="The audience 'empty' is invalid"`.
   The realm import is at `.docker/keycloak/realm-export.json`. After editing
   the realm, recreate the volume with `docker compose down -v` - a plain
   `restart` will not re-import.
2. **Nested roles**: Keycloak emits roles nested in
   `realm_access.roles` / `resource_access.<client>.roles`. The default
   JwtBearer mapper does **not** flatten them, so role-based authorization is
   implemented with `RequireAssertion` policies that parse the JSON claim
   (see `BuilderExtensions.AddKeycloakAuth`). Do **not** replace with
   `RequireRole` / `IsInRole` alone - it will silently 403 even with valid
   tokens.
3. **Token exchange**: `POST /auth/token` accepts JSON or form-urlencoded
   credentials and proxies Keycloak's `password` grant through a named
   `HttpClient` `"keycloak-token"` (15s timeout, no auto-redirect). Keep the
   dual `Accepts<>` declarations so both content types work.
4. **Token for local testing** after `docker compose up`:

   ```bash
   curl -X POST http://localhost:5157/auth/token \
        -H "Content-Type: application/json" \
        -d '{"username":"admin","password":"admin","scope":"openid profile email"}'
   ```

   Then call protected endpoints with `Authorization: Bearer <access_token>`.

5. **Issuer split between Docker API and browser-facing Keycloak**: when the
   API runs in Docker and the SPA (backoffice) runs in the host browser, the
   API discovers OIDC metadata at `http://keycloak:8080/realms/company` (the
   in-container authority), but tokens received from the browser by Keycloak
   are stamped with `iss=http://localhost:8080/realms/company` (the
   browser-facing authority, taken from the `Host` header). They differ, so a
   token issued through a proper OIDC flow will be rejected with
   `error_description="The issuer 'http://localhost:8080/...' is invalid"`.
   The fix is in `BuilderExtensions.AddKeycloakAuth`: `authority` is used for
   discovery/jwks only, and `Keycloak:ValidIssuers` (semicolon-separated,
   `appsettings.Development.json` + `docker-compose.override.yml`) lists every
   issuer that may legitimately appear in a token. Keep both `localhost` and
   `keycloak` hostnames whenever the API is consumed by a browser client.
6. **SPAs must use a dedicated PKCE public client**: the React backoffice
   (`backoffice/`) uses the `backoffice-web` client configured in
   `realm-export.json` (`standardFlowEnabled=true`, `directAccessGrantsEnabled
   =false`, `pkce.code.challenge.method=S256`) with `react-oidc-context` +
   `oidc-client-ts`. The `backoffice-web` client has an
   `oidc-audience-mapper` that mints `aud=portal-api` (the resource-server
   client) on its access tokens, so a single audience validation covers both
   the password-grant and PKCE flows. Do **not** reuse `portal-api` for the
   SPA - enabling direct access grants on a browser client is an anti-pattern.

### Backoffice SPA conventions (`backoffice/`)

- **Stack**: React 19 + Vite 7 + TypeScript (`strict`), OIDC via
  `react-oidc-context` (built on `oidc-client-ts`).
- **Dev server**: `npm run dev` -> `http://localhost:5173`. The Vite dev proxy
  forwards `/api/*` to the API (`VITE_API_PROXY_TARGET`, default
  `http://localhost:5157`), so the SPA calls relative URLs (no CORS preflight)
  during local development.
- **OIDC config** (`src/oidc.ts`): a single `UserManager` with
  `response_type: 'code'`, `scope: 'openid profile email'`, state in
  `localStorage` (prefix `backoffice.oidc.`). PKCE S256 challenge/verifier is
  generated automatically by `oidc-client-ts` - **do not** supply a partial
  `metadata` object (it short-circuits the discovery fetch and breaks the
  authorize redirect). Leave `metadata` unset so the discovery doc is loaded
  from `authority`.
- **Auto sign-in** (`src/App.tsx`): `react-oidc-context` v3 has no `autoSignin`
  prop; an `useEffect` triggers `auth.signinRedirect()` when the user is not
  authenticated and no navigator is in flight.
- **Sign-in callback** (`src/main.tsx`): `onSigninCallback` strips the
  `code`/`state` querystring after the redirect so a refresh does not replay
  the callback.
- **Container**: `Dockerfile` builds the Vite bundle (with build-time
  `VITE_OIDC_*` / `VITE_API_BASE_URL` args) then serves `dist/` with Nginx on
  port 80, port-mapped to host `5173`. Build args must reflect
  **browser-reachable** URLs (`localhost`), not Docker service names.
- **CORS**: even though the dev proxy avoids CORS in local dev, the API
  registers the `"spa"` CORS policy (`BuilderExtensions.AddSpaCors`) so calls
  from other origins (e.g. the Nginx container at
  `http://localhost:5173` -> `http://localhost:5157`) work without a proxy.
  Origins come from `Cors:AllowedOrigins` (semicolon-separated) in
  `docker-compose.override.yml`.

### Observability convention

- `ObservabilityExtensions` exposes a static `ActivitySource` named
  `Sample.Api` (`1.0.0`). Start custom spans with
  `ObservabilityExtensions.ActivitySource.StartActivity("name")`.
- Mediator's own telemetry is enabled (`Sample.Mediator` activity source and
  meter). Keep the names consistent - the OTel wiring subscribes to both.
- Logs go to stdout as JSON (`AddJsonConsole`); Alloy tails them via the Docker
  socket into Loki. Any log line carrying `"trace_id":"<id>"` renders a
  cross-link from Loki to Tempo.
- Health endpoints (`/health*`) are excluded from ASP.NET Core trace
  instrumentation - preserve this filter when adding new instrumentation.

## Testing Instructions

The solution ships an **integration test project** at
`backend/tests/Sample.Api.IntegrationTests/`. It boots the real `Sample.Api`
in-memory via `WebApplicationFactory<Program>` and exercises the protected
endpoints against a **real Keycloak** instance brought up on demand by
[Testcontainers]. No mocked JWT middleware, no in-memory auth - the JwtBearer
stack, the `admin` policy's `RequireAssertion` and the `oidc-audience-mapper`
audience validation are all exercised against tokens minted by a live Keycloak.

Conventions:

- **Framework**: xUnit (`[Fact]` / `[Theory]`), FluentAssertions-style explicit
  asserts. The `csharp-xunit` skill is installed; see `.agents/skills/csharp-xunit/`.
- **Test discovery**: file naming `*Tests.cs`, methods `public async Task` or
  `public void`.
- **Test classes** extend `IntegrationTestBase`, decorated with the xUnit
  collection `IntegrationTests`. The collection hosts a single
  `KeycloakFixture` (`ICollectionFixture<>`), so the Keycloak container is
  started **once** per test run and reused across tests (~1-2s per assertion).
- **Coverage** (current): `PublicEndpointsTests` (health + token endpoint) and
  `UsersEndpointAuthorizationTests` (401 without token, 200 with admin role,
  403 with user role, 401 with garbage token).
- **No unit test project yet**: layer-scoped unit tests
  (`Sample.Application.Tests`, `Sample.Domain.Tests`, ...) are not scaffolded.
  Drop the first one at `backend/tests/Sample.<Layer>.Tests/`, referencing the
  layer under test only.

### Run

```bash
# All integration tests (Testcontainers pulls the Keycloak image on first run):
dotnet test backend/tests/Sample.Api.IntegrationTests

# Whole solution:
dotnet test Sample.slnx

# Single test:
dotnet test backend/tests/Sample.Api.IntegrationTests `
  --filter "FullyQualifiedName~UsersEndpointAuthorizationTests"
```

Prerequisites: **Docker** running locally (Testcontainers creates and destroys
the Keycloak container via the Docker API; the full `docker-compose` stack is
not needed). No Postgres, no `docker-compose` Keycloak, no `dotnet run` of the
API - the test host boots the API in-process and talks to the throwaway
container. To pre-pull:

```bash
docker pull quay.io/keycloak/keycloak:26.2
```

### Coverage tooling

Configure `coverlet` / `dotnet-coverage` when scaffolding the first unit test
project; report a target here once set.

## Code Style Guidelines

- Enforced by `Directory.Build.props`:
  - `TargetFramework=net10.0`, `ImplicitUsings=enable`, `Nullable=enable`.
  - `EnableNETAnalyzers=true`, `EnforceCodeStyleInBuild=true` - the analyzer
    rules run on every build. Keep the build warning-free (verified: 0 warnings).
- `.editorconfig`: `root = true`, `indent_style = space`. Do not tab-indent.
- **Namespace style**: file-scoped namespaces (`namespace Foo;`) everywhere.
  References to sibling namespaces inside a `using`-block use a leading
  `global::` when shadowing would otherwise occur (see `BuilderExtensions`).
- **Classes are `sealed` by default** unless explicitly designed for
  inheritance (every endpoint class is `sealed`).
- **Endpoint classes**: one feature per file, `sealed`, implements `IEndpoint`,
  methods `private static`.
- **Records for DTOs / messages**: prefer `record` over `class` for request,
  response, and command/query types.
- **Async**: every I/O path returns `Task` / `ValueTask` / `IResult`-async and
  takes a `CancellationToken cancellationToken` last. Do not call `.Result` /
  `.Wait()`; never `async void`. Consult the `csharp-async` skill for edge
  cases.
- **XML docs**: public APIs must carry `/// <summary>` XML docs. The
  `csharp-docs` skill documents the conventions and is installed locally.
- **Configuration keys**: use `Section:Key` (e.g. `Keycloak:Authority`),
  bind via `IConfiguration` in extension methods (see `BuilderExtensions`).
- **Package versions**: managed centrally in `Directory.Packages.props` (CPM).
  Use `dotnet add package <name>` (resolves to the centrally pinned version) or
  bump the version in `Directory.Packages.props` directly. Never write a
  `<PackageVersion>` into an individual `.csproj`. See the `nuget-manager` skill.

## Build and Deployment

### Build

```bash
dotnet build Sample.slnx -c Release
dotnet publish backend/src/Sample.Api/Sample.Api.csproj -c Release -o ./publish
```

### Docker image

`backend/src/Sample.Api/Dockerfile` is a multi-stage build
(consult the `multi-stage-dockerfile` skill when authoring or optimizing it):

1. `base`  - `mcr.microsoft.com/dotnet/aspnet:10.0`, non-root `$APP_UID`,
   `EXPOSE 8080 8081`.
2. `build` - `mcr.microsoft.com/dotnet/sdk:10.0`, restores (CPM props copied
   first for layer caching), builds Release.
3. `publish` - `dotnet publish /p:UseAppHost=false`.
4. `final` - copies `/app/publish` into the base image.

Build context is the repo root (`DockerfileContext = ../../..`). Build with:

```bash
docker compose -f .docker/docker-compose.yml -f .docker/docker-compose.override.yml build sample.api
```

### Environment configuration

- `appsettings.json` - logging defaults only.
- `appsettings.Development.json` - `Otel:*` (OTLP HTTP to `localhost:4318`) and
  `Keycloak:*` (authority `http://localhost:8080/realms/company`, audience
  `portal-api`).
- `docker-compose.override.yml` - in-container overrides:
  - `Keycloak__Authority=http://keycloak:8080/realms/company`
  - `Keycloak__Audience=portal-api`, `Keycloak__RequireHttpsMetadata=false`
  - `OTEL_EXPORTER_OTLP_ENDPOINT=http://alloy:4318`,
    `OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf`
  - Host port `5157` -> container `8080` (note: host port differs from
    container to avoid collisions with the host's `dotnet run` on `8080`).

There is **no** CI pipeline yet. When one is added, follow the
`github-actions-hardening` skill: SHA-pin third-party actions, least-privilege
`permissions`, audit `pull_request_target` / `${{ }}` interpolation for script
injection. Until then, do not add workflows that run on `@latest` tags.

## Security Considerations

- **Secrets never in JSON**: Keycloak client secrets, connection strings with
  passwords, and signing keys go to User Secrets (`dotnet user-secrets`) or
  environment variables. The committed `appsettings*.json` files must not
  contain secrets.
- **TLS**: `app.UseHttpsRedirection()` is on. When running the API outside
  Docker in dev, Keycloak `RequireHttpsMetadata` is `false` (HTTP dev realm) -
  this is dev-only and must be `true` in any non-dev environment.
- **CORS**: configured via `BuilderExtensions.AddSpaCors` under the `"spa"`
  policy. Origins come from `Cors:AllowedOrigins` (semicolon-separated) in
  `docker-compose.override.yml` (dev: `http://localhost:5173;5174;4173;3000`)
  with a fallback to the well-known dev origins if unset. Keep explicit origins
  - never wildcard - and add new frontends here when they are introduced.
- **Keycloak admin**: `admin/admin` is only acceptable for the local dev realm.
  Rotate before any deployment.
- **Database**: Postgres dev credentials `keycloak/keycloak` are dev-only.
- **JWT audience validation**: `ValidateAudience=true` with
  `ValidAudience=portal-api` - the `oidc-audience-mapper` on the Keycloak
  client is mandatory (see Development Workflow -> Keycloak auth nuances).
- Run the `security-review` skill before merging changes that touch
  authentication, endpoints, or data access.

## Skills (installed at project scope)

The following agent skills are pinned in `skills-lock.json` and live in
`.agents/skills/`. They are committable and shared with the team. Both backend
and frontend skill sets are installed (the full manifest is in `skills-lock.json`;
17 skills total).

### Backend (.NET 10 / ASP.NET Core / EF Core stack)

- **`dotnet-best-practices`**: Any .NET/C# change - baseline quality rules
- **`csharp-async`**: Async/await patterns, cancellation, `ValueTask` edge cases
- **`csharp-docs`**: Writing/auditing XML doc comments on public APIs
- **`aspnet-minimal-api-openapi`**: Adding endpoints, OpenAPI/Scalar integration
- **`ef-core`**: DbContext, migrations, Npgsql querying patterns
- **`nuget-manager`**: Adding/updating packages with Central Package Management
- **`csharp-xunit`**: Writing tests for `backend/tests/Sample.Api.IntegrationTests`
  (and future `Sample.<Layer>.Tests` projects)
- **`github-actions-hardening`**: When CI workflows are introduced
- **`multi-stage-dockerfile`**: Authoring/optimizing the API / SPA Dockerfiles

### Frontend (React 19 + Vite + TypeScript stack)

- **`premium-frontend-ui`**: Crafting immersive, high-performance SPA UIs
  (motion, typography, architecture)
- **`web-design-reviewer`**: Reviewing the SPA visually (responsive design,
  accessibility, layout breakage) and fixing it at the source level
- **`react19-source-patterns`**: React 19 source patterns (refs, context, API changes)
- **`react19-test-patterns`**: React 19 test patterns (`act()`, `Simulate` removal,
  `StrictMode` call count changes)
- **`react19-concurrent-patterns`**: `useTransition`, `useDeferredValue`,
  `Suspense`, `use()`, `useOptimistic`
- **`javascript-typescript-jest`**: Writing Jest tests for the SPA once added
- **`chrome-devtools`**: Browser automation, debugging and performance analysis
- **`ui-screenshots`**: Capturing screenshots of the SPA to validate UI changes

Update them with `npx skills update` (or `npx skills update <name>`). Restore
on a fresh clone with `npx skills experimental_install`.

## Debugging and Troubleshooting

- **401 `audience 'empty' is invalid`**: missing `oidc-audience-mapper` on the
  Keycloak client. Edit `.docker/keycloak/realm-export.json` and
  `docker compose down -v && docker compose up -d` to re-import.
- **401 `signature key was not found`**: OIDC discovery document did not
  download; verify `Keycloak:Authority` matches the realm URL exactly
  (trailing slash / `realms/company` segment).
- **403 on a protected endpoint (no `WWW-Authenticate`)**: token is valid but
  the policy's `RequireAssertion` did not find the role in the `realm_access`
  / `resource_access` JSON. Check the realm import assigns the role to the
  user and that the JSON claim is present in the token (decode at jwt.io).
- **`dotnet run` returns 404 on a known endpoint**: a Docker container
  (often `wslrelay` / `api`) is capturing the same port. Run
  `Get-NetTCPConnection -LocalPort <port> -State Listen` and stop the
  container, or use the container override port (`5157`) instead.
- **`NETSDK1057` informational message**: running on a preview .NET SDK.
  Build is still clean - this is noise, not a warning.
- **Mediator dispatch fails at runtime**: confirm
  `options.Assemblies = [typeof(Application.AssemblyInfo).Assembly]` and
  `options.Namespace = "Sample"` were not changed; the source generator emits
  dispatch types into that namespace.

## Pull Request Guidelines

- Title: concise, imperative (e.g. "Add user creation endpoint").
- Commits: small and focused. When the team adopts Conventional Commits,
  update this section.
- The build must be warning-free: `dotnet build Sample.slnx` reports 0 warnings
  on `main`. New warnings block merge.
- New endpoints must declare OpenAPI metadata (`WithSummary`, `WithDescription`,
  `Produces`, `ProducesProblem`) so Scalar stays accurate.
- New application features must flow through `IMediator` (no direct handler
  invocation from endpoints).
- Secrets, connection strings with passwords, and signing material must never
  be committed - use User Secrets or env vars.
- Run the relevant installed skill (`dotnet-best-practices`, `ef-core`,
  `aspnet-minimal-api-openapi`, etc.) before requesting review.
