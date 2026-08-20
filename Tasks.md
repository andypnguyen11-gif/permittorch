# PermitTorch — Task Breakdown

Derived from [Prd.md](Prd.md) (§80 phases, §81 build priority) and [Architecture.md](Architecture.md).
Ordering within a phase is roughly dependency order. Every PR includes tests (see [CLAUDE.md](CLAUDE.md)).

---

## Phase 0 — Scraper (separate repo, in progress)

> Owned in the Apify scraper project — tracked here only as the dependency gate for Phase 1+.

- [ ] Fire permit extraction working for first market (Houston)
- [ ] Actor deployed to Apify with scheduled runs
- [x] Stable dataset output shape documented — Zod-validated `PermitLeadSchema`, README §4–5 (see Architecture.md §6.1)
- [x] Run metadata available — status/timestamps/counts from the Apify Run object; per-source quality from `COVERAGE_REPORT` in the run key-value store
- [ ] `COVERAGE_REPORT` field-level shape documented in scraper README (requested on scraper side)

---

## Phase 1 — Foundation & Marketing Validation

**Goal: acquire first paying customers with minimal product.**

### 1.1 Repo & Infrastructure Setup

- [ ] Initialize monorepo structure (`apps/web`, `apps/api`, `packages/types`, `docs/`)
- [ ] Scaffold Next.js + TypeScript + Tailwind + shadcn/ui (`apps/web`)
- [ ] Scaffold ASP.NET Core API with vertical-slice layout (`apps/api`)
- [ ] Provision Railway (API + PostgreSQL) and Vercel (web); wire env vars
- [ ] CI: build + test on every PR (xUnit, Vitest; Playwright on main flows)
- [ ] Sentry wired into web and API

### 1.2 Data Pipeline (P0 — the product)

- [ ] EF Core schema + migrations: `sources`, `permits`, `permit_participants`, `scraper_runs`
- [ ] `IPermitSourceProvider` interface + `ApifyPermitProvider` implementation
- [ ] Ingestion job (`IHostedService`): poll/webhook Apify runs, pull new records
- [ ] Read `COVERAGE_REPORT` per run; drive per-source health from `sourceStats[]`/`failedDetails[]`, detect truncation (5,000-record cap)
- [ ] Normalization: map source fields → canonical Permit model
- [ ] Deduplication: `source_id + external_id` primary, fingerprint fallback
- [ ] Classification engine: keyword rules → regex rules → (later) LLM fallback
- [ ] Schema + logic: `fire_opportunities`, `lead_signals`
- [ ] Deterministic scoring engine (0–100) with configurable weights
- [ ] Ingestion run logging: run ID, counts, duplicates, failures, duration
- [ ] Source health tracking (`HEALTHY/WARNING/STALE/FAILED/DISABLED`) + staleness detection

### 1.3 Marketing Site (P0)

- [ ] Homepage (hero: "Find the permits worth chasing", how-it-works, CTAs)
- [ ] `/pricing` (Starter $49 / Pro $129 / Territory $249)
- [ ] `/how-it-works`
- [ ] Houston market page with real aggregate data (`/locations/texas/houston`)
- [ ] Two additional market pages (once sources are live)
- [ ] SEO infrastructure: unique metadata, canonical URLs, OpenGraph, sitemap.xml, robots.txt, structured data
- [ ] Free lead magnet: email capture → 5–10 sample leads
- [ ] Weekly lead digest to captured emails (Resend)

---

## Phase 2 — SaaS MVP

**Goal: 10 paying users.**

### 2.1 Accounts & Billing (P0)

- [ ] Clerk integration: signup, login, logout, password reset, email verification
- [ ] Clerk JWT validation middleware in API
- [ ] Organization model: orgs, memberships, roles (member/admin)
- [ ] Market entitlements: subscription plan → accessible markets, enforced in API queries
- [ ] Stripe Billing: checkout, free trial, upgrade/downgrade, cancellation, customer portal
- [ ] Stripe webhook handler with signature verification + failed payment handling
- [ ] Rate limiting middleware on API endpoints

### 2.2 Lead Application (P0)

- [ ] `GET /api/leads` with filtering (market, category, score band, age, status) + pagination
- [ ] Lead dashboard `/app/leads`: header stats, lead table/cards, freshness indicator
- [ ] Search: address, permit number, description, company, city (Postgres FTS/ILIKE)
- [ ] Lead detail `/app/leads/[id]`: opportunity, permit, participants, signals, source link
- [ ] Lead score explanation UI (signal-by-signal breakdown)
- [ ] Saved leads: save/unsave, `/app/saved`, optional Contacted status
- [ ] Email digest: daily/weekly preference, digest generation job, Resend templates
- [ ] Data freshness UI ("Updated N minutes ago", stale warnings)

### 2.3 Admin (P0)

- [ ] Role-gated admin area
- [ ] Source health dashboard: per-source status, last run, record counts
- [ ] Scraper run inspection (including failures)
- [ ] Enable/disable sources
- [ ] User & subscription management views
- [ ] Manual lead classification override

### 2.4 P1 — Strongly Desired

- [ ] CSV export for Pro+ (filtered leads)
- [ ] Free-account gating: 3 full leads visible, rest locked, 7-day Pro trial CTA
- [ ] PostHog events: signup, pricing view, lead opened/saved, search, filter, digest enabled, checkout started, subscription created
- [ ] LLM classification fallback for low-confidence permits
- [ ] Terms of service + privacy policy (public-records data disclaimers)

---

## Phase 3 — Growth (post-validation, do not start early)

- [ ] Additional markets (per market-health scoring, PRD §65–66)
- [ ] Classification tuning from user behavior
- [ ] Data-driven SEO reports (monthly market activity pages)
- [ ] Blog content (PRD §25 topics)
- [ ] Team accounts / multi-seat improvements
- [ ] Configurable admin alert thresholds
- [ ] Hangfire migration if job volume demands it
