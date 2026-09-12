# EMT Backend — Deployment

Solo-dev **demo** platform: one environment (Production), one droplet, one Neon
database. This describes what is actually running as of 2026-09-12, and why each
piece is the way it is.

The Octopus configuration lives in [octopus-steps.md](octopus-steps.md).

```
GitHub (YawDev/employee-management-identity)
   │  merge PR to main
   ▼
GitHub Actions ──► build + test
   │              └─► image ──► ghcr.io/yawdev/emt-identity-api
   ▼
Octopus Deploy ──► release created automatically, you click Deploy
   │
   └──SSH──► DigitalOcean Droplet  (159.89.246.38, reserved IP)
                │
                ├─ emt-caddy     :80/:443, TLS, routes by hostname
                ├─ emt-identity  (no published ports)         ──TLS──► Neon
                └─ emt-api       (microservice, not yet built)
```

Caddy is the only container bound to host ports. The APIs join the `emt` Docker
network and are reachable **only** through Caddy, by container name.

---

## Costs

| Item | Monthly |
|---|---|
| Droplet, 2 GB / 1 vCPU — Caddy + both API containers | $12 |
| Reserved IP (free while attached to a running droplet) | $0 |
| Neon PostgreSQL, free tier | $0 |
| GitHub Container Registry | $0 |
| Octopus Deploy Cloud, Free tier | $0 |
| Domain `employee-management-tool.com` (Cloudflare, at cost) | ~$0.87 |
| **Total** | **~$13** |

The droplet never builds anything — GitHub Actions builds the image, the droplet
only pulls and runs it. 2 GB rather than 1 GB because it hosts two API containers
plus Caddy, with `bootstrap.sh` adding 2 GB of swap for headroom.

**Registry choice.** DigitalOcean's free registry tier allows one repository and
500 MB, which cannot hold two services and leaves no room for the older image
tags rollback depends on. Its next tier is $5/mo for what GitHub Packages gives
free — and since Actions builds the image anyway, ghcr also removes a stored
credential by authenticating with the per-run `GITHUB_TOKEN`.

**Octopus Free is free forever, not a trial** — 10 projects, 10 targets. This
uses 1 and 1.

---

## One environment — what that changes

No staging means "catch it in staging" doesn't exist. Three things replace it:

1. **PR checks** — build + tests must pass before merge.
2. **A manual deploy click** — CI creates the release; *you* press Deploy. With
   no staging, that click is the only human gate before production.
3. **A health check that fails the deploy**, plus one-click rollback to the
   previous image tag.

What none of that protects is a **bad schema change**, because rolling back the
container doesn't roll back the database. Schema changes are applied by hand
(§ Database), so the discipline there is the whole safety net.

---

## Neon — three behaviours that shape the config

1. **Two endpoints.** A *direct* host and a *pooled* host (with `-pooler` in it).
   The app uses pooled; anything touching schema uses direct. Backwards causes
   intermittent, hard-to-read failures rather than clean ones.
2. **Scale to zero.** The free tier suspends after idle and cold-starts on the
   next connection, silently killing connections Npgsql believes are alive. Hence
   `Connection Idle Lifetime=60;Connection Pruning Interval=10` in the connection
   string — without them you get a `connection reset` on the first request after
   every quiet period.
3. **No IP allowlist on the free tier.** The password is the *entire* security
   boundary. It lives only in Octopus sensitive variables and your local `.env` —
   never in `appsettings.json`, never in a commit.

---

## Phase 1 — Application changes ✅ done

Recorded here because the reasoning isn't obvious from the diff.

- **`/health`** with `AddDbContextCheck` — what Octopus verifies a deploy against,
  and it opens a real connection so a deploy fails fast if Neon is unreachable.
  Mapped before `UseAuthentication` and marked `AllowAnonymous`.
- **`UseForwardedHeaders`** before `UseHttpsRedirection`. Caddy terminates TLS and
  forwards plain HTTP, so without this the app only ever sees `http://` and
  redirects forever. Caddy sets `X-Forwarded-Proto: https`, which makes the
  redirect correctly no-op.
- **CORS registered.** `CorsOriginSettings:DomainList` existed in config but
  `AddCors`/`UseCors` were never called — every browser call from the frontend
  would have failed. `AllowCredentials` is required because the refresh token is
  an HttpOnly cookie. Empty strings are filtered out, since the shipped config
  contains `["", ""]` and `WithOrigins("")` throws at startup.
- **Swagger stays off in production.** An unauthenticated schema dump of an auth
  API isn't a good trade for convenience.
- **`Dockerfile`** — csproj-first layer caching, non-root `$APP_UID`, port 8080.
- **`docker-compose.yml` + `.env`** — runs the exact production image locally
  against the real Neon database. With no staging environment, this is the only
  pre-production check. `.env` is gitignored and read only by compose; production
  values come from Octopus. The two paths never touch.

`Jwt:Key`, `Jwt:Issuer` and `Jwt:Audience` are read by the code but deliberately
absent from `appsettings.json` — supplied purely by environment variables. Keep
it that way.

---

## Phase 2 — Domain and DNS ✅ done

`employee-management-tool.com`, registered with **Cloudflare Registrar** (at-cost
pricing, no renewal markup, free WHOIS redaction, and registrar + DNS in one
console). Auto-renew on.

| Record | Type | Value | Proxy |
|---|---|---|---|
| `auth` | A | `159.89.246.38` | **DNS only** |
| `api` | A | `159.89.246.38` | **DNS only** |
| `sys` | A | `159.89.246.38` | **DNS only** |

**Grey cloud is required, not optional.** Caddy gets its certificate by answering
a Let's Encrypt HTTP-01 challenge on port 80. Proxied, Cloudflare terminates TLS
at its edge and talks to the origin over plain HTTP, which collides with
`UseHttpsRedirection` and leaves two systems trying to own certificates.

Proxying can be enabled later for DDoS protection — but then set SSL mode to
**Full (strict)** and let Caddy keep its own certificate.

Note `dig` returns Cloudflare edge IPs while a record is proxied; that is what
proxying *is*. Only the grey-cloud toggle makes the origin visible.

**Subdomain layout.** All three share one registrable domain so the refresh-token
cookie stays same-site and works with `SameSite=Lax` — a frontend on a different
domain would need `SameSite=None`, which Safari blocks outright.

`sys` currently resolves to the droplet but nothing serves it: that frontend is
still a stock `create-next-app` scaffold. If it ends up on Vercel or Pages it
becomes a CNAME instead.

---

## Phase 3 — Droplet ✅ done

| Resource | Value |
|---|---|
| Droplet | `emt-prod-01`, Ubuntu 24.04, 2 GB / 1 vCPU, `nyc3` |
| Reserved IP | `159.89.246.38` |
| Firewall | `emt-prod-fw` — inbound 22/80/443, **outbound all** |
| SSH | `ssh -i ~/.ssh/emt_do deploy@159.89.246.38` |

**Region:** `nyc3` is the closest DigitalOcean region to Neon's `us-east-2`.
Different providers, so there's no private networking either way — this is purely
about round-trip latency on every query.

**Use the reserved IP for everything.** The droplet's own address disappears if
you ever rebuild it, taking every DNS record and the Octopus target with it. A
reserved IP detaches from a dying droplet and reattaches to a fresh one with no
reconfiguration. Free while attached; ~$4/mo if left floating, so release it if
you tear the droplet down.

The reserved IP won't appear in `ip addr` on the droplet — DigitalOcean routes it
via an anchor IP (`10.17.0.5`). That's normal, and irrelevant to Caddy, which
binds `0.0.0.0`.

**Outbound rules must stay open.** The droplet needs to reach Neon, ghcr.io, and
Let's Encrypt. A firewall with inbound rules but no outbound ones blocks all three.

### Provisioning

One script, in the microservice repo, written to serve both services:

```bash
scp deploy/bootstrap.sh root@<ip>:/root/
ssh root@<ip> 'bash /root/bootstrap.sh deploy'
```

It creates the `deploy` user, hardens SSH (disabling root and password auth),
adds 2 GB swap, installs Docker, caps container logs at 10 MB × 3, and enables
`fail2ban` and unattended security upgrades.

**No `ufw`** — deliberately. The DigitalOcean Cloud Firewall is the single place
port policy lives; a second layer drifts out of sync with the first.

> **Known bug.** The script writes the SSH hardening config *before* validating
> it, and `sshd -t` fails on a fresh Ubuntu 24.04 droplet because `/run/sshd`
> doesn't exist. Combined with socket-activated SSH, root access is revoked the
> moment the file is written — and the script then dies before installing
> Docker. Recoverable only because the `deploy` user is created first:
> `ssh deploy@<ip> 'sudo mkdir -p /run/sshd && sudo bash /root/bootstrap.sh deploy'`.
> Fix by creating `/run/sshd` before the `sshd -t` check.

### Caddy

Runs as a container from `~/emt/caddy` (source of truth:
`employee-management-microservice/deploy/caddy/`). The shared network is created
once, outside compose, so API containers can join it independently of Caddy's
lifecycle:

```bash
docker network create emt
cd ~/emt/caddy && docker compose up -d
```

The Caddyfile routes by hostname to container names — `emt-identity:8080` and
`emt-api:8080`. Certificates are obtained and renewed automatically; the
`caddy_data` volume holds them, so don't delete it casually — losing it means
re-issuing every certificate, which is what burns Let's Encrypt rate limits.

**Success before any app is deployed is a 502 with a valid certificate.** TLS
terminates, Caddy routes, nothing is listening yet.

---

## Phase 4 — Database ✅ done

The Neon database is created and seeded: the `AspNet*` tables from the EF
`InitialCreate` migration, plus the eight domain tables (`Tenant`,
`Organization`, `Department`, `DomainUser`, `Employee`, `Manager`,
`ReportingLine`, `RefreshToken`).

**Nothing in the pipeline touches the database.** No migration step, no schema
package, no Octopus package feed. The deployed container simply connects.

### Schema changes are manual

When a change needs one, apply it **by hand, before merging the code that depends
on it**, against the **direct** (non-pooler) endpoint:

```bash
export ConnectionStrings__DefaultConnection="Host=ep-xxxx.us-east-2.aws.neon.tech;Database=...;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=true"

# EF-owned tables (AspNet*)
dotnet ef database update --project employee.management.identity.infrastructure --startup-project employee.management.identity

# Domain tables — script-owned, never in EF migrations
psql "postgresql://USER:PASSWORD@ep-xxxx.us-east-2.aws.neon.tech/DB?sslmode=require" -v ON_ERROR_STOP=1 -f your-change.sql
```

Order matters: **schema first, then deploy.** Deploying code that expects a
column that isn't there is the main way to break production with this setup.

⚠️ Never run DDL through the `-pooler` host. PgBouncer's transaction pooling
fails intermittently rather than cleanly, which is the worst way for it to fail.

Rules that still apply:

1. **Never edit a migration that has already been applied.** Add a new one.
2. **Expand/contract for breaking changes.** Renaming a column = add new → deploy
   → backfill → switch code → deploy → drop old. Three releases, zero downtime.
3. **Read the generated SQL before merging:**
   `dotnet ef migrations script --idempotent --project ... --startup-project ...`
4. **Rehearse risky migrations on a Neon branch.** `neonctl branches create
   --name migration-test` gives a copy-on-write clone of production data in
   seconds, for free. Anything that drops or alters a populated column earns this.
5. **Know your restore window.** The free tier's history retention is short. For
   anything you'd miss, take your own dump: `pg_dump ... -Fc -f emt-$(date +%Y%m%d).dump`.
6. **Domain tables are not in EF migrations.** They're `ExcludeFromMigrations()`,
   so `dotnet ef` will never touch them — those are plain SQL you run yourself.

### Bootstrapping the first admin

Roles here are **not** ASP.NET Identity roles. `AspNetRoles`, `AspNetUserRoles`
and `AspNetRoleClaims` are created by the migration as a side effect of
`AddIdentity<...>()` but nothing ever writes to them — `RoleManager` is unused.
The real role is `"DomainUser"."Role"`, read by `UserRepository.GetUserRoleAsync`
and stamped into the JWT by `TokenService.GenerateAccessToken`.

A fresh database has a two-link chicken-and-egg:

1. Registration rejects `TenantId <= 0` and throws if the tenant row is missing,
   so an empty `"Tenant"` table means **nobody can register at all**.
2. Every new user gets `RoleConstants.Default` (`"default-user"`), which satisfies
   none of the policies — and the only promotion endpoint sits behind
   `[Authorize(Policy = "SysAdmin")]`.

So the first admin is made by hand, once:

```sql
INSERT INTO "Tenant" ("Uid","Name","TimeZone","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(),'Yawdev','UTC',now(),now()) RETURNING "TenantId";
```

Register through the API with that `TenantId`, then promote that row directly —
the only step that must bypass the API:

```sql
UPDATE "DomainUser" SET "Role" = 'sys-admin', "UpdatedAt" = now()
WHERE "Email" = 'you@example.com';
```

Log in again afterwards: the role is baked into the JWT at issue time, so the old
token still says `default-user`.

> Role strings are matched exactly against `RoleConstants` — `sys-admin`,
> `company-admin`, `manager`, `employee`. A typo produces a silent 403.
>
> A `"DomainUser"` row is mandatory for login. `GetUserRoleAsync` throws
> "Role not found" without one, so an `AspNetUsers` row with no partner is a user
> who can never authenticate.

---

## Phase 5 — Octopus ✅ done

See [octopus-steps.md](octopus-steps.md) for accounts, target, feed, variables
and both deploy-step scripts.

Summary: SSH target `emt-prod-01` tagged `emt-identity`, ghcr Docker feed,
lifecycle `EMT Release Path` with a single **manual** Production phase, and a
two-step process — deploy container, then health check.

---

## Phase 6 — GitHub Actions

`.github/workflows/deploy.yml`. Three repository secrets: `OCTOPUS_API_KEY`,
`OCTOPUS_URL`, `OCTOPUS_SPACE`. No registry credential — ghcr authenticates with
the per-run `GITHUB_TOKEN`, which is why the `publish` job declares
`permissions: packages: write`.

The job builds and pushes `ghcr.io/yawdev/emt-identity-api:1.0.<run_number>` plus
`:latest`, then creates an Octopus release. There is no `deploy_to:` — the
release is created, and you deploy it from the Octopus UI. Add
`deploy_to: 'Production'` if you ever want merge-to-deploy.

> Octopus's GitHub Actions get major-version bumps periodically. If `@v3` errors,
> check the current major on the Marketplace rather than pinning blind.

---

## Git workflow

- `main` is always deployable. Never commit to it directly.
- Branch names: `feat/…`, `fix/…`, `chore/…`, `docs/…`.
- Squash-merge PRs — one commit per feature makes a bad release map to exactly
  one commit.

Protect `main` (Settings → Branches → Add ruleset): require a PR, require the
`build-test` check, require branches up to date, block force pushes, restrict
deletions. As a solo dev don't require approvals — you can't approve your own PR
and you'll end up disabling the rule. Green checks are the part that protects you.

Before opening a PR, verify against the real image:

```bash
docker compose up --build && curl localhost:8080/health
```

---

## First deploy

1. Merge to `main`.
2. Watch Actions: build → test → image pushed → release created.
3. In Octopus, open the release → **Deploy to Production**.
4. Watch both steps: container, then health.
5. `curl -i https://auth.employee-management-tool.com/health`

### If it fails

| Symptom | Cause | Fix |
|---|---|---|
| `denied` / `unauthorized` pulling image | ghcr PAT expired or missing `read:packages` | Check `EMT.Ghcr.Token` |
| Image pulled but tag looks empty | `Octopus.Action.Package[...]` keyed on Package ID instead of the reference **Name** | Must be `emt-identity-api` |
| Health check times out | Container crashed on startup | Logs are already in the task output — step 2 dumps them |
| `Jwt:Key` errors / every request 500s | `Jwt__Key` empty or under 32 bytes | HMAC-SHA256 needs ≥256 bits |
| `connection reset` on first request after idle | Neon suspended; Npgsql served a dead pooled connection | Confirm `Connection Idle Lifetime=60` is in the connection string |
| `password authentication failed` | Neon rotated the role password | Re-copy, update the Octopus variable |
| Redirect loop | `UseHttpsRedirection` without forwarded headers | Phase 1 |
| Caddy 502 | Container not on the `emt` network, or named something else | `docker network inspect emt` |
| Cert not issued | Record still proxied, or DNS not resolving to the droplet | `dig auth.… +short` must return `159.89.246.38` |
| 403 on an `[Authorize]` endpoint | `"DomainUser"."Role"` doesn't match `RoleConstants` | Phase 4 |
| Step runs but does nothing | Target tag doesn't match — step header says "0 deployment targets" | Fix the tag |

**Rollback:** open the previous successful release → **Deploy**. It re-pulls that
image tag. It does **not** roll back the database.

---

## After it works

Within the first week — with one environment and one droplet, these *are* your
resilience story:

1. **Verify a restore.** Take a `pg_dump`, restore it into a local Postgres,
   confirm the data is there. An untested backup isn't a backup, and on the free
   tier this dump *is* your backup.
2. **Uptime monitoring** on `https://auth.employee-management-tool.com/health`.
   You have no staging canary, so external monitoring is how you find out first.
3. **Rotate the JWT signing key** and confirm one was never committed.
4. **Diarise the credential expiries** — the Octopus API key and the ghcr PAT.
   Both fail with auth errors that don't mention expiry.
5. **Resolve the AutoMapper advisory.** AutoMapper 15.1.0 has a known
   high-severity vulnerability (GHSA-rvv3-g6hj-g44x), and
   `AutoMapper.Collection`/`ExpressionMapping` want `<15.0.0` anyway.
6. **Convert the Octopus project to Config as Code** now that the process is
   proven — it puts the deployment process in this repo as files, making
   `octopus-steps.md` the thing itself rather than a copy that can drift.
7. **Version-control the infrastructure.** `deploy/` in the microservice repo —
   `bootstrap.sh` and the Caddy config — is still untracked. Right now the script
   that built your droplet exists only on your laptop and the droplet it built.
8. **If this stops being a demo**, buy Neon's IP allowlist (so the password stops
   being the only boundary) and a tier with a real PITR window.
