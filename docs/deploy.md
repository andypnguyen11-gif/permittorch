# Deploying PermitTorch

Production runs on **Railway**. There is one project, `permittorch`, with one environment, `production`, and three services. Secrets are only ever referenced **by name** in this document. Real values live in Railway service variables and in the owner's git-ignored `.env`.

| Service | Source | Public URL | Healthcheck |
| --- | --- | --- | --- |
| `api` | GitHub `andypnguyen11-gif/permittorch` @ `main`, Dockerfile `apps/api/Dockerfile` (repo-root context) | https://api-production-bab7.up.railway.app | `GET /api/health` → `{"status":"ok"}` |
| `web` | same repo @ `main`, Dockerfile `apps/web/Dockerfile` (repo-root context) | https://web-production-2b8db2.up.railway.app | `GET /` → 200 |
| `Postgres` | Railway PostgreSQL template (volume `postgres-volume`) | private only (`postgres.railway.internal`) | Railway managed |

Service settings are applied through the Railway API or the dashboard. They are not stored as files, because Railway no longer accepts `railway.json` config-as-code:

- **Dockerfile path:** `apps/api/Dockerfile` or `apps/web/Dockerfile`. Root directory stays `/`, because both Dockerfiles need the repo root as build context.
- **Healthcheck:** as in the table above, with a 120 s timeout.
- **Restart policy:** `ON_FAILURE`, 5 retries.
- **Watch patterns (api):** `apps/api/**`, `global.json`.
- **Watch patterns (web):** `apps/web/**`, `packages/**`, `pnpm-lock.yaml`, `package.json`.

## How a deploy works

- **Auto-deploy:** a push to `main` that touches a service's watch patterns rebuilds that service.
- **API boot:** the API migrates the database on boot (`RUN_MIGRATIONS_ON_STARTUP=true`, EF `Migrate()`). It **refuses to start** in any of these cases:
  - `DATABASE_URL` is missing (only `Development` falls back to localhost);
  - any required setting is missing (`FIREBASE_PROJECT_ID`, `STRIPE_*`, `EMAIL_UNSUBSCRIBE_SECRET`, `API_PUBLIC_URL`).
  
  A failed boot fails the healthcheck, so the previous deployment keeps serving.
- **Web build:** the web image **prerenders marketing pages against the live API**. The homepage, `/locations` and the sitemap are ISR pages revalidated hourly, and `generateStaticParams` calls `GET /api/markets` + `GET /api/markets/stats`. The API must therefore be healthy before a web build. If a web build runs during an API outage, it fails, and the old web deployment stays up.
- **`NEXT_PUBLIC_*` values:** these are inlined **at build time**. The Dockerfile declares an `ARG` for each one, and Railway passes matching service variables as build args. Changing one requires a rebuild; Railway redeploys automatically when a variable is set.
- **Missing API URL:** a production build without `NEXT_PUBLIC_API_URL` fails in `next.config.ts`. The only exception is `NEXT_PUBLIC_API_MOCK=1`, which must never be set in production.
- **Containers:** both run as non-root users. The API runs as `app` (uid 1654) under `tini`; web runs as `node`. Each binds Railway's `PORT`: the API uses 8080 and web uses 3000, and both are set explicitly as variables.

## Variables (names only)

**api:**
- `ASPNETCORE_ENVIRONMENT=Production`
- `PORT=8080`
- `DATABASE_URL=${{Postgres.DATABASE_URL}}` (a reference to the private-network URL; the API accepts `postgresql://` URLs)
- `RUN_MIGRATIONS_ON_STARTUP=true`
- `Pipeline__Enabled=true`
- `ForwardedHeaders__ForwardLimit=2` (see below)
- `WEB_ORIGIN=https://web-production-2b8db2.up.railway.app`
- `API_PUBLIC_URL=https://api-production-bab7.up.railway.app`
- `EMAIL_FROM=leads@permittorch.dev` (placeholder until Resend is set up)
- `RAILWAY_DOCKERFILE_PATH=apps/api/Dockerfile`
- Secrets and IDs: `FIREBASE_PROJECT_ID`, `STRIPE_SECRET_KEY`, `STRIPE_WEBHOOK_SECRET`, `STRIPE_PRICE_STARTER`, `STRIPE_PRICE_PRO`, `STRIPE_PRICE_TERRITORY`, `EMAIL_UNSUBSCRIBE_SECRET`, `APIFY_TOKEN`, `APIFY_TASK_ID`.
- **Deliberately unset:**
  - `RESEND_API_KEY`: digests are skipped until it is set.
  - `SENTRY_DSN`: Sentry stays off until it is set.
  - `SUPERADMIN_FIREBASE_UID`, `E2E_*`: identities must never be seeded in production.

**web:**
- `NODE_ENV=production`
- `PORT=3000`
- `NEXT_PUBLIC_API_URL=https://api-production-bab7.up.railway.app` (the **public** API domain, because browsers call it)
- `RAILWAY_DOCKERFILE_PATH=apps/web/Dockerfile`
- Build-time: `NEXT_PUBLIC_FIREBASE_API_KEY`, `NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN`, `NEXT_PUBLIC_FIREBASE_PROJECT_ID`, `NEXT_PUBLIC_FIREBASE_APP_ID`
- Runtime (server): `FIREBASE_PROJECT_ID`, `FIREBASE_CLIENT_EMAIL`, `FIREBASE_PRIVATE_KEY` (single line with `\n` escapes), `AUTH_COOKIE_SIGNATURE_KEY_CURRENT`, `AUTH_COOKIE_SIGNATURE_KEY_PREVIOUS`
- **Not yet set:** `NEXT_PUBLIC_POSTHOG_KEY` and `NEXT_PUBLIC_SENTRY_DSN`. Analytics and Sentry stay off until they are set.
- **Must be absent:** `NEXT_PUBLIC_API_MOCK`.

**Setting a secret without echoing it.** `railway variable set NAME --stdin --service api` reads the value from stdin. Omit `--skip-deploys` to redeploy right away. Never paste secret values on the command line or into logs. `railway variable list --json/--kv` prints raw values, so do not share its output.

## Stripe webhook

- **Endpoint:** `we_1UK6Vb2ObeZMEPuNk4U6pVNV` (TEST mode, account `acct_1TuG0M2ObeZMEPuN`), URL `https://api-production-bab7.up.railway.app/api/webhooks/stripe`.
- **Events:** `checkout.session.completed`, `customer.subscription.updated`, `customer.subscription.deleted`. These are the three the API handles. It re-fetches the subscription on every subscription event.
- **Signing secret:** stored in `STRIPE_WEBHOOK_SECRET` on `api`.
- **Unsigned or wrongly signed requests** return 400.
- **At go-live (live mode):**
  1. Create the live products and prices with the same lookup keys.
  2. Create a live-mode endpoint with the same URL and events.
  3. Set the live `STRIPE_SECRET_KEY` (use a restricted `rk_live_…` key), `STRIPE_WEBHOOK_SECRET` and `STRIPE_PRICE_*`.

## Forwarded headers (rate limiting)

Observed 2026-09-27 with a temporary header-echo service behind Railway's edge:

- The edge **drops** any client-sent `X-Forwarded-For`.
- It forwards `X-Forwarded-For: <client ip>, <edge node ip>`, plus `X-Real-IP: <client ip>`.
- The socket peer is an internal `100.64.0.x` address.

With the default `ForwardLimit=1`, the API used the *edge node* IP, so every anonymous visitor shared a few rate-limit buckets. With `ForwardedHeaders__ForwardLimit=2`, verified in production, each real client gets its own bucket:

- 20 unsubscribe GETs succeeded and then returned 429, even with spoofed `X-Forwarded-For` values.
- A request from another IP (the web container) was unaffected.

Re-check this if a CDN or custom proxy is ever placed in front of Railway.

## Redeploying

```bash
railway service redeploy --service api -y        # same image, fresh container (e.g. after a variable change made with --skip-deploys)
git push origin main                             # rebuilds the services whose watch patterns changed
railway up --service web --detach                # emergency: build + deploy the local working tree (respects .gitignore; .dockerignore keeps .env out of the image)
railway deployment list --service api --json     # status: BUILDING → DEPLOYING → SUCCESS / FAILED
railway logs --service api --lines 200           # runtime logs (add --build for build logs)
```

Rollback: in the dashboard, open the service's **Deployments**, pick the previous successful deployment and choose **Redeploy**. Migrations are forward-only, so rolling back across a schema change needs a compensating migration.

## Running the seeder (markets and sources only)

The seeder is idempotent. It upserts the 31 registry markets and their 40 sources. It has **no environment guard yet**:

- If `APIFY_TOKEN` is empty, it inserts sample permits.
- If any `*_FIREBASE_UID` variable is set, it creates test identities, including a SuperAdmin and a free Pro subscription.

So, in production:

1. Confirm the `api` service has `APIFY_TOKEN` set and has **no** `SUPERADMIN_FIREBASE_UID`, `E2E_ENTITLED_FIREBASE_UID` or `E2E_UNENTITLED_FIREBASE_UID` variables. Check names only:
   ```bash
   railway variable list --service api --json | python3 -c 'import json,sys; d=json.load(sys.stdin); print(bool(d.get("APIFY_TOKEN")), [k for k in d if "FIREBASE_UID" in k or k.startswith("E2E_")])'
   ```
2. Run it **inside the api container**, so it only sees the service's own variables. Never run it from a local shell with a sourced `.env`:
   ```bash
   railway ssh --service api -- sh -c 'cd /app && dotnet PermitTorch.Api.dll seed'
   # → Seed complete. markets=31 sources=40 permits=0 opportunities=0
   ```
3. Verify: `curl -s https://api-production-bab7.up.railway.app/api/markets` returns 31 markets. Check the database with `railway ssh --service Postgres -- sh -c 'psql -U "$PGUSER" -d "$PGDATABASE" -c "select count(*) from permits where external_id like \$\$seed-%\$\$"'`, which must return 0.

Seed markets and sources **before** the first boot with `Pipeline__Enabled=true`. Otherwise ingestion could consume an Apify run while the sources table is still empty.

## Rotating secrets

Each `railway variable set` below redeploys the service unless you pass `--skip-deploys`.

| Secret | How |
| --- | --- |
| `STRIPE_WEBHOOK_SECRET` | Stripe Dashboard → Developers → Webhooks → endpoint `we_1UK6Vb2ObeZMEPuNk4U6pVNV` → **Roll secret**. Choose an overlap window, then set the new value with `--stdin` on `api`. |
| `STRIPE_SECRET_KEY` | Create a new restricted key (Checkout Sessions, Customers, Subscriptions, Billing Portal, Prices), set it on `api`, verify checkout, then revoke the old key. |
| `AUTH_COOKIE_SIGNATURE_KEY_*` | Set `PREVIOUS` to the current `CURRENT` value, then set `CURRENT` to a new `openssl rand -base64 48` (both on `web`). Existing sessions stay valid. Drop the old `PREVIOUS` after the session lifetime. |
| `FIREBASE_PRIVATE_KEY` / `FIREBASE_CLIENT_EMAIL` | GCP IAM → service account `firebase-adminsdk-fbsvc@permittorch-app.iam.gserviceaccount.com` → new key. Set it on `web` (one line, `\n`-escaped), confirm login, then delete the old key. |
| `EMAIL_UNSUBSCRIBE_SECRET` | Set a new random value on `api`. **This invalidates the unsubscribe links in already-sent emails**, so rotate only on compromise. |
| `APIFY_TOKEN` | Apify console → Settings → Integrations → new token. Set it on `api`, then revoke the old token. |
| `NEXT_PUBLIC_*` | Public by nature. Changing one rebuilds `web`. |

## Backups

The Railway volume backup schedules on `postgres-volume` are **DAILY** (6-day retention) and **WEEKLY** (27-day retention). A manual "post-deploy baseline" backup was taken at deploy. Restore from Railway → `Postgres` → **Backups**. Postgres is the system's only durable store; Firebase holds authentication only. Point-in-time recovery is disabled; it needs a bucket, and can be enabled with `railway postgres pitr enable` if it is ever needed.

## Smoke test (after every deploy)

```bash
API=https://api-production-bab7.up.railway.app; WEB=https://web-production-2b8db2.up.railway.app
curl -s $API/api/health                                                    # {"status":"ok"}
curl -s $API/api/markets | python3 -c 'import json,sys; print(len(json.load(sys.stdin)))'   # 31
for p in / /pricing /locations /login /sitemap.xml; do curl -s -o /dev/null -w "$p %{http_code}\n" $WEB$p; done   # 200s
curl -s $WEB/sitemap.xml | grep -m3 '<loc>'                                # absolute https://permittorch.com/... URLs
curl -s -o /dev/null -w "%{http_code}\n" -H "Origin: $WEB" $API/api/leads  # 401 (+ Access-Control-Allow-Origin: $WEB)
curl -s -o /dev/null -w "%{http_code}\n" "$API/api/email/unsubscribe?token=bad"            # 400 HTML page
curl -s -o /dev/null -w "%{http_code}\n" -X POST -d '{}' $API/api/webhooks/stripe          # 400 (no signature)
```

## Remaining human steps

1. **Firebase console** (https://console.firebase.google.com/project/permittorch-app/authentication):
   - Click **Get started**, then under **Sign-in method** enable **Email/Password** and **Google**.
   - Under **Settings → Authorized domains**, add `web-production-2b8db2.up.railway.app`.
2. **Stripe key:** replace `STRIPE_SECRET_KEY` on `api` (currently a full `sk_test_…` key) with a restricted `rk_test_…` key. Also disable plan switching in the Billing Portal settings.
3. **Resend:** verify a sending domain, then set `RESEND_API_KEY` and a real `EMAIL_FROM` on `api`.
4. **Sentry and PostHog:** set `SENTRY_DSN` (api) and `NEXT_PUBLIC_SENTRY_DSN` + `NEXT_PUBLIC_POSTHOG_KEY` (web; a web rebuild follows automatically).
5. **Browser smoke pass:** sign up → `/app/leads` locked → `/pricing` → test card `4242 4242 4242 4242` → leads visible. Stripe → Webhooks → the endpoint should show a 200.
6. **Custom domain** (when ready):
   - Railway `web` → Settings → Networking → Custom Domain `permittorch.com` + `www`, and add the CNAMEs shown.
   - Optionally add `api.permittorch.com` on `api`. Then update `NEXT_PUBLIC_API_URL`, `API_PUBLIC_URL`, `WEB_ORIGIN` and the Stripe endpoint URL, and add the domains to Firebase authorized domains.
   - `SITE_URL` (canonical, sitemap, OG) is already `https://permittorch.com`.

## Alternative: web on Vercel

Import the repo at https://vercel.com/new with **Root Directory** `apps/web`. Vercel detects the pnpm workspace. Set the same web variables. Set `WEB_ORIGIN` on `api` to the Vercel domain and add that domain to Firebase authorized domains. `output: "standalone"` is harmless on Vercel.
