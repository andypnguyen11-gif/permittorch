# Pre-sprinkler building-permit leads (scraper + PermitTorch)

Date: 2026-10-15
Status: accepted 2026-10-15 with the owner's changes (Fremont classified from the record page, an 800 detail-fetch
safety stop, closed-out rows counted in coverage, CSV export defaults to standing Open)
Repos: `fire-permit-leads-scraper` (this repo) and `Permit Torch`. The same file is placed in both repos'
`docs/superpowers/specs/`. Each repo gets its own implementation plan.

## Goal

Produce automatically the list that was hand-built for the first Bay Area buyer on 2026-10-13
(`Lucky Builders - new homes, ADUs and additions (pre-sprinkler) 2026-10-13.xlsx`, kept outside both
repos because it holds contact details). The list covers building permits in **Fremont, Hayward, San Mateo
and Menlo Park** for **new single-family homes, ADUs and large additions** that meet three conditions:

- The permit is **filed or in plan check**.
- The record names **a person to call** (role, name, and a phone or email).
- **No fire sprinkler application** is on file at the address yet.

New homes are the sure rows, because California requires an NFPA 13D system in every new one- or two-family
dwelling. ADUs and additions are conditional.

The leads land in PermitTorch as a **separate lead type**, with their own scoring and a "No sprinkler permit
yet" label. Only admins (`UserRole.SuperAdmin`) can see them, and only in those four markets.

## Non-goals

- `permittorch-daily` and `includeResidential` stay as they are. The 154 Fremont and Menlo Park Fire
  Protection District fire leads that the 2026-10-12 pilot marked as seen also stay as they are.
- These rows never enter the fire-permit feed: not `permits`, not `fire_opportunities`, not digests, not
  public market stats, not subscriber CSV.
- Santa Clara, Redwood City (city portal), Atherton and East Palo Alto building permits are out.
- Outreach, pricing and any success fee are out.
- Subscriber access is out. Opening the lead type to subscribers is a later spec.
- "A sprinkler permit is pending next door" notes are out. The check is per address.
- There is no contact enrichment. Every contact comes from the permit record as published.

## Part A: scraper

### A1. A separate private actor in this repo

The fire actor stays untouched: its input schema, dataset schema, registry, classifier, `fire-permit-leads-state`
store and pay-per-event charging do not change. The new actor reuses the repo's ACA and EnerGov plumbing.

- **Actor:** `pre-sprinkler-building-leads`, private, no pay-per-event charging. Its config lives in
  `actors/pre-sprinkler/.actor/` (`actor.json`, input, dataset and output schemas). It uses the repo's
  Dockerfile through `dockerContextDir` (the plan confirms the exact `actor.json` keys against the Apify CLI
  before the first push) and starts with `node dist/presprinkler/main.js`.
- **Code:** `src/presprinkler/` holds the entry point, run, classifier, sprinkler check, output schema and
  registry loader.
- **Registry:** `src/presprinkler/registry.json` is a registry of its own with its own zod schema. It reuses
  the ACA and EnerGov sub-schemas from `src/types/source.ts` but not the fire superRefine, which forces
  `sourceIsFireSpecific`.
- **State:** a named store `pre-sprinkler-leads-state`, never `fire-permit-leads-state`.
- **Input:**
  - `cities`: any of the four.
  - `startDate` / `endDate` or `lookbackDays` (default 90), applied to the **filing date**.
  - `sprinklerLookbackDays` (default 180, see A5).
  - `onlyNewRecords` (default false, see A6).
  - `maxResults`.
  - No fire options.

### A2. Sources

Each source has an `id`, `jurisdictionKey`, portal, and a classifier rule set.

| Source id | Portal | Search | Candidates |
| --- | --- | --- | --- |
| `fremont-building-permits` | Accela agency `COF`, module `Building` | Untyped date search and CSV export. Row filter: Record Type `Building Permit`. | Project Name text (A3) |
| `menlo-park-building-permits` | Accela agency `MENLOPARK` (the city, not the fire district), module `Building` | Typed `Building/Permit/NA/NA`, CSV export | Project Name codes `NEW/SFR`, `NEW/ACC (ADU)`, `ADD/SFR` |
| `hayward-building-permits` | EnerGov tenant `HaywardCAProd` | Pinned building case types (below) | Case type |
| `san-mateo-building-permits` | EnerGov tenant `SanMateoCAProd` | Pinned building case types (below) | Case type and description |

EnerGov case types:

- **Hayward:** `Residential - New Single Family`, `2 Unit Dwelling - New Residential`,
  `Residential - Accessory Dwelling Unit (ADU)` and its pre-approved-plans variant,
  `Junior Accessory Dwelling Unit (JADU)`, `Residential - Room Addition w/Subs`.
- **San Mateo:** `Single Family (New) - New`, `Single Family (Addition) - Add`,
  `Single Family (Addition) - Add/Alter`. The Duplex new type is in if it has records in a year.

The type GUIDs come from the 2026-10-13 spike output and are re-read live when the registry entry is written.

Adapter changes, all opt-in per source so the fire sources behave exactly as today:

1. **EnerGov date field.** A new source option, `energovDateField: 'IssueDate' | 'ApplyDate'`, default
   `IssueDate`.
   - With `ApplyDate`, the body sets `PermitCriteria.ApplyDateFrom/To`, keeps `SortBy 'IssueDate'` and
     `PageSize 10` (the only variant the spike saw work), and the date canary checks `ApplyDate`.
   - Unissued filings have a null `IssueDate`, which today's canary would reject.
2. **EnerGov detail.** `GET /energov/permits/permit/{CaseId}`, once per candidate after classification.
   - It reads `Contacts[]` (`ContactTypeName`, first and last name, `GlobalEntityName`, `Phone`, `EmailTo`),
     `SquareFeet` and `ValuationValue`.
   - New module: `src/adapters/energov/permit-detail.ts`.
3. **ACA CapDetail.** A new module, `src/adapters/aca/cap-detail.ts`.
   - It searches by record number, follows the 302 to `CapDetail.aspx` (the spike's route; it also gives
     the record link), and parses these blocks:
     - Applicant (name, company, phone, email)
     - Owner (name; phone and email from "Billing Party information" where present)
     - Licensed Professional / Contractor (name, company, CSLB number, phone, email)
     - "Who is doing the work"
     - Menlo Park only: Type of Work, Valuation, the `ADU: Yes/No` field and square footage
   - **Fremont:** the page is opened for **every `Building Permit` row whose status is kept** (A4),
     because the CSV cannot classify Fremont. Its six tract-lot new homes at one Olive Ave address are filed
     as `Lunara Homes (Lot N) - NSFD & ADU Plot Plans`, and the CSV has no description. There is no
     project-name skip list; one may be added after the acceptance report, from the opened pages that
     turned out not to be a new home, ADU or addition.
   - **Menlo Park:** the page is opened only for candidates whose Project Name code survives
     classification, and only for kept statuses.
   - Budget: at most 2 requests per page.
   - Mailing addresses are never kept.

**Detail-fetch safety stop.** One run opens at most **800** record pages, ACA and EnerGov together.

- 800 is a safety stop for a four-city, 90-day run, not a target. The spike's 90 days had 478 Fremont
  `Building Permit` rows in kept statuses.
- When a run reaches 800, it **fails loudly and stops**. Rows are buffered and nothing is pushed to the
  dataset, so a capped run never delivers a partial list that could be mistaken for a complete one. The
  coverage report names the cap and how far each source got.

### A3. Classification

Classification is deterministic. Each row gets two values:

- `kind`: `new_home`, `adu`, or `addition`. Anything else is dropped.
- `sprinklerRequirement`: `required` for `new_home`, `conditional` for the other two.

Precedence is new home, then ADU, then addition. A new house with an ADU is a `new_home`, and the ADU is
recorded in `notes`.

- **Fremont:** classified from the **record page**, using its `Project Type` field and its Project
  Information text (the project name plus the description).
  - `Project Type: Accessory Dwelling Unit` → `adu`, unless the text also says new single-family (NSFD).
  - `Project Type: Single-Family Dwelling`, `Two-Family Dwelling` or similar dwelling types:
    - `new_home` when the text says `NSFD`, `new SFD`, "new single-family", "new single family dwelling",
      "new residence" or "new home".
    - Otherwise `addition` when the text says "addition".
    - Otherwise dropped (remodels and alterations).
  - Any other Project Type is dropped, and its value is counted in coverage (`droppedProjectTypes`), so
    the skip list can be built from real values later.
  - **Required to survive:** BLD2027-01845, -01849, -01850, -02040, -02041 and -02042 (six NSFD lots at
    one address). A unit test with their scrubbed page fixture pins this.
- **Menlo Park:**
  - `NEW/SFR` → `new_home`; `NEW/ACC (ADU)` → `adu`; `ADD/SFR` → `addition`. `ALT/SFR` is dropped.
  - A detail page showing `ADU: Yes` on a `NEW/SFR` record keeps it `new_home`, with the ADU noted.
- **Hayward:**
  - New Single Family and 2 Unit Dwelling → `new_home`.
  - ADU and JADU types → `adu`.
  - Room Addition → `addition`.
- **San Mateo:**
  - `Single Family (New)` is `adu` when the description mentions ADU, JADU, accessory dwelling or
    "detached" with "unit"; otherwise it is `new_home`. The spike found 9 of 11 such records were detached
    ADUs.
  - The Addition types → `addition`.
- **Large additions:** an `addition` with a known size under `minAdditionSqFt` (default 500) is dropped. An
  addition of unknown size is kept, with `sizeKnown: false`.

### A4. Statuses and dates

- The row's date is the **filing date**: the Accela CSV `Date`, or EnerGov `ApplyDate`.
- **Kept:** every filed, in-review, plan-check, resubmittal, waiting-on-applicant and fees-due status.
- **Dropped:** issued, finaled or closed, void, withdrawn, cancelled, denied and expired.
- **Closed-out count.** Rows dropped as issued, finaled or closed are never delivered, but coverage
  counts how many of them would otherwise qualify: right kind, a contact, and no sprinkler permit
  (`closedOutQualifying`, per source and per status). This tells the owner later whether they belong.
  - For Menlo Park, Hayward and San Mateo, those rows are classified from their code or type, then their
    record page is opened for contacts.
  - For Fremont, classifying an issued row would cost a page per row, so issued, finaled and closed
    Fremont rows are **preselected from Project Name**: `NSFD`, `SFD`, "single family", `ADU`, `JADU`,
    "accessory dwelling" or "addition". Only those pages are opened. The count is labelled as preselected.
  - These pages count toward the 800 stop.
- The city's own status word is delivered unchanged. Each source carries an explicit `keepStatuses` list,
  pinned from live data during implementation. An unknown status is held back and listed in the coverage
  report. This follows the fire adapter's fail-closed style.

### A5. Sprinkler check

For each city, the run reads that city's fire sprinkler permits **internally**. They are never emitted. It
uses the same registry entries as the fire actor, with these differences:

- **Residential types included.** `includeResidential` is in effect for this read only.
- **EnerGov sources use `ApplyDate`**, so pending applications count.
- **Window:** from `startDate - sprinklerLookbackDays` to today.

| Building source | Fire source checked against |
| --- | --- |
| `fremont-building-permits` | `fremont-fire-permits` (Fire AFES) |
| `menlo-park-building-permits` | `mpfd-fire-permits`, all 5 types |
| `hayward-building-permits` | `hayward-fire-permits` |
| `san-mateo-building-permits` | `san-mateo-fire-permits` |

**Address key:** `normalizeAddressKey(street)` + `|` + upper-case city.

- Unit numbers and the ZIP are stripped, because Fremont rows have no ZIP and San Mateo's are ZIP+4.
- For Menlo Park, the city is taken from the fire row's own grid address. The district also serves
  Atherton.

**Result on each row:**
`sprinklerCheck: { status: 'none_found' | 'found' | 'unavailable', matches: [{ permitNumber, sourceId, filedDate, status }], checkedFrom, checkedAt }`

- A row with `found` is **still emitted**, so PermitTorch can retire a lead it already holds. The admin list
  hides those rows by default (B4).
- If the city's fire read fails, every row for that city is marked `unavailable` and the run reports the
  source as failed. A failed read is never treated as an empty one.

### A6. Contacts and output

**Contacts:**
`contacts[]: { role, name, company, phone, email, licenseNumber }`

- `role` is one of `contractor`, `applicant`, `owner`, `architect`, `engineer`, or `other` (with the
  portal's label kept in `roleLabel`).
- Order: contractor, applicant, owner, then the rest.
- A contact counts only if it has a name or company **and** a phone or email.
- A row with no such contact is **dropped** and counted in coverage as `noContact`.
- `"Owner/builder"` with no details is not a contact.

**Dataset item** (`PreSprinklerLeadSchema`):

- Identity: `recordId` (`{sourceId}:{permitNumber}`), `sourceId`, `jurisdictionKey`, `permitNumber`.
- What the city filed: `permitTypeLabel` (the city's own), `kind`, `sprinklerRequirement`, `status`,
  `filedDate`, `description`, `projectName`.
- Where: `address { street, city, state, zip }`.
- Size and value: `squareFeet`, `sizeKnown`, `valuation`.
- `contacts[]` and `sprinklerCheck`.
- `notes[]`: short machine-written facts, such as "ADU on the same permit" or "replaces a cancelled filing".
- `recordUrl`: the CapDetail link, or the EnerGov `#/permit/{CaseId}` link.
- `portalUrl` and `scrapedAt`.

**No score.** Scoring belongs to PermitTorch (B3).

**Change detection:** a row's content hash covers `status`, `sprinklerCheck.status` and the contacts. A
weekly run with `onlyNewRecords: true` therefore re-sends a lead whose sprinkler status flipped. The
acceptance run uses `onlyNewRecords: false`.

**Coverage report** (`COVERAGE_REPORT`), per source:

- Counts: `candidates`, `kept`, `droppedByStatus`, `droppedNoContact`, `droppedSmallAddition`,
  `droppedNotAKind`, `detailFetches`.
- `droppedProjectTypes` (Fremont) and `closedOutQualifying` (A4).
- `unknownStatuses`.
- Per-city sprinkler-check outcome.

### A7. Tests and data hygiene

Tests use Vitest with recorded fixtures under `tests/fixtures/presprinkler/`, following the existing ACA and
EnerGov tests:

- the classifier's rules
- the status lists
- the address key
- the EnerGov `ApplyDate` body and canary
- the CapDetail parser
- the contact rule
- the sprinkler join
- an end-to-end run with fakes

A regression test confirms that a fire source with no `energovDateField` builds the same body as before.

**Every fixture is scrubbed before commit:**

- names become `Test Owner` / `Test Builder Inc`
- phones become `(555) 010-0xxx`
- emails use `example.com`
- CSLB numbers become `0000000`

Real contact data never enters either repo. The acceptance comparison runs from a scratch script outside the
repo.

## Part B: PermitTorch

### B1. Ingestion from a second task, in its own slice

- New slice: `apps/api/Features/PreSprinkler/`.
- A `PreSprinklerIngestionJob` polls a **second** Apify task, configured in `APIFY_PRESPRINKLER_TASK_ID`, on
  the same interval. If that variable is unset, the job is off.
- It reuses `ApifyClient` and its run selection (succeeded, not yet ingested, oldest first, failure
  retries). The locked `IPermitSourceProvider` and `RawPermitRecord` are **not** changed.
- `scraper_runs` gains a `feed` column (`Fire` = 0, the default; `PreSprinkler` = 1), so the admin runs page
  shows both feeds.
- **Defence:** the fire ingestion rejects any record whose source has `feed = PreSprinkler`, and the
  pre-sprinkler job rejects fire sources.

### B2. Data model (one migration)

**`sources`** gains a `feed` column. Four rows are seeded under the existing markets:

- `fremont-building-permits` → `fremont-ca`
- `hayward-building-permits` → `hayward-ca`
- `san-mateo-building-permits` → `san-mateo-ca`
- `menlo-park-building-permits` → `menlo-park-ca`

They go in `DevSeeder.SourceDefs` with `feed = PreSprinkler`. They are not added to
`source-permit-types.json`.

**New tables:**

| Table | Columns |
| --- | --- |
| `pre_sprinkler_leads` | `id`, `source_id`, `external_id` (unique per source), `permit_number`, `permit_type_label`, `kind`, `sprinkler_requirement`, `status`, `filed_on`, `description`, `project_name`, `street`, `city`, `zip`, `square_feet`, `size_known`, `valuation`, `record_url`, `portal_url`, `sprinkler_check_status`, `sprinkler_checked_at`, `sprinkler_matches` (jsonb), `notes` (jsonb), `lead_score`, `standing`, `first_seen_at`, `last_seen_at`, `last_run_id` |
| `pre_sprinkler_contacts` | `lead_id`, `role`, `role_label`, `name`, `company`, `phone`, `email`, `license_number` |
| `pre_sprinkler_signals` | `lead_id`, `signal_type`, `description`, `weight` |

**Upsert and contacts:**

- Upserts use `(source_id, external_id)`. A merge never overwrites a value with null, as the fire side does.
- The removals filter that runs at fire ingest (phone, email, name, record) is applied to these contacts too,
  so an opt-out holds across both feeds.

### B3. Scoring and label

There is a separate `PreSprinklerScoringEngine`. It is deterministic, its weights live in `appsettings.json`
under `PreSprinklerScoring`, and every point is a `pre_sprinkler_signals` row.

| Signal | Default weight | Rule |
| --- | --- | --- |
| `BASE` | 30 | always |
| `NEW_HOME` | +30 | `kind = new_home` |
| `ADU` / `ADDITION` | +10 | conditional kinds |
| `LARGE_SIZE` | +10 | ADU ≥ 1,000 sq ft, or addition ≥ 1,000 sq ft |
| `FRESH_FILING` | +10 | filed ≤ 30 days before scoring |
| `PLAN_CHECK` | +5 | status is in plan check |
| `LICENSED_BUILDER_NAMED` | +5 | a contractor contact carries a licence number |
| `PHONE_PRESENT` | +5 | some contact has a phone |

**Standing:**

- `Open` when `sprinkler_check_status = none_found`.
- `SprinklerFiled` when it is `found`. The score is kept, but the lead is hidden by default.
- `CheckUnavailable` when it is `unavailable`.

The score is recomputed at ingest, and once a day for freshness.

**Labels**, following "say what the record shows":

- `none_found` → "No sprinkler permit yet". The tooltip reads: "No fire sprinkler application found at this
  address in {city}'s fire permits filed since {checkedFrom}, checked {checkedAt}."
- `found` → "Sprinkler permit filed", followed by the permit number.
- `required` → "Sprinklers required (new home)". `conditional` → "Sprinklers depend on the city's rule".
- Reasons never contain a name.

### B4. Admin-only API and page

**Endpoints:** a new endpoint group, `/api/admin/pre-sprinkler`, under the `SuperAdmin` policy.

- `GET /`: list. Filters: market, kind, standing (default `Open`), filed from/to. Sort: score, then filed
  date.
- `GET /{id}`: detail, with contacts and signals.
- `GET /export.csv`: CSV in the column order of the 2026-10-13 sheet, with formula-injection escaping.
  It takes the same filters as the list and **defaults to standing `Open`**, so rows that already have a
  sprinkler permit stay out of the download unless asked for.

The group only returns rows whose market slug is in `PreSprinkler:MarketSlugs` (default: the four slugs). A
market outside that list is never served, even to an admin.

**Web:**

- A new page, `/app/admin/pre-sprinkler`, guarded by `requireSuperAdmin()`, with a sidebar link beside
  Sources, Runs and Removals.
- Table columns: kind badge, label, address, city record link, status, filed date, size, contacts, score.
- Rows sharing an address and the same first contact are grouped under one heading, as with the six lots at
  one Fremont address.

**What subscribers can't reach.** Nothing outside `/api/admin/pre-sprinkler` reads the new tables:

- `LeadQueries`, digests, market stats, saved leads, the subscriber CSV and `RescoringJob` keep reading only
  `fire_opportunities`.
- Tests prove this (B5).

### B5. Tests

Tests use xUnit with Testcontainers:

- Ingestion: upsert, null-safe merge, removals applied, feed mismatch rejected, unset task id means off.
- Every scoring signal.
- Standing transitions: `none_found` → `found`.
- Endpoint authorization:
  - Member, customer Admin and a paid subscriber of `fremont-ca` all get 403.
  - SuperAdmin gets 200.
  - A market outside the allowlist is empty.
- **Isolation:** seed one fire lead and one pre-sprinkler lead in `fremont-ca`. The subscriber feed, CSV,
  digest, sample digest and market stats contain only the fire lead.

The migration and model tests are updated. A Vitest test covers the page's grouping. A Playwright check in
`e2e/tests/admin.spec.ts` covers the page loading for an admin and redirecting for a member.

### B6. Deploy notes

`docs/deploy.md` gets a "Pre-sprinkler leads (admin only)" section covering:

- the private actor
- the task
- `APIFY_PRESPRINKLER_TASK_ID`
- the seed
- a SQL count check

The existing Bay Area fire section is left as it is. This spec does not decide the second fire task.

## Acceptance (the next build)

1. Run the actor once with:
   - the **800** detail-fetch stop. A run that hits it fails the acceptance run outright.
   - `startDate: 2026-07-14`, `endDate: 2026-10-12`
   - all four cities
   - `onlyNewRecords: false`
   - the sprinkler check as of the run date
2. A scratch script outside both repos compares the dataset with the 23 rows of the 2026-10-13 sheet, by
   permit number. It reports:
   - **The 7 new-home rows.** All 12 permits must appear: the six lots at one Fremont address are six permits
     on one row. The contacts given on the sheet must appear with the same name and phone or email. A
     permit absent from the delivered rows must have one of two reasons:
     - `sprinklerCheck` now finds a sprinkler application at that address; the report names the sprinkler
       permit.
     - Its status has moved to issued, finaled, closed, void, withdrawn or cancelled; the report gives the
       status. This is reported the same way as for the ADU and addition rows.
     Any other absence fails the acceptance run.
   - **The 12 ADU and 4 addition rows.** Each is matched, or missing with its reason: status moved past plan
     check, sprinkler permit since filed, no contact, or below the addition size floor.
   - **Rows not on the sheet**, listed for the owner to review.
   - **Status words that differ from the sheet** are reported but are not failures. Statuses have moved
     since 2026-10-13.
3. The owner reviews that report before PermitTorch ingestion, the schedule, or any weekly cadence is set up.

## Decisions (2026-10-15)

1. **Addition floor:** 500 sq ft. An addition of unknown size is kept.
2. **Issued, finaled and closed building permits** are not delivered. Coverage counts how many would
   otherwise qualify (A4), for a later decision.
3. **No schedule in this spec.** After the owner accepts the comparison report, the cadence is a weekly
   Friday run of the separate task. Until then nothing is scheduled and nothing is ingested into
   PermitTorch.
4. **Detail-fetch stop:** 800 per run, fail loudly when hit (A2).
