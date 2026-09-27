# PermitTorch E2E (Playwright)

End-to-end specs that drive the real stack: Next.js web app → ASP.NET Core API → seeded PostgreSQL.
The suite does **not** start servers; it expects them running and fails fast (global setup) when they are not.

## Run locally

```bash
# 1. Postgres (docker, :5432) + migrations + seed (31 markets, 40 sources, 10 sample leads)
docker compose up -d
set -a; source .env; set +a
dotnet run --project apps/api/PermitTorch.Api.csproj --no-launch-profile -- seed

# 2. API on :5050 (macOS AirPlay holds :5000)
dotnet run --project apps/api/PermitTorch.Api.csproj --no-launch-profile --urls http://localhost:5050

# 3. Web on :3000 with the API mock OFF (apps/web/.env.local: NEXT_PUBLIC_API_URL=http://localhost:5050,
#    no NEXT_PUBLIC_API_MOCK) — in another terminal
pnpm --dir apps/web dev -p 3000

# 4. Once per machine
pnpm --dir e2e exec playwright install chromium

# 5. Run (from the repo root)
pnpm e2e                                  # whole suite
pnpm e2e tests/marketing.spec.ts          # one file
```

`playwright.config.ts` loads the repo-root `.env` itself (values already exported in the shell win),
so the gates below see the same values as the API. Overrides: `E2E_BASE_URL` (default
`http://localhost:3000`), `E2E_API_URL` (default `NEXT_PUBLIC_API_URL`, then `http://localhost:5050`).

Stop the servers afterwards with `pkill -f "PermitTorch.Api|next dev|next start"`.

## What runs when

| Spec | Covers | Needs |
| --- | --- | --- |
| `marketing.spec.ts` | Home, pricing, how-it-works, locations, seeded Austin page (stats + freshness match the API), 404 for markets without data, category landers, blog, terms, privacy, sitemap.xml, robots.txt | stack only |
| `public-boundary.spec.ts` | Signed-out `/app/**` (incl. dotted paths) → `/login?redirect=`; every protected API route → 401 `{ error }`; forged token → 401; unsigned Stripe webhook → 400; sample-lead form against the real API + idempotent resubmit | stack only |
| `auth.spec.ts` | Sign-up (→ `/app/leads`; `?plan=` → account picker), wrong password, sign-in/sign-out, deep-link return | Firebase users |
| `leads.spec.ts` | Feed (7 Austin leads), category filter, search, empty state, lead detail score breakdown (score = clamp(Σ signal weights)) and source | Firebase users |
| `saved.spec.ts` | Save → `/app/saved` → mark contacted (persisted) → remove; re-runnable | Firebase users |
| `entitlement.spec.ts` | Unsubscribed user sees nothing; Austin-only user gets not-found for San Antonio lead `…000201` and never sees other markets | Firebase users |
| `admin.spec.ts` | SuperAdmin sees all 40 sources + admin pages; member has no admin nav and is redirected to `/app` | Firebase users |
| `billing.spec.ts` | Checkout picker posts `{ plan, marketSlugs }` and lands on `checkout.stripe.com` (never pays) | Firebase users + Stripe test key |

Specs that cannot run skip themselves with the missing variable names in the reason.

## Unlocking the skipped specs (one-time, human steps)

1. **Firebase console** → <https://console.firebase.google.com/project/permittorch-app/authentication> →
   "Get started" → Sign-in method → enable **Email/Password** (and Google for manual QA).
2. Put a strong `E2E_USER_PASSWORD` (8+ characters) in the root `.env`.
3. `pnpm e2e:create-users` — idempotent; prints three lines. Copy them into `.env`:
   `SUPERADMIN_FIREBASE_UID=…`, `E2E_ENTITLED_FIREBASE_UID=…`, `E2E_UNENTITLED_FIREBASE_UID=…`.
   (It stops with "Enable Email/Password in the Firebase console first" until step 1 is done.)
4. Re-seed so the API knows those users (SuperAdmin; Acme = Pro on austin-tx; NoPlan = no subscription):
   `set -a; source .env; set +a; dotnet run --project apps/api/PermitTorch.Api.csproj --no-launch-profile -- seed`
5. Billing only: set `STRIPE_SECRET_KEY` to a real **test-mode** restricted key (`rk_test_…`) and restart the API.

## Stripe webhook (manual companion check)

With `stripe listen --forward-to localhost:5050/api/webhooks/stripe` running (and its `whsec_…` in
`STRIPE_WEBHOOK_SECRET` for the API), run `stripe trigger checkout.session.completed` and expect
`[200] POST http://localhost:5050/api/webhooks/stripe` — the fixture's customer matches no org and must be a no-op 200.

## Notes

- Seeded scores come from the real scoring engine and decay as permits age (RescoringJob), so specs
  assert titles, ids, counts and signal rows — never exact scores. Values live in `helpers/seed.ts`.
- Sample-lead submissions use `delivered+e2e-…@resend.dev` (Resend's test inbox), and the anonymous
  endpoint allows 5 requests/minute per IP — rerunning that spec in quick succession can hit 429.
- Sign-up specs delete the Firebase accounts they create when the service-account env is present.
