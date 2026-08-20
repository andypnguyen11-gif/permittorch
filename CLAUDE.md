# CLAUDE.md — PermitTorch

Guidance for Claude Code when working in this repository.

## Project

PermitTorch is a lead-intelligence SaaS for fire-protection contractors. It ingests public permit data (via an Apify scraper in a **separate repo**), then normalizes, deduplicates, classifies, and scores it into explainable sales leads.

Read these before making significant changes:

- [Prd.md](Prd.md) — product requirements (source of truth for scope)
- [Architecture.md](Architecture.md) — system design, data pipeline, security model
- [Tasks.md](Tasks.md) — phased task breakdown

**Stack:** Next.js + TypeScript (web, Vercel) · ASP.NET Core / C# (API, Railway) · PostgreSQL (Railway) · Clerk (auth) · Stripe (billing) · Resend (email) · Tailwind + shadcn/ui.

## Git Rules

- **Never reference PR numbers or task IDs in commit messages.** Describe the change itself (e.g., `Add deduplication fingerprint fallback`, not `Complete task 3.2` or `PR #14: dedupe`).
- **Do not add Claude as a co-author or contributor.** No `Co-Authored-By` trailers or "Generated with Claude" lines in commits or PR descriptions.
- Commit messages: imperative mood, concise subject line, body only when the why isn't obvious.

## Testing — Required for Every PR

Every PR must include tests covering its change. No test-less PRs.

- **API (C#):** xUnit. Unit tests for domain logic (scoring, classification, deduplication are the highest-value targets — they are the product). Integration tests for API endpoints, including authorization.
- **Web (TypeScript):** Vitest for unit/component tests.
- **E2E:** Playwright for critical flows (signup → subscribe → view leads; filtering; saved leads).
- A bug fix must include a regression test that fails without the fix.

## Engineering Principles

Build with **scalability, maintainability, and security** in mind — in the specific ways below, not as slogans.

### Scalability

- Adding a new city/market must require only configuration and a new source — never application rework (PRD Goal 5).
- Keep the pipeline stages (normalize → dedupe → classify → score) independent and provider-agnostic.
- Do NOT introduce heavy infrastructure preemptively: no Redis, Elasticsearch, Kafka, microservices, or Kubernetes during MVP. PostgreSQL + indexes first; escalate only on measured need (Architecture.md §9).

### Maintainability

- **Modular monolith with vertical slices.** API features live in `Features/<FeatureName>/`, not generic Controllers/Services/Repositories folders.
- Keep domain logic in the API. The Next.js app renders and calls the API — it holds no business rules.
- All Apify-specific code stays behind `IPermitSourceProvider` in `Infrastructure/`. Raw scraper structures must never leak into domain code or the frontend.
- Lead scoring is deterministic and rule-based; weights live in configuration. Never route the primary score through an LLM.
- Every score must remain explainable — each point traces to a persisted `LeadSignal`.

### Security

- Authorization is always server-side. Market entitlements are enforced in API queries; never rely on hiding UI elements.
- EF Core parameterized queries only — never string-concatenated SQL.
- Verify Stripe webhook signatures before processing any event.
- Validate all user input (filters, search, pagination) at the API boundary.
- Secrets live in environment variables only — never committed, never logged.
- Admin endpoints require role-based checks.

## Product Guardrails

- Data freshness is a feature: never present stale data as current; surface "Updated N ago" honestly.
- No duplicate leads may reach users — dedupe on `source_id + external_id` with fingerprint fallback.
- SEO pages are only generated for markets with real data — no thin programmatic pages.
- Respect the MVP non-goals (PRD §5, §64): no CRM, no mobile app, no customer-facing API, no contact enrichment, no AI chatbot.
