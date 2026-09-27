# PermitTorch — Task Breakdown

Derived from [Prd.md](Prd.md) (§80 phases, §81 build priority) and [Architecture.md](Architecture.md).
Ordering within a phase is roughly dependency order. Every PR includes tests (see [CLAUDE.md](CLAUDE.md)).

---

## Status (2026-09-27)

- Live in production: API `https://api-production-bab7.up.railway.app`, web `https://web-production-2b8db2.up.railway.app`. All 31 markets have real ingested data (`GET /api/markets/stats` reports `totalLast30Days > 0` for every market).
- Daily Apify ingestion (`permittorch-daily`) runs on schedule, `0 6 * * *` UTC, enabled; 90-day backfill complete.
- Test totals from this verification pass: API 461/461 passing (xUnit, `dotnet test`, Release, no-build); web 56 test files / 554 tests passing (Vitest); typecheck clean across all workspaces; E2E (Playwright) 36 passed, 20 skipped, 0 failed — skips are the Firebase-auth-dependent specs (signup, sign-in, leads, saved, entitlement, admin, billing), pending the Firebase console step below.
- Production API/web health, authorization boundaries (`/app/leads` → `/login`, `/api/leads` → 401), and SEO surface (unique titles, one canonical + ≥2 OG tags per page, sitemap, robots) all verified green.
- Remaining for the product owner before full go-live:
  - Enable Firebase **Email/Password** and **Google** sign-in providers in the Firebase console, and add the production web domain to Firebase's authorized domains — unblocks the 20 skipped E2E specs and real user sign-in.
  - Set `RESEND_API_KEY` in the API's Railway environment — the digest code (subscriber + sample-lead nurture) is implemented and tested but cannot send until this is set.
  - Set `SENTRY_DSN` (API + web) and `NEXT_PUBLIC_POSTHOG_KEY` (web) in Railway — both integrations are wired but inert without them.
  - Issue a dedicated Stripe **restricted** API key for the API (currently a broader test-mode secret key) and configure the customer portal; switch to live Stripe products/prices/webhook at go-live (`STRIPE_SECRET_KEY` in production is currently `sk_test_…`).
  - Rotate the Apify token (the one in use has been shared across local/CI verification).

---

## Phase 0 — Scraper (separate repo, in progress)

> Owned in the Apify scraper project — tracked here only as the dependency gate for Phase 1+.

- [x] Fire permit extraction working — 31 supported jurisdictions / 40 sources (Houston is unsupported: no record-level permit data; see Architecture.md §6.1 and `docs/superpowers/plans/2026-08-19-permittorch-mvp/scraper-source-registry.json`)
- [x] Actor deployed to Apify (`scrapelabmax/us-fire-permit-leads-scraper`, build 0.1.11)
- [x] Dedicated ingestion task `scrapelabmax/permittorch-daily` (id `xatpyth2FgbUydjLd`): all 31 cities, `onlyNewRecords`, 5,000 cap, explicit charge limit (2026-09-26)
- [x] Daily schedule `0 6 * * *` UTC enabled after the 90-day backfill (confirmed live via the Apify API, 2026-09-27)
- [x] Stable dataset output shape documented — Zod-validated `PermitLeadSchema`, README §4–5 (see Architecture.md §6.1)
- [x] Run metadata available — status/timestamps/counts from the Apify Run object; per-source quality from `COVERAGE_REPORT` in the run key-value store
- [x] `COVERAGE_REPORT` field-level shape captured from a real run (Architecture.md §6.1, `docs/superpowers/plans/2026-08-19-permittorch-mvp/scraper-sample.json`)

---

## Phase 1 — Foundation & Marketing Validation

**Goal: acquire first paying customers with minimal product.**

### 1.1 Repo & Infrastructure Setup

- [x] Initialize monorepo structure (`apps/web`, `apps/api`, `packages/types`, `docs/`)
- [x] Scaffold Next.js + TypeScript + Tailwind + shadcn/ui (`apps/web`)
- [x] Scaffold ASP.NET Core API with vertical-slice layout (`apps/api`)
- [x] Provision Railway (API + PostgreSQL + web); wire env vars
- [x] CI: build + test on every PR (xUnit, Vitest; Playwright on main flows)
- [x] Sentry wired into web and API

### 1.2 Data Pipeline (P0 — the product)

- [x] EF Core schema + migrations: `sources`, `permits`, `permit_participants`, `scraper_runs`
- [x] `IPermitSourceProvider` interface + `ApifyPermitProvider` implementation
- [x] Ingestion job (`IHostedService`): poll/webhook Apify runs, pull new records
- [x] Read `COVERAGE_REPORT` per run; drive per-source health from `sourceStats[]`/`failedDetails[]`, detect truncation (5,000-record cap)
- [x] Normalization: map source fields → canonical Permit model
- [x] Deduplication: `source_id + external_id` primary, fingerprint fallback
- [x] Classification engine: keyword rules → regex rules → (later) LLM fallback
- [x] Schema + logic: `fire_opportunities`, `lead_signals`
- [x] Deterministic scoring engine (0–100) with configurable weights
- [x] Ingestion run logging: run ID, counts, duplicates, failures, duration
- [x] Source health tracking (`HEALTHY/WARNING/STALE/FAILED/DISABLED`) + staleness detection

### 1.3 Marketing Site (P0)

- [x] Homepage (hero: "Find the permits worth chasing", how-it-works, CTAs)
- [x] `/pricing` (Starter $49 / Pro $129 / Territory $249)
- [x] `/how-it-works`
- [x] Market pages with real aggregate data for the 31 scraper-supported markets (`/locations/[state]/[city]`, generated only where data exists)
- [x] SEO infrastructure: unique metadata, canonical URLs, OpenGraph, sitemap.xml, robots.txt, structured data
- [x] Free lead magnet: email capture → 5–10 sample leads
- [x] Weekly lead digest to captured emails (Resend) — sending disabled until `RESEND_API_KEY` is set

---

## Phase 2 — SaaS MVP

**Goal: 10 paying users.**

### 2.1 Accounts & Billing (P0)

- [x] Firebase Auth integration: signup, login, logout, password reset, email verification (email/password + Google) — implemented; end-to-end run pending Firebase provider enablement
- [x] Firebase ID-token validation in API
- [x] Organization model: orgs, memberships, roles (member/admin)
- [x] Market entitlements: subscription plan → accessible markets, enforced in API queries
- [x] Stripe Billing: checkout, free trial, upgrade/downgrade, cancellation, customer portal — test mode; live keys at go-live
- [x] Stripe webhook handler with signature verification + failed payment handling
- [x] Rate limiting middleware on API endpoints

### 2.2 Lead Application (P0)

- [x] `GET /api/leads` with filtering (market, category, score band, age, status) + pagination
- [x] Lead dashboard `/app/leads`: header stats, lead table/cards, freshness indicator
- [x] Search: address, permit number, description, company, city (Postgres FTS/ILIKE)
- [x] Lead detail `/app/leads/[id]`: opportunity, permit, participants, signals, source link
- [x] Lead score explanation UI (signal-by-signal breakdown)
- [x] Saved leads: save/unsave, `/app/saved`, optional Contacted status
- [x] Email digest: daily/weekly preference, digest generation job, Resend templates — sending disabled until `RESEND_API_KEY` is set
- [x] Data freshness UI ("Updated N minutes ago", stale warnings)

### 2.3 Admin (P0)

- [x] Role-gated admin area
- [x] Source health dashboard: per-source status, last run, record counts
- [x] Scraper run inspection (including failures)
- [x] Enable/disable sources
- [x] User & subscription management views
- [x] Manual lead classification override

### 2.4 P1 — Strongly Desired

- [x] CSV export for Pro+ (filtered leads)
- [ ] Free-account gating: 3 full leads visible, rest locked, 7-day Pro trial CTA
- [x] PostHog events: signup, pricing view, lead opened/saved, search, filter, digest enabled, checkout started, subscription created — wired but inert until `NEXT_PUBLIC_POSTHOG_KEY` is set in production
- [ ] LLM classification fallback for low-confidence permits
- [x] Terms of service + privacy policy (public-records data disclaimers)

---

## Phase 3 — Growth (post-validation, do not start early)

- [ ] Additional markets (per market-health scoring, PRD §65–66)
- [ ] Classification tuning from user behavior
- [ ] Data-driven SEO reports (monthly market activity pages)
- [ ] Blog content (PRD §25 topics)
- [ ] Team accounts / multi-seat improvements
- [ ] Configurable admin alert thresholds
- [ ] Hangfire migration if job volume demands it
