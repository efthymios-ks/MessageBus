# Deployment

Linux runtime, Docker for Operations, Aspire for dev orchestration.

## Runtime

- Linux is the source of truth. Windows dev boxes are fine, but nothing ships from a Windows agent.
- Full rules: [linux-runtime.md](../linux-runtime.md).

## Operations.Web in Docker

Multi-stage Dockerfile at `src/MessageBus.Operations.Web/Dockerfile`.
Build context is the repo root so `Directory.*.props`, `global.json`, and `NuGet.Config` are visible:

```bash
docker build -t messagebus-operations -f src/MessageBus.Operations.Web/Dockerfile .
```

- Base runtime: `mcr.microsoft.com/dotnet/aspnet:10.0`.
- Non-root user: `$APP_UID` (baked into the base image).
- Kestrel binds `http://+:8080`.
- TLS is expected upstream (ingress / gateway / sidecar).

### Environment variables

ASP.NET Core maps `__` in an env var name to `:` in config.
Set only the values that differ from defaults.

Required — no defaults:

| Env var | Maps to | Notes |
| --- | --- | --- |
| `ConnectionStrings__OperationsDb` | `ConnectionStrings:OperationsDb` | SQL Server connection string. |
| `ConnectionStrings__RMQ` | `ConnectionStrings:RMQ` | RabbitMQ AMQP URI. Required when `Messaging__Transport=rmq`. |
| `ConnectionStrings__ASB` | `ConnectionStrings:ASB` | Azure Service Bus connection string. Required when `Messaging__Transport=asb`. |
| `Messaging__Operations__EndpointKeys__<endpoint-name>` | `Messaging:Operations:EndpointKeys:<name>` | Seeds one endpoint + API key on first boot. Repeat per endpoint. |

Optional — defaults good enough for most deployments:

| Env var | Default | Notes |
| --- | --- | --- |
| `Messaging__Transport` | `rmq` | `rmq` or `asb`. |
| `Messaging__Operations__ErrorQueueName` | `messagebus-error` | Shared error queue name. |
| `Messaging__Operations__AuditQueueName` | `messagebus-audit` | Shared audit queue name. |
| `ASPNETCORE_URLS` | `http://+:8080` | Listen address. Change if fronting a non-8080 port. |
| `Logging__LogLevel__Default` | `Warning` | Standard ASP.NET logging knob. |

Example `docker run`:

```bash
docker run -d --name messagebus-operations \
  -p 8080:8080 \
  -e ConnectionStrings__OperationsDb="Server=sql;Database=Operations;User Id=sa;Password=…;TrustServerCertificate=true" \
  -e ConnectionStrings__RMQ="amqp://user:password@rabbit:5672/" \
  -e Messaging__Operations__EndpointKeys__orders="ops_key_orders_…" \
  -e Messaging__Operations__EndpointKeys__billing="ops_key_billing_…" \
  messagebus-operations
```

## Where to run it

| Platform | Fit |
| --- | --- |
| Kubernetes | Best if the estate already runs K8s. Small Deployment + Service + Ingress. |
| Azure Container Apps | Best on Azure without K8s ops. Managed, cheap, HTTPS + scale out of the box. |
| App Service for Containers | If you already pay for App Service plans elsewhere. |
| Nomad / plain Docker on VM | If the estate is small enough that K8s is overkill. |

## Endpoint reachability

Every endpoint host POSTs heartbeats to Operations.
Whatever platform you pick, pin Operations to a stable DNS name inside the same network boundary as the endpoints.
No reason to expose it to the public internet.

## Aspire (dev)

```bash
dotnet run --project src/samples/Hosts.Aspire --launch-profile RMQ
dotnet run --project src/samples/Hosts.Aspire --launch-profile ASB
```

- One AppHost, a switch for the broker.
- RabbitMQ is the day-to-day default. No entity ceiling, management UI on 15672, state survives restart.
- Service Bus emulator earns its place before merging anything that touches topology, message names, or serialization. It fails the way production fails.
- Neither creates topology. RabbitMQ loads a definitions file mounted into the container. The emulator declares its entities in the AppHost.
- Stop one profile before starting the other.
