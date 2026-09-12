# Octopus — EMT Identity API project

Copy-paste reference for the Octopus project. Two steps, both **Run a Script**
(Bash) against target role `emt-api`.

## Prerequisites

| Thing | Value |
|---|---|
| Environment | `Production` |
| SSH target | `159.89.246.38`, user `deploy`, key `emt_octopus`, role `emt-api` |
| Lifecycle | `EMT Release Path` — single phase, **manual** |
| Docker feed | `https://ghcr.io`, username = GitHub handle, password = PAT with `read:packages` |

## Variables

🔒 = mark **Sensitive**.

| Name | Value |
|---|---|
| 🔒 `EMT.Db.ConnectionString` | Neon **pooled** string (`-pooler` host), Npgsql format |
| 🔒 `EMT.Jwt.Key` | `openssl rand -base64 64` |
| `EMT.Jwt.Issuer` | `https://auth.employee-management-tool.com` |
| `EMT.Jwt.Audience` | `emt-api` |
| `EMT.Cors.Origin.App` | `https://app.employee-management-tool.com` |
| `EMT.Cors.Origin.Sys` | `https://sys.employee-management-tool.com` |
| `EMT.Container.Name` | `emt-identity` |
| `EMT.PublicUrl` | `https://auth.employee-management-tool.com` |
| `EMT.Ghcr.User` | GitHub username |
| 🔒 `EMT.Ghcr.Token` | PAT with `read:packages` |

Put `EMT.Jwt.Key`, `EMT.Jwt.Issuer` and `EMT.Jwt.Audience` in a **Library
Variable Set** shared with the microservice project — the microservice validates
tokens this service mints, so the three values must be byte-identical. Duplicated
secrets drift, and the failure mode (login succeeds, every subsequent call 401s)
is unpleasant to debug.

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
