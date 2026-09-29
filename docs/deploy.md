# Deploying PermitTorch

Production runs on **Railway**. There is one project, `permittorch`, with one environment, `production`, and three services. Secrets are only ever referenced **by name** in this document. Real values live in Railway service variables and in the owner's git-ignored `.env`.

| Service | Source | Public URL | Healthcheck |
| --- | --- | --- | --- |
| `api` | GitHub `andypnguyen11-gif/permittorch` @ `main`, Dockerfile `apps/api/Dockerfile` (repo-root context) | https://api.permittorch.com (Railway domain `api-production-bab7.up.railway.app` stays attached) | `GET /api/health` → `{"status":"ok"}` |
| `web` | same repo @ `main`, Dockerfile `apps/web/Dockerfile` (repo-root context) | https://permittorch.com (`www.permittorch.com` redirects here; Railway domain `web-production-2b8db2.up.railway.app` stays attached) | `GET /` → 200 |
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
- `WEB_ORIGIN=https://permittorch.com` (the API allows this one browser origin, so the app must be used on the apex)
- `API_PUBLIC_URL=https://api.permittorch.com`
- `EMAIL_FROM=leads@permittorch.dev` (placeholder until Resend is set up)
- `RAILWAY_DOCKERFILE_PATH=apps/api/Dockerfile`
- Secrets and IDs: `FIREBASE_PROJECT_ID`, `STRIPE_SECRET_KEY`, `STRIPE_WEBHOOK_SECRET`, `STRIPE_PRICE_STARTER`, `STRIPE_PRICE_PRO`, `STRIPE_PRICE_TERRITORY`, `EMAIL_UNSUBSCRIBE_SECRET`, `APIFY_TOKEN`, `APIFY_TASK_ID`.
- **Deliberately unset:**
  - `RESEND_API_KEY`: digests are skipped until it is set.
  - `SENTRY_DSN`: Sentry stays off until it is set.
  - `SEED_SAMPLE_DATA`, `SEED_E2E_IDENTITIES`, `SUPERADMIN_FIREBASE_UID`, `E2E_*`: sample data and test identities never belong in production. The seeder ignores the two flags in Production anyway and logs a `WARN` naming them.
  - `SEED_SUPERADMIN_FIREBASE_UID`, `SEED_SUPERADMIN_EMAIL`: set only while running the one-time operator seed below, then remove.

**web:**
- `NODE_ENV=production`
- `PORT=3000`
- `NEXT_PUBLIC_API_URL=https://api.permittorch.com` (the **public** API domain, because browsers call it)
- `RAILWAY_DOCKERFILE_PATH=apps/web/Dockerfile`
- Build-time: `NEXT_PUBLIC_FIREBASE_API_KEY`, `NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN`, `NEXT_PUBLIC_FIREBASE_PROJECT_ID`, `NEXT_PUBLIC_FIREBASE_APP_ID`
- Runtime (server): `FIREBASE_PROJECT_ID`, `FIREBASE_CLIENT_EMAIL`, `FIREBASE_PRIVATE_KEY` (single line with `\n` escapes), `AUTH_COOKIE_SIGNATURE_KEY_CURRENT`, `AUTH_COOKIE_SIGNATURE_KEY_PREVIOUS`
- **Not yet set:** `NEXT_PUBLIC_POSTHOG_KEY` and `NEXT_PUBLIC_SENTRY_DSN`. Analytics and Sentry stay off until they are set.
- **Must be absent:** `NEXT_PUBLIC_API_MOCK`.

**Setting a secret without echoing it.** `railway variable set NAME --stdin --service api` reads the value from stdin. Omit `--skip-deploys` to redeploy right away. Never paste secret values on the command line or into logs. `railway variable list --json/--kv` prints raw values, so do not share its output.

## Stripe webhook

- **Endpoint:** `we_1UK6Vb2ObeZMEPuNk4U6pVNV` (TEST mode, account `acct_1TuG0M2ObeZMEPuN`), URL `https://api.permittorch.com/api/webhooks/stripe`.
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

## Running the seeder (registry, plus an optional operator SuperAdmin)

The seeder is idempotent and gated per part:

- **Registry** (32 markets, 44 sources): always upserted.
- **Sample permits**: only with `SEED_SAMPLE_DATA=true`, outside Production, and while the database holds no real permits.
- **E2E identities** (E2E SuperAdmin from `SUPERADMIN_FIREBASE_UID`, the entitled/unentitled orgs and the seeded Pro subscription): only with `SEED_E2E_IDENTITIES=true` outside Production.
- **Operator SuperAdmin**: `SEED_SUPERADMIN_FIREBASE_UID` (+ `SEED_SUPERADMIN_EMAIL`), in any environment. It creates that one user (or promotes them if they already signed up) and nothing else: no subscription.

With `ASPNETCORE_ENVIRONMENT=Production` the sample and identity parts refuse even when their flags are set, and log `WARN: SEED_… =true is ignored in Production`. `APIFY_TOKEN` no longer affects seeding. `seed --refresh-samples` is refused in Production.

In production:

1. Run the registry seed **inside the Railway api service only**, so it sees the service's own variables (`ASPNETCORE_ENVIRONMENT=Production`). Never run it from a local shell with a sourced `.env` and a production `DATABASE_URL`: that shell's environment is Development, where the local opt-ins apply.
   ```bash
   railway ssh --service api -- sh -c 'cd /app && dotnet PermitTorch.Api.dll seed'
   # → Seed complete. markets=32 sources=44 permits=<n> opportunities=<n>
   ```
   The output must contain no `WARN: SEED_…` line. If it does, remove that variable from the service.
2. Optional, once the operator has signed up in the web app: make them SuperAdmin by setting `SEED_SUPERADMIN_FIREBASE_UID` (their Firebase uid) and `SEED_SUPERADMIN_EMAIL` on the api service with `--skip-deploys`, run the same command, then delete both variables.
3. Verify: `curl -s https://api.permittorch.com/api/markets` returns 32 markets. Check the database with `railway ssh --service Postgres -- sh -c 'psql -U "$PGUSER" -d "$PGDATABASE" -c "select count(*) from permits where external_id like \$\$seed-%\$\$"'`, which must return 0.

Seed markets and sources **before** the first boot with `Pipeline__Enabled=true`. Otherwise ingestion could consume an Apify run while the sources table is still empty.

## Reprocessing recorded runs

Use this when a pipeline change must be applied to records that are already stored, for example a new field or a new classification rule. Permits do not keep the raw scraper record, so the only way to refresh them is to ingest the recorded Apify runs again. Upserts are idempotent: no permit or lead is duplicated.

1. Confirm the datasets still exist. Apify keeps unnamed datasets for a limited time, so a run older than the retention period cannot be reprocessed.
2. Save the run history, then remove it so ingestion sees the runs as new:
   ```bash
   railway ssh --service Postgres -- sh -c 'psql -U "$PGUSER" -d "$PGDATABASE" -c "create table scraper_runs_before_reprocess as select * from scraper_runs" -c "delete from scraper_runs"'
   ```
3. Ingestion takes one run per pass, oldest first. To shorten the wait, set `Ingestion__IntervalMinutes=1` on `api`, and remove it again afterwards.
4. Watch progress: `select count(*) from scraper_runs` climbs back to the original count.
5. When it is done, compare the totals with the saved table, then drop the saved table.

Source freshness never moves backwards during this, because it is taken from the run time and only ever advances. A record that becomes a lead during reprocessing is dated from the run that found it, so it is not shown as new.

Two things look odd while reprocessing runs and settle by themselves. Source health and records-per-run show the values of whichever run was processed last, until the newest run is reached. Public 30-day market counts rise where new kinds of leads appear, which is a true count and not an error.

## Filling record links on stored permits

Each lead links to its own record on the government site. The link comes from the scraper (`source.recordUrl`, build 0.1.16 and later) and is stored per permit, never per source. The lead page words the link by what it opens:

| Stored kind | Link text | Opens |
|---|---|---|
| Page | View original record | The city's own page for the record |
| Rest or Data | View source data for this record | The raw data row for the record |
| No link | View source dataset | The dataset's home page |

A permit stored before build 0.1.16 has no link until the scraper delivers it again. Reprocessing recorded runs does not help here: the old datasets do not hold the link, and the app cannot build it.

To fill the links, scrape again over the wanted window:

1. Deploy the API first, so the new fields are read. Register any new source before the run (see the seeder section): records for a source the app does not know are dropped and counted as failures.
2. Start runs of the `permittorch-daily` task, split by state group, with these input overrides. Pass them as overrides on the run and never save them to the task, or the daily run stops being only-new.
   - `onlyNewRecords: false`.
   - `maxResults` above the expected total for the group. When the cap binds, sources share it equally and the rest of each source is cut. A full 90-day run over all markets returns about 17,100 leads.
   - `lookbackDays` (up to 365) only if leads older than 90 days need links.
   - `includeContactDetails: true`, so the run carries each party's phone and email where the record publishes them. The saved `permittorch-daily` task holds this setting for the daily run; a run started by hand must pass it too, or that run delivers none.

   Start the runs from the task, not from the actor: ingestion only reads runs of the task.
3. Let ingestion take every run that was started. A run marks the leads it delivers as seen, so a run that is never ingested hides those leads from the daily only-new feed.
4. Check the result:
   ```bash
   railway ssh --service Postgres -- sh -c 'psql -U "$PGUSER" -d "$PGDATABASE" -c "select s.jurisdiction, count(*) permits, count(p.record_url) with_link from permits p join sources s on s.id = p.source_id group by 1 order by 1"'
   ```
   Omaha and Tulsa stay at zero: their portals have no link that can be verified. A few Atlanta and Colorado Springs records on a temporary number also have none.

Permits are matched on the scraper's record id, so a second scrape updates the stored permit and never adds a second one. A stored link is kept when a later record arrives without one.

A party's phone, email and licence number arrive the same way and are stored on the participant. A run that carries none leaves the stored ones in place. They are replaced when the same party publishes new ones and dropped when the permit names a different party.

Every lead a run touches is scored again as it is ingested, from the permit's merged fields. A full rescoring pass is only needed for leads the run did not reach.

## Rescoring every lead after a scoring change

The daily rescoring pass only revisits leads with a date inside the last 91 days. After a deploy that changes scoring rules or weights, set `Rescoring__FullPassOnStartup=true` on `api`. The next start rescores every lead once. Remove the variable afterwards, or every restart repeats the full pass.

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
API=https://api.permittorch.com; WEB=https://permittorch.com
curl -s $API/api/health                                                    # {"status":"ok"}
curl -s $API/api/markets | python3 -c 'import json,sys; print(len(json.load(sys.stdin)))'   # 32
for p in / /pricing /locations /login /sitemap.xml; do curl -s -o /dev/null -w "$p %{http_code}\n" $WEB$p; done   # 200s
curl -s $WEB/sitemap.xml | grep -m3 '<loc>'                                # absolute https://permittorch.com/... URLs
curl -s -o /dev/null -w "%{http_code}\n" -H "Origin: $WEB" $API/api/leads  # 401 (+ Access-Control-Allow-Origin: $WEB)
curl -s -o /dev/null -w "%{http_code}\n" "$API/api/email/unsubscribe?token=bad"            # 400 HTML page
curl -s -o /dev/null -w "%{http_code}\n" -X POST -d '{}' $API/api/webhooks/stripe          # 400 (no signature)
curl -s -o /dev/null -w "%{http_code} %{redirect_url}\n" https://www.permittorch.com/pricing   # 308 https://permittorch.com/pricing
```

## Remaining human steps

1. **Firebase console** (https://console.firebase.google.com/project/permittorch-app/authentication):
   - Click **Get started**, then under **Sign-in method** enable **Email/Password** and **Google**.
   - Under **Settings → Authorized domains**, add `permittorch.com` and `www.permittorch.com`.
2. **Stripe key:** replace `STRIPE_SECRET_KEY` on `api` (currently a full `sk_test_…` key) with a restricted `rk_test_…` key. Also disable plan switching in the Billing Portal settings.
3. **Resend:** verify a sending domain, then set `RESEND_API_KEY` and a real `EMAIL_FROM` on `api`.
4. **Sentry and PostHog:** set `SENTRY_DSN` (api) and `NEXT_PUBLIC_SENTRY_DSN` + `NEXT_PUBLIC_POSTHOG_KEY` (web; a web rebuild follows automatically).
5. **Browser smoke pass:** sign up → `/app/leads` locked → `/pricing` → test card `4242 4242 4242 4242` → leads visible. Stripe → Webhooks → the endpoint should show a 200.
## Custom domain

`permittorch.com` is registered at Porkbun, which also hosts its DNS. Cut over on 2026-09-27.

| Hostname | Railway service | Role |
| --- | --- | --- |
| `permittorch.com` | `web` | The one public web address. Matches `SITE_URL`, so canonical links, the sitemap and Open Graph URLs use it. |
| `www.permittorch.com` | `web` | Redirects every path to the apex with a 308 (`lib/canonical-host.ts`, wired in `next.config.ts`). |
| `api.permittorch.com` | `api` | Public API address, used by browsers, email links and the Stripe webhook. |

- **One web host on purpose.** Session cookies are per hostname and `WEB_ORIGIN` holds a single allowed origin. Signed-in pages therefore work on the apex only. The Railway `web` domain still serves pages, but its browser calls to the API are refused.
- **Railway domains stay attached** as a fallback and for direct health checks.
- **DNS records** live at Porkbun: an ALIAS for the apex, CNAMEs for `www` and `api`, and one `_railway-verify` TXT record per hostname. Railway shows the required values under each service's Networking settings.
- **Mail:** the MX and SPF records belong to Porkbun email forwarding. When Resend is added, merge its include into the existing SPF record. A second SPF record breaks delivery.

## Alternative: web on Vercel

Import the repo at https://vercel.com/new with **Root Directory** `apps/web`. Vercel detects the pnpm workspace. Set the same web variables. Set `WEB_ORIGIN` on `api` to the Vercel domain and add that domain to Firebase authorized domains. `output: "standalone"` is harmless on Vercel.

## Making an account an admin

The admin pages, the removals page among them, need the role SuperAdmin. Roles are stored as numbers: 0 Member, 1 Admin, 2 SuperAdmin. The account must have signed in once, so that its row exists.

1. Look first. The email is the one Firebase has verified; an unverified account is stored under an address ending `@unknown.permittorch.invalid`.

   ```bash
   railway ssh --service Postgres -- sh -c 'psql -U "$PGUSER" -d "$PGDATABASE" -c "select u.email, u.role, o.name from app_users u join organizations o on o.id = u.organization_id order by u.email"'
   ```

2. Change one account, naming it by its email:

   ```bash
   railway ssh --service Postgres -- sh -c 'psql -U "$PGUSER" -d "$PGDATABASE" -c "update app_users set role = 2 where email = \$\$owner@example.com\$\$ returning email, role"'
   ```

   It must return one row. No row means the email is not the stored one.

3. The account signs out and in again. The sidebar then shows the admin links.
