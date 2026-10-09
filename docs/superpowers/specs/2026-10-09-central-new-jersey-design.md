# Central New Jersey market — design (2026-10-09)

Owner decisions are recorded in the conversation of 2026-10-09; this file is what the app does about them.

## The feed

The scraper sends one source, `nj-ucc-fire-permits`, for 66 towns in Middlesex, Somerset and Union
counties. It is the state's register of construction permits that carry the fire subcode. The state
publishes monthly, one to three months after the permit date, and never publishes the contractor,
owner, zip or which fire system the work is.

## Decisions

1. **One market** `central-new-jersey-nj`, named "Central New Jersey (Middlesex, Somerset & Union counties)"
   so it never reads as statewide. One source under it; the scraper source name is unchanged. It is
   not public yet: listed in `DevSeeder.NotYetPublicMarketSlugs` and the web app's
   `NOT_YET_PUBLIC_MARKET_SLUGS`, it gets no marketing page, sitemap entry or location listing, and
   it stays out of the registry JSON until it is marketed.
2. **Source settings** (new columns on `Source`, all defaulting to today's behaviour):
   - `PublishesContractor` (default true). When false, a permit with no contractor gets the new
     `ContractorStatus.NotPublished`: no `NO_CONTRACTOR_LISTED` points, the reason opens "This source
     does not publish the contractor.", the lead page shows "Not published by this source", and the
     CSV status column says `NOT_PUBLISHED`.
   - `PublishCadence` (`Daily` | `Monthly`, default Daily). A monthly source is described by the newest
     permit date it holds (`Source.LatestRecordDate`, kept by ingestion), never by its last run.
   - `RecencyFromFirstSeenSince` (go-live date, default null). Null means the permit date clock, as
     every source has today. When set, a permit first seen at or after it is timed from `FirstSeenAt`
     for both `PERMIT_RECENT` and `OLD_PERMIT`, and the reasons name that clock: "Appeared in the
     public data within the last 7 days" / "Appeared in the public data more than 90 days ago". A
     permit first seen before it (the backfill) stays on the permit date. The seeder never sets or
     clears it; it is set by hand after the backfill is in and checked.
3. **"Fire subcode" is fire work.** The fire-work reader did not know the phrase, so every NJ permit
   would have been hidden as "not fire work". It now counts as a fire term and is named
   "fire subcode work"; it never becomes an alarm or sprinkler label. The classifier already files
   `other_fire_protection` with no specific words as General Fire Protection.
4. **Freshness.** `Freshness.lastUpdatedAt` covers daily sources only. Monthly sources in view are
   listed separately as `{ marketName, dataThrough }` and shown as
   "Central New Jersey: data through Aug 7, 2026, published monthly". Lead detail's source card
   carries the cadence and the same date. Public market stats are unchanged: the market is not public.
5. **Rescoring** also revisits permits first seen inside the window, on sources with the first-seen
   clock only, so that clock ages out and every other source keeps today's window.
6. **No full rescoring pass** is needed: nothing changes for the existing sources.

The wording names the publisher generically ("the public data", "this source") rather than "the
state", so the settings can serve another source later without misdescribing it.

## Not in this change

Marketing pages and the static source registry (after real NJ data is in); the scraper backfill and
daily input; setting the go-live date.
