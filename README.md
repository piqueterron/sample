# sample

Reference backend implementation in **.NET 10** (DDD layering + vertical-slices
feature layout) with an ASP.NET Core **Minimal API** secured with **Keycloak**
(OIDC / JWT Bearer), persisting to **PostgreSQL** via **Entity Framework Core**
(Npgsql), fully instrumented with **OpenTelemetry**. It ships alongside a
**React 19 backoffice SPA** that authenticates against Keycloak using
**Authorization Code Flow + PKCE (S256)** and calls the protected API endpoints
with the resulting access token.

A high-level architecture and the verified development workflow (endpoints,
auth nuances, observability, code style) live in [`AGENTS.md`](./AGENTS.md).
This README focuses on **how to run and operate** the full stack locally.

## Solution layout

```text
Sample.slnx                       # XML solution (.NET 10 feature)
Directory.Build.props / Directory.Packages.props   # CPM + analyzers
backend/
  src/
    Sample.Api/                   # Minimal API composition root + endpoints
      Endpoints/                  # vertical slices: Auth/, Users/
      Extensions/                 # Builder, Endpoint, Observability
      Program.cs                  # top-level host
      Dockerfile                   # multi-stage, aspnet:10.0
      appsettings*.json           # config; secrets via User Secrets / env
    Sample.Application/           # use-cases (Features/<Feature>/<Query|Command>/)
    Sample.Infrastructure/        # Mediator pipeline behaviors, EF Core wiring
    Sample.Domain/                # pure domain model (no dependencies)
  tests/
    Sample.Api.IntegrationTests/   # xUnit + Testcontainers (Keycloak) + WebApplicationFactory
      keycloak/realm-export.json    # minimal realm imported into the test Keycloak
      Infrastructure/              # KeycloakFixture, SampleApiFactory, IntegrationTestBase
      Endpoints/                   # PublicEndpointsTests, UsersEndpointAuthorizationTests
backoffice/                        # React 19 + Vite + TypeScript SPA
  src/                            # main.tsx, App.tsx, oidc.ts, api.ts
  public/                         # favicon.svg
  Dockerfile                      # multi-stage: node build -> nginx serve
  package.json, vite.config.ts   # Vite dev server :5173, /api proxy
  .env                            # VITE_OIDC_* (public PKCE client, no secret)
.docker/                          # local dev observability + Keycloak stack
  docker-compose.yml              # base services
  docker-compose.override.yml     # dev overrides: ports, env, CORS, issuers
  alloy/ grafana/ keycloak/ loki/ prometheus/ tempo/
.agents/skills/                    # pinned AI agent skills (committable)
skills-lock.json                  # lock manifest for npm-style reproducible installs
```

## Prerequisites

- **.NET SDK 10** (`dotnet --version` reports a `10.x` build; preview SDK
  emits an informational `NETSDK1057` message — expected, not an error).
- **Docker** + Docker Compose (for the local Keycloak + Postgres + Grafana stack).
- **Node.js 22+** + npm (only for the React backoffice outside Docker, or for
  the agent skills CLI).

## Quick start (full stack)

```bash
# Backend (Keycloak + Postgres + Grafana LGTM + API + backoffice SPA)
docker compose -f .docker/docker-compose.yml -f .docker/docker-compose.override.yml up -d

# Or per-layer:
docker compose -f .docker/docker-compose.yml -f .docker/docker-compose.override.yml up -d postgres keycloak
docker compose -f .docker/docker-compose.yml -f .docker/docker-compose.override.yml up -d --build sample.api backoffice
```

Host ports exposed:

| Service        | Container     | Host Port        | Purpose                                    |
|----------------|---------------|------------------|--------------------------------------------|
| `sample.api`   | `api`         | `5157`           | .NET API (OIDC-protected)                  |
| `backoffice`   | `backoffice`  | `5173`           | React SPA (PKCE OIDC + admin UI)           |
| `keycloak`     | `keycloak`    | `8080`           | Keycloak admin + OIDC issuer               |
| `postgres`     | `postgres`    | `5432`           | Keycloak + future app DB                   |
| `grafana`      | `grafana`     | `3000`           | Dashboards (`admin` / `admin`)             |
| `alloy`        | `alloy`       | `4317, 4318`     | OTLP receiver (gRPC + HTTP)                |
| `tempo`        | `tempo`       | `3200`           | Trace storage + query API                  |
| `loki`         | `loki`        | `3100`           | Log storage + query API                    |
| `prometheus`   | `prometheus`  | `9090`           | Metrics TSDB                               |

### Default credentials (dev only - rotate before any deployment)

- Keycloak admin: `admin` / `admin`
- Grafana admin: `admin` / `admin`
- Postgres: `keycloak` / `keycloak`
- Realm `company` test users: `admin`/`admin` (role `admin`),
  `test`/`Password123!` (role `user`).

## Backend API

The API registers OpenAPI and is documented at **Scalar**: <http://localhost:5157/scalar/v1>.
Health endpoint: <http://localhost:5157/health> (excluded from tracing).

### Endpoints

| Method | Path          | Auth              | Notes                                          |
|--------|---------------|-------------------|------------------------------------------------|
| `GET`  | `/health`     | none              | Liveness                                        |
| `POST` | `/auth/token` | anonymous         | Password grant proxy to Keycloak (admin testing)|
| `GET`  | `/users`      | policy `admin`    | Calls `UserQuery` via Mediator. 200 if admin.   |

### Authentication model

- OIDC/JWT Bearer against Keycloak realm `company`.
- **Two clients** are configured in `.docker/keycloak/realm-export.json`:
  - `portal-api` — public resource-server client; used by `/auth/token` (password
    grant) for direct testing and as the API's `Audience`.
  - `backoffice-web` — public browser client for the React SPA. PKCE S256 only
    (`standardFlowEnabled=true`, `directAccessGrantsEnabled=false`,
    `pkce.code.challenge.method=S256`). It carries an `oidc-audience-mapper`
    that stamps `aud=portal-api` so the same audience validation covers both
    flows.
- Authorization is **not** `RequireRole` (Keycloak nests roles in
  `realm_access.roles` / `resource_access.<client>.roles`, which the default
  JwtBearer mapper does not flatten). Instead, `BuilderExtensions.AddKeycloakAuth`
  registers a `RequireAssertion` policy (`admin`) that parses the JSON claim
  directly. Never replace it with `IsInRole`.
- **Issuer split gotcha**: when the API runs in Docker (OIDC discovery at
  `http://keycloak:8080/realms/company`) but tokens are issued via a browser
  flow (SPA at `http://localhost:5173` -> Keycloak at `http://localhost:8080`),
  Keycloak stamps `iss=http://localhost:8080/realms/company`. The API therefore
  accepts a set of **valid issuers** (`Keycloak:ValidIssuers`,
  semicolon-separated in `docker-compose.override.yml` and
  `appsettings.Development.json`) covering both hostnames. See `AGENTS.md`
  "Keycloak auth nuances §5".

### Local testing (password grant)

```bash
# Get an admin access token via the API's password-grant proxy:
TOKEN=$(curl -s -X POST http://localhost:5157/auth/token \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin","scope":"openid profile email"}' \
  | jq -r .access_token)

# Call the protected endpoint:
curl -i http://localhost:5157/users -H "Authorization: Bearer $TOKEN"
# -> 200 OK
```

Verify negative cases work as expected:

| Scenario                           | Expected |
|------------------------------------|----------|
| `admin` token -> `/users`          | `200 OK` |
| `user` (role=user) token           | `403 Forbidden` |
| No token                           | `401 Unauthorized` |
| Bad/expired token                  | `401 Unauthorized` |

## Integration tests (`backend/tests/Sample.Api.IntegrationTests/`)

End-to-end tests that boot the **real** `Sample.Api` in-memory via
`WebApplicationFactory<Program>` and exercise the protected endpoints against a
**real Keycloak** instance brought up on demand by [Testcontainers]. No mocked
JWT middleware, no in-memory auth - the JwtBearer stack, the `admin` policy's
`RequireAssertion` and the `oidc-audience-mapper` audience validation are all
exercised against tokens minted by a live Keycloak.

### What the tests spin up (and what they deliberately do NOT)

| Service         | Started by tests? | Why                                            |
|-----------------|:-----------------:|------------------------------------------------|
| Keycloak 26.2   | yes (Testcontainers)             | OIDC issuer for the JwtBearer auth stack       |
| Postgres        | no                               | The API has no EF Core DbContext wired yet     |
| Alloy / Tempo / Loki / Prometheus / Grafana | no | Observability adds nothing to the assertions and slows the run |

The Keycloak container uses the **dev-grade `quay.io/keycloak/keycloak:26.2`**
image in `start-dev` mode and imports `keycloak/realm-export.json` (a copy of
the dev realm that ships with the docker-compose stack, minus the
`backoffice-web` client - the tests never run a browser flow). The fixture waits
for the OIDC discovery document to become reachable before yielding, so a test
never sees a half-bootstrapped realm.

### Test coverage

| Test (xUnit `[Fact]`)                                              | Endpoint          | Asserts                                  |
|--------------------------------------------------------------------|-------------------|------------------------------------------|
| `PublicEndpointsTests.GetHealth_Returns200`                        | `GET /health`     | `200 OK`                                 |
| `PublicEndpointsTests.PostToken_WithAdminCredentials_…`            | `POST /auth/token`| `200 OK` + `access_token`                |
| `PublicEndpointsTests.PostToken_WithMissingCredentials_…`          | `POST /auth/token`| `400 Bad Request`                        |
| `UsersEndpointAuthorizationTests.GetUsers_WithoutToken_…`         | `GET /users`      | `401 Unauthorized`                       |
| `UsersEndpointAuthorizationTests.GetUsers_WithAdminToken_…`       | `GET /users`      | `200 OK` (role `admin`)                  |
| `UsersEndpointAuthorizationTests.GetUsers_WithUserRoleToken_…`    | `GET /users`      | `403 Forbidden` (role `user`)            |
| `UsersEndpointAuthorizationTests.GetUsers_WithGarbageToken_…`     | `GET /users`      | `401 Unauthorized`                       |

Every test class extends `IntegrationTestBase`, which is decorated with the
shared xUnit collection `IntegrationTests`. The collection itself hosts the
single `KeycloakFixture` (`ICollectionFixture<>`), so the Keycloak container is
started **once** per test run and torn down at the end - subsequent tests in
the same run reuse the running container (~1-2s per assertion).

### How the API is rewired in tests

`SampleApiFactory : WebApplicationFactory<Program>` overrides `ConfigureWebHost`
and uses `IWebHostBuilder.UseSetting(...)` (NOT `ConfigureAppConfiguration`):
`Program.cs` reads `builder.Configuration["Keycloak:Authority"]` during its
top-level execution, which happens **before** any `ConfigureAppConfiguration`
callback runs - so the settings must be fed into the pre-built configuration the
entrypoint sees. The factory points `Keycloak:Authority`,
`Keycloak:TokenEndpoint` and `Keycloak:ValidIssuers` at the Testcontainers
Keycloak URL (so that `iss` validation passes) and rewrites the OTLP endpoints
to unreachable placeholders (so OpenTelemetry fails fast without bringing up
Alloy).

### Run the tests

```bash
# All integration tests (pulls the Keycloak image on first run, ~30-40s):
dotnet test backend/tests/Sample.Api.IntegrationTests

# A single test:
dotnet test backend/tests/Sample.Api.IntegrationTests `
  --filter "FullyQualifiedName~UsersEndpointAuthorizationTests"

# Whole solution (backend src + tests):
dotnet test Sample.slnx
```

### Prerequisites

- **Docker** running locally (Testcontainers creates and destroys the Keycloak
  container via the Docker API - the full `docker-compose` stack is NOT
  needed).
- The Keycloak image is pulled automatically on the first run. To pre-pull:
  ```bash
  docker pull quay.io/keycloak/keycloak:26.2
  ```
- No Postgres, no Keycloak from `docker-compose`, no `dotnet run` of the API -
  the test host boots the API in-process and talks to the throwaway container.

## Backoffice SPA (`backoffice/`)

React 19 + Vite + TypeScript SPA that simulates a browser login against Keycloak
using **Authorization Code Flow with PKCE (S256)**.

### Stack

- **React 19.1** with `react-oidc-context` (v3, built on `oidc-client-ts`)
  for the OIDC flow. PKCE S256 challenge/verifier is generated automatically by
  `oidc-client-ts`.
- **Vite 7** dev server on port `5173` with a `/api` proxy to the API (default
  target `http://localhost:5157`, configurable via `VITE_API_PROXY_TARGET`). The
  Dev proxy means the SPA can use **relative** `/api/*` URLs in dev and avoid
  CORS preflight; but CORS is also configured on the API as a fallback.
- **TypeScript** strict mode (`tsconfig.json`), ESLint flat config.
- Multi-stage **Dockerfile**: Node build -> Nginx serve `dist/` on port `80`
  (host-mapped to `5173`). Browser-reachable URLs are baked into the bundle
  via build-time args (must use `localhost`, not Docker service names, because
  the SPA runs in the host browser).

### Run the SPA

```bash
# Option A - from Docker (built into the docker-compose stack)
docker compose -f .docker/docker-compose.yml -f .docker/docker-compose.override.yml up -d backoffice
# -> http://localhost:5173/

# Option B - from source (npm dev server; still needs Keycloak + API running)
cd backoffice
npm install
npm run dev     # http://localhost:5173/ (Vite dev server with /api proxy)
npm run build   # type-check + production build into dist/
npm run preview # serve the production build on :4173
```

### OIDC configuration

| Env var (`backoffice/.env`)         | Default                                          |
|--------------------------------------|--------------------------------------------------|
| `VITE_OIDC_CLIENT_ID`                | `backoffice-web` (matches realm client)          |
| `VITE_OIDC_REALM`                    | `company`                                         |
| `VITE_OIDC_AUTHORITY`                | `http://localhost:8080/realms/company`            |
| `VITE_OIDC_REDIRECT_URI`             | `http://localhost:5173/`                          |
| `VITE_API_PROXY_TARGET`              | `http://localhost:5157` (dev proxy target)        |

OIDC is wired in `backoffice/src/oidc.ts` (a single `UserManager`). Notes:

- The `metadata` property **must be left unset** — providing only
  `{ issuer }` makes `oidc-client-ts` treat that as the complete discovery
  response and skip the discovery fetch, breaking the authorize redirect.
- `react-oidc-context` v3 has **no** `autoSignin` prop on `<AuthProvider>`;
  `src/App.tsx` triggers the redirect with a `useEffect` when the user is
  unauthenticated and no navigator is in flight.
- `src/main.tsx` registers an `onSigninCallback` that strips `?code&state`
  from the URL so a refresh doesn't replay the callback.
- State is persisted in `localStorage` with prefix `backoffice.oidc.`.
- Silent token renewal is enabled.

### End-to-end flow (verified)

1. Open <http://localhost:5173/>.
2. Auto-redirect to Keycloak `/protocol/openid-connect/auth` with
   `code_challenge_method=S256`.
3. Sign in as `admin` / `admin`.
4. Keycloak redirects back with `?code=...`; the SPA exchanges it (with the
   PKCE verifier) for tokens.
5. Click **Call GET /users**. The SPA sends the access token as
   `Authorization: Bearer <jwt>`; the API validates it (`iss`, `aud`,
   signature, `realm_access.roles`) and the `admin` policy resolves →
   **`200 OK`**.

## Observability (local development)

The `docker-compose` stack spins up a Grafana LGTM-style pipeline using
**Alloy** (OTLP collector) -> **Tempo** (traces) + **Prometheus** (metrics)
with **Grafana** as the visualization layer.

### Pipeline

```
.NET API --(OTLP HTTP/protobuf)--> Alloy --+--> Tempo (traces)
                                            +--> Prometheus (remote_write) -+
.NET API (stdout JSON logs) -> Alloy (docker.sock) -> Loki -----------------+
                                                                           |
                                                                          Grafana
```

### Endpoints

- **Grafana**: http://localhost:3000 (`admin` / `admin`)
  - Datasources `Prometheus`, `Tempo` and `Loki` are auto-provisioned (see
    `.docker/grafana/provisioning/datasources/datasources.yml`).
  - The **Sample API Observability** dashboard is auto-provisioned under the
    `Observability` folder (see `.docker/grafana/dashboards/sample-api.json`).
    Open it directly at http://localhost:3000/d/sample-api-observability.
- **Tempo API**: http://localhost:3200
- **Loki API**: http://localhost:3100
- **Prometheus**: http://localhost:9090
- **Alloy UI**: http://localhost:12345

### .NET instrumentation

The API registers OpenTelemetry in
`backend/src/Sample.Api/Extensions/ObservabilityExtensions.cs` and is wired
from `Program.cs` via `builder.Services.AddObservability(builder.Configuration)`.

Configuration lives in `appsettings.json` under `Otel:` and is overridable
through the standard environment variables `OTEL_*` (see
`.docker/docker-compose.yml` and `.docker/docker-compose.override.yml`):

```
Otel__ServiceName=sample-api
Otel__Endpoint__Traces=http://alloy:4318/v1/traces
Otel__Endpoint__Metrics=http://alloy:4318/v1/metrics
```

> When running the API **outside** Docker (e.g. `dotnet run` from the host),
> use `http://localhost:4318/...` instead - that's the default in
> `appsettings.json`, so no overrides are needed.

### Custom spans

Use the static `ActivitySource` exposed by `ObservabilityExtensions`:

```csharp
using Sample.Api.Extensions;

using var activity = ObservabilityExtensions.ActivitySource.StartActivity("do-work");
// ... work ...
```

### Logs (.NET -> Loki)

The API writes logs to stdout as JSON (`builder.Logging.AddJsonConsole(...)` in
`Program.cs`). Alloy tails every container's stdout via the Docker socket
(`loki.source.docker`) and pushes them to Loki with the labels `level`,
`logger` and (when present) `app`.

In Grafana Explore -> Loki, query e.g.:

```
{container="api"} | json | line_format "{{.level}} {{.logger}}: {{.message}}"
```

If a log line contains `"trace_id":"<id>"`, the Loki datasource's
`derivedFields` will render a clickable link that jumps straight into Tempo
with that trace id - this is the same wiring used by the **API Logs** panel of
the provisioned dashboard.

### Sample API dashboard

The dashboard **Sample API Observability** (uid `sample-api-observability`) is
provisioned automatically and contains:

| Panel                    | Source      | What it shows                                          |
|--------------------------|-------------|--------------------------------------------------------|
| Request Rate (req/s)     | Prometheus  | `sum(rate(http_server_request_duration_seconds_count))`|
| Success Rate             | Prometheus  | non-5xx / total                                        |
| Latency p99              | Prometheus  | `histogram_quantile(0.99, ...)`                        |
| 5xx Error Rate           | Prometheus  | `sum(rate(...{http_status_code=~"5.."}))`             |
| Request Rate by Route    | Prometheus  | stacked by `http_method`/`http_route`                 |
| Latency Percentiles      | Prometheus  | p50/p95/p99 by route                                   |
| API Logs                 | Loki        | `{container="api"}` live tail                         |
| Recent Traces            | Tempo       | TraceQL `{resource.service.name = "sample-api"}`       |

Direct URL: http://localhost:3000/d/sample-api-observability

## AI agent skills (`.agents/skills/`)

Reusable agent skill packages pinned in [`skills-lock.json`](./skills-lock.json)
and materialized under `.agents/skills/`. Each skill is a `SKILL.md` that loads
into the GitHub Copilot agent context automatically. They are committable and
shared with the team; install only to `github-copilot` (the `.claude/` folder is
**not** used by this repo).

### Update / restore skills

```bash
npx skills add github/awesome-copilot --skill <name> --agent github-copilot -y   # add
npx skills update                                                                # update all
npx skills update <name>                                                         # update one
npx skills experimental_install                                                  # restore on a fresh clone
```

> On hosts behind a corporate proxy, the embedded git may reject Keycloak's
> self-signed cert. Fix once: `git config --global http.sslBackend schannel`
> and `git config --global http.schannelCheckRevoke false`.

### Backend (.NET 10 / ASP.NET Core / EF Core stack)

| Skill                        | When to use                                                   |
|------------------------------|---------------------------------------------------------------|
| `dotnet-best-practices`      | Any .NET/C# change - baseline quality rules                   |
| `csharp-async`               | Async/await patterns, cancellation, `ValueTask` edge cases     |
| `csharp-docs`                | Writing/auditing XML doc comments on public APIs              |
| `aspnet-minimal-api-openapi` | Adding endpoints, OpenAPI/Scalar integration                  |
| `ef-core`                    | DbContext, migrations, Npgsql querying patterns               |
| `nuget-manager`              | Adding/updating packages under Central Package Management      |
| `csharp-xunit`               | Writing tests for `backend/tests/` once scaffolded             |
| `github-actions-hardening`  | When CI workflows are introduced                              |
| `multi-stage-dockerfile`    | Authoring/optimizing the API Dockerfile                        |

### Frontend (React 19 + Vite + TS stack)

Selected from the `github/awesome-copilot` catalogue to match the backoffice SPA
stack. Installed at project scope (`.agents/skills/`, not global):

| Skill                            | When to use                                                                    |
|----------------------------------|--------------------------------------------------------------------------------|
| `premium-frontend-ui`            | Crafting immersive, high-performance web UIs (motion, typography, architecture) |
| `web-design-reviewer`           | Reviewing the SPA visually (responsive design, accessibility, layout)           |
| `react19-source-patterns`       | React 19 source patterns (refs, context, API changes)                           |
| `react19-test-patterns`          | React 19 test patterns (`act()`, `Simulate` removal, `StrictMode`)             |
| `react19-concurrent-patterns`   | `useTransition`, `useDeferredValue`, `Suspense`, `use()`, `useOptimistic`      |
| `javascript-typescript-jest`    | Writing Jest tests for the SPA once tests are added                            |
| `chrome-devtools`                | Browser automation, debugging and performance analysis                         |
| `ui-screenshots`                | Capturing screenshots of the SPA to validate UI changes                         |

Loaded automatically when working in matching files; `go-repo-contribution`
forces a re-scan to follow the right steps before any issue/branch/commit/PR.
Run the relevant skill before requesting review.
