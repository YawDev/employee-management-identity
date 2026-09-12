# Octopus — Employee-Management-Identity project

Copy-paste reference for the Octopus project. Two steps, both **Run a Script**
(Bash) against target tag `emt-identity`.

## Prerequisites

| Thing | Value |
|---|---|
| Environment | `Production` |
| SSH target | `159.89.246.38`, user `deploy`, key `emt_octopus`, target tag `emt-identity` |
| Lifecycle | `EMT Release Path` — single phase, **manual** |
| Docker feed | `https://ghcr.io`, username = GitHub handle, password = PAT with `read:packages` |

## Variables

🔒 = mark **Sensitive**. Leave every scope blank — you have one environment and
one target, so scoping can only cause a variable to silently resolve empty.

### Library variable set `EMT Shared Configs`

Shared with the microservice project. Everything here is identical for both
services, so it lives in one place and cannot drift.

| Name | Value |
|---|---|
| 🔒 `EMT.Jwt.Key` | `openssl rand -base64 64` |
| `EMT.Jwt.Issuer` | `https://auth.employee-management-tool.com` |
| `EMT.Jwt.Audience` | `emt-api` |
| `EMT.Ghcr.User` | GitHub username |
| 🔒 `EMT.Ghcr.Token` | PAT with `read:packages` |
| `EMT.Cors.Origin.App` | `https://app.employee-management-tool.com` |
| `EMT.Cors.Origin.Sys` | `https://sys.employee-management-tool.com` |
| 🔒 `EMT.Db.ConnectionString` | Neon **pooled** string (`-pooler` host), Npgsql format |

The three JWT values matter most: the microservice validates tokens this service
mints, so a mismatch means login succeeds and every subsequent call 401s with
nothing in the logs explaining why.

### Project variables (this project only)

| Name | Value |
|---|---|
| `EMT.Container.Name` | `emt-identity` |
| `EMT.PublicUrl` | `https://auth.employee-management-tool.com` |

Only what genuinely differs between the two services. Both share one Neon
database — identity maps the domain tables itself (`ExcludeFromMigrations`), so
they cannot be separated — which also means the Neon password is rotated in
exactly one place.

## Step 1 — Deploy container

Reference the ghcr package `yawdev/identity-api` from the Docker feed (ghcr requires the fully-qualified `owner/image` form — a bare `identity-api` is rejected) so Octopus tracks
the version and can roll back to a specific tag.

```bash
set -euo pipefail

IMAGE="ghcr.io/yawdev/identity-api:$(get_octopusvariable 'Octopus.Action.Package[yawdev/identity-api].PackageVersion')"
NAME="$(get_octopusvariable 'EMT.Container.Name')"

# ghcr packages are private by default, so the droplet needs credentials to pull.
echo "$(get_octopusvariable 'EMT.Ghcr.Token')" \
  | docker login ghcr.io -u "$(get_octopusvariable 'EMT.Ghcr.User')" --password-stdin

docker pull "$IMAGE"
docker rm -f "$NAME" 2>/dev/null || true

# No published ports: Caddy reaches this container by name over the `emt`
# network. Publishing to the host would expose the API over plain HTTP.
docker run -d --name "$NAME" --restart unless-stopped \
  --network emt \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection="$(get_octopusvariable 'EMT.Db.ConnectionString')" \
  -e Jwt__Key="$(get_octopusvariable 'EMT.Jwt.Key')" \
  -e Jwt__Issuer="$(get_octopusvariable 'EMT.Jwt.Issuer')" \
  -e Jwt__Audience="$(get_octopusvariable 'EMT.Jwt.Audience')" \
  -e CorsOriginSettings__DomainList__0="$(get_octopusvariable 'EMT.Cors.Origin.App')" \
  -e CorsOriginSettings__DomainList__1="$(get_octopusvariable 'EMT.Cors.Origin.Sys')" \
  "$IMAGE"

docker image prune -af --filter "until=168h"
```

`__` (double underscore) is how .NET maps environment variables onto nested
configuration keys, so `Jwt__Key` lands in `Configuration["Jwt:Key"]`. The prune
keeps old image layers from filling the disk.

## Step 2 — Health check

```bash
URL="$(get_octopusvariable 'EMT.PublicUrl')/health"
NAME="$(get_octopusvariable 'EMT.Container.Name')"

for i in $(seq 1 30); do
  if curl -fsS "$URL" | grep -q Healthy; then
    echo "Healthy after $((i * 2))s"
    exit 0
  fi
  sleep 2
done

echo "Health check failed after 60s"
docker logs --tail 50 "$NAME"
exit 1
```

Failing the deploy on a bad health check is what makes the pipeline trustworthy —
without it a broken container deploys "successfully". The 60s window also absorbs
a Neon cold start, since `AddDbContextCheck` opens a real connection and the free
tier suspends after idle.

## Rollback

Open the previous successful release → **Deploy**. It re-pulls that image tag.
It does **not** roll back the database — schema changes are manual and outside
the pipeline by design.
