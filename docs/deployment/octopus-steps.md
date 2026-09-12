# Octopus — Employee-Management-Identity

The Octopus configuration for the identity service, recorded here so it is
reviewable in git rather than existing only as text pasted into a web console.
Built 2026-09-12 against Octopus Cloud (Free tier), space `Default`.

The space is shared with other projects, so everything EMT-specific is prefixed.

## Droplet prerequisites

These exist outside Octopus and must be in place first — Octopus assumes them:

- The `emt` Docker network: `docker network create emt`
- Caddy running from `~/emt/caddy` (source: `employee-management-microservice/deploy/caddy/`),
  holding TLS for `auth.` and `api.` and proxying to containers **by name**
- The `deploy` user with Docker access (created by `deploy/bootstrap.sh`)

Caddy is the only container bound to host ports. The API publishes none.

## Infrastructure

| Thing | Value | Where in the UI |
|---|---|---|
| Environment | `Production` | Infrastructure → Environments |
| SSH account | `emt-deploy`, username `deploy`, private key `~/.ssh/emt_octopus` (ED25519, no passphrase) | **MANAGE → Accounts** |
| Deployment target | `emt-prod-01` — `159.89.246.38:22`, platform `linux-x64`, self-contained Calamari | Infrastructure → Deployment Targets → **Linux → SSH Connection** |
| Target tag | `emt-identity` | on the target |
| Docker feed | `ghcr`, URL `https://ghcr.io`, username = GitHub handle, password = PAT with `read:packages` | MANAGE → External Feeds |
| Lifecycle | `EMT Release Path` — one phase `Production`, **manual** | MANAGE → Lifecycles |

Notes from building it:

- **Use the reserved IP `159.89.246.38`**, not the droplet's own address. The
  droplet can be rebuilt; the reserved IP survives and nothing needs reconfiguring.
- **Verify the host fingerprint** rather than accepting blindly. Get the real
  values with `ssh-keyscan 159.89.246.38 | ssh-keygen -lf -`.
- **Target tags name the machine's role, not the app's environment.** `emt-identity`
  rather than `emt-identity-prod` — environment is already a separate dimension,
  and merging them means duplicated steps the moment a second environment exists.
  The microservice will add `emt-microservice` to this *same* target.
- **The lifecycle phase must be manual.** With no staging environment, that click
  is the only human gate before production.

## Variables

🔒 = **Sensitive**. Leave every scope blank — with one environment and one target,
scoping can only cause a variable to silently resolve *empty*, which surfaces as
a baffling runtime error rather than a config one.

### Library variable set `EMT Shared Configs`

Shared with the microservice project — identical for both services, so it lives
in one place and cannot drift.

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

`EMT.Jwt.Audience` is a logical name, deliberately **not** a frontend URL —
there are two frontends (`app.` and `sys.`) and audience identifies who the token
is *for*, which is the API surface.

### Project variables

| Name | Value |
|---|---|
| `EMT.Container.Name` | `emt-identity` |
| `EMT.PublicUrl` | `https://auth.employee-management-tool.com` |

Only what genuinely differs between the two services. Both share one Neon
database — identity maps the domain tables itself (`ExcludeFromMigrations`), so
they cannot be separated — which also means the Neon password, the only thing
guarding a free-tier database with no IP allowlist, is rotated in one place.

## Deployment process

Both steps: **Run a Script** (Development and Scripting → Script), language
**Bash** (the editor defaults to PowerShell), Execution Location *Run on each
deployment target*, Target Tags `emt-identity`, retries off.

The step header should read **"Can deploy to 1 deployment target"**. If it says
0, the tag doesn't match the target and the step will silently do nothing.

### Step 1 — Deploy identity container

Timeout 10 minutes. Add one package reference:

| Field | Value |
|---|---|
| Package feed | `ghcr` |
| Package ID | `yawdev/emt-identity-api` — ghcr rejects bare names, it needs `owner/image` |
| Name | `emt-identity-api` |
| Package Acquisition | **The package won't be downloaded** |

Two traps here:

- `Octopus.Action.Package[...]` keys on the **Name**, not the Package ID. Key it
  wrong and it resolves empty, producing a *tagless* image reference.
- Acquisition must be "won't be downloaded". The reference exists to track the
  version and enable rollback; the script performs its own authenticated pull.

Until the first image is pushed, Octopus shows *"could not be found"* on the
package. That is expected — save anyway.

```bash
set -euo pipefail

IMAGE="ghcr.io/yawdev/emt-identity-api:$(get_octopusvariable 'Octopus.Action.Package[emt-identity-api].PackageVersion')"
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

### Step 2 — Health check

Timeout 5 minutes, no package reference.

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
without it a broken container deploys "successfully". Dumping the container logs
on failure puts the cause in the Octopus task log instead of requiring an SSH
session.

The 60s window absorbs a Neon cold start: `AddDbContextCheck` opens a real
connection, and the free tier suspends the compute after idle.

## GitHub side

**Octopus API key** — avatar → Profile → My API Keys → New API Key:

- Purpose `github-actions`, expiry **1 year**
- "What is this key for?" → **An agent** (so deploys are attributed to automation,
  not to you, in the audit log)
- Permissions → **Full access**. Read-only cannot create releases, and the failure
  appears only at the last step of an otherwise green build.

**Repository secrets** — Settings → Secrets and variables → Actions:

| Secret | Value |
|---|---|
| `OCTOPUS_API_KEY` | the key above |
| `OCTOPUS_URL` | e.g. `https://yawdev.octopus.app` — include `https://`, no trailing slash |
| `OCTOPUS_SPACE` | `Default` |

No DigitalOcean token is needed: images go to ghcr.io, which authenticates with
the per-run `GITHUB_TOKEN`.

Diarise both expiry dates — the Octopus key and the ghcr PAT. When either lapses
the pipeline fails with an auth error that doesn't mention expiry.

## Rollback

Open the previous successful release → **Deploy**. It re-pulls that image tag.

It does **not** roll back the database — schema changes are manual and outside
the pipeline by design. See the migration runbook in `digitalocean-octopus.md` §7.4.

## Adding the microservice later

Most of this is reused rather than repeated:

- **Same** account, deployment target, environment, Docker feed, lifecycle, and
  `EMT Shared Configs` variable set
- **Add** the tag `emt-microservice` to the existing `emt-prod-01` target
- **New** project with its own two variables (`EMT.Container.Name` = `emt-api`,
  `EMT.PublicUrl` = `https://api.employee-management-tool.com`) and its own
  image `yawdev/emt-microservice-api`

The microservice also needs the Phase 1 work the identity service already has:
a `/health` endpoint, `UseForwardedHeaders`, a Dockerfile, and a workflow.
