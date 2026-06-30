# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Thinktecture RelayServer v3 — a distributed HTTPS reverse proxy. On-premises **Connectors** open a single outbound persistent connection to an internet-facing **Server**; clients hit the Server, which routes requests down to the right tenant's Connector and relays the response back. No inbound firewall ports or VPN on the on-premises side.

v3 is shipped as a **collection of NuGet libraries**, not ready-to-run executables. The `src/docker/*` projects are reference host apps wiring the libraries together; real deployments build their own hosts. Everything is plain ASP.NET Core (middlewares, controllers, SignalR Hubs, hosted services).

## Build / run

```bash
dotnet build src/Thinktecture.Relay.sln          # build everything
./src/full-rebuild.ps1                            # restore + Release build + dotnet pack to ./packages
docker compose -f src/docker/docker-compose.yml build   # build all reference host images
docker compose -f src/docker/docker-compose.yml up      # run full local env (DB, RabbitMQ, Keycloak, Seq, server, connectors)
```

- .NET SDK pinned to `8.0.100` (`global.json`, `rollForward: latestFeature`).
- No automated test projects exist in this repo. Manual/integration testing is via the docker-compose environment + the Postman collection in `src/samples/`.
- `RELAYSERVER_DATABASE_TYPE` env var = `PostgreSql` (default) or `SqlServer`, selects the persistence backend in the docker env.
- Local env endpoints: Server `:5000`, ManagementApi swagger `:5004`, Keycloak `:5002`, Seq logs `:5341`, RabbitMQ UI `:15672`/`:15673`. Full list in `docs/development-getting-started.md`.

EF Core migrations live in dedicated assemblies (`...Persistence.EntityFrameworkCore.PostgreSql` / `.SqlServer`). Regenerate via `src/tools/create-migrations.ps1` (or `.sh`) — do not hand-edit generated migrations.

## Architecture: the modularity contract

Everything swappable is an interface in `Thinktecture.Relay.Server.Abstractions` (server side) or `Thinktecture.Relay.Connector.Abstractions` (connector side). Each has exactly one shipped implementation today; respect the interface boundary when extending. The four pluggable module groups:

- **Persistence** — `ITenantService`, `IStatisticsService`, `IRequestService`, `IConnectionService`. Shipped impl: EF Core (`RelayDbContext`).
- **BodyStore** (`IBodyStore`) — persists large request/response bodies. In-memory for single-server; **file-based for multi-server** (all server instances must share one mounted volume).
- **Server-to-Server protocol** — `IServerDispatcher`/`IServerHandler` + `ITenantDispatcher`/`ITenantHandler`. Routes a request/response to the *other* server instance that holds the relevant connection. Shipped impl: RabbitMQ. Only needed in multi-server mode.
- **Server↔Connector protocol** — server side: `ITenantConnectorAdapter(Factory)` + `IConnectorTransport`; connector side: `IConnectorConnection` + `IConnectorTransport`. Shipped impl: SignalR.

Request flow: client → Server → (S2S protocol if connector is on another node) → SignalR down to Connector → Connector invokes a configured **Target** → response relayed back the same path. A Connector receiving a request calls `IClientRequestHandler.HandleAsync` then sends the result via its transport.

### Project layout (`src/`)

`*.Abstractions` = interfaces/contracts. `Thinktecture.Relay.Server` / `.Connector` = core impls. `*.Protocols.SignalR` / `.Protocols.RabbitMq` = protocol impls. `Server.Management` = management API. `Server.Interceptors` = request/response interception hooks. `Server.Persistence.EntityFrameworkCore[.PostgreSql|.SqlServer]` = persistence + migrations.

### Connector targets

Targets are what a Connector forwards to. Registered in DI (`AddRelayConnector(...).AddSignalRConnectorTransport().AddTarget<T>("name")` etc). `RelayWebTarget` (config-driven, see `Connector.Docker/appsettings.json`) forwards to a URL; custom in-proc targets implement `IRelayTargetFunc` / `IRelayTargetAction`. Connector config binds the `RelayConnector` config section.

## Fork note: Windows service connector

This fork adds `src/docker/Thinktecture.Relay.Connector.Windows` — a Connector hosted as a **Windows Service** (`.UseWindowsService()`) rather than a Linux/Docker host. Key difference from the Docker connector: it uses Serilog with a **file sink** writing to `%ProgramData%\RelayConnector\Logs\connector-*.log` (daily rolling, 7 retained) — chosen because the service account can write there but not to the app dir. It contains demo in-proc targets (`InProcFunc`, `InProcAction`) in `Startup.cs`.

## Conventions

- Commit messages: Conventional Commits, enforced informally. `<type>(<scope>): <subject>` — imperative present, lowercase subject, no trailing dot, ≤100 chars/line. Types: build, ci, docs, feat, fix, perf, refactor, style, chore. Scopes: abstractions, connector, connector-abstractions, docker, persistence, server, server-abstractions, management, statistics, interceptor, protocols (+ packaging, changelog, contributing). See `CONTRIBUTING.md`.
- All public API methods must be XML-documented (`GenerateDocumentationFile` is on solution-wide).
- `Nullable` enabled everywhere; `LangVersion latest`. Version is set centrally in `src/Directory.Build.props`.
- Markdown is prettier-formatted on pre-commit (husky + lint-staged).
