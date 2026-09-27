# Octopus — Employee-Management-Identity

The Octopus configuration for the identity service, recorded here so it is
reviewable in git rather than existing only as text pasted into a web console.
Built 2026-09-12 against Octopus Cloud (Free tier), space `Default`.

The space is shared with other projects, so everything EMT-specific is prefixed.

## Droplet prerequisites

These exist outside Octopus and must be in place first — Octopus assumes them:

- k3s, with `~/.kube/config` for the `deploy` user (the user the SSH target runs as)
- Helm 4 at `/usr/local/bin/helm`, and `python3` (stock on Ubuntu)
- The `emt` namespace
- Caddy running as a pod in `emt` (source: `employee-management-microservice/deploy/caddy/`),
  holding TLS for `auth.` and `api.` and proxying to the k3s Services **by name**

Caddy is the only pod bound to host ports. The API is reachable only through its
Service. Outbound access to `github.com` is needed: step 1 fetches the chart there.

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

### Step 1 — Deploy identity with Helm

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
  version and enable rollback; k3s pulls the image itself, with the `ghcr-pull`
  Secret the script keeps up to date.

Until the first image is pushed, Octopus shows *"could not be found"* on the
package. That is expected — save anyway.

```bash
set -euo pipefail
# kubectl/helm need this: without it they read the root-only k3s config and fail.
export KUBECONFIG=$HOME/.kube/config

RELEASE="$(get_octopusvariable 'EMT.Container.Name')"
REPO=YawDev/employee-management-identity
VERSION="$(get_octopusvariable 'Octopus.Action.Package[emt-identity-api].PackageVersion')"
[ -n "$VERSION" ] || { echo "Package version is empty — check the package reference Name"; exit 1; }

umask 077; WORK="$(mktemp -d)"; trap 'rm -rf "$WORK"' EXIT

# Chart at the release's git tag, so the chart that ships matches the code it was
# built with — and redeploying an old release redeploys its chart too.
curl -fsSL "https://github.com/$REPO/archive/refs/tags/$VERSION.tar.gz" \
  | tar -xz -C "$WORK" --strip-components=1 --wildcards '*/helm/*'
CHART="$WORK/helm"
# Stamp the release version on the chart (helm upgrade has no flag for it), so labels
# and `helm list` show the running version.
sed -i "s/^appVersion:.*/appVersion: \"$VERSION\"/" "$CHART/Chart.yaml"

# Fill each "#{Variable}" in values.prod.yaml from Octopus. Octopus's own file
# substitution is documented for package steps only, so the script does it. Values
# are JSON-quoted, so any character stays valid YAML. A missing variable fails here.
for ph in $(grep -o '"#{[^}]*}"' "$CHART/values.prod.yaml" | sort -u); do
  name="${ph#\"\#\{}"; name="${name%\}\"}"
  value="$(get_octopusvariable "$name")"
  [ -n "$value" ] || { echo "Octopus variable '$name' is empty"; exit 1; }
  PH="$ph" VAL="$value" python3 -c 'import json,os,sys; p=sys.argv[1]; s=open(p).read(); open(p,"w").write(s.replace(os.environ["PH"], json.dumps(os.environ["VAL"])))' "$CHART/values.prod.yaml"
done

# ghcr packages are private; refresh the cluster's pull credentials every deploy so
# a rotated PAT takes effect. Built in a file, so the token never appears in `ps`.
AUTH="$(printf '%s:%s' "$(get_octopusvariable 'EMT.Ghcr.User')" "$(get_octopusvariable 'EMT.Ghcr.Token')" | base64 -w0)"
printf '{"auths":{"ghcr.io":{"auth":"%s"}}}' "$AUTH" > "$WORK/docker.json"
kubectl -n emt create secret generic ghcr-pull --type=kubernetes.io/dockerconfigjson \
  --from-file=.dockerconfigjson="$WORK/docker.json" --dry-run=client -o yaml | kubectl apply -f -

# --wait: block until the new pod passes readiness. --rollback-on-failure (Helm 4's
# name for --atomic): if it doesn't within 4 minutes, go back to the previous release.
helm upgrade --install "$RELEASE" "$CHART" -n emt \
  -f "$CHART/values.prod.yaml" --set image.tag="$VERSION" \
  --wait --timeout 4m --rollback-on-failure
```

What each part is for:

- **The chart comes from GitHub at the release's tag.** Both repos are public, and
  CI tags every release with the same string as the image and the Octopus release.
  A release whose tag has no `helm/` folder (anything before 1.1.16 here, 1.0.6 for
  EMT core) fails at `curl`/`tar` — those predate k3s and can't be redeployed.
- **Secrets:** `values.prod.yaml` holds `"#{EMT.Db.ConnectionString}"` and
  `"#{EMT.Jwt.Key}"`; the loop swaps in the Octopus values. Everything else that
  isn't secret (log levels, JWT issuer/audience, CORS) is in `appsettings.Production.json`
  in the image, so `EMT.Jwt.Issuer`, `EMT.Jwt.Audience` and `EMT.Cors.Origin.*` are no
  longer read by any step and can be deleted.
- **Changed a secret?** The chart puts a checksum of the Secret on the pod, so a
  deploy with a changed value restarts the pod. Redeploying an *existing* release
  uses its variable snapshot — click **Update variables** on the release first.
- k3s removes unused images itself when the disk passes 85%, so there's no prune.

### Step 2 — Health check

Timeout 5 minutes, no package reference.

```bash
export KUBECONFIG=$HOME/.kube/config
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
kubectl -n emt logs "deployment/$NAME" --tail 50
exit 1
```

Step 1 already waits for the pod's readiness probe, but that probe
(`/health/live`) deliberately skips the database. This step checks the whole path —
DNS, Caddy, TLS, the app and Neon — through the public URL. Dumping the container logs
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

Open the previous successful release → **Deploy**. It redeploys that release's
image *and* its chart. A deploy that never becomes ready is already rolled back by
step 1 (`--rollback-on-failure`).

Instant, from the droplet: `helm rollback emt-identity -n emt` (previous revision),
or `helm history emt-identity -n emt` to pick one.

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
