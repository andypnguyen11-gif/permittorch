# PermitTorch WS5 — Integration, E2E & Deploy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Take the four parallel workstream branches (ws/pipeline, ws/api, ws/marketing, ws/dashboard), merge them serially into `main` with a full verification gate after each merge, wire the web app to the real API (mock off), seed local data, instrument analytics and error reporting, prove every critical flow with Playwright E2E against the real local stack, and deploy the whole system to Railway (API + web + Postgres) with a production checklist. This workstream is SERIAL and runs last — it owns `main` and may touch any file.

**Architecture:** Modular monolith per Architecture.md — Next.js web (marketing + dashboard) calls the ASP.NET Core API with a Clerk JWT; the API enforces market entitlement in queries against a single PostgreSQL database. WS5 adds no new architecture; it adds the connective tissue: seed data, env plumbing, docker-compose Postgres for local dev, PostHog/Sentry instrumentation, a Playwright suite at `e2e/`, Dockerfiles for both apps, and Railway project wiring (two Docker services + Postgres in one project, private networking to the DB, public domains for browser traffic).

**Tech Stack:** .NET 10 / ASP.NET Core minimal APIs / EF Core 10 / Npgsql / xUnit · Next.js 15+ App Router / TypeScript strict / Tailwind / shadcn/ui / Vitest · Node 22 / pnpm workspaces · Playwright + @clerk/testing · PostgreSQL 16 (docker-compose locally, Railway managed in prod) · Clerk · Stripe (+ Stripe CLI) · Resend · Sentry (`Sentry.AspNetCore`, `@sentry/nextjs`) · PostHog (`posthog-js`) · Railway (primary deploy for API **and** web) · Vercel (documented alternative for web only).

**Spec:** `/Users/andynguyen/Desktop/Permit Torch/docs/superpowers/plans/2026-08-19-permittorch-mvp/00-overview-and-contracts.md` (LOCKED contracts — §1 merge order, §6 API contract, §8 client, §9 env vars) · `/Users/andynguyen/Desktop/Permit Torch/Architecture.md` (§2 hosting, §5 auth flow, §8 security) · `/Users/andynguyen/Desktop/Permit Torch/Prd.md` (§41 hosting, §48–49 observability/analytics, §80–81 phases) · `/Users/andynguyen/Desktop/Permit Torch/CLAUDE.md` · `/Users/andynguyen/Desktop/Permit Torch/Tasks.md`

## Global Constraints

Copied from master §2, binding for every task below:

- .NET 10 (LTS), ASP.NET Core minimal APIs + EF Core 10 + Npgsql. Test framework: xUnit.
- Next.js 15+ (App Router), TypeScript strict, Tailwind CSS, shadcn/ui. Test framework: Vitest + @testing-library/react. E2E: Playwright.
- Node 22 LTS, pnpm workspaces.
- Commit messages: imperative, descriptive, **no PR/task references, no Claude co-author trailers** (CLAUDE.md).
- No Redis, no Elasticsearch, no microservices, no message queues (Architecture.md §9).
- All timestamps stored UTC (`timestamptz`). All money `numeric` / C# `decimal`.
- Server-side authorization always; market entitlement enforced in API queries.
- UI style: match `UI Mockup.png` — light theme, orange (#F97316-family) brand accents, sidebar nav, score-badged tables. Mockup data is illustrative only.

WS5-specific rules:

- **WS5 may create or modify ANY file** (it is serial; the file-ownership matrix no longer constrains it). Locked names/types/routes from master §3–§9 must still never be renamed.
- All repo commands run from the repo root `/Users/andynguyen/Desktop/Permit Torch` (quote the path — it contains a space). Worktrees live at `/Users/andynguyen/Desktop/pt-pipeline`, `pt-api`, `pt-marketing`, `pt-dashboard`.
- Steps marked `- [ ] **HUMAN:**` cannot be automated — a person must do them in a third-party dashboard/CLI login and paste the resulting value where instructed. The agent stops and asks when it reaches one that is not yet done.
- The **FULL GATE** (defined in Task 1) must pass after every merge and before the final commit. Never claim a merge complete without running it.

---

## Task 1: Rebase and merge ws/pipeline into main

**Files:** none created — git history only (`main` gains WS1's `apps/api/Domain/`, `Infrastructure/`, `Jobs/`, `Setup/PipelineSetup.cs`, pipeline tests).
**Interfaces:** consumes locked pipeline interfaces (master §5). Defines the reusable **FULL GATE** command block used verbatim after every merge.

- [ ] Preflight — confirm clean state and expected branches:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git status --short                 # → expect: empty output
  git branch --list 'ws/*'           # → expect: ws/api  ws/dashboard  ws/marketing  ws/pipeline
  git worktree list                  # → expect: main checkout + 4 worktrees (pt-pipeline, pt-api, pt-marketing, pt-dashboard)
  ```
- [ ] Rebase the pipeline branch onto latest main (run inside its worktree via `-C`):
  ```bash
  git -C /Users/andynguyen/Desktop/pt-pipeline rebase main
  ```
  → expect: `Successfully rebased and updated refs/heads/ws/pipeline.` If conflicts occur (should be near-zero given file ownership): inspect with `git -C /Users/andynguyen/Desktop/pt-pipeline status`, resolve preserving BOTH sides' intent (locked contract shapes win over local drift), then `git -C /Users/andynguyen/Desktop/pt-pipeline add -A && git -C /Users/andynguyen/Desktop/pt-pipeline rebase --continue`.
- [ ] Merge into main with a descriptive no-fast-forward merge commit:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git merge --no-ff ws/pipeline -m "Merge ingestion pipeline: Apify provider, normalization, dedupe, classification, scoring, source health"
  ```
- [ ] Run the **FULL GATE** (this exact block is reused in Tasks 2–4 and 18):
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  dotnet test apps/api/tests/PermitTorch.Api.Tests/PermitTorch.Api.Tests.csproj
  pnpm -r typecheck
  pnpm --dir apps/web exec vitest run --passWithNoTests
  ```
  → expect: `Passed!  - Failed: 0` (xUnit summary, all WS1 domain/infra/jobs tests green); `pnpm -r typecheck` exits 0 with no `error TS` lines; vitest prints `Test Files  N passed` (or `No test files found` accepted via `--passWithNoTests` — web tests arrive with Tasks 3–4). If the xUnit test project lives at a different path, locate it first with `ls apps/api/tests/*/ *.csproj` and use that path everywhere this gate appears.
- [ ] If the gate fails: fix on `main` directly with a focused commit (e.g. `Fix scoring options binding after pipeline merge`) — do NOT amend the merge commit. Re-run the gate until green.

## Task 2: Rebase and merge ws/api into main

**Files:** git history only (`main` gains `apps/api/Features/`, `Setup/FeaturesSetup.cs`, feature tests).
**Interfaces:** after this merge, every route in master §6 exists on `main`.

- [ ] Rebase and merge:
  ```bash
  git -C /Users/andynguyen/Desktop/pt-api rebase main
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git merge --no-ff ws/api -m "Merge API features: leads, markets, saved leads, account, billing, digests, admin endpoints"
  ```
  → expect: rebase reports success (WS2 owns `Features/` only — conflicts should not occur; resolve as in Task 1 if they do).
- [ ] Run the **FULL GATE** (Task 1 block). → expect: all WS1 + WS2 xUnit tests green, typecheck 0, vitest pass.
- [ ] Sanity-compile check that WS1's `AddPipelineServices` and WS2's `AddFeatureServices` are both invoked in `apps/api/Program.cs` (WS0 wired them):
  ```bash
  grep -n "AddPipelineServices\|AddFeatureServices" apps/api/Program.cs   # → expect: one line each
  ```

## Task 3: Rebase and merge ws/marketing into main

**Files:** git history only (`main` gains `apps/web/app/(marketing)/`, `components/marketing/`, `lib/seo.ts`, `app/sitemap.ts`, `app/robots.ts`, marketing tests).
**Interfaces:** marketing routes `/`, `/pricing`, `/how-it-works`, `/locations/[state]/[city]`, sitemap/robots now on `main`.

- [ ] Rebase and merge:
  ```bash
  git -C /Users/andynguyen/Desktop/pt-marketing rebase main
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git merge --no-ff ws/marketing -m "Merge marketing site: public pages, SEO infrastructure, locations pages, sample-lead capture"
  ```
- [ ] Run the **FULL GATE**. → expect: all suites green; vitest now runs WS3's `__tests__/marketing/` suites.

## Task 4: Rebase and merge ws/dashboard into main

**Files:** git history only (`main` gains `apps/web/app/app/`, `components/app/`, `lib/fixtures/`, dashboard tests). Worktree/branch cleanup at the end.
**Interfaces:** the complete MVP codebase is now on `main`; all subsequent tasks commit directly to `main`.

- [ ] Rebase and merge:
  ```bash
  git -C /Users/andynguyen/Desktop/pt-dashboard rebase main
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git merge --no-ff ws/dashboard -m "Merge app dashboard: leads table, lead detail, saved leads, account, admin UI on mock API"
  ```
- [ ] Run the **FULL GATE**. → expect: all four workstreams' suites green. Note: dashboard vitest suites run against `NEXT_PUBLIC_API_MOCK` fixtures — if `lib/api.ts` fixture imports fail to typecheck here, that is exactly what Task 5 fixes; proceed to Task 5 and re-run the gate there before calling this merge done.
- [ ] Clean up worktrees and branches (all merged):
  ```bash
  git worktree remove /Users/andynguyen/Desktop/pt-pipeline
  git worktree remove /Users/andynguyen/Desktop/pt-api
  git worktree remove /Users/andynguyen/Desktop/pt-marketing
  git worktree remove /Users/andynguyen/Desktop/pt-dashboard
  git branch -d ws/pipeline ws/api ws/marketing ws/dashboard
  git worktree list    # → expect: only the main checkout remains
  ```

## Task 5: Reconcile lib/api.ts mock branch with WS4 fixture exports

**Files:** `apps/web/lib/api.ts` (modify — WS5 now owns it), read-only: `apps/web/lib/fixtures/*`.
**Interfaces:** the exported function signatures in master §8 are LOCKED and must not change; only the internal mock-mode fixture imports/names are aligned.

- [ ] Read both sides:
  ```bash
  cat apps/web/lib/api.ts
  ls apps/web/lib/fixtures/
  grep -rn "export" apps/web/lib/fixtures/ | head -50
  ```
  WS0 wrote `api.ts` guessing fixture export names; WS4 created the actual fixtures. List every mismatch (import path, export name, shape helper).
- [ ] Align `api.ts`'s `NEXT_PUBLIC_API_MOCK === "1"` branches to the real fixture exports. Rules: keep the master §8 signatures byte-identical; fixtures must satisfy `packages/types` exactly (fix a fixture only if it violates the locked types — otherwise adapt `api.ts`); mock branches for mutating calls (`saveLead`, `updateSavedLead`, etc.) keep returning fixture-derived values, no real fetch.
- [ ] Verify and commit as ONE commit:
  ```bash
  pnpm -r typecheck && pnpm --dir apps/web exec vitest run
  git add apps/web/lib/api.ts apps/web/lib/fixtures/
  git commit -m "Align mock API client with dashboard fixture exports"
  ```
  → expect: typecheck 0 errors; all vitest suites pass (this also retroactively greens Task 4's gate — re-run the FULL GATE now and confirm).

## Task 6: Local Postgres via docker-compose and env plumbing

**Files:** `docker-compose.yml` (create, repo root), `.env.example` (create or extend, repo root), `.env` (create locally, git-ignored), `apps/web/.env.local` (create locally, git-ignored), `.gitignore` (verify `.env` and `.env.local` entries).
**Interfaces:** env var names per master §9 plus WS5 additions: `CLERK_SUPERADMIN_USER_ID`, `CLERK_SUPERADMIN_EMAIL`, `E2E_ENTITLED_CLERK_USER_ID`, `E2E_ENTITLED_EMAIL`, `E2E_UNENTITLED_CLERK_USER_ID`, `E2E_UNENTITLED_EMAIL`, `E2E_USER_PASSWORD`, `RUN_MIGRATIONS_ON_STARTUP`, `WEB_ORIGIN`, `NEXT_PUBLIC_SENTRY_DSN`.

- [ ] Create `docker-compose.yml` at repo root:
  ```yaml
  services:
    postgres:
      image: postgres:16-alpine
      container_name: permittorch-postgres
      environment:
        POSTGRES_USER: permittorch
        POSTGRES_PASSWORD: permittorch
        POSTGRES_DB: permittorch
      ports:
        - "5432:5432"
      volumes:
        - pgdata:/var/lib/postgresql/data
      healthcheck:
        test: ["CMD-SHELL", "pg_isready -U permittorch -d permittorch"]
        interval: 5s
        timeout: 3s
        retries: 10
  volumes:
    pgdata:
  ```
- [ ] Start it and confirm health:
  ```bash
  docker compose up -d && sleep 6 && docker compose ps
  ```
  → expect: `permittorch-postgres` with STATUS containing `(healthy)`.
- [ ] Determine which `DATABASE_URL` format the API consumes: `grep -n "DATABASE_URL" apps/api/Program.cs apps/api/Setup/*.cs`. If Program.cs converts URL form (Railway style `postgresql://user:pass@host:port/db`) to an Npgsql string, use URL form locally; if it passes the value straight to `UseNpgsql`, use keyword form `Host=localhost;Port=5432;Database=permittorch;Username=permittorch;Password=permittorch`. If neither conversion exists, add URL→keyword conversion in Program.cs now (Railway emits URL form; this is required for Task 15) and commit `Support URL-form DATABASE_URL connection strings`.
- [ ] Write `.env.example` at repo root — every var, dummy values, one comment each (master §9 plus WS5 additions):
  ```bash
  # --- API (apps/api) ---
  DATABASE_URL=postgresql://permittorch:permittorch@localhost:5432/permittorch
  APIFY_TOKEN=                                   # leave empty locally -> seeder inserts sample permits
  APIFY_ACTOR_ID=
  CLERK_SECRET_KEY=sk_test_replace
  CLERK_JWKS_URL=https://your-subdomain.clerk.accounts.dev/.well-known/jwks.json
  CLERK_ISSUER=https://your-subdomain.clerk.accounts.dev
  STRIPE_SECRET_KEY=sk_test_replace
  STRIPE_WEBHOOK_SECRET=whsec_replace
  STRIPE_PRICE_STARTER=price_replace
  STRIPE_PRICE_PRO=price_replace
  STRIPE_PRICE_TERRITORY=price_replace
  RESEND_API_KEY=re_replace
  EMAIL_FROM=leads@permittorch.dev
  SENTRY_DSN=
  WEB_ORIGIN=http://localhost:3000               # CORS allow-origin + Stripe redirect base
  RUN_MIGRATIONS_ON_STARTUP=true
  ASPNETCORE_URLS=http://localhost:5000
  # --- Seeder / E2E identities (Clerk user IDs, user_...) ---
  CLERK_SUPERADMIN_USER_ID=
  CLERK_SUPERADMIN_EMAIL=e2e-admin+clerk_test@permittorch.dev
  E2E_ENTITLED_CLERK_USER_ID=
  E2E_ENTITLED_EMAIL=e2e-entitled+clerk_test@permittorch.dev
  E2E_UNENTITLED_CLERK_USER_ID=
  E2E_UNENTITLED_EMAIL=e2e-unentitled+clerk_test@permittorch.dev
  E2E_USER_PASSWORD=
  # --- Web (apps/web/.env.local mirrors the NEXT_PUBLIC_* + CLERK_SECRET_KEY subset) ---
  NEXT_PUBLIC_API_URL=http://localhost:5000
  NEXT_PUBLIC_API_MOCK=                          # UNSET from WS5 onward (real API)
  NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY=pk_test_replace
  CLERK_PUBLISHABLE_KEY=pk_test_replace          # same value; read by @clerk/testing
  NEXT_PUBLIC_POSTHOG_KEY=
  NEXT_PUBLIC_SENTRY_DSN=
  ```
- [ ] `cp .env.example .env` (fill real values as Task 7 produces them) and create `apps/web/.env.local` containing exactly: `NEXT_PUBLIC_API_URL=http://localhost:5000`, `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY=<pk_test_…>`, `CLERK_SECRET_KEY=<sk_test_…>`, `NEXT_PUBLIC_POSTHOG_KEY=<phc_…>`, `NEXT_PUBLIC_SENTRY_DSN=<dsn>` — and **no** `NEXT_PUBLIC_API_MOCK` line (mock is now permanently off for local dev).
- [ ] Verify `.gitignore` covers `.env` and `.env.local` (`grep -n "^\.env" .gitignore apps/web/.gitignore 2>/dev/null`); add entries if missing.
- [ ] Commit: `git add docker-compose.yml .env.example .gitignore && git commit -m "Add local Postgres compose file and environment template"`.
- [ ] Convention for every later step that runs the API or a script needing secrets: load the env file with
  ```bash
  set -a; source .env; set +a
  ```
  (referred to below as **LOAD ENV**).

## Task 7: HUMAN — third-party service setup (Clerk, Stripe, Resend, Sentry, PostHog)

**Files:** none in repo — values land in `.env` and `apps/web/.env.local`.
**Interfaces:** produces every credential in master §9. Clerk dev-instance value formats: `CLERK_ISSUER = https://<subdomain>.clerk.accounts.dev` and `CLERK_JWKS_URL = https://<subdomain>.clerk.accounts.dev/.well-known/jwks.json`, where `<subdomain>` is shown as the "Frontend API URL" in the Clerk dashboard (it is also recoverable by base64-decoding the tail of the `pk_test_…` key). Production Clerk instances instead use `https://clerk.<your-domain>`.

- [ ] **HUMAN:** Create the Clerk application. Go to https://dashboard.clerk.com → **Create application** → name `PermitTorch` → enable **Email** sign-in with **Password** (and keep email verification code on). On the **Configure → API keys** page copy: Publishable key (`pk_test_…`) → `.env` `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY` + `CLERK_PUBLISHABLE_KEY` + `apps/web/.env.local`; Secret key (`sk_test_…`) → `.env` `CLERK_SECRET_KEY` + `apps/web/.env.local`; the **Frontend API URL** (e.g. `https://relaxed-mole-42.clerk.accounts.dev`) → set `CLERK_ISSUER` to that URL and `CLERK_JWKS_URL` to that URL + `/.well-known/jwks.json`.
- [ ] **HUMAN:** In Clerk dashboard → **Users** → **Create user**, create three users, all with the SAME password you invent and store in `.env` `E2E_USER_PASSWORD` (Clerk treats `+clerk_test` addresses as test users; OTP is always 424242):
  1. `e2e-admin+clerk_test@permittorch.dev` → open the created user, copy its User ID (`user_…`) → `.env` `CLERK_SUPERADMIN_USER_ID`.
  2. `e2e-entitled+clerk_test@permittorch.dev` → copy ID → `E2E_ENTITLED_CLERK_USER_ID`.
  3. `e2e-unentitled+clerk_test@permittorch.dev` → copy ID → `E2E_UNENTITLED_CLERK_USER_ID`.
- [ ] **HUMAN:** Stripe test-mode products. Go to https://dashboard.stripe.com (toggle **Test mode** ON) → **Product catalog** → **Add product** three times: `PermitTorch Starter` recurring monthly USD $49.00; `PermitTorch Pro` $129.00; `PermitTorch Territory` $249.00. Open each product's price and copy the `price_…` ID → `.env` `STRIPE_PRICE_STARTER` / `STRIPE_PRICE_PRO` / `STRIPE_PRICE_TERRITORY`. From **Developers → API keys** copy the test Secret key (`sk_test_…`) → `STRIPE_SECRET_KEY`.
- [ ] **HUMAN:** Stripe CLI for local webhooks. Install (`brew install stripe/stripe-cli/stripe`), run `stripe login` (opens browser, confirm pairing), then in a dedicated terminal that stays open during local dev and E2E:
  ```bash
  stripe listen --forward-to localhost:5000/api/webhooks/stripe
  ```
  Copy the printed signing secret (`whsec_…`) → `.env` `STRIPE_WEBHOOK_SECRET`.
- [ ] **HUMAN:** Resend. Go to https://resend.com → **API Keys** → **Create API key** (full access) → `.env` `RESEND_API_KEY`. Under **Domains** either verify your sending domain (add the shown DNS records, wait for "Verified") and set `EMAIL_FROM=leads@<your-domain>`, or for local-only dev set `EMAIL_FROM=onboarding@resend.dev` (Resend's sandbox sender — delivers only to your own Resend account email).
- [ ] **HUMAN:** Sentry. Go to https://sentry.io → create TWO projects in your org: platform **ASP.NET Core** named `permittorch-api` (copy DSN → `.env` `SENTRY_DSN`) and platform **Next.js** named `permittorch-web` (copy DSN → `.env` + `apps/web/.env.local` `NEXT_PUBLIC_SENTRY_DSN`).
- [ ] **HUMAN:** PostHog. Go to https://us.posthog.com → create project `PermitTorch` → **Settings → Project API key** (`phc_…`) → `.env` + `apps/web/.env.local` `NEXT_PUBLIC_POSTHOG_KEY`.
- [ ] Confirm no required `.env` value is still a `replace`/empty placeholder before starting Task 9: `grep -n "replace" .env` → expect: no output (APIFY_TOKEN, SENTRY DSNs may stay empty locally).

## Task 8: Dev seed script (DevSeeder + `dotnet run -- seed`)

**Files:** `apps/api/Data/Seed/DevSeeder.cs` (create), `apps/api/Program.cs` (modify: seed entry + startup-migration flag), `apps/api/tests/PermitTorch.Api.Tests/Data/DevSeederTests.cs` (create).
**Interfaces:** `public static Task DevSeeder.SeedAsync(AppDbContext db, IConfiguration config, CancellationToken ct = default)`; CLI entry `dotnet run --project apps/api -- seed`; deterministic IDs `Guid G(int n) => 00000000-0000-4000-8000-{n:D12}` — opportunity `G(201)` is a Dallas lead used by the entitlement E2E 404 test; startup migrations gated by `RUN_MIGRATIONS_ON_STARTUP=true`.

Decision: an EF-based C# seeder (not raw SQL) is the cleanest — it reuses the WS0 entity model and migrations, stays idempotent, and runs identically locally and against Railway. It is invoked explicitly via a `seed` CLI arg, never automatically.

- [ ] Create `apps/api/Data/Seed/DevSeeder.cs` (adjust `namespace`/usings to the project's actual root namespace — check `apps/api/Data/AppDbContext.cs`):
  ```csharp
  using System.Security.Cryptography;
  using System.Text;
  using Microsoft.EntityFrameworkCore;

  namespace PermitTorch.Api.Data.Seed;

  public static class DevSeeder
  {
      private static Guid G(int n) => Guid.Parse($"00000000-0000-4000-8000-{n:D12}");

      public static async Task SeedAsync(AppDbContext db, IConfiguration config, CancellationToken ct = default)
      {
          await db.Database.MigrateAsync(ct);

          var markets = await UpsertMarketsAsync(db, ct);
          var sources = await UpsertSourcesAsync(db, markets, ct);
          await UpsertSuperAdminAsync(db, config, ct);
          await UpsertE2EOrgsAsync(db, markets, config, ct);

          var apifyToken = config["APIFY_TOKEN"];
          if (string.IsNullOrWhiteSpace(apifyToken) && !await db.Permits.AnyAsync(ct))
              await SeedSamplePermitsAsync(db, sources, ct);

          await db.SaveChangesAsync(ct);
          Console.WriteLine(
              $"Seed complete. markets={await db.Markets.CountAsync(ct)} sources={await db.Sources.CountAsync(ct)} " +
              $"permits={await db.Permits.CountAsync(ct)} opportunities={await db.FireOpportunities.CountAsync(ct)}");
      }

      private static async Task<Dictionary<string, Market>> UpsertMarketsAsync(AppDbContext db, CancellationToken ct)
      {
          var defs = new (string Slug, string Name, string City)[]
          {
              ("houston-tx", "Houston", "Houston"),
              ("dallas-tx",  "Dallas",  "Dallas"),
              ("austin-tx",  "Austin",  "Austin"),
          };
          var result = new Dictionary<string, Market>();
          foreach (var d in defs)
          {
              var m = await db.Markets.FirstOrDefaultAsync(x => x.Slug == d.Slug, ct);
              if (m is null)
              {
                  m = new Market { Id = Guid.NewGuid(), Slug = d.Slug, Name = d.Name, City = d.City, State = "TX", Active = true };
                  db.Markets.Add(m);
              }
              m.Active = true;
              result[d.Slug] = m;
          }
          return result;
      }

      private static async Task<Dictionary<string, Source>> UpsertSourcesAsync(
          AppDbContext db, Dictionary<string, Market> markets, CancellationToken ct)
      {
          // Jurisdiction MUST match the scraper's COVERAGE_REPORT jurisdiction keys (verify against scraper README).
          var defs = new (string Jurisdiction, string MarketSlug, string Name, string PortalType, string Url)[]
          {
              ("houston-tx",       "houston-tx", "Houston Permitting Center",       "accela",  "https://www.houstonpermittingcenter.org"),
              ("harris-county-tx", "houston-tx", "Harris County Permits",           "arcgis",  "https://permits.harriscountytx.gov"),
              ("dallas-tx",        "dallas-tx",  "Dallas Development Services",     "socrata", "https://www.dallasopendata.com"),
              ("austin-tx",        "austin-tx",  "Austin Development Services",     "socrata", "https://data.austintexas.gov"),
          };
          var result = new Dictionary<string, Source>();
          foreach (var d in defs)
          {
              var market = markets[d.MarketSlug];
              var s = await db.Sources.FirstOrDefaultAsync(x => x.Jurisdiction == d.Jurisdiction, ct);
              if (s is null)
              {
                  s = new Source
                  {
                      Id = Guid.NewGuid(), MarketId = market.Id, Jurisdiction = d.Jurisdiction,
                      Name = d.Name, City = market.City, State = "TX",
                      PortalType = d.PortalType, SourceUrl = d.Url,
                      Active = true, HealthStatus = HealthStatus.Healthy, RecordsLastRun = 0,
                  };
                  db.Sources.Add(s);
              }
              result[d.Jurisdiction] = s;
          }
          return result;
      }

      private static async Task UpsertSuperAdminAsync(AppDbContext db, IConfiguration config, CancellationToken ct)
      {
          var clerkId = config["CLERK_SUPERADMIN_USER_ID"];
          if (string.IsNullOrWhiteSpace(clerkId))
          {
              Console.WriteLine("WARN: CLERK_SUPERADMIN_USER_ID not set - skipping SuperAdmin seed.");
              return;
          }
          var email = config["CLERK_SUPERADMIN_EMAIL"] ?? "admin@permittorch.dev";
          if (await db.AppUsers.AnyAsync(u => u.ClerkUserId == clerkId, ct)) return;
          var org = new Organization { Id = Guid.NewGuid(), Name = "PermitTorch (Internal)" };
          db.Organizations.Add(org);
          db.AppUsers.Add(new AppUser
          {
              Id = Guid.NewGuid(), ClerkUserId = clerkId, Email = email,
              OrganizationId = org.Id, Role = UserRole.SuperAdmin,
          });
      }

      private static async Task UpsertE2EOrgsAsync(
          AppDbContext db, Dictionary<string, Market> markets, IConfiguration config, CancellationToken ct)
      {
          // Entitled org: active Pro subscription on houston-tx only.
          var entitledId = config["E2E_ENTITLED_CLERK_USER_ID"];
          if (!string.IsNullOrWhiteSpace(entitledId) &&
              !await db.AppUsers.AnyAsync(u => u.ClerkUserId == entitledId, ct))
          {
              var org = new Organization { Id = Guid.NewGuid(), Name = "Acme Fire Protection" };
              db.Organizations.Add(org);
              db.AppUsers.Add(new AppUser
              {
                  Id = Guid.NewGuid(), ClerkUserId = entitledId,
                  Email = config["E2E_ENTITLED_EMAIL"] ?? "e2e-entitled+clerk_test@permittorch.dev",
                  OrganizationId = org.Id, Role = UserRole.Member,
              });
              var sub = new Subscription
              {
                  Id = Guid.NewGuid(), OrganizationId = org.Id,
                  StripeCustomerId = "cus_seed_entitled", StripeSubscriptionId = "sub_seed_entitled",
                  Plan = PlanTier.Pro, Status = "active",
              };
              db.Subscriptions.Add(sub);
              db.SubscriptionMarkets.Add(new SubscriptionMarket
              {
                  SubscriptionId = sub.Id, MarketId = markets["houston-tx"].Id,
              });
          }

          // Unentitled org: user exists, NO subscription row.
          var unentitledId = config["E2E_UNENTITLED_CLERK_USER_ID"];
          if (!string.IsNullOrWhiteSpace(unentitledId) &&
              !await db.AppUsers.AnyAsync(u => u.ClerkUserId == unentitledId, ct))
          {
              var org = new Organization { Id = Guid.NewGuid(), Name = "NoPlan Fire Co" };
              db.Organizations.Add(org);
              db.AppUsers.Add(new AppUser
              {
                  Id = Guid.NewGuid(), ClerkUserId = unentitledId,
                  Email = config["E2E_UNENTITLED_EMAIL"] ?? "e2e-unentitled+clerk_test@permittorch.dev",
                  OrganizationId = org.Id, Role = UserRole.Member,
              });
          }
      }

      private sealed record SeedLead(
          int N, string Jurisdiction, string Title, string? PermitType, string Address,
          FireCategory Category, int Score, decimal Confidence, string Reason,
          PermitStatusKind Status, int FiledHoursAgo, decimal? Value, int? Sqft,
          string? Contractor, (string Type, string Desc, int Weight)[] Signals);

      private static async Task SeedSamplePermitsAsync(
          AppDbContext db, Dictionary<string, Source> sources, CancellationToken ct)
      {
          var now = DateTime.UtcNow;
          var leads = new SeedLead[]
          {
              new(101, "houston-tx", "New commercial warehouse with fire sprinkler system", "Commercial New Construction",
                  "1215 Industrial Blvd", FireCategory.FireSprinkler, 94, 0.95m,
                  "New commercial construction with fire sprinkler scope detected. Permit filed 2 days ago.",
                  PermitStatusKind.Active, 48, 2_800_000m, 85_000, null,
                  new[] { ("NEW_COMMERCIAL_BUILD", "New commercial construction", 25),
                          ("FIRE_SPRINKLER_SCOPE", "Explicit sprinkler scope", 25),
                          ("PERMIT_RECENT", "Permit filed within 72 hours", 15),
                          ("HIGH_PROJECT_VALUE", "Project value over $500K", 10),
                          ("LARGE_SQUARE_FOOTAGE", "Large commercial footprint", 10),
                          ("NO_CONTRACTOR_LISTED", "No fire contractor listed", 10) }),
              new(102, "houston-tx", "Office tenant build-out - fire alarm system upgrade", "Commercial Alteration",
                  "500 Main St Ste 300", FireCategory.FireAlarm, 88, 0.9m,
                  "Fire alarm scope in an active office alteration filed yesterday.",
                  PermitStatusKind.New, 24, 750_000m, 12_000, null,
                  new[] { ("FIRE_ALARM_SCOPE", "Explicit fire alarm scope", 20),
                          ("PERMIT_RECENT", "Permit filed within 72 hours", 15),
                          ("HIGH_PROJECT_VALUE", "Project value over $500K", 10),
                          ("NO_CONTRACTOR_LISTED", "No fire contractor listed", 10) }),
              new(103, "houston-tx", "Restaurant kitchen hood suppression install", "Mechanical",
                  "8801 Westheimer Rd", FireCategory.KitchenSuppression, 76, 0.85m,
                  "Kitchen suppression system required for new restaurant hood.",
                  PermitStatusKind.Active, 120, 180_000m, 4_500, "Gulf Coast Mechanical",
                  new[] { ("FIRE_SPRINKLER_SCOPE", "Suppression scope detected", 25) }),
              new(104, "harris-county-tx", "Failed sprinkler hydrostatic inspection - correction required", "Fire Protection",
                  "2200 Aldine Bender Rd", FireCategory.ViolationCorrection, 81, 0.9m,
                  "Failed fire inspection requires corrective sprinkler work.",
                  PermitStatusKind.Failed, 72, null, null, null,
                  new[] { ("FAILED_INSPECTION", "Failed inspection on record", 20),
                          ("FIRE_SPRINKLER_SCOPE", "Sprinkler system involved", 25),
                          ("NO_CONTRACTOR_LISTED", "No fire contractor listed", 10) }),
              new(105, "houston-tx", "Annual fire inspection - storage facility", "Fire Inspection",
                  "4410 N Freeway", FireCategory.FireInspection, 55, 0.8m,
                  "Scheduled fire inspection at a large storage facility.",
                  PermitStatusKind.Inspection, 480, null, 40_000, "SecureStor Facilities",
                  new[] { ("LARGE_SQUARE_FOOTAGE", "Large commercial footprint", 10) }),
              new(106, "houston-tx", "Completed sprinkler retrofit - closed", "Fire Protection",
                  "77 Harbor Dr", FireCategory.FireSprinkler, 25, 0.9m,
                  "Older sprinkler permit now closed - low priority.",
                  PermitStatusKind.Closed, 2880, 90_000m, null, "Bayou Fire Systems",
                  new[] { ("FIRE_SPRINKLER_SCOPE", "Sprinkler scope", 25),
                          ("OLD_PERMIT", "Permit older than 90 days", -20),
                          ("CLOSED_PERMIT", "Permit closed", -30) }),
              new(107, "houston-tx", "Data center clean-agent suppression system", "Commercial New Construction",
                  "10550 Katy Fwy", FireCategory.FireSuppression, 90, 0.92m,
                  "New data center build with dedicated suppression scope, filed hours ago.",
                  PermitStatusKind.New, 3, 5_200_000m, 60_000, null,
                  new[] { ("NEW_COMMERCIAL_BUILD", "New commercial construction", 25),
                          ("FIRE_SPRINKLER_SCOPE", "Suppression scope detected", 25),
                          ("PERMIT_RECENT", "Permit filed within 72 hours", 15),
                          ("HIGH_PROJECT_VALUE", "Project value over $500K", 10) }),
              new(201, "dallas-tx", "Distribution center fire sprinkler system", "Commercial New Construction",
                  "3900 Irving Blvd", FireCategory.FireSprinkler, 92, 0.94m,
                  "New distribution center with sprinkler scope in Dallas.",
                  PermitStatusKind.Active, 36, 3_100_000m, 110_000, null,
                  new[] { ("NEW_COMMERCIAL_BUILD", "New commercial construction", 25),
                          ("FIRE_SPRINKLER_SCOPE", "Explicit sprinkler scope", 25),
                          ("PERMIT_RECENT", "Permit filed within 72 hours", 15),
                          ("LARGE_SQUARE_FOOTAGE", "Large commercial footprint", 10) }),
              new(202, "dallas-tx", "Apartment renovation - fire alarm replacement", "Multifamily Alteration",
                  "6100 Gaston Ave", FireCategory.FireAlarm, 70, 0.85m,
                  "Fire alarm replacement in a multifamily renovation.",
                  PermitStatusKind.Active, 96, 420_000m, null, null,
                  new[] { ("FIRE_ALARM_SCOPE", "Explicit fire alarm scope", 20),
                          ("NO_CONTRACTOR_LISTED", "No fire contractor listed", 10) }),
              new(301, "austin-tx", "Mixed-use tower fire sprinkler rough-in", "Commercial New Construction",
                  "98 Red River St", FireCategory.FireSprinkler, 85, 0.93m,
                  "High-rise sprinkler rough-in underway in Austin.",
                  PermitStatusKind.Active, 60, 1_900_000m, 45_000, "Capitol Fire Protection",
                  new[] { ("NEW_COMMERCIAL_BUILD", "New commercial construction", 25),
                          ("FIRE_SPRINKLER_SCOPE", "Explicit sprinkler scope", 25),
                          ("PERMIT_RECENT", "Permit filed within 72 hours", 15) }),
          };

          foreach (var l in leads)
          {
              var source = sources[l.Jurisdiction];
              var filed = now.AddHours(-l.FiledHoursAgo);
              var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                  $"{l.Address}|{l.PermitType}|{filed:yyyy-MM-dd}|{l.Title}"))).ToLowerInvariant();
              var permit = new Permit
              {
                  Id = G(1000 + l.N), SourceId = source.Id,
                  ExternalId = $"seed-{l.N}", PermitNumber = $"BP2026-{l.N:D5}",
                  PermitType = l.PermitType, Description = l.Title,
                  Status = l.Status, RawStatus = l.Status.ToString().ToUpperInvariant(),
                  Address = l.Address, City = source.City, State = "TX", Zip = null,
                  FiledDate = filed, IssuedDate = l.Status == PermitStatusKind.New ? null : filed.AddHours(12),
                  EstimatedValue = l.Value, SquareFootage = l.Sqft,
                  OwnerName = null, ContractorName = l.Contractor,
                  SourceUrl = $"{source.SourceUrl}/records/seed-{l.N}",
                  Fingerprint = fingerprint,
                  FirstSeenAt = filed, LastSeenAt = now, CreatedAt = filed, UpdatedAt = now,
              };
              db.Permits.Add(permit);
              var opp = new FireOpportunity
              {
                  Id = G(l.N), PermitId = permit.Id, Category = l.Category,
                  LeadScore = l.Score, Confidence = l.Confidence, Reason = l.Reason,
                  FirstDetectedAt = filed, LastUpdatedAt = now,
              };
              db.FireOpportunities.Add(opp);
              foreach (var (type, desc, weight) in l.Signals)
                  db.LeadSignals.Add(new LeadSignal
                  {
                      Id = Guid.NewGuid(), FireOpportunityId = opp.Id,
                      SignalType = type, Description = desc, Weight = weight,
                  });
              source.LastSuccessfulRunAt = now;
              source.LastRecordSeenAt = now;
              source.RecordsLastRun += 1;
          }
          await Task.CompletedTask;
      }
  }
  ```
  Adjust `DbSet` property names (`db.AppUsers`, `db.FireOpportunities`, `db.SubscriptionMarkets`, …) to whatever `AppDbContext` actually exposes — check the file first; do NOT rename the context's sets.
- [ ] Verify the seeded `Jurisdiction` keys against the scraper: open the scraper repo README's COVERAGE_REPORT jurisdiction key list (Architecture.md §6.1). If its Houston/Dallas/Austin keys differ from `houston-tx`/`harris-county-tx`/`dallas-tx`/`austin-tx`, change the seeder's strings to match the scraper exactly (the ingestion job joins on this value).
- [ ] Wire the CLI entry and the startup-migration flag into `apps/api/Program.cs` — insert immediately after `var app = builder.Build();`:
  ```csharp
  if (args.Contains("seed"))
  {
      using var seedScope = app.Services.CreateScope();
      await DevSeeder.SeedAsync(
          seedScope.ServiceProvider.GetRequiredService<AppDbContext>(), app.Configuration);
      return;
  }

  if (Environment.GetEnvironmentVariable("RUN_MIGRATIONS_ON_STARTUP") == "true")
  {
      using var migrateScope = app.Services.CreateScope();
      await migrateScope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
  }
  ```
- [ ] Add `apps/api/tests/PermitTorch.Api.Tests/Data/DevSeederTests.cs` (xUnit, EF InMemory or the test-DB pattern the suite already uses; note `MigrateAsync` must be skipped for InMemory — extract seeding logic so the test calls the upsert methods or guard `MigrateAsync` with `db.Database.IsRelational()`): asserts (1) two consecutive `SeedAsync` calls yield the same market/source/permit counts (idempotent), (2) `G(201)` opportunity belongs to the `dallas-tx` market, (3) no permits are seeded when `APIFY_TOKEN` is set in the test config.
- [ ] Run it for real:
  ```bash
  set -a; source .env; set +a
  dotnet run --project apps/api -- seed
  ```
  → expect final line: `Seed complete. markets=3 sources=4 permits=10 opportunities=10`. Run it AGAIN → expect identical counts (idempotent).
- [ ] Gate + commit:
  ```bash
  dotnet test apps/api/tests/PermitTorch.Api.Tests/PermitTorch.Api.Tests.csproj
  git add apps/api && git commit -m "Add idempotent dev seeder with markets, sources, sample leads, and admin identity"
  ```

## Task 9: Real wiring — run web against the live API and verify auth + entitlement

**Files:** `apps/api/Program.cs` (modify only if CORS is missing), no other repo changes — this task is verification.
**Interfaces:** consumes master §6 routes and §8 client with `NEXT_PUBLIC_API_MOCK` unset.

- [ ] Start the API (terminal 1):
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  set -a; source .env; set +a
  dotnet run --project apps/api
  ```
  → expect: `Now listening on: http://localhost:5000`. Then `curl -s http://localhost:5000/api/health` → expect `{"status":"ok"}`.
- [ ] Verify CORS: `curl -s -i -X OPTIONS http://localhost:5000/api/leads -H "Origin: http://localhost:3000" -H "Access-Control-Request-Method: GET" | grep -i access-control` → expect `Access-Control-Allow-Origin: http://localhost:3000`. If absent, add to Program.cs (before `builder.Build()`): `builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(Environment.GetEnvironmentVariable("WEB_ORIGIN") ?? "http://localhost:3000").AllowAnyHeader().AllowAnyMethod()));` and `app.UseCors();` after build; commit `Allow web origin via CORS policy driven by WEB_ORIGIN`.
- [ ] Start the web app with mock OFF (terminal 2): `pnpm --dir apps/web dev` → expect Next.js ready on `http://localhost:3000`. Confirm no `NEXT_PUBLIC_API_MOCK` in `apps/web/.env.local`.
- [ ] Verify unauthenticated market data flows from the real API: `curl -s http://localhost:5000/api/markets` → expect JSON array of 3 markets with slugs `houston-tx`, `dallas-tx`, `austin-tx`. Open `http://localhost:3000/locations/texas/houston` in a browser → expect real aggregate numbers (from `/api/markets/houston-tx/stats`), not fixture numbers.
- [ ] Verify the full auth flow end-to-end (Architecture §5): in the browser, log in at `http://localhost:3000/login` as `e2e-entitled+clerk_test@permittorch.dev` (password = `E2E_USER_PASSWORD`), land on `/app/leads` → expect the seeded Houston leads table (scores 94, 90, 88, …) and an "Updated … ago" freshness line. This proves: Clerk session → JWT minted with issuer `https://<subdomain>.clerk.accounts.dev` → API validates against `CLERK_JWKS_URL` → entitlement query returns houston-tx rows only.
- [ ] Verify entitlement scoping at the API layer directly. In the browser devtools console on `/app/leads` run `await window.Clerk.session.getToken()` and copy the JWT, then:
  ```bash
  TOKEN="<paste>"
  curl -s "http://localhost:5000/api/leads?page=1&pageSize=25" -H "Authorization: Bearer $TOKEN" | head -c 600
  # → expect: items[] containing only city "Houston" leads, total 7, freshness.lastUpdatedAt non-null
  curl -s -o /dev/null -w "%{http_code}\n" \
    "http://localhost:5000/api/leads/00000000-0000-4000-8000-000000000201" -H "Authorization: Bearer $TOKEN"
  # → expect: 404   (Dallas lead is outside the entitled market)
  ```
- [ ] Log in as `e2e-unentitled+clerk_test@permittorch.dev` → expect `/app/leads` to show the locked/empty no-subscription state (whatever WS4 built), never another org's data.

## Task 10: Analytics wrapper, PostHog init, Sentry in both apps

**Files:** `apps/web/lib/analytics.ts` (create), `apps/web/components/app/analytics-provider.tsx` (create), `apps/web/app/layout.tsx` (modify: mount provider), `apps/web/instrumentation.ts` + `apps/web/instrumentation-client.ts` + `apps/web/sentry.server.config.ts` + `apps/web/sentry.edge.config.ts` (create), `apps/web/next.config.ts` (verify only), `apps/api/Program.cs` + `apps/api/PermitTorch.Api.csproj` (modify: Sentry), `apps/web/package.json` (modify: deps), event call-site edits in WS3/WS4 components (located by grep, listed below), `apps/web/__tests__/app/analytics.test.ts` (create).
**Interfaces:** `track(event, props)` — typed event union exactly: `signup`, `pricing_viewed`, `lead_opened`, `lead_saved`, `search_performed`, `filter_changed`, `digest_enabled`, `checkout_started` (PRD §49; `subscription created` is a server-side Stripe fact and is read from Stripe/PostHog webhooksless — out of client scope).

- [ ] Install deps (WS5 may edit package.json — serial): `pnpm --dir apps/web add posthog-js @sentry/nextjs` → expect lockfile update, no peer errors.
- [ ] Create `apps/web/lib/analytics.ts` (complete file):
  ```typescript
  import posthog from "posthog-js";

  export type AnalyticsEvent =
    | "signup"
    | "pricing_viewed"
    | "lead_opened"
    | "lead_saved"
    | "search_performed"
    | "filter_changed"
    | "digest_enabled"
    | "checkout_started";

  type EventProps = {
    signup: Record<string, never>;
    pricing_viewed: Record<string, never>;
    lead_opened: { leadId: string; score: number; category: string };
    lead_saved: { leadId: string };
    search_performed: { query: string };
    filter_changed: { filter: string; value: string };
    digest_enabled: { frequency: "DAILY" | "WEEKLY" };
    checkout_started: { plan: "STARTER" | "PRO" | "TERRITORY" };
  };

  let initialized = false;

  export function initAnalytics(): void {
    if (initialized || typeof window === "undefined") return;
    const key = process.env.NEXT_PUBLIC_POSTHOG_KEY;
    if (!key) return; // analytics silently disabled without a key (local/CI)
    posthog.init(key, {
      api_host: "https://us.i.posthog.com",
      capture_pageview: true,
      capture_pageleave: true,
    });
    initialized = true;
  }

  export function identifyUser(clerkUserId: string, email?: string): void {
    if (!initialized) return;
    posthog.identify(clerkUserId, email ? { email } : undefined);
  }

  export function track<E extends AnalyticsEvent>(event: E, props: EventProps[E]): void {
    if (!initialized) return;
    posthog.capture(event, props);
  }
  ```
- [ ] Create `apps/web/components/app/analytics-provider.tsx`:
  ```tsx
  "use client";

  import { useEffect } from "react";
  import { useUser } from "@clerk/nextjs";
  import { initAnalytics, identifyUser } from "@/lib/analytics";

  export function AnalyticsProvider({ children }: { children: React.ReactNode }) {
    const { user } = useUser();
    useEffect(() => {
      initAnalytics();
    }, []);
    useEffect(() => {
      if (user) identifyUser(user.id, user.primaryEmailAddress?.emailAddress);
    }, [user]);
    return <>{children}</>;
  }
  ```
  Mount it in `apps/web/app/layout.tsx` inside the existing `<ClerkProvider>`, wrapping `{children}`. (Adjust the `@/` import alias to the project's tsconfig paths.)
- [ ] Add the eight call sites — locate each component by grep (WS3/WS4 chose the filenames), add the import + one `track(...)` call:
  1. `signup` — grep `signUp\|SignUp` under `apps/web/app/(marketing)/`; configure the signup flow's after-signup destination to `/app/leads?signup=1` (Clerk component prop `forceRedirectUrl` or env `NEXT_PUBLIC_CLERK_SIGN_UP_FORCE_REDIRECT_URL`), then in the `/app/leads` page's client shell fire `track("signup", {})` once when `searchParams` contains `signup=1` and strip the param with `router.replace`.
  2. `pricing_viewed` — `useEffect(() => { track("pricing_viewed", {}) }, [])` in a small client component rendered by `apps/web/app/(marketing)/pricing/page.tsx`.
  3. `lead_opened` — in the lead-detail client component (grep `getLead(` under `apps/web/app/app/leads/`): `track("lead_opened", { leadId, score, category })` on mount.
  4. `lead_saved` — in the save-button handler (grep `saveLead(` under `apps/web/components/app/`): after a successful save.
  5. `search_performed` — in the leads search input submit/debounce handler (grep the `q` filter param usage): `track("search_performed", { query })`.
  6. `filter_changed` — in each filter control's onChange (grep `minScore\|maxAgeDays\|category` in the filter bar component): `track("filter_changed", { filter, value: String(value) })`.
  7. `digest_enabled` — in the account/alerts digest control (grep `updateEmailPreferences(`): fire only when the new frequency is `DAILY` or `WEEKLY`.
  8. `checkout_started` — wherever `createCheckout(` is called (pricing page and/or account billing): `track("checkout_started", { plan })` immediately before `window.location.assign(url)`.
- [ ] Add `apps/web/__tests__/app/analytics.test.ts` (Vitest): mock `posthog-js`; assert `track` is a no-op before `initAnalytics`, that `initAnalytics` without `NEXT_PUBLIC_POSTHOG_KEY` never calls `posthog.init`, and that after init with a key, `track("lead_saved", { leadId: "x" })` calls `posthog.capture("lead_saved", { leadId: "x" })`.
- [ ] Sentry web — create the four files:
  ```typescript
  // apps/web/instrumentation.ts
  import * as Sentry from "@sentry/nextjs";

  export async function register() {
    if (process.env.NEXT_RUNTIME === "nodejs") await import("./sentry.server.config");
    if (process.env.NEXT_RUNTIME === "edge") await import("./sentry.edge.config");
  }

  export const onRequestError = Sentry.captureRequestError;
  ```
  ```typescript
  // apps/web/instrumentation-client.ts
  import * as Sentry from "@sentry/nextjs";

  Sentry.init({
    dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
    tracesSampleRate: 0.1,
    enabled: Boolean(process.env.NEXT_PUBLIC_SENTRY_DSN),
  });

  export const onRouterTransitionStart = Sentry.captureRouterTransitionStart;
  ```
  ```typescript
  // apps/web/sentry.server.config.ts
  import * as Sentry from "@sentry/nextjs";

  Sentry.init({
    dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
    tracesSampleRate: 0.1,
    enabled: Boolean(process.env.NEXT_PUBLIC_SENTRY_DSN),
  });
  ```
  ```typescript
  // apps/web/sentry.edge.config.ts
  import * as Sentry from "@sentry/nextjs";

  Sentry.init({
    dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
    tracesSampleRate: 0.1,
    enabled: Boolean(process.env.NEXT_PUBLIC_SENTRY_DSN),
  });
  ```
  Deliberately NOT wrapping `next.config` with `withSentryConfig` (source-map upload needs an org auth token; error capture works without it — note this as a follow-up in the Sentry project).
- [ ] Sentry API: `dotnet add apps/api/PermitTorch.Api.csproj package Sentry.AspNetCore` then in `apps/api/Program.cs` before `builder.Build()`:
  ```csharp
  if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SENTRY_DSN")))
  {
      builder.WebHost.UseSentry(o =>
      {
          o.Dsn = Environment.GetEnvironmentVariable("SENTRY_DSN");
          o.TracesSampleRate = 0.1;
      });
  }
  ```
- [ ] Gate + commit:
  ```bash
  dotnet test apps/api/tests/PermitTorch.Api.Tests/PermitTorch.Api.Tests.csproj
  pnpm -r typecheck && pnpm --dir apps/web exec vitest run
  git add -A && git commit -m "Add PostHog analytics wrapper with typed events and Sentry error reporting in web and API"
  ```

## Task 11: Playwright E2E scaffold with Clerk testing tokens

**Files:** `e2e/package.json` (create), `pnpm-workspace.yaml` (modify: add `e2e`), `e2e/playwright.config.ts` (create), `e2e/global-setup.ts` (create), `e2e/helpers/auth.ts` (create), `e2e/tests/` (dir), `e2e/.gitignore` (create: `test-results/`, `playwright-report/`).
**Interfaces:** `signIn(page, USERS.entitled | USERS.unentitled | USERS.superadmin)` helper; deterministic seed IDs from Task 8; runner command `set -a; source .env; set +a; pnpm --dir e2e exec playwright test`.

- [ ] Create `e2e/package.json`:
  ```json
  {
    "name": "e2e",
    "private": true,
    "scripts": { "test": "playwright test" },
    "devDependencies": {
      "@playwright/test": "^1.49.0",
      "@clerk/testing": "^1.4.0"
    }
  }
  ```
  Add `- e2e` to `pnpm-workspace.yaml`, then `pnpm install && pnpm --dir e2e exec playwright install chromium` → expect chromium download completes.
- [ ] Create `e2e/playwright.config.ts`:
  ```typescript
  import { defineConfig } from "@playwright/test";

  export default defineConfig({
    testDir: "./tests",
    globalSetup: "./global-setup.ts",
    timeout: 60_000,
    retries: 1,
    workers: 1, // serial: specs share one seeded database
    reporter: [["list"]],
    use: {
      baseURL: "http://localhost:3000",
      trace: "retain-on-failure",
      screenshot: "only-on-failure",
    },
    webServer: [
      {
        command:
          "bash -c 'set -a; source ../.env; set +a; ASPNETCORE_URLS=http://localhost:5000 dotnet run --project ../apps/api'",
        url: "http://localhost:5000/api/health",
        reuseExistingServer: true,
        timeout: 180_000,
      },
      {
        command: "bash -c 'pnpm --dir ../apps/web dev'",
        url: "http://localhost:3000",
        reuseExistingServer: true,
        timeout: 180_000,
      },
    ],
  });
  ```
- [ ] Create `e2e/global-setup.ts`:
  ```typescript
  import { clerkSetup } from "@clerk/testing/playwright";

  export default async function globalSetup() {
    await clerkSetup(); // reads CLERK_PUBLISHABLE_KEY + CLERK_SECRET_KEY from env
  }
  ```
- [ ] Create `e2e/helpers/auth.ts`:
  ```typescript
  import { clerk, setupClerkTestingToken } from "@clerk/testing/playwright";
  import type { Page } from "@playwright/test";

  export interface TestUser {
    email: string;
    password: string;
  }

  const password = process.env.E2E_USER_PASSWORD ?? "";

  export const USERS = {
    entitled: {
      email: process.env.E2E_ENTITLED_EMAIL ?? "e2e-entitled+clerk_test@permittorch.dev",
      password,
    },
    unentitled: {
      email: process.env.E2E_UNENTITLED_EMAIL ?? "e2e-unentitled+clerk_test@permittorch.dev",
      password,
    },
    superadmin: {
      email: process.env.CLERK_SUPERADMIN_EMAIL ?? "e2e-admin+clerk_test@permittorch.dev",
      password,
    },
  } satisfies Record<string, TestUser>;

  export async function signIn(page: Page, user: TestUser): Promise<void> {
    await setupClerkTestingToken({ page });
    await page.goto("/");
    await clerk.signIn({
      page,
      signInParams: { strategy: "password", identifier: user.email, password: user.password },
    });
  }

  export async function getApiToken(page: Page): Promise<string> {
    return page.evaluate(async () => {
      // @ts-expect-error Clerk is attached to window by @clerk/nextjs
      return (await window.Clerk.session.getToken()) as string;
    });
  }
  ```
- [ ] Ensure the database is freshly seeded before the suite: `set -a; source .env; set +a; dotnet run --project apps/api -- seed` → expect the Task 8 counts (idempotent re-run is fine).
- [ ] Smoke the scaffold with zero specs: `set -a; source .env; set +a; pnpm --dir e2e exec playwright test` → expect both webServers boot (health URL + web URL reachable) and Playwright reports no tests found (specs arrive in Tasks 12–14).
- [ ] Commit: `git add e2e pnpm-workspace.yaml pnpm-lock.yaml && git commit -m "Scaffold Playwright E2E suite with Clerk testing tokens and dual web servers"`.

## Task 12: E2E specs — marketing smoke and auth signup

**Files:** `e2e/tests/marketing.spec.ts` (create), `e2e/tests/auth.spec.ts` (create).
**Interfaces:** consumes marketing routes (Architecture §4.1) and Clerk test-mode OTP `424242` for `+clerk_test` addresses.

- [ ] First locate the sample-lead form's page: `grep -rn "submitSampleLeadRequest" apps/web/app apps/web/components` — note the route it renders on (likely `/` or a locations page). Use that route in the spec below (shown as `/` — adjust the single `goto` if WS3 placed it elsewhere) and align field labels with the actual form markup.
- [ ] Create `e2e/tests/marketing.spec.ts`:
  ```typescript
  import { test, expect } from "@playwright/test";

  test.describe("marketing smoke", () => {
    test("home renders hero and CTAs", async ({ page }) => {
      await page.goto("/");
      await expect(page).toHaveTitle(/PermitTorch/i);
      await expect(
        page.getByRole("heading", { name: /find the permits worth chasing/i })
      ).toBeVisible();
      await expect(page.getByRole("link", { name: /find leads|start free/i }).first()).toBeVisible();
    });

    test("pricing renders three tiers", async ({ page }) => {
      await page.goto("/pricing");
      await expect(page.getByText("$49")).toBeVisible();
      await expect(page.getByText("$129")).toBeVisible();
      await expect(page.getByText("$249")).toBeVisible();
    });

    test("how-it-works renders", async ({ page }) => {
      await page.goto("/how-it-works");
      await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    });

    test("locations page renders seeded Houston market with real stats", async ({ page }) => {
      await page.goto("/locations/texas/houston");
      await expect(page.getByRole("heading", { name: /houston/i }).first()).toBeVisible();
      await expect(page.getByText(/last 30 days|opportunities/i).first()).toBeVisible();
      await expect(page.getByText(/updated/i).first()).toBeVisible();
    });

    test("sample lead form submits", async ({ page }) => {
      await page.goto("/");
      const unique = Date.now();
      await page.getByLabel(/name/i).first().fill("E2E Tester");
      await page.getByLabel(/email/i).first().fill(`e2e-sample-${unique}@permittorch.dev`);
      await page.getByLabel(/company/i).first().fill("E2E Fire Co");
      await page.getByLabel(/market/i).first().selectOption({ index: 1 });
      await page.getByRole("button", { name: /get sample leads|send|request/i }).click();
      await expect(page.getByText(/on their way|check your email|received|thanks/i)).toBeVisible();
    });
  });
  ```
  Note: `POST /api/sample-leads` is rate-limited — the unique email keeps reruns from hitting the `(email, market_slug)` unique index; a 429 on rapid re-runs means the API rate limiter fired (restart the API or wait).
- [ ] Create `e2e/tests/auth.spec.ts`:
  ```typescript
  import { test, expect } from "@playwright/test";
  import { setupClerkTestingToken } from "@clerk/testing/playwright";

  test("signup lands on /app/leads", async ({ page }) => {
    await setupClerkTestingToken({ page });
    const email = `e2e-signup-${Date.now()}+clerk_test@permittorch.dev`;

    await page.goto("/signup");
    await page.getByLabel(/email/i).fill(email);
    await page.getByLabel(/^password/i).fill("E2e-Sup3r-Secret!42");
    await page.getByRole("button", { name: /continue|sign up|create account/i }).click();

    // Clerk test-mode verification code for +clerk_test addresses is always 424242
    const otp = page.locator('input[autocomplete="one-time-code"], [data-otp-input]').first();
    await otp.waitFor({ state: "visible", timeout: 15_000 });
    await otp.click();
    await page.keyboard.type("424242");

    await expect(page).toHaveURL(/\/app\/leads/, { timeout: 30_000 });
  });
  ```
- [ ] Run and stabilize: `set -a; source .env; set +a; pnpm --dir e2e exec playwright test tests/marketing.spec.ts tests/auth.spec.ts` → expect `6 passed`. Fix selector drift by reading the actual WS3 markup, not by loosening assertions to tautologies.
- [ ] Commit: `git add e2e && git commit -m "Add marketing smoke and signup E2E specs"`.

## Task 13: E2E specs — leads flow, saved flow, entitlement negative

**Files:** `e2e/tests/leads.spec.ts`, `e2e/tests/saved.spec.ts`, `e2e/tests/entitlement.spec.ts` (create).
**Interfaces:** consumes seeded data (Task 8): entitled user sees exactly 7 Houston leads; Dallas opportunity id `00000000-0000-4000-8000-000000000201`; score-signal rows render `+NN` weights.

- [ ] Create `e2e/tests/leads.spec.ts`:
  ```typescript
  import { test, expect } from "@playwright/test";
  import { signIn, USERS } from "../helpers/auth";

  test.describe("leads flow (entitled user)", () => {
    test.beforeEach(async ({ page }) => {
      await signIn(page, USERS.entitled);
      await page.goto("/app/leads");
      await expect(page.getByText(/94/).first()).toBeVisible({ timeout: 15_000 });
    });

    test("filters change results", async ({ page }) => {
      const rows = page.getByRole("row").or(page.getByTestId("lead-card"));
      const before = await rows.count();

      // Filter to Fire Alarm — seeded Houston data has exactly one alarm lead (score 88)
      await page.getByLabel(/category/i).selectOption({ label: /fire alarm/i as unknown as string });
      await expect(page.getByText("88")).toBeVisible();
      await expect(page.getByText("94")).toHaveCount(0);
      const after = await rows.count();
      expect(after).toBeLessThan(before);
    });

    test("search works", async ({ page }) => {
      await page.getByPlaceholder(/search/i).fill("warehouse");
      await page.keyboard.press("Enter");
      await expect(page.getByText(/warehouse/i).first()).toBeVisible();
      await expect(page.getByText(/restaurant kitchen hood/i)).toHaveCount(0);
    });

    test("lead detail shows score breakdown and freshness is visible", async ({ page }) => {
      await expect(page.getByText(/updated .* ago/i).first()).toBeVisible(); // freshness on the feed
      await page.getByText(/new commercial warehouse/i).first().click();
      await expect(page).toHaveURL(/\/app\/leads\/[0-9a-f-]{36}/);
      await expect(page.getByText(/why this/i).first()).toBeVisible();
      await expect(page.getByText("+25").first()).toBeVisible(); // NEW_COMMERCIAL_BUILD signal weight
      await expect(page.getByText(/new commercial construction/i).first()).toBeVisible();
      await expect(page.getByText(/houston permitting center/i)).toBeVisible(); // source block
    });
  });
  ```
  If the category filter is a shadcn `<Select>` rather than a native `<select>`, replace `selectOption` with `await page.getByLabel(/category/i).click(); await page.getByRole("option", { name: /fire alarm/i }).click();` — read the WS4 filter-bar component to pick the right interaction.
- [ ] Create `e2e/tests/saved.spec.ts`:
  ```typescript
  import { test, expect } from "@playwright/test";
  import { signIn, USERS } from "../helpers/auth";

  test("save a lead, see it in /app/saved, toggle contacted", async ({ page }) => {
    await signIn(page, USERS.entitled);

    await page.goto("/app/leads/00000000-0000-4000-8000-000000000101");
    await page.getByRole("button", { name: /save/i }).first().click();
    await expect(page.getByRole("button", { name: /saved|unsave/i }).first()).toBeVisible();

    await page.goto("/app/saved");
    await expect(page.getByText(/new commercial warehouse/i)).toBeVisible();

    await page.getByRole("button", { name: /contacted|mark contacted/i }).first().click();
    await page.reload();
    await expect(page.getByText(/contacted/i).first()).toBeVisible(); // status persisted via PATCH
  });
  ```
- [ ] Create `e2e/tests/entitlement.spec.ts`:
  ```typescript
  import { test, expect } from "@playwright/test";
  import { signIn, getApiToken, USERS } from "../helpers/auth";

  const DALLAS_LEAD_ID = "00000000-0000-4000-8000-000000000201";
  const API = "http://localhost:5000";

  test.describe("market entitlement", () => {
    test("user without a subscription sees the locked/empty state", async ({ page }) => {
      await signIn(page, USERS.unentitled);
      await page.goto("/app/leads");
      await expect(
        page.getByText(/no market access|choose a plan|subscribe|unlock/i).first()
      ).toBeVisible({ timeout: 15_000 });
      await expect(page.getByText(/new commercial warehouse/i)).toHaveCount(0);
    });

    test("direct API GET of a non-entitled lead returns 404", async ({ page }) => {
      await signIn(page, USERS.entitled); // entitled to houston-tx ONLY
      await page.goto("/app/leads");
      const token = await getApiToken(page);
      const res = await page.request.get(`${API}/api/leads/${DALLAS_LEAD_ID}`, {
        headers: { Authorization: `Bearer ${token}` },
      });
      expect(res.status()).toBe(404); // master §6: 404 outside entitled markets, never 403 leaking existence
    });

    test("leads feed never contains other-market rows", async ({ page }) => {
      await signIn(page, USERS.entitled);
      await page.goto("/app/leads");
      const token = await getApiToken(page);
      const res = await page.request.get(`${API}/api/leads?page=1&pageSize=100`, {
        headers: { Authorization: `Bearer ${token}` },
      });
      const body = (await res.json()) as { items: { city: string }[]; total: number };
      expect(body.total).toBe(7);
      expect(body.items.every((i) => i.city === "Houston")).toBe(true);
    });
  });
  ```
  Align the locked-state regex with WS4's actual empty/locked component copy (read `apps/web/app/app/leads/` before running; tighten the regex to the real string).
- [ ] Run: `set -a; source .env; set +a; pnpm --dir e2e exec playwright test tests/leads.spec.ts tests/saved.spec.ts tests/entitlement.spec.ts` → expect `7 passed`. Note saved-state leakage between runs: `saved.spec.ts` must pass on re-run — if the save button already shows "Saved", have the spec unsave first (add a conditional click) rather than reseeding.
- [ ] Commit: `git add e2e && git commit -m "Add leads, saved-lead, and entitlement E2E specs"`.

## Task 14: E2E specs — billing checkout and admin gating

**Files:** `e2e/tests/billing.spec.ts`, `e2e/tests/admin.spec.ts` (create).
**Interfaces:** `POST /api/billing/checkout` → `{ url }` on `checkout.stripe.com`; webhook simulation via Stripe CLI fixture `stripe trigger checkout.session.completed`; admin routes SuperAdmin-only.

- [ ] Create `e2e/tests/billing.spec.ts` (asserts the Stripe URL only — never completes payment):
  ```typescript
  import { test, expect } from "@playwright/test";
  import { signIn, USERS } from "../helpers/auth";

  test("checkout redirects to a Stripe test checkout URL", async ({ page }) => {
    await signIn(page, USERS.unentitled); // no subscription -> sees plan CTAs
    await page.goto("/pricing");
    await page.getByRole("button", { name: /start|subscribe|choose pro|get pro/i }).first().click();
    await page.waitForURL(/https:\/\/checkout\.stripe\.com\/.+/, { timeout: 30_000 });
    expect(page.url()).toContain("checkout.stripe.com");
    // Stop here: do NOT fill card details or complete payment.
  });
  ```
  If the pricing CTA for a signed-in user routes through `/app/account` billing instead, follow WS3/WS4's actual flow (grep `createCheckout(`) and adjust the `goto` + button name once.
- [ ] Webhook simulation (manual companion step, requires the Task 7 `stripe listen` terminal still running): in another terminal run
  ```bash
  stripe trigger checkout.session.completed
  ```
  → expect `Trigger succeeded` from the CLI, and in the `stripe listen` terminal a line ending `[200] POST http://localhost:5000/api/webhooks/stripe` — proving signature verification + handler return 200 on a real signed event. (The fixture's customer will not match a seeded org; the handler must no-op unknown customers with 200, not 500 — if it 500s, fix the handler and add an xUnit regression test in `apps/api/tests/.../Features/`.)
- [ ] Create `e2e/tests/admin.spec.ts`:
  ```typescript
  import { test, expect } from "@playwright/test";
  import { signIn, USERS } from "../helpers/auth";

  test.describe("admin area", () => {
    test("SuperAdmin sees source health", async ({ page }) => {
      await signIn(page, USERS.superadmin);
      await page.goto("/app/admin/sources");
      await expect(page.getByText(/houston permitting center/i)).toBeVisible({ timeout: 15_000 });
      await expect(page.getByText(/healthy/i).first()).toBeVisible();
      await expect(page.getByText(/dallas development services/i)).toBeVisible();
    });

    test("member is redirected away from admin", async ({ page }) => {
      await signIn(page, USERS.entitled); // Role: Member
      await page.goto("/app/admin/sources");
      await expect(page).not.toHaveURL(/\/app\/admin/, { timeout: 15_000 });
      await expect(page.getByText(/houston permitting center/i)).toHaveCount(0);
    });
  });
  ```
- [ ] Full suite run: `set -a; source .env; set +a; pnpm --dir e2e exec playwright test` → expect ALL specs pass (`16 passed` if every spec above landed as written; record the actual count — it becomes the Task 18 expected value).
- [ ] Commit: `git add e2e && git commit -m "Add billing checkout and admin gating E2E specs"`.

## Task 15: Deploy — API Dockerfile and boot-time migrations

**Files:** `apps/api/Dockerfile` (create), `apps/api/.dockerignore` (create), `apps/api/Program.cs` (already has the `RUN_MIGRATIONS_ON_STARTUP` guard from Task 8 — verify only).
**Interfaces:** container listens on Railway's injected `$PORT` (default 8080); healthcheck path `/api/health`; migrations run on boot only when `RUN_MIGRATIONS_ON_STARTUP=true` (`MigrateAsync` — the runtime image has no `dotnet ef` tooling, so programmatic migration is the correct equivalent of `dotnet ef database update`).

- [ ] Confirm the main project path: `ls apps/api/*.csproj` → expect `apps/api/PermitTorch.Api.csproj` (if named differently, substitute that name in the Dockerfile below and in Task 1's gate).
- [ ] Create `apps/api/.dockerignore`:
  ```
  bin/
  obj/
  tests/
  ```
- [ ] Create `apps/api/Dockerfile` (built with repo root as context: `docker build -f apps/api/Dockerfile .`):
  ```dockerfile
  FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
  WORKDIR /src
  COPY apps/api/PermitTorch.Api.csproj apps/api/
  RUN dotnet restore apps/api/PermitTorch.Api.csproj
  COPY apps/api/ apps/api/
  RUN dotnet publish apps/api/PermitTorch.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

  FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
  WORKDIR /app
  COPY --from=build /app/publish .
  ENV DOTNET_EnableDiagnostics=0
  EXPOSE 8080
  # Railway injects PORT; bind to it (default 8080 for local docker runs)
  ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet PermitTorch.Api.dll"]
  ```
- [ ] Prove it locally end-to-end against the compose Postgres:
  ```bash
  docker build -f apps/api/Dockerfile -t permittorch-api .
  docker run --rm -p 8080:8080 \
    -e DATABASE_URL="postgresql://permittorch:permittorch@host.docker.internal:5432/permittorch" \
    -e RUN_MIGRATIONS_ON_STARTUP=true \
    -e CLERK_JWKS_URL="$CLERK_JWKS_URL" -e CLERK_ISSUER="$CLERK_ISSUER" \
    -e CLERK_SECRET_KEY="$CLERK_SECRET_KEY" -e STRIPE_SECRET_KEY="$STRIPE_SECRET_KEY" \
    -e STRIPE_WEBHOOK_SECRET="$STRIPE_WEBHOOK_SECRET" -e WEB_ORIGIN="http://localhost:3000" \
    permittorch-api &
  sleep 8 && curl -s http://localhost:8080/api/health
  ```
  → expect `{"status":"ok"}` and container logs showing migrations applied (or "already up to date"). Stop the container afterward.
- [ ] Commit: `git add apps/api/Dockerfile apps/api/.dockerignore && git commit -m "Add API Dockerfile with PORT binding and boot-time migration flag"`.

## Task 16: Deploy — web Dockerfile (Next standalone) and Railway project wiring

**Files:** `apps/web/Dockerfile` (create), `apps/web/.dockerignore` (create), `apps/web/next.config.ts` (modify: standalone output), plus HUMAN steps in the Railway dashboard.
**Interfaces:** Railway project topology — ONE project, three services: `postgres` (Railway plugin), `api` (Dockerfile `apps/api/Dockerfile`), `web` (Dockerfile `apps/web/Dockerfile`). DB reached over Railway private networking via `${{Postgres.DATABASE_URL}}`; `NEXT_PUBLIC_API_URL` is the API's PUBLIC domain (browser-originated calls — the private domain `api.railway.internal` is unreachable from browsers).

- [ ] Edit `apps/web/next.config.ts`: add to the config object
  ```typescript
  output: "standalone",
  outputFileTracingRoot: path.join(__dirname, "../../"),
  ```
  (add `import path from "node:path";` at top). The tracing root makes the standalone bundle monorepo-aware (`.next/standalone/apps/web/server.js`).
- [ ] Create `apps/web/.dockerignore`:
  ```
  node_modules/
  .next/
  __tests__/
  ```
- [ ] Create `apps/web/Dockerfile` (context = repo root; NEXT_PUBLIC_* are inlined at BUILD time, hence the ARGs — Railway exposes service variables as Docker build args automatically when the Dockerfile declares a matching `ARG`):
  ```dockerfile
  FROM node:22-alpine AS build
  RUN corepack enable
  WORKDIR /repo
  COPY pnpm-workspace.yaml pnpm-lock.yaml package.json ./
  COPY packages ./packages
  COPY apps/web/package.json ./apps/web/package.json
  RUN pnpm install --frozen-lockfile --filter ./apps/web...
  COPY apps/web ./apps/web

  ARG NEXT_PUBLIC_API_URL
  ARG NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY
  ARG NEXT_PUBLIC_POSTHOG_KEY
  ARG NEXT_PUBLIC_SENTRY_DSN
  ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL \
      NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY=$NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY \
      NEXT_PUBLIC_POSTHOG_KEY=$NEXT_PUBLIC_POSTHOG_KEY \
      NEXT_PUBLIC_SENTRY_DSN=$NEXT_PUBLIC_SENTRY_DSN \
      NEXT_TELEMETRY_DISABLED=1
  RUN pnpm --filter ./apps/web build

  FROM node:22-alpine AS runner
  WORKDIR /app
  ENV NODE_ENV=production HOSTNAME=0.0.0.0 NEXT_TELEMETRY_DISABLED=1
  COPY --from=build /repo/apps/web/.next/standalone ./
  COPY --from=build /repo/apps/web/.next/static ./apps/web/.next/static
  COPY --from=build /repo/apps/web/public ./apps/web/public
  EXPOSE 3000
  CMD ["sh", "-c", "PORT=${PORT:-3000} exec node apps/web/server.js"]
  ```
  If the workspace also contains a `packages/config` consumed by web's tsconfig, the `COPY packages ./packages` line already covers it. If `pnpm --filter ./apps/web` does not match (filter syntax needs the package NAME on some setups), read `apps/web/package.json` `"name"` and use `--filter <name>` in both RUN lines.
- [ ] Prove it locally:
  ```bash
  docker build -f apps/web/Dockerfile \
    --build-arg NEXT_PUBLIC_API_URL=http://localhost:8080 \
    --build-arg NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY="$NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY" \
    -t permittorch-web .
  docker run --rm -p 3100:3000 -e CLERK_SECRET_KEY="$CLERK_SECRET_KEY" permittorch-web &
  sleep 5 && curl -s -o /dev/null -w "%{http_code}\n" http://localhost:3100/
  ```
  → expect `200`. Stop the container.
- [ ] Commit: `git add apps/web/Dockerfile apps/web/.dockerignore apps/web/next.config.ts && git commit -m "Add standalone Next.js Dockerfile for monorepo web deployment"`.
- [ ] **HUMAN:** Push the repo to GitHub (create a private repo, `git remote add origin …`, `git push -u origin main`) — Railway deploys from GitHub.
- [ ] **HUMAN:** Create the Railway project. At https://railway.app → **New Project** → name `permittorch` → **Add PostgreSQL** (provisions the `Postgres` service with private networking).
- [ ] **HUMAN:** Add the API service: **New → GitHub Repo** → select the repo → open the service **Settings**: set **Service name** `api`; under Build set **Dockerfile Path** `apps/api/Dockerfile` (Root Directory stays `/` — the Dockerfile needs repo-root context); under **Networking** click **Generate Domain** (note it: `https://api-….up.railway.app`); under **Healthcheck** set path `/api/health`. In **Variables** add: `DATABASE_URL=${{Postgres.DATABASE_URL}}` (reference — resolves to the private-network URL), `RUN_MIGRATIONS_ON_STARTUP=true`, `CLERK_SECRET_KEY`, `CLERK_JWKS_URL`, `CLERK_ISSUER`, `STRIPE_SECRET_KEY`, `STRIPE_WEBHOOK_SECRET` (production value arrives in Task 17), `STRIPE_PRICE_STARTER`, `STRIPE_PRICE_PRO`, `STRIPE_PRICE_TERRITORY`, `RESEND_API_KEY`, `EMAIL_FROM`, `SENTRY_DSN`, `APIFY_TOKEN`, `APIFY_ACTOR_ID`, `CLERK_SUPERADMIN_USER_ID`, `CLERK_SUPERADMIN_EMAIL`, and `WEB_ORIGIN=<web public URL from the next step, come back to fill it>`. Deploy → expect healthcheck green.
- [ ] **HUMAN:** Add the web service: **New → GitHub Repo** (same repo) → **Service name** `web`; **Dockerfile Path** `apps/web/Dockerfile`; **Generate Domain** (`https://web-….up.railway.app`); Healthcheck path `/`. Variables: `NEXT_PUBLIC_API_URL=https://<api public domain>` (PUBLIC domain — the browser calls it; do not use `api.railway.internal`), `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY`, `CLERK_SECRET_KEY`, `NEXT_PUBLIC_POSTHOG_KEY`, `NEXT_PUBLIC_SENTRY_DSN`. (These are consumed at build time via the Dockerfile ARGs — Railway passes them automatically.) Deploy → expect `/` returns the homepage. Now go back and set the API service's `WEB_ORIGIN` to this web URL and redeploy the API (CORS + Stripe redirect base).
- [ ] **HUMAN:** Seed production data (one-time): install Railway CLI (`brew install railway`), `railway login`, `railway link` (pick the `permittorch` project + `api` service environment), then run the seeder against the prod DB from your machine: `railway run --service api bash -c 'dotnet run --project apps/api -- seed'` — OR simpler and dependency-free: temporarily set a variable `SEED_ON_BOOT` is NOT provided; instead run locally with the prod DB URL: copy `DATABASE_URL` from the Postgres service's **Connect** tab (PUBLIC URL variant), then locally `DATABASE_URL="<prod public url>" CLERK_SUPERADMIN_USER_ID=... dotnet run --project apps/api -- seed` → expect the Task 8 seed-complete line. Remove the URL from your shell history afterward.

### Vercel alternative (web only — documented, not the primary path)

If web hosting ever moves to Vercel: import the GitHub repo at https://vercel.com/new → **Root Directory** `apps/web` → framework preset Next.js (build command `next build`, install command `pnpm install` — Vercel detects the pnpm workspace from the repo-root lockfile) → set the same five web env vars (`NEXT_PUBLIC_API_URL`, `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY`, `CLERK_SECRET_KEY`, `NEXT_PUBLIC_POSTHOG_KEY`, `NEXT_PUBLIC_SENTRY_DSN`) in Project Settings → Environment Variables. Remove the `output: "standalone"` requirement is unnecessary — Vercel ignores it harmlessly. Update the API's `WEB_ORIGIN` to the Vercel domain. Nothing else changes.

## Task 17: Production config — Stripe webhook, env checklist, deployed smoke tests, custom domain

**Files:** none in repo (dashboard config + verification), except any bug fixes found by smoke tests (commit each individually).
**Interfaces:** production webhook endpoint `https://<api public domain>/api/webhooks/stripe`; env placement per master §9 (api vars on the Railway `api` service, web vars on the `web` service — verified below).

- [ ] **HUMAN:** Register the production Stripe webhook: Stripe dashboard (Test mode stays ON until real launch) → **Developers → Webhooks → Add endpoint** → URL `https://<api public domain>/api/webhooks/stripe` → select events: `checkout.session.completed`, `customer.subscription.created`, `customer.subscription.updated`, `customer.subscription.deleted`, `invoice.payment_failed` → **Add endpoint** → reveal the **Signing secret** (`whsec_…`) → set it as `STRIPE_WEBHOOK_SECRET` on the Railway `api` service (replacing the CLI value) → redeploy.
- [ ] Verify env placement matches master §9 exactly — audit both Railway services against this checklist (every row present, no web-only var on the API and vice versa):

  | Var | Railway service |
  | --- | --- |
  | `DATABASE_URL`, `APIFY_TOKEN`, `APIFY_ACTOR_ID`, `CLERK_SECRET_KEY`, `CLERK_JWKS_URL`, `CLERK_ISSUER`, `STRIPE_SECRET_KEY`, `STRIPE_WEBHOOK_SECRET`, `STRIPE_PRICE_STARTER`, `STRIPE_PRICE_PRO`, `STRIPE_PRICE_TERRITORY`, `RESEND_API_KEY`, `EMAIL_FROM`, `SENTRY_DSN`, `WEB_ORIGIN`, `RUN_MIGRATIONS_ON_STARTUP`, `CLERK_SUPERADMIN_USER_ID`, `CLERK_SUPERADMIN_EMAIL` | api |
  | `NEXT_PUBLIC_API_URL`, `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY`, `CLERK_SECRET_KEY`, `NEXT_PUBLIC_POSTHOG_KEY`, `NEXT_PUBLIC_SENTRY_DSN` | web |

  (`NEXT_PUBLIC_API_MOCK` is deliberately absent everywhere in production.)
- [ ] Smoke test the deployed system (substitute the real domains):
  ```bash
  curl -s https://<api>/api/health                       # → {"status":"ok"}
  curl -s https://<api>/api/markets | head -c 300        # → JSON array with houston-tx
  curl -s -o /dev/null -w "%{http_code}\n" https://<web>/            # → 200
  curl -s -o /dev/null -w "%{http_code}\n" https://<web>/pricing     # → 200
  curl -s https://<web>/sitemap.xml | head -5            # → <urlset ...> with absolute URLs
  curl -s -o /dev/null -w "%{http_code}\n" -H "Origin: https://<web>" https://<api>/api/leads  # → 401 (auth required, NOT a CORS failure)
  ```
- [ ] **HUMAN:** Browser smoke pass on the deployed web URL: sign up with a fresh real email → verify code arrives → land on `/app/leads` (locked state, no subscription) → `/pricing` → subscribe with Stripe test card `4242 4242 4242 4242` (any future expiry/CVC) → complete checkout → return to app → expect entitled leads visible within a few seconds (webhook processed; check Stripe dashboard → Webhooks → the endpoint shows a `200`). Then open Sentry (no new errors) and PostHog (events `signup`, `pricing_viewed`, `checkout_started` present).
- [ ] Custom-domain note (when ready — not blocking): Railway `web` service → Settings → Networking → **Custom Domain** `permittorch.com` + `www` (add the shown CNAME at the DNS provider); optionally `api.permittorch.com` on the `api` service, then update `NEXT_PUBLIC_API_URL` (rebuild web) and `WEB_ORIGIN`; in Clerk switch to a production instance for the domain (issuer becomes `https://clerk.permittorch.com` — update `CLERK_ISSUER`/`CLERK_JWKS_URL`/keys); update the Stripe webhook URL if the API domain changes.

## Task 18: Final verification gate and Tasks.md reconciliation

**Files:** `Tasks.md` (modify: checkboxes + one hosting-line correction), no other changes unless a check fails.
**Interfaces:** superpowers:verification-before-completion — every claim below is backed by a freshly run command and observed output; nothing is checked off without evidence.

- [ ] Run the complete verification battery, in order, all from a clean shell:
  ```bash
  cd "/Users/andynguyen/Desktop/Permit Torch"
  git status --short                                   # → empty (everything committed)
  docker compose up -d && docker compose ps            # → postgres (healthy)
  set -a; source .env; set +a
  dotnet run --project apps/api -- seed                # → Seed complete. markets=3 sources=4 permits=10 opportunities=10
  dotnet test apps/api/tests/PermitTorch.Api.Tests/PermitTorch.Api.Tests.csproj
                                                       # → Passed! - Failed: 0 (all WS1+WS2+WS5 suites)
  pnpm -r typecheck                                    # → exit 0, zero TS errors
  pnpm --dir apps/web exec vitest run                  # → Test Files N passed (marketing + app + analytics suites)
  pnpm --dir e2e exec playwright test                  # → all specs passed (count recorded in Task 14)
  ```
- [ ] Manual SEO checks (PRD §50) against the local web server (`pnpm --dir apps/web dev` running) on 3 pages — `/`, `/pricing`, `/locations/texas/houston`:
  ```bash
  for p in "" "pricing" "locations/texas/houston"; do
    echo "== /$p"
    curl -s "http://localhost:3000/$p" | grep -oE "<title>[^<]+</title>" | head -1
    curl -s "http://localhost:3000/$p" | grep -coE 'rel="canonical"'
    curl -s "http://localhost:3000/$p" | grep -coE 'property="og:(title|description)"'
  done
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:3000/sitemap.xml   # → 200
  curl -s http://localhost:3000/robots.txt | head -3                            # → User-agent + Sitemap lines
  ```
  → expect: each page a UNIQUE `<title>` (three different strings), canonical count `1`, og count `>= 2`; sitemap reachable and listing marketing + locations URLs.
- [ ] Update `Tasks.md` to reflect shipped scope — evidence-gated, edit these exact items:
  - Phase 1.1: check all six items. First reword the hosting line to match reality: `Provision Railway (API + PostgreSQL + web); wire env vars` — then check it.
  - Phase 1.2: check all eleven items (WS1 merged + gates green).
  - Phase 1.3: check homepage, pricing, how-it-works, Houston page, two additional market pages (dallas/austin seeded + pages verified above), SEO infrastructure, free lead magnet. For `Weekly lead digest to captured emails`: run `grep -rn "SampleLeadRequest" apps/api/Features/EmailDigests/ apps/api/Jobs/` — check the box only if the digest job actually mails captured sample-lead emails; otherwise leave unchecked (known scope gap, note it in the final report).
  - Phase 2.1, 2.2, 2.3: check all items (WS2/WS4 merged, E2E proved auth, entitlement, billing, saved leads, digest prefs, admin).
  - Phase 2.4: check `CSV export` (route exists per master §6 — verify `grep -rn "export.csv" apps/api/Features/Leads/` first) and `PostHog events` (Task 10). For `Free-account gating` run `grep -rn "trial\|locked" apps/web/app/app/leads/` and check only if the 3-visible/rest-locked behavior shipped; for `LLM classification fallback` run `grep -rn "llm\|openai\|anthropic\|Claude" -i apps/api/Domain/Classification/` and check only on evidence; for `Terms of service + privacy policy` run `ls apps/web/app/\(marketing\)/terms apps/web/app/\(marketing\)/privacy 2>/dev/null` and check only if the pages exist. Leave any unshipped item unchecked.
  - Phase 0 and Phase 3: untouched.
- [ ] Commit and push:
  ```bash
  git add Tasks.md
  git commit -m "Update task list to reflect shipped MVP scope"
  git push origin main
  ```
  → expect: push succeeds; Railway auto-deploys both services from the new main; re-run the two deployed health curls from Task 17 → both green. Done means: every command above ran in this session with the stated output — if any expectation failed, fix, commit, and re-run the battery before declaring WS5 complete.
