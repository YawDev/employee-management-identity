# EMT Backend API — Deployment Plan (DigitalOcean + Octopus Deploy + GitHub Actions)

Solo-dev **demo** project: **one environment (Production)**, one droplet, one
Neon database, minimal spend. Step-by-step in the order you should do it. Each
phase ends with a checkpoint so you never move on with something broken behind
you.

Target end state:

```
GitHub (YawDev/employee-management-identity)
   │  merge PR to main
   ▼
GitHub Actions ──► builds + tests
   │              └─► Docker image  ──► DigitalOcean Container Registry
   ▼
Octopus Deploy ──► release created automatically, you click Deploy
   │
   └──SSH──► DigitalOcean Droplet (Ubuntu + Docker + Caddy)
                │                              │
                ├─ caddy    (TLS, :443)        └──TLS──► Neon (free tier)
                └─ emt-api  (127.0.0.1:5000)                └─ emt_identity
```

---

## Phase 0 — Decisions and costs (read first, 10 min)

### 0.1 The architecture choice

DigitalOcean gives you three ways to host a .NET API:

| Option | Cost | Octopus fit | Verdict |
|---|---|---|---|
| **Droplet + Docker** | ~$6–12/mo | Native — Octopus has first-class SSH targets | **Use this** |
| App Platform (PaaS) | ~$12/mo | Poor — no Octopus step, you'd shell out to `doctl` | Skip |
| DOKS (managed k8s) | ~$36/mo+ | Excellent, but heavy | Later, if you outgrow the droplet |

Droplet + Docker is chosen below. Octopus talks to Linux over SSH with no agent
install, which is the whole reason this combination is low-friction.

### 0.2 Running cost estimate

| Item | Monthly |
|---|---|
| Droplet, 1 GB / 1 vCPU (one API container + Caddy) | ~$6 |
| **Neon PostgreSQL, free tier** | **$0** |
| Container Registry, Starter tier | $0 |
| Domain (`.com`) | ~$1/mo (~$11/yr) |
| **Octopus Deploy Cloud, Free tier** | **$0** |
| **Total** | **~$7/mo** |

1 GB is enough because **the droplet never builds anything** — GitHub Actions
builds the image, the droplet only pulls and runs it. An idle ASP.NET container
sits around 100 MB, Caddy around 20 MB. Move to 2 GB ($12) when you add the
microservice container alongside it — that's a second container on the *same*
box, not a second bill.

Nothing here bills per service. The droplet bills per VM, Neon per project,
the registry per account. Adding the microservice adds ~$6, once.

**Octopus's free tier is free forever, not a trial** — 10 projects, 10 machines,
10 tenants, 10 users, community support. This plan uses 1 project and 1 machine,
so you're at 10% of the free ceiling and have room for the microservice, the
frontend, and several more environments before you'd ever hit a paywall.

The next tier up (Professional, ~$4.3k/yr Cloud) is priced for companies, so the
practical rule is: **stay inside the free limits.** The things that would push
you out are more than 10 deployment targets or more than 10 projects — neither
is remotely in reach for this project.

Prices drift; verify at [octopus.com/pricing](https://octopus.com/pricing) if
it's been a while.

### 0.3 One environment — what that changes

No staging means the usual "catch it in staging" safety net doesn't exist. Three
things replace it, and all three are built into the phases below:

1. **PR checks** — build + tests must pass before anything can merge (Phase 7.2).
2. **A manual deploy click** — CI creates the Octopus release automatically, but
   *you* press Deploy (Phase 5.6). This is the human gate that staging would
   otherwise have been.
3. **A health-check step that fails the deploy** and a one-click rollback to the
   previous image (Phase 5.7 step 2, Phase 8).

The one thing none of that protects is **a bad schema change**, because rolling
back the image doesn't roll back the schema. Since schema changes are applied by
hand here (§1.5), the discipline in Phase 7.4 is the whole safety net — read it
before your first one. **Neon branching makes rehearsing them nearly free**
(§7.4 rule 4).

### 0.4 One database, on Neon

The identity service currently maps the business-domain tables itself
(`EmployeeManagementDbContext` has `DbSet`s for `Organization`, `Department`,
`Employee`, etc., marked `ExcludeFromMigrations()`). So identity and the domain
tables must live in the **same database**: one Neon project, one database
`emt_identity`, holding both the `AspNet*` tables and the domain tables.

When the org domain genuinely moves to the microservice, you split into two
databases and this plan gains a second Octopus project — not a rewrite.

**Three Neon behaviours that change how you configure the app**, all covered in
Phase 3.7:

1. **Two endpoints.** Neon gives you a *direct* host and a *pooled* host (the
   one with `-pooler` in it). The app uses pooled; migrations and `psql` use
   direct. Getting this backwards causes intermittent, hard-to-read failures.
2. **Scale to zero.** The free-tier compute suspends after a few minutes idle
   and cold-starts on the next connection. Harmless for a demo, but it means
   Npgsql's connection pool needs tuning or it'll hand out dead connections.
3. **No IP allowlist on the free tier.** The DO Managed Postgres plan relied on
   "Trusted Sources" to close the database to everything but the droplet. Neon's
   free tier has no equivalent, so **the password is your entire security
   boundary**. It must live only in Octopus sensitive variables — never in
   `appsettings.json`, never in a commit.

---

## Phase 1 — Code changes required before anything can deploy

✅ **Done — 2026-08-12**, on the `deployment` branch. Recorded here so you can
see *why* each change exists when you revisit it. Files added: `Dockerfile`,
`.dockerignore`, `docker-compose.yml`, `.env.example`,
`.github/workflows/deploy.yml`. `Program.cs` gained health checks, forwarded
headers, and CORS registration.

### 1.1 Add a health endpoint

Octopus needs something to verify a deploy against, and uptime monitoring needs
it too.

```bash
dotnet add employee.management.identity/employee.management.identity.csproj package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore
```

In `Program.cs`, with the other service registrations:

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<EmployeeManagementDbContext>("database");
```

and in the pipeline, **before** `UseAuthentication`, so it stays anonymous:

```csharp
app.MapHealthChecks("/health").AllowAnonymous();
```

### 1.2 Fix HTTPS redirection behind the reverse proxy

`Program.cs:155` calls `app.UseHttpsRedirection()`. Caddy terminates TLS and
forwards plain HTTP to the container, so the app sees `http://` and redirects —
an infinite loop. Add forwarded-headers handling **before** the redirect:

```csharp
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
```

Caddy sets `X-Forwarded-Proto: https` automatically, so the redirect then
correctly no-ops. (`KnownProxies`/`KnownNetworks` default to loopback only,
which is exactly right when Caddy runs on the same host.)

### 1.3 Wire up CORS — it's configured but never registered

`appsettings.json` has a `CorsOriginSettings:DomainList` section, but `Program.cs`
never calls `AddCors` or `UseCors`. The moment your Next.js frontend calls the
deployed API from a browser, every request fails. Add:

```csharp
var corsOrigins = builder.Configuration
    .GetSection("CorsOriginSettings:DomainList").Get<string[]>()?
    .Where(o => !string.IsNullOrWhiteSpace(o)).ToArray() ?? [];

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));   // needed — refresh token is an HttpOnly cookie
```

The `Where` filter matters: `appsettings.json` ships `"DomainList": ["", ""]`,
and empty strings passed to `WithOrigins` throw at startup.

and `app.UseCors();` after `UseForwardedHeaders()`, before `UseAuthentication()`.
The origin value comes from an Octopus variable (Phase 5.5).

### 1.4 Decide about Swagger

`Program.cs:145` gates Swagger on `IsDevelopment()`, so production has no
Swagger UI. **Leave it that way** — with no staging environment the temptation
is to open it up in prod, and an unauthenticated schema dump of your auth API is
not a good trade. Use the `.http` file or Postman against prod instead.

### 1.5 Database schema — out of the pipeline (decided)

**The schema is already created and seeded in Neon, and the pipeline does not
touch the database.** The deployed container simply connects to the existing
remote database. This removes two Octopus steps, the `efbundle` build, and the
Octopus package feed entirely.

The consequence, stated plainly so it isn't a surprise later: **every future
schema change is a manual step you run yourself** before deploying the code that
depends on it. Nothing in CI or Octopus will apply a migration or a SQL script.
See §7.4 for the runbook.

### 1.6 Add the Dockerfile

At the repo root, `Dockerfile`:

```dockerfile
# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy only csproj files first so restore layers cache well
COPY employee.management.identity/employee.management.identity.csproj                             employee.management.identity/
COPY employee.management.identity.core/employee.management.identity.core.csproj                   employee.management.identity.core/
COPY employee.management.identity.infrastructure/employee.management.identity.infrastructure.csproj employee.management.identity.infrastructure/
COPY employee.management.identity.models/employee.management.identity.models.csproj               employee.management.identity.models/
COPY employee.management.identity.utility/employee.management.identity.utility.csproj             employee.management.identity.utility/
RUN dotnet restore employee.management.identity/employee.management.identity.csproj

COPY . .
RUN dotnet publish employee.management.identity/employee.management.identity.csproj \
      -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "employee.management.identity.dll"]
```

Add a `.dockerignore` at the repo root:

```
**/bin/
**/obj/
.git/
.idea/
.vscode/
tests/
```

### 1.7 Add a local compose file — your staging substitute

With no staging environment, this is where you catch things before they reach
prod. `docker-compose.yml` runs **the exact image that gets deployed**, against
the same Neon database, so a green run here means the image works.

Secrets come from a local `.env` (gitignored) — copy `.env.example` and fill in
the Neon **pooled** connection string and a JWT key.

`.env` and the Octopus variables are **independent paths to the same settings**,
not duplication: `.env` is read only by `docker compose` on your laptop, and the
Octopus variables are injected as `-e` flags on the droplet (§5.7 step 1). The
pipeline never reads `.env`; your laptop never reads Octopus.

Because it points at the real database, treat anything you do here as production
data. That's the price of not running a local Postgres; if it starts to bite,
add a `db: postgres:16` service back and point `EMT_DB_CONNECTION` at it.

### 1.8 Verify locally before you touch DigitalOcean

```bash
cp .env.example .env   # then fill it in
docker compose up --build
```

✅ **Checkpoint:** `curl http://localhost:8080/health` returns `Healthy` — which
also proves the container can reach Neon. Merge to `main` before continuing.

---

## Phase 2 — Buy the domain and point DNS at DigitalOcean

### 2.1 Buy the domain

**DigitalOcean is not a domain registrar** — it hosts DNS but does not sell
domains. Buy from one of:

- **Porkbun** — cheapest, free WHOIS privacy (recommended)
- **Cloudflare Registrar** — at-cost pricing, but you must use Cloudflare DNS
- **Namecheap** — fine, upsells at checkout

Buy something like `emt-tool.com`. You'll use:

| Host | Purpose |
|---|---|
| `api.emt-tool.com` | The API |
| `app.emt-tool.com` | Next.js frontend (later) |

Decline every add-on except free WHOIS privacy. Turn on auto-renew.

### 2.2 Delegate DNS to DigitalOcean

At the registrar, replace the nameservers with:

```
ns1.digitalocean.com
ns2.digitalocean.com
ns3.digitalocean.com
```

Propagation is usually minutes, occasionally up to 24h. Don't continue until
it's live.

### 2.3 Add the domain in DigitalOcean

DigitalOcean → **Networking → Domains** → add `emt-tool.com`. Leave the records
empty for now; you'll add the A record in Phase 3 once you have a droplet IP.

✅ **Checkpoint:** `dig NS emt-tool.com +short` returns the three DigitalOcean
nameservers.

---

## Phase 3 — DigitalOcean infrastructure

### 3.1 Install and authenticate `doctl` locally

```bash
brew install doctl
```

Create a token at **API → Tokens → Generate New Token** (name: `local-cli`,
scopes: read + write). Then:

```bash
doctl auth init
```

### 3.2 Create the Container Registry

Console → **Container Registry → Create**. Name it `emt` (registry names are
globally unique — if taken, use `emt-yawdev`). Starter tier is free and holds
one repository, which is enough for this one API.

Your image path will be `registry.digitalocean.com/emt/identity-api`.

```bash
doctl registry login
```

### 3.3 Create the Droplet

Console → **Droplets → Create**:

- **Image:** Ubuntu 24.04 LTS
- **Plan:** Basic → Regular → **1 GB / 1 vCPU**
- **Region:** geographically closest to your **Neon** region. They're different
  providers so there's no private networking either way; you're just minimising
  round-trip latency on every query. Neon `aws-us-east-1` → DO `nyc1`/`nyc3`;
  Neon `aws-eu-central-1` → DO `fra1`; Neon `aws-eu-west-2` → DO `lon1`.
- **Authentication:** **SSH key**, never password
- **Hostname:** `emt-prod-01`
- **Enable:** monitoring (backups optional — the droplet is disposable, all
  state lives in Neon)

### 3.4 Harden the droplet and install Docker

SSH in as root, then:

```bash
adduser deploy && usermod -aG sudo deploy && rsync --archive --chown=deploy:deploy ~/.ssh /home/deploy
```

Install Docker:

```bash
curl -fsSL https://get.docker.com | sh && usermod -aG docker deploy
```

Install Caddy's prerequisites:

```bash
apt-get update && apt-get install -y debian-keyring debian-archive-keyring apt-transport-https curl
```

(No `postgresql-client` needed — the droplet never talks to the database
directly. Schema changes run from your laptop, §7.4.)

```bash
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg && curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | tee /etc/apt/sources.list.d/caddy-stable.list && apt-get update && apt-get install -y caddy
```

Firewall — only SSH and web from outside:

```bash
ufw allow OpenSSH && ufw allow 80/tcp && ufw allow 443/tcp && ufw --force enable
```

Also create a **DigitalOcean Cloud Firewall** (Networking → Firewalls) with the
same rules and attach it to the droplet. Belt and braces; the cloud firewall
survives a misconfigured `ufw`.

### 3.5 Point DNS at the droplet

Networking → Domains → `emt-tool.com` → add:

| Type | Hostname | Value | TTL |
|---|---|---|---|
| A | `api` | *droplet IP* | 3600 |

### 3.6 Configure Caddy

`/etc/caddy/Caddyfile` on the droplet:

```
api.emt-tool.com {
    reverse_proxy 127.0.0.1:5000
    encode zstd gzip
    log {
        output file /var/log/caddy/api.log
    }
}
```

```bash
systemctl reload caddy
```

Caddy provisions Let's Encrypt certificates automatically on first request and
renews them forever. There is no certbot step and no renewal cron.

### 3.7 Neon — grab both connection strings

Your Neon project is already created. What you need from the dashboard is
**two** connection strings, not one.

In the Neon console → your project → **Connection Details**. There's a
**"Connection pooling"** toggle. Copy the string with it **on** and again with
it **off**:

| | Host looks like | Used by |
|---|---|---|
| **Pooled** | `ep-xyz-123-**pooler**.us-east-1.aws.neon.tech` | The running app |
| **Direct** | `ep-xyz-123.us-east-1.aws.neon.tech` | EF migrations, `psql` |

The pooled endpoint is PgBouncer in transaction mode. It's what you want for a
web app — it absorbs connection churn and cold starts. It's *not* what you want
for schema changes: transaction-pooled sessions don't reliably hold the
session-level state DDL and migration tooling expect. Use direct for anything
that changes the schema.

Convert the **pooled** one to Npgsql format — this is
`ConnectionStrings__DefaultConnection` for the app:

```
Host=ep-xyz-123-pooler.us-east-1.aws.neon.tech;Database=emt_identity;Username=YOUR_USER;Password=YOUR_PASSWORD;SSL Mode=Require;Trust Server Certificate=true;Connection Idle Lifetime=60;Connection Pruning Interval=10
```

Those last two parameters exist because of **scale-to-zero**. Neon suspends the
compute after a few minutes idle, which silently kills connections Npgsql still
believes are alive — you get a `connection reset` on the first request after
every quiet period. Pruning idle connections at 60s keeps the pool ahead of
Neon's suspend, so the demo doesn't throw an error the first time someone
touches it after lunch.

Keep the **direct** string in `psql`/libpq form for migrations:

```
postgresql://YOUR_USER:YOUR_PASSWORD@ep-xyz-123.us-east-1.aws.neon.tech/emt_identity?sslmode=require
```

> **Security note.** The DO Managed Postgres version of this plan closed the
> database to everything but the droplet via Trusted Sources. Neon's free tier
> has no IP allowlist (it's a paid feature), so **anyone with the connection
> string can reach your database from anywhere.** That's acceptable for a demo,
> but it means: the password lives *only* in Octopus sensitive variables and
> your local user-secrets, never in `appsettings.json`, never in a commit, never
> pasted into a chat or issue. If it ever leaks, rotate it in the Neon console
> immediately.

✅ **Checkpoint:** from the droplet,
`psql "postgresql://...@ep-xyz-123.us-east-1.aws.neon.tech/emt_identity?sslmode=require" -c "SELECT version();"`
returns a PostgreSQL version string.

---

## Phase 4 — Get the schema into the database (manual first run)

✅ **Done — 2026-08-12.** The Neon database is created and all tables are
seeded: the `AspNet*` tables from the EF `InitialCreate` migration, and the
eight domain tables (`Tenant`, `Organization`, `Department`, `DomainUser`,
`Employee`, `Manager`, `ReportingLine`, `RefreshToken`).

The deployed container just connects to it — nothing in the pipeline recreates
or migrates anything (§1.5).

### 4.1 Confirm the schema is what the app expects

Worth one command before you deploy, against the **direct** (non-pooler)
endpoint:

```bash
psql "postgresql://YOUR_USER:YOUR_PASSWORD@ep-xxxx.REGION.aws.neon.tech/emt_identity?sslmode=require" -c "\dt"
```

Expect 16 tables plus `__EFMigrationsHistory`.

> Your `psql` comes from Postgres.app and isn't on the default `PATH` for
> non-interactive shells. If the command isn't found:
> `export PATH="/Applications/Postgres.app/Contents/Versions/latest/bin:$PATH"`

### 4.2 Bootstrap the first tenant and admin

Skip if your seed data already includes a `"Tenant"` row and a `"DomainUser"`
with `"Role" = 'sys-admin'`. Check with:

```sql
SELECT "TenantId", "Name" FROM "Tenant";
SELECT "Email", "Role" FROM "DomainUser" WHERE "Role" = 'sys-admin';
```

If both return rows, you're done — jump to Phase 5. If not, here's why it
matters and how to fix it.

Roles in this service are **not** ASP.NET Identity roles. `AspNetRoles`,
`AspNetUserRoles` and `AspNetRoleClaims` get created by the migration as a side
effect of `AddIdentity<ApplicationUser, IdentityRole<Guid>>()`, but nothing ever
writes to them — `RoleManager` is never used. The real role is the
`"DomainUser"."Role"` column (`varchar(50)`), read by
`UserRepository.GetUserRoleAsync` and stamped into the JWT as a single
`ClaimTypes.Role` claim in `TokenService.GenerateAccessToken`. So there is
nothing to seed in `AspNetRoles`; leave those three tables empty.

What *does* block a fresh database is a two-link chicken-and-egg:

1. **Registration requires a tenant that already exists.**
   `UserIdentityService.CreateUserAndIdentityAsync` rejects `TenantId <= 0` and
   then throws `"Tenant does not exist"` if the row isn't there. A fresh
   `"Tenant"` table is empty, so **nobody can register at all**.
2. **Every new user gets `RoleConstants.Default` (`"default-user"`)**, hardcoded
   at registration — and `"default-user"` satisfies **none** of the three
   policies (`sys-admin`, `company-admin`, `manager`/`employee`). The only
   endpoint that promotes a user, `PUT /sys-api/permissions/edit-role`, sits on
   `SysController`, which is `[Authorize(Policy = "SysAdmin")]`. You cannot
   reach it until a `sys-admin` already exists.

So the first admin has to be made by hand, once:

```sql
INSERT INTO "Tenant" ("Uid", "Name", "TimeZone", "CreatedAt", "UpdatedAt")
VALUES (gen_random_uuid(), 'Yawdev', 'UTC', now(), now())
RETURNING "TenantId";
```

Register through the API with that `TenantId`:

```bash
curl -X POST https://api.emt-tool.com/api/auth/register -H 'Content-Type: application/json' -d '{"userName":"jason","email":"jason@emt-tool.com","password":"Ch4nge-Me!","firstName":"Jason","lastName":"Ampah","tenantId":1}'
```

Then promote that one row directly — the only step that has to bypass the API:

```sql
UPDATE "DomainUser" SET "Role" = 'sys-admin', "UpdatedAt" = now()
WHERE "Email" = 'jason@emt-tool.com';
```

Log in again to get a fresh token (the role is baked into the JWT at issue time,
so the old token still says `default-user`). From here every other user can be
promoted through `PUT /sys-api/permissions/edit-role`.

> **Role strings are matched exactly.** The policies compare against the literal
> values in `RoleConstants` — `sys-admin`, `company-admin`, `manager`,
> `employee`. A typo or a capitalised variant in a `"DomainUser"."Role"` row
> produces a silent 403 with no log line explaining why. Copy them from
> `employee.management.identity.models/Constants/RoleConstants.cs`.

> **A `DomainUser` row is mandatory for login.** `GetUserRoleAsync` throws
> `"Role not found"` when no `"DomainUser"` matches the `IdentityUserId`. An
> `AspNetUsers` row without its `"DomainUser"` partner is a user who can never
> authenticate — worth knowing if you ever restore one table and not the other.

Note this phase runs **after** the API is reachable over HTTPS, since you
register through it. If you'd rather bootstrap before the first deploy, insert
the `Tenant` row now and do the registration in Phase 8.

✅ **Checkpoint:** 16 tables + `__EFMigrationsHistory`, one `"Tenant"` row, and
one `"DomainUser"` with `"Role" = 'sys-admin'` that can call
`GET /sys-api/get-all-users`.

---

## Phase 5 — Octopus Deploy setup

### 5.1 Create the instance

Sign up at octopus.com for **Octopus Cloud** and pick the **Free** tier. You get
a URL like `https://yawdev.octopus.app`.

Signup starts you on a 30-day Enterprise trial. When it ends you drop to Free
automatically — no card, no lapse. Just don't build anything during the trial
that depends on an Enterprise-only feature, or it'll quietly stop working on day
31. Nothing in this plan does.

### 5.2 Environment

**Infrastructure → Environments** → create one: `Production`.

That's it. Don't create a Staging environment you won't use — an unused
environment still costs you variable-scoping complexity on every variable you
add later.

### 5.3 Deployment target

**Infrastructure → Deployment Targets → Add → SSH Connection**:

- **Hostname:** droplet IP
- **Port:** 22
- **Account:** create an **SSH Key Pair** account — username `deploy`, upload
  the private key whose public half is in `/home/deploy/.ssh/authorized_keys`
- **.NET platform:** self-contained Calamari (no Mono needed)
- **Environments:** `Production`
- **Target roles:** `emt-api`

Click **Save and Test** — it must go green before you continue.

### 5.4 Docker feed for DOCR

**Library → External Feeds → Add Feed**:

- **Type:** Docker Container Registry
- **URL:** `https://registry.digitalocean.com`
- **Username:** a DigitalOcean API token (read scope is enough)
- **Password:** the same token

DigitalOcean registry auth uses the API token as both username and password.

### 5.5 Project and variables

**Projects → Add Project** → `EMT Identity API`.

Under **Variables**, add (⚠️ mark the first **two** as **Sensitive**). With one
environment, none of these need scoping — leave them unscoped:

| Name | Value |
|---|---|
| `EMT.Db.ConnectionString` | Npgsql string, **pooled** host — used by the app |
| `EMT.Jwt.Key` | 64+ random chars |
| `EMT.Jwt.Issuer` | `https://api.emt-tool.com` |
| `EMT.Jwt.Audience` | `https://app.emt-tool.com` |
| `EMT.Cors.Origins` | `https://app.emt-tool.com` |
| `EMT.Container.Name` | `emt-api` |
| `EMT.Host.Port` | `5000` |
| `EMT.PublicUrl` | `https://api.emt-tool.com` |

Only the **pooled** connection string is needed here, because the pipeline never
touches the schema (§1.5) — the direct endpoint is only for the manual migration
runbook in §7.4. Mark it **Sensitive**: on Neon's free tier the password is the
only thing protecting the database (§3.7).

Generate the JWT key with:

```bash
openssl rand -base64 64
```

### 5.6 Lifecycle

**Library → Lifecycles → Add**: `EMT Release Path`

- Single phase: `Production`, **manual**.

Manual is deliberate. CI will create a release on every merge to `main`, but the
deploy waits for you to click. With no staging environment, that click is your
only human checkpoint before production — see §0.3. If you later decide you want
merge-to-deploy, flip this phase to automatic and add `deploy_to: 'Production'`
to the workflow in Phase 6.

Set the project's lifecycle to this.

### 5.7 Deployment process — two steps

The database is out of the pipeline (§1.5), so a deploy is just: swap the
container, prove it's healthy.

**Step 1 — Deploy the container** (Run a Script, role `emt-api`, Bash).
Reference the DOCR image `identity-api` from the Docker feed so Octopus tracks
the version and can roll back to a specific tag.

```bash
IMAGE="registry.digitalocean.com/emt/identity-api:$(get_octopusvariable 'Octopus.Action.Package[identity-api].PackageVersion')"
NAME="$(get_octopusvariable 'EMT.Container.Name')"
PORT="$(get_octopusvariable 'EMT.Host.Port')"

echo "$DOCR_TOKEN" | docker login registry.digitalocean.com -u "$DOCR_TOKEN" --password-stdin
docker pull "$IMAGE"
docker rm -f "$NAME" 2>/dev/null || true

docker run -d --name "$NAME" --restart unless-stopped \
  -p 127.0.0.1:"$PORT":8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection="$(get_octopusvariable 'EMT.Db.ConnectionString')" \
  -e Jwt__Key="$(get_octopusvariable 'EMT.Jwt.Key')" \
  -e Jwt__Issuer="$(get_octopusvariable 'EMT.Jwt.Issuer')" \
  -e Jwt__Audience="$(get_octopusvariable 'EMT.Jwt.Audience')" \
  -e CorsOriginSettings__DomainList__0="$(get_octopusvariable 'EMT.Cors.Origins')" \
  "$IMAGE"

docker image prune -af --filter "until=168h"
```

Note `-p 127.0.0.1:PORT:8080` — the container binds to loopback only, so the
API is reachable **exclusively** through Caddy. Never `-p 5000:8080`; that
exposes the container publicly on plain HTTP.

`__` (double underscore) is how .NET maps env vars to nested config keys, which
is why `Jwt__Key` lands in `Configuration["Jwt:Key"]`. The `prune` keeps a 1 GB
droplet from filling up with old images.

**Step 2 — Health check** (Run a Script, role `emt-api`, Bash):

```bash
URL="$(get_octopusvariable 'EMT.PublicUrl')/health"
for i in $(seq 1 30); do
  if curl -fsS "$URL" | grep -q Healthy; then echo "Healthy after $((i*2))s"; exit 0; fi
  sleep 2
done
echo "Health check failed after 60s"; docker logs --tail 50 "$(get_octopusvariable 'EMT.Container.Name')"; exit 1
```

Failing the deploy on a bad health check is what makes the pipeline trustworthy —
without it, a broken container deploys "successfully". With one environment this
step matters more, not less.

The 60s window also absorbs a Neon cold start: `AddDbContextCheck` opens a real
connection, and if the compute has been suspended the first attempt wakes it.
Expect the first check after a quiet period to take a second or two longer.

✅ **Checkpoint:** the process has 2 steps, the SSH target tests green, and all
variables are set.

---

## Phase 6 — GitHub Actions

### 6.1 Repository secrets

GitHub → repo → **Settings → Secrets and variables → Actions → New secret**:

| Secret | Value |
|---|---|
| `DIGITALOCEAN_ACCESS_TOKEN` | DO API token with registry read/write |
| `OCTOPUS_API_KEY` | Octopus → your profile → API Keys |
| `OCTOPUS_URL` | `https://yawdev.octopus.app` |
| `OCTOPUS_SPACE` | `Default` |

### 6.2 The workflow

`.github/workflows/deploy.yml`:

```yaml
name: Build and Release

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  workflow_dispatch:

env:
  DOTNET_VERSION: '9.0.x'
  REGISTRY: registry.digitalocean.com/emt
  IMAGE_NAME: identity-api

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release
      - run: dotnet test --no-build -c Release --verbosity normal

  publish:
    needs: build-test
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Set version
        run: echo "VERSION=1.0.${{ github.run_number }}" >> $GITHUB_ENV

      # --- Docker image -> DigitalOcean Container Registry ---
      - uses: digitalocean/action-doctl@v2
        with:
          token: ${{ secrets.DIGITALOCEAN_ACCESS_TOKEN }}
      - run: doctl registry login --expiry-seconds 1200
      - name: Build and push image
        run: |
          docker build -t $REGISTRY/$IMAGE_NAME:$VERSION .
          docker push $REGISTRY/$IMAGE_NAME:$VERSION

      # --- Octopus: create the release; you click Deploy in the UI ---
      - uses: OctopusDeploy/install-octopus-cli-action@v3
      - name: Create release
        uses: OctopusDeploy/create-release-action@v3
        with:
          api_key: ${{ secrets.OCTOPUS_API_KEY }}
          server: ${{ secrets.OCTOPUS_URL }}
          space: ${{ secrets.OCTOPUS_SPACE }}
          project: 'EMT Identity API'
          release_number: ${{ env.VERSION }}
```

Note there's no `deploy_to:` — the release is created, you deploy it from the
Octopus UI. Add `deploy_to: 'Production'` if you later want merge-to-deploy.

> Octopus's GitHub Actions get major-version bumps periodically. If `@v3` errors,
> check the current major on the GitHub Marketplace rather than pinning blind.

✅ **Checkpoint:** push to `main`, watch the Actions run go green, and see a
release appear in Octopus awaiting deployment.

---

## Phase 7 — Git workflow

### 7.1 Branch model

```
main ──────────●────────────●────────────●──────►  each merge builds a release
                ╲          ╱ ╲          ╱          (you click Deploy in Octopus)
  feat/xyz       ●────────●   ╲        ╱
  fix/abc                      ●──────●            PR + green checks required
```

- `main` is always deployable. Never commit to it directly.
- Branch names: `feat/…`, `fix/…`, `chore/…`, `docs/…` — matches what you're
  already doing (`add-tests-add-get-all-users-api`, though `feat/get-all-users`
  reads better).
- Squash-merge PRs. One commit per feature on `main` makes rollback trivial:
  a bad release maps to exactly one commit.

### 7.2 Protect `main`

GitHub → **Settings → Branches → Add branch ruleset** for `main`:

- ✅ Require a pull request before merging
- ✅ Require status checks to pass → select `build-test`
- ✅ Require branches to be up to date before merging
- ✅ Block force pushes
- ✅ Restrict deletions

As a solo dev, don't require approvals — you can't approve your own PR and
you'll just end up disabling the rule. Requiring green checks is the part that
actually protects you, and with no staging environment it's the *only* automated
gate between your keyboard and production.

### 7.3 The day-to-day loop

```bash
git switch main && git pull && git switch -c feat/refresh-endpoint
```

Work, then verify against the real image before you even open the PR:

```bash
docker compose up --build
```

Then:

```bash
git push -u origin feat/refresh-endpoint && gh pr create --fill
```

Checks run → merge → GitHub Actions builds and pushes a release → you click
**Deploy** in Octopus → verify `https://api.emt-tool.com/health`.

### 7.4 Schema changes — the manual runbook

Nothing in CI or Octopus touches the database (§1.5). So when a change needs a
schema change, **you apply it by hand, from your laptop, against the direct
(non-pooler) endpoint, before you merge the code that depends on it.**

Order matters: schema first, then deploy. Deploying code that expects a column
that isn't there yet is the one way to break production with this setup.

```bash
export ConnectionStrings__DefaultConnection="Host=ep-xxxx.REGION.aws.neon.tech;Database=emt_identity;Username=USER;Password=PASSWORD;SSL Mode=Require;Trust Server Certificate=true"

# EF-owned tables (AspNet*)
dotnet ef database update --project employee.management.identity.infrastructure --startup-project employee.management.identity

# Domain tables — script-owned, not in EF migrations
psql "postgresql://USER:PASSWORD@ep-xxxx.REGION.aws.neon.tech/emt_identity?sslmode=require" -v ON_ERROR_STOP=1 -f your-change.sql
```

⚠️ Use the **direct** host, never the `-pooler` one. Running DDL through
PgBouncer's transaction pooling is the classic Neon footgun — it fails
intermittently rather than cleanly, which is the worst way for it to fail.

The rules that still apply:

1. **Never edit a migration that has already been applied.** Add a new one.
2. **Expand/contract for breaking changes.** Renaming a column = add new column
   → deploy → backfill → switch code → deploy → drop old column. Three releases,
   zero downtime, every intermediate state is safe.
3. **Read the generated SQL before merging:**

```bash
dotnet ef migrations script --idempotent --project employee.management.identity.infrastructure --startup-project employee.management.identity
```

4. **Rehearse risky migrations on a Neon branch.** This is the one place where
   the free tier beats the $15 managed database outright. A Neon branch is a
   copy-on-write clone of production data, created in seconds and costing
   nothing:

   ```bash
   neonctl branches create --name migration-test
   ```

   Point `dotnet ef database update` at the branch's direct connection string,
   confirm the migration does what you expect **against real production-shaped
   data**, then:

   ```bash
   neonctl branches delete migration-test
   ```

   Anything that drops or alters a column with data in it earns this. It takes
   about two minutes, so there's no excuse to skip it.

5. **Know your restore window before you need it.** Neon's free tier keeps a
   limited history-retention window for point-in-time restore — check the
   current figure in your project settings, because it's much shorter than the
   daily backups a managed cluster would give you. For anything you'd genuinely
   miss, take your own dump first:

   ```bash
   pg_dump "postgresql://USER:PASSWORD@ep-xyz-123.us-east-1.aws.neon.tech/emt_identity?sslmode=require" -Fc -f "emt-$(date +%Y%m%d).dump"
   ```
6. **Domain tables are not in EF migrations.** They're `ExcludeFromMigrations()`,
   so `dotnet ef` will never touch `Tenant`, `Organization`, `Department`,
   `DomainUser`, `Employee`, `Manager`, `ReportingLine` or `RefreshToken` —
   those are plain SQL you run yourself.

---

## Phase 8 — First end-to-end deploy

1. Merge `feat/deployment-prep` to `main`.
2. Watch the Actions run: build → test → image pushed →
   release created.
3. In Octopus, open the release and click **Deploy to Production**.
4. Watch the two steps: container → health.
5. Verify:

```bash
curl -i https://api.emt-tool.com/health
```

6. Complete Phase 4.2 if you haven't — insert the `Tenant` row, register your
   user, promote it to `sys-admin`.
7. Log in and confirm `GET /sys-api/get-all-users` returns 200 with the token,
   and that the refresh-token cookie comes back on login.

### If it fails

| Symptom | Cause | Fix |
|---|---|---|
| Health check times out | Container crashed on startup | `docker logs emt-api` |
| `connection reset` on first request after idle | Neon suspended; Npgsql served a dead pooled connection | Confirm `Connection Idle Lifetime=60` is in the app connection string (§3.7) |
| Manual migration hangs or fails intermittently | `dotnet ef` pointed at the **pooled** endpoint | Use the direct (non-pooler) host (§7.4) |
| 500s referencing a missing column | Code deployed ahead of its schema change | Apply the schema change (§7.4), then redeploy |
| `password authentication failed` | Neon rotated/reset the role password | Re-copy from Neon console, update the Octopus variables |
| Redirect loop / `ERR_TOO_MANY_REDIRECTS` | `UseHttpsRedirection` without forwarded headers | Phase 1.2 |
| `JWT Key not configured` | `Jwt__Key` env var missing | Check the Octopus variable name |
| Caddy serves 502 | Container bound to wrong port | Confirm `-p 127.0.0.1:5000:8080` matches Caddyfile |
| TLS cert not issued | DNS not propagated | `dig api.emt-tool.com +short` must return droplet IP |
| 403 on a `[Authorize]` endpoint | `"DomainUser"."Role"` doesn't match `RoleConstants` | Phase 4.2 |
| `Role not found` on login | No `"DomainUser"` row for that identity user | Phase 4.2 |

**Rollback:** in Octopus, open the previous successful release → **Deploy**. It
re-pulls the old image tag. This does **not** roll back migrations — which is
exactly why §7.4 matters.

---

## Phase 9 — After it works

Do these within the first week, not "eventually". With a single environment and
a single droplet, these *are* your resilience story:

1. **Verify a database restore.** Take a `pg_dump` (§7.4 rule 5), restore it
   into the compose Postgres, and confirm the data is there. An untested backup
   isn't a backup — and on the free tier this dump *is* your backup, since the
   built-in retention window is short.
2. **Uptime monitoring.** Point a free monitor (UptimeRobot / Better Stack) at
   `https://api.emt-tool.com/health`, alerting to email. You have no staging
   canary, so external monitoring is how you find out first.
3. **Log rotation.** `Program.cs` already emits structured JSON when hosted. Add
   `/etc/docker/daemon.json` →
   `{"log-driver":"json-file","log-opts":{"max-size":"10m","max-file":"3"}}`,
   then `systemctl restart docker`. On a 10 GB disk this matters.
4. **Rotate the JWT signing key** and confirm you never committed one.
   `Jwt:Key` should exist only in user secrets locally and Octopus variables in
   the cloud.
5. **Unattended security upgrades:** `apt-get install -y unattended-upgrades`.
6. **If this ever stops being a demo**, the two things to buy first are Neon's
   IP allowlist (so the password stops being the only boundary, §3.7) and a
   paid tier with a real PITR window. Neither matters at demo traffic; both
   matter the moment there's data you'd be upset to lose.

---

## Appendix — repo findings behind this plan

Things I checked in `employee-management-identity` that shaped the steps above:

| Finding | Where | Impact |
|---|---|---|
| Only migration is `InitialCreate`, covering `AspNet*` tables only | `…infrastructure/Migrations/20260627163902_InitialCreate.cs` | Phase 4 needs two separate schema steps |
| All 8 domain tables use `ExcludeFromMigrations()` | `EmployeeManagementDbContext.cs:46–193` | Domain schema is script-owned, not EF-owned |
| Schema SQL lives outside any git repo | `../Employee Management Tool.postgres.sql` | Phase 1.5 — must be versioned before CI can use it |
| `ConnectionStrings` absent from `appsettings.json` | `appsettings.json` | Supplied purely by env var in hosted envs — good, keep it that way |
| Design-time factory reads `ConnectionStrings__DefaultConnection` | `EmployeeManagementDbContextFactory.cs:19` | `dotnet ef` works with zero extra config |
| `CorsOriginSettings` configured but `AddCors`/`UseCors` never called | `Program.cs` | Phase 1.3 — frontend would break on first browser call |
| `UseHttpsRedirection()` with no forwarded-headers middleware | `Program.cs:155` | Phase 1.2 — redirect loop behind Caddy |
| Swagger gated on `IsDevelopment()` | `Program.cs:145` | Stays off in prod — correct, leave it |
| Structured JSON logging already enabled when hosted | `Program.cs:36–41` | Nothing to do; log aggregation will work as-is |
| Roles live in `"DomainUser"."Role"`, not `AspNetRoles`; `RoleManager` unused | `UserRepository.cs:88`, `TokenService.cs:31` | `AspNet{Roles,UserRoles,RoleClaims}` are created but stay permanently empty — nothing to seed |
| Registration needs a pre-existing `"Tenant"` row and hardcodes `default-user` | `UserIdentityService.cs:28–32,55` | Phase 4.2 — fresh DB can't register anyone; first admin needs manual SQL |
| The only role-promotion endpoint is behind `[Authorize(Policy="SysAdmin")]` | `SysController.cs:8,29` | Phase 4.2 — first `sys-admin` must be set by direct `UPDATE` |
| Repo has no Dockerfile and no `.github/workflows` | repo root | Phases 1.6 and 6 create them |
